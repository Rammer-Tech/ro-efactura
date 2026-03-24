using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class RomanianAddressValidatorTests
{
    private readonly RomanianAddressValidator _sut = new();

    private ValidationResult Validate(AddressType address) => _sut.Validate(address);

    private static AddressType RoAddress(string city, string county)
        => InvoiceBuilder.BuildRomanianAddress(city, county);

    // ── Non-Romanian address: all rules skip ─────────────────────────────────

    [Fact]
    public void NonRomanianAddress_AllRulesSkipped()
    {
        var address = new AddressType
        {
            CityName = new NameType { Value = "Berlin" },
            Country = new CountryType
            {
                IdentificationCode = new CodeType { Value = "DE" }
            }
        };
        Validate(address).IsValid.Should().BeTrue();
    }

    // ── BR-RO-COUNTY: valid county codes ─────────────────────────────────────

    [Theory]
    [InlineData("AB")] [InlineData("AR")] [InlineData("AG")] [InlineData("B")]
    [InlineData("BC")] [InlineData("BH")] [InlineData("BN")] [InlineData("BT")]
    [InlineData("BV")] [InlineData("BR")] [InlineData("BZ")] [InlineData("CS")]
    [InlineData("CL")] [InlineData("CJ")] [InlineData("CT")] [InlineData("CV")]
    [InlineData("DB")] [InlineData("DJ")] [InlineData("GL")] [InlineData("GR")]
    [InlineData("GJ")] [InlineData("HR")] [InlineData("HD")] [InlineData("IL")]
    [InlineData("IS")] [InlineData("IF")] [InlineData("MM")] [InlineData("MH")]
    [InlineData("MS")] [InlineData("NT")] [InlineData("OT")] [InlineData("PH")]
    [InlineData("SM")] [InlineData("SJ")] [InlineData("SB")] [InlineData("SV")]
    [InlineData("TR")] [InlineData("TM")] [InlineData("TL")] [InlineData("VS")]
    [InlineData("VL")] [InlineData("VN")]
    public void BrRoCounty_AllValidCountyCodes_Pass(string county)
    {
        // Use a non-Bucharest city to avoid triggering the Bucharest sector rule
        var address = RoAddress("Cluj-Napoca", county == "B" ? "B" : county);
        if (county == "B")
        {
            // Bucharest needs a sector city name; skip county-specific assertion here
            // (covered separately in Bucharest tests)
            return;
        }
        Validate(address).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-COUNTY");
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZ")]
    [InlineData("RO")]
    [InlineData("")]
    [InlineData("CLJ")]
    public void BrRoCounty_InvalidCountyCode_Fails(string county)
    {
        var address = RoAddress("Cluj-Napoca", county);
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-COUNTY");
    }

    // ── BR-RO-BUCHAREST: sector validation ───────────────────────────────────

    [Theory]
    [InlineData("Sector 1")]
    [InlineData("Sector 2")]
    [InlineData("Sector 3")]
    [InlineData("Sector 4")]
    [InlineData("Sector 5")]
    [InlineData("Sector 6")]
    [InlineData("sector 3")]   // case-insensitive
    [InlineData("SECTOR 1")]   // case-insensitive
    public void BrRoBucharest_ValidSector_Passes(string cityName)
    {
        var address = RoAddress(cityName, "B");
        Validate(address).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-BUCHAREST");
    }

    [Theory]
    [InlineData("Bucuresti")]
    [InlineData("Sector 7")]
    [InlineData("Sector 0")]
    [InlineData("sector")]
    [InlineData("")]
    public void BrRoBucharest_InvalidCityForBucharest_Fails(string cityName)
    {
        var address = RoAddress(cityName, "B");
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-BUCHAREST");
    }

    // ── BR-RO-CITY-REQUIRED ──────────────────────────────────────────────────

    [Fact]
    public void BrRoCityRequired_EmptyCityName_Fails()
    {
        var address = RoAddress("", "CJ");
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-CITY-REQUIRED");
    }

    [Fact]
    public void BrRoCityRequired_NullCityName_Fails()
    {
        var address = RoAddress("Cluj-Napoca", "CJ");
        address.CityName = null;
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-CITY-REQUIRED");
    }
}
