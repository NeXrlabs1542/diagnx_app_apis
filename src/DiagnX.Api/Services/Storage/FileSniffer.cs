using System.Text;

namespace DiagnX.Api.Services.Storage;

/// <summary>
/// Detects the real file type from its first bytes instead of trusting the client's
/// Content-Type / extension. Returns null for anything we don't accept.
/// </summary>
public static class FileSniffer
{
    private static readonly string[] HeifBrands = { "heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1" };

    public static string? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return "image/png";
        if (b.Length >= 5 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46 && b[4] == 0x2D) return "application/pdf";
        if (b.Length >= 12 && Encoding.ASCII.GetString(b.Slice(4, 4)) == "ftyp" &&
            HeifBrands.Contains(Encoding.ASCII.GetString(b.Slice(8, 4)))) return "image/heic";
        return null;
    }

    public static string Extension(string mime) => mime switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/heic" => ".heic",
        "application/pdf" => ".pdf",
        _ => "",
    };
}
