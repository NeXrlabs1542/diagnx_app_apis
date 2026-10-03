using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DiagnX.Api.Services.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "diagnx-api";
    public string Audience { get; set; } = "diagnx-apps";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 60;
}

public static class Roles
{
    public const string Patient = "patient";
    public const string Partner = "partner";
    public const string Admin = "admin";
}

public sealed record TokenPair(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);

public sealed record SessionContext(string? DeviceId, string? Platform, string? AppVersion);

/// <summary>Issues short-lived JWT access tokens + rotating opaque refresh tokens (stored hashed).</summary>
public sealed class TokenService(AppDbContext db, SecretKeys keys, Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public static string RoleFor(string userType) => userType switch
    {
        UserTypes.Patient => Roles.Patient,
        UserTypes.Partner => Roles.Partner,
        _ => Roles.Admin,
    };

    public async Task<TokenPair> IssueAsync(string userType, Guid userId, SessionContext ctx, string? adminRole = null)
    {
        var refresh = NewRefreshToken();
        var now = DateTime.UtcNow;
        db.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            UserType = userType,
            UserId = userId,
            RefreshTokenHash = HashRefresh(refresh),
            DeviceId = Trim(ctx.DeviceId, 80),
            DevicePlatform = Trim(ctx.Platform?.ToUpperInvariant(), 10),
            AppVersion = Trim(ctx.AppVersion, 20),
            ExpiresAt = now.AddDays(_opt.RefreshTokenDays),
            CreatedAt = now,
            LastUsedAt = now,
        });
        await db.SaveChangesAsync();
        return new TokenPair(CreateAccessToken(userType, userId, adminRole), refresh, "Bearer", _opt.AccessTokenMinutes * 60);
    }

    /// <summary>AUTH-04: rotate the refresh token. The old one stops working immediately.</summary>
    public async Task<(TokenPair Tokens, AuthSession Session)> RefreshAsync(string refreshToken, string expectedUserType)
    {
        var hash = HashRefresh(refreshToken ?? "");
        var session = await db.AuthSessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == hash);
        if (session == null || session.RevokedAt != null || session.UserType != expectedUserType)
            throw new ApiException(401, ErrorCodes.Unauthorized, "Your session has ended. Please log in again.");
        if (session.ExpiresAt < DateTime.UtcNow)
            throw new ApiException(401, ErrorCodes.TokenExpired, "Your session has expired. Please log in again.");

        string? adminRole = null;
        if (session.UserType == UserTypes.Admin)
        {
            var admin = await db.AdminUsers.FindAsync(session.UserId);
            if (admin is not { IsActive: true }) throw ApiException.Unauthorized();
            adminRole = admin.Role;
        }

        var refresh = NewRefreshToken();
        session.RefreshTokenHash = HashRefresh(refresh);
        session.LastUsedAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.AddDays(_opt.RefreshTokenDays);
        await db.SaveChangesAsync();
        return (new TokenPair(CreateAccessToken(session.UserType, session.UserId, adminRole), refresh, "Bearer", _opt.AccessTokenMinutes * 60), session);
    }

    /// <summary>AUTH-05: revoke the given refresh token, or every session of the user when none is given.</summary>
    public async Task RevokeAsync(string userType, Guid userId, string? refreshToken)
    {
        var now = DateTime.UtcNow;
        var query = db.AuthSessions.Where(s => s.UserType == userType && s.UserId == userId && s.RevokedAt == null);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var hash = HashRefresh(refreshToken);
            query = query.Where(s => s.RefreshTokenHash == hash);
        }
        foreach (var s in await query.ToListAsync()) s.RevokedAt = now;
        await db.SaveChangesAsync();
    }

    private string CreateAccessToken(string userType, Guid userId, string? adminRole)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Role, RoleFor(userType)),
        };
        if (adminRole != null) claims.Add(new Claim("admin_role", adminRole));

        var creds = new SigningCredentials(new SymmetricSecurityKey(keys.JwtSigningKey), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(_opt.AccessTokenMinutes), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    private string HashRefresh(string token) => SecretKeys.Hmac(keys.TokenHashKey, token);
    private static string? Trim(string? v, int max) => v == null ? null : v.Length > max ? v[..max] : v;
}
