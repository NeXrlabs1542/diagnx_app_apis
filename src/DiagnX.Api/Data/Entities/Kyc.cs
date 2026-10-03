namespace DiagnX.Api.Data.Entities;

public static class KycStatus
{
    public const string Draft = "DRAFT";
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string ChangesRequested = "CHANGES_REQUESTED";

    /// <summary>The partner can edit (and resubmit) only in these states.</summary>
    public static bool IsEditable(string status) => status is Draft or Rejected or ChangesRequested;
}

/// <summary>One KYC application per partner — the parent of every KYC table.</summary>
public class KycApplication
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public string? ReferenceId { get; set; }
    public string Status { get; set; } = KycStatus.Draft;
    public short CurrentStep { get; set; } = 1;
    public List<short> CompletedSteps { get; set; } = new();
    public DateTime? LastSavedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ExpectedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    /// <summary>jsonb: [{field, message}] for REJECTED / CHANGES_REQUESTED.</summary>
    public string? RejectionReason { get; set; }
    /// <summary>jsonb: partial sections saved with header X-Draft: true (never contains Aadhaar / account numbers).</summary>
    public string? DraftSections { get; set; }
    public string? SubmitIdempotencyKey { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Partner Partner { get; set; } = null!;
    public Lab? Lab { get; set; }
    public KycSignatory? Signatory { get; set; }
    public KycDeclaration? Declaration { get; set; }
    public List<KycDocument> Documents { get; set; } = new();
    public List<KycStatusHistory> History { get; set; } = new();
    public List<KycVerificationCheck> Checks { get; set; } = new();
}

