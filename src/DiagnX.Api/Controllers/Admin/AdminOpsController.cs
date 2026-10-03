using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Features.Catalog;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.Admin;

/// <summary>Ops panel: platform stats, labs, bookings, runtime config and admin users.</summary>
[Route("api/v1/admin")]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminOpsController(AppDbContext db, BookingWorkflow workflow, AuditService audit) : ApiControllerBase
{
    public sealed record StatsDto(
        int Patients, int Partners, int LiveLabs, int PendingKyc, int BookingsToday, int ActiveBookings, int ReportsPublished30Days,
        decimal Gmv30Days);

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<StatsDto>>> Stats()
    {
        var today = IstClock.Today;
        var since = DateTime.UtcNow.AddDays(-30);
        return Ok(new StatsDto(
            await db.Patients.CountAsync(),
            await db.Partners.CountAsync(),
            await db.Labs.CountAsync(l => l.IsActive),
            await db.KycApplications.CountAsync(a => a.Status == KycStatus.Pending),
            await db.Bookings.CountAsync(b => b.ScheduledDate == today && b.Status != BookingStatus.Cancelled),
            await db.Bookings.CountAsync(b => BookingStatus.Active.Contains(b.Status) && b.Status != BookingStatus.Ready),
            await db.Reports.CountAsync(r => r.Status == ReportStatus.Published && r.PublishedAt > since),
            await db.Payments.Where(p => p.Status == "PAID" && p.PaidAt > since).SumAsync(p => (decimal?)p.Amount) ?? 0));
    }

    public sealed record AdminLabDto(LabCardDto Card, string LegalName, string Phone, string Email, bool IsActive, bool AcceptingBookings,
        Guid? PartnerId, int TestsOffered, DateTime CreatedAt);

    [HttpGet("labs")]
    public async Task<ActionResult<ApiResponse<List<AdminLabDto>>>> Labs([FromQuery] bool? active)
    {
        var q = db.Labs.AsNoTracking().Include(l => l.Operations).Include(l => l.Licences).AsQueryable();
        if (active != null) q = q.Where(l => l.IsActive == active);
        var labs = await q.OrderBy(l => l.LegalName).ToListAsync();
        var counts = await db.LabTests.Where(lt => lt.IsActive).GroupBy(lt => lt.LabId).Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C);
        return Ok(labs.Select(l => new AdminLabDto(CatalogService.ToCard(l, null), l.LegalName, l.LabPhone, l.LabEmail, l.IsActive,
            l.AcceptingBookings, l.PartnerId, counts.GetValueOrDefault(l.Id), l.CreatedAt)).ToList());
    }

    public sealed class LabStatusRequest
    {
        public bool? IsActive { get; set; }
        public bool? IsoCertified { get; set; }
        public string? LogoColor { get; set; }
    }

    /// <summary>Suspend / re-activate a lab (hides it from patients), mark ISO, set its brand colour.</summary>
    [HttpPatch("labs/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> UpdateLab(Guid id, LabStatusRequest r)
    {
        var lab = await db.Labs.FindAsync(id) ?? throw ApiException.NotFound();
        if (r.IsActive != null) lab.IsActive = r.IsActive.Value;
        if (r.IsoCertified != null) lab.IsoCertified = r.IsoCertified.Value;
        if (!string.IsNullOrWhiteSpace(r.LogoColor)) lab.LogoColor = r.LogoColor.Trim()[..Math.Min(9, r.LogoColor.Trim().Length)];
        lab.UpdatedAt = DateTime.UtcNow;
        audit.Add("ADMIN", UserId.ToString(), "LAB_UPDATED", "lab", id.ToString(), new { r.IsActive, r.IsoCertified });
        await db.SaveChangesAsync();
        return Success();
    }

    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingDto>>>> Bookings(
        [FromQuery] string? status, [FromQuery] Guid? labId, [FromQuery] string? date, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        var query = workflow.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(b => b.Status == status);
        if (labId != null) query = query.Where(b => b.LabId == labId);
        if (IstClock.TryParseDate(date, out var d)) query = query.Where(b => b.ScheduledDate == d);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(b => b.BookingNumber.Contains(q.Trim().ToUpper()) || EF.Functions.ILike(b.PatientName, $"%{q.Trim()}%"));
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(b => b.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<BookingDto>(rows.Select(workflow.ToDto).ToList(), page, pageSize, total));
    }

    [HttpPost("bookings/{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> CancelBooking(Guid id, CancelRequest r)
    {
        var b = await workflow.Query().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        await workflow.CancelAsync(b, UserTypes.Admin, r.Reason);
        audit.Add("ADMIN", UserId.ToString(), "BOOKING_CANCELLED", "booking", id.ToString());
        await db.SaveChangesAsync();
        return Ok(workflow.ToDto(b));
    }

    // ---------------------------------------------------------------- config

    [HttpGet("config")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> Config([FromServices] AppConfigService config) =>
        Ok(await config.AllAsync());

    /// <summary>Set runtime values, e.g. { "supportPhone": "+91 ...", "kycTatBusinessDays": "3" }.</summary>
    [HttpPut("config")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> SetConfig(Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            if (!AppConfigService.Defaults.ContainsKey(key)) throw ApiException.Field(key, ErrorCodes.InvalidValue, "Unknown config key");
            var row = await db.AppConfig.FindAsync(key);
            if (row == null) db.AppConfig.Add(row = new AppConfig { Key = key });
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
        }
        audit.Add("ADMIN", UserId.ToString(), "CONFIG_UPDATED", metadata: values.Keys);
        await db.SaveChangesAsync();
        return Success();
    }

    // ---------------------------------------------------------------- admin users

    public sealed class AdminUserRequest
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Password { get; set; }
        public string Role { get; set; } = "OPS";
    }

    public sealed record AdminUserDto(Guid Id, string Email, string Name, string Role, bool IsActive, DateTime? LastLoginAt);

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<List<AdminUserDto>>>> Users() =>
        Ok(await db.AdminUsers.AsNoTracking().OrderBy(a => a.Email)
            .Select(a => new AdminUserDto(a.Id, a.Email, a.Name, a.Role, a.IsActive, a.LastLoginAt)).ToListAsync());

    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> AddUser(AdminUserRequest r, [FromServices] IPasswordHasher<AdminUser> hasher)
    {
        if (User.FindFirst("admin_role")?.Value != "SUPER_ADMIN") throw ApiException.Forbidden("Only a super admin can add users");
        var e = new FieldErrors();
        if (!IndianIds.IsEmail(r.Email)) e.Add("email", ErrorCodes.InvalidEmail, "Enter a valid email");
        e.RequireText("name", r.Name, 2, 100);
        if ((r.Password ?? "").Length < 10) e.Add("password", ErrorCodes.InvalidValue, "Use at least 10 characters");
        if (r.Role is not ("SUPER_ADMIN" or "COMPLIANCE_OFFICER" or "OPS")) e.Add("role", ErrorCodes.InvalidValue, "SUPER_ADMIN, COMPLIANCE_OFFICER or OPS");
        e.ThrowIfAny();
        var email = r.Email!.Trim().ToLowerInvariant();
        if (await db.AdminUsers.AnyAsync(a => a.Email == email)) throw ApiException.Field("email", ErrorCodes.InvalidValue, "Already exists");

        var user = new AdminUser { Id = Guid.NewGuid(), Email = email, Name = r.Name!.Trim(), Role = r.Role, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, r.Password!);
        db.AdminUsers.Add(user);
        audit.Add("ADMIN", UserId.ToString(), "ADMIN_USER_CREATED", "admin_user", user.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(new AdminUserDto(user.Id, user.Email, user.Name, user.Role, user.IsActive, null));
    }
}
