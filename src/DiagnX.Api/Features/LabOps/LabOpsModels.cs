using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Features.Catalog;

namespace DiagnX.Api.Features.LabOps;

public sealed record LabProfileDto(
    Guid Id, string Name, string LegalName, bool IsLive, bool AcceptingBookings, int TurnaroundHours, bool WalkIn, bool HomeCollection,
    int SlotCapacity, bool IsoCertified, decimal Rating, int ReviewCount, string Address, string? OpenTime, string? CloseTime,
    IReadOnlyList<string> WorkingDays, IReadOnlyList<string> ServiceablePincodes);

public sealed class LabSettingsRequest
{
    public bool? AcceptingBookings { get; set; }
    public int? TurnaroundHours { get; set; }
    public bool? WalkIn { get; set; }
    public bool? HomeCollection { get; set; }
    public int? SlotCapacity { get; set; }
    public bool? IsoCertified { get; set; }
    public List<string>? ServiceablePincodes { get; set; }
    public List<string>? WorkingDays { get; set; }
    public string? OpenTime { get; set; }
    public string? CloseTime { get; set; }
}

public sealed record CatalogTestDto(TestSummaryDto Test, bool Enrolled, bool IsActive, decimal? Price, decimal? Mrp);

public sealed class LabTestPriceRequest
{
    public decimal Mrp { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record PhlebotomistDto(Guid Id, string Name, string Phone, bool IsActive, int ActiveAssignments);

public sealed class PhlebotomistRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record PartnerBookingDto(
    Guid Id, string BookingNumber, string Status, string TestName, Guid TestId, string Mode, string Date, string SlotLabel,
    string PatientName, int? PatientAge, string? PatientGender, string PatientPhone, string? AddressLabel, string? Address,
    string? AddressPincode, decimal Price, string PaymentMethod, string PaymentStatus, Guid? PhlebotomistId, string? PhlebotomistName,
    string? CancelReason, DateTime CreatedAt, bool ReportPublished, IReadOnlyList<string> NextActions, IReadOnlyList<TimelineStepDto> Timeline);

public sealed class AssignRequest
{
    public Guid PhlebotomistId { get; set; }
}

public sealed class CollectRequest
{
    /// <summary>4-digit code shown in the patient's app.</summary>
    public string? Otp { get; set; }
    /// <summary>For pay-on-collection bookings: cash / UPI received at collection.</summary>
    public bool PaymentCollected { get; set; } = true;
}

public sealed record ReportEditorParameter(
    Guid ParameterId, string Name, string Unit, string ReferenceRange, string ValueType, string? Result, string? Flag);

public sealed record ReportEditorDto(
    Guid BookingId, string? ReportId, string Status, string? PathologistName, string? PathologistRegNo, string? Remarks,
    IReadOnlyList<ReportEditorParameter> Parameters, string? AttachmentUrl);

public sealed class SaveReportRequest
{
    public string? PathologistName { get; set; }
    public string? PathologistRegNo { get; set; }
    public string? Remarks { get; set; }
    public List<ReportValueInput>? Values { get; set; }
}

public sealed class ReportValueInput
{
    public Guid ParameterId { get; set; }
    public string? Result { get; set; }
    /// <summary>Only used for text parameters (numeric ones are flagged from the reference range).</summary>
    public string? Flag { get; set; }
}

public sealed record DashboardDto(
    LabProfileDto Lab, int TodayBookings, int AwaitingCollection, int InProcessing, int ReportsPending, int NewSinceYesterday,
    decimal EarningsToday, decimal Earnings7Days, decimal Earnings30Days, int EnrolledTests, int ActivePhlebotomists,
    IReadOnlyList<PartnerBookingDto> Upcoming);
