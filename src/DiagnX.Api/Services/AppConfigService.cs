using DiagnX.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Services;

/// <summary>Key-value runtime settings (app_config table), editable by admins without a release.</summary>
public sealed class AppConfigService(AppDbContext db)
{
    public const string MaxDocBytes = "maxDocBytes";
    public const string MaxSelfieBytes = "maxSelfieBytes";
    public const string KycTatLabel = "kycTatLabel";
    public const string KycTatBusinessDays = "kycTatBusinessDays";
    public const string SupportEmail = "supportEmail";
    public const string SupportPhone = "supportPhone";
    public const string AgreementVersion = "agreementVersion";
    public const string PatientSupportEmail = "patientSupportEmail";
    public const string PatientSupportPhone = "patientSupportPhone";
    public const string BookingWindowDays = "bookingWindowDays";
    public const string MaxReportAttachmentBytes = "maxReportAttachmentBytes";

    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        [MaxDocBytes] = "5242880",
        [MaxSelfieBytes] = "15728640",
        [KycTatLabel] = "1-2 business days",
        [KycTatBusinessDays] = "2",
        [SupportEmail] = "partners@diagnx.in",
        [SupportPhone] = "+91 80000 12345",
        [AgreementVersion] = "v1.0",
        [PatientSupportEmail] = "care@diagnx.in",
        [PatientSupportPhone] = "+91 80000 12346",
        [BookingWindowDays] = "14",
        [MaxReportAttachmentBytes] = "10485760",
    };

    private Dictionary<string, string>? _cache;

    public async Task<Dictionary<string, string>> AllAsync()
    {
        if (_cache != null) return _cache;
        var rows = await db.AppConfig.AsNoTracking().ToDictionaryAsync(c => c.Key, c => c.Value);
        foreach (var (k, v) in Defaults) rows.TryAdd(k, v);
        return _cache = rows;
    }

    public async Task<string> GetAsync(string key) => (await AllAsync()).TryGetValue(key, out var v) ? v : "";

    public async Task<int> GetIntAsync(string key) =>
        int.TryParse(await GetAsync(key), out var v) ? v : int.Parse(Defaults[key]);
}
