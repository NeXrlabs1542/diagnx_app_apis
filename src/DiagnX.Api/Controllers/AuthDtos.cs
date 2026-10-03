namespace DiagnX.Api.Controllers;

public sealed record OtpSendRequest(string? Phone, string? CountryCode);
public sealed record OtpVerifyRequest(Guid OtpRequestId, string? Otp, string? Platform, string? AppVersion);
public sealed record OtpResendRequest(Guid OtpRequestId);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
public sealed record DeviceTokenRequest(string? PushToken, string? Platform);
