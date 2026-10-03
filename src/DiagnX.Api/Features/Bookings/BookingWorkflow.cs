using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Payments;
using DiagnX.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Features.Bookings;

/// <summary>
/// The order state machine shared by the patient, partner and admin APIs:
/// payment_pending → confirmed → (enroute →) collected → processing → ready → completed, or → cancelled.
/// Every change writes a booking_status_events row (timeline timestamps) and notifies the patient.
/// </summary>
public sealed class BookingWorkflow(AppDbContext db, NotificationService notifications, IPaymentGateway gateway, SignedUrl signer)
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [BookingStatus.PaymentPending] = new[] { BookingStatus.Confirmed, BookingStatus.Cancelled },
        [BookingStatus.Confirmed] = new[] { BookingStatus.Enroute, BookingStatus.Collected, BookingStatus.Cancelled },
        [BookingStatus.Enroute] = new[] { BookingStatus.Collected, BookingStatus.Cancelled },
        [BookingStatus.Collected] = new[] { BookingStatus.Processing, BookingStatus.Ready },
        [BookingStatus.Processing] = new[] { BookingStatus.Ready },
        [BookingStatus.Ready] = new[] { BookingStatus.Completed },
        [BookingStatus.Completed] = Array.Empty<string>(),
        [BookingStatus.Cancelled] = Array.Empty<string>(),
    };

    public IQueryable<Booking> Query() => db.Bookings
        .Include(b => b.Phlebotomist).Include(b => b.Events).Include(b => b.Review).Include(b => b.Report)
        .AsSplitQuery();

    public void Transition(Booking b, string to, string actorType, string? note = null)
    {
        if (!Allowed.TryGetValue(b.Status, out var next) || !next.Contains(to))
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, $"This booking can't move from '{b.Status}' to '{to}'.");

        var now = DateTime.UtcNow;
        b.Status = to;
        b.UpdatedAt = now;
        if (to == BookingStatus.Collected) b.CollectedAt = now;
        if (to == BookingStatus.Ready) b.ReportReadyAt = now;
        if (to == BookingStatus.Completed) b.CompletedAt = now;
        var ev = new BookingStatusEvent { BookingId = b.Id, Status = to, Note = note, ActorType = actorType, CreatedAt = now };
        db.BookingStatusEvents.Add(ev);
        b.Events.Add(ev);
        NotifyPatient(b, to);
    }

    public void AddInitialEvent(Booking b, string actorType)
    {
        var ev = new BookingStatusEvent { BookingId = b.Id, Status = b.Status, ActorType = actorType, CreatedAt = b.CreatedAt };
        db.BookingStatusEvents.Add(ev);
        b.Events.Add(ev);
        if (b.Status == BookingStatus.Confirmed) OnConfirmed(b);
    }

    /// <summary>Patient + lab notifications when a booking becomes confirmed (COD at once, prepaid after payment).</summary>
    public void OnConfirmed(Booking b)
    {
        notifications.Add(UserTypes.Patient, b.PatientId, "booking", "Booking confirmed",
            $"Your {b.TestName} at {b.LabName} is confirmed for {FriendlyDate(b.ScheduledDate)}, {b.SlotLabel}.", b.Id);
        var partnerId = db.Labs.Where(l => l.Id == b.LabId).Select(l => l.PartnerId).FirstOrDefault();
        if (partnerId != null)
            notifications.Add(UserTypes.Partner, partnerId.Value, "order", "New booking",
                $"{b.BookingNumber}: {b.TestName} · {(b.Mode == CollectionModes.Home ? "Home collection" : "Walk-in")} · {FriendlyDate(b.ScheduledDate)}, {b.SlotLabel}", b.Id);
    }

    private void NotifyPatient(Booking b, string status)
    {
        (string Kind, string Title, string Body)? n = status switch
        {
            BookingStatus.Confirmed => null, // handled by OnConfirmed
            BookingStatus.Enroute => ("enroute", "Phlebotomist en route", $"{b.Phlebotomist?.Name ?? "Your phlebotomist"} is on the way to your address."),
            BookingStatus.Collected => ("collected", "Sample collected", "Your sample has been handed to the lab for processing."),
            BookingStatus.Processing => ("system", "Standardizing your report", "Running unit & range checks against the platform standard."),
            BookingStatus.Ready => ("ready", "Report ready", $"Your {b.TestName} report from {b.LabName} is ready to view."),
            BookingStatus.Cancelled => ("system", "Booking cancelled",
                $"Your {b.TestName} booking {b.BookingNumber} was cancelled." + (b.PaymentStatus == PaymentStatus.Refunded ? " Your refund has been initiated." : "")),
            _ => null,
        };
        if (n != null) notifications.Add(UserTypes.Patient, b.PatientId, n.Value.Kind, n.Value.Title, n.Value.Body, b.Id);
        if (status == BookingStatus.Confirmed) OnConfirmed(b);
    }

    /// <summary>Cancel + refund any captured payment.</summary>
    public async Task CancelAsync(Booking b, string actorType, string? reason)
    {
        if (b.Status is not (BookingStatus.PaymentPending or BookingStatus.Confirmed or BookingStatus.Enroute))
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "This booking can no longer be cancelled.");

        var paid = await db.Payments.Where(p => p.BookingId == b.Id && p.Status == "PAID").ToListAsync();
        foreach (var p in paid)
        {
            if (p.GatewayPaymentId != null && await gateway.RefundAsync(p.GatewayPaymentId, p.Amount))
            {
                p.Status = "REFUNDED";
                p.RefundedAt = DateTime.UtcNow;
            }
        }
        if (paid.Count > 0) b.PaymentStatus = PaymentStatus.Refunded;
        else if (b.PaymentStatus is PaymentStatus.Pending or PaymentStatus.PayOnCollection) b.PaymentStatus = PaymentStatus.NotRequired;

        b.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(300, reason.Trim().Length)];
        b.CancelledBy = actorType;
        Transition(b, BookingStatus.Cancelled, actorType, b.CancelReason);
    }

    // ---------------------------------------------------------------- DTO mapping

    public BookingDto ToDto(Booking b)
    {
        var canReview = b.Status is BookingStatus.Ready or BookingStatus.Completed && b.Review == null;
        var showOtp = b.Status is BookingStatus.Confirmed or BookingStatus.Enroute;
        return new BookingDto(
            b.Id, b.BookingNumber, b.TestId, b.TestName, b.LabId, b.LabName, b.Mode, b.AddressLabel, b.AddressText,
            IstClock.FormatDate(b.ScheduledDate), b.SlotLabel, b.Price, b.Mrp, b.Discount, b.PaymentMethod, b.PaymentStatus, b.Status,
            b.CreatedAt, b.PatientName, b.Phlebotomist?.Name, b.Phlebotomist == null ? null : FormatPhone(b.Phlebotomist.Phone),
            showOtp ? b.CollectionOtp : null, b.CancelReason,
            CanCancel: b.Status is BookingStatus.PaymentPending or BookingStatus.Confirmed,
            CanReview: canReview,
            HasReport: b.Report?.Status == ReportStatus.Published,
            b.Review == null ? null : new ReviewDto(b.Review.LabRating, b.Review.PhleboRating, b.Review.Comment, b.Review.CreatedAt),
            Timeline(b));
    }

    private static readonly (string Status, string Title, string HomeSub, string WalkInSub)[] Steps =
    {
        (BookingStatus.Confirmed, "Booking confirmed", "Your slot is reserved", "Your slot is reserved"),
        (BookingStatus.Enroute, "Phlebotomist en route", "On the way to your address", ""),
        (BookingStatus.Collected, "Sample collected", "Handed over to the lab", "Collected at the lab"),
        (BookingStatus.Processing, "Standardizing & processing", "Quality checks in progress", "Quality checks in progress"),
        (BookingStatus.Ready, "Report delivered", "Standardized report is ready", "Standardized report is ready"),
    };

    /// <summary>Same stages as the app's OrderTimeline component (walk-in skips "en route").</summary>
    public static IReadOnlyList<TimelineStepDto> Timeline(Booking b)
    {
        var home = b.Mode == CollectionModes.Home;
        var steps = Steps.Where(s => home || s.Status != BookingStatus.Enroute).ToList();
        var reached = b.Events.GroupBy(e => e.Status).ToDictionary(g => g.Key, g => g.Min(e => e.CreatedAt));
        var activeIndex = steps.FindIndex(s => s.Status == b.Status);
        if (b.Status == BookingStatus.Completed) activeIndex = steps.Count;

        return steps.Select((s, i) =>
        {
            string state;
            if (b.Status is BookingStatus.Cancelled or BookingStatus.PaymentPending) state = reached.ContainsKey(s.Status) ? "DONE" : "TODO";
            else state = i < activeIndex ? "DONE" : i == activeIndex ? (s.Status == BookingStatus.Ready ? "DONE" : "ACTIVE") : "TODO";
            return new TimelineStepDto(s.Status, s.Title, home ? s.HomeSub : s.WalkInSub, state,
                reached.TryGetValue(s.Status, out var at) ? at : null);
        }).ToList();
    }

    public ReportDto ToReportDto(Booking b)
    {
        var r = b.Report!;
        return new ReportDto(
            b.Id, r.ReportNumber, b.TestName, b.LabName, b.PatientName, b.PatientAge, b.PatientGender, b.CollectedAt,
            r.PublishedAt ?? r.UpdatedAt, r.PathologistName ?? "", r.PathologistRegNo, r.Remarks,
            r.Values.OrderBy(v => v.SortOrder).Select(v => new ReportParameterDto(v.Name, v.Result, v.Unit, v.ReferenceRange, v.Flag)).ToList(),
            r.HasFlags, r.AttachmentFileId == null ? null : signer.Create(r.AttachmentFileId.Value));
    }

    public static string FriendlyDate(DateOnly d)
    {
        var today = IstClock.Today;
        if (d == today) return "today";
        if (d == today.AddDays(1)) return "tomorrow";
        return d.ToString("d MMM yyyy");
    }

    public static string FormatPhone(string phone) => phone.Length == 10 ? $"+91 {phone[..5]} {phone[5..]}" : phone;
}
