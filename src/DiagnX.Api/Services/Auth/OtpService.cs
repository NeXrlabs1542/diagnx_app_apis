using System.Security.Cryptography;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services.Security;
using DiagnX.Api.Services.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiagnX.Api.Services.Auth;

public sealed class OtpOptions
{
    /// <summary>When true no SMS is sent and the fixed dev codes below always work.</summary>
    public bool DevMode { get; set; } = true;
    /// <summary>Return the OTP in the send response as devOtp (only honoured in DevMode).</summary>
    public bool ExposeInResponse { get; set; } = true;
    public string DevCodePatient { get; set; } = "1234";
    public string DevCodePartner { get; set; } = "123456";
    /// <summary>Patient app shows 4 boxes, partner app 6.</summary>
    public int PatientLength { get; set; } = 4;
    public int PartnerLength { get; set; } = 6;
    public int ExpirySeconds { get; set; } = 300;
    public int ResendAfterSeconds { get; set; } = 30;
    public int MaxResends { get; set; } = 3;
    public int MaxAttempts { get; set; } = 5;
    public int MaxSendsPerPhonePerHour { get; set; } = 5;
    public int MaxSendsPerIpPerHour { get; set; } = 20;
}

public sealed record OtpSendResult(
    Guid OtpRequestId, string MaskedPhone, int ExpiresInSeconds, int ResendAfterSeconds, int OtpLength, string? DevOtp);

public sealed record OtpResendResult(int ExpiresInSeconds, int ResendAfterSeconds, int ResendsLeft, string? DevOtp);

/// <summary>AUTH-01/02/03 for both apps. OTPs are stored only as an HMAC hash.</summary>
public sealed class OtpService(AppDbContext db, SecretKeys keys, ISmsSender sms, IOptions<OtpOptions> options)
{
    private readonly OtpOptions _opt = options.Value;

    public int LengthFor(string audience) => audience == UserTypes.Patient ? _opt.PatientLength : _opt.PartnerLength;

    public async Task<OtpSendResult> SendAsync(string audience, string rawPhone, string? deviceId, string? ip)
    {
        var phone = IndianIds.NormalisePhone(rawPhone);
        if (!IndianIds.IsMobile(phone))
            throw ApiException.Field("phone", ErrorCodes.InvalidPhone, "Enter a valid 10-digit mobile number");

        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var byPhone = await db.OtpRequests.CountAsync(o => o.Phone == phone && o.Audience == audience && o.CreatedAt > hourAgo);
        if (byPhone >= _opt.MaxSendsPerPhonePerHour)
            throw new ApiException(429, ErrorCodes.RateLimited, "Too many OTP requests. Please try again in a while.") { RetryAfterSeconds = 900 };
        if (ip != null)
        {
            var byIp = await db.OtpRequests.CountAsync(o => o.IpAddress == ip && o.CreatedAt > hourAgo);
            if (byIp >= _opt.MaxSendsPerIpPerHour)
                throw new ApiException(429, ErrorCodes.RateLimited, "Too many OTP requests. Please try again in a while.") { RetryAfterSeconds = 900 };
        }

        var now = DateTime.UtcNow;
        var request = new OtpRequest
        {
            Id = Guid.NewGuid(),
            Audience = audience,
            Phone = phone,
            ExpiresAt = now.AddSeconds(_opt.ExpirySeconds),
            LastSentAt = now,
            DeviceId = deviceId is { Length: > 80 } ? deviceId[..80] : deviceId,
            IpAddress = ip,
            CreatedAt = now,
        };
        var code = NewCode(audience);
        request.OtpHash = Hash(request.Id, code);
        db.OtpRequests.Add(request);
        await db.SaveChangesAsync();

        await DeliverAsync(phone, code, audience);
        return new OtpSendResult(request.Id, Masking.Phone(phone), _opt.ExpirySeconds, _opt.ResendAfterSeconds,
            LengthFor(audience), ExposedCode(code));
    }

