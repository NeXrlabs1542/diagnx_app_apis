using DiagnX.Api.Common;
using DiagnX.Api.Data.Entities;

namespace DiagnX.Api.Features.Kyc;

/// <summary>Allowed dropdown codes, loaded from master_options / master_states.</summary>
public sealed class KycMasterLists
{
    public required HashSet<string> BusinessTypes { get; init; }
    public required HashSet<string> Services { get; init; }
    public required HashSet<string> States { get; init; }
    public required HashSet<string> Qualifications { get; init; }
    public required HashSet<string> Councils { get; init; }
    public required HashSet<string> Designations { get; init; }
    public required HashSet<string> AccountTypes { get; init; }
    public required HashSet<string> StaffCounts { get; init; }
    public required HashSet<string> ProcessingModes { get; init; }
    public required HashSet<string> Weekdays { get; init; }
    public required Dictionary<string, string> BusinessTypeLabels { get; init; }
    public required Dictionary<string, string> ServiceLabels { get; init; }
}

/// <summary>Context from earlier steps that later steps' rules depend on.</summary>
public sealed record KycContext(string? BusinessType, IReadOnlyCollection<string> Services, string? BusinessPan);

/// <summary>
/// Server-side re-validation of every KYC step (VR-01..VR-20). Mirrors
/// src/utils/kycValidation.ts in the partner app so the same input gets the same answer.
/// </summary>
public sealed class KycValidator(KycMasterLists lists, Func<Guid?, string, (string Code, string Message)?> checkDoc)
{
    public FieldErrors Profile(ProfileSection p)
    {
        var e = new FieldErrors();
        e.RequireText("labLegalName", p.LabLegalName, 3, 150, "Enter the legal name as on your registration");
        e.MaxLength("labBrandName", p.LabBrandName, 100);
        if (string.IsNullOrEmpty(p.BusinessType) || !lists.BusinessTypes.Contains(p.BusinessType))
            e.Add("businessType", ErrorCodes.Required, "Select your business type");

        var year = p.EstablishedYear ?? 0;
        if (year < 1900 || year > IstClock.Today.Year) e.Add("establishedYear", ErrorCodes.InvalidYear, "Enter a valid 4-digit year");

        var services = p.Services ?? new List<string>();
        if (services.Count == 0) e.Add("services", ErrorCodes.Required, "Select at least one service");
        else if (services.Any(s => !lists.Services.Contains(s))) e.Add("services", ErrorCodes.InvalidValue, "Unknown service selected");

        e.RequireText("addressLine1", p.AddressLine1, 5, 150, "Enter the building / street address");
        e.MaxLength("addressLine2", p.AddressLine2, 150);
        e.MaxLength("landmark", p.Landmark, 100);
        e.RequireText("city", p.City, 1, 80);
        if (string.IsNullOrEmpty(p.State) || !lists.States.Contains(p.State)) e.Add("state", ErrorCodes.Required, "Select a state");
        if (!IndianIds.IsPincode(p.Pincode)) e.Add("pincode", ErrorCodes.InvalidPincode, "Enter a valid 6-digit PIN code");
        e.RequireText("area", p.Area, 2, 80, "Select or enter the area / locality");

        // Reported against `latitude` so recapturing clears it (same as the app).
        if (p.Latitude == null || p.Longitude == null)
            e.Add("latitude", ErrorCodes.InvalidLocation, "Capture your lab's GPS location to continue");
        else if (p.Latitude < 6 || p.Latitude > 38 || p.Longitude < 68 || p.Longitude > 98)
            e.Add("latitude", ErrorCodes.InvalidLocation, "This location is outside India — capture it again at the lab");

        if (!IndianIds.IsLabPhone(p.LabPhone)) e.Add("labPhone", ErrorCodes.InvalidPhone, "Enter a valid 10-digit contact number");
        if (!IndianIds.IsEmail(p.LabEmail) || p.LabEmail!.Length > 120) e.Add("labEmail", ErrorCodes.InvalidEmail, "Enter a valid email address");
        if (!string.IsNullOrWhiteSpace(p.Website) && (!IndianIds.IsWebsite(p.Website) || p.Website.Length > 150))
            e.Add("website", ErrorCodes.InvalidWebsite, "Enter a valid website, e.g. www.mylab.in");
        return e;
    }

