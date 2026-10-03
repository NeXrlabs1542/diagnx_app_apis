using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace DiagnX.Api.Common;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Ok<T>(T data) =>
        base.Ok(new ApiResponse<T>(true, data, HttpContext.TraceIdentifier));

    protected ActionResult<ApiResponse<SuccessResult>> Success() =>
        base.Ok(new ApiResponse<SuccessResult>(true, new SuccessResult(true), HttpContext.TraceIdentifier));

    /// <summary>Id of the logged-in patient / partner / admin (JWT "sub").</summary>
    protected Guid UserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : throw ApiException.Unauthorized();
        }
    }

    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
    protected string? DeviceId => Request.Headers["X-Device-Id"].FirstOrDefault();
}

public sealed record SuccessResult(bool Success);
