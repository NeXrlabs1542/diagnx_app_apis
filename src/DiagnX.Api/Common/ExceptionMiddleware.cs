using System.Text.Json;

namespace DiagnX.Api.Common;

/// <summary>
/// Converts every exception into the standard error envelope and stamps each response
/// with an X-Request-Id header (also echoed as requestId in the body).
/// </summary>
public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 64) requestId = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = requestId;
        context.Response.Headers["X-Request-Id"] = requestId;

        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            if (ex.RetryAfterSeconds is { } retry) context.Response.Headers.RetryAfter = retry.ToString();
            await Write(context, ex.StatusCode, new ApiError
            {
                Code = ex.Code,
                Message = ex.Message,
                Fields = ex.Fields,
                FirstInvalidStep = ex.FirstInvalidStep,
                Details = ex.Details,
            });
        }
        catch (BadHttpRequestException ex)
        {
            await Write(context, 400, new ApiError { Code = ErrorCodes.BadRequest, Message = ex.Message });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nothing to write.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error for {Method} {Path} (requestId {RequestId})",
                context.Request.Method, context.Request.Path, requestId);
            await Write(context, 500, new ApiError
            {
                Code = ErrorCodes.ServerError,
                Message = "Something went wrong on our side. Please try again.",
            });
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task Write(HttpContext context, int status, ApiError error)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new ApiErrorResponse(false, error, context.TraceIdentifier), Json));
    }
}