/// <summary>
/// The lab. Created by KYC step 1 for partner labs (becomes live when KYC is approved).
/// Labs seeded by DiagnX for the pilot have no application / partner.
/// </summary>
public class Lab
{
    public Guid Id { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid? PartnerId { get; set; }

    // KYC step 1 — business identity, address, contact
    public string LegalName { get; set; } = "";
    public string? BrandName { get; set; }
    public string BusinessType { get; set; } = "";
    public short EstablishedYear { get; set; }
    public string AddressLine1 { get; set; } = "";
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Pincode { get; set; } = "";
    public string Area { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? LocationAccuracyM { get; set; }
    public DateTime? LocationCapturedAt { get; set; }
    public string LabPhone { get; set; } = "";
    public string LabEmail { get; set; } = "";
    public string? Website { get; set; }
    /// <summary>KYC step 2 answer to "Is your lab NABL accredited?" (null = not answered).</summary>
    public bool? HasNabl { get; set; }

    /// <summary>false until KYC is APPROVED; only active labs are visible to patients.</summary>
    public bool IsActive { get; set; }

    // Marketplace settings (managed by the lab after approval)
    public short TurnaroundHours { get; set; } = 24;
    public bool WalkIn { get; set; } = true;
    public bool IsoCertified { get; set; }
    public bool AcceptingBookings { get; set; } = true;
    /// <summary>Bookings the lab can take per time slot.</summary>
    public short SlotCapacity { get; set; } = 4;
    public string LogoColor { get; set; } = "#B4536A";
    public decimal RatingAvg { get; set; }
    public int RatingCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(BrandName) ? LegalName : BrandName!;

    public KycApplication? Application { get; set; }
    public List<LabService> Services { get; set; } = new();
    public List<LabLicence> Licences { get; set; } = new();
    public LabTaxDetail? TaxDetail { get; set; }
    public List<LabMedicalDirector> MedicalDirectors { get; set; } = new();
    public List<LabBankAccount> BankAccounts { get; set; } = new();
    public LabOperations? Operations { get; set; }
    public List<LabWorkingDay> WorkingDays { get; set; } = new();
    public List<LabServiceablePincode> ServiceablePincodes { get; set; } = new();
    public List<LabTest> Tests { get; set; } = new();
}

public class LabService
{
    public Guid LabId { get; set; }
    public string ServiceCode { get; set; } = ""; // pathology | radiology | cardiac
}

public static class LicenceTypes
{
    public const string ClinicalEst = "CLINICAL_EST";
    public const string Bmw = "BMW";
    public const string Trade = "TRADE";
    public const string Nabl = "NABL";
    public const string Aerb = "AERB";
    public const string Pcpndt = "PCPNDT";
    public const string EntityReg = "ENTITY_REG";
}

public class LabLicence
{
    public Guid Id { get; set; }
    public Guid LabId { get; set; }
    public string LicenceType { get; set; } = "";
    public string Number { get; set; } = "";
    public DateOnly? ValidUpto { get; set; }
    public Guid? DocumentId { get; set; }
    public string VerificationStatus { get; set; } = "NOT_CHECKED";
    public DateTime? VerifiedAt { get; set; }
}

public class LabTaxDetail
{
    public Guid LabId { get; set; }
    public string BusinessPan { get; set; } = "";
    public string? Gstin { get; set; }
    public bool PanVerified { get; set; }
    public bool GstinVerified { get; set; }
    public string? RegisteredName { get; set; }
}

public class LabMedicalDirector
{
    public Guid Id { get; set; }
    public Guid LabId { get; set; }
    public string Name { get; set; } = "";
    public string Qualification { get; set; } = "";
    public string CouncilName { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public Guid? DocumentId { get; set; }
    public string VerificationStatus { get; set; } = "NOT_CHECKED";
}

/// <summary>Authorised signatory. HIGHLY SENSITIVE: PAN and Aadhaar are encrypted at rest.</summary>
public class KycSignatory
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = "";
    public string Designation { get; set; } = "";
    public DateOnly Dob { get; set; }
    public string Email { get; set; } = "";
    public string PanEnc { get; set; } = "";
    public string PanMasked { get; set; } = "";
    /// <summary>AES-256-GCM ciphertext of the Aadhaar number (swap for an Aadhaar Data Vault token in production).</summary>
    public string AadhaarRef { get; set; } = "";
    public string AadhaarLast4 { get; set; } = "";
    public bool AadhaarConsent { get; set; }
    public DateTime AadhaarConsentAt { get; set; }
    public string? AadhaarConsentIp { get; set; }
    public Guid? PanDocId { get; set; }
    public Guid? AadhaarFrontDocId { get; set; }
    public Guid? AadhaarBackDocId { get; set; }
    public Guid? SelfieDocId { get; set; }
    public bool PanVerified { get; set; }
    public bool AadhaarVerified { get; set; }
    public decimal? NameMatchScore { get; set; }
}

public class LabBankAccount
{
    public Guid Id { get; set; }
    public Guid LabId { get; set; }
    public string AccountHolder { get; set; } = "";
    public string AccountNumberEnc { get; set; } = "";
    public string AccountLast4 { get; set; } = "";
    public string Ifsc { get; set; } = "";
    public string BankName { get; set; } = "";
    public string? BranchName { get; set; }
    public string AccountType { get; set; } = "";
    public Guid? DocumentId { get; set; }
    public string PennyDropStatus { get; set; } = "NOT_STARTED";
    public string? PennyDropName { get; set; }
    public bool IsPrimary { get; set; } = true;
}

public class LabOperations
{
    public Guid LabId { get; set; }
    public string ProcessingMode { get; set; } = ""; // in_house | outsourced
    public bool HomeCollection { get; set; }
    public string? PhlebotomistCount { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public Guid? FrontPhotoDocId { get; set; }
    public Guid? InteriorPhotoDocId { get; set; }
}

public class LabWorkingDay
{
    public Guid LabId { get; set; }
    public string DayCode { get; set; } = ""; // Mon..Sun
}

public class LabServiceablePincode
{
    public Guid LabId { get; set; }
    public string Pincode { get; set; } = "";
}

public static class DocTypes
{
    public const string ClinicalEst = "CLINICAL_EST";
    public const string Bmw = "BMW";
    public const string TradeLicence = "TRADE_LICENCE";
    public const string EntityReg = "ENTITY_REG";
    public const string Nabl = "NABL";
    public const string Aerb = "AERB";
    public const string MedicalReg = "MEDICAL_REG";
    public const string PanCard = "PAN_CARD";
    public const string AadhaarFront = "AADHAAR_FRONT";
    public const string AadhaarBack = "AADHAAR_BACK";
    public const string Selfie = "SELFIE";
    public const string CancelledCheque = "CANCELLED_CHEQUE";
    public const string LabFrontPhoto = "LAB_FRONT_PHOTO";
    public const string LabInteriorPhoto = "LAB_INTERIOR_PHOTO";

    public static readonly string[] All =
    {
        ClinicalEst, Bmw, TradeLicence, EntityReg, Nabl, Aerb, MedicalReg, PanCard, AadhaarFront, AadhaarBack,
        Selfie, CancelledCheque, LabFrontPhoto, LabInteriorPhoto,
    };

    /// <summary>Doc types that must be a photo (no PDF).</summary>
    public static readonly HashSet<string> PhotoOnly = new() { Selfie, LabFrontPhoto, LabInteriorPhoto };
}

public class KycDocument
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string DocType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public int SizeBytes { get; set; }
    public Guid StoredFileId { get; set; }
    public string? ChecksumSha256 { get; set; }
    /// <summary>UPLOADED | VERIFIED | REJECTED | REPLACED | DELETED</summary>
    public string Status { get; set; } = "UPLOADED";
    public string? RejectReason { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class KycDeclaration
{
    public Guid ApplicationId { get; set; }
    public bool DeclTrue { get; set; }
    public bool DeclVerify { get; set; }
    public bool DeclTerms { get; set; }
    public string AgreementVersion { get; set; } = "";
    public DateTime AcceptedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceId { get; set; }
}

public class KycStatusHistory
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public string ChangedBy { get; set; } = ""; // partner:<id> | admin:<id> | system
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class CheckTypes
{
    public static readonly string[] All =
        { "PAN", "GSTIN", "AADHAAR", "BANK_PENNY_DROP", "MEDICAL_COUNCIL", "NABL", "CIN_LLPIN", "CLINICAL_EST" };
}

public class KycVerificationCheck
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string CheckType { get; set; } = "";
    /// <summary>QUEUED | PASSED | FAILED | MANUAL_REVIEW</summary>
    public string Status { get; set; } = "QUEUED";
    public string? Provider { get; set; }
    public string? RequestRef { get; set; }
    public string? ResponseJson { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? CheckedAt { get; set; }
}
