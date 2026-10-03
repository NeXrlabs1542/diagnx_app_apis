using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.PartnerApp;

/// <summary>AUTH-01..05 for the partner (lab) app. The same flow registers new labs and logs in returning ones.</summary>
[Route("api/v1/partner/auth")]
[ApiExplorerSettings(GroupName = "partner")]
public sealed class PartnerAuthController(
    AppDbContext db, OtpService otp, TokenService tokens, DeviceTokenService devices) : ApiControllerBase
{
    /// <summary>AUTH-01 · Send a 6-digit OTP (registers the number on first use).</summary>
    [HttpPost("otp/send")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OtpSendResult>>> Send(OtpSendRequest req) =>
        Ok(await otp.SendAsync(UserTypes.Partner, req.Phone ?? "", DeviceId, ClientIp));

    /// <summary>AUTH-03 · Resend the OTP (after the 30 s cool-down, max 3 times).</summary>
    [HttpPost("otp/resend")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OtpResendResult>>> Resend(OtpResendRequest req) =>
        Ok(await otp.ResendAsync(UserTypes.Partner, req.OtpRequestId));

    /// <summary>AUTH-02 · Verify the OTP; creates the partner (and an empty KYC application) on first login.</summary>
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PartnerLoginResult>>> Verify(OtpVerifyRequest req)
    {
        var phone = await otp.VerifyAsync(UserTypes.Partner, req.OtpRequestId, req.Otp);
        var now = DateTime.UtcNow;
        var partner = await db.Partners.Include(p => p.Application).FirstOrDefaultAsync(p => p.Phone == phone);
        var isNew = partner == null;
        if (partner == null)
        {
            partner = new Partner { Id = Guid.NewGuid(), Phone = phone, CreatedAt = now, UpdatedAt = now };
            partner.Application = new KycApplication
            {
                Id = Guid.NewGuid(), PartnerId = partner.Id, Status = KycStatus.Draft, CurrentStep = 1, CreatedAt = now, UpdatedAt = now,
            };
            db.Partners.Add(partner);
        }
        if (partner.Status != "ACTIVE") throw new ApiException(403, ErrorCodes.AccountBlocked, "This account is blocked. Please contact support.");
        partner.LastLoginAt = now;
        await db.SaveChangesAsync();

        var pair = await tokens.IssueAsync(UserTypes.Partner, partner.Id, new SessionContext(DeviceId, req.Platform, req.AppVersion));
        return Ok(new PartnerLoginResult(pair.AccessToken, pair.RefreshToken, pair.TokenType, pair.ExpiresIn,
            new PartnerSummary(partner.Id, partner.Phone, isNew, partner.KycStatus, CurrentStep(partner))));
    }

    /// <summary>AUTH-04 · Exchange a refresh token for a new pair (the old refresh token is invalidated).</summary>
    [HttpPost("token/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TokenPair>>> Refresh(RefreshRequest req) =>
        Ok((await tokens.RefreshAsync(req.RefreshToken ?? "", UserTypes.Partner)).Tokens);

    /// <summary>AUTH-05 · Log out: revoke the refresh token and deactivate push tokens.</summary>
    [HttpPost("logout")]
    [Authorize(Roles = Roles.Partner)]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> Logout(LogoutRequest? req)
    {
        await tokens.RevokeAsync(UserTypes.Partner, UserId, req?.RefreshToken);
        await devices.DeactivateAllAsync(UserTypes.Partner, UserId);
        return Success();
    }

    internal static short? CurrentStep(Partner p) =>
        p.KycStatus == PartnerKycStatus.NotStarted ? null : p.Application?.CurrentStep;
}

public sealed record PartnerSummary(Guid PartnerId, string Phone, bool IsNewPartner, string KycStatus, short? KycCurrentStep);

public sealed record PartnerLoginResult(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, PartnerSummary Partner);
