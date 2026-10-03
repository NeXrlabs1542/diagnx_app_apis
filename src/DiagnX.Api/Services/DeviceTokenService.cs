using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Services;

public sealed class DeviceTokenService(AppDbContext db)
{
    private static readonly string[] Platforms = { "ANDROID", "IOS", "WEB" };

    /// <summary>PRT-02 / patient equivalent: register or move a push token to this user.</summary>
    public async Task RegisterAsync(string userType, Guid userId, string? pushToken, string? platform)
    {
        var errors = new FieldErrors();
        if (string.IsNullOrWhiteSpace(pushToken) || pushToken.Length > 300) errors.Add("pushToken", ErrorCodes.Required, "Push token is required");
        var p = platform?.ToUpperInvariant();
        if (p == null || !Platforms.Contains(p)) errors.Add("platform", ErrorCodes.InvalidValue, "platform must be ANDROID, IOS or WEB");
        errors.ThrowIfAny();

        var token = await db.DeviceTokens.FirstOrDefaultAsync(t => t.PushToken == pushToken);
        if (token == null)
        {
            token = new DeviceToken { Id = Guid.NewGuid(), PushToken = pushToken! };
            db.DeviceTokens.Add(token);
        }
        token.UserType = userType;
        token.UserId = userId;
        token.Platform = p!;
        token.IsActive = true;
        token.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeactivateAllAsync(string userType, Guid userId) =>
        await db.DeviceTokens.Where(t => t.UserType == userType && t.UserId == userId && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false).SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
}
