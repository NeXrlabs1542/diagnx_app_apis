namespace DiagnX.Api.Common;

public static class Masking
{
    private const char Dot = '•';

    /// <summary>ABCPS1234D -> ABCPS••••D (first 5 + last char visible).</summary>
    public static string Pan(string? pan) =>
        string.IsNullOrEmpty(pan) || pan.Length != 10 ? "" : pan[..5] + new string(Dot, 4) + pan[9];

    /// <summary>Last 4 digits -> "•••• •••• 0124".</summary>
    public static string Aadhaar(string? last4) =>
        string.IsNullOrEmpty(last4) ? "" : $"{new string(Dot, 4)} {new string(Dot, 4)} {last4}";

    /// <summary>Last 4 digits -> "••••9012".</summary>
    public static string Account(string? last4) => string.IsNullOrEmpty(last4) ? "" : new string(Dot, 4) + last4;

    /// <summary>9876543210 -> XXXXXX3210.</summary>
    public static string Phone(string phone) => phone.Length < 4 ? phone : new string('X', phone.Length - 4) + phone[^4..];

    public static string Last4(string value) => value.Length <= 4 ? value : value[^4..];
}
