namespace DiagnX.Api.Features.Catalog;

public sealed record CategoryDto(Guid Id, string Slug, string Name, string Icon, string Color);

public sealed record TestSummaryDto(
    Guid Id, string Slug, string Name, CategoryDto Category, bool FastingRequired, string SampleType, int Parameters,
    string Description, bool IsPackage, IReadOnlyList<string> IncludedTests, decimal? FromPrice, decimal? Mrp, int LabCount);

public sealed record ParameterDto(Guid Id, string Name, string Unit, string ReferenceRange);

public sealed record TestDetailDto(TestSummaryDto Test, IReadOnlyList<ParameterDto> ReportParameters);

/// <summary>The patient app's Lab shape (src/types Lab).</summary>
public sealed record LabCardDto(
    Guid Id, string Name, string Initials, string LogoColor, IReadOnlyList<string> Accreditation, decimal Rating, int ReviewCount,
    double? DistanceKm, string Address, int TurnaroundHours, bool HomeCollection, bool WalkIn, bool Verified, int Since);

public sealed record LabOfferDto(LabCardDto Lab, Guid TestId, decimal Price, decimal Mrp, int DiscountPercent, bool HomeCollectionAvailable);

public sealed record LabTestPriceDto(TestSummaryDto Test, decimal Price, decimal Mrp);

public sealed record LabReviewDto(Guid Id, string PatientName, string PatientInitials, int Rating, string? Comment, DateTime CreatedAt, bool Verified);

public sealed record LabDetailDto(
    LabCardDto Lab, string FullAddress, decimal? Latitude, decimal? Longitude, string? OpenTime, string? CloseTime,
    IReadOnlyList<string> WorkingDays, IReadOnlyList<string> ServiceablePincodes, IReadOnlyList<LabTestPriceDto> Tests,
    IReadOnlyList<LabReviewDto> RecentReviews);

public sealed record SlotDto(Guid Id, string Label, string Period, string StartTime, bool Available, int Remaining);

public sealed record DaySlotsDto(string Date, IReadOnlyList<SlotDto> Slots);

public sealed record PromoBannerDto(Guid Id, string Title, string Subtitle, string Icon, IReadOnlyList<string> Gradient, string? TargetTestSlug);

public sealed record HealthTipDto(Guid Id, string Title, string Body, string Icon, int ReadMins);

public sealed record TrustPointDto(string Id, string Icon, string Title, string Subtitle);
