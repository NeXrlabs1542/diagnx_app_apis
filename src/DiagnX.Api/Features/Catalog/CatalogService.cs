using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Features.Catalog;

public sealed record GeoPoint(double Latitude, double Longitude);

/// <summary>Everything the patient app browses: categories, tests, lab comparison, lab profiles, slots.</summary>
public sealed class CatalogService(AppDbContext db)
{
    // ---------------------------------------------------------------- categories + tests

    public async Task<List<CategoryDto>> CategoriesAsync(bool homeOnly) =>
        await db.TestCategories.AsNoTracking().Where(c => c.IsActive && (!homeOnly || c.ShowOnHome))
            .OrderBy(c => c.SortOrder).Select(c => new CategoryDto(c.Id, c.Slug, c.Name, c.Icon, c.Color)).ToListAsync();

    private sealed record PriceStat(Guid TestId, decimal MinPrice, decimal MinMrp, int Count);

    private async Task<Dictionary<Guid, PriceStat>> PriceStatsAsync(IEnumerable<Guid>? testIds = null)
    {
        var q = db.LabTests.AsNoTracking().Where(lt => lt.IsActive && lt.Lab.IsActive && lt.Lab.AcceptingBookings);
        if (testIds != null)
        {
            var ids = testIds.ToList();
            q = q.Where(lt => ids.Contains(lt.TestId));
        }
        return await q.GroupBy(lt => lt.TestId)
            .Select(g => new PriceStat(g.Key, g.Min(x => x.Price), g.Min(x => x.Mrp), g.Count()))
            .ToDictionaryAsync(s => s.TestId);
    }

