using FluentAssertions;
using RoEFactura.Generation;
using RoEFactura.Validation.Constants;
using Xunit;

namespace RoEFactura.Tests.Generation;

/// <summary>
/// County (ISO 3166-2:RO, BR-RO-110/111) and Bucharest sector (SECTOR-RO, BR-RO-100/101) conversion.
/// Diacritics are written as \u escapes: ș U+0219, ş U+015F, ț U+021B, ţ U+0163, ă U+0103, â U+00E2.
/// </summary>
public class RomanianAddressConverterTests
{
    [Theory]
    [InlineData("Brașov", "RO-BV")]
    [InlineData("Braşov", "RO-BV")]
    [InlineData("BRASOV", "RO-BV")]
    [InlineData("județul Brașov", "RO-BV")]
    [InlineData("Cluj", "RO-CJ")]
    public void ToCountyCode_FullNameWithDiacritics_ReturnsIsoCode(string county, string expected)
    {
        RomanianAddressConverter.ToCountyCode(county).Should().Be(expected);
    }

    [Theory]
    [InlineData("cj", "RO-CJ")]
    [InlineData("RO-CJ", "RO-CJ")]
    public void ToCountyCode_AbbreviationOrIsoCode_ReturnsIsoCode(string county, string expected)
    {
        RomanianAddressConverter.ToCountyCode(county).Should().Be(expected);
    }

    [Theory]
    [InlineData("București")]
    [InlineData("Municipiul Bucuresti")]
    [InlineData("B")]
    public void ToCountyCode_Bucharest_ReturnsRoB(string county)
    {
        RomanianAddressConverter.ToCountyCode(county).Should().Be(RomanianConstants.BucharestCountyCode);
    }

    [Fact]
    public void ToCountyCode_AllFortyTwoCodes_RoundTrip()
    {
        RomanianConstants.ValidCountyCodes.Should().HaveCount(42);

        foreach (string code in RomanianConstants.ValidCountyCodes)
        {
            RomanianAddressConverter.ToCountyCode(code).Should().Be(code);
            RomanianAddressConverter.ToCountyCode(code.ToLowerInvariant()).Should().Be(code);
            RomanianAddressConverter.ToCountyCode(code["RO-".Length..]).Should().Be(code);
        }
    }

    [Fact]
    public void ToCountyCode_UnknownCounty_ThrowsArgumentException()
    {
        Action act = () => RomanianAddressConverter.ToCountyCode("Atlantida");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BR-RO-110*Atlantida*")
            .Which.ParamName.Should().Be("county");
    }

    [Theory]
    [InlineData("Sector 3")]
    [InlineData("sectorul 3")]
    [InlineData("S3")]
    [InlineData("3")]
    [InlineData("SECTOR3")]
    public void ToBucharestSector_VariousForms_ReturnsSectorCode(string city)
    {
        RomanianAddressConverter.ToBucharestSector(city).Should().Be("SECTOR3");
    }

    [Fact]
    public void ToBucharestSector_OutOfRange_ThrowsArgumentException()
    {
        Action act = () => RomanianAddressConverter.ToBucharestSector("Sector 7");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BR-RO-100*")
            .Which.ParamName.Should().Be("city");
    }

    [Fact]
    public void Convert_BucharestWithoutSector_ThrowsArgumentException()
    {
        Action act = () => RomanianAddressConverter.Convert("București", "București");

        act.Should().Throw<ArgumentException>()
            .WithMessage("[BR-RO-100] Bucharest address requires a sector (1-6)*")
            .Which.ParamName.Should().Be("city");
    }

    [Fact]
    public void Convert_NonBucharestCounty_KeepsCityName()
    {
        RomanianAddress address = RomanianAddressConverter.Convert("jud. Cluj", "  Cluj-Napoca ");

        address.Should().Be(new RomanianAddress("RO-CJ", "Cluj-Napoca"));
        RomanianAddressConverter.Convert("București", "București, Sector 6")
            .Should().Be(new RomanianAddress("RO-B", "SECTOR6"));
    }

    [Fact]
    public void TryToCountyCode_Unknown_ReturnsFalse()
    {
        RomanianAddressConverter.TryToCountyCode("Atlantida", out string code).Should().BeFalse();
        code.Should().BeEmpty();
        RomanianAddressConverter.TryToCountyCode(null, out _).Should().BeFalse();
        RomanianAddressConverter.TryToCountyCode("RO-ZZ", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("Caraș-Severin", "RO-CS")]
    [InlineData("Caraş-Severin", "RO-CS")]
    [InlineData("Caras Severin", "RO-CS")]
    [InlineData("CARAS-SEVERIN", "RO-CS")]
    [InlineData("Bistrița-Năsăud", "RO-BN")]
    [InlineData("Bistriţa-Năsăud", "RO-BN")]
    [InlineData("bistrita nasaud", "RO-BN")]
    [InlineData("Satu Mare", "RO-SM")]
    [InlineData("Satu-Mare", "RO-SM")]
    [InlineData("Vâlcea", "RO-VL")]
    public void ToCountyCode_DiacriticCaseAndHyphenVariants_ReturnsIsoCode(string county, string expected)
    {
        RomanianAddressConverter.ToCountyCode(county).Should().Be(expected);
    }
}