    public FieldErrors Licences(LicencesSection l, KycContext ctx)
    {
        var e = new FieldErrors();
        e.RequireText("clinicalEstNumber", l.ClinicalEstNumber, 4, 40, "Enter your registration number");
        Doc(e, "clinicalEstDocId", l.ClinicalEstDocId, DocTypes.ClinicalEst);

        e.RequireText("bmwNumber", l.BmwNumber, 4, 40, "Enter your authorisation number");
        FutureDate(e, "bmwValidUpto", l.BmwValidUpto);
        Doc(e, "bmwDocId", l.BmwDocId, DocTypes.Bmw);

        e.RequireText("tradeLicenseNumber", l.TradeLicenseNumber, 4, 40, "Enter your licence number");
        Doc(e, "tradeLicenseDocId", l.TradeLicenseDocId, DocTypes.TradeLicence);

        if (!IndianIds.IsPan(l.BusinessPan))
            e.Add("businessPan", ErrorCodes.InvalidPan, "Enter a valid PAN, e.g. ABCDE1234F");
        else if (ctx.BusinessType != null && IndianIds.PanEntityChars.TryGetValue(ctx.BusinessType, out var allowed) && !allowed.Contains(l.BusinessPan![3]))
        {
            var label = lists.BusinessTypeLabels.GetValueOrDefault(ctx.BusinessType, "this business type").ToLowerInvariant();
            e.Add("businessPan", ErrorCodes.InvalidPan, $"4th character of the PAN should be {string.Join(" / ", allowed)} for a {label}");
        }

        if (!string.IsNullOrEmpty(l.Gstin))
        {
            if (!IndianIds.IsGstin(l.Gstin)) e.Add("gstin", ErrorCodes.InvalidGstin, "Enter a valid 15-character GSTIN");
            else if (IndianIds.IsPan(l.BusinessPan) && !IndianIds.GstinMatchesPan(l.Gstin, l.BusinessPan!))
                e.Add("gstin", ErrorCodes.InvalidGstin, "This GSTIN's PAN doesn't match the business PAN above");
        }

        switch (ctx.BusinessType)
        {
            case "pvt_ltd" or "public_ltd":
                if (!IndianIds.IsCin(l.EntityRegNumber)) e.Add("entityRegNumber", ErrorCodes.InvalidCin, "Enter a valid CIN");
                Doc(e, "entityRegDocId", l.EntityRegDocId, DocTypes.EntityReg);
                break;
            case "llp":
                if (!IndianIds.IsLlpin(l.EntityRegNumber)) e.Add("entityRegNumber", ErrorCodes.InvalidLlpin, "Enter a valid LLPIN");
                Doc(e, "entityRegDocId", l.EntityRegDocId, DocTypes.EntityReg);
                break;
            case "trust":
                e.RequireText("entityRegNumber", l.EntityRegNumber, 3, 40, "Enter a valid Registration number");
                Doc(e, "entityRegDocId", l.EntityRegDocId, DocTypes.EntityReg);
                break;
            case "partnership":
                Doc(e, "entityRegDocId", l.EntityRegDocId, DocTypes.EntityReg);
                break;
        }

        if (l.HasNabl == null) e.Add("hasNabl", ErrorCodes.Required, "Please choose Yes or No");
        if (l.HasNabl == true)
        {
            e.RequireText("nablNumber", l.NablNumber, 3, 40, "Enter your NABL certificate number");
            FutureDate(e, "nablValidUpto", l.NablValidUpto);
            Doc(e, "nablDocId", l.NablDocId, DocTypes.Nabl);
        }

        if (ctx.Services.Contains("radiology"))
        {
            e.RequireText("aerbNumber", l.AerbNumber, 3, 40, "Enter your AERB registration / licence number");
            Doc(e, "aerbDocId", l.AerbDocId, DocTypes.Aerb);
        }
        e.MaxLength("pcpndtNumber", l.PcpndtNumber, 40);

        e.RequireText("directorName", l.DirectorName, 3, 100, "Enter the in-charge doctor's full name");
        if (string.IsNullOrEmpty(l.DirectorQualification) || !lists.Qualifications.Contains(l.DirectorQualification))
            e.Add("directorQualification", ErrorCodes.Required, "Select a qualification");
        if (string.IsNullOrEmpty(l.CouncilName) || !lists.Councils.Contains(l.CouncilName))
            e.Add("councilName", ErrorCodes.Required, "Select the registering council");
        e.RequireText("medicalRegNumber", l.MedicalRegNumber, 3, 40, "Enter the medical registration number");
        Doc(e, "medicalRegDocId", l.MedicalRegDocId, DocTypes.MedicalReg);
        return e;
    }

