namespace DiagnX.Api.Data.Entities;

/// <summary>booking | enroute | collected | ready | offer | system (patient) + kyc | order (partner).</summary>
public class Notification
{
    public Guid Id { get; set; }
    public string UserType { get; set; } = "";
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public Guid? BookingId { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Uploaded file bytes. Kept in PostgreSQL for the pilot (Render's disk is wiped on deploy);
/// everything goes through IFileStorage so it can move to S3 / R2 without API changes.
/// </summary>
public class StoredFile
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string OwnerType { get; set; } = "";
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MasterOption
{
    public int Id { get; set; }
    public string GroupCode { get; set; } = "";
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Hint { get; set; }
    public short SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MasterState
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? GstStateCode { get; set; }
    public string Type { get; set; } = "STATE"; // STATE | UT
}

public class AppConfig
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Cache of India Post PIN code lookups (MST-04).</summary>
public class PincodeCache
{
    public string Pincode { get; set; } = "";
    public List<string> Areas { get; set; } = new();
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime FetchedAt { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public string ActorType { get; set; } = ""; // PARTNER | ADMIN | PATIENT | SYSTEM
    public string? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    /// <summary>jsonb. Never put PII / full numbers here.</summary>
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
