namespace DiagnX.Api.Features.Bookings;

public sealed class CreateBookingRequest
{
    public Guid LabId { get; set; }
    /// <summary>Test id (uuid) or slug, e.g. "test-cbc".</summary>
    public string? TestId { get; set; }
    /// <summary>home | walkin</summary>
    public string? Mode { get; set; }
    /// <summary>Required for home collection.</summary>
    public Guid? AddressId { get; set; }
    /// <summary>Book for a family member instead of the account holder.</summary>
    public Guid? FamilyMemberId { get; set; }
    /// <summary>YYYY-MM-DD (IST).</summary>
    public string? Date { get; set; }
    public Guid SlotId { get; set; }
    /// <summary>upi | card | cod</summary>
    public string? PaymentMethod { get; set; }
}

public sealed record TimelineStepDto(string Status, string Title, string Subtitle, string State, DateTime? At);

public sealed record ReviewDto(int LabRating, int? PhleboRating, string? Comment, DateTime CreatedAt);

/// <summary>The patient app's Booking shape (src/types Booking) plus timeline + action flags.</summary>
public sealed record BookingDto(
    Guid Id,
    string BookingNumber,
    Guid TestId,
    string TestName,
    Guid LabId,
    string LabName,
    string Mode,
    string? AddressLabel,
    string? Address,
    string Date,
    string SlotLabel,
    decimal Price,
    decimal Mrp,
    decimal Discount,
    string PaymentMethod,
    string PaymentStatus,
    string Status,
    DateTime CreatedAt,
    string PatientName,
    string? PhlebotomistName,
    string? PhlebotomistPhone,
    string? Otp,
    string? CancelReason,
    bool CanCancel,
    bool CanReview,
    bool HasReport,
    ReviewDto? Review,
    IReadOnlyList<TimelineStepDto> Timeline);

public sealed record PaymentIntentDto(Guid PaymentId, string Gateway, string GatewayOrderId, decimal Amount, string Currency, string? PublicKey);

public sealed record CreateBookingResult(BookingDto Booking, PaymentIntentDto? Payment, string NextAction);

public sealed class VerifyPaymentRequest
{
    public string? GatewayPaymentId { get; set; }
    public string? GatewaySignature { get; set; }
}

public sealed class CancelRequest
{
    public string? Reason { get; set; }
}

public sealed class ReviewRequest
{
    public int LabRating { get; set; }
    public int? PhleboRating { get; set; }
    public string? Comment { get; set; }
}

public sealed record ReportParameterDto(string Name, string Result, string Unit, string ReferenceRange, string Flag);

/// <summary>The standardized report (src/types ReportResult).</summary>
public sealed record ReportDto(
    Guid BookingId,
    string ReportId,
    string TestName,
    string LabName,
    string PatientName,
    int? PatientAge,
    string? PatientGender,
    DateTime? CollectedOn,
    DateTime ReportedOn,
    string Pathologist,
    string? PathologistRegNo,
    string? Remarks,
    IReadOnlyList<ReportParameterDto> Parameters,
    bool HasFlags,
    string? LabReportPdfUrl);

public sealed record ReportListItemDto(Guid BookingId, string ReportId, string TestName, string LabName, DateTime ReportedOn, bool HasFlags, int FlagCount);
