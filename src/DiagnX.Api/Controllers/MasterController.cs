using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers;

/// <summary>MST-01..04 — dropdowns, runtime config and lookups shared by both apps.</summary>
[Route("api/v1/master")]
[ApiExplorerSettings(GroupName = "master")]
public sealed class MasterController(AppDbContext db, AppConfigService config, LookupService lookups) : ApiControllerBase
{
    public sealed record OptionDto(string Code, string Label, string? Hint);

    /// <summary>MST-01 · Dropdown values grouped by code. ?groups=STATE,BUSINESS_TYPE (omit for all).</summary>
    [HttpGet("options")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<OptionDto>>>>> Options([FromQuery] string? groups)
    {
        var wanted = (groups ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(g => g.ToUpperInvariant()).ToHashSet();

        var result = new Dictionary<string, List<OptionDto>>();
        var options = await db.MasterOptions.AsNoTracking().Where(o => o.IsActive)
            .OrderBy(o => o.GroupCode).ThenBy(o => o.SortOrder).ToListAsync();
        foreach (var g in options.GroupBy(o => o.GroupCode))
            if (wanted.Count == 0 || wanted.Contains(g.Key))
                result[g.Key] = g.Select(o => new OptionDto(o.Code, o.Label, o.Hint)).ToList();

        if (wanted.Count == 0 || wanted.Contains("STATE"))
            result["STATE"] = await db.MasterStates.AsNoTracking().OrderBy(s => s.Name)
                .Select(s => new OptionDto(s.Name, s.Name, null)).ToListAsync();

        if (wanted.Count == 0 || wanted.Contains("TIME_SLOT"))
        {
            // 05:00 .. 23:30 every 30 minutes (38 values), displayed as 5:00 AM .. 11:30 PM.
            result["TIME_SLOT"] = Enumerable.Range(0, 38)
                .Select(i => new TimeOnly(5, 0).AddMinutes(30 * i))
                .Select(t => new OptionDto(t.ToString("HH:mm"), t.ToString("h:mm tt"), null)).ToList();
        }
        return Ok(result);
    }

    public sealed record ConfigDto(
        int MaxDocBytes, int MaxSelfieBytes, int OtpLength, int PatientOtpLength, string KycTatLabel, int KycTatBusinessDays,
        string SupportEmail, string SupportPhone, string AgreementVersion, string PatientSupportEmail, string PatientSupportPhone,
        int BookingWindowDays);

    /// <summary>MST-02 · Runtime settings: upload limits, OTP length, KYC turnaround, support contacts, agreement version.</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ConfigDto>>> Config([FromServices] Microsoft.Extensions.Options.IOptions<Services.Auth.OtpOptions> otp)
    {
        return Ok(new ConfigDto(
            await config.GetIntAsync(AppConfigService.MaxDocBytes),
            await config.GetIntAsync(AppConfigService.MaxSelfieBytes),
            otp.Value.PartnerLength,
            otp.Value.PatientLength,
            await config.GetAsync(AppConfigService.KycTatLabel),
            await config.GetIntAsync(AppConfigService.KycTatBusinessDays),
            await config.GetAsync(AppConfigService.SupportEmail),
            await config.GetAsync(AppConfigService.SupportPhone),
            await config.GetAsync(AppConfigService.AgreementVersion),
            await config.GetAsync(AppConfigService.PatientSupportEmail),
            await config.GetAsync(AppConfigService.PatientSupportPhone),
            await config.GetIntAsync(AppConfigService.BookingWindowDays)));
    }

    /// <summary>MST-04 · PIN code → areas + city + state (India Post, cached).</summary>
    [HttpGet("pincode/{pincode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PincodeResult>>> Pincode(string pincode, CancellationToken ct) =>
        Ok(await lookups.PincodeAsync(pincode, ct));

    /// <summary>MST-03 · IFSC → bank name + branch (auto-fill on the bank step).</summary>
    [HttpGet("ifsc/{ifsc}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IfscResult>>> Ifsc(string ifsc, CancellationToken ct) =>
        Ok(await lookups.IfscAsync(ifsc, ct));
}
