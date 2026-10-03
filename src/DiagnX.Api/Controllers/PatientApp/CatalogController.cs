using DiagnX.Api.Common;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiagnX.Api.Controllers.PatientApp;

/// <summary>Browse, search and compare — public (no login needed) so a website can use it too.</summary>
[Route("api/v1/patient")]
[ApiExplorerSettings(GroupName = "patient")]
[AllowAnonymous]
public sealed class CatalogController(CatalogService catalog) : ApiControllerBase
{
    /// <summary>Categories. ?home=true returns only the "Browse by category" strip.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> Categories([FromQuery] bool home = false) =>
        Ok(await catalog.CategoriesAsync(home));

    /// <summary>Search tests &amp; packages. ?q=thyroid&amp;category=vitamins&amp;type=test|package&amp;popular=true</summary>
    [HttpGet("tests")]
    public async Task<ActionResult<ApiResponse<List<TestSummaryDto>>>> Tests(
        [FromQuery] string? q, [FromQuery] string? category, [FromQuery] string? type, [FromQuery] bool? popular, [FromQuery] int limit = 100) =>
        Ok(await catalog.SearchTestsAsync(q, category, type, popular, limit));

    /// <summary>Test detail with the standard report parameters. Accepts id or slug (e.g. test-cbc).</summary>
    [HttpGet("tests/{idOrSlug}")]
    public async Task<ActionResult<ApiResponse<TestDetailDto>>> Test(string idOrSlug) => Ok(await catalog.TestDetailAsync(idOrSlug));

    /// <summary>
    /// "Compare &amp; Book" — every lab offering the test. ?sort=price|rating|distance|turnaround&amp;lat=&amp;lng=&amp;pincode=
    /// (pincode sets homeCollectionAvailable per lab).
    /// </summary>
    [HttpGet("tests/{idOrSlug}/labs")]
    public async Task<ActionResult<ApiResponse<List<LabOfferDto>>>> Offers(
        string idOrSlug, [FromQuery] string? sort, [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] string? pincode) =>
        Ok(await catalog.OffersAsync(idOrSlug, sort, Geo(lat, lng), pincode));

    /// <summary>Labs list. ?sort=rating|distance|turnaround&amp;q=&amp;city=&amp;lat=&amp;lng=</summary>
    [HttpGet("labs")]
    public async Task<ActionResult<ApiResponse<List<LabCardDto>>>> Labs(
        [FromQuery] string? q, [FromQuery] string? sort, [FromQuery] string? city, [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] int limit = 50) =>
        Ok(await catalog.LabsAsync(q, sort, Geo(lat, lng), city, limit));

    /// <summary>Lab profile: accreditation, hours, tests offered with prices, recent reviews.</summary>
    [HttpGet("labs/{labId:guid}")]
    public async Task<ActionResult<ApiResponse<LabDetailDto>>> Lab(Guid labId, [FromQuery] double? lat, [FromQuery] double? lng) =>
        Ok(await catalog.LabDetailAsync(labId, Geo(lat, lng)));

    [HttpGet("labs/{labId:guid}/reviews")]
    public async Task<ActionResult<ApiResponse<List<LabReviewDto>>>> Reviews(Guid labId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await catalog.ReviewsAsync(labId, page, pageSize));

    /// <summary>Time slots for a date. ?date=YYYY-MM-DD&amp;mode=home|walkin</summary>
    [HttpGet("labs/{labId:guid}/slots")]
    public async Task<ActionResult<ApiResponse<DaySlotsDto>>> Slots(Guid labId, [FromQuery] string? date, [FromQuery] string? mode)
    {
        if (!IstClock.TryParseDate(date, out var d)) throw ApiException.Field("date", ErrorCodes.InvalidDate, "Use YYYY-MM-DD");
        return Ok(await catalog.SlotsAsync(labId, d, mode == CollectionModes.WalkIn ? CollectionModes.WalkIn : CollectionModes.Home));
    }

    internal static GeoPoint? Geo(double? lat, double? lng) =>
        lat is >= -90 and <= 90 && lng is >= -180 and <= 180 ? new GeoPoint(lat.Value, lng.Value) : null;
}
