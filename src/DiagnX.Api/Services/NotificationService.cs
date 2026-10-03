using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;

namespace DiagnX.Api.Services;

/// <summary>
/// Writes in-app notifications (the Notifications screen). Push delivery (FCM / APNs via
/// device_tokens) plugs in here later; callers don't change.
/// </summary>
public sealed class NotificationService(AppDbContext db)
{
    /// <summary>Adds the notification to the context; the caller's SaveChanges persists it.</summary>
    public void Add(string userType, Guid userId, string kind, string title, string body, Guid? bookingId = null)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserType = userType,
            UserId = userId,
            Kind = kind,
            Title = title,
            Body = body,
            BookingId = bookingId,
            CreatedAt = DateTime.UtcNow,
        });
    }
}

public sealed class AuditService(AppDbContext db, IHttpContextAccessor http)
{
    /// <summary>Adds an audit row; persisted with the caller's SaveChanges. Never pass PII in metadata.</summary>
    public void Add(string actorType, string? actorId, string action, string? entityType = null, string? entityId = null, object? metadata = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = actorType,
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            MetadataJson = metadata == null ? null : System.Text.Json.JsonSerializer.Serialize(metadata),
            CreatedAt = DateTime.UtcNow,
        });
    }
}
