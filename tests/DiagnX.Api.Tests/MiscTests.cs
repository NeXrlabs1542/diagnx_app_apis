using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Features.Catalog;
using DiagnX.Api.Features.LabOps;
using DiagnX.Api.Services.Security;
using DiagnX.Api.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace DiagnX.Api.Tests;

public class MiscTests
{
    [Theory]
    [InlineData("11.8", 4.0, 10.0, "high")]
    [InlineData("3.2", 4.0, 10.0, "low")]
    [InlineData("5", 4.0, 10.0, "normal")]
    [InlineData("212", null, 199.9, "high")]
    [InlineData("46", 40.01, null, "normal")]
    [InlineData("Within range", 1.0, 2.0, "normal")]
    public void ReportFlags(string result, double? low, double? high, string expected) =>
        Assert.Equal(expected, LabOpsService.ComputeFlag(result, (decimal?)low, (decimal?)high));

    [Fact]
    public void RenderDatabaseUrlIsParsed()
    {
        var cs = new NpgsqlConnectionStringBuilder(ConnectionStrings.FromUrl("postgresql://diagnx:p%40ss@dpg-abc.oregon-postgres.render.com/diagnx_db"));
        Assert.Equal("dpg-abc.oregon-postgres.render.com", cs.Host);
        Assert.Equal(5432, cs.Port);
        Assert.Equal("diagnx", cs.Username);
        Assert.Equal("p@ss", cs.Password);
        Assert.Equal("diagnx_db", cs.Database);
        Assert.Equal(SslMode.Require, cs.SslMode);

        var internalCs = new NpgsqlConnectionStringBuilder(ConnectionStrings.FromUrl("postgresql://u:p@dpg-abc-a:5433/db"));
        Assert.Equal(5433, internalCs.Port);
        Assert.Equal(SslMode.Prefer, internalCs.SslMode);
    }

    [Fact]
    public void FileSnifferDetectsRealTypes()
    {
        Assert.Equal("image/jpeg", FileSniffer.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Equal("image/png", FileSniffer.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.Equal("application/pdf", FileSniffer.Detect("%PDF-1.7"u8.ToArray()));
        Assert.Equal("image/heic", FileSniffer.Detect("\0\0\0\u0018ftypheic"u8.ToArray()));
        Assert.Null(FileSniffer.Detect("MZ\x90\0"u8.ToArray())); // an .exe renamed to .jpg
    }

    [Fact]
    public void FieldEncryptionRoundTrips()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:MasterKey"] = "unit-test-master-key-0123456789abcdef",
        }).Build();
        var keys = new SecretKeys(config, new TestEnv());
        var enc = new FieldEncryptor(keys);
        var cipher = enc.Encrypt("234567890124");
        Assert.DoesNotContain("234567890124", cipher);
        Assert.NotEqual(cipher, enc.Encrypt("234567890124")); // random nonce
        Assert.Equal("234567890124", enc.Decrypt(cipher));
    }

    [Fact]
    public void BusinessDaysSkipWeekends()
    {
        // Friday 2026-10-02 10:00 IST + 2 business days = Tuesday 2026-10-06
        var friday = IstClock.ToUtc(new DateOnly(2026, 10, 2), new TimeOnly(10, 0));
        var result = IstClock.AddBusinessDays(friday, 2) + IstClock.Offset;
        Assert.Equal(new DateTime(2026, 10, 6, 10, 0, 0), result);
    }

    [Fact]
    public void WalkInTimelineSkipsEnroute()
    {
        var b = new Booking { Mode = CollectionModes.WalkIn, Status = BookingStatus.Collected };
        b.Events.Add(new BookingStatusEvent { Status = BookingStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddHours(-2) });
        b.Events.Add(new BookingStatusEvent { Status = BookingStatus.Collected, CreatedAt = DateTime.UtcNow });
        var t = BookingWorkflow.Timeline(b);
        Assert.DoesNotContain(t, s => s.Status == BookingStatus.Enroute);
        Assert.Equal(new[] { "DONE", "ACTIVE", "TODO", "TODO" }, t.Select(s => s.State));
        Assert.NotNull(t[0].At);
    }

    [Fact]
    public void DistanceAndInitials()
    {
        var lab = new Lab { Latitude = 17.4600m, Longitude = 78.3640m };
        var km = CatalogService.DistanceKm(new GeoPoint(17.4401, 78.3489), lab);
        Assert.InRange(km!.Value, 2.5, 2.9);
        Assert.Equal("VD", CatalogService.Initials("Vitalis Diagnostics"));
        Assert.Equal("Ravi K.", CatalogService.ShortName("Ravi Kumar"));
    }

    private sealed class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
