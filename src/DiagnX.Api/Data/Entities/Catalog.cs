namespace DiagnX.Api.Data.Entities;

public class TestCategory
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Ionicons name used by the app, e.g. "heart".</summary>
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
    /// <summary>Shown in the home screen's "Browse by category" strip.</summary>
    public bool ShowOnHome { get; set; }
    public short SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Extra categories a test is listed under (e.g. Vitamin D under "Women's Health"),
/// on top of its primary <see cref="DiagnosticTest.CategoryId"/>.
/// </summary>
public class TestCategoryLink
{
    public Guid TestId { get; set; }
    public Guid CategoryId { get; set; }
}

/// <summary>
/// A test or package in DiagnX's master catalog. Labs don't create tests — they enrol in
/// catalog tests and set their own price (<see cref="LabTest"/>). This is what makes every
/// lab's report map onto one standard template.
/// </summary>
public class DiagnosticTest
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid CategoryId { get; set; }
    public bool FastingRequired { get; set; }
    public string SampleType { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsPackage { get; set; }
    public List<string> IncludedTests { get; set; } = new();
    /// <summary>Parameter count advertised to patients (packages report a subset in the standard template).</summary>
    public short ParameterCount { get; set; }
    public bool IsPopular { get; set; }
    public short SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TestCategory Category { get; set; } = null!;
    public List<TestParameter> Parameters { get; set; } = new();
    public List<TestCategoryLink> CategoryLinks { get; set; } = new();
}

/// <summary>One row of the standard report template for a test.</summary>
public class TestParameter
{
    public Guid Id { get; set; }
    public Guid TestId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    /// <summary>Human text shown on the report, e.g. "13.0 – 17.0" or "&lt; 200".</summary>
    public string ReferenceRange { get; set; } = "";
    /// <summary>Numeric bounds used to compute the low / high flag. Null = no bound on that side.</summary>
    public decimal? RefLow { get; set; }
    public decimal? RefHigh { get; set; }
    /// <summary>numeric | text</summary>
    public string ValueType { get; set; } = "numeric";
    public short SortOrder { get; set; }
}

/// <summary>A lab's price for a catalog test.</summary>
public class LabTest
{
    public Guid LabId { get; set; }
    public Guid TestId { get; set; }
    public decimal Mrp { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }

    public Lab Lab { get; set; } = null!;
    public DiagnosticTest Test { get; set; } = null!;
}

public class TimeSlot
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public string Period { get; set; } = ""; // Morning | Afternoon | Evening
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool HomeCollection { get; set; } = true;
    public bool WalkIn { get; set; } = true;
    public short SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PromoBanner
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Icon { get; set; } = "";
    public string GradientFrom { get; set; } = "";
    public string GradientTo { get; set; } = "";
    /// <summary>Optional deep link, e.g. a test id to open.</summary>
    public string? TargetTestSlug { get; set; }
    public short SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}

public class HealthTip
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Icon { get; set; } = "";
    public short ReadMins { get; set; }
    public short SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
