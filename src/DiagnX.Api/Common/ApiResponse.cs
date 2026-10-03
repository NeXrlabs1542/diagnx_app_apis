using System.Text.Json.Serialization;

namespace DiagnX.Api.Common;

/// <summary>Success envelope: { success: true, data, requestId } (see API spec "Conventions").</summary>
public sealed record ApiResponse<T>(bool Success, T Data, string RequestId);

/// <summary>Error envelope: { success: false, error: {...}, requestId }.</summary>
public sealed record ApiErrorResponse(bool Success, ApiError Error, string RequestId);

public sealed class ApiError
{
    public required string Code { get; init; }
    public required string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FieldError>? Fields { get; init; }

    /// <summary>Only on 422 INCOMPLETE_APPLICATION — the KYC step (1-6) the app should jump to.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FirstInvalidStep { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }
}

/// <summary>A per-input error. <c>Field</c> is the JSON field name so the app can highlight that input.</summary>
public sealed record FieldError(string Field, string Code, string Message);

/// <summary>Paged list wrapper used by every list endpoint.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
