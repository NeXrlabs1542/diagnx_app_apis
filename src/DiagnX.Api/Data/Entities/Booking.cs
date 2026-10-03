namespace DiagnX.Api.Data.Entities;

/// <summary>Order statuses exactly as the patient app uses them (src/types OrderStatus) + payment_pending.</summary>
public static class BookingStatus
{
    public const string PaymentPending = "payment_pending";
    public const string Confirmed = "confirmed";
    public const string Enroute = "enroute";
    public const string Collected = "collected";
    public const string Processing = "processing";
    public const string Ready = "ready";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All =
        { PaymentPending, Confirmed, Enroute, Collected, Processing, Ready, Completed, Cancelled };

    /// <summary>Statuses shown on the Orders screen's "Active" tab.</summary>
    public static readonly string[] Active = { PaymentPending, Confirmed, Enroute, Collected, Processing, Ready };
    /// <summary>Statuses shown on the "Past" tab (the app lists ready under both).</summary>
    public static readonly string[] Past = { Ready, Completed, Cancelled };
}

public static class CollectionModes
{
    public const string Home = "home";
    public const string WalkIn = "walkin";
}

public static class PaymentMethods
{
    public const string Upi = "upi";
    public const string Card = "card";
    public const string Cod = "cod";
    public static readonly string[] All = { Upi, Card, Cod };
}

public static class PaymentStatus
{
    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string PayOnCollection = "PAY_ON_COLLECTION";
    public const string Failed = "FAILED";
    public const string Refunded = "REFUNDED";
    public const string NotRequired = "NOT_REQUIRED";
}

public class PatientAddress
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string Label { get; set; } = "";
    public string Line1 { get; set; } = "";
    public string? Line2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = "";
    public string? State { get; set; }
    public string Pincode { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDefault { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FamilyMember
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string Name { get; set; } = "";
    public string Relation { get; set; } = "";
    public short? Age { get; set; }
    public string? Gender { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Phlebotomist
{
    public Guid Id { get; set; }
    public Guid LabId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Booking
{
    public Guid Id { get; set; }
    public string BookingNumber { get; set; } = "";
    public Guid PatientId { get; set; }
    public Guid? FamilyMemberId { get; set; }

    // Snapshots: what the patient booked, independent of later catalog / profile edits
    public string PatientName { get; set; } = "";
    public short? PatientAge { get; set; }
    public string? PatientGender { get; set; }
    public Guid LabId { get; set; }
    public string LabName { get; set; } = "";
    public Guid TestId { get; set; }
    public string TestName { get; set; } = "";

    public string Mode { get; set; } = CollectionModes.Home;
    public Guid? AddressId { get; set; }
    public string? AddressLabel { get; set; }
    public string? AddressText { get; set; }
    public string? AddressPincode { get; set; }

    public DateOnly ScheduledDate { get; set; }
    public Guid SlotId { get; set; }
    public string SlotLabel { get; set; } = "";
    public TimeOnly SlotStart { get; set; }

    public decimal Mrp { get; set; }
    public decimal Discount { get; set; }
    public decimal Price { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Upi;
    public string PaymentStatus { get; set; } = Entities.PaymentStatus.Pending;

    public string Status { get; set; } = BookingStatus.Confirmed;
    /// <summary>4-digit code the patient shows the phlebotomist / lab counter at collection.</summary>
    public string CollectionOtp { get; set; } = "";
    public Guid? PhlebotomistId { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledBy { get; set; }
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? ReportReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public Lab Lab { get; set; } = null!;
    public DiagnosticTest Test { get; set; } = null!;
    public Phlebotomist? Phlebotomist { get; set; }
    public List<BookingStatusEvent> Events { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public Report? Report { get; set; }
    public Review? Review { get; set; }
}

public class BookingStatusEvent
{
    public long Id { get; set; }
    public Guid BookingId { get; set; }
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public string ActorType { get; set; } = ""; // PATIENT | PARTNER | ADMIN | SYSTEM
    public DateTime CreatedAt { get; set; }
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>CREATED | PAID | FAILED | REFUNDED</summary>
    public string Status { get; set; } = "CREATED";
    public string Gateway { get; set; } = "";
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class ReportStatus
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";
}

/// <summary>The standardized report for a booking: values mapped onto the catalog test's template.</summary>
public class Report
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string ReportNumber { get; set; } = "";
    public string Status { get; set; } = ReportStatus.Draft;
    public string? PathologistName { get; set; }
    public string? PathologistRegNo { get; set; }
    public string? Remarks { get; set; }
    public bool HasFlags { get; set; }
    /// <summary>Optional original PDF from the lab's LIS, kept alongside the standard report.</summary>
    public Guid? AttachmentFileId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ReportValue> Values { get; set; } = new();
}

public class ReportValue
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public Guid? ParameterId { get; set; }
    public string Name { get; set; } = "";
    public string Result { get; set; } = "";
    public string Unit { get; set; } = "";
    public string ReferenceRange { get; set; } = "";
    /// <summary>low | high | normal</summary>
    public string Flag { get; set; } = "normal";
    public short SortOrder { get; set; }
}

public class Review
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid PatientId { get; set; }
    public Guid LabId { get; set; }
    public Guid? PhlebotomistId { get; set; }
    public short LabRating { get; set; }
    public short? PhleboRating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