    /// <summary>Step 3. <paramref name="aadhaarAlreadyStored"/> is true when the number was saved earlier and the app omitted it.</summary>
    public FieldErrors Owner(OwnerSection o, KycContext ctx, bool aadhaarAlreadyStored)
    {
        var e = new FieldErrors();
        e.RequireText("ownerName", o.OwnerName, 3, 100, "Enter your full name as on your PAN card");
        if (string.IsNullOrEmpty(o.OwnerDesignation) || !lists.Designations.Contains(o.OwnerDesignation))
            e.Add("ownerDesignation", ErrorCodes.Required, "Select your designation");

        if (!IstClock.TryParseDate(o.OwnerDob, out var dob)) e.Add("ownerDob", ErrorCodes.InvalidDob, "Enter a valid date of birth");
        else
        {
            var age = IstClock.AgeOn(dob, IstClock.Today);
            if (age < 18) e.Add("ownerDob", ErrorCodes.InvalidDob, "The signatory must be at least 18 years old");
            else if (age > 100) e.Add("ownerDob", ErrorCodes.InvalidDob, "Enter a valid date of birth");
        }

        if (!IndianIds.IsEmail(o.OwnerEmail) || o.OwnerEmail!.Length > 120) e.Add("ownerEmail", ErrorCodes.InvalidEmail, "Enter a valid email address");

        if (!IndianIds.IsPan(o.OwnerPan)) e.Add("ownerPan", ErrorCodes.InvalidPan, "Enter a valid PAN, e.g. ABCDE1234F");
        else if (o.OwnerPan![3] != 'P') e.Add("ownerPan", ErrorCodes.InvalidPan, "Use your personal PAN (4th character must be P)");
        else if (ctx.BusinessType == "proprietorship" && IndianIds.IsPan(ctx.BusinessPan) && o.OwnerPan != ctx.BusinessPan)
            e.Add("ownerPan", ErrorCodes.InvalidPan, "For a sole proprietorship this must match the business PAN");

        var aadhaar = IndianIds.DigitsOnly(o.AadhaarNumber);
        if (!(aadhaar.Length == 0 && aadhaarAlreadyStored) && !IndianIds.IsAadhaar(aadhaar))
            e.Add("aadhaarNumber", ErrorCodes.InvalidAadhaar, "Enter a valid 12-digit Aadhaar number");
        if (o.AadhaarConsent != true) e.Add("aadhaarConsent", ErrorCodes.ConsentRequired, "Your consent is needed to verify your Aadhaar");

        Doc(e, "panDocId", o.PanDocId, DocTypes.PanCard);
        Doc(e, "aadhaarFrontDocId", o.AadhaarFrontDocId, DocTypes.AadhaarFront);
        Doc(e, "aadhaarBackDocId", o.AadhaarBackDocId, DocTypes.AadhaarBack);
        Doc(e, "selfieDocId", o.SelfieDocId, DocTypes.Selfie, "Please take a selfie");
        return e;
    }

    public FieldErrors Bank(BankSection b, bool accountAlreadyStored)
    {
        var e = new FieldErrors();
        e.RequireText("accountHolder", b.AccountHolder, 3, 100, "Enter the name as on the bank account");
        var number = b.AccountNumber?.Trim() ?? "";
        var confirm = b.ConfirmAccountNumber?.Trim() ?? "";
        if (!(number.Length == 0 && accountAlreadyStored))
        {
            if (!IndianIds.IsAccountNumber(number)) e.Add("accountNumber", ErrorCodes.InvalidAccount, "Enter a valid account number (9–18 digits)");
            if (confirm.Length == 0) e.Add("confirmAccountNumber", ErrorCodes.Required, "Re-enter your account number");
            else if (confirm != number) e.Add("confirmAccountNumber", ErrorCodes.Mismatch, "Account numbers do not match");
        }
        if (!IndianIds.IsIfsc(b.Ifsc)) e.Add("ifsc", ErrorCodes.InvalidIfsc, "Enter a valid 11-character IFSC, e.g. HDFC0001234");
        e.RequireText("bankName", b.BankName, 2, 80, "Enter your bank name");
        e.MaxLength("branchName", b.BranchName, 80);
        if (string.IsNullOrEmpty(b.AccountType) || !lists.AccountTypes.Contains(b.AccountType))
            e.Add("accountType", ErrorCodes.Required, "Select an account type");
        Doc(e, "chequeDocId", b.ChequeDocId, DocTypes.CancelledCheque);
        return e;
    }

