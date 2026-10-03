using DiagnX.Api.Common;
using DiagnX.Api.Features.Kyc;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiagnX.Api.Controllers.PartnerApp;

/// <summary>
/// KYC wizard (spec KYC-01..09). Each step is a PUT that replaces that section.
/// Send header <c>X-Draft: true</c> for background autosave of a partial step (no validation).
/// </summary>
[Route("api/v1/partner/kyc")]
[ApiExplorerSettings(GroupName = "partner")]
[Authorize(Roles = Roles.Partner)]
public sealed class KycController(KycService kyc) : ApiControllerBase
{
    private bool IsDraft => string.Equals(Request.Headers["X-Draft"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>KYC-01 · Progress summary (drives "Start verification" vs "Resume application – Step N").</summary>
    [HttpGet("progress")]
    public async Task<ActionResult<ApiResponse<KycProgressDto>>> Progress() => Ok(await kyc.ProgressAsync(UserId));

    /// <summary>KYC-02 · Full application to pre-fill every step and the Review screen (sensitive values masked).</summary>
    [HttpGet("application")]
    public async Task<ActionResult<ApiResponse<KycApplicationDto>>> Application() => Ok(await kyc.ApplicationAsync(UserId));

    /// <summary>KYC-03 · Save step 1 — lab profile.</summary>
    [HttpPut("application/profile")]
    public async Task<ActionResult<ApiResponse<StepSaveResult>>> Profile(ProfileSection body) =>
        Ok(await kyc.SaveProfileAsync(UserId, body, IsDraft));

    /// <summary>KYC-04 · Save step 2 — licences &amp; compliance.</summary>
    [HttpPut("application/licences")]
    public async Task<ActionResult<ApiResponse<StepSaveResult>>> Licences(LicencesSection body) =>
        Ok(await kyc.SaveLicencesAsync(UserId, body, IsDraft));

    /// <summary>KYC-05 · Save step 3 — owner / authorised signatory (Aadhaar is write-only).</summary>
    [HttpPut("application/owner")]
    public async Task<ActionResult<ApiResponse<StepSaveResult>>> Owner(OwnerSection body) =>
        Ok(await kyc.SaveOwnerAsync(UserId, body, IsDraft, ClientIp));

    /// <summary>KYC-06 · Save step 4 — payout bank account (account number is write-only).</summary>
    [HttpPut("application/bank")]
    public async Task<ActionResult<ApiResponse<StepSaveResult>>> Bank(BankSection body) =>
        Ok(await kyc.SaveBankAsync(UserId, body, IsDraft));

    /// <summary>KYC-07 · Save step 5 — operations &amp; premises.</summary>
    [HttpPut("application/operations")]
    public async Task<ActionResult<ApiResponse<StepSaveResult>>> Operations(OperationsSection body) =>
        Ok(await kyc.SaveOperationsAsync(UserId, body, IsDraft));

    /// <summary>
    /// KYC-08 · Accept declarations, re-validate everything and lock for review. Idempotent with header Idempotency-Key.
    /// On failure: 422 INCOMPLETE_APPLICATION with error.firstInvalidStep + error.fields.
    /// </summary>
    [HttpPost("application/submit")]
    public async Task<ActionResult<ApiResponse<SubmitResult>>> Submit(SubmitRequest body) =>
        Ok(await kyc.SubmitAsync(UserId, body, Request.Headers["Idempotency-Key"].FirstOrDefault(), ClientIp, DeviceId));

    /// <summary>KYC-09 · KYC-pending screen: status, timeline, masked summary (also used for pull-to-refresh).</summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<KycStatusDto>>> Status() => Ok(await kyc.StatusAsync(UserId));

    public sealed record DocumentUrl(string Url, int ExpiresInSeconds);

    /// <summary>DOC-01 · Upload one document / photo (multipart: docType + file). Re-uploading a docType replaces it.</summary>
    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<KycDocumentDto>>> Upload([FromForm] string? docType, IFormFile? file) =>
        Ok(await kyc.UploadDocumentAsync(UserId, docType, file));

    /// <summary>DOC-02 · Remove an attached document (blocked after submit).</summary>
    [HttpDelete("documents/{documentId:guid}")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> DeleteDocument(Guid documentId)
    {
        await kyc.DeleteDocumentAsync(UserId, documentId);
        return Success();
    }

    /// <summary>DOC-03 · Short-lived signed URL for a thumbnail / preview.</summary>
    [HttpGet("documents/{documentId:guid}/url")]
    public async Task<ActionResult<ApiResponse<DocumentUrl>>> DocumentUrlFor(Guid documentId)
    {
        var (url, ttl) = await kyc.DocumentUrlAsync(UserId, documentId);
        return Ok(new DocumentUrl(url, ttl));
    }
}
