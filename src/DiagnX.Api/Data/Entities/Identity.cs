namespace DiagnX.Api.Data.Entities;

public static class UserTypes
{
    public const string Patient = "PATIENT";
    public const string Partner = "PARTNER";
    public const string Admin = "ADMIN";
}

public class Patient
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = "";
    public string CountryCode { get; set; } = "+91";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public short? Age { get; set; }
    public string? Gender { get; set; } // Male | Female | Other
    public string? City { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE | BLOCKED
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A lab-partner login (one per mobile number). See spec table "partners".</summary>
public class Partner
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = "";
    public string CountryCode { get; set; } = "+91";
    /// <summary>NOT_STARTED | IN_PROGRESS | PENDING | APPROVED | REJECTED | CHANGES_REQUESTED</summary>
    public string KycStatus { get; set; } = PartnerKycStatus.NotStarted;
    public string Status { get; set; } = "ACTIVE";
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public KycApplication? Application { get; set; }
}

public static class PartnerKycStatus
{
    public const string NotStarted = "NOT_STARTED";
    public const string InProgress = "IN_PROGRESS";
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string ChangesRequested = "CHANGES_REQUESTED";
}

public class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>SUPER_ADMIN | COMPLIANCE_OFFICER | OPS</summary>
    public string Role { get; set; } = "SUPER_ADMIN";
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OtpRequest
{
    public Guid Id { get; set; }
    /// <summary>PATIENT | PARTNER — each app has its own OTP flow.</summary>
    public string Audience { get; set; } = "";
    public string Phone { get; set; } = "";
    public string OtpHash { get; set; } = "";
    public short Attempts { get; set; }
    public short ResendCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>One row per logged-in device (refresh-token store), for any user type.</summary>
public class AuthSession
{
    public Guid Id { get; set; }
    public string UserType { get; set; } = "";
    public Guid UserId { get; set; }
    public string RefreshTokenHash { get; set; } = "";
    public string? DeviceId { get; set; }
    public string? DevicePlatform { get; set; }
    public string? AppVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DeviceToken
{
    public Guid Id { get; set; }
    public string UserType { get; set; } = "";
    public Guid UserId { get; set; }
    public string PushToken { get; set; } = "";
    public string Platform { get; set; } = ""; // ANDROID | IOS | WEB
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
