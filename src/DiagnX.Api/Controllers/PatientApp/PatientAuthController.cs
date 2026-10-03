using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.PatientApp;

/// <summary>Patient login: phone → 4-digit OTP → tokens. First login creates the patient.</summary>
[Route("api/v1/patient/auth")]
[ApiExplorerSettings(GroupName = "patient")]
public sealed class PatientAuthController(
    AppDbContext db, OtpService otp, TokenService tokens, DeviceTokenService devices) : ApiControllerBase
{
    [HttpPost("otp/send")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OtpSendResult>>> Send(OtpSendRequest req) =>
        Ok(await otp.SendAsync(UserTypes.Patient, req.Phone ?? "", DeviceId, ClientIp));

    [HttpPost("otp/resend")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OtpResendResult>>> Resend(OtpResendRequest req) =>
        Ok(await otp.ResendAsync(UserTypes.Patient, req.OtpRequestId));

    [HttpPost("otp/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PatientLoginResult>>> Verify(OtpVerifyRequest req)
    {
        var phone = await otp.VerifyAsync(UserTypes.Patient, req.OtpRequestId, req.Otp);
        var now = DateTime.UtcNow;
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Phone == phone);
        var isNew = patient == null;
        if (patient == null)
        {
            patient = new Patient { Id = Guid.NewGuid(), Phone = phone, CreatedAt = now, UpdatedAt = now };
            db.Patients.Add(patient);
        }
        if (patient.Status != "ACTIVE") throw new ApiException(403, ErrorCodes.AccountBlocked, "This account is blocked. Please contact support.");
        patient.LastLoginAt = now;
        await db.SaveChangesAsync();

        var pair = await tokens.IssueAsync(UserTypes.Patient, patient.Id, new SessionContext(DeviceId, req.Platform, req.AppVersion));
        return Ok(new PatientLoginResult(pair.AccessToken, pair.RefreshToken, pair.TokenType, pair.ExpiresIn, isNew,
            ProfileController.ToDto(patient)));
    }

    [HttpPost("token/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TokenPair>>> Refresh(RefreshRequest req) =>
        Ok((await tokens.RefreshAsync(req.RefreshToken ?? "", UserTypes.Patient)).Tokens);

    [HttpPost("logout")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> Logout(LogoutRequest? req)
    {
        await tokens.RevokeAsync(UserTypes.Patient, UserId, req?.RefreshToken);
        await devices.DeactivateAllAsync(UserTypes.Patient, UserId);
        return Success();
    }
}

public sealed record PatientLoginResult(
    string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, bool IsNewUser, PatientProfileDto Patient);
