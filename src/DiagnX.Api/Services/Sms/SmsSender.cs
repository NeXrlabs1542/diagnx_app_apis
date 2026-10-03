namespace DiagnX.Api.Services.Sms;

/// <summary>
/// SMS gateway abstraction. Only the dev (log-only) sender exists today; add an
/// MSG91 / Twilio implementation and register it in Program.cs when going live.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(string phone, string message);
}

public sealed class LogSmsSender(ILogger<LogSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string message)
    {
        logger.LogInformation("[DEV SMS] to XXXXXX{Last4}: {Message}", phone[^4..], message);
        return Task.CompletedTask;
    }
}
