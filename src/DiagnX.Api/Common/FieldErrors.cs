namespace DiagnX.Api.Common;

/// <summary>Collects field errors (first error per field wins) and throws them as one 422.</summary>
public sealed class FieldErrors
{
    private readonly List<FieldError> _errors = new();

    public bool Any => _errors.Count > 0;
    public IReadOnlyList<FieldError> Items => _errors;

    public FieldErrors Add(string field, string code, string message)
    {
        if (_errors.All(e => e.Field != field)) _errors.Add(new FieldError(field, code, message));
        return this;
    }

    public void AddRange(IEnumerable<FieldError> errors)
    {
        foreach (var e in errors) Add(e.Field, e.Code, e.Message);
    }

    /// <summary>Required text with a minimum trimmed length.</summary>
    public FieldErrors RequireText(string field, string? value, int min = 1, int max = int.MaxValue, string? message = null)
    {
        var len = value?.Trim().Length ?? 0;
        if (len < min) Add(field, ErrorCodes.Required, message ?? "This field is required");
        else if (len > max) Add(field, ErrorCodes.TooLong, $"Keep this under {max} characters");
        return this;
    }

    public FieldErrors MaxLength(string field, string? value, int max)
    {
        if (value != null && value.Trim().Length > max) Add(field, ErrorCodes.TooLong, $"Keep this under {max} characters");
        return this;
    }

    public void ThrowIfAny(string message = "Please correct the highlighted fields")
    {
        if (Any) throw ApiException.Validation(_errors, message);
    }
}
