using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Features.Catalog;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.PatientApp;

/// <summary>Everything the Home screen shows, in one call.</summary>
[Route("api/v1/patient/home")]
[ApiExplorerSettings(GroupName = "patient")]
[Authorize(Roles = Roles.Patient)]
public sealed class HomeController(AppDbContext db, CatalogService catalog, BookingWorkflow workflow) : ApiControllerBase
{
    public sealed record HomeDto(
        string? PatientName,
        int UnreadNotifications,
        BookingDto? ActiveBooking,
        IReadOnlyList<PromoBannerDto> PromoBanners,
        IReadOnlyList<CategoryDto> Categories,
        IReadOnlyList<TestSummaryDto> PopularTests,
        IReadOnlyList<TestSummaryDto> Packages,
        IReadOnlyList<LabCardDto> TopLabs,
        IReadOnlyList<HealthTipDto> HealthTips,
        IReadOnlyList<TrustPointDto> TrustPoints);

    private static readonly TrustPointDto[] TrustPoints =
    {
        new("trust-1", "ribbon", "NABL certified", "Every partner lab audited"),
        new("trust-2", "shield-checkmark", "Verified staff", "Background-checked phlebotomists"),
        new("trust-3", "time", "Fast reports", "Most in 12–24 hours"),
        new("trust-4", "lock-closed", "Private & secure", "Encrypted, only you see results"),
    };

    /// <summary>?lat=&amp;lng= (optional) adds distance to the lab cards.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<HomeDto>>> Get([FromQuery] double? lat, [FromQuery] double? lng)
    {
        var patientId = UserId;
        var name = await db.Patients.Where(p => p.Id == patientId).Select(p => p.Name).FirstOrDefaultAsync();
        var unread = await db.Notifications.CountAsync(n => n.UserType == UserTypes.Patient && n.UserId == patientId && n.ReadAt == null);
        var active = await workflow.Query().AsNoTracking()
            .Where(b => b.PatientId == patientId && b.Status != BookingStatus.Completed && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.ScheduledDate).ThenBy(b => b.SlotStart).FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        var banners = await db.PromoBanners.AsNoTracking()
            .Where(b => b.IsActive && (b.StartsAt == null || b.StartsAt <= now) && (b.EndsAt == null || b.EndsAt > now))
            .OrderBy(b => b.SortOrder)
            .Select(b => new PromoBannerDto(b.Id, b.Title, b.Subtitle, b.Icon, new[] { b.GradientFrom, b.GradientTo }, b.TargetTestSlug))
            .ToListAsync();
        var tips = await db.HealthTips.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder)
            .Select(t => new HealthTipDto(t.Id, t.Title, t.Body, t.Icon, t.ReadMins)).ToListAsync();

        return Ok(new HomeDto(
            name,
            unread,
            active == null ? null : workflow.ToDto(active),
            banners,
            await catalog.CategoriesAsync(homeOnly: true),
            await catalog.SearchTestsAsync(null, null, "test", popular: true, limit: 10),
            await catalog.SearchTestsAsync(null, null, "package", null, limit: 10),
            await catalog.LabsAsync(null, "rating", CatalogController.Geo(lat, lng), null, limit: 5),
            tips,
            TrustPoints));
    }
}
