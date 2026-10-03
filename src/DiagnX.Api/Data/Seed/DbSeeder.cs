using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Data.Seed;

/// <summary>
/// Idempotent startup seeding:
///  • always: master dropdowns + states (spec "Master Data"), app config defaults, time slots, first admin user;
///  • when Seed:DemoData = true and the catalog is empty: the prototype's categories, tests, report templates,
///    5 Hyderabad labs with prices, banners, health tips and one demo partner login that owns a live lab.
/// </summary>
public sealed class DbSeeder(AppDbContext db, IConfiguration config, IHostEnvironment env, IPasswordHasher<AdminUser> hasher, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync()
    {
        await SeedMasterOptionsAsync();
        await SeedStatesAsync();
        await SeedConfigAsync();
        await SeedTimeSlotsAsync();
        await SeedAdminAsync();
        if (config.GetValue("Seed:DemoData", true) && !await db.Tests.AnyAsync())
        {
            await SeedCatalogAsync();
            logger.LogInformation("Seeded demo catalog, labs and prices");
        }
    }

    // ------------------------------------------------------------------ master data

    private static readonly (string Group, string Code, string Label, string? Hint)[] Options =
    {
        ("BUSINESS_TYPE", "proprietorship", "Sole proprietorship", null),
        ("BUSINESS_TYPE", "partnership", "Partnership firm", null),
        ("BUSINESS_TYPE", "llp", "Limited Liability Partnership (LLP)", null),
        ("BUSINESS_TYPE", "pvt_ltd", "Private Limited company", null),
        ("BUSINESS_TYPE", "public_ltd", "Public Limited company", null),
        ("BUSINESS_TYPE", "trust", "Trust / Society / Hospital unit", null),
        ("SERVICE", "pathology", "Pathology", null),
        ("SERVICE", "radiology", "Radiology & imaging", "Makes AERB fields mandatory"),
        ("SERVICE", "cardiac", "Cardiac diagnostics", null),
        ("PROCESSING_MODE", "in_house", "In-house lab", "We process samples at our own lab"),
        ("PROCESSING_MODE", "outsourced", "Reference lab", "We collect, a partner lab processes"),
        ("DESIGNATION", "Proprietor", "Proprietor", null),
        ("DESIGNATION", "Partner", "Partner", null),
        ("DESIGNATION", "Director", "Director", null),
        ("DESIGNATION", "Trustee", "Trustee", null),
        ("DESIGNATION", "Authorised signatory", "Authorised signatory", null),
        ("QUALIFICATION", "MD Pathology", "MD Pathology", null),
        ("QUALIFICATION", "MD Microbiology", "MD Microbiology", null),
        ("QUALIFICATION", "MD Biochemistry", "MD Biochemistry", null),
        ("QUALIFICATION", "DNB Pathology", "DNB Pathology", null),
        ("QUALIFICATION", "DCP (Diploma in Clinical Pathology)", "DCP (Diploma in Clinical Pathology)", null),
        ("QUALIFICATION", "MD Radiodiagnosis", "MD Radiodiagnosis", null),
        ("QUALIFICATION", "MBBS", "MBBS", null),
        ("QUALIFICATION", "Other", "Other", null),
        ("ACCOUNT_TYPE", "Current", "Current account", null),
        ("ACCOUNT_TYPE", "Savings", "Savings account", null),
        ("STAFF_COUNT", "1", "1", null), ("STAFF_COUNT", "2", "2", null), ("STAFF_COUNT", "3", "3", null),
        ("STAFF_COUNT", "4", "4", null), ("STAFF_COUNT", "5", "5", null), ("STAFF_COUNT", "6-10", "6-10", null),
        ("STAFF_COUNT", "11-20", "11-20", null), ("STAFF_COUNT", "20+", "20+", null),
        ("WEEKDAY", "Mon", "Mon", null), ("WEEKDAY", "Tue", "Tue", null), ("WEEKDAY", "Wed", "Wed", null),
        ("WEEKDAY", "Thu", "Thu", null), ("WEEKDAY", "Fri", "Fri", null), ("WEEKDAY", "Sat", "Sat", null),
        ("WEEKDAY", "Sun", "Sun", null),
    };

    // (short code, name, GST state code, type)
    private static readonly (string Code, string Name, string Gst, string Type)[] States =
    {
        ("AN", "Andaman and Nicobar Islands", "35", "UT"), ("AP", "Andhra Pradesh", "37", "STATE"),
        ("AR", "Arunachal Pradesh", "12", "STATE"), ("AS", "Assam", "18", "STATE"), ("BR", "Bihar", "10", "STATE"),
        ("CH", "Chandigarh", "04", "UT"), ("CG", "Chhattisgarh", "22", "STATE"),
        ("DN", "Dadra and Nagar Haveli and Daman and Diu", "26", "UT"), ("DL", "Delhi", "07", "UT"), ("GA", "Goa", "30", "STATE"),
        ("GJ", "Gujarat", "24", "STATE"), ("HR", "Haryana", "06", "STATE"), ("HP", "Himachal Pradesh", "02", "STATE"),
        ("JK", "Jammu and Kashmir", "01", "UT"), ("JH", "Jharkhand", "20", "STATE"), ("KA", "Karnataka", "29", "STATE"),
        ("KL", "Kerala", "32", "STATE"), ("LA", "Ladakh", "38", "UT"), ("LD", "Lakshadweep", "31", "UT"),
        ("MP", "Madhya Pradesh", "23", "STATE"), ("MH", "Maharashtra", "27", "STATE"), ("MN", "Manipur", "14", "STATE"),
        ("ML", "Meghalaya", "17", "STATE"), ("MZ", "Mizoram", "15", "STATE"), ("NL", "Nagaland", "13", "STATE"),
        ("OD", "Odisha", "21", "STATE"), ("PY", "Puducherry", "34", "UT"), ("PB", "Punjab", "03", "STATE"),
        ("RJ", "Rajasthan", "08", "STATE"), ("SK", "Sikkim", "11", "STATE"), ("TN", "Tamil Nadu", "33", "STATE"),
        ("TS", "Telangana", "36", "STATE"), ("TR", "Tripura", "16", "STATE"), ("UP", "Uttar Pradesh", "09", "STATE"),
        ("UK", "Uttarakhand", "05", "STATE"), ("WB", "West Bengal", "19", "STATE"),
    };

    private async Task SeedMasterOptionsAsync()
    {
        var existing = (await db.MasterOptions.Select(o => o.GroupCode + "|" + o.Code).ToListAsync()).ToHashSet();
        var all = Options.ToList();
        all.Add(("MEDICAL_COUNCIL", "National Medical Commission (NMC)", "National Medical Commission (NMC)", null));
        all.AddRange(States.OrderBy(s => s.Name).Select(s => ("MEDICAL_COUNCIL", $"{s.Name} Medical Council", $"{s.Name} Medical Council", (string?)null)));

        var order = new Dictionary<string, short>();
        foreach (var (group, code, label, hint) in all)
        {
            order[group] = (short)(order.GetValueOrDefault(group) + 1);
            if (existing.Contains(group + "|" + code)) continue;
            db.MasterOptions.Add(new MasterOption { GroupCode = group, Code = code, Label = label, Hint = hint, SortOrder = order[group] });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedStatesAsync()
    {
        var existing = (await db.MasterStates.Select(s => s.Code).ToListAsync()).ToHashSet();
        foreach (var s in States.Where(s => !existing.Contains(s.Code)))
            db.MasterStates.Add(new MasterState { Code = s.Code, Name = s.Name, GstStateCode = s.Gst, Type = s.Type });
        await db.SaveChangesAsync();
    }

    private async Task SeedConfigAsync()
    {
        var existing = (await db.AppConfig.Select(c => c.Key).ToListAsync()).ToHashSet();
        foreach (var (k, v) in AppConfigService.Defaults.Where(kv => !existing.Contains(kv.Key)))
            db.AppConfig.Add(new AppConfig { Key = k, Value = v, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task SeedTimeSlotsAsync()
    {
        if (await db.TimeSlots.AnyAsync()) return;
        (int H, string Label, string Period)[] slots =
        {
            (6, "6:00 – 7:00 AM", "Morning"), (7, "7:00 – 8:00 AM", "Morning"), (8, "8:00 – 9:00 AM", "Morning"),
            (9, "9:00 – 10:00 AM", "Morning"), (12, "12:00 – 1:00 PM", "Afternoon"), (14, "2:00 – 3:00 PM", "Afternoon"),
            (16, "4:00 – 5:00 PM", "Afternoon"), (18, "6:00 – 7:00 PM", "Evening"), (19, "7:00 – 8:00 PM", "Evening"),
        };
        short i = 0;
        foreach (var s in slots)
            db.TimeSlots.Add(new TimeSlot
            {
                Id = Guid.NewGuid(), Label = s.Label, Period = s.Period, StartTime = new TimeOnly(s.H, 0), EndTime = new TimeOnly(s.H + 1, 0),
                SortOrder = i++,
            });
        await db.SaveChangesAsync();
    }

    private async Task SeedAdminAsync()
    {
        if (await db.AdminUsers.AnyAsync()) return;
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (!env.IsDevelopment())
            {
                logger.LogWarning("No admin user exists. Set Admin__Email and Admin__Password to create the first one.");
                return;
            }
            (email, password) = ("admin@diagnx.local", "Admin@12345");
        }
        var admin = new AdminUser { Id = Guid.NewGuid(), Email = email.Trim().ToLowerInvariant(), Name = "DiagnX Admin", Role = "SUPER_ADMIN", CreatedAt = DateTime.UtcNow };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Created first admin user {Email}", admin.Email);
    }

    // ------------------------------------------------------------------ demo catalog (from the patient-app prototype)

    private sealed record P(string Name, string Unit, string Range, decimal? Low, decimal? High);

    private async Task SeedCatalogAsync()
    {
        var now = DateTime.UtcNow;

        // Categories: primary categories (badge on each test) + the home "Browse by category" strip.
        var cats = new Dictionary<string, TestCategory>();
        void Cat(string slug, string name, string icon, string color, bool home, short order) =>
            cats[slug] = new TestCategory { Id = Guid.NewGuid(), Slug = slug, Name = name, Icon = icon, Color = color, ShowOnHome = home, SortOrder = order };
        Cat("full-body", "Full Body", "body", "#E27C8F", true, 1);
        Cat("diabetes", "Diabetes", "water", "#8DB8FF", true, 2);
        Cat("heart", "Heart", "heart", "#FF8A80", true, 3);
        Cat("thyroid", "Thyroid", "pulse", "#B79CFF", true, 4);
        Cat("womens-health", "Women's Health", "flower", "#F5B54A", true, 5);
        Cat("vitamins", "Vitamins", "sunny", "#4CC38A", true, 6);
        Cat("blood-basics", "Blood Basics", "water-outline", "#E27C8F", false, 10);
        Cat("heart-health", "Heart Health", "heart-outline", "#FF8A80", false, 11);
        Cat("hormones", "Hormones", "pulse-outline", "#B79CFF", false, 12);
        Cat("organ-health", "Organ Health", "fitness-outline", "#4CC38A", false, 13);
        Cat("packages", "Packages", "gift-outline", "#F5B54A", false, 14);
        db.TestCategories.AddRange(cats.Values);

        var tests = new Dictionary<string, DiagnosticTest>();
        short sort = 0;
        void Test(string slug, string name, string cat, string[] extra, bool fasting, string sample, short paramCount, string desc,
            bool popular, P[] parameters, string[]? included = null)
        {
            var t = new DiagnosticTest
            {
                Id = Guid.NewGuid(), Slug = slug, Name = name, CategoryId = cats[cat].Id, FastingRequired = fasting, SampleType = sample,
                ParameterCount = paramCount, Description = desc, IsPopular = popular, IsPackage = included != null,
                IncludedTests = included?.ToList() ?? new List<string>(), SortOrder = sort++, CreatedAt = now, UpdatedAt = now,
            };
            short i = 0;
            t.Parameters = parameters.Select(p => new TestParameter
            {
                Id = Guid.NewGuid(), TestId = t.Id, Name = p.Name, Unit = p.Unit, ReferenceRange = p.Range, RefLow = p.Low, RefHigh = p.High, SortOrder = i++,
            }).ToList();
            t.CategoryLinks = extra.Select(c => new TestCategoryLink { TestId = t.Id, CategoryId = cats[c].Id }).ToList();
            tests[slug] = t;
        }

        var hb = new P("Hemoglobin", "g/dL", "13.0 – 17.0", 13.0m, 17.0m);
        var tsh = new P("TSH", "µIU/mL", "0.4 – 4.0", 0.4m, 4.0m);
        var vitd = new P("Vitamin D (25-OH)", "ng/mL", "30 – 100", 30m, 100m);
        var b12 = new P("Vitamin B12", "pg/mL", "211 – 911", 211m, 911m);
        var chol = new P("Total Cholesterol", "mg/dL", "< 200", null, 199.9m);
        var sgpt = new P("SGPT (ALT)", "U/L", "5 – 45", 5m, 45m);
        var creat = new P("Creatinine", "mg/dL", "0.6 – 1.3", 0.6m, 1.3m);

        Test("test-cbc", "Complete Blood Count (CBC)", "blood-basics", new[] { "full-body" }, false, "Blood (EDTA)", 24,
            "Measures red cells, white cells and platelets to screen for anemia, infection and general health.", true, new[]
            {
                hb, new P("RBC Count", "mill/µL", "4.5 – 5.5", 4.5m, 5.5m), new P("WBC Count", "thou/µL", "4.0 – 10.0", 4.0m, 10.0m),
                new P("Platelet Count", "lakh/µL", "1.5 – 4.1", 1.5m, 4.1m), new P("Hematocrit (PCV)", "%", "40 – 50", 40m, 50m),
            });
        Test("test-lipid", "Lipid Profile", "heart-health", new[] { "heart" }, true, "Blood (Serum)", 8,
            "Checks total cholesterol, HDL, LDL and triglycerides to assess cardiovascular risk.", true, new[]
            {
                chol, new P("HDL Cholesterol", "mg/dL", "> 40", 40.01m, null), new P("LDL Cholesterol", "mg/dL", "< 100", null, 99.9m),
                new P("Triglycerides", "mg/dL", "< 150", null, 149.9m),
            });
        Test("test-thyroid", "Thyroid Profile (T3, T4, TSH)", "hormones", new[] { "thyroid", "womens-health" }, false, "Blood (Serum)", 3,
            "Evaluates thyroid gland function and screens for hypo/hyperthyroidism.", true, new[]
            {
                tsh, new P("Total T3", "ng/mL", "0.8 – 2.0", 0.8m, 2.0m), new P("Total T4", "µg/dL", "5.0 – 12.0", 5.0m, 12.0m),
            });
        Test("test-hba1c", "HbA1c (Diabetes Screen)", "diabetes", Array.Empty<string>(), false, "Blood (EDTA)", 1,
            "Shows average blood sugar levels over the past 3 months.", true, new[]
            {
                new P("HbA1c", "%", "< 5.7", null, 5.6m), new P("Estimated Avg. Glucose", "mg/dL", "70 – 126", 70m, 126m),
            });
        Test("test-liver", "Liver Function Test (LFT)", "organ-health", Array.Empty<string>(), true, "Blood (Serum)", 11,
            "Assesses liver enzymes, bilirubin and proteins for liver health.", false, new[]
            {
                new P("SGOT (AST)", "U/L", "5 – 40", 5m, 40m), sgpt, new P("Total Bilirubin", "mg/dL", "0.2 – 1.2", 0.2m, 1.2m),
                new P("Total Protein", "g/dL", "6.0 – 8.3", 6.0m, 8.3m),
            });
        Test("test-kidney", "Kidney Function Test (KFT)", "organ-health", Array.Empty<string>(), false, "Blood (Serum)", 9,
            "Checks creatinine, urea and electrolytes for kidney performance.", false, new[]
            {
                creat, new P("Urea", "mg/dL", "15 – 40", 15m, 40m), new P("Uric Acid", "mg/dL", "3.5 – 7.2", 3.5m, 7.2m),
            });
        Test("test-vitd", "Vitamin D (25-OH)", "vitamins", new[] { "womens-health" }, false, "Blood (Serum)", 1,
            "Measures vitamin D levels to check for deficiency.", true, new[] { vitd });
        Test("test-vitb12", "Vitamin B12", "vitamins", new[] { "womens-health" }, false, "Blood (Serum)", 1,
            "Checks B12 levels linked to nerve health and energy.", false, new[] { b12 });
        Test("pkg-full-body", "Full Body Checkup — Essential", "packages", new[] { "full-body", "diabetes" }, true, "Blood + Urine", 72,
            "A comprehensive screen covering blood count, sugar, lipids, liver, kidney and thyroid.", false, new[]
            {
                hb, new P("Fasting Glucose", "mg/dL", "70 – 100", 70m, 100m), chol, sgpt, creat, tsh,
            }, new[] { "Complete Blood Count", "Lipid Profile", "Liver Function Test", "Kidney Function Test", "Thyroid Profile", "HbA1c", "Urine Routine" });
        Test("pkg-womens-health", "Women's Wellness Panel", "packages", new[] { "womens-health", "full-body" }, true, "Blood", 58,
            "Covers hormones, iron, thyroid, vitamin levels and general wellness markers for women.", false, new[]
            {
                new P("Hemoglobin", "g/dL", "12.0 – 15.5", 12.0m, 15.5m), tsh, vitd, b12, new P("Ferritin", "ng/mL", "15 – 150", 15m, 150m),
            }, new[] { "CBC", "Thyroid Profile", "Vitamin D", "Vitamin B12", "Iron Studies", "HbA1c" });
        db.Tests.AddRange(tests.Values);

        // Labs (Hyderabad), priced with the prototype's deterministic formula.
        var basePrice = new Dictionary<string, int>
        {
            ["test-cbc"] = 349, ["test-lipid"] = 599, ["test-thyroid"] = 449, ["test-hba1c"] = 399, ["test-liver"] = 649,
            ["test-kidney"] = 599, ["test-vitd"] = 999, ["test-vitb12"] = 799, ["pkg-full-body"] = 1499, ["pkg-womens-health"] = 1899,
        };
        var labs = new[]
        {
            MakeLab("Vitalis Diagnostics", "Kondapur", "500084", 17.4600m, 78.3640m, 2014, true, true, 4.8m, 2140, 12, true, true, "#B4536A", 1.00m,
                new[] { "500084", "500081", "500032", "500033", "500019" }),
            MakeLab("MedCore Labs", "Gachibowli", "500032", 17.4401m, 78.3489m, 2011, true, false, 4.6m, 1580, 24, true, true, "#4A6FA5", 0.90m,
                new[] { "500032", "500084", "500019", "500046", "500008" }),
            MakeLab("HealthFirst Path Lab", "Madhapur", "500081", 17.4483m, 78.3915m, 2018, true, true, 4.4m, 940, 18, true, false, "#C8643F", 0.95m,
                new[] { "500081", "500033", "500034", "500084" }),
            MakeLab("Accura Diagnostics", "Kukatpally", "500072", 17.4948m, 78.3996m, 2016, false, true, 4.2m, 610, 24, false, true, "#7B5EA7", 0.82m,
                Array.Empty<string>()),
            MakeLab("Prime Pathology Centre", "Miyapur", "500049", 17.4968m, 78.3614m, 2009, true, false, 4.7m, 1275, 8, true, true, "#A97A2B", 1.08m,
                new[] { "500049", "500050", "500072", "500084", "500019" }),
        };

        foreach (var (lab, factor) in labs)
        {
            db.Labs.Add(lab);
            foreach (var (slug, price) in basePrice)
            {
                var p = Math.Round(price * factor / 10m) * 10 - 1;
                var mrp = Math.Round(p * 1.35m / 10m) * 10 - 1;
                db.LabTests.Add(new LabTest { LabId = lab.Id, TestId = tests[slug].Id, Price = p, Mrp = mrp, UpdatedAt = now });
            }
        }

        (string Name, string Phone)[][] staff =
        {
            new[] { ("Ravi Kumar", "9000011122"), ("Anil Reddy", "9000011133") },
            new[] { ("Kiran Rao", "9000022211") },
            new[] { ("Sandeep Goud", "9000044411") },
            Array.Empty<(string, string)>(),
            new[] { ("Suresh Naidu", "9000033344") },
        };
        for (var i = 0; i < labs.Length; i++)
            foreach (var (name, phone) in staff[i])
                db.Phlebotomists.Add(new Phlebotomist { Id = Guid.NewGuid(), LabId = labs[i].Lab.Id, Name = name, Phone = phone, CreatedAt = now });

        // A demo partner login that owns the first lab, so the partner-side order flow can be tested end to end.
        var demoPhone = config["Seed:DemoPartnerPhone"] ?? "9000000001";
        if (!await db.Partners.AnyAsync(p => p.Phone == demoPhone))
        {
            var partner = new Partner { Id = Guid.NewGuid(), Phone = demoPhone, KycStatus = PartnerKycStatus.Approved, CreatedAt = now, UpdatedAt = now };
            var app = new KycApplication
            {
                Id = Guid.NewGuid(), PartnerId = partner.Id, ReferenceId = "DXP-DEMO-000001", Status = KycStatus.Approved, CurrentStep = 6,
                CompletedSteps = new List<short> { 1, 2, 3, 4, 5 }, SubmittedAt = now, ExpectedBy = now, ReviewedAt = now, CreatedAt = now, UpdatedAt = now,
            };
            db.Partners.Add(partner);
            db.KycApplications.Add(app);
            labs[0].Lab.PartnerId = partner.Id;
            labs[0].Lab.ApplicationId = app.Id;
        }

        db.PromoBanners.AddRange(
            new PromoBanner { Id = Guid.NewGuid(), Title = "Free home collection", Subtitle = "On packages above ₹999, this week only", Icon = "bicycle", GradientFrom = "#C9687D", GradientTo = "#8E3A4E", SortOrder = 1 },
            new PromoBanner { Id = Guid.NewGuid(), Title = "Full Body Checkup at 40% off", Subtitle = "Includes 72 parameters · Reports in 24 hrs", Icon = "body", GradientFrom = "#8C4A63", GradientTo = "#4A2233", TargetTestSlug = "pkg-full-body", SortOrder = 2 },
            new PromoBanner { Id = Guid.NewGuid(), Title = "Refer a friend, earn ₹150", Subtitle = "Credited instantly after their first booking", Icon = "gift", GradientFrom = "#7B5EA7", GradientTo = "#3E2A5C", SortOrder = 3 });
        db.HealthTips.AddRange(
            new HealthTip { Id = Guid.NewGuid(), Title = "Why fasting matters for lipid tests", Body = "A 10–12 hour fast keeps triglycerides from skewing your cholesterol numbers — water is fine.", Icon = "water-outline", ReadMins = 2, SortOrder = 1 },
            new HealthTip { Id = Guid.NewGuid(), Title = "HbA1c vs. fasting sugar", Body = "HbA1c reflects your average blood sugar over 3 months, so one heavy meal won't move it.", Icon = "pulse-outline", ReadMins = 3, SortOrder = 2 },
            new HealthTip { Id = Guid.NewGuid(), Title = "Vitamin D deficiency is common indoors", Body = "15–20 minutes of morning sun a few times a week goes a long way — get levels checked yearly.", Icon = "sunny-outline", ReadMins = 2, SortOrder = 3 });

        await db.SaveChangesAsync();

        (Lab Lab, decimal Factor) MakeLab(string name, string area, string pin, decimal lat, decimal lng, short since, bool nabl, bool iso,
            decimal rating, int reviews, short tat, bool home, bool walkIn, string color, decimal factor, string[] pins)
        {
            var id = Guid.NewGuid();
            var lab = new Lab
            {
                Id = id, LegalName = $"{name} Pvt Ltd", BrandName = name, BusinessType = "pvt_ltd", EstablishedYear = since,
                AddressLine1 = $"{area} Main Road", Area = area, City = "Hyderabad", State = "Telangana", Pincode = pin,
                Latitude = lat, Longitude = lng, LabPhone = "4040000000", LabEmail = $"contact@{name.Split(' ')[0].ToLowerInvariant()}.example",
                HasNabl = nabl, IsoCertified = iso, IsActive = true, RatingAvg = rating, RatingCount = reviews, TurnaroundHours = tat,
                WalkIn = walkIn, LogoColor = color, SlotCapacity = 4, CreatedAt = now, UpdatedAt = now,
                Operations = new LabOperations
                {
                    LabId = id, ProcessingMode = "in_house", HomeCollection = home, PhlebotomistCount = home ? "3" : null,
                    OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(21, 0),
                },
                WorkingDays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }.Select(d => new LabWorkingDay { LabId = id, DayCode = d }).ToList(),
                ServiceablePincodes = pins.Select(p => new LabServiceablePincode { LabId = id, Pincode = p }).ToList(),
                Services = new List<LabService> { new() { LabId = id, ServiceCode = "pathology" } },
            };
            if (nabl)
                lab.Licences.Add(new LabLicence { Id = Guid.NewGuid(), LabId = id, LicenceType = LicenceTypes.Nabl, Number = $"MC-{1000 + since}", VerificationStatus = "VERIFIED" });
            lab.MedicalDirectors.Add(new LabMedicalDirector
            {
                Id = Guid.NewGuid(), LabId = id, Name = "Dr. Ramesh Rao", Qualification = "MD Pathology", CouncilName = "Telangana Medical Council",
                RegistrationNumber = $"TSMC/{40000 + since}", VerificationStatus = "VERIFIED",
            });
            return (lab, factor);
        }
    }
}