    /// <summary>Search tests / packages. <paramref name="category"/> accepts a category id or slug.</summary>
    public async Task<List<TestSummaryDto>> SearchTestsAsync(string? q, string? category, string? type, bool? popular, int limit = 100)
    {
        var query = db.Tests.AsNoTracking().Include(t => t.Category).Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = await db.TestCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == category || c.Id.ToString() == category);
            if (cat == null) return new List<TestSummaryDto>();
            query = query.Where(t => t.CategoryId == cat.Id || t.CategoryLinks.Any(l => l.CategoryId == cat.Id));
        }
        if (type == "test") query = query.Where(t => !t.IsPackage);
        else if (type == "package") query = query.Where(t => t.IsPackage);
        if (popular == true) query = query.Where(t => t.IsPopular);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(t => EF.Functions.ILike(t.Name, term) || EF.Functions.ILike(t.Category.Name, term) ||
                                     EF.Functions.ILike(t.Description, term) || t.IncludedTests.Any(i => EF.Functions.ILike(i, term)));
        }

        var tests = await query.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).Take(Math.Clamp(limit, 1, 200)).ToListAsync();
        var stats = await PriceStatsAsync(tests.Select(t => t.Id));
        return tests.Select(t => ToSummary(t, stats.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<DiagnosticTest> FindTestAsync(string idOrSlug) =>
        await db.Tests.AsNoTracking().Include(t => t.Category).Include(t => t.Parameters)
            .FirstOrDefaultAsync(t => t.IsActive && (t.Slug == idOrSlug || t.Id.ToString() == idOrSlug))
        ?? throw ApiException.NotFound("Test not found");

    public async Task<TestDetailDto> TestDetailAsync(string idOrSlug)
    {
        var t = await FindTestAsync(idOrSlug);
        var stats = await PriceStatsAsync(new[] { t.Id });
        return new TestDetailDto(ToSummary(t, stats.GetValueOrDefault(t.Id)),
            t.Parameters.OrderBy(p => p.SortOrder).Select(p => new ParameterDto(p.Id, p.Name, p.Unit, p.ReferenceRange)).ToList());
    }

    private static TestSummaryDto ToSummary(DiagnosticTest t, PriceStat? s) => new(
        t.Id, t.Slug, t.Name, new CategoryDto(t.Category.Id, t.Category.Slug, t.Category.Name, t.Category.Icon, t.Category.Color),
        t.FastingRequired, t.SampleType, t.ParameterCount, t.Description, t.IsPackage, t.IncludedTests,
        s?.MinPrice, s?.MinMrp, s?.Count ?? 0);

    // ---------------------------------------------------------------- labs

    /// <summary>Active labs with what the cards need. Small at pilot scale, so filtering / sorting happens in memory.</summary>
    private async Task<List<Lab>> ActiveLabsAsync(Guid? labId = null)
    {
        var q = db.Labs.AsNoTracking().Include(l => l.Operations).Include(l => l.Licences).Include(l => l.ServiceablePincodes)
            .Where(l => l.IsActive);
        if (labId != null) q = q.Where(l => l.Id == labId);
        return await q.AsSplitQuery().ToListAsync();
    }

    public static LabCardDto ToCard(Lab l, GeoPoint? from)
    {
        var accreditation = new List<string>();
        if (l.HasNabl == true || l.Licences.Any(x => x.LicenceType == LicenceTypes.Nabl)) accreditation.Add("NABL");
        if (l.IsoCertified) accreditation.Add("ISO");
        return new LabCardDto(
            l.Id, l.DisplayName, Initials(l.DisplayName), l.LogoColor, accreditation, l.RatingAvg, l.RatingCount,
            DistanceKm(from, l), $"{l.Area}, {l.City}", l.TurnaroundHours, l.Operations?.HomeCollection ?? false, l.WalkIn,
            l.IsActive, l.EstablishedYear);
    }

    public async Task<List<LabCardDto>> LabsAsync(string? q, string? sort, GeoPoint? from, string? city, int limit = 50)
    {
        var labs = await ActiveLabsAsync();
        IEnumerable<Lab> list = labs;
        if (!string.IsNullOrWhiteSpace(q))
            list = list.Where(l => l.DisplayName.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                   l.Area.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(city)) list = list.Where(l => l.City.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase));
        var cards = list.Select(l => ToCard(l, from));
        cards = sort switch
        {
            "distance" => cards.OrderBy(c => c.DistanceKm ?? double.MaxValue),
            "turnaround" => cards.OrderBy(c => c.TurnaroundHours),
            _ => cards.OrderByDescending(c => c.Rating).ThenByDescending(c => c.ReviewCount),
        };
        return cards.Take(Math.Clamp(limit, 1, 100)).ToList();
    }

    /// <summary>"Compare &amp; Book": every lab offering the test, sortable by price / rating / distance / turnaround.</summary>
    public async Task<List<LabOfferDto>> OffersAsync(string testIdOrSlug, string? sort, GeoPoint? from, string? pincode)
    {
        var test = await FindTestAsync(testIdOrSlug);
        var prices = await db.LabTests.AsNoTracking().Where(lt => lt.TestId == test.Id && lt.IsActive).ToListAsync();
        var labs = (await ActiveLabsAsync()).Where(l => l.AcceptingBookings).ToDictionary(l => l.Id);

        var offers = prices.Where(p => labs.ContainsKey(p.LabId)).Select(p =>
        {
            var lab = labs[p.LabId];
            var card = ToCard(lab, from);
            return new LabOfferDto(card, test.Id, p.Price, p.Mrp, DiscountPercent(p.Mrp, p.Price),
                card.HomeCollection && Serves(lab, pincode));
        });
        return (sort switch
        {
            "rating" => offers.OrderByDescending(o => o.Lab.Rating),
            "distance" => offers.OrderBy(o => o.Lab.DistanceKm ?? double.MaxValue),
            "turnaround" => offers.OrderBy(o => o.Lab.TurnaroundHours),
            _ => offers.OrderBy(o => o.Price),
        }).ToList();
    }

    public async Task<LabDetailDto> LabDetailAsync(Guid labId, GeoPoint? from)
    {
        var lab = (await ActiveLabsAsync(labId)).FirstOrDefault() ?? throw ApiException.NotFound("Lab not found");
        var days = await db.LabWorkingDays.AsNoTracking().Where(d => d.LabId == labId).Select(d => d.DayCode).ToListAsync();

        var labTests = await db.LabTests.AsNoTracking().Include(lt => lt.Test).ThenInclude(t => t.Category)
            .Where(lt => lt.LabId == labId && lt.IsActive && lt.Test.IsActive)
            .OrderBy(lt => lt.Test.IsPackage).ThenBy(lt => lt.Test.SortOrder).ToListAsync();
        var stats = await PriceStatsAsync(labTests.Select(lt => lt.TestId));

        var fullAddress = string.Join(", ", new[] { lab.AddressLine1, lab.AddressLine2, lab.Landmark, lab.Area, lab.City, lab.State, lab.Pincode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        return new LabDetailDto(
            ToCard(lab, from), fullAddress, lab.Latitude, lab.Longitude,
            lab.Operations == null ? null : IstClock.FormatTime(lab.Operations.OpenTime),
            lab.Operations == null ? null : IstClock.FormatTime(lab.Operations.CloseTime),
            Kyc.KycService.SortDays(days),
            lab.ServiceablePincodes.Select(p => p.Pincode).OrderBy(p => p).ToList(),
            labTests.Select(lt => new LabTestPriceDto(ToSummary(lt.Test, stats.GetValueOrDefault(lt.TestId)), lt.Price, lt.Mrp)).ToList(),
            await ReviewsAsync(labId, 1, 5));
    }

    public async Task<List<LabReviewDto>> ReviewsAsync(Guid labId, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var rows = await (from r in db.Reviews.AsNoTracking()
                          join p in db.Patients.AsNoTracking() on r.PatientId equals p.Id
                          where r.LabId == labId
                          orderby r.CreatedAt descending
                          select new { r.Id, p.Name, r.LabRating, r.Comment, r.CreatedAt })
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync();
        return rows.Select(r =>
        {
            var name = ShortName(r.Name);
            return new LabReviewDto(r.Id, name, Initials(name), r.LabRating, r.Comment, r.CreatedAt, Verified: true);
        }).ToList();
    }

    // ---------------------------------------------------------------- slots

    /// <summary>Slots for a lab on a date. A slot is unavailable when it has started / is too close, or the lab is full.</summary>
    public async Task<DaySlotsDto> SlotsAsync(Guid labId, DateOnly date, string mode)
    {
        var lab = await db.Labs.AsNoTracking().Include(l => l.Operations).FirstOrDefaultAsync(l => l.Id == labId && l.IsActive)
                  ?? throw ApiException.NotFound("Lab not found");
        var slots = await db.TimeSlots.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();
        var counts = await SlotCountsAsync(labId, date);

        var dayCode = date.DayOfWeek.ToString()[..3];
        var labOpenToday = await db.LabWorkingDays.AnyAsync(d => d.LabId == labId && d.DayCode == dayCode)
                           || !await db.LabWorkingDays.AnyAsync(d => d.LabId == labId);

        var result = slots.Where(s => mode == CollectionModes.Home ? s.HomeCollection : s.WalkIn).Select(s =>
        {
            var used = counts.GetValueOrDefault(s.Id);
            var remaining = Math.Max(0, lab.SlotCapacity - used);
            var available = labOpenToday && remaining > 0 && IsBookableTime(date, s.StartTime) && WithinHours(lab, s);
            return new SlotDto(s.Id, s.Label, s.Period, IstClock.FormatTime(s.StartTime), available, available ? remaining : 0);
        }).ToList();
        return new DaySlotsDto(IstClock.FormatDate(date), result);
    }

    public async Task<Dictionary<Guid, int>> SlotCountsAsync(Guid labId, DateOnly date)
    {
        // Unpaid bookings hold a slot for 15 minutes.
        var holdCutoff = DateTime.UtcNow.AddMinutes(-15);
        return await db.Bookings.AsNoTracking()
            .Where(b => b.LabId == labId && b.ScheduledDate == date && b.Status != BookingStatus.Cancelled &&
                        (b.Status != BookingStatus.PaymentPending || b.CreatedAt > holdCutoff))
            .GroupBy(b => b.SlotId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    /// <summary>Bookable if the slot starts at least 60 minutes from now (IST).</summary>
    public static bool IsBookableTime(DateOnly date, TimeOnly start) =>
        IstClock.ToUtc(date, start) > DateTime.UtcNow.AddMinutes(60);

    public static bool WithinHours(Lab lab, TimeSlot slot) =>
        lab.Operations == null || (slot.StartTime >= lab.Operations.OpenTime && slot.StartTime < lab.Operations.CloseTime);

    // ---------------------------------------------------------------- helpers

    public static bool Serves(Lab lab, string? pincode) =>
        string.IsNullOrEmpty(pincode) || lab.ServiceablePincodes.Count == 0 || lab.ServiceablePincodes.Any(p => p.Pincode == pincode);

    public static int DiscountPercent(decimal mrp, decimal price) => mrp <= 0 || price >= mrp ? 0 : (int)Math.Round((mrp - price) / mrp * 100);

    public static double? DistanceKm(GeoPoint? from, Lab lab)
    {
        if (from == null || lab.Latitude == null || lab.Longitude == null) return null;
        const double r = 6371;
        double Rad(double d) => d * Math.PI / 180;
        var lat1 = Rad(from.Latitude);
        var lat2 = Rad((double)lab.Latitude.Value);
        var dLat = lat2 - lat1;
        var dLng = Rad((double)lab.Longitude.Value - from.Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return Math.Round(r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 1);
    }

    public static string Initials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetter(w[0])).ToList();
        return words.Count switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}",
        };
    }

    /// <summary>"Ravi Kumar" -> "Ravi K." for public reviews.</summary>
    public static string ShortName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "DiagnX user";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? parts[0] : $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }
}
