using DiagnX.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Identity
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<OtpRequest> OtpRequests => Set<OtpRequest>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    // KYC + labs
    public DbSet<KycApplication> KycApplications => Set<KycApplication>();
    public DbSet<Lab> Labs => Set<Lab>();
    public DbSet<LabService> LabServices => Set<LabService>();
    public DbSet<LabLicence> LabLicences => Set<LabLicence>();
    public DbSet<LabTaxDetail> LabTaxDetails => Set<LabTaxDetail>();
    public DbSet<LabMedicalDirector> LabMedicalDirectors => Set<LabMedicalDirector>();
    public DbSet<KycSignatory> KycSignatories => Set<KycSignatory>();
    public DbSet<LabBankAccount> LabBankAccounts => Set<LabBankAccount>();
    public DbSet<LabOperations> LabOperations => Set<LabOperations>();
    public DbSet<LabWorkingDay> LabWorkingDays => Set<LabWorkingDay>();
    public DbSet<LabServiceablePincode> LabServiceablePincodes => Set<LabServiceablePincode>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<KycDeclaration> KycDeclarations => Set<KycDeclaration>();
    public DbSet<KycStatusHistory> KycStatusHistory => Set<KycStatusHistory>();
    public DbSet<KycVerificationCheck> KycVerificationChecks => Set<KycVerificationCheck>();

    // Catalog
    public DbSet<TestCategory> TestCategories => Set<TestCategory>();
    public DbSet<DiagnosticTest> Tests => Set<DiagnosticTest>();
    public DbSet<TestParameter> TestParameters => Set<TestParameter>();
    public DbSet<TestCategoryLink> TestCategoryLinks => Set<TestCategoryLink>();
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<PromoBanner> PromoBanners => Set<PromoBanner>();
    public DbSet<HealthTip> HealthTips => Set<HealthTip>();

    // Patient + orders
    public DbSet<PatientAddress> PatientAddresses => Set<PatientAddress>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Phlebotomist> Phlebotomists => Set<Phlebotomist>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingStatusEvent> BookingStatusEvents => Set<BookingStatusEvent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportValue> ReportValues => Set<ReportValue>();
    public DbSet<Review> Reviews => Set<Review>();

    // Platform
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<MasterOption> MasterOptions => Set<MasterOption>();
    public DbSet<MasterState> MasterStates => Set<MasterState>();
    public DbSet<AppConfig> AppConfig => Set<AppConfig>();
    public DbSet<PincodeCache> PincodeCache => Set<PincodeCache>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public const string BookingNumberSeq = "booking_number_seq";
    public const string ReportNumberSeq = "report_number_seq";
    public const string KycReferenceSeq = "kyc_reference_seq";

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasSequence<long>(BookingNumberSeq).StartsAt(100001);
        b.HasSequence<long>(ReportNumberSeq).StartsAt(100001);
        b.HasSequence<long>(KycReferenceSeq).StartsAt(100001);

        // ---------- Identity ----------
        b.Entity<Patient>(e =>
        {
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.Phone).HasMaxLength(10);
            e.Property(x => x.CountryCode).HasMaxLength(4);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.Gender).HasMaxLength(10);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.Status).HasMaxLength(10);
        });

        b.Entity<Partner>(e =>
        {
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.KycStatus);
            e.Property(x => x.Phone).HasMaxLength(10);
            e.Property(x => x.CountryCode).HasMaxLength(4);
            e.Property(x => x.KycStatus).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(10);
            e.HasOne(x => x.Application).WithOne(x => x.Partner).HasForeignKey<KycApplication>(x => x.PartnerId);
        });

        b.Entity<AdminUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Role).HasMaxLength(30);
        });

        b.Entity<OtpRequest>(e =>
        {
            e.HasIndex(x => new { x.Phone, x.Audience, x.CreatedAt });
            e.HasIndex(x => new { x.IpAddress, x.CreatedAt });
            e.Property(x => x.Audience).HasMaxLength(10);
            e.Property(x => x.Phone).HasMaxLength(10);
            e.Property(x => x.OtpHash).HasMaxLength(100);
            e.Property(x => x.DeviceId).HasMaxLength(80);
            e.Property(x => x.IpAddress).HasMaxLength(45);
        });

        b.Entity<AuthSession>(e =>
        {
            e.HasIndex(x => x.RefreshTokenHash).IsUnique();
            e.HasIndex(x => new { x.UserType, x.UserId });
            e.Property(x => x.UserType).HasMaxLength(10);
            e.Property(x => x.RefreshTokenHash).HasMaxLength(100);
            e.Property(x => x.DeviceId).HasMaxLength(80);
            e.Property(x => x.DevicePlatform).HasMaxLength(10);
            e.Property(x => x.AppVersion).HasMaxLength(20);
        });

        b.Entity<DeviceToken>(e =>
        {
            e.HasIndex(x => x.PushToken).IsUnique();
            e.HasIndex(x => new { x.UserType, x.UserId });
            e.Property(x => x.PushToken).HasMaxLength(300);
            e.Property(x => x.Platform).HasMaxLength(10);
            e.Property(x => x.UserType).HasMaxLength(10);
        });

        // ---------- KYC ----------
        b.Entity<KycApplication>(e =>
        {
            e.HasIndex(x => x.PartnerId).IsUnique();
            e.HasIndex(x => x.ReferenceId).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.ReferenceId).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.RejectionReason).HasColumnType("jsonb");
            e.Property(x => x.DraftSections).HasColumnType("jsonb");
            e.Property(x => x.SubmitIdempotencyKey).HasMaxLength(80);
            e.HasOne(x => x.Lab).WithOne(x => x.Application).HasForeignKey<Lab>(x => x.ApplicationId);
            e.HasOne(x => x.Signatory).WithOne().HasForeignKey<KycSignatory>(x => x.ApplicationId);
            e.HasOne(x => x.Declaration).WithOne().HasForeignKey<KycDeclaration>(x => x.ApplicationId);
            e.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.ApplicationId);
            e.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ApplicationId);
            e.HasMany(x => x.Checks).WithOne().HasForeignKey(x => x.ApplicationId);
        });

        b.Entity<Lab>(e =>
        {
            e.HasIndex(x => x.ApplicationId).IsUnique();
            e.HasIndex(x => x.PartnerId);
            e.HasIndex(x => x.City);
            e.HasIndex(x => x.Pincode);
            e.HasIndex(x => new { x.Latitude, x.Longitude });
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.LegalName).HasMaxLength(150);
            e.Property(x => x.BrandName).HasMaxLength(100);
            e.Property(x => x.BusinessType).HasMaxLength(20);
            e.Property(x => x.AddressLine1).HasMaxLength(150);
            e.Property(x => x.AddressLine2).HasMaxLength(150);
            e.Property(x => x.Landmark).HasMaxLength(100);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.State).HasMaxLength(60);
            e.Property(x => x.Pincode).HasMaxLength(6).IsFixedLength();
            e.Property(x => x.Area).HasMaxLength(80);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.LabPhone).HasMaxLength(10);
            e.Property(x => x.LabEmail).HasMaxLength(120);
            e.Property(x => x.Website).HasMaxLength(150);
            e.Property(x => x.LogoColor).HasMaxLength(9);
            e.Property(x => x.RatingAvg).HasPrecision(2, 1);
            e.Ignore(x => x.DisplayName);
            e.HasMany(x => x.Services).WithOne().HasForeignKey(x => x.LabId);
            e.HasMany(x => x.Licences).WithOne().HasForeignKey(x => x.LabId);
            e.HasOne(x => x.TaxDetail).WithOne().HasForeignKey<LabTaxDetail>(x => x.LabId);
            e.HasMany(x => x.MedicalDirectors).WithOne().HasForeignKey(x => x.LabId);
            e.HasMany(x => x.BankAccounts).WithOne().HasForeignKey(x => x.LabId);
            e.HasOne(x => x.Operations).WithOne().HasForeignKey<LabOperations>(x => x.LabId);
            e.HasMany(x => x.WorkingDays).WithOne().HasForeignKey(x => x.LabId);
            e.HasMany(x => x.ServiceablePincodes).WithOne().HasForeignKey(x => x.LabId);
            e.HasMany(x => x.Tests).WithOne(x => x.Lab).HasForeignKey(x => x.LabId);
            e.HasOne<Partner>().WithMany().HasForeignKey(x => x.PartnerId);
        });

        b.Entity<LabService>(e =>
        {
            e.HasKey(x => new { x.LabId, x.ServiceCode });
            e.Property(x => x.ServiceCode).HasMaxLength(20);
        });

        b.Entity<LabLicence>(e =>
        {
            e.HasIndex(x => new { x.LabId, x.LicenceType }).IsUnique();
            e.Property(x => x.LicenceType).HasMaxLength(20);
            e.Property(x => x.Number).HasMaxLength(40);
            e.Property(x => x.VerificationStatus).HasMaxLength(15);
            e.HasOne<KycDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<LabTaxDetail>(e =>
        {
            e.HasKey(x => x.LabId);
            e.HasIndex(x => x.BusinessPan);
            e.Property(x => x.BusinessPan).HasMaxLength(10).IsFixedLength();
            e.Property(x => x.Gstin).HasMaxLength(15).IsFixedLength();
            e.Property(x => x.RegisteredName).HasMaxLength(150);
        });

        b.Entity<LabMedicalDirector>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Qualification).HasMaxLength(60);
            e.Property(x => x.CouncilName).HasMaxLength(80);
            e.Property(x => x.RegistrationNumber).HasMaxLength(40);
            e.Property(x => x.VerificationStatus).HasMaxLength(15);
            e.HasOne<KycDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<KycSignatory>(e =>
        {
            e.HasIndex(x => x.ApplicationId).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Designation).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.PanEnc).HasMaxLength(200);
            e.Property(x => x.PanMasked).HasMaxLength(20);
            e.Property(x => x.AadhaarRef).HasMaxLength(200);
            e.Property(x => x.AadhaarLast4).HasMaxLength(4).IsFixedLength();
            e.Property(x => x.AadhaarConsentIp).HasMaxLength(45);
            e.Property(x => x.NameMatchScore).HasPrecision(5, 2);
        });

        b.Entity<LabBankAccount>(e =>
        {
            e.Property(x => x.AccountHolder).HasMaxLength(100);
            e.Property(x => x.AccountNumberEnc).HasMaxLength(200);
            e.Property(x => x.AccountLast4).HasMaxLength(4).IsFixedLength();
            e.Property(x => x.Ifsc).HasMaxLength(11).IsFixedLength();
            e.Property(x => x.BankName).HasMaxLength(80);
            e.Property(x => x.BranchName).HasMaxLength(80);
            e.Property(x => x.AccountType).HasMaxLength(10);
            e.Property(x => x.PennyDropStatus).HasMaxLength(15);
            e.Property(x => x.PennyDropName).HasMaxLength(100);
            e.HasOne<KycDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<LabOperations>(e =>
        {
            e.HasKey(x => x.LabId);
            e.Property(x => x.ProcessingMode).HasMaxLength(12);
            e.Property(x => x.PhlebotomistCount).HasMaxLength(6);
        });

        b.Entity<LabWorkingDay>(e =>
        {
            e.HasKey(x => new { x.LabId, x.DayCode });
            e.Property(x => x.DayCode).HasMaxLength(3).IsFixedLength();
        });

        b.Entity<LabServiceablePincode>(e =>
        {
            e.HasKey(x => new { x.LabId, x.Pincode });
            e.HasIndex(x => x.Pincode);
            e.Property(x => x.Pincode).HasMaxLength(6).IsFixedLength();
        });

        b.Entity<KycDocument>(e =>
        {
            e.HasIndex(x => new { x.ApplicationId, x.DocType });
            e.Property(x => x.DocType).HasMaxLength(25);
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.MimeType).HasMaxLength(50);
            e.Property(x => x.ChecksumSha256).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.Status).HasMaxLength(10);
            e.Property(x => x.RejectReason).HasMaxLength(200);
            e.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.StoredFileId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<KycDeclaration>(e =>
        {
            e.HasKey(x => x.ApplicationId);
            e.Property(x => x.AgreementVersion).HasMaxLength(10);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.DeviceId).HasMaxLength(80);
        });

        b.Entity<KycStatusHistory>(e =>
        {
            e.HasIndex(x => x.ApplicationId);
            e.Property(x => x.FromStatus).HasMaxLength(20);
            e.Property(x => x.ToStatus).HasMaxLength(20);
            e.Property(x => x.ChangedBy).HasMaxLength(60);
            e.Property(x => x.Note).HasMaxLength(300);
        });

        b.Entity<KycVerificationCheck>(e =>
        {
            e.HasIndex(x => new { x.ApplicationId, x.CheckType }).IsUnique();
            e.Property(x => x.CheckType).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(15);
            e.Property(x => x.Provider).HasMaxLength(40);
            e.Property(x => x.RequestRef).HasMaxLength(80);
            e.Property(x => x.ResponseJson).HasColumnType("jsonb");
            e.Property(x => x.FailureReason).HasMaxLength(200);
        });

        // ---------- Catalog ----------
        b.Entity<TestCategory>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(60);
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Icon).HasMaxLength(40);
            e.Property(x => x.Color).HasMaxLength(9);
        });

        b.Entity<DiagnosticTest>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.CategoryId);
            e.Property(x => x.Slug).HasMaxLength(60);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.SampleType).HasMaxLength(60);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Parameters).WithOne().HasForeignKey(x => x.TestId);
            e.HasMany(x => x.CategoryLinks).WithOne().HasForeignKey(x => x.TestId);
        });

        b.Entity<TestCategoryLink>(e =>
        {
            e.HasKey(x => new { x.TestId, x.CategoryId });
            e.HasIndex(x => x.CategoryId);
            e.HasOne<TestCategory>().WithMany().HasForeignKey(x => x.CategoryId);
        });

        b.Entity<TestParameter>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.ReferenceRange).HasMaxLength(60);
            e.Property(x => x.RefLow).HasPrecision(12, 4);
            e.Property(x => x.RefHigh).HasPrecision(12, 4);
            e.Property(x => x.ValueType).HasMaxLength(10);
        });

        b.Entity<LabTest>(e =>
        {
            e.HasKey(x => new { x.LabId, x.TestId });
            e.HasIndex(x => x.TestId);
            e.Property(x => x.Mrp).HasPrecision(10, 2);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.HasOne(x => x.Test).WithMany().HasForeignKey(x => x.TestId);
        });

        b.Entity<TimeSlot>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(40);
            e.Property(x => x.Period).HasMaxLength(10);
        });

        b.Entity<PromoBanner>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(80);
            e.Property(x => x.Subtitle).HasMaxLength(150);
            e.Property(x => x.Icon).HasMaxLength(40);
            e.Property(x => x.GradientFrom).HasMaxLength(9);
            e.Property(x => x.GradientTo).HasMaxLength(9);
            e.Property(x => x.TargetTestSlug).HasMaxLength(60);
        });

        b.Entity<HealthTip>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(500);
            e.Property(x => x.Icon).HasMaxLength(40);
        });

        // ---------- Patient + orders ----------
        b.Entity<PatientAddress>(e =>
        {
            e.HasIndex(x => x.PatientId);
            e.Property(x => x.Label).HasMaxLength(30);
            e.Property(x => x.Line1).HasMaxLength(150);
            e.Property(x => x.Line2).HasMaxLength(150);
            e.Property(x => x.Landmark).HasMaxLength(100);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.State).HasMaxLength(60);
            e.Property(x => x.Pincode).HasMaxLength(6).IsFixedLength();
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId);
        });

        b.Entity<FamilyMember>(e =>
        {
            e.HasIndex(x => x.PatientId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Relation).HasMaxLength(30);
            e.Property(x => x.Gender).HasMaxLength(10);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId);
        });

        b.Entity<Phlebotomist>(e =>
        {
            e.HasIndex(x => x.LabId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(10);
            e.HasOne<Lab>().WithMany().HasForeignKey(x => x.LabId);
        });

        b.Entity<Booking>(e =>
        {
            e.HasIndex(x => x.BookingNumber).IsUnique();
            e.HasIndex(x => new { x.PatientId, x.CreatedAt });
            e.HasIndex(x => new { x.LabId, x.ScheduledDate, x.SlotId });
            e.HasIndex(x => new { x.PatientId, x.IdempotencyKey }).IsUnique().HasFilter("idempotency_key IS NOT NULL");
            e.HasIndex(x => x.Status);
            e.Property(x => x.BookingNumber).HasMaxLength(20);
            e.Property(x => x.PatientName).HasMaxLength(100);
            e.Property(x => x.PatientGender).HasMaxLength(10);
            e.Property(x => x.LabName).HasMaxLength(150);
            e.Property(x => x.TestName).HasMaxLength(150);
            e.Property(x => x.Mode).HasMaxLength(10);
            e.Property(x => x.AddressLabel).HasMaxLength(30);
            e.Property(x => x.AddressText).HasMaxLength(400);
            e.Property(x => x.AddressPincode).HasMaxLength(6).IsFixedLength();
            e.Property(x => x.SlotLabel).HasMaxLength(40);
            e.Property(x => x.Mrp).HasPrecision(10, 2);
            e.Property(x => x.Discount).HasPrecision(10, 2);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.PaymentMethod).HasMaxLength(10);
            e.Property(x => x.PaymentStatus).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.CollectionOtp).HasMaxLength(6);
            e.Property(x => x.CancelReason).HasMaxLength(300);
            e.Property(x => x.CancelledBy).HasMaxLength(10);
            e.Property(x => x.IdempotencyKey).HasMaxLength(80);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lab).WithMany().HasForeignKey(x => x.LabId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Test).WithMany().HasForeignKey(x => x.TestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Phlebotomist).WithMany().HasForeignKey(x => x.PhlebotomistId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<FamilyMember>().WithMany().HasForeignKey(x => x.FamilyMemberId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.BookingId);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Report).WithOne().HasForeignKey<Report>(x => x.BookingId);
            e.HasOne(x => x.Review).WithOne().HasForeignKey<Review>(x => x.BookingId);
        });

        b.Entity<BookingStatusEvent>(e =>
        {
            e.HasIndex(x => x.BookingId);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(300);
            e.Property(x => x.ActorType).HasMaxLength(10);
        });

        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.BookingId);
            e.HasIndex(x => x.GatewayOrderId);
            e.Property(x => x.Method).HasMaxLength(10);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Status).HasMaxLength(10);
            e.Property(x => x.Gateway).HasMaxLength(20);
            e.Property(x => x.GatewayOrderId).HasMaxLength(80);
            e.Property(x => x.GatewayPaymentId).HasMaxLength(80);
            e.Property(x => x.FailureReason).HasMaxLength(200);
        });

        b.Entity<Report>(e =>
        {
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasIndex(x => x.ReportNumber).IsUnique();
            e.Property(x => x.ReportNumber).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(10);
            e.Property(x => x.PathologistName).HasMaxLength(100);
            e.Property(x => x.PathologistRegNo).HasMaxLength(40);
            e.Property(x => x.Remarks).HasMaxLength(1000);
            e.HasMany(x => x.Values).WithOne().HasForeignKey(x => x.ReportId);
            e.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.AttachmentFileId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ReportValue>(e =>
        {
            e.HasIndex(x => x.ReportId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Result).HasMaxLength(60);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.ReferenceRange).HasMaxLength(60);
            e.Property(x => x.Flag).HasMaxLength(10);
        });

        b.Entity<Review>(e =>
        {
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasIndex(x => new { x.LabId, x.CreatedAt });
            e.Property(x => x.Comment).HasMaxLength(500);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Lab>().WithMany().HasForeignKey(x => x.LabId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Platform ----------
        b.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserType, x.UserId, x.CreatedAt });
            e.Property(x => x.UserType).HasMaxLength(10);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(500);
        });

        b.Entity<StoredFile>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.ContentType).HasMaxLength(80);
            e.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.OwnerType).HasMaxLength(20);
        });

        b.Entity<MasterOption>(e =>
        {
            e.HasIndex(x => new { x.GroupCode, x.Code }).IsUnique();
            e.Property(x => x.GroupCode).HasMaxLength(30);
            e.Property(x => x.Code).HasMaxLength(80);
            e.Property(x => x.Label).HasMaxLength(120);
            e.Property(x => x.Hint).HasMaxLength(150);
        });

        b.Entity<MasterState>(e =>
        {
            e.HasKey(x => x.Code);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Code).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Name).HasMaxLength(60);
            e.Property(x => x.GstStateCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Type).HasMaxLength(5);
        });

        b.Entity<AppConfig>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(50);
            e.Property(x => x.Value).HasMaxLength(1000);
        });

        b.Entity<PincodeCache>(e =>
        {
            e.HasKey(x => x.Pincode);
            e.Property(x => x.Pincode).HasMaxLength(6).IsFixedLength();
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.State).HasMaxLength(60);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.ActorType).HasMaxLength(10);
            e.Property(x => x.ActorId).HasMaxLength(40);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.EntityType).HasMaxLength(30);
            e.Property(x => x.EntityId).HasMaxLength(40);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
        });
    }

    public Task<long> NextSequenceValue(string sequence, CancellationToken ct = default)
    {
        var sql = sequence switch
        {
            BookingNumberSeq => "SELECT nextval('booking_number_seq') AS \"Value\"",
            ReportNumberSeq => "SELECT nextval('report_number_seq') AS \"Value\"",
            KycReferenceSeq => "SELECT nextval('kyc_reference_seq') AS \"Value\"",
            _ => throw new ArgumentOutOfRangeException(nameof(sequence)),
        };
        return Database.SqlQueryRaw<long>(sql).SingleAsync(ct);
    }
}
