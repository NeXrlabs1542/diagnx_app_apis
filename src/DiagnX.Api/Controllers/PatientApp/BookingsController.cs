using DiagnX.Api.Common;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiagnX.Api.Controllers.PatientApp;

[Route("api/v1/patient")]
[ApiExplorerSettings(GroupName = "patient")]
[Authorize(Roles = Roles.Patient)]
public sealed class BookingsController(PatientBookingService bookings) : ApiControllerBase
{
    /// <summary>
    /// Create a booking. COD → confirmed immediately (nextAction NONE). UPI / card → payment_pending with a
    /// payment intent (nextAction PAY): open the gateway, then call POST /payments/{paymentId}/verify.
    /// Send an Idempotency-Key header so a retried request doesn't double-book.
    /// </summary>
    [HttpPost("bookings")]
    public async Task<ActionResult<ApiResponse<CreateBookingResult>>> Create(CreateBookingRequest req) =>
        Ok(await bookings.CreateAsync(UserId, req, Request.Headers["Idempotency-Key"].FirstOrDefault()));

    /// <summary>Orders screen. ?tab=active|past (omit for all).</summary>
    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<List<BookingDto>>>> List([FromQuery] string? tab) => Ok(await bookings.ListAsync(UserId, tab));

    /// <summary>Order tracking — status, timeline with timestamps, phlebotomist, collection OTP.</summary>
    [HttpGet("bookings/{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> Get(Guid bookingId) => Ok(await bookings.GetAsync(UserId, bookingId));

    /// <summary>Cancel (only before the phlebotomist is on the way). Paid bookings are refunded.</summary>
    [HttpPost("bookings/{bookingId:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> Cancel(Guid bookingId, CancelRequest? req) =>
        Ok(await bookings.CancelAsync(UserId, bookingId, req?.Reason));

    /// <summary>Start a new payment attempt for a booking still waiting for payment.</summary>
    [HttpPost("bookings/{bookingId:guid}/payment")]
    public async Task<ActionResult<ApiResponse<PaymentIntentDto>>> RetryPayment(Guid bookingId) =>
        Ok(await bookings.RetryPaymentAsync(UserId, bookingId));

    /// <summary>Verify a payment with the gateway response. Mock gateway: any value succeeds, "fail" fails.</summary>
    [HttpPost("payments/{paymentId:guid}/verify")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> VerifyPayment(Guid paymentId, VerifyPaymentRequest req) =>
        Ok(await bookings.VerifyPaymentAsync(UserId, paymentId, req));

    /// <summary>Rate the lab and the phlebotomist (after the report is ready). Marks the booking completed.</summary>
    [HttpPost("bookings/{bookingId:guid}/review")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> Review(Guid bookingId, ReviewRequest req) =>
        Ok(await bookings.ReviewAsync(UserId, bookingId, req));

    /// <summary>The standardized report for a booking.</summary>
    [HttpGet("bookings/{bookingId:guid}/report")]
    public async Task<ActionResult<ApiResponse<ReportDto>>> Report(Guid bookingId) => Ok(await bookings.ReportAsync(UserId, bookingId));

    /// <summary>Reports screen — every published report.</summary>
    [HttpGet("reports")]
    public async Task<ActionResult<ApiResponse<List<ReportListItemDto>>>> Reports() => Ok(await bookings.ReportsAsync(UserId));
}
