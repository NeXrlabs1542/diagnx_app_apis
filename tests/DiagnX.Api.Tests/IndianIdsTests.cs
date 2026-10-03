using DiagnX.Api.Common;

namespace DiagnX.Api.Tests;

/// <summary>Valid / invalid examples come straight from the spec's "Validation Rules" sheet.</summary>
public class IndianIdsTests
{
    [Theory]
    [InlineData("AAACS1234C", true)]
    [InlineData("ABCPS1234D", true)]
    [InlineData("AAACS1234", false)]
    [InlineData("aaacs1234c", false)]
    [InlineData("1AACS1234C", false)]
    public void Pan(string value, bool valid) => Assert.Equal(valid, IndianIds.IsPan(value));

    [Theory]
    [InlineData("36AAACS1234C1ZZ", true)]  // VR-03 valid example (PAN AAACS1234C)
    [InlineData("36AAACS1234C1ZW", false)] // bad checksum
    [InlineData("40AAACS1234C1Z8", false)] // state code 40 doesn't exist
    [InlineData("36AAACS1234C1Z", false)]
    public void Gstin(string value, bool valid) => Assert.Equal(valid, IndianIds.IsGstin(value));

    [Fact]
    public void GstinEmbedsPan()
    {
        Assert.True(IndianIds.GstinMatchesPan("36AAACS1234C1ZZ", "AAACS1234C"));
        Assert.False(IndianIds.GstinMatchesPan("36AAACS1234C1ZZ", "AAACS9999C"));
    }

    [Theory]
    [InlineData("234567890124", true)]  // VR-06 valid example
    [InlineData("234567890123", false)] // checksum fails
    [InlineData("134567890124", false)] // can't start with 1
    [InlineData("23456789012", false)]
    public void Aadhaar(string value, bool valid) => Assert.Equal(valid, IndianIds.IsAadhaar(value));

    [Theory]
    [InlineData("HDFC0001234", true)]
    [InlineData("HDFC1001234", false)]
    [InlineData("HDF00001234", false)]
    public void Ifsc(string value, bool valid) => Assert.Equal(valid, IndianIds.IsIfsc(value));

    [Theory]
    [InlineData("U85110TG2015PTC123456", true)]
    [InlineData("X85110TG2015PTC123456", false)]
    public void Cin(string value, bool valid) => Assert.Equal(valid, IndianIds.IsCin(value));

    [Theory]
    [InlineData("AAB-1234", true)]
    [InlineData("AAB1234", false)]
    public void Llpin(string value, bool valid) => Assert.Equal(valid, IndianIds.IsLlpin(value));

    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("1234567890", false)]
    [InlineData("98765", false)]
    public void Mobile(string value, bool valid) => Assert.Equal(valid, IndianIds.IsMobile(value));

    [Theory]
    [InlineData("500034", true)]
    [InlineData("050034", false)]
    [InlineData("50003", false)]
    public void Pincode(string value, bool valid) => Assert.Equal(valid, IndianIds.IsPincode(value));

    [Theory]
    [InlineData("reports@saisdiagnostics.in", true)]
    [InlineData("reports@lab", false)]
    public void Email(string value, bool valid) => Assert.Equal(valid, IndianIds.IsEmail(value));

    [Theory]
    [InlineData("+91 98765 43210", "9876543210")]
    [InlineData("919876543210", "9876543210")]
    [InlineData("09876543210", "9876543210")]
    [InlineData("9876543210", "9876543210")]
    public void NormalisePhone(string raw, string expected) => Assert.Equal(expected, IndianIds.NormalisePhone(raw));

    [Fact]
    public void Masks()
    {
        Assert.Equal("ABCPS••••D", Masking.Pan("ABCPS1234D"));
        Assert.Equal("•••• •••• 0124", Masking.Aadhaar("0124"));
        Assert.Equal("••••9012", Masking.Account("9012"));
        Assert.Equal("XXXXXX3210", Masking.Phone("9876543210"));
    }
}
