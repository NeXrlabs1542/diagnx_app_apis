namespace DiagnX.Api.Features.Kyc;

// Request bodies use exactly the field names of the partner app's KycDraft (spec sheets P5-P10),
// except uploads which are sent as <name>DocId (the id returned by DOC-01).

/// <summary>KYC-03 · Step 1 — Lab profile.</summary>
public sealed class ProfileSection
{
    public string? LabLegalName { get; set; }
    public string? LabBrandName { get; set; }
    public string? BusinessType { get; set; }
    public int? EstablishedYear { get; set; }
    public List<string>? Services { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Area { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? LocationAccuracy { get; set; }
    public DateTime? LocationCapturedAt { get; set; }
    public string? LabPhone { get; set; }
    public string? LabEmail { get; set; }
    public string? Website { get; set; }
}

/// <summary>KYC-04 · Step 2 — Licences &amp; compliance.</summary>
public sealed class LicencesSection
{
    public string? ClinicalEstNumber { get; set; }
    public Guid? ClinicalEstDocId { get; set; }
    public string? BmwNumber { get; set; }
    public string? BmwValidUpto { get; set; }
    public Guid? BmwDocId { get; set; }
    public string? TradeLicenseNumber { get; set; }
    public Guid? TradeLicenseDocId { get; set; }
    public string? AerbNumber { get; set; }
    public Guid? AerbDocId { get; set; }
    public string? PcpndtNumber { get; set; }
    public string? BusinessPan { get; set; }
    public string? Gstin { get; set; }
    public string? EntityRegNumber { get; set; }
    public Guid? EntityRegDocId { get; set; }
    public bool? HasNabl { get; set; }
    public string? NablNumber { get; set; }
    public string? NablValidUpto { get; set; }
    public Guid? NablDocId { get; set; }
    public string? DirectorName { get; set; }
    public string? DirectorQualification { get; set; }
    public string? CouncilName { get; set; }
    public string? MedicalRegNumber { get; set; }
    public Guid? MedicalRegDocId { get; set; }
}

/// <summary>KYC-05 · Step 3 — Authorised signatory.</summary>
public sealed class OwnerSection
{
    public string? OwnerName { get; set; }
    public string? OwnerDesignation { get; set; }
    public string? OwnerDob { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPan { get; set; }
    /// <summary>WRITE-ONLY. May be omitted when already saved (GET returns aadhaarProvided = true).</summary>
    public string? AadhaarNumber { get; set; }
    public bool? AadhaarConsent { get; set; }
    public Guid? PanDocId { get; set; }
    public Guid? AadhaarFrontDocId { get; set; }
    public Guid? AadhaarBackDocId { get; set; }
    public Guid? SelfieDocId { get; set; }
}

/// <summary>KYC-06 · Step 4 — Bank account.</summary>
public sealed class BankSection
{
    public string? AccountHolder { get; set; }
    /// <summary>WRITE-ONLY. May be omitted when already saved (GET returns accountProvided = true).</summary>
    public string? AccountNumber { get; set; }
    public string? ConfirmAccountNumber { get; set; }
    public string? Ifsc { get; set; }
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? AccountType { get; set; }
    public Guid? ChequeDocId { get; set; }
}

/// <summary>KYC-07 · Step 5 — Operations &amp; premises.</summary>
public sealed class OperationsSection
{
    public string? ProcessingMode { get; set; }
    public bool? HomeCollection { get; set; }
    public List<string>? ServiceablePincodes { get; set; }
    public string? PhlebotomistCount { get; set; }
    public List<string>? WorkingDays { get; set; }
    public string? OpenTime { get; set; }
    public string? CloseTime { get; set; }
    public Guid? FrontPhotoDocId { get; set; }
    public Guid? InteriorPhotoDocId { get; set; }
}

/// <summary>KYC-08 · Step 6 — Declarations + submit.</summary>
public sealed class SubmitRequest
{
    public bool? DeclTrue { get; set; }
    public bool? DeclVerify { get; set; }
    public bool? DeclTerms { get; set; }
    public string? AgreementVersion { get; set; }
}

public sealed record StepSaveResult(Guid ApplicationId, int Step, IReadOnlyList<short> CompletedSteps, int NextStep, DateTime SavedAt, bool IsDraft);

public sealed record KycProgressDto(string Status, bool HasProgress, int CurrentStep, IReadOnlyList<short> CompletedSteps, DateTime? LastSavedAt);

public sealed record KycDocumentDto(
    Guid DocumentId, string DocType, string FileName, string MimeType, int SizeBytes, string Status, DateTime UploadedAt, string PreviewUrl);

public sealed record DeclarationsDto(bool DeclTrue, bool DeclVerify, bool DeclTerms, string? AgreementVersion);

public sealed record RejectionReason(string Field, string Message);

/// <summary>KYC-02 · the whole application for pre-filling every step and the Review screen.</summary>
public sealed record KycApplicationDto(
    Guid ApplicationId,
    string Status,
    string? ReferenceId,
    int CurrentStep,
    IReadOnlyList<short> CompletedSteps,
    DateTime? LastSavedAt,
    object? Profile,
    object? Licences,
    object? Owner,
    object? Bank,
    object? Operations,
    IReadOnlyList<KycDocumentDto> Documents,
    DeclarationsDto? Declarations,
    IReadOnlyList<RejectionReason> RejectionReasons);

/// <summary>Step 3 as returned by GET — Aadhaar / PAN masked, never the full numbers.</summary>
public sealed record OwnerView(
    string OwnerName, string OwnerDesignation, string OwnerDob, string OwnerEmail, string OwnerPanMasked,
    bool AadhaarProvided, string AadhaarMasked, bool AadhaarConsent,
    Guid? PanDocId, Guid? AadhaarFrontDocId, Guid? AadhaarBackDocId, Guid? SelfieDocId);

/// <summary>Step 4 as returned by GET — account number masked.</summary>
public sealed record BankView(
    string AccountHolder, bool AccountProvided, string AccountMasked, string Ifsc, string BankName, string? BranchName,
    string AccountType, Guid? ChequeDocId);

public sealed record SubmitResult(string ReferenceId, string Status, DateTime SubmittedAt, DateTime ExpectedBy);

public sealed record TimelineStage(string Code, string Title, string Subtitle, string State, DateTime? At);

public sealed record KycSummaryDto(
    string LabName, string BusinessType, string Location, string? Coordinates, IReadOnlyList<string> Services,
    string BusinessPan, string? Gstin, string Signatory, string SignatoryPan, string AadhaarMasked, string Bank,
    string Ifsc, int DocumentCount);

/// <summary>KYC-09 · the KYC-pending screen.</summary>
public sealed record KycStatusDto(
    string ReferenceId, string Status, DateTime SubmittedAt, DateTime? ExpectedBy, DateTime LastUpdatedAt,
    IReadOnlyList<TimelineStage> Timeline, IReadOnlyList<RejectionReason> RejectionReasons, KycSummaryDto Summary);
