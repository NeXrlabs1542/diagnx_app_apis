using System.Globalization;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Features.Catalog;
using DiagnX.Api.Features.Kyc;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Features.LabOps;

/// <summary>What an approved lab does day to day: prices, staff, orders, results.</summary>
public sealed class LabOpsService(
    AppDbContext db, BookingWorkflow workflow, CatalogService catalog, IFileStorage storage, SignedUrl signer, AppConfigService config)
{
    /// <summary>The partner's live lab — 403 KYC_NOT_APPROVED until the application is approved.</summary>
    public async Task<Lab> LabAsync(Guid partnerId)
    {
        var lab = await db.Labs.Include(l => l.Operations).Include(l => l.WorkingDays).Include(l => l.ServiceablePincodes)
            .Include(l => l.MedicalDirectors).AsSplitQuery()
            .FirstOrDefaultAsync(l => l.PartnerId == partnerId);
        if (lab is not { IsActive: true })
            throw new ApiException(403, ErrorCodes.KycNotApproved, "This unlocks once your KYC is approved.");
        return lab;
    }

    // ---------------------------------------------------------------- lab profile + settings

    public static LabProfileDto ToProfile(Lab l) => new(
        l.Id, l.DisplayName, l.LegalName, l.IsActive, l.AcceptingBookings, l.TurnaroundHours, l.WalkIn, l.Operations?.HomeCollection ?? false,
        l.SlotCapacity, l.IsoCertified, l.RatingAvg, l.RatingCount,
        string.Join(", ", new[] { l.AddressLine1, l.Area, l.City, l.Pincode }.Where(s => !string.IsNullOrWhiteSpace(s))),
        l.Operations == null ? null : IstClock.FormatTime(l.Operations.OpenTime),
        l.Operations == null ? null : IstClock.FormatTime(l.Operations.CloseTime),
        KycService.SortDays(l.WorkingDays.Select(d => d.DayCode)),
        l.ServiceablePincodes.Select(p => p.Pincode).OrderBy(p => p).ToList());

    public async Task<LabProfileDto> UpdateSettingsAsync(Guid partnerId, LabSettingsRequest r)
    {
        var lab = await LabAsync(partnerId);
        var e = new FieldErrors();
        if (r.TurnaroundHours is < 1 or > 240) e.Add("turnaroundHours", ErrorCodes.InvalidValue, "Turnaround must be 1–240 hours");
        if (r.SlotCapacity is < 1 or > 50) e.Add("slotCapacity", ErrorCodes.InvalidValue, "Bookings per slot must be 1–50");
        if (r.ServiceablePincodes != null && (r.ServiceablePincodes.Count > 30 || r.ServiceablePincodes.Any(p => !IndianIds.IsPincode(p))))
            e.Add("serviceablePincodes", ErrorCodes.InvalidPincode, "Up to 30 valid 6-digit PIN codes");
        var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        if (r.WorkingDays != null && (r.WorkingDays.Count == 0 || r.WorkingDays.Any(d => !days.Contains(d))))
            e.Add("workingDays", ErrorCodes.InvalidValue, "Select valid working days");
        TimeOnly open = default, close = default;
        if (r.OpenTime != null && !IstClock.TryParseTime(r.OpenTime, out open)) e.Add("openTime", ErrorCodes.InvalidHours, "Use HH:mm");
        if (r.CloseTime != null && !IstClock.TryParseTime(r.CloseTime, out close)) e.Add("closeTime", ErrorCodes.InvalidHours, "Use HH:mm");
        e.ThrowIfAny();

        if (r.AcceptingBookings != null) lab.AcceptingBookings = r.AcceptingBookings.Value;
        if (r.TurnaroundHours != null) lab.TurnaroundHours = (short)r.TurnaroundHours.Value;
        if (r.WalkIn != null) lab.WalkIn = r.WalkIn.Value;
        if (r.SlotCapacity != null) lab.SlotCapacity = (short)r.SlotCapacity.Value;
        if (r.IsoCertified != null) lab.IsoCertified = r.IsoCertified.Value;
        if (lab.Operations != null)
        {
            if (r.HomeCollection != null) lab.Operations.HomeCollection = r.HomeCollection.Value;
            if (r.OpenTime != null) lab.Operations.OpenTime = open;
            if (r.CloseTime != null) lab.Operations.CloseTime = close;
            if (lab.Operations.CloseTime <= lab.Operations.OpenTime)
                throw ApiException.Field("closeTime", ErrorCodes.InvalidHours, "Closing time must be after opening time");
        }
        if (r.WorkingDays != null) Sync(lab.WorkingDays, r.WorkingDays.Distinct().ToList(), d => d.DayCode, d => new LabWorkingDay { LabId = lab.Id, DayCode = d });
        if (r.ServiceablePincodes != null)
            Sync(lab.ServiceablePincodes, r.ServiceablePincodes.Distinct().ToList(), p => p.Pincode, p => new LabServiceablePincode { LabId = lab.Id, Pincode = p });
        lab.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToProfile(lab);
    }

    private void Sync<T>(List<T> current, IReadOnlyCollection<string> wanted, Func<T, string> key, Func<string, T> create) where T : class
    {
        foreach (var row in current.Where(r => !wanted.Contains(key(r))).ToList()) { current.Remove(row); db.Remove(row); }
        foreach (var k in wanted.Where(k => current.All(r => key(r) != k))) { var row = create(k); db.Add(row); current.Add(row); }
    }

    // ---------------------------------------------------------------- test menu + pricing

    public async Task<List<CatalogTestDto>> CatalogAsync(Guid partnerId, string? q, string? category, bool enrolledOnly)
    {
        var lab = await LabAsync(partnerId);
        var tests = await catalog.SearchTestsAsync(q, category, null, null, 200);
        var mine = await db.LabTests.AsNoTracking().Where(lt => lt.LabId == lab.Id).ToDictionaryAsync(lt => lt.TestId);
        return tests
            .Select(t => mine.TryGetValue(t.Id, out var lt)
                ? new CatalogTestDto(t, true, lt.IsActive, lt.Price, lt.Mrp)
                : new CatalogTestDto(t, false, false, null, null))
            .Where(x => !enrolledOnly || x.Enrolled).ToList();
    }

    public async Task<CatalogTestDto> SetPriceAsync(Guid partnerId, string testIdOrSlug, LabTestPriceRequest r)
    {
        var lab = await LabAsync(partnerId);
        var test = await catalog.FindTestAsync(testIdOrSlug);
        var e = new FieldErrors();
        if (r.Price <= 0 || r.Price > 500000) e.Add("price", ErrorCodes.InvalidValue, "Enter a valid price");
        if (r.Mrp < r.Price || r.Mrp > 500000) e.Add("mrp", ErrorCodes.InvalidValue, "MRP must be at least the selling price");
        e.ThrowIfAny();

        var lt = await db.LabTests.FirstOrDefaultAsync(x => x.LabId == lab.Id && x.TestId == test.Id);
        if (lt == null)
        {
            lt = new LabTest { LabId = lab.Id, TestId = test.Id };
            db.LabTests.Add(lt);
        }
        lt.Mrp = Math.Round(r.Mrp, 2);
        lt.Price = Math.Round(r.Price, 2);
        lt.IsActive = r.IsActive;
        lt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (await CatalogAsync(partnerId, null, null, true)).First(x => x.Test.Id == test.Id);
    }

    public async Task RemoveTestAsync(Guid partnerId, string testIdOrSlug)
    {
        var lab = await LabAsync(partnerId);
        var test = await catalog.FindTestAsync(testIdOrSlug);
        var lt = await db.LabTests.FirstOrDefaultAsync(x => x.LabId == lab.Id && x.TestId == test.Id) ?? throw ApiException.NotFound("Test not enrolled");
        db.LabTests.Remove(lt); // existing bookings keep their own price snapshot
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- phlebotomists

    public async Task<List<PhlebotomistDto>> PhlebotomistsAsync(Guid partnerId)
    {
        var lab = await LabAsync(partnerId);
        var open = new[] { BookingStatus.Confirmed, BookingStatus.Enroute };
        return await db.Phlebotomists.AsNoTracking().Where(p => p.LabId == lab.Id).OrderByDescending(p => p.IsActive).ThenBy(p => p.Name)
            .Select(p => new PhlebotomistDto(p.Id, p.Name, p.Phone, p.IsActive,
                db.Bookings.Count(b => b.PhlebotomistId == p.Id && open.Contains(b.Status))))
            .ToListAsync();
    }

    public async Task<PhlebotomistDto> SavePhlebotomistAsync(Guid partnerId, Guid? id, PhlebotomistRequest r)
    {
        var lab = await LabAsync(partnerId);
        var e = new FieldErrors();
        e.RequireText("name", r.Name, 2, 100, "Enter the phlebotomist's name");
        var phone = IndianIds.NormalisePhone(r.Phone);
        if (!IndianIds.IsMobile(phone)) e.Add("phone", ErrorCodes.InvalidPhone, "Enter a valid 10-digit mobile number");
        e.ThrowIfAny();

        Phlebotomist p;
        if (id == null)
        {
            p = new Phlebotomist { Id = Guid.NewGuid(), LabId = lab.Id, CreatedAt = DateTime.UtcNow };
            db.Phlebotomists.Add(p);
        }
        else p = await db.Phlebotomists.FirstOrDefaultAsync(x => x.Id == id && x.LabId == lab.Id) ?? throw ApiException.NotFound("Phlebotomist not found");
        p.Name = r.Name!.Trim();
        p.Phone = phone;
        p.IsActive = r.IsActive;
        await db.SaveChangesAsync();
        return new PhlebotomistDto(p.Id, p.Name, p.Phone, p.IsActive, 0);
    }

    // ---------------------------------------------------------------- orders

    public async Task<PagedResult<PartnerBookingDto>> BookingsAsync(Guid partnerId, string? status, string? date, int page, int pageSize)
    {
        var lab = await LabAsync(partnerId);
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        var q = workflow.Query().AsNoTracking().Include(b => b.Patient).Where(b => b.LabId == lab.Id && b.Status != BookingStatus.PaymentPending);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            q = q.Where(b => statuses.Contains(b.Status));
        }
        if (IstClock.TryParseDate(date, out var d)) q = q.Where(b => b.ScheduledDate == d);

        var total = await q.CountAsync();
        var rows = await q.OrderBy(b => b.ScheduledDate).ThenBy(b => b.SlotStart).ThenBy(b => b.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<PartnerBookingDto>(rows.Select(ToPartnerDto).ToList(), page, pageSize, total);
    }

    public async Task<PartnerBookingDto> BookingAsync(Guid partnerId, Guid bookingId) => ToPartnerDto(await LabBooking(partnerId, bookingId));

    public async Task<PartnerBookingDto> AssignAsync(Guid partnerId, Guid bookingId, Guid phlebotomistId)
    {
        var b = await LabBooking(partnerId, bookingId);
        if (b.Mode != CollectionModes.Home) throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "Walk-in bookings don't need a phlebotomist.");
        if (b.Status is not (BookingStatus.Confirmed or BookingStatus.Enroute))
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "A phlebotomist can only be assigned before collection.");
        var p = await db.Phlebotomists.FirstOrDefaultAsync(x => x.Id == phlebotomistId && x.LabId == b.LabId && x.IsActive)
                ?? throw ApiException.Field("phlebotomistId", ErrorCodes.InvalidValue, "Phlebotomist not found");
        b.PhlebotomistId = p.Id;
        b.Phlebotomist = p;
        b.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToPartnerDto(b);
    }

    public async Task<PartnerBookingDto> EnrouteAsync(Guid partnerId, Guid bookingId)
    {
        var b = await LabBooking(partnerId, bookingId);
        if (b.Mode != CollectionModes.Home) throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "Only home collections go en route.");
        if (b.PhlebotomistId == null) throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "Assign a phlebotomist first.");
        workflow.Transition(b, BookingStatus.Enroute, UserTypes.Partner);
        await db.SaveChangesAsync();
        return ToPartnerDto(b);
    }

    /// <summary>Sample collected — the patient's 4-digit collection OTP proves the right person was sampled.</summary>
    public async Task<PartnerBookingDto> CollectAsync(Guid partnerId, Guid bookingId, CollectRequest r)
    {
        var b = await LabBooking(partnerId, bookingId);
        if ((r.Otp ?? "").Trim() != b.CollectionOtp)
            throw ApiException.Field("otp", ErrorCodes.CollectionOtpInvalid, "Incorrect code. Ask the patient for the 4-digit code in their app.");
        if (b.Mode == CollectionModes.Home && b.PhlebotomistId == null)
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "Assign a phlebotomist first.");

        if (b.PaymentStatus == PaymentStatus.PayOnCollection && r.PaymentCollected)
        {
            b.PaymentStatus = PaymentStatus.Paid;
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(), BookingId = b.Id, Method = PaymentMethods.Cod, Amount = b.Price, Status = "PAID", Gateway = "CASH",
                PaidAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            });
        }
        workflow.Transition(b, BookingStatus.Collected, UserTypes.Partner);
        await db.SaveChangesAsync();
        return ToPartnerDto(b);
    }

    public async Task<PartnerBookingDto> ProcessingAsync(Guid partnerId, Guid bookingId)
    {
        var b = await LabBooking(partnerId, bookingId);
        workflow.Transition(b, BookingStatus.Processing, UserTypes.Partner);
        await db.SaveChangesAsync();
        return ToPartnerDto(b);
    }

    public async Task<PartnerBookingDto> CancelAsync(Guid partnerId, Guid bookingId, string? reason)
    {
        var b = await LabBooking(partnerId, bookingId);
        if (string.IsNullOrWhiteSpace(reason)) throw ApiException.Field("reason", ErrorCodes.Required, "Tell the patient why the booking is cancelled");
        await workflow.CancelAsync(b, UserTypes.Partner, reason);
        await db.SaveChangesAsync();
        return ToPartnerDto(b);
    }

    // ---------------------------------------------------------------- results / standardized report

    public async Task<ReportEditorDto> ReportEditorAsync(Guid partnerId, Guid bookingId)
    {
        var b = await LabBooking(partnerId, bookingId);
        var template = await Template(b.TestId);
        var report = await db.Reports.AsNoTracking().Include(r => r.Values).FirstOrDefaultAsync(r => r.BookingId == b.Id);
        var director = await db.LabMedicalDirectors.AsNoTracking().Where(d => d.LabId == b.LabId).FirstOrDefaultAsync();
        return new ReportEditorDto(
            b.Id, report?.ReportNumber, report?.Status ?? "NOT_STARTED",
            report?.PathologistName ?? director?.Name, report?.PathologistRegNo ?? director?.RegistrationNumber, report?.Remarks,
            template.Select(p =>
            {
                var v = report?.Values.FirstOrDefault(x => x.ParameterId == p.Id);
                return new ReportEditorParameter(p.Id, p.Name, p.Unit, p.ReferenceRange, p.ValueType, v?.Result, v?.Flag);
            }).ToList(),
            report?.AttachmentFileId == null ? null : signer.Create(report.AttachmentFileId.Value));
    }

    /// <summary>Save (draft) results. Values are mapped onto the catalog template; numeric flags are computed here.</summary>
    public async Task<ReportEditorDto> SaveReportAsync(Guid partnerId, Guid bookingId, SaveReportRequest r)
    {
        var b = await LabBooking(partnerId, bookingId);
        if (b.Status is not (BookingStatus.Collected or BookingStatus.Processing))
            throw ApiException.Unprocessable(ErrorCodes.InvalidTransition, "Results can be entered after the sample is collected.");
        var report = await EnsureReport(b);
        if (report.Status == ReportStatus.Published) throw ApiException.Conflict(ErrorCodes.InvalidState, "This report is already published.");

        var template = await Template(b.TestId);
        var e = new FieldErrors();
        e.MaxLength("pathologistName", r.PathologistName, 100);
        e.MaxLength("pathologistRegNo", r.PathologistRegNo, 40);
        e.MaxLength("remarks", r.Remarks, 1000);
        var inputs = r.Values ?? new List<ReportValueInput>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var param = template.FirstOrDefault(p => p.Id == input.ParameterId);
            if (param == null) { e.Add($"values[{i}].parameterId", ErrorCodes.InvalidValue, "Unknown parameter for this test"); continue; }
            var result = input.Result?.Trim() ?? "";
            if (result.Length > 60) e.Add($"values[{i}].result", ErrorCodes.TooLong, "Keep results under 60 characters");
            if (param.ValueType == "numeric" && result.Length > 0 && !TryNumber(result, out _))
                e.Add($"values[{i}].result", ErrorCodes.InvalidValue, $"{param.Name} must be a number");
            if (input.Flag != null && input.Flag is not ("low" or "high" or "normal"))
                e.Add($"values[{i}].flag", ErrorCodes.InvalidValue, "Flag must be low, high or normal");
        }
        e.ThrowIfAny();

        if (r.PathologistName != null) report.PathologistName = r.PathologistName.Trim();
        if (r.PathologistRegNo != null) report.PathologistRegNo = r.PathologistRegNo.Trim();
        if (r.Remarks != null) report.Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim();

        foreach (var input in inputs)
        {
            var param = template.First(p => p.Id == input.ParameterId);
            var result = input.Result?.Trim() ?? "";
            var value = report.Values.FirstOrDefault(v => v.ParameterId == param.Id);
            if (result.Length == 0)
            {
                if (value != null) { report.Values.Remove(value); db.ReportValues.Remove(value); }
                continue;
            }
            if (value == null)
            {
                value = new ReportValue { Id = Guid.NewGuid(), ReportId = report.Id, ParameterId = param.Id };
                db.ReportValues.Add(value);
                report.Values.Add(value);
            }
            value.Name = param.Name;
            value.Unit = param.Unit;
            value.ReferenceRange = param.ReferenceRange;
            value.SortOrder = param.SortOrder;
            value.Result = result;
            value.Flag = param.ValueType == "numeric" ? ComputeFlag(result, param.RefLow, param.RefHigh) : input.Flag ?? "normal";
        }
        report.HasFlags = report.Values.Any(v => v.Flag != "normal");
        report.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ReportEditorAsync(partnerId, bookingId);
    }

    public async Task<ReportEditorDto> AttachLabPdfAsync(Guid partnerId, Guid bookingId, IFormFile? file)
    {
        var b = await LabBooking(partnerId, bookingId);
        if (file == null || file.Length == 0) throw ApiException.Field("file", ErrorCodes.Required, "Choose the PDF report");
        var limit = await config.GetIntAsync(AppConfigService.MaxReportAttachmentBytes);
        if (file.Length > limit)
            throw new ApiException(413, ErrorCodes.DocTooLarge, $"File is {file.Length / 1048576.0:0.0} MB - please keep it under {limit / 1048576.0:0.0} MB");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        if (FileSniffer.Detect(ms.ToArray()) != "application/pdf")
            throw new ApiException(415, ErrorCodes.DocTypeUnsupported, "Please upload a PDF");

        var report = await EnsureReport(b);
        if (report.Status == ReportStatus.Published) throw ApiException.Conflict(ErrorCodes.InvalidState, "This report is already published.");
        ms.Position = 0;
        var stored = await storage.SaveAsync(ms, $"{b.BookingNumber}-lab-report.pdf", "application/pdf", "REPORT", report.Id);
        var old = report.AttachmentFileId;
        report.AttachmentFileId = stored.Id;
        report.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (old != null) await storage.DeleteAsync(old.Value);
        return await ReportEditorAsync(partnerId, bookingId);
    }

    /// <summary>Publish the standardized report: every template parameter needs a result. Moves the booking to "ready".</summary>
    public async Task<PartnerBookingDto> PublishReportAsync(Guid partnerId, Guid bookingId)
    {
        var b = await LabBooking(partnerId, bookingId);
        var report = await db.Reports.Include(r => r.Values).FirstOrDefaultAsync(r => r.BookingId == b.Id)
                     ?? throw ApiException.Unprocessable(ErrorCodes.ReportIncomplete, "Enter the results first.");
        if (report.Status == ReportStatus.Published) return ToPartnerDto(b);

        var template = await Template(b.TestId);
        var missing = template.Where(p => report.Values.All(v => v.ParameterId != p.Id))
            .Select(p => new FieldError($"parameter:{p.Id}", ErrorCodes.Required, $"{p.Name} has no result")).ToList();
        if (string.IsNullOrWhiteSpace(report.PathologistName))
            missing.Add(new FieldError("pathologistName", ErrorCodes.Required, "Enter the signing pathologist"));
        if (missing.Count > 0)
            throw new ApiException(422, ErrorCodes.ReportIncomplete, "Some results are missing.") { Fields = missing };

        if (b.Status == BookingStatus.Collected) workflow.Transition(b, BookingStatus.Processing, UserTypes.Partner);
        report.Status = ReportStatus.Published;
        report.PublishedAt = DateTime.UtcNow;
        report.UpdatedAt = report.PublishedAt.Value;
        b.Report = report;
        workflow.Transition(b, BookingStatus.Ready, UserTypes.Partner);
        await db.SaveChangesAsync();
        return ToPartnerDto(b);
    }

    public static string ComputeFlag(string result, decimal? low, decimal? high)
    {
        if (!TryNumber(result, out var v)) return "normal";
        if (low != null && v < low) return "low";
        if (high != null && v > high) return "high";
        return "normal";
    }

    private static bool TryNumber(string s, out decimal v) =>
        decimal.TryParse(s.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out v);

    private async Task<Report> EnsureReport(Booking b)
    {
        var report = await db.Reports.Include(r => r.Values).FirstOrDefaultAsync(r => r.BookingId == b.Id);
        if (report != null) return report;
        var director = await db.LabMedicalDirectors.AsNoTracking().Where(d => d.LabId == b.LabId).FirstOrDefaultAsync();
        var now = DateTime.UtcNow;
        report = new Report
        {
            Id = Guid.NewGuid(),
            BookingId = b.Id,
            ReportNumber = $"RPT-{IstClock.Now.Year}-{await db.NextSequenceValue(AppDbContext.ReportNumberSeq):D6}",
            PathologistName = director?.Name,
            PathologistRegNo = director?.RegistrationNumber,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Reports.Add(report);
        return report;
    }

    private Task<List<TestParameter>> Template(Guid testId) =>
        db.TestParameters.AsNoTracking().Where(p => p.TestId == testId).OrderBy(p => p.SortOrder).ToListAsync();

    private async Task<Booking> LabBooking(Guid partnerId, Guid bookingId)
    {
        var lab = await LabAsync(partnerId);
        return await workflow.Query().Include(b => b.Patient)
                   .FirstOrDefaultAsync(b => b.Id == bookingId && b.LabId == lab.Id && b.Status != BookingStatus.PaymentPending)
               ?? throw ApiException.NotFound("Booking not found");
    }

    // ---------------------------------------------------------------- dashboard

    public async Task<DashboardDto> DashboardAsync(Guid partnerId)
    {
        var lab = await LabAsync(partnerId);
        var today = IstClock.Today;
        var mine = db.Bookings.AsNoTracking().Where(b => b.LabId == lab.Id && b.Status != BookingStatus.PaymentPending);
        var dayAgo = DateTime.UtcNow.AddDays(-1);

        async Task<decimal> Earned(int days)
        {
            var since = IstClock.ToUtc(today.AddDays(-(days - 1)), TimeOnly.MinValue);
            return await db.Payments.Where(p => p.Status == "PAID" && p.PaidAt >= since && db.Bookings.Any(b => b.Id == p.BookingId && b.LabId == lab.Id))
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
        }

        var upcoming = await workflow.Query().AsNoTracking().Include(b => b.Patient)
            .Where(b => b.LabId == lab.Id && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Enroute) && b.ScheduledDate >= today)
            .OrderBy(b => b.ScheduledDate).ThenBy(b => b.SlotStart).Take(10).ToListAsync();

        return new DashboardDto(
            ToProfile(lab),
            await mine.CountAsync(b => b.ScheduledDate == today && b.Status != BookingStatus.Cancelled),
            await mine.CountAsync(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Enroute),
            await mine.CountAsync(b => b.Status == BookingStatus.Collected || b.Status == BookingStatus.Processing),
            await mine.CountAsync(b => (b.Status == BookingStatus.Collected || b.Status == BookingStatus.Processing) &&
                                       (b.Report == null || b.Report.Status != ReportStatus.Published)),
            await mine.CountAsync(b => b.CreatedAt > dayAgo),
            await Earned(1), await Earned(7), await Earned(30),
            await db.LabTests.CountAsync(lt => lt.LabId == lab.Id && lt.IsActive),
            await db.Phlebotomists.CountAsync(p => p.LabId == lab.Id && p.IsActive),
            upcoming.Select(ToPartnerDto).ToList());
    }

    public PartnerBookingDto ToPartnerDto(Booking b)
    {
        var actions = new List<string>();
        switch (b.Status)
        {
            case BookingStatus.Confirmed when b.Mode == CollectionModes.Home:
                actions.Add("assign");
                if (b.PhlebotomistId != null) { actions.Add("enroute"); actions.Add("collect"); }
                actions.Add("cancel");
                break;
            case BookingStatus.Confirmed:
                actions.AddRange(new[] { "collect", "cancel" });
                break;
            case BookingStatus.Enroute:
                actions.AddRange(new[] { "assign", "collect", "cancel" });
                break;
            case BookingStatus.Collected:
                actions.AddRange(new[] { "processing", "enter_results", "publish" });
                break;
            case BookingStatus.Processing:
                actions.AddRange(new[] { "enter_results", "publish" });
                break;
        }
        return new PartnerBookingDto(
            b.Id, b.BookingNumber, b.Status, b.TestName, b.TestId, b.Mode, IstClock.FormatDate(b.ScheduledDate), b.SlotLabel,
            b.PatientName, b.PatientAge, b.PatientGender, BookingWorkflow.FormatPhone(b.Patient.Phone), b.AddressLabel, b.AddressText,
            b.AddressPincode, b.Price, b.PaymentMethod, b.PaymentStatus, b.PhlebotomistId, b.Phlebotomist?.Name, b.CancelReason,
            b.CreatedAt, b.Report?.Status == ReportStatus.Published, actions, BookingWorkflow.Timeline(b));
    }
}
