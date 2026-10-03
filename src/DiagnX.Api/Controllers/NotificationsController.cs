using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers;

public sealed record NotificationDto(Guid Id, string Kind, string Title, string Body, Guid? BookingId, DateTime CreatedAt, bool Read);

public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount, int Page, int PageSize);

/// <summary>In-app notifications — same endpoints under /patient and /partner.</summary>
public abstract class NotificationsControllerBase(AppDbContext db) : ApiControllerBase
{
    protected abstract string UserType { get; }

    private IQueryable<Notification> Mine() => db.Notifications.Where(n => n.UserType == UserType && n.UserId == UserId);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<NotificationListDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        var items = await Mine().AsNoTracking().OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.BookingId, n.CreatedAt, n.ReadAt != null)).ToListAsync();
        return Ok(new NotificationListDto(items, await Mine().CountAsync(n => n.ReadAt == null), page, pageSize));
    }

    public sealed record UnreadDto(int UnreadCount);

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<UnreadDto>>> Unread() => Ok(new UnreadDto(await Mine().CountAsync(n => n.ReadAt == null)));

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> ReadAll()
    {
        await Mine().Where(n => n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return Success();
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> Read(Guid id)
    {
        var updated = await Mine().Where(n => n.Id == id && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        if (updated == 0 && !await Mine().AnyAsync(n => n.Id == id)) throw ApiException.NotFound();
        return Success();
    }
}

[Route("api/v1/patient/notifications")]
[ApiExplorerSettings(GroupName = "patient")]
[Authorize(Roles = Roles.Patient)]
public sealed class PatientNotificationsController(AppDbContext db) : NotificationsControllerBase(db)
{
    protected override string UserType => UserTypes.Patient;
}

[Route("api/v1/partner/notifications")]
[ApiExplorerSettings(GroupName = "partner")]
[Authorize(Roles = Roles.Partner)]
public sealed class PartnerNotificationsController(AppDbContext db) : NotificationsControllerBase(db)
{
    protected override string UserType => UserTypes.Partner;
}
