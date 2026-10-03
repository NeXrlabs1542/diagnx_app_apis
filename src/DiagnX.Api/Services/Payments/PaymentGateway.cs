namespace DiagnX.Api.Services.Payments;

public sealed record GatewayOrder(string Gateway, string GatewayOrderId, decimal Amount, string Currency, string? PublicKey);

public sealed record GatewayVerification(bool Success, string? GatewayPaymentId, string? FailureReason);

/// <summary>
/// Payment gateway abstraction. <see cref="MockPaymentGateway"/> is used until Razorpay is
/// wired: implement this interface with Razorpay Orders + signature verification and register
/// it in Program.cs — the booking / payment endpoints stay the same.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }
    Task<GatewayOrder> CreateOrderAsync(Guid paymentId, decimal amount, string receipt);
    Task<GatewayVerification> VerifyAsync(string gatewayOrderId, string? gatewayPaymentId, string? signature);
    Task<bool> RefundAsync(string gatewayPaymentId, decimal amount);
}

/// <summary>
/// Accepts every payment instantly. For testing failures, send gatewayPaymentId = "fail".
/// </summary>
public sealed class MockPaymentGateway : IPaymentGateway
{
    public string Name => "MOCK";

    public Task<GatewayOrder> CreateOrderAsync(Guid paymentId, decimal amount, string receipt) =>
        Task.FromResult(new GatewayOrder(Name, $"mock_order_{paymentId:N}", amount, "INR", null));

    public Task<GatewayVerification> VerifyAsync(string gatewayOrderId, string? gatewayPaymentId, string? signature) =>
        Task.FromResult(gatewayPaymentId == "fail"
            ? new GatewayVerification(false, null, "Payment declined (mock)")
            : new GatewayVerification(true, gatewayPaymentId ?? $"mock_pay_{Guid.NewGuid():N}", null));

    public Task<bool> RefundAsync(string gatewayPaymentId, decimal amount) => Task.FromResult(true);
}
