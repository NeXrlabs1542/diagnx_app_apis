using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.PartnerApp;

[Route("api/v1/partner")]
[ApiExplorerSettings(GroupName = "partner")]
[Authorize(Roles = Roles.Partner)]
public sealed class PartnerController(AppDbContext db, DeviceTokenService devices) : ApiControllerBase
{
    public sealed record PartnerMeDto(
        Guid PartnerId, string Phone, string KycStatus, short? KycCurrentStep, string? KycReferenceId, Guid? LabId, string? LabName, bool LabIsLive);

    /// <summary>PRT-01 · Called on app start to decide the first screen (KYC intro / form / pending / dashboard).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<PartnerMeDto>>> Me()
    {
        var p = await db.Partners.AsNoTracking().Include(x => x.Application).ThenInclude(a => a!.Lab)
                    .FirstOrDefaultAsync(x => x.Id == UserId) ?? throw ApiException.Unauthorized();
        var lab = p.Application?.Lab;
        return Ok(new PartnerMeDto(p.Id, p.Phone, p.KycStatus, PartnerAuthController.CurrentStep(p), p.Application?.ReferenceId,
            lab?.Id, lab?.DisplayName, lab?.IsActive ?? false));
    }

    /// <summary>PRT-02 · Register / refresh the device push token.</summary>
    [HttpPost("devices")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> Device(DeviceTokenRequest req)
    {
        await devices.RegisterAsync(UserTypes.Partner, UserId, req.PushToken, req.Platform);
        return Success();
    }
}
