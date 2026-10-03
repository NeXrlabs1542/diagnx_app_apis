using System.Security.Cryptography;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Catalog;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Features.Bookings;

/// <summary>Booking → payment → tracking → report → review, from the patient's side.</summary>
public sealed class PatientBookingService(
    AppDbContext db, BookingWorkflow workflow, CatalogService catalog, IPaymentGateway gateway, AppConfigService config)
{
    public async Task<CreateBookingResult> CreateAsync(Guid patientId, CreateBookingRequest req, string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await workflow.Query().FirstOrDefaultAsync(b => b.PatientId == patientId && b.IdempotencyKey == idempotencyKey);
            if (existing != null)
            {
                var pending = existing.Status == BookingStatus.PaymentPending
                    ? await db.Payments.Where(p => p.BookingId == existing.Id && p.Status == "CREATED").OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync()
                    : null;
                return new CreateBookingResult(workflow.ToDto(existing), pending == null ? null : ToIntent(pending, null), pending == null ? "NONE" : "PAY");
            }
        }

        var errors = new FieldErrors();
        var mode = req.Mode?.Trim().ToLowerInvariant();
        if (mode is not (CollectionModes.Home or CollectionModes.WalkIn)) errors.Add("mode", ErrorCodes.InvalidValue, "Choose home collection or walk-in");
        var method = req.PaymentMethod?.Trim().ToLowerInvariant();
        if (method == null || !PaymentMethods.All.Contains(method)) errors.Add("paymentMethod", ErrorCodes.InvalidValue, "Choose a payment method");
        if (!IstClock.TryParseDate(req.Date, out var date)) errors.Add("date", ErrorCodes.InvalidDate, "Choose a date");
        if (string.IsNullOrWhiteSpace(req.TestId)) errors.Add("testId", ErrorCodes.Required, "Choose a test");
        errors.ThrowIfAny();

        var window = await config.GetIntAsync(AppConfigService.BookingWindowDays);
        if (date < IstClock.Today || date > IstClock.Today.AddDays(window))
            throw ApiException.Field("date", ErrorCodes.InvalidDate, $"Choose a date within the next {window} days");

        var patient = await db.Patients.FindAsync(patientId) ?? throw ApiException.Unauthorized();
        var test = await catalog.FindTestAsync(req.TestId!);
        var lab = await db.Labs.Include(l => l.Operations).Include(l => l.ServiceablePincodes)
                      .FirstOrDefaultAsync(l => l.Id == req.LabId && l.IsActive)
                  ?? throw ApiException.Field("labId", ErrorCodes.LabUnavailable, "This lab is not available");
        if (!lab.AcceptingBookings) throw ApiException.Field("labId", ErrorCodes.LabUnavailable, "This lab is not taking bookings right now");
        var labTest = await db.LabTests.FirstOrDefaultAsync(lt => lt.LabId == lab.Id && lt.TestId == test.Id && lt.IsActive)
                      ?? throw ApiException.Field("testId", ErrorCodes.TestNotOffered, "This lab doesn't offer this test");

        // Who the sample is for
        var name = patient.Name ?? "";
        var age = patient.Age;
        var gender = patient.Gender;
        if (req.FamilyMemberId != null)
        {
            var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.Id == req.FamilyMemberId && m.PatientId == patientId && !m.IsDeleted)
                         ?? throw ApiException.Field("familyMemberId", ErrorCodes.InvalidValue, "Family member not found");
            (name, age, gender) = (member.Name, member.Age, member.Gender);
        }
        if (string.IsNullOrWhiteSpace(name))
            throw ApiException.Field("name", ErrorCodes.Required, "Please add your name in your profile before booking");

        // Where
        PatientAddress? address = null;
        if (mode == CollectionModes.Home)
        {
            if (lab.Operations?.HomeCollection != true)
                throw ApiException.Field("mode", ErrorCodes.NotServiceable, "This lab doesn't offer home collection");
            if (req.AddressId == null) throw ApiException.Field("addressId", ErrorCodes.Required, "Choose an address for home collection");
            address = await db.PatientAddresses.FirstOrDefaultAsync(a => a.Id == req.AddressId && a.PatientId == patientId && !a.IsDeleted)
                      ?? throw ApiException.Field("addressId", ErrorCodes.InvalidValue, "Address not found");
            if (!CatalogService.Serves(lab, address.Pincode))
                throw ApiException.Field("addressId", ErrorCodes.NotServiceable, $"{lab.DisplayName} doesn't collect samples at PIN code {address.Pincode} yet");
        }
        else if (!lab.WalkIn)
            throw ApiException.Field("mode", ErrorCodes.NotServiceable, "This lab doesn't accept walk-ins");

        // When
        var slot = await db.TimeSlots.FirstOrDefaultAsync(s => s.Id == req.SlotId && s.IsActive)
                   ?? throw ApiException.Field("slotId", ErrorCodes.SlotUnavailable, "Choose a time slot");
        var counts = await catalog.SlotCountsAsync(lab.Id, date);
        if (!CatalogService.IsBookableTime(date, slot.StartTime) || !CatalogService.WithinHours(lab, slot) ||
            counts.GetValueOrDefault(slot.Id) >= lab.SlotCapacity || (mode == CollectionModes.Home ? !slot.HomeCollection : !slot.WalkIn))
            throw ApiException.Field("slotId", ErrorCodes.SlotUnavailable, "That slot is no longer available. Please pick another.");

        var now = DateTime.UtcNow;
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingNumber = $"DX{await db.NextSequenceValue(AppDbContext.BookingNumberSeq)}",
            PatientId = patientId,
            FamilyMemberId = req.FamilyMemberId,
            PatientName = name,
            PatientAge = age,
            PatientGender = gender,
            LabId = lab.Id,
            LabName = lab.DisplayName,
            TestId = test.Id,
            TestName = test.Name,
            Mode = mode!,
            AddressId = address?.Id,
            AddressLabel = address?.Label,
            AddressText = address == null ? null : AddressText(address),
            AddressPincode = address?.Pincode,
            ScheduledDate = date,
            SlotId = slot.Id,
            SlotLabel = slot.Label,
            SlotStart = slot.StartTime,
            Mrp = labTest.Mrp,
            Price = labTest.Price,
            Discount = Math.Max(0, labTest.Mrp - labTest.Price),
            PaymentMethod = method!,
            PaymentStatus = method == PaymentMethods.Cod ? PaymentStatus.PayOnCollection : PaymentStatus.Pending,
            Status = method == PaymentMethods.Cod ? BookingStatus.Confirmed : BookingStatus.PaymentPending,
            CollectionOtp = RandomNumberGenerator.GetInt32(1000, 10000).ToString(),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()[..Math.Min(80, idempotencyKey.Trim().Length)],
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Bookings.Add(booking);
        workflow.AddInitialEvent(booking, UserTypes.Patient);

        Payment? payment = null;
        GatewayOrderResult? order = null;
        if (booking.Status == BookingStatus.PaymentPending) (payment, order) = await CreatePaymentAsync(booking);
        await db.SaveChangesAsync();

        return new CreateBookingResult(workflow.ToDto(booking), payment == null ? null : ToIntent(payment, order), payment == null ? "NONE" : "PAY");
    }

    private sealed record GatewayOrderResult(string? PublicKey);

    private async Task<(Payment, GatewayOrderResult)> CreatePaymentAsync(Booking booking)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(), BookingId = booking.Id, Method = booking.PaymentMethod, Amount = booking.Price,
            Gateway = gateway.Name, CreatedAt = DateTime.UtcNow,
        };
        var order = await gateway.CreateOrderAsync(payment.Id, payment.Amount, booking.BookingNumber);
        payment.GatewayOrderId = order.GatewayOrderId;
        db.Payments.Add(payment);
        return (payment, new GatewayOrderResult(order.PublicKey));
    }

    private static PaymentIntentDto ToIntent(Payment p, GatewayOrderResult? o) =>
        new(p.Id, p.Gateway, p.GatewayOrderId ?? "", p.Amount, "INR", o?.PublicKey);

    /// <summary>New payment attempt for a booking still waiting for payment (e.g. after a failure).</summary>
    public async Task<PaymentIntentDto> RetryPaymentAsync(Guid patientId, Guid bookingId)
    {
        var booking = await Owned(patientId, bookingId);
        if (booking.Status != BookingStatus.PaymentPending)
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "This booking doesn't need a payment.");
        var (payment, order) = await CreatePaymentAsync(booking);
        await db.SaveChangesAsync();
        return ToIntent(payment, order);
    }

    /// <summary>Confirm a payment with the gateway's callback values; confirms the booking on success.</summary>
    public async Task<BookingDto> VerifyPaymentAsync(Guid patientId, Guid paymentId, VerifyPaymentRequest req)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId) ?? throw ApiException.NotFound("Payment not found");
        var booking = await Owned(patientId, payment.BookingId);

        if (payment.Status == "PAID") return workflow.ToDto(booking); // idempotent
        if (booking.Status != BookingStatus.PaymentPending)
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "This booking is no longer waiting for payment.");

        var result = await gateway.VerifyAsync(payment.GatewayOrderId ?? "", req.GatewayPaymentId, req.GatewaySignature);
        if (!result.Success)
        {
            payment.Status = "FAILED";
            payment.FailureReason = result.FailureReason;
            await db.SaveChangesAsync();
            throw new ApiException(402, ErrorCodes.PaymentFailed, result.FailureReason ?? "Payment failed. Please try again.");
        }

        payment.Status = "PAID";
        payment.GatewayPaymentId = result.GatewayPaymentId;
        payment.PaidAt = DateTime.UtcNow;
        booking.PaymentStatus = PaymentStatus.Paid;
        workflow.Transition(booking, BookingStatus.Confirmed, UserTypes.Patient, "Payment received");
        await db.SaveChangesAsync();
        return workflow.ToDto(booking);
    }

    public async Task<List<BookingDto>> ListAsync(Guid patientId, string? tab)
    {
        var q = workflow.Query().AsNoTracking().Where(b => b.PatientId == patientId);
        if (tab == "active") q = q.Where(b => BookingStatus.Active.Contains(b.Status));
        else if (tab == "past") q = q.Where(b => BookingStatus.Past.Contains(b.Status));
        var list = await q.OrderByDescending(b => b.CreatedAt).Take(100).ToListAsync();
        return list.Select(workflow.ToDto).ToList();
    }

    public async Task<BookingDto> GetAsync(Guid patientId, Guid bookingId) => workflow.ToDto(await Owned(patientId, bookingId));

    public async Task<BookingDto> CancelAsync(Guid patientId, Guid bookingId, string? reason)
    {
        var booking = await Owned(patientId, bookingId);
        if (booking.Status is not (BookingStatus.PaymentPending or BookingStatus.Confirmed))
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "This booking can no longer be cancelled. Please contact support.");
        await workflow.CancelAsync(booking, UserTypes.Patient, reason);
        await db.SaveChangesAsync();
        return workflow.ToDto(booking);
    }

    public async Task<BookingDto> ReviewAsync(Guid patientId, Guid bookingId, ReviewRequest req)
    {
        var booking = await Owned(patientId, bookingId);
        if (booking.Review != null) throw ApiException.Conflict(ErrorCodes.AlreadyReviewed, "You've already rated this booking.");
        if (booking.Status is not (BookingStatus.Ready or BookingStatus.Completed))
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "You can rate this booking once your report is ready.");

        var errors = new FieldErrors();
        if (req.LabRating is < 1 or > 5) errors.Add("labRating", ErrorCodes.InvalidValue, "Rate the lab from 1 to 5 stars");
        if (booking.Mode == CollectionModes.Home && booking.PhlebotomistId != null && req.PhleboRating is null or < 1 or > 5)
            errors.Add("phleboRating", ErrorCodes.InvalidValue, "Rate the phlebotomist from 1 to 5 stars");
        if (req.PhleboRating is < 1 or > 5) errors.Add("phleboRating", ErrorCodes.InvalidValue, "Rate the phlebotomist from 1 to 5 stars");
        errors.MaxLength("comment", req.Comment, 500);
        errors.ThrowIfAny();

        var review = new Review
        {
            Id = Guid.NewGuid(), BookingId = booking.Id, PatientId = patientId, LabId = booking.LabId, PhlebotomistId = booking.PhlebotomistId,
            LabRating = (short)req.LabRating, PhleboRating = (short?)req.PhleboRating,
            Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim(), CreatedAt = DateTime.UtcNow,
        };
        db.Reviews.Add(review);
        booking.Review = review;

        var lab = await db.Labs.FirstAsync(l => l.Id == booking.LabId);
        lab.RatingAvg = Math.Round((lab.RatingAvg * lab.RatingCount + review.LabRating) / (lab.RatingCount + 1), 1);
        lab.RatingCount++;

        if (booking.Status == BookingStatus.Ready) workflow.Transition(booking, BookingStatus.Completed, UserTypes.Patient, "Rated by patient");
        await db.SaveChangesAsync();
        return workflow.ToDto(booking);
    }

    public async Task<List<ReportListItemDto>> ReportsAsync(Guid patientId)
    {
        var rows = await db.Bookings.AsNoTracking().Include(b => b.Report!).ThenInclude(r => r.Values)
            .Where(b => b.PatientId == patientId && b.Report != null && b.Report.Status == ReportStatus.Published)
            .OrderByDescending(b => b.Report!.PublishedAt).ToListAsync();
        return rows.Select(b => new ReportListItemDto(b.Id, b.Report!.ReportNumber, b.TestName, b.LabName, b.Report.PublishedAt!.Value,
            b.Report.HasFlags, b.Report.Values.Count(v => v.Flag != "normal"))).ToList();
    }

    public async Task<ReportDto> ReportAsync(Guid patientId, Guid bookingId)
    {
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Report!).ThenInclude(r => r.Values)
                          .FirstOrDefaultAsync(b => b.Id == bookingId && b.PatientId == patientId)
                      ?? throw ApiException.NotFound("Booking not found");
        if (booking.Report?.Status != ReportStatus.Published) throw ApiException.NotFound("Your report isn't ready yet.");
        return workflow.ToReportDto(booking);
    }

    private async Task<Booking> Owned(Guid patientId, Guid bookingId) =>
        await workflow.Query().FirstOrDefaultAsync(b => b.Id == bookingId && b.PatientId == patientId)
        ?? throw ApiException.NotFound("Booking not found");

    public static string AddressText(PatientAddress a) =>
        string.Join(", ", new[] { a.Line1, a.Line2, a.Landmark, a.City, a.Pincode }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
