using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.Admin;

/// <summary>DiagnX's master catalog (categories, tests + standard report parameters), home banners and health tips.</summary>
[Route("api/v1/admin")]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminCatalogController(AppDbContext db) : ApiControllerBase
{
    // ---------------------------------------------------------------- categories

    public sealed class CategoryRequest
    {
        public string? Slug { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool ShowOnHome { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<TestCategory>>>> Categories() =>
        Ok(await db.TestCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync());

    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<TestCategory>>> AddCategory(CategoryRequest r) => Ok(await SaveCategory(null, r));

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult<ApiResponse<TestCategory>>> UpdateCategory(Guid id, CategoryRequest r) => Ok(await SaveCategory(id, r));

    private async Task<TestCategory> SaveCategory(Guid? id, CategoryRequest r)
    {
        var e = new FieldErrors();
        e.RequireText("slug", r.Slug, 2, 60);
        e.RequireText("name", r.Name, 2, 80);
        e.ThrowIfAny();
        var slug = r.Slug!.Trim().ToLowerInvariant();
        if (await db.TestCategories.AnyAsync(c => c.Slug == slug && c.Id != id))
            throw ApiException.Field("slug", ErrorCodes.InvalidValue, "Slug already in use");

        TestCategory c;
        if (id == null) db.TestCategories.Add(c = new TestCategory { Id = Guid.NewGuid() });
        else c = await db.TestCategories.FindAsync(id) ?? throw ApiException.NotFound();
        c.Slug = slug;
        c.Name = r.Name!.Trim();
        c.Icon = r.Icon ?? "";
        c.Color = r.Color ?? "";
        c.ShowOnHome = r.ShowOnHome;
        c.SortOrder = (short)r.SortOrder;
        c.IsActive = r.IsActive;
        await db.SaveChangesAsync();
        return c;
    }

    // ---------------------------------------------------------------- tests

    public sealed class ParameterInput
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public string? ReferenceRange { get; set; }
        public decimal? RefLow { get; set; }
        public decimal? RefHigh { get; set; }
        public string ValueType { get; set; } = "numeric";
    }

    public sealed class TestRequest
    {
        public string? Slug { get; set; }
        public string? Name { get; set; }
        public Guid CategoryId { get; set; }
        public List<Guid>? ExtraCategoryIds { get; set; }
        public bool FastingRequired { get; set; }
        public string? SampleType { get; set; }
        public string? Description { get; set; }
        public bool IsPackage { get; set; }
        public List<string>? IncludedTests { get; set; }
        public int ParameterCount { get; set; }
        public bool IsPopular { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>The standard report template, in display order. Replaces the existing list.</summary>
        public List<ParameterInput>? Parameters { get; set; }
    }

    public sealed record ParameterView(Guid Id, string Name, string Unit, string ReferenceRange, decimal? RefLow, decimal? RefHigh, string ValueType, int SortOrder);

    public sealed record TestView(
        Guid Id, string Slug, string Name, Guid CategoryId, IReadOnlyList<Guid> ExtraCategoryIds, bool FastingRequired, string SampleType,
        string Description, bool IsPackage, IReadOnlyList<string> IncludedTests, int ParameterCount, bool IsPopular, int SortOrder, bool IsActive,
        IReadOnlyList<ParameterView> Parameters, int LabsOffering);

    private async Task<TestView> View(Guid id)
    {
        var t = await db.Tests.AsNoTracking().Include(x => x.Parameters).Include(x => x.CategoryLinks).FirstAsync(x => x.Id == id);
        return new TestView(t.Id, t.Slug, t.Name, t.CategoryId, t.CategoryLinks.Select(l => l.CategoryId).ToList(), t.FastingRequired,
            t.SampleType, t.Description, t.IsPackage, t.IncludedTests, t.ParameterCount, t.IsPopular, t.SortOrder, t.IsActive,
            t.Parameters.OrderBy(p => p.SortOrder)
                .Select(p => new ParameterView(p.Id, p.Name, p.Unit, p.ReferenceRange, p.RefLow, p.RefHigh, p.ValueType, p.SortOrder)).ToList(),
            await db.LabTests.CountAsync(lt => lt.TestId == id && lt.IsActive));
    }

    [HttpGet("tests")]
    public async Task<ActionResult<ApiResponse<List<TestView>>>> Tests()
    {
        var ids = await db.Tests.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Name).Select(t => t.Id).ToListAsync();
        var list = new List<TestView>();
        foreach (var id in ids) list.Add(await View(id));
        return Ok(list);
    }

    [HttpGet("tests/{id:guid}")]
    public async Task<ActionResult<ApiResponse<TestView>>> Test(Guid id) =>
        await db.Tests.AnyAsync(t => t.Id == id) ? Ok(await View(id)) : throw ApiException.NotFound();

    [HttpPost("tests")]
    public async Task<ActionResult<ApiResponse<TestView>>> AddTest(TestRequest r) => Ok(await View(await SaveTest(null, r)));

    [HttpPut("tests/{id:guid}")]
    public async Task<ActionResult<ApiResponse<TestView>>> UpdateTest(Guid id, TestRequest r) => Ok(await View(await SaveTest(id, r)));

    private async Task<Guid> SaveTest(Guid? id, TestRequest r)
    {
        var e = new FieldErrors();
        e.RequireText("slug", r.Slug, 2, 60);
        e.RequireText("name", r.Name, 2, 150);
        e.RequireText("sampleType", r.SampleType, 2, 60);
        e.MaxLength("description", r.Description, 1000);
        if (!await db.TestCategories.AnyAsync(c => c.Id == r.CategoryId)) e.Add("categoryId", ErrorCodes.InvalidValue, "Unknown category");
        var parameters = r.Parameters ?? new List<ParameterInput>();
        for (var i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            if (string.IsNullOrWhiteSpace(p.Name)) e.Add($"parameters[{i}].name", ErrorCodes.Required, "Parameter name is required");
            if (p.ValueType is not ("numeric" or "text")) e.Add($"parameters[{i}].valueType", ErrorCodes.InvalidValue, "numeric or text");
            if (p.RefLow != null && p.RefHigh != null && p.RefLow > p.RefHigh) e.Add($"parameters[{i}].refLow", ErrorCodes.InvalidValue, "Low must be ≤ high");
        }
        e.ThrowIfAny();
        var slug = r.Slug!.Trim().ToLowerInvariant();
        if (await db.Tests.AnyAsync(t => t.Slug == slug && t.Id != id)) throw ApiException.Field("slug", ErrorCodes.InvalidValue, "Slug already in use");

        var now = DateTime.UtcNow;
        DiagnosticTest t;
        if (id == null) db.Tests.Add(t = new DiagnosticTest { Id = Guid.NewGuid(), CreatedAt = now });
        else t = await db.Tests.Include(x => x.Parameters).Include(x => x.CategoryLinks).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();

        t.Slug = slug;
        t.Name = r.Name!.Trim();
        t.CategoryId = r.CategoryId;
        t.FastingRequired = r.FastingRequired;
        t.SampleType = r.SampleType!.Trim();
        t.Description = r.Description?.Trim() ?? "";
        t.IsPackage = r.IsPackage;
        t.IncludedTests = r.IncludedTests ?? new List<string>();
        t.IsPopular = r.IsPopular;
        t.SortOrder = (short)r.SortOrder;
        t.IsActive = r.IsActive;
        t.UpdatedAt = now;

        // Parameters: update by id, add new, drop missing (old reports keep their own snapshot).
        var keep = new HashSet<Guid>();
        for (short i = 0; i < parameters.Count; i++)
        {
            var input = parameters[i];
            var p = input.Id == null ? null : t.Parameters.FirstOrDefault(x => x.Id == input.Id);
            if (p == null)
            {
                p = new TestParameter { Id = Guid.NewGuid(), TestId = t.Id };
                db.TestParameters.Add(p);
                t.Parameters.Add(p);
            }
            p.Name = input.Name!.Trim();
            p.Unit = input.Unit?.Trim() ?? "";
            p.ReferenceRange = input.ReferenceRange?.Trim() ?? "";
            p.RefLow = input.RefLow;
            p.RefHigh = input.RefHigh;
            p.ValueType = input.ValueType;
            p.SortOrder = i;
            keep.Add(p.Id);
        }
        if (r.Parameters != null)
            foreach (var old in t.Parameters.Where(p => !keep.Contains(p.Id)).ToList()) { t.Parameters.Remove(old); db.TestParameters.Remove(old); }
        t.ParameterCount = (short)Math.Max(r.ParameterCount, t.Parameters.Count);

        var extra = (r.ExtraCategoryIds ?? new List<Guid>()).Where(c => c != r.CategoryId).Distinct().ToList();
        foreach (var link in t.CategoryLinks.Where(l => !extra.Contains(l.CategoryId)).ToList()) { t.CategoryLinks.Remove(link); db.Remove(link); }
        foreach (var c in extra.Where(c => t.CategoryLinks.All(l => l.CategoryId != c)))
        {
            var link = new TestCategoryLink { TestId = t.Id, CategoryId = c };
            db.Add(link);
            t.CategoryLinks.Add(link);
        }
        await db.SaveChangesAsync();
        return t.Id;
    }

    // ---------------------------------------------------------------- banners + tips

    [HttpGet("banners")]
    public async Task<ActionResult<ApiResponse<List<PromoBanner>>>> Banners() =>
        Ok(await db.PromoBanners.AsNoTracking().OrderBy(b => b.SortOrder).ToListAsync());

    [HttpPost("banners")]
    public async Task<ActionResult<ApiResponse<PromoBanner>>> AddBanner(PromoBanner b)
    {
        b.Id = Guid.NewGuid();
        ValidateBanner(b);
        db.PromoBanners.Add(b);
        await db.SaveChangesAsync();
        return Ok(b);
    }

    [HttpPut("banners/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PromoBanner>>> UpdateBanner(Guid id, PromoBanner input)
    {
        ValidateBanner(input);
        var b = await db.PromoBanners.FindAsync(id) ?? throw ApiException.NotFound();
        input.Id = id;
        db.Entry(b).CurrentValues.SetValues(input);
        await db.SaveChangesAsync();
        return Ok(b);
    }

    [HttpDelete("banners/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> DeleteBanner(Guid id)
    {
        if (await db.PromoBanners.Where(b => b.Id == id).ExecuteDeleteAsync() == 0) throw ApiException.NotFound();
        return Success();
    }

    private static void ValidateBanner(PromoBanner b)
    {
        var e = new FieldErrors();
        e.RequireText("title", b.Title, 2, 80);
        e.RequireText("subtitle", b.Subtitle, 0, 150);
        e.ThrowIfAny();
    }

    [HttpGet("health-tips")]
    public async Task<ActionResult<ApiResponse<List<HealthTip>>>> Tips() =>
        Ok(await db.HealthTips.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync());

    [HttpPost("health-tips")]
    public async Task<ActionResult<ApiResponse<HealthTip>>> AddTip(HealthTip t)
    {
        t.Id = Guid.NewGuid();
        ValidateTip(t);
        db.HealthTips.Add(t);
        await db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpPut("health-tips/{id:guid}")]
    public async Task<ActionResult<ApiResponse<HealthTip>>> UpdateTip(Guid id, HealthTip input)
    {
        ValidateTip(input);
        var t = await db.HealthTips.FindAsync(id) ?? throw ApiException.NotFound();
        input.Id = id;
        db.Entry(t).CurrentValues.SetValues(input);
        await db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpDelete("health-tips/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> DeleteTip(Guid id)
    {
        if (await db.HealthTips.Where(t => t.Id == id).ExecuteDeleteAsync() == 0) throw ApiException.NotFound();
        return Success();
    }

    private static void ValidateTip(HealthTip t)
    {
        var e = new FieldErrors();
        e.RequireText("title", t.Title, 2, 120);
        e.RequireText("body", t.Body, 2, 500);
        e.ThrowIfAny();
    }
}
