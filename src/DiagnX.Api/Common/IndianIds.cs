using System.Text.RegularExpressions;

namespace DiagnX.Api.Common;

/// <summary>
/// Format + checksum validators for the Indian identifiers KYC asks for. These mirror
/// src/utils/validators.ts in the partner app exactly (spec sheet "Validation Rules").
/// </summary>
public static partial class IndianIds
{
    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")] private static partial Regex PanRx();
    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")] private static partial Regex GstinRx();
    [GeneratedRegex("^[A-Z]{4}0[A-Z0-9]{6}$")] private static partial Regex IfscRx();
    [GeneratedRegex("^[LU][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}$")] private static partial Regex CinRx();
    [GeneratedRegex("^[A-Z]{3}-[0-9]{4}$")] private static partial Regex LlpinRx();
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")] private static partial Regex EmailRx();
    [GeneratedRegex("^[1-9][0-9]{5}$")] private static partial Regex PincodeRx();
    [GeneratedRegex("^[6-9][0-9]{9}$")] private static partial Regex MobileRx();
    [GeneratedRegex("^[1-9][0-9]{9}$")] private static partial Regex LabPhoneRx();
    [GeneratedRegex("^[0-9]{9,18}$")] private static partial Regex AccountRx();
    [GeneratedRegex(@"^(https?://)?([a-z0-9-]+\.)+[a-z]{2,}(/\S*)?$", RegexOptions.IgnoreCase)] private static partial Regex WebsiteRx();
    [GeneratedRegex("^[2-9][0-9]{11}$")] private static partial Regex AadhaarRx();

    public static bool IsPan(string? v) => v != null && PanRx().IsMatch(v);
    public static bool IsIfsc(string? v) => v != null && IfscRx().IsMatch(v);
    public static bool IsCin(string? v) => v != null && CinRx().IsMatch(v);
    public static bool IsLlpin(string? v) => v != null && LlpinRx().IsMatch(v);
    public static bool IsEmail(string? v) => v != null && EmailRx().IsMatch(v.Trim());
    public static bool IsPincode(string? v) => v != null && PincodeRx().IsMatch(v);
    /// <summary>Indian mobile (login): starts 6-9.</summary>
    public static bool IsMobile(string? v) => v != null && MobileRx().IsMatch(v);
    /// <summary>Lab contact number: mobile or STD + landline.</summary>
    public static bool IsLabPhone(string? v) => v != null && LabPhoneRx().IsMatch(v);
    public static bool IsAccountNumber(string? v) => v != null && AccountRx().IsMatch(v);
    public static bool IsWebsite(string? v) => v != null && WebsiteRx().IsMatch(v.Trim());

    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>GSTIN: pattern, valid state code and the mod-36 check character (VR-03).</summary>
    public static bool IsGstin(string? v)
    {
        if (v == null || !GstinRx().IsMatch(v)) return false;
        var state = int.Parse(v[..2]);
        if (!((state >= 1 && state <= 38) || state == 97 || state == 99)) return false;

        var sum = 0;
        for (var i = 0; i < 14; i++)
        {
            var product = Base36.IndexOf(v[i]) * (i % 2 == 0 ? 1 : 2);
            sum += product / 36 + product % 36;
        }
        return Base36[(36 - sum % 36) % 36] == v[14];
    }

    /// <summary>A GSTIN embeds the holder's PAN in characters 3-12.</summary>
    public static bool GstinMatchesPan(string gstin, string pan) => gstin.Length >= 12 && gstin.Substring(2, 10) == pan;

    private static readonly int[,] D =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 }, { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
        { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 }, { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 }, { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
        { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 }, { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 }, { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
        { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
    };

    private static readonly int[,] P =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 }, { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
        { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 }, { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 }, { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
        { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 }, { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
    };

    /// <summary>Aadhaar: 12 digits, first digit 2-9, valid Verhoeff checksum (VR-06).</summary>
    public static bool IsAadhaar(string? digits)
    {
        if (digits == null || !AadhaarRx().IsMatch(digits)) return false;
        var c = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i] - '0';
            c = D[c, P[i % 8, digit]];
        }
        return c == 0;
    }

    /// <summary>4th PAN character allowed for each business type (VR-02).</summary>
    public static readonly IReadOnlyDictionary<string, char[]> PanEntityChars = new Dictionary<string, char[]>
    {
        ["proprietorship"] = new[] { 'P' },
        ["partnership"] = new[] { 'F' },
        ["llp"] = new[] { 'F' },
        ["pvt_ltd"] = new[] { 'C' },
        ["public_ltd"] = new[] { 'C' },
        ["trust"] = new[] { 'T', 'A', 'B' },
    };

    public static string DigitsOnly(string? v) => v == null ? "" : new string(v.Where(char.IsDigit).ToArray());

    /// <summary>Normalises "+91 98765 43210" / "919876543210" / "9876543210" to 10 digits.</summary>
    public static string NormalisePhone(string? v)
    {
        var d = DigitsOnly(v);
        if (d.Length == 12 && d.StartsWith("91")) d = d[2..];
        if (d.Length == 11 && d.StartsWith('0')) d = d[1..];
        return d;
    }
}
