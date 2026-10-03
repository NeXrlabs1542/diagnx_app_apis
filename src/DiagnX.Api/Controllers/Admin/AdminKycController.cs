using System.Text.Json;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Kyc;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Auth;
using DiagnX.Api.Services.Security;
using DiagnX.Api.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.Admin;

/// <summary>ADM-01..04 — compliance review of partner KYC applications.</summary>
[Route("api/v1/admin/kyc/applications")]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminKycController(
    AppDbContext db, KycService kyc, FieldEncryptor encryptor, SignedUrl signer, NotificationService notifications, AuditService audit)
    : ApiControllerBase
{
    public sealed record ApplicationListItem(
        Guid ApplicationId, string? ReferenceId, string LabName, string City, string Phone, string Status, DateTime? SubmittedAt,
        DateTime? ExpectedBy, int ChecksPassed, int ChecksTotal);

    /// <summary>ADM-01 · ?status=PENDING&amp;from=2026-09-01&amp;to=2026-09-30&amp;page=1&amp;pageSize=20</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ApplicationListItem>>>> List(
        [FromQuery] string? status, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        var q = db.KycApplications.AsNoTracking().Where(a => a.SubmittedAt != null || a.Status != KycStatus.Draft);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status.ToUpperInvariant());
        if (IstClock.TryParseDate(from, out var f)) { var fu = IstClock.ToUtc(f, TimeOnly.MinValue); q = q.Where(a => a.SubmittedAt >= fu); }
        if (IstClock.TryParseDate(to, out var t)) { var tu = IstClock.ToUtc(t.AddDays(1), TimeOnly.MinValue); q = q.Where(a => a.SubmittedAt < tu); }

        var total = await q.CountAsync();
        var items = await q.OrderBy(a => a.Status == KycStatus.Pending ? 0 : 1).ThenBy(a => a.SubmittedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ApplicationListItem(a.Id, a.ReferenceId, a.Lab != null ? a.Lab.LegalName : "", a.Lab != null ? a.Lab.City : "",
                a.Partner.Phone, a.Status, a.SubmittedAt, a.ExpectedBy,
                a.Checks.Count(c => c.Status == "PASSED"), a.Checks.Count()))
            .ToListAsync();
        return Ok(new PagedResult<ApplicationListItem>(items, page, pageSize, total));
    }

    public sealed record CheckDto(string CheckType, string Status, string? Provider, string? FailureReason, DateTime? CheckedAt);

    public sealed record HistoryDto(string? FromStatus, string ToStatus, string ChangedBy, string? Note, DateTime At);

    public sealed record AdminApplicationDto(
        Guid ApplicationId, Guid PartnerId, string PartnerPhone, string Status, string? ReferenceId, DateTime? SubmittedAt, DateTime? ExpectedBy,
        DateTime? ReviewedAt, ProfileSection? Profile, LicencesSection? Licences, object? Owner, object? Bank, OperationsSection? Operations,
        IReadOnlyList<KycDocumentDto> Documents, IReadOnlyList<CheckDto> Checks, IReadOnlyList<HistoryDto> History,
        IReadOnlyList<RejectionReason> RejectionReasons);

    /// <summary>ADM-02 · Full application, UNMASKED for the reviewer (the access is audit-logged).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminApplicationDto>>> Get(Guid id)
    {
        var partnerId = await db.KycApplications.Where(a => a.Id == id).Select(a => (Guid?)a.PartnerId).FirstOrDefaultAsync()
                        ?? throw ApiException.NotFound("Application not found");
        var app = await kyc.LoadAsync(partnerId);
        var history = await db.KycStatusHistory.AsNoTracking().Where(h => h.ApplicationId == id).OrderBy(h => h.CreatedAt)
            .Select(h => new HistoryDto(h.FromStatus, h.ToStatus, h.ChangedBy, h.Note, h.CreatedAt)).ToListAsync();
        var lab = app.Lab;
        var sig = app.Signatory;
        var bank = lab?.BankAccounts.FirstOrDefault(b => b.IsPrimary);

        audit.Add("ADMIN", UserId.ToString(), "KYC_VIEWED_UNMASKED", "kyc_application", id.ToString());
        await db.SaveChangesAsync();

        return Ok(new AdminApplicationDto(
            app.Id, app.PartnerId, app.Partner.Phone, app.Status, app.ReferenceId, app.SubmittedAt, app.ExpectedBy, app.ReviewedAt,
            lab == null ? null : KycService.ToProfile(lab),
            lab?.TaxDetail == null ? null : KycService.ToLicences(lab),
            sig == null ? null : new
            {
                sig.FullName, sig.Designation, Dob = IstClock.FormatDate(sig.Dob), sig.Email, Pan = encryptor.Decrypt(sig.PanEnc),
                Aadhaar = encryptor.Decrypt(sig.AadhaarRef), sig.AadhaarConsent, sig.AadhaarConsentAt, sig.AadhaarConsentIp,
                sig.PanDocId, sig.AadhaarFrontDocId, sig.AadhaarBackDocId, sig.SelfieDocId,
            },
            bank == null ? null : new
            {
                bank.AccountHolder, AccountNumber = encryptor.Decrypt(bank.AccountNumberEnc), bank.Ifsc, bank.BankName, bank.BranchName,
                bank.AccountType, ChequeDocId = bank.DocumentId, bank.PennyDropStatus, bank.PennyDropName,
            },
            lab?.Operations == null ? null : KycService.ToOperations(lab),
            app.Documents.Where(d => d.Status != "DELETED" && d.Status != "REPLACED").Select(kyc.ToDto).ToList(),
            app.Checks.OrderBy(c => c.CheckType).Select(c => new CheckDto(c.CheckType, c.Status, c.Provider, c.FailureReason, c.CheckedAt)).ToList(),
            history,
            KycService.RejectionReasons(app)));
    }

    public sealed class DecisionRequest
    {
        /// <summary>APPROVE | REJECT | REQUEST_CHANGES</summary>
        public string? Decision { get; set; }
        /// <summary>Shown to the partner. Required for REJECT / REQUEST_CHANGES.</summary>
        public List<RejectionReason>? Reasons { get; set; }
        /// <summary>Internal note (not shown to the partner).</summary>
        public string? Note { get; set; }
    }

    public sealed record DecisionResult(string Status, DateTime ReviewedAt);

    /// <summary>ADM-03 · Approve (lab goes live), reject, or request changes (partner can edit and resubmit).</summary>
    [HttpPost("{id:guid}/decision")]
    public async Task<ActionResult<ApiResponse<DecisionResult>>> Decide(Guid id, DecisionRequest req)
    {
        var app = await db.KycApplications.Include(a => a.Partner).Include(a => a.Lab).FirstOrDefaultAsync(a => a.Id == id)
                  ?? throw ApiException.NotFound("Application not found");
        if (app.Status != KycStatus.Pending)
            throw ApiException.Conflict(ErrorCodes.InvalidState, $"Only PENDING applications can be decided (this one is {app.Status}).");

        var to = req.Decision?.ToUpperInvariant() switch
        {
            "APPROVE" => KycStatus.Approved,
            "REJECT" => KycStatus.Rejected,
            "REQUEST_CHANGES" => KycStatus.ChangesRequested,
            _ => throw ApiException.Field("decision", ErrorCodes.InvalidValue, "decision must be APPROVE, REJECT or REQUEST_CHANGES"),
        };
        var reasons = (req.Reasons ?? new List<RejectionReason>()).Where(r => !string.IsNullOrWhiteSpace(r.Message)).ToList();
        if (to != KycStatus.Approved && reasons.Count == 0)
            throw ApiException.Field("reasons", ErrorCodes.Required, "Tell the partner what to fix");

        var now = DateTime.UtcNow;
        var from = app.Status;
        app.Status = to;
        app.ReviewedAt = now;
        app.ReviewedBy = UserId;
        app.RejectionReason = to == KycStatus.Approved ? null : JsonSerializer.Serialize(reasons, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        app.UpdatedAt = now;
        app.Partner.KycStatus = to;
        app.Partner.UpdatedAt = now;
        if (to == KycStatus.Approved && app.Lab != null)
        {
            app.Lab.IsActive = true;
            app.Lab.UpdatedAt = now;
        }

        db.KycStatusHistory.Add(new KycStatusHistory
        {
            ApplicationId = app.Id, FromStatus = from, ToStatus = to, ChangedBy = $"admin:{UserId}",
            Note = req.Note is { Length: > 300 } ? req.Note[..300] : req.Note, CreatedAt = now,
        });
        var (title, body) = to switch
        {
            KycStatus.Approved => ("KYC approved — you're live!", "Your lab is verified. Add your tests and prices to start receiving bookings."),
            KycStatus.Rejected => ("KYC not approved", "We couldn't verify your application. Open the app to see why."),
            _ => ("Changes needed in your KYC", "A few details need your attention. Open the app to update and resubmit."),
        };
        notifications.Add(UserTypes.Partner, app.PartnerId, "kyc", title, body);
        audit.Add("ADMIN", UserId.ToString(), $"KYC_{to}", "kyc_application", id.ToString());
        await db.SaveChangesAsync();
        return Ok(new DecisionResult(to, now));
    }

    public sealed class VerifyRequest
    {
        public string? CheckType { get; set; }
        /// <summary>Record a manual result (PASSED | FAILED | MANUAL_REVIEW). Omit to run the automated provider.</summary>
        public string? Status { get; set; }
        public string? FailureReason { get; set; }
    }

    /// <summary>
    /// ADM-04 · (Re-)run a verification check. No third-party provider is configured yet, so automated runs
    /// go to MANUAL_REVIEW; reviewers record the result they verified by passing status.
    /// </summary>
    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<ApiResponse<CheckDto>>> Verify(Guid id, VerifyRequest req)
    {
        var type = req.CheckType?.ToUpperInvariant();
        if (type == null || !CheckTypes.All.Contains(type))
            throw ApiException.Field("checkType", ErrorCodes.InvalidValue, $"checkType must be one of {string.Join(", ", CheckTypes.All)}");
        var status = req.Status?.ToUpperInvariant();
        if (status != null && status is not ("PASSED" or "FAILED" or "MANUAL_REVIEW"))
            throw ApiException.Field("status", ErrorCodes.InvalidValue, "status must be PASSED, FAILED or MANUAL_REVIEW");
        if (!await db.KycApplications.AnyAsync(a => a.Id == id)) throw ApiException.NotFound("Application not found");

        var check = await db.KycVerificationChecks.FirstOrDefaultAsync(c => c.ApplicationId == id && c.CheckType == type);
        if (check == null)
        {
            check = new KycVerificationCheck { Id = Guid.NewGuid(), ApplicationId = id, CheckType = type };
            db.KycVerificationChecks.Add(check);
        }
        check.Status = status ?? "MANUAL_REVIEW";
        check.Provider = status == null ? "NONE" : "MANUAL";
        check.RequestRef = $"admin:{UserId}";
        check.FailureReason = status == null ? "No automated provider configured — verify manually" : req.FailureReason;
        check.CheckedAt = DateTime.UtcNow;
        audit.Add("ADMIN", UserId.ToString(), "KYC_CHECK", "kyc_application", id.ToString(), new { type, check.Status });
        await db.SaveChangesAsync();
        return Ok(new CheckDto(check.CheckType, check.Status, check.Provider, check.FailureReason, check.CheckedAt));
    }

    /// <summary>Signed URL for a KYC document (audit-logged).</summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/url")]
    public async Task<ActionResult<ApiResponse<DocumentUrlDto>>> DocumentUrl(Guid id, Guid documentId)
    {
        var doc = await db.KycDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && d.ApplicationId == id)
                  ?? throw ApiException.NotFound("Document not found");
        audit.Add("ADMIN", UserId.ToString(), "DOC_VIEWED", "kyc_document", documentId.ToString());
        await db.SaveChangesAsync();
        return Ok(new DocumentUrlDto(signer.Create(doc.StoredFileId), SignedUrl.DefaultTtlSeconds));
    }

    public sealed record DocumentUrlDto(string Url, int ExpiresInSeconds);
}
