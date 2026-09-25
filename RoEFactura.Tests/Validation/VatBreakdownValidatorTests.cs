using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Validation;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class VatBreakdownValidatorTests
{
    private readonly VatBreakdownValidator _sut = new();

    private ValidationResult Validate(TaxSubtotalType subtotal) => _sut.Validate(subtotal);

    private static TaxSubtotalType Subtotal(string categoryId, string? reasonCode = null, string? reasonText = null)
    {
        return new TaxSubtotalType
        {
            TaxableAmount = new AmountType { Value = 100m, currencyID = "RON" },
            TaxAmount = new AmountType { Value = 0m, currencyID = "RON" },
            TaxCategory = new TaxCategoryType
            {
                ID = new IdentifierType { Value = categoryId },
                Percent = new PercentType { Value = 0m },
                TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } },
                TaxExemptionReasonCode = reasonCode == null ? null : new CodeType { Value = reasonCode },
                TaxExemptionReason = reasonText == null
                    ? null
                    : new List<TextType> { new TextType { Value = reasonText } }
            }
        };
    }

    [Theory]
    [InlineData("E", "BR-E-10")]
    [InlineData("AE", "BR-AE-10")]
    [InlineData("K", "BR-IC-10")]
    [InlineData("G", "BR-G-10")]
    [InlineData("O", "BR-O-10")]
    public void ExemptCategoryWithoutReason_Fails(string categoryId, string expectedErrorCode)
    {
        var subtotal = Subtotal(categoryId);
        Validate(subtotal).Errors.Should().Contain(e => e.ErrorCode == expectedErrorCode);
    }

    [Theory]
    [InlineData("E")]
    [InlineData("AE")]
    [InlineData("K")]
    [InlineData("G")]
    [InlineData("O")]
    public void ExemptCategoryWithReasonText_Passes(string categoryId)
    {
        var subtotal = Subtotal(categoryId, reasonText: "Scutit conform art. 292");
        Validate(subtotal).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("E")]
    [InlineData("AE")]
    [InlineData("K")]
    [InlineData("G")]
    [InlineData("O")]
    public void ExemptCategoryWithReasonCode_Passes(string categoryId)
    {
        var subtotal = Subtotal(categoryId, reasonCode: "VATEX-EU-132");
        Validate(subtotal).IsValid.Should().BeTrue();
    }

    [Fact]
    public void StandardCategory_NoReasonRequired_Passes()
    {
        var subtotal = Subtotal("S");
        Validate(subtotal).IsValid.Should().BeTrue();
    }
}
