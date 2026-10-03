using DiagnX.Api.Common;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Features.LabOps;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiagnX.Api.Controllers.PartnerApp;

/// <summary>
/// Post-approval lab operations (the partner app's "Unlocks after approval" screens):
/// dashboard, test menu &amp; pricing, phlebotomists, orders, sample collection and result entry.
/// Every endpoint returns 403 KYC_NOT_APPROVED until the lab's KYC is approved.
/// </summary>
[Route("api/v1/partner")]
[ApiExplorerSettings(GroupName = "partner")]
[Authorize(Roles = Roles.Partner)]
public sealed class LabOpsController(LabOpsService ops) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> Dashboard() => Ok(await ops.DashboardAsync(UserId));

    [HttpGet("lab")]
    public async Task<ActionResult<ApiResponse<LabProfileDto>>> Lab() => Ok(LabOpsService.ToProfile(await ops.LabAsync(UserId)));

    /// <summary>Update marketplace settings (accepting bookings, turnaround, slot capacity, hours, serviceable PIN codes…). Send only what changes.</summary>
    [HttpPatch("lab/settings")]
    public async Task<ActionResult<ApiResponse<LabProfileDto>>> Settings(LabSettingsRequest req) => Ok(await ops.UpdateSettingsAsync(UserId, req));

    // ---- test menu

    /// <summary>DiagnX master catalog with this lab's enrolment + price. ?enrolled=true for the lab's menu only.</summary>
    [HttpGet("catalog")]
    public async Task<ActionResult<ApiResponse<List<CatalogTestDto>>>> Catalog([FromQuery] string? q, [FromQuery] string? category, [FromQuery] bool enrolled = false) =>
        Ok(await ops.CatalogAsync(UserId, q, category, enrolled));

    /// <summary>Enrol in a catalog test or update its price (MRP ≥ price).</summary>
    [HttpPut("tests/{testIdOrSlug}")]
    public async Task<ActionResult<ApiResponse<CatalogTestDto>>> SetPrice(string testIdOrSlug, LabTestPriceRequest req) =>
        Ok(await ops.SetPriceAsync(UserId, testIdOrSlug, req));

    [HttpDelete("tests/{testIdOrSlug}")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> RemoveTest(string testIdOrSlug)
    {
        await ops.RemoveTestAsync(UserId, testIdOrSlug);
        return Success();
    }

    // ---- phlebotomists

    [HttpGet("phlebotomists")]
    public async Task<ActionResult<ApiResponse<List<PhlebotomistDto>>>> Phlebotomists() => Ok(await ops.PhlebotomistsAsync(UserId));

    [HttpPost("phlebotomists")]
    public async Task<ActionResult<ApiResponse<PhlebotomistDto>>> AddPhlebotomist(PhlebotomistRequest req) =>
        Ok(await ops.SavePhlebotomistAsync(UserId, null, req));

    /// <summary>Edit a phlebotomist (set isActive=false to deactivate).</summary>
    [HttpPut("phlebotomists/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PhlebotomistDto>>> UpdatePhlebotomist(Guid id, PhlebotomistRequest req) =>
        Ok(await ops.SavePhlebotomistAsync(UserId, id, req));

    // ---- orders

    /// <summary>Orders. ?status=confirmed,enroute&amp;date=YYYY-MM-DD&amp;page=1&amp;pageSize=20. Each row lists its nextActions.</summary>
    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<PagedResult<PartnerBookingDto>>>> Bookings(
        [FromQuery] string? status, [FromQuery] string? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await ops.BookingsAsync(UserId, status, date, page, pageSize));

    [HttpGet("bookings/{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<PartnerBookingDto>>> Booking(Guid bookingId) => Ok(await ops.BookingAsync(UserId, bookingId));

    [HttpPost("bookings/{bookingId:guid}/assign")]
    public async Task<ActionResult<ApiResponse<PartnerBookingDto>>> Assign(Guid bookingId, AssignRequest req) =>
        Ok(await ops.AssignAsync(UserId, bookingId, req.PhlebotomistId));

    /// <summary>Phlebotomist has left for the patient's address.</summary>
    [HttpPost("bookings/{bookingId:guid}/enroute")]
    public async Task<ActionResult<ApiResponse<PartnerBookingDto>>> Enroute(Guid bookingId) => Ok(await ops.EnrouteAsync(UserId, bookingId));

    /// <summary>Sample collected — requires the patient's 4-digit collection OTP.</summary>
    [HttpPost("bookings/{bookingId:guid}/collect")]
    public async Task<ActionResult<ApiResponse<PartnerBookingDto>>> Collect(Guid bookingId, CollectRequest req) =>
        Ok(await ops.CollectAsync(UserId, bookingId, req));

    [HttpPost("bookings/{bookingId:guid}/processing")]
    public async Task<ActionResult<ApiResponse<PartnerBookingDto>>> Processing(Guid bookingId) => Ok(await ops.ProcessingAsync(UserId, bookingId));

    [HttpPost("bookings/{bookingId:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<PartnerBookingDto>>> Cancel(Guid bookingId, CancelRequest req) =>
        Ok(await ops.CancelAsync(UserId, bookingId, req.Reason));

    // ---- results

    /// <summary>Result-entry form: the test's standard parameters with any values saved so far.</summary>
    [HttpGet("bookings/{bookingId:guid}/report")]
    public async Task<ActionResult<ApiResponse<ReportEditorDto>>> Report(Guid bookingId) => Ok(await ops.ReportEditorAsync(UserId, bookingId));

    /// <summary>Save results (draft). Numeric values are flagged low / high against the standard reference range.</summary>
    [HttpPut("bookings/{bookingId:guid}/report")]
    public async Task<ActionResult<ApiResponse<ReportEditorDto>>> SaveReport(Guid bookingId, SaveReportRequest req) =>
        Ok(await ops.SaveReportAsync(UserId, bookingId, req));

    /// <summary>Attach the lab's original PDF (optional, kept alongside the standard report).</summary>
    [HttpPost("bookings/{bookingId:guid}/report/attachment")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ReportEditorDto>>> Attach(Guid bookingId, IFormFile? file) =>
        Ok(await ops.AttachLabPdfAsync(UserId, bookingId, file));

    /// <summary>Publish the report to the patient (booking → ready).</summary>
    [HttpPost("bookings/{bookingId:guid}/report/publish")]
    public async Task<ActionResult<ApiResponse<PartnerBookingDto>>> Publish(Guid bookingId) => Ok(await ops.PublishReportAsync(UserId, bookingId));
}
