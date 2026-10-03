using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Security;
using DiagnX.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Features.Kyc;

/// <summary>KYC-01..09 and DOC-01..03 — the partner onboarding wizard.</summary>
public sealed class KycService(
    AppDbContext db,
    FieldEncryptor encryptor,
    AppConfigService config,
    IFileStorage storage,
    SignedUrl signer,
    NotificationService notifications,
    AuditService audit)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] SectionNames = { "", "profile", "licences", "owner", "bank", "operations" };
    private static readonly HashSet<string> ActiveDocStatuses = new() { "UPLOADED", "VERIFIED", "REJECTED" };

    // ---------------------------------------------------------------- loading

    public async Task<KycApplication> LoadAsync(Guid partnerId, bool full = true)
    {
        IQueryable<KycApplication> q = db.KycApplications.Include(a => a.Partner);
        if (full)
        {
            q = q.Include(a => a.Lab!).ThenInclude(l => l.Services)
                .Include(a => a.Lab!).ThenInclude(l => l.Licences)
                .Include(a => a.Lab!).ThenInclude(l => l.TaxDetail)
                .Include(a => a.Lab!).ThenInclude(l => l.MedicalDirectors)
                .Include(a => a.Lab!).ThenInclude(l => l.BankAccounts)
                .Include(a => a.Lab!).ThenInclude(l => l.Operations)
                .Include(a => a.Lab!).ThenInclude(l => l.WorkingDays)
                .Include(a => a.Lab!).ThenInclude(l => l.ServiceablePincodes)
                .Include(a => a.Signatory)
                .Include(a => a.Declaration)
                .Include(a => a.Documents)
                .Include(a => a.Checks)
                .AsSplitQuery();
        }

        var app = await q.FirstOrDefaultAsync(a => a.PartnerId == partnerId);
        if (app != null) return app;

        // Partner exists (they have a token) but has no application row — create it lazily.
        var partner = await db.Partners.FindAsync(partnerId) ?? throw ApiException.Unauthorized();
        var now = DateTime.UtcNow;
        app = new KycApplication { Id = Guid.NewGuid(), PartnerId = partnerId, Partner = partner, CreatedAt = now, UpdatedAt = now };
        db.KycApplications.Add(app);
        await db.SaveChangesAsync();
        return app;
    }

    public async Task<KycMasterLists> MasterListsAsync()
    {
        var options = await db.MasterOptions.AsNoTracking().Where(o => o.IsActive).ToListAsync();
        HashSet<string> Group(string g) => options.Where(o => o.GroupCode == g).Select(o => o.Code).ToHashSet();
        return new KycMasterLists
        {
            BusinessTypes = Group("BUSINESS_TYPE"),
            Services = Group("SERVICE"),
            Qualifications = Group("QUALIFICATION"),
            Councils = Group("MEDICAL_COUNCIL"),
            Designations = Group("DESIGNATION"),
            AccountTypes = Group("ACCOUNT_TYPE"),
            StaffCounts = Group("STAFF_COUNT"),
            ProcessingModes = Group("PROCESSING_MODE"),
            Weekdays = Group("WEEKDAY"),
            States = (await db.MasterStates.AsNoTracking().Select(s => s.Name).ToListAsync()).ToHashSet(),
            BusinessTypeLabels = options.Where(o => o.GroupCode == "BUSINESS_TYPE").ToDictionary(o => o.Code, o => o.Label),
            ServiceLabels = options.Where(o => o.GroupCode == "SERVICE").ToDictionary(o => o.Code, o => o.Label),
        };
    }

    private KycValidator Validator(KycMasterLists lists, KycApplication app)
    {
        var docs = app.Documents.Where(d => ActiveDocStatuses.Contains(d.Status)).ToDictionary(d => d.Id);
        return new KycValidator(lists, (id, type) =>
        {
            if (id == null || !docs.TryGetValue(id.Value, out var doc))
                return (ErrorCodes.DocRequired, "Please upload this document");
            if (doc.DocType != type)
                return (ErrorCodes.DocWrongType, "This file was uploaded for a different document");
            return null;
        });
    }

    private static KycContext Context(KycApplication app) =>
        new(app.Lab?.BusinessType, app.Lab?.Services.Select(s => s.ServiceCode).ToList() ?? new List<string>(), app.Lab?.TaxDetail?.BusinessPan);

    private static void EnsureEditable(KycApplication app)
    {
        if (!KycStatus.IsEditable(app.Status))
            throw ApiException.Conflict(ErrorCodes.ApplicationLocked, "Your application is submitted and can't be edited while it is under review.");
    }

    private static Lab RequireLab(KycApplication app) =>
        app.Lab ?? throw new ApiException(422, ErrorCodes.IncompleteApplication, "Please complete the lab profile (step 1) first.") { FirstInvalidStep = 1 };

    // ---------------------------------------------------------------- step saves

    public async Task<StepSaveResult> SaveProfileAsync(Guid partnerId, ProfileSection req, bool isDraft)
    {
        var app = await LoadAsync(partnerId);
        EnsureEditable(app);
        if (isDraft) return await SaveDraftAsync(app, 1, req);

        Validator(await MasterListsAsync(), app).Profile(req).ThrowIfAny();

        var now = DateTime.UtcNow;
        var lab = app.Lab;
        if (lab == null)
        {
            lab = new Lab { Id = Guid.NewGuid(), ApplicationId = app.Id, PartnerId = app.PartnerId, CreatedAt = now, IsActive = false };
            db.Labs.Add(lab);
            app.Lab = lab;
        }
        lab.LegalName = req.LabLegalName!.Trim();
        lab.BrandName = NullIfEmpty(req.LabBrandName);
        lab.BusinessType = req.BusinessType!;
        lab.EstablishedYear = (short)req.EstablishedYear!.Value;
        lab.AddressLine1 = req.AddressLine1!.Trim();
        lab.AddressLine2 = NullIfEmpty(req.AddressLine2);
        lab.Landmark = NullIfEmpty(req.Landmark);
        lab.City = req.City!.Trim();
        lab.State = req.State!;
        lab.Pincode = req.Pincode!;
        lab.Area = req.Area!.Trim();
        lab.Latitude = Math.Round(req.Latitude!.Value, 6);
        lab.Longitude = Math.Round(req.Longitude!.Value, 6);
        lab.LocationAccuracyM = req.LocationAccuracy;
        lab.LocationCapturedAt = req.LocationCapturedAt?.ToUniversalTime();
        lab.LabPhone = req.LabPhone!;
        lab.LabEmail = req.LabEmail!.Trim();
        lab.Website = NullIfEmpty(req.Website);
        lab.UpdatedAt = now;

        SyncSet(lab.Services, req.Services!.Distinct().ToList(), s => s.ServiceCode,
            code => new LabService { LabId = lab.Id, ServiceCode = code });
        return await FinishStepAsync(app, 1);
    }

    public async Task<StepSaveResult> SaveLicencesAsync(Guid partnerId, LicencesSection req, bool isDraft)
    {
        var app = await LoadAsync(partnerId);
        EnsureEditable(app);
        if (isDraft) return await SaveDraftAsync(app, 2, req);

        var lab = RequireLab(app);
        Normalise(req);
        Validator(await MasterListsAsync(), app).Licences(req, Context(app) with { BusinessPan = req.BusinessPan }).ThrowIfAny();

        UpsertLicence(lab, LicenceTypes.ClinicalEst, req.ClinicalEstNumber, null, req.ClinicalEstDocId);
        UpsertLicence(lab, LicenceTypes.Bmw, req.BmwNumber, ParseDate(req.BmwValidUpto), req.BmwDocId);
        UpsertLicence(lab, LicenceTypes.Trade, req.TradeLicenseNumber, null, req.TradeLicenseDocId);

        if (lab.Services.Any(s => s.ServiceCode == "radiology")) UpsertLicence(lab, LicenceTypes.Aerb, req.AerbNumber, null, req.AerbDocId);
        else RemoveLicence(lab, LicenceTypes.Aerb);

        if (!string.IsNullOrWhiteSpace(req.PcpndtNumber)) UpsertLicence(lab, LicenceTypes.Pcpndt, req.PcpndtNumber, null, null);
        else RemoveLicence(lab, LicenceTypes.Pcpndt);

        if (lab.BusinessType == "proprietorship") RemoveLicence(lab, LicenceTypes.EntityReg);
        else UpsertLicence(lab, LicenceTypes.EntityReg, lab.BusinessType == "partnership" ? "" : req.EntityRegNumber, null, req.EntityRegDocId);

        lab.HasNabl = req.HasNabl;
        if (req.HasNabl == true) UpsertLicence(lab, LicenceTypes.Nabl, req.NablNumber, ParseDate(req.NablValidUpto), req.NablDocId);
        else RemoveLicence(lab, LicenceTypes.Nabl);

        if (lab.TaxDetail == null)
        {
            lab.TaxDetail = new LabTaxDetail { LabId = lab.Id };
            db.LabTaxDetails.Add(lab.TaxDetail);
        }
        if (lab.TaxDetail.BusinessPan != req.BusinessPan) lab.TaxDetail.PanVerified = false;
        if (lab.TaxDetail.Gstin != NullIfEmpty(req.Gstin)) lab.TaxDetail.GstinVerified = false;
        lab.TaxDetail.BusinessPan = req.BusinessPan!;
        lab.TaxDetail.Gstin = NullIfEmpty(req.Gstin);

        var director = lab.MedicalDirectors.FirstOrDefault();
        if (director == null)
        {
            director = new LabMedicalDirector { Id = Guid.NewGuid(), LabId = lab.Id };
            db.LabMedicalDirectors.Add(director);
            lab.MedicalDirectors.Add(director);
        }
        director.Name = req.DirectorName!.Trim();
        director.Qualification = req.DirectorQualification!;
        director.CouncilName = req.CouncilName!;
        director.RegistrationNumber = req.MedicalRegNumber!.Trim();
        director.DocumentId = req.MedicalRegDocId;
        director.VerificationStatus = "NOT_CHECKED";
        lab.UpdatedAt = DateTime.UtcNow;
        return await FinishStepAsync(app, 2);
    }

    public async Task<StepSaveResult> SaveOwnerAsync(Guid partnerId, OwnerSection req, bool isDraft, string? ip)
    {
        var app = await LoadAsync(partnerId);
        EnsureEditable(app);
        if (isDraft)
        {
            req.AadhaarNumber = null; // never persisted outside the encrypted column
            return await SaveDraftAsync(app, 3, req);
        }

        RequireLab(app);
        req.OwnerPan = req.OwnerPan?.Trim().ToUpperInvariant();
        var sig = app.Signatory;
        Validator(await MasterListsAsync(), app).Owner(req, Context(app), aadhaarAlreadyStored: sig?.AadhaarRef is { Length: > 0 }).ThrowIfAny();

        var now = DateTime.UtcNow;
        if (sig == null)
        {
            sig = new KycSignatory { Id = Guid.NewGuid(), ApplicationId = app.Id };
            db.KycSignatories.Add(sig);
            app.Signatory = sig;
        }
        sig.FullName = req.OwnerName!.Trim();
        sig.Designation = req.OwnerDesignation!;
        sig.Dob = ParseDate(req.OwnerDob)!.Value;
        sig.Email = req.OwnerEmail!.Trim();
        sig.PanEnc = encryptor.Encrypt(req.OwnerPan!);
        sig.PanMasked = Masking.Pan(req.OwnerPan);

        var aadhaar = IndianIds.DigitsOnly(req.AadhaarNumber);
        if (aadhaar.Length > 0)
        {
            sig.AadhaarRef = encryptor.Encrypt(aadhaar);
            sig.AadhaarLast4 = Masking.Last4(aadhaar);
            sig.AadhaarVerified = false;
        }
        sig.AadhaarConsent = true;
        sig.AadhaarConsentAt = now;
        sig.AadhaarConsentIp = ip;
        sig.PanDocId = req.PanDocId;
        sig.AadhaarFrontDocId = req.AadhaarFrontDocId;
        sig.AadhaarBackDocId = req.AadhaarBackDocId;
        sig.SelfieDocId = req.SelfieDocId;

        audit.Add("PARTNER", partnerId.ToString(), "AADHAAR_CONSENT", "kyc_application", app.Id.ToString());
        return await FinishStepAsync(app, 3);
    }

    public async Task<StepSaveResult> SaveBankAsync(Guid partnerId, BankSection req, bool isDraft)
    {
        var app = await LoadAsync(partnerId);
        EnsureEditable(app);
        if (isDraft)
        {
            req.AccountNumber = null;
            req.ConfirmAccountNumber = null;
            return await SaveDraftAsync(app, 4, req);
        }

        var lab = RequireLab(app);
        req.Ifsc = req.Ifsc?.Trim().ToUpperInvariant();
        var bank = lab.BankAccounts.FirstOrDefault(b => b.IsPrimary);
        Validator(await MasterListsAsync(), app).Bank(req, accountAlreadyStored: bank?.AccountNumberEnc is { Length: > 0 }).ThrowIfAny();

        if (bank == null)
        {
            bank = new LabBankAccount { Id = Guid.NewGuid(), LabId = lab.Id, IsPrimary = true };
            db.LabBankAccounts.Add(bank);
            lab.BankAccounts.Add(bank);
        }
        bank.AccountHolder = req.AccountHolder!.Trim();
        var number = req.AccountNumber?.Trim() ?? "";
        if (number.Length > 0)
        {
            bank.AccountNumberEnc = encryptor.Encrypt(number);
            bank.AccountLast4 = Masking.Last4(number);
            bank.PennyDropStatus = "NOT_STARTED";
            bank.PennyDropName = null;
        }
        bank.Ifsc = req.Ifsc!;
        bank.BankName = req.BankName!.Trim();
        bank.BranchName = NullIfEmpty(req.BranchName);
        bank.AccountType = req.AccountType!;
        bank.DocumentId = req.ChequeDocId;
        return await FinishStepAsync(app, 4);
    }

    public async Task<StepSaveResult> SaveOperationsAsync(Guid partnerId, OperationsSection req, bool isDraft)
    {
        var app = await LoadAsync(partnerId);
        EnsureEditable(app);
        if (isDraft) return await SaveDraftAsync(app, 5, req);

        var lab = RequireLab(app);
        if (req.PhlebotomistCount != null) req.PhlebotomistCount = KycValidator.NormaliseStaffCount(req.PhlebotomistCount);
        req.ServiceablePincodes = req.ServiceablePincodes?.Select(p => p.Trim()).ToList();
        Validator(await MasterListsAsync(), app).Operations(req).ThrowIfAny();

        var ops = lab.Operations;
        if (ops == null)
        {
            ops = new LabOperations { LabId = lab.Id };
            db.LabOperations.Add(ops);
            lab.Operations = ops;
        }
        ops.ProcessingMode = req.ProcessingMode!;
        ops.HomeCollection = req.HomeCollection!.Value;
        ops.PhlebotomistCount = ops.HomeCollection ? req.PhlebotomistCount : null;
        ops.OpenTime = TimeOnly.ParseExact(req.OpenTime!, "HH:mm");
        ops.CloseTime = TimeOnly.ParseExact(req.CloseTime!, "HH:mm");
        ops.FrontPhotoDocId = req.FrontPhotoDocId;
        ops.InteriorPhotoDocId = req.InteriorPhotoDocId;

        SyncSet(lab.WorkingDays, req.WorkingDays!, d => d.DayCode, d => new LabWorkingDay { LabId = lab.Id, DayCode = d });
        var pins = ops.HomeCollection ? req.ServiceablePincodes!.Distinct().ToList() : new List<string>();
        SyncSet(lab.ServiceablePincodes, pins, p => p.Pincode, p => new LabServiceablePincode { LabId = lab.Id, Pincode = p });
        lab.UpdatedAt = DateTime.UtcNow;
        return await FinishStepAsync(app, 5);
    }

    private async Task<StepSaveResult> FinishStepAsync(KycApplication app, int step)
    {
        if (!app.CompletedSteps.Contains((short)step))
            app.CompletedSteps = app.CompletedSteps.Append((short)step).OrderBy(s => s).ToList();
        SetDraft(app, step, null);
        return await TouchAsync(app, step, isDraft: false);
    }

    /// <summary>X-Draft: true — background autosave of a partial section, no validation, not marked complete.</summary>
    private async Task<StepSaveResult> SaveDraftAsync(KycApplication app, int step, object section)
    {
        SetDraft(app, step, JsonSerializer.SerializeToNode(section, Json));
        return await TouchAsync(app, step, isDraft: true);
    }

    private async Task<StepSaveResult> TouchAsync(KycApplication app, int step, bool isDraft)
    {
        var now = DateTime.UtcNow;
        var next = Enumerable.Range(1, 5).FirstOrDefault(s => !app.CompletedSteps.Contains((short)s));
        if (next == 0) next = 6;
        app.CurrentStep = (short)next;
        app.LastSavedAt = now;
        app.UpdatedAt = now;
        app.Version++;
        MarkInProgress(app.Partner, now);
        await db.SaveChangesAsync();
        return new StepSaveResult(app.Id, step, app.CompletedSteps, next, now, isDraft);
    }

    private static void MarkInProgress(Partner partner, DateTime now)
    {
        if (partner.KycStatus != PartnerKycStatus.NotStarted) return;
        partner.KycStatus = PartnerKycStatus.InProgress;
        partner.UpdatedAt = now;
    }

    private static JsonObject Drafts(KycApplication app) =>
        string.IsNullOrEmpty(app.DraftSections) ? new JsonObject() : JsonNode.Parse(app.DraftSections)!.AsObject();

    private static void SetDraft(KycApplication app, int step, JsonNode? value)
    {
        var drafts = Drafts(app);
        if (value == null) drafts.Remove(SectionNames[step]);
        else drafts[SectionNames[step]] = value;
        app.DraftSections = drafts.Count == 0 ? null : drafts.ToJsonString();
    }

    // ---------------------------------------------------------------- documents

    public async Task<KycDocumentDto> UploadDocumentAsync(Guid partnerId, string? docType, IFormFile? file)
    {
        var app = await LoadAsync(partnerId, full: false);
        EnsureEditable(app);

        docType = docType?.Trim().ToUpperInvariant();
        if (docType == null || !DocTypes.All.Contains(docType))
            throw ApiException.Field("docType", ErrorCodes.InvalidValue, "Unknown document type");
        if (file == null || file.Length == 0)
            throw ApiException.Field("file", ErrorCodes.Required, "Please choose a file");

        var limit = await config.GetIntAsync(docType == DocTypes.Selfie ? AppConfigService.MaxSelfieBytes : AppConfigService.MaxDocBytes);
        if (file.Length > limit)
            throw new ApiException(413, ErrorCodes.DocTooLarge,
                $"File is {file.Length / 1048576.0:0.0} MB - please keep it under {limit / 1048576.0:0.0} MB");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var mime = FileSniffer.Detect(bytes)
                   ?? throw new ApiException(415, ErrorCodes.DocTypeUnsupported, "Please upload a JPG, PNG, HEIC or PDF file");
        if (mime == "application/pdf" && DocTypes.PhotoOnly.Contains(docType))
            throw new ApiException(415, ErrorCodes.DocTypeUnsupported, "Please upload a photo (JPG, PNG or HEIC) for this document");

        ms.Position = 0;
        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(file.FileName) ? $"{docType.ToLowerInvariant()}{FileSniffer.Extension(mime)}" : file.FileName);
        var stored = await storage.SaveAsync(ms, fileName, mime, "KYC_DOCUMENT", app.Id);

        var now = DateTime.UtcNow;
        var previous = await db.KycDocuments
            .Where(d => d.ApplicationId == app.Id && d.DocType == docType && ActiveDocStatuses.Contains(d.Status)).ToListAsync();
        foreach (var p in previous) p.Status = "REPLACED";

        var doc = new KycDocument
        {
            Id = Guid.NewGuid(),
            ApplicationId = app.Id,
            DocType = docType,
            FileName = stored.FileName,
            MimeType = mime,
            SizeBytes = stored.SizeBytes,
            StoredFileId = stored.Id,
            ChecksumSha256 = stored.Sha256,
            UploadedAt = now,
        };
        db.KycDocuments.Add(doc);
        app.UpdatedAt = now;
        MarkInProgress(app.Partner, now);
        await db.SaveChangesAsync();
        return ToDto(doc);
    }

    public async Task DeleteDocumentAsync(Guid partnerId, Guid documentId)
    {
        var app = await LoadAsync(partnerId, full: false);
        var doc = await db.KycDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.ApplicationId == app.Id && d.Status != "DELETED")
                  ?? throw ApiException.NotFound("Document not found");
        EnsureEditable(app);
        doc.Status = "DELETED";
        doc.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<(string Url, int ExpiresInSeconds)> DocumentUrlAsync(Guid partnerId, Guid documentId)
    {
        var app = await LoadAsync(partnerId, full: false);
        var doc = await db.KycDocuments.AsNoTracking()
                      .FirstOrDefaultAsync(d => d.Id == documentId && d.ApplicationId == app.Id && d.Status != "DELETED")
                  ?? throw ApiException.NotFound("Document not found");
        return (signer.Create(doc.StoredFileId), SignedUrl.DefaultTtlSeconds);
    }

    public KycDocumentDto ToDto(KycDocument d) =>
        new(d.Id, d.DocType, d.FileName, d.MimeType, d.SizeBytes, d.Status, d.UploadedAt, signer.Create(d.StoredFileId));

    // ---------------------------------------------------------------- reads

    public async Task<KycProgressDto> ProgressAsync(Guid partnerId)
    {
        var app = await LoadAsync(partnerId, full: false);
        var hasDocs = await db.KycDocuments.AnyAsync(d => d.ApplicationId == app.Id && d.Status != "DELETED");
        var hasProgress = app.CompletedSteps.Count > 0 || app.DraftSections != null || hasDocs || app.CurrentStep > 1;
        return new KycProgressDto(app.Status, hasProgress, app.CurrentStep, app.CompletedSteps, app.LastSavedAt);
    }

    public async Task<KycApplicationDto> ApplicationAsync(Guid partnerId)
    {
        var app = await LoadAsync(partnerId);
        var drafts = Drafts(app);
        var lab = app.Lab;

        object? Section(int step, Func<object?> fromDb) => drafts[SectionNames[step]] is { } d ? d : fromDb();

        return new KycApplicationDto(
            app.Id, app.Status, app.ReferenceId, app.CurrentStep, app.CompletedSteps, app.LastSavedAt,
            Section(1, () => lab == null ? null : ToProfile(lab)),
            Section(2, () => lab?.TaxDetail == null ? null : ToLicences(lab)),
            Section(3, () => app.Signatory == null ? null : ToOwnerView(app.Signatory)),
            Section(4, () => lab?.BankAccounts.FirstOrDefault(b => b.IsPrimary) is { } b ? ToBankView(b) : null),
            Section(5, () => lab?.Operations == null ? null : ToOperations(lab)),
            app.Documents.Where(d => ActiveDocStatuses.Contains(d.Status)).OrderBy(d => d.UploadedAt).Select(ToDto).ToList(),
            app.Declaration == null ? null
                : new DeclarationsDto(app.Declaration.DeclTrue, app.Declaration.DeclVerify, app.Declaration.DeclTerms, app.Declaration.AgreementVersion),
            RejectionReasons(app));
    }

    // ---------------------------------------------------------------- submit + status

    public async Task<SubmitResult> SubmitAsync(Guid partnerId, SubmitRequest req, string? idempotencyKey, string? ip, string? deviceId)
    {
        var app = await LoadAsync(partnerId);
        if (app.Status is KycStatus.Pending or KycStatus.Approved)
        {
            if (idempotencyKey != null && idempotencyKey == app.SubmitIdempotencyKey && app.ReferenceId != null)
                return new SubmitResult(app.ReferenceId, app.Status, app.SubmittedAt!.Value, app.ExpectedBy!.Value);
            throw ApiException.Conflict(ErrorCodes.AlreadySubmitted, "This application has already been submitted.");
        }

        // Re-validate the WHOLE application from what is stored — never trust the client.
        var lists = await MasterListsAsync();
        var validator = Validator(lists, app);
        var ctx = Context(app);
        var lab = app.Lab;
        var checks = new List<(int Step, Func<FieldErrors> Run)>
        {
            (1, () => lab == null ? new FieldErrors().Add("labLegalName", ErrorCodes.Required, "Please complete your lab profile") : validator.Profile(ToProfile(lab))),
            (2, () => lab?.TaxDetail == null ? new FieldErrors().Add("businessPan", ErrorCodes.Required, "Please complete licences & compliance") : validator.Licences(ToLicences(lab), ctx)),
            (3, () => app.Signatory == null ? new FieldErrors().Add("ownerName", ErrorCodes.Required, "Please complete owner details") : validator.Owner(ToOwnerSection(app.Signatory), ctx, false)),
            (4, () => lab?.BankAccounts.FirstOrDefault(b => b.IsPrimary) is { } bank ? validator.Bank(ToBankSection(bank), false) : new FieldErrors().Add("accountHolder", ErrorCodes.Required, "Please complete bank details")),
            (5, () => lab?.Operations == null ? new FieldErrors().Add("processingMode", ErrorCodes.Required, "Please complete operations & premises") : validator.Operations(ToOperations(lab))),
            (6, () => KycValidator.Declarations(req)),
        };
        foreach (var (step, run) in checks)
        {
            var errors = run();
            if (errors.Any)
                throw new ApiException(422, ErrorCodes.IncompleteApplication, "Some details need your attention before you can submit.")
                    { FirstInvalidStep = step, Fields = errors.Items };
        }

        var now = DateTime.UtcNow;
        var from = app.Status;
        app.ReferenceId ??= $"DXP-{IstClock.Now.Year}-{await db.NextSequenceValue(AppDbContext.KycReferenceSeq):D6}";
        app.Status = KycStatus.Pending;
        app.SubmittedAt = now;
        app.ExpectedBy = IstClock.AddBusinessDays(now, await config.GetIntAsync(AppConfigService.KycTatBusinessDays));
        app.RejectionReason = null;
        app.DraftSections = null;
        app.SubmitIdempotencyKey = idempotencyKey;
        app.CurrentStep = 6;
        app.UpdatedAt = now;
        app.Version++;

        if (app.Declaration == null)
        {
            app.Declaration = new KycDeclaration { ApplicationId = app.Id };
            db.KycDeclarations.Add(app.Declaration);
        }
        app.Declaration.DeclTrue = app.Declaration.DeclVerify = app.Declaration.DeclTerms = true;
        app.Declaration.AgreementVersion = req.AgreementVersion!.Trim();
        app.Declaration.AcceptedAt = now;
        app.Declaration.IpAddress = ip;
        app.Declaration.DeviceId = deviceId;

        db.KycStatusHistory.Add(new KycStatusHistory
        {
            ApplicationId = app.Id, FromStatus = from, ToStatus = KycStatus.Pending, ChangedBy = $"partner:{partnerId}", CreatedAt = now,
        });
        QueueVerificationChecks(app, lab!);

        app.Partner.KycStatus = PartnerKycStatus.Pending;
        app.Partner.UpdatedAt = now;
        notifications.Add(UserTypes.Partner, partnerId, "kyc", "Application submitted",
            $"Your KYC application {app.ReferenceId} is under review. We'll notify you once it's verified.");
        audit.Add("PARTNER", partnerId.ToString(), "KYC_SUBMITTED", "kyc_application", app.Id.ToString());
        await db.SaveChangesAsync();

        return new SubmitResult(app.ReferenceId, app.Status, now, app.ExpectedBy.Value);
    }

    private void QueueVerificationChecks(KycApplication app, Lab lab)
    {
        var wanted = new List<string> { "PAN", "AADHAAR", "BANK_PENNY_DROP", "MEDICAL_COUNCIL", "CLINICAL_EST" };
        if (!string.IsNullOrEmpty(lab.TaxDetail?.Gstin)) wanted.Add("GSTIN");
        if (lab.HasNabl == true) wanted.Add("NABL");
        if (lab.BusinessType is "pvt_ltd" or "public_ltd" or "llp") wanted.Add("CIN_LLPIN");

        foreach (var type in wanted)
        {
            var check = app.Checks.FirstOrDefault(c => c.CheckType == type);
            if (check == null)
            {
                check = new KycVerificationCheck { Id = Guid.NewGuid(), ApplicationId = app.Id, CheckType = type };
                db.KycVerificationChecks.Add(check);
                app.Checks.Add(check);
            }
            check.Status = "QUEUED";
            check.FailureReason = null;
            check.CheckedAt = null;
        }
        foreach (var stale in app.Checks.Where(c => !wanted.Contains(c.CheckType)).ToList())
        {
            app.Checks.Remove(stale);
            db.KycVerificationChecks.Remove(stale);
        }
    }

    public async Task<KycStatusDto> StatusAsync(Guid partnerId)
    {
        var app = await LoadAsync(partnerId);
        if (app.SubmittedAt == null || app.ReferenceId == null)
            throw ApiException.NotFound("No submitted application yet.");

        var lists = await MasterListsAsync();
        return new KycStatusDto(app.ReferenceId, app.Status, app.SubmittedAt.Value, app.ExpectedBy, app.UpdatedAt,
            Timeline(app), RejectionReasons(app), Summary(app, lists));
    }

    public static IReadOnlyList<TimelineStage> Timeline(KycApplication app)
    {
        var anyChecked = app.Checks.Any(c => c.Status != "QUEUED");
        var firstChecked = app.Checks.Where(c => c.CheckedAt != null).Min(c => c.CheckedAt);
        var decided = app.Status is KycStatus.Approved or KycStatus.Rejected or KycStatus.ChangesRequested;
        return new[]
        {
            new TimelineStage("SUBMITTED", "Application submitted", "We've received your details", "DONE", app.SubmittedAt),
            new TimelineStage("DOCUMENT_REVIEW", "Documents & licences review", "Our team is checking your uploads",
                decided || anyChecked ? "DONE" : "ACTIVE", firstChecked),
            new TimelineStage("REGISTRY_CHECKS", "Registry checks", "PAN, GST, medical council and NABL verified with issuing authorities",
                decided ? "DONE" : anyChecked ? "ACTIVE" : "TODO", decided ? app.ReviewedAt : null),
            new TimelineStage("APPROVED", "Approved & live", "Start enrolling tests and receiving bookings",
                app.Status == KycStatus.Approved ? "DONE" : "TODO", app.Status == KycStatus.Approved ? app.ReviewedAt : null),
        };
    }

    public KycSummaryDto Summary(KycApplication app, KycMasterLists lists)
    {
        var lab = app.Lab;
        var bank = lab?.BankAccounts.FirstOrDefault(b => b.IsPrimary);
        return new KycSummaryDto(
            lab?.LegalName ?? "",
            lab == null ? "" : lists.BusinessTypeLabels.GetValueOrDefault(lab.BusinessType, lab.BusinessType),
            lab == null ? "" : string.Join(", ", new[] { lab.Area, lab.City, lab.State }.Where(s => !string.IsNullOrWhiteSpace(s))),
            lab?.Latitude is { } lat && lab.Longitude is { } lng
                ? $"{lat.ToString("F6", CultureInfo.InvariantCulture)}, {lng.ToString("F6", CultureInfo.InvariantCulture)}" : null,
            lab?.Services.Select(s => lists.ServiceLabels.GetValueOrDefault(s.ServiceCode, s.ServiceCode)).ToList() ?? new List<string>(),
            Masking.Pan(lab?.TaxDetail?.BusinessPan),
            lab?.TaxDetail?.Gstin,
            app.Signatory?.FullName ?? "",
            app.Signatory?.PanMasked ?? "",
            Masking.Aadhaar(app.Signatory?.AadhaarLast4),
            bank == null ? "" : $"{bank.BankName} {Masking.Account(bank.AccountLast4)}",
            bank?.Ifsc ?? "",
            app.Documents.Count(d => ActiveDocStatuses.Contains(d.Status)));
    }

    public static IReadOnlyList<RejectionReason> RejectionReasons(KycApplication app) =>
        string.IsNullOrEmpty(app.RejectionReason)
            ? Array.Empty<RejectionReason>()
            : JsonSerializer.Deserialize<List<RejectionReason>>(app.RejectionReason, Json) ?? new List<RejectionReason>();

    // ---------------------------------------------------------------- DB -> section mapping

    public static ProfileSection ToProfile(Lab l) => new()
    {
        LabLegalName = l.LegalName, LabBrandName = l.BrandName, BusinessType = l.BusinessType, EstablishedYear = l.EstablishedYear,
        Services = l.Services.Select(s => s.ServiceCode).OrderBy(s => s).ToList(),
        AddressLine1 = l.AddressLine1, AddressLine2 = l.AddressLine2, Landmark = l.Landmark, City = l.City, State = l.State,
        Pincode = l.Pincode, Area = l.Area, Latitude = l.Latitude, Longitude = l.Longitude, LocationAccuracy = l.LocationAccuracyM,
        LocationCapturedAt = l.LocationCapturedAt, LabPhone = l.LabPhone, LabEmail = l.LabEmail, Website = l.Website,
    };

    public static LicencesSection ToLicences(Lab l)
    {
        LabLicence? Lic(string type) => l.Licences.FirstOrDefault(x => x.LicenceType == type);
        var director = l.MedicalDirectors.FirstOrDefault();
        return new LicencesSection
        {
            ClinicalEstNumber = Lic(LicenceTypes.ClinicalEst)?.Number, ClinicalEstDocId = Lic(LicenceTypes.ClinicalEst)?.DocumentId,
            BmwNumber = Lic(LicenceTypes.Bmw)?.Number, BmwValidUpto = FormatDate(Lic(LicenceTypes.Bmw)?.ValidUpto), BmwDocId = Lic(LicenceTypes.Bmw)?.DocumentId,
            TradeLicenseNumber = Lic(LicenceTypes.Trade)?.Number, TradeLicenseDocId = Lic(LicenceTypes.Trade)?.DocumentId,
            AerbNumber = Lic(LicenceTypes.Aerb)?.Number, AerbDocId = Lic(LicenceTypes.Aerb)?.DocumentId,
            PcpndtNumber = Lic(LicenceTypes.Pcpndt)?.Number,
            BusinessPan = l.TaxDetail?.BusinessPan, Gstin = l.TaxDetail?.Gstin,
            EntityRegNumber = NullIfEmpty(Lic(LicenceTypes.EntityReg)?.Number), EntityRegDocId = Lic(LicenceTypes.EntityReg)?.DocumentId,
            HasNabl = l.HasNabl, NablNumber = Lic(LicenceTypes.Nabl)?.Number, NablValidUpto = FormatDate(Lic(LicenceTypes.Nabl)?.ValidUpto),
            NablDocId = Lic(LicenceTypes.Nabl)?.DocumentId,
            DirectorName = director?.Name, DirectorQualification = director?.Qualification, CouncilName = director?.CouncilName,
            MedicalRegNumber = director?.RegistrationNumber, MedicalRegDocId = director?.DocumentId,
        };
    }

    private OwnerSection ToOwnerSection(KycSignatory s) => new()
    {
        OwnerName = s.FullName, OwnerDesignation = s.Designation, OwnerDob = FormatDate(s.Dob), OwnerEmail = s.Email,
        OwnerPan = encryptor.Decrypt(s.PanEnc), AadhaarNumber = encryptor.Decrypt(s.AadhaarRef), AadhaarConsent = s.AadhaarConsent,
        PanDocId = s.PanDocId, AadhaarFrontDocId = s.AadhaarFrontDocId, AadhaarBackDocId = s.AadhaarBackDocId, SelfieDocId = s.SelfieDocId,
    };

    private BankSection ToBankSection(LabBankAccount b)
    {
        var number = encryptor.Decrypt(b.AccountNumberEnc);
        return new BankSection
        {
            AccountHolder = b.AccountHolder, AccountNumber = number, ConfirmAccountNumber = number, Ifsc = b.Ifsc,
            BankName = b.BankName, BranchName = b.BranchName, AccountType = b.AccountType, ChequeDocId = b.DocumentId,
        };
    }

    public static OwnerView ToOwnerView(KycSignatory s) => new(
        s.FullName, s.Designation, FormatDate(s.Dob)!, s.Email, s.PanMasked, s.AadhaarRef.Length > 0, Masking.Aadhaar(s.AadhaarLast4),
        s.AadhaarConsent, s.PanDocId, s.AadhaarFrontDocId, s.AadhaarBackDocId, s.SelfieDocId);

    public static BankView ToBankView(LabBankAccount b) => new(
        b.AccountHolder, b.AccountNumberEnc.Length > 0, $"{b.BankName} {Masking.Account(b.AccountLast4)}", b.Ifsc, b.BankName,
        b.BranchName, b.AccountType, b.DocumentId);

    public static OperationsSection ToOperations(Lab l) => new()
    {
        ProcessingMode = l.Operations!.ProcessingMode, HomeCollection = l.Operations.HomeCollection,
        ServiceablePincodes = l.ServiceablePincodes.Select(p => p.Pincode).OrderBy(p => p).ToList(),
        PhlebotomistCount = l.Operations.PhlebotomistCount,
        WorkingDays = SortDays(l.WorkingDays.Select(d => d.DayCode)),
        OpenTime = IstClock.FormatTime(l.Operations.OpenTime), CloseTime = IstClock.FormatTime(l.Operations.CloseTime),
        FrontPhotoDocId = l.Operations.FrontPhotoDocId, InteriorPhotoDocId = l.Operations.InteriorPhotoDocId,
    };

    // ---------------------------------------------------------------- helpers

    private static readonly string[] DayOrder = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    public static List<string> SortDays(IEnumerable<string> days) => days.OrderBy(d => Array.IndexOf(DayOrder, d)).ToList();

    private void UpsertLicence(Lab lab, string type, string? number, DateOnly? validUpto, Guid? docId)
    {
        var row = lab.Licences.FirstOrDefault(x => x.LicenceType == type);
        if (row == null)
        {
            row = new LabLicence { Id = Guid.NewGuid(), LabId = lab.Id, LicenceType = type };
            db.LabLicences.Add(row);
            lab.Licences.Add(row);
        }
        var value = number?.Trim() ?? "";
        if (row.Number != value || row.ValidUpto != validUpto || row.DocumentId != docId)
        {
            row.VerificationStatus = "NOT_CHECKED";
            row.VerifiedAt = null;
        }
        row.Number = value;
        row.ValidUpto = validUpto;
        row.DocumentId = docId;
    }

    private void RemoveLicence(Lab lab, string type)
    {
        var row = lab.Licences.FirstOrDefault(x => x.LicenceType == type);
        if (row == null) return;
        lab.Licences.Remove(row);
        db.LabLicences.Remove(row);
    }

    /// <summary>Make a child collection match <paramref name="wanted"/> without delete+insert of unchanged rows.</summary>
    private void SyncSet<T>(List<T> current, IReadOnlyCollection<string> wanted, Func<T, string> key, Func<string, T> create) where T : class
    {
        foreach (var row in current.Where(r => !wanted.Contains(key(r))).ToList())
        {
            current.Remove(row);
            db.Remove(row);
        }
        foreach (var k in wanted.Where(k => current.All(r => key(r) != k)))
        {
            var row = create(k);
            db.Add(row);
            current.Add(row);
        }
    }

    private static void Normalise(LicencesSection l)
    {
        l.BusinessPan = l.BusinessPan?.Trim().ToUpperInvariant();
        l.Gstin = NullIfEmpty(l.Gstin)?.ToUpperInvariant();
        l.EntityRegNumber = l.EntityRegNumber?.Trim().ToUpperInvariant();
        l.ClinicalEstNumber = l.ClinicalEstNumber?.Trim().ToUpperInvariant();
    }

    private static DateOnly? ParseDate(string? v) => IstClock.TryParseDate(v, out var d) ? d : null;
    private static string? FormatDate(DateOnly? d) => d == null ? null : IstClock.FormatDate(d.Value);
    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