    public FieldErrors Operations(OperationsSection o)
    {
        var e = new FieldErrors();
        if (string.IsNullOrEmpty(o.ProcessingMode) || !lists.ProcessingModes.Contains(o.ProcessingMode))
            e.Add("processingMode", ErrorCodes.Required, "Tell us how samples are processed");

        if (o.HomeCollection == null) e.Add("homeCollection", ErrorCodes.Required, "Please choose Yes or No");
        if (o.HomeCollection == true)
        {
            var pins = o.ServiceablePincodes ?? new List<string>();
            if (pins.Count == 0) e.Add("serviceablePincodes", ErrorCodes.Required, "Add at least one PIN code you serve");
            else if (pins.Count > 30) e.Add("serviceablePincodes", ErrorCodes.InvalidPincode, "You can add up to 30 PIN codes");
            else if (pins.Any(p => !IndianIds.IsPincode(p))) e.Add("serviceablePincodes", ErrorCodes.InvalidPincode, "Enter a valid 6-digit PIN code");
            else if (pins.Distinct().Count() != pins.Count) e.Add("serviceablePincodes", ErrorCodes.InvalidPincode, "PIN codes must be unique");

            if (string.IsNullOrEmpty(o.PhlebotomistCount)) e.Add("phlebotomistCount", ErrorCodes.Required, "Select the number of phlebotomists");
            else if (!lists.StaffCounts.Contains(NormaliseStaffCount(o.PhlebotomistCount)))
                e.Add("phlebotomistCount", ErrorCodes.InvalidValue, "Select the number of phlebotomists");
        }

        var days = o.WorkingDays ?? new List<string>();
        if (days.Count == 0) e.Add("workingDays", ErrorCodes.Required, "Select at least one working day");
        else if (days.Any(d => !lists.Weekdays.Contains(d)) || days.Distinct().Count() != days.Count)
            e.Add("workingDays", ErrorCodes.InvalidValue, "Select valid working days");

        var openOk = TryHalfHour(o.OpenTime, out var open);
        var closeOk = TryHalfHour(o.CloseTime, out var close);
        if (!openOk) e.Add("openTime", ErrorCodes.InvalidHours, "Select opening time");
        if (!closeOk) e.Add("closeTime", ErrorCodes.InvalidHours, "Select closing time");
        else if (openOk && close <= open) e.Add("closeTime", ErrorCodes.InvalidHours, "Closing time must be after opening time");

        Doc(e, "frontPhotoDocId", o.FrontPhotoDocId, DocTypes.LabFrontPhoto);
        Doc(e, "interiorPhotoDocId", o.InteriorPhotoDocId, DocTypes.LabInteriorPhoto);
        return e;
    }

    public static FieldErrors Declarations(SubmitRequest s)
    {
        var e = new FieldErrors();
        if (s.DeclTrue != true) e.Add("declTrue", ErrorCodes.ConsentRequired, "Please confirm to continue");
        if (s.DeclVerify != true) e.Add("declVerify", ErrorCodes.ConsentRequired, "Please authorise verification to continue");
        if (s.DeclTerms != true) e.Add("declTerms", ErrorCodes.ConsentRequired, "Please accept the agreement to continue");
        if (string.IsNullOrWhiteSpace(s.AgreementVersion) || s.AgreementVersion.Length > 10)
            e.Add("agreementVersion", ErrorCodes.Required, "Agreement version is required");
        return e;
    }

    /// <summary>The app used an en-dash ("6–10"); the API standard is an ASCII hyphen.</summary>
    public static string NormaliseStaffCount(string value) => value.Replace('–', '-').Replace('—', '-').Trim();

    private static bool TryHalfHour(string? value, out TimeOnly time)
    {
        if (!IstClock.TryParseTime(value, out time)) return false;
        return time.Minute % 30 == 0 && time >= new TimeOnly(5, 0) && time <= new TimeOnly(23, 30);
    }

    private static void FutureDate(FieldErrors e, string field, string? value)
    {
        if (!IstClock.TryParseDate(value, out var date)) e.Add(field, ErrorCodes.InvalidDate, "Enter a valid date");
        else if (date < IstClock.Today) e.Add(field, ErrorCodes.LicenceExpired, "This has expired — upload a renewed document");
    }

    private void Doc(FieldErrors e, string field, Guid? docId, string docType, string message = "Please upload this document")
    {
        if (docId == null) { e.Add(field, ErrorCodes.DocRequired, message); return; }
        if (checkDoc(docId, docType) is { } problem) e.Add(field, problem.Code, problem.Message);
    }
}
