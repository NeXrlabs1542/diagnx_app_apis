using System.Net;
using System.Text.Json;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Services;

public sealed record PincodeResult(string Pincode, IReadOnlyList<string> Areas, string City, string State);
public sealed record IfscResult(string Ifsc, string BankName, string BranchName, string City);

/// <summary>
/// MST-04 (PIN code → areas / city / state, India Post, cached in the DB) and
/// MST-03 (IFSC → bank / branch, Razorpay's free public IFSC API).
/// </summary>
public sealed class LookupService(AppDbContext db, IHttpClientFactory httpFactory, ILogger<LookupService> logger)
{
    public const string IndiaPostClient = "indiapost";
    public const string IfscClient = "ifsc";

    // India Post spells some states / UTs differently from master_states (same map as the partner app).
    private static readonly Dictionary<string, string> StateAliases = new()
    {
        ["andaman & nicobar islands"] = "Andaman and Nicobar Islands",
        ["dadra & nagar haveli"] = "Dadra and Nagar Haveli and Daman and Diu",
        ["daman & diu"] = "Dadra and Nagar Haveli and Daman and Diu",
        ["dadra and nagar haveli"] = "Dadra and Nagar Haveli and Daman and Diu",
        ["daman and diu"] = "Dadra and Nagar Haveli and Daman and Diu",
        ["jammu & kashmir"] = "Jammu and Kashmir",
        ["nct of delhi"] = "Delhi",
        ["orissa"] = "Odisha",
        ["pondicherry"] = "Puducherry",
        ["uttaranchal"] = "Uttarakhand",
    };

    public async Task<PincodeResult> PincodeAsync(string pincode, CancellationToken ct)
    {
        if (!IndianIds.IsPincode(pincode))
            throw ApiException.Field("pincode", ErrorCodes.InvalidPincode, "Enter a valid 6-digit PIN code");

        var cached = await db.PincodeCache.AsNoTracking().FirstOrDefaultAsync(p => p.Pincode == pincode, ct);
        if (cached != null && cached.FetchedAt > DateTime.UtcNow.AddDays(-90))
            return new PincodeResult(pincode, cached.Areas, cached.City, cached.State);

        try
        {
            var http = httpFactory.CreateClient(IndiaPostClient);
            using var res = await http.GetAsync($"pincode/{pincode}", ct);
            if (!res.IsSuccessStatusCode) throw Upstream();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var first = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0 ? doc.RootElement[0] : default;
            if (first.ValueKind != JsonValueKind.Object || first.GetProperty("Status").GetString() != "Success" ||
                !first.TryGetProperty("PostOffice", out var offices) || offices.ValueKind != JsonValueKind.Array || offices.GetArrayLength() == 0)
                throw ApiException.NotFound("We couldn't find this PIN code. Please type the area manually.");

            var areas = offices.EnumerateArray().Select(o => o.GetProperty("Name").GetString()?.Trim() ?? "")
                .Where(a => a.Length > 0).Distinct().ToList();
            var city = offices[0].GetProperty("District").GetString()?.Trim() ?? "";
            var state = await NormaliseStateAsync(offices[0].GetProperty("State").GetString(), ct);

            var row = await db.PincodeCache.FirstOrDefaultAsync(p => p.Pincode == pincode, ct);
            if (row == null) db.PincodeCache.Add(row = new PincodeCache { Pincode = pincode });
            row.Areas = areas;
            row.City = city;
            row.State = state;
            row.FetchedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new PincodeResult(pincode, areas, city, state);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "India Post lookup failed for {Pincode}", pincode);
            if (cached != null) return new PincodeResult(pincode, cached.Areas, cached.City, cached.State);
            throw Upstream();
        }
    }

    public async Task<IfscResult> IfscAsync(string ifsc, CancellationToken ct)
    {
        ifsc = ifsc.Trim().ToUpperInvariant();
        if (!IndianIds.IsIfsc(ifsc))
            throw ApiException.Field("ifsc", ErrorCodes.InvalidIfsc, "Enter a valid 11-character IFSC, e.g. HDFC0001234");
        try
        {
            var http = httpFactory.CreateClient(IfscClient);
            using var res = await http.GetAsync(ifsc, ct);
            if (res.StatusCode == HttpStatusCode.NotFound) throw ApiException.NotFound("IFSC not found. Please check the code.");
            if (!res.IsSuccessStatusCode) throw Upstream();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            string Get(string name) => doc.RootElement.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
            return new IfscResult(ifsc, Get("BANK"), Get("BRANCH"), Get("CITY"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "IFSC lookup failed for {Ifsc}", ifsc);
            throw Upstream();
        }
    }

    private async Task<string> NormaliseStateAsync(string? raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var key = raw.Trim().ToLowerInvariant();
        if (StateAliases.TryGetValue(key, out var alias)) return alias;
        var flat = key.Replace("&", "and");
        var names = await db.MasterStates.AsNoTracking().Select(s => s.Name).ToListAsync(ct);
        return names.FirstOrDefault(n => n.Equals(flat, StringComparison.OrdinalIgnoreCase)) ?? "";
    }

    private static ApiException Upstream() =>
        new(502, ErrorCodes.UpstreamUnavailable, "Lookup service is unavailable right now. Please enter the details manually.");
}