    public async Task<OtpResendResult> ResendAsync(string audience, Guid otpRequestId)
    {
        var request = await db.OtpRequests.FirstOrDefaultAsync(o => o.Id == otpRequestId && o.Audience == audience)
                      ?? throw ApiException.NotFound("OTP request not found. Please request a new code.");
        var now = DateTime.UtcNow;
        if (request.VerifiedAt != null || request.ExpiresAt < now)
            throw new ApiException(410, ErrorCodes.OtpExpired, "This code has expired. Please request a new one.");
        var wait = (int)Math.Ceiling((request.LastSentAt.AddSeconds(_opt.ResendAfterSeconds) - now).TotalSeconds);
        if (wait > 0)
            throw new ApiException(429, ErrorCodes.RateLimited, $"Please wait {wait}s before requesting a new code.") { RetryAfterSeconds = wait };
        if (request.ResendCount >= _opt.MaxResends)
            throw new ApiException(429, ErrorCodes.RateLimited, "Resend limit reached. Please start again.");

        var code = NewCode(audience);
        request.OtpHash = Hash(request.Id, code);
        request.ResendCount++;
        request.Attempts = 0;
        request.LastSentAt = now;
        request.ExpiresAt = now.AddSeconds(_opt.ExpirySeconds);
        await db.SaveChangesAsync();

        await DeliverAsync(request.Phone, code, audience);
        return new OtpResendResult(_opt.ExpirySeconds, _opt.ResendAfterSeconds, _opt.MaxResends - request.ResendCount, ExposedCode(code));
    }

    /// <summary>Returns the verified phone number, or throws OTP_INVALID / OTP_EXPIRED / OTP_ATTEMPTS_EXCEEDED.</summary>
    public async Task<string> VerifyAsync(string audience, Guid otpRequestId, string? otp)
    {
        var request = await db.OtpRequests.FirstOrDefaultAsync(o => o.Id == otpRequestId && o.Audience == audience)
                      ?? throw new ApiException(401, ErrorCodes.OtpInvalid, "Incorrect code. Please try again.");
        var now = DateTime.UtcNow;
        if (request.VerifiedAt != null || request.ExpiresAt < now)
            throw new ApiException(410, ErrorCodes.OtpExpired, "This code has expired. Please request a new one.");
        if (request.Attempts >= _opt.MaxAttempts)
            throw new ApiException(429, ErrorCodes.OtpAttemptsExceeded, "Too many wrong attempts. Please request a new code.");

        var code = (otp ?? "").Trim();
        var expected = Hash(request.Id, code);
        if (code.Length != LengthFor(audience) ||
            !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(expected), System.Text.Encoding.ASCII.GetBytes(request.OtpHash)))
        {
            request.Attempts++;
            await db.SaveChangesAsync();
            var left = Math.Max(0, _opt.MaxAttempts - request.Attempts);
            if (left == 0)
                throw new ApiException(429, ErrorCodes.OtpAttemptsExceeded, "Too many wrong attempts. Please request a new code.");
            throw new ApiException(401, ErrorCodes.OtpInvalid, "Incorrect code. Please try again.") { Details = new { attemptsLeft = left } };
        }

        request.VerifiedAt = now;
        await db.SaveChangesAsync();
        return request.Phone;
    }

    private string NewCode(string audience)
    {
        if (_opt.DevMode) return audience == UserTypes.Patient ? _opt.DevCodePatient : _opt.DevCodePartner;
        var length = LengthFor(audience);
        return RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, length)).ToString().PadLeft(length, '0');
    }

    private string? ExposedCode(string code) => _opt.DevMode && _opt.ExposeInResponse ? code : null;

    private Task DeliverAsync(string phone, string code, string audience)
    {
        var app = audience == UserTypes.Patient ? "DiagnX" : "DiagnX Partner";
        return sms.SendAsync(phone, $"{code} is your {app} verification code. It expires in {_opt.ExpirySeconds / 60} minutes. Do not share it with anyone.");
    }

    private string Hash(Guid requestId, string code) => SecretKeys.Hmac(keys.OtpHashKey, $"{requestId:N}:{code}");
}
