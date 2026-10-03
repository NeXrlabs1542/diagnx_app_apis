using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.Admin;

/// <summary>Back-office login (email + password). The first admin is seeded from Admin__Email / Admin__Password.</summary>
[Route("api/v1/admin/auth")]
[ApiExplorerSettings(GroupName = "admin")]
public sealed class AdminAuthController(AppDbContext db, TokenService tokens, IPasswordHasher<AdminUser> hasher) : ApiControllerBase
{
    public sealed record LoginRequest(string? Email, string? Password);
    public sealed record AdminLoginResult(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, AdminDto Admin);
    public sealed record AdminDto(Guid Id, string Email, string Name, string Role);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AdminLoginResult>>> Login(LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == email);
        if (admin is not { IsActive: true } ||
            hasher.VerifyHashedPassword(admin, admin.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
        {
            await Task.Delay(Random.Shared.Next(200, 400)); // blunt timing / brute-force probes
            throw new ApiException(401, ErrorCodes.InvalidCredentials, "Incorrect email or password");
        }

        admin.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var pair = await tokens.IssueAsync(UserTypes.Admin, admin.Id, new SessionContext(DeviceId, "WEB", null), admin.Role);
        return Ok(new AdminLoginResult(pair.AccessToken, pair.RefreshToken, pair.TokenType, pair.ExpiresIn,
            new AdminDto(admin.Id, admin.Email, admin.Name, admin.Role)));
    }

    [HttpPost("token/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TokenPair>>> Refresh(RefreshRequest req) =>
        Ok((await tokens.RefreshAsync(req.RefreshToken ?? "", UserTypes.Admin)).Tokens);

    [HttpPost("logout")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> Logout(LogoutRequest? req)
    {
        await tokens.RevokeAsync(UserTypes.Admin, UserId, req?.RefreshToken);
        return Success();
    }
}
