using FluentAssertions;
using RoEFactura.Generation;
using Xunit;

namespace RoEFactura.Tests.Generation;

/// <summary>
/// The official code lists copied from the CIUS-RO 1.0.9 artifacts: BR-CO-09 VAT prefixes
/// (EN16931-UBL-model.sch:74), BR-CL-14 countries (EN16931-UBL-codes.sch:81) and BR-CL-23 units
/// (EN16931-UBL-codes.sch:156).
/// </summary>
public class OfficialCodeListsTests
{
    [Fact]
    public void OfficialCodeLists_Counts_MatchOfficial109()
    {
        OfficialCodeLists.VatIdPrefixes.Should().HaveCount(252);
        OfficialCodeLists.CountryCodes.Should().HaveCount(251);
        OfficialCodeLists.UnitCodes.Should().HaveCount(2162);
    }

    [Theory]
    [InlineData("RO")]
    [InlineData("DE")]
    [InlineData("EL")]
    [InlineData("XI")]
    public void VatIdPrefixes_OfficialPrefix_IsContained(string prefix)
    {
        OfficialCodeLists.VatIdPrefixes.Should().Contain(prefix);
    }

    [Theory]
    [InlineData("RO")]
    [InlineData("DE")]
    public void CountryCodes_OfficialCode_IsContained(string code)
    {
        OfficialCodeLists.CountryCodes.Should().Contain(code);
    }

    [Theory]
    [InlineData("ZZ")]
    public void CountryCodes_UnknownCode_IsNotContained(string code)
    {
        OfficialCodeLists.CountryCodes.Should().NotContain(code);
    }

    [Theory]
    [InlineData("H87")]
    [InlineData("C62")]
    [InlineData("KGM")]
    public void UnitCodes_OfficialCode_IsContained(string code)
    {
        OfficialCodeLists.UnitCodes.Should().Contain(code);
    }

    [Theory]
    [InlineData("buc")]
    public void UnitCodes_UnknownCode_IsNotContained(string code)
    {
        OfficialCodeLists.UnitCodes.Should().NotContain(code);
    }
}
