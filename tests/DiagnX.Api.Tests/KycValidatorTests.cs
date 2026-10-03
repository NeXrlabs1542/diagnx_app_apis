using DiagnX.Api.Common;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Kyc;

namespace DiagnX.Api.Tests;

public class KycValidatorTests
{
    private static readonly KycMasterLists Lists = new()
    {
        BusinessTypes = new() { "proprietorship", "partnership", "llp", "pvt_ltd", "public_ltd", "trust" },
        Services = new() { "pathology", "radiology", "cardiac" },
        States = new() { "Telangana", "Karnataka" },
        Qualifications = new() { "MD Pathology" },
        Councils = new() { "Telangana Medical Council" },
        Designations = new() { "Director", "Proprietor" },
        AccountTypes = new() { "Current", "Savings" },
        StaffCounts = new() { "1", "2", "3", "4", "5", "6-10", "11-20", "20+" },
        ProcessingModes = new() { "in_house", "outsourced" },
        Weekdays = new() { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
        BusinessTypeLabels = new() { ["pvt_ltd"] = "Private Limited company", ["proprietorship"] = "Sole proprietorship" },
        ServiceLabels = new() { ["pathology"] = "Pathology" },
    };

    // Every doc id is accepted as the right type unless it is Guid.Empty ("wrong type" in these tests).
    private static readonly KycValidator Validator = new(Lists, (id, _) =>
        id == Guid.Empty ? (ErrorCodes.DocWrongType, "wrong type") : null);

    private static ProfileSection ValidProfile() => new()
    {
        LabLegalName = "Sri Sai Diagnostics Pvt Ltd", BusinessType = "pvt_ltd", EstablishedYear = 2015, Services = new() { "pathology" },
        AddressLine1 = "12-4-56, Road No. 3, Banjara Hills", City = "Hyderabad", State = "Telangana", Pincode = "500034",
        Area = "Banjara Hills", Latitude = 17.423912m, Longitude = 78.473802m, LabPhone = "4023456789", LabEmail = "reports@sai.in",
    };

    [Fact]
    public void ValidProfilePasses() => Assert.False(Validator.Profile(ValidProfile()).Any);

    [Fact]
    public void ProfileRejectsLocationOutsideIndia()
    {
        var p = ValidProfile();
        (p.Latitude, p.Longitude) = (51.5m, -0.12m); // London
        var e = Validator.Profile(p);
        Assert.Contains(e.Items, f => f.Field == "latitude" && f.Code == ErrorCodes.InvalidLocation);
    }

    [Fact]
    public void ProfileRequiresGps()
    {
        var p = ValidProfile();
        p.Latitude = null;
        Assert.Contains(Validator.Profile(p).Items, f => f.Field == "latitude");
    }

    private static LicencesSection ValidLicences() => new()
    {
        ClinicalEstNumber = "TSCEA/HYD/2019/004521", ClinicalEstDocId = Guid.NewGuid(),
        BmwNumber = "TSPCB/BMW/2023/11876", BmwValidUpto = "2030-12-31", BmwDocId = Guid.NewGuid(),
        TradeLicenseNumber = "GHMC/SEA/2019/778812", TradeLicenseDocId = Guid.NewGuid(),
        BusinessPan = "AAACS1234C", Gstin = "36AAACS1234C1ZZ",
        EntityRegNumber = "U85110TG2015PTC123456", EntityRegDocId = Guid.NewGuid(),
        HasNabl = false, DirectorName = "Dr. Ramesh Rao", DirectorQualification = "MD Pathology",
        CouncilName = "Telangana Medical Council", MedicalRegNumber = "TSMC/45678", MedicalRegDocId = Guid.NewGuid(),
    };

    private static readonly KycContext PvtLtd = new("pvt_ltd", new[] { "pathology" }, null);

    [Fact]
    public void ValidLicencesPass() => Assert.False(Validator.Licences(ValidLicences(), PvtLtd).Any);

    [Fact]
    public void PanFourthCharMustMatchBusinessType()
    {
        var l = ValidLicences();
        l.BusinessPan = "AAAPA1234C"; // 4th char P = individual, not a company
        l.Gstin = null;
        var e = Validator.Licences(l, PvtLtd);
        var err = Assert.Single(e.Items, f => f.Field == "businessPan");
        Assert.Contains("should be C", err.Message);
    }

    [Fact]
    public void GstinMustEmbedBusinessPan()
    {
        var l = ValidLicences();
        l.BusinessPan = "AAACX1234C";
        Assert.Contains(Validator.Licences(l, PvtLtd).Items, f => f.Field == "gstin");
    }

    [Fact]
    public void ExpiredBmwIsRejected()
    {
        var l = ValidLicences();
        l.BmwValidUpto = "2020-01-01";
        Assert.Contains(Validator.Licences(l, PvtLtd).Items, f => f.Field == "bmwValidUpto" && f.Code == ErrorCodes.LicenceExpired);
    }

    [Fact]
    public void RadiologyRequiresAerb()
    {
        var e = Validator.Licences(ValidLicences(), PvtLtd with { Services = new[] { "pathology", "radiology" } });
        Assert.Contains(e.Items, f => f.Field == "aerbNumber");
        Assert.Contains(e.Items, f => f.Field == "aerbDocId");
    }

    [Fact]
    public void NablYesRequiresCertificate()
    {
        var l = ValidLicences();
        l.HasNabl = true;
        var e = Validator.Licences(l, PvtLtd);
        Assert.Contains(e.Items, f => f.Field == "nablNumber");
        Assert.Contains(e.Items, f => f.Field == "nablDocId");
    }

    [Fact]
    public void WrongDocTypeIsReported()
    {
        var l = ValidLicences();
        l.ClinicalEstDocId = Guid.Empty;
        Assert.Contains(Validator.Licences(l, PvtLtd).Items, f => f.Field == "clinicalEstDocId" && f.Code == ErrorCodes.DocWrongType);
    }

    private static OwnerSection ValidOwner() => new()
    {
        OwnerName = "Rajesh Kumar Sharma", OwnerDesignation = "Director", OwnerDob = "1980-06-15", OwnerEmail = "rajesh@sai.in",
        OwnerPan = "ABCPS1234D", AadhaarNumber = "234567890124", AadhaarConsent = true,
        PanDocId = Guid.NewGuid(), AadhaarFrontDocId = Guid.NewGuid(), AadhaarBackDocId = Guid.NewGuid(), SelfieDocId = Guid.NewGuid(),
    };

    [Fact]
    public void ValidOwnerPasses() => Assert.False(Validator.Owner(ValidOwner(), PvtLtd, false).Any);

    [Fact]
    public void OwnerMustBeAdult()
    {
        var o = ValidOwner();
        o.OwnerDob = IstClock.FormatDate(IstClock.Today.AddYears(-14));
        Assert.Contains(Validator.Owner(o, PvtLtd, false).Items, f => f.Field == "ownerDob");
    }

    [Fact]
    public void AadhaarMayBeOmittedWhenAlreadyStored()
    {
        var o = ValidOwner();
        o.AadhaarNumber = null;
        Assert.Contains(Validator.Owner(o, PvtLtd, aadhaarAlreadyStored: false).Items, f => f.Field == "aadhaarNumber");
        Assert.DoesNotContain(Validator.Owner(o, PvtLtd, aadhaarAlreadyStored: true).Items, f => f.Field == "aadhaarNumber");
    }

    [Fact]
    public void ProprietorPanMustEqualBusinessPan()
    {
        var ctx = new KycContext("proprietorship", new[] { "pathology" }, "ABCPX9999Z");
        Assert.Contains(Validator.Owner(ValidOwner(), ctx, false).Items, f => f.Field == "ownerPan");
    }

    [Fact]
    public void BankAccountsMustMatch()
    {
        var b = new BankSection
        {
            AccountHolder = "Sri Sai Diagnostics Pvt Ltd", AccountNumber = "123456789012", ConfirmAccountNumber = "123456789013",
            Ifsc = "HDFC0001234", BankName = "HDFC Bank", AccountType = "Current", ChequeDocId = Guid.NewGuid(),
        };
        Assert.Contains(Validator.Bank(b, false).Items, f => f.Field == "confirmAccountNumber" && f.Code == ErrorCodes.Mismatch);
    }

    [Fact]
    public void OperationsHoursAndEnDash()
    {
        var o = new OperationsSection
        {
            ProcessingMode = "in_house", HomeCollection = true, ServiceablePincodes = new() { "500034", "500081" },
            PhlebotomistCount = "6–10", // en-dash from the app
            WorkingDays = new() { "Mon", "Tue" }, OpenTime = "20:00", CloseTime = "09:00",
            FrontPhotoDocId = Guid.NewGuid(), InteriorPhotoDocId = Guid.NewGuid(),
        };
        var e = Validator.Operations(o);
        Assert.DoesNotContain(e.Items, f => f.Field == "phlebotomistCount");
        Assert.Contains(e.Items, f => f.Field == "closeTime" && f.Code == ErrorCodes.InvalidHours);
    }

    [Fact]
    public void DeclarationsMustAllBeTrue()
    {
        var e = KycValidator.Declarations(new SubmitRequest { DeclTrue = true, DeclVerify = false, DeclTerms = true, AgreementVersion = "v1.0" });
        Assert.Single(e.Items);
        Assert.Equal("declVerify", e.Items[0].Field);
    }
}
