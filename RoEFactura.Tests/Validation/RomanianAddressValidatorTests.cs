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

    // ── BR-RO-110: valid ISO 3166-2:RO county codes ──────────────────────────

    [Theory]
    [InlineData("RO-AB")] [InlineData("RO-AR")] [InlineData("RO-AG")] [InlineData("RO-B")]
    [InlineData("RO-BC")] [InlineData("RO-BH")] [InlineData("RO-BN")] [InlineData("RO-BT")]
    [InlineData("RO-BV")] [InlineData("RO-BR")] [InlineData("RO-BZ")] [InlineData("RO-CS")]
    [InlineData("RO-CL")] [InlineData("RO-CJ")] [InlineData("RO-CT")] [InlineData("RO-CV")]
    [InlineData("RO-DB")] [InlineData("RO-DJ")] [InlineData("RO-GL")] [InlineData("RO-GR")]
    [InlineData("RO-GJ")] [InlineData("RO-HR")] [InlineData("RO-HD")] [InlineData("RO-IL")]
    [InlineData("RO-IS")] [InlineData("RO-IF")] [InlineData("RO-MM")] [InlineData("RO-MH")]
    [InlineData("RO-MS")] [InlineData("RO-NT")] [InlineData("RO-OT")] [InlineData("RO-PH")]
    [InlineData("RO-SM")] [InlineData("RO-SJ")] [InlineData("RO-SB")] [InlineData("RO-SV")]
    [InlineData("RO-TR")] [InlineData("RO-TM")] [InlineData("RO-TL")] [InlineData("RO-VS")]
    [InlineData("RO-VL")] [InlineData("RO-VN")]
    public void BrRo110_AllIsoCountyCodes_Pass(string county)
    {
        // RO-B needs a SECTORn city name to also satisfy BR-RO-100; any other county uses a plain city.
        string city = county == "RO-B" ? "SECTOR1" : "Cluj-Napoca";
        var address = RoAddress(city, county);
        Validate(address).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-110");
    }

    [Theory]
    [InlineData("CJ")]
    [InlineData("B")]
    [InlineData("XX")]
    [InlineData("RO-XX")]
    [InlineData("RO")]
    [InlineData("")]
    [InlineData("ro-cj")]
    [InlineData("RO-CLJ")]
    public void BrRo110_InvalidCountyCode_Fails(string county)
    {
        var address = RoAddress("Cluj-Napoca", county);
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-110");
    }

    // ── BR-RO-100: Bucharest sector validation ───────────────────────────────

    [Theory]
    [InlineData("SECTOR1")]
    [InlineData("SECTOR2")]
    [InlineData("SECTOR3")]
    [InlineData("SECTOR4")]
    [InlineData("SECTOR5")]
    [InlineData("SECTOR6")]
    public void BrRo100_ValidSectorCode_Passes(string cityName)
    {
        var address = RoAddress(cityName, "RO-B");
        Validate(address).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-100");
    }

    [Theory]
    [InlineData("Bucuresti")]
    [InlineData("Sector 3")]
    [InlineData("sector3")]
    [InlineData("SECTOR 1")]
    [InlineData("SECTOR7")]
    [InlineData("SECTOR0")]
    [InlineData("")]
    public void BrRo100_InvalidCityForBucharest_Fails(string cityName)
    {
        var address = RoAddress(cityName, "RO-B");
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-100");
    }

    // ── BR-RO-CITY-REQUIRED ──────────────────────────────────────────────────

    [Fact]
    public void BrRoCityRequired_EmptyCityName_Fails()
    {
        var address = RoAddress("", "RO-CJ");
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-CITY-REQUIRED");
    }

    [Fact]
    public void BrRoCityRequired_NullCityName_Fails()
    {
        var address = RoAddress("Cluj-Napoca", "RO-CJ");
        address.CityName = null;
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-CITY-REQUIRED");
    }

    // ── Role-dependent error codes (Seller vs Buyer) ─────────────────────────

    [Fact]
    public void BuyerRole_ReportsBrRo111AndBrRo101()
    {
        var buyerValidator = new RomanianAddressValidator(RomanianAddressRole.Buyer);

        var invalidCounty = RoAddress("Cluj-Napoca", "CJ");
        buyerValidator.Validate(invalidCounty).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-111");

        var invalidSector = RoAddress("Bucuresti", "RO-B");
        buyerValidator.Validate(invalidSector).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-101");
    }
}
