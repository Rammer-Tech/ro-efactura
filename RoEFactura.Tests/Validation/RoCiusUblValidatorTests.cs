using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using RoEFactura.Validation.Constants;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class RoCiusUblValidatorTests
{
    private readonly RoCiusUblValidator _sut = new();

    private ValidationResult Validate(UblSharp.InvoiceType invoice)
        => _sut.Validate(invoice);

    private static void ShouldContainErrorCode(ValidationResult result, string errorCode)
    {
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == errorCode,
            $"Expected error code {errorCode} but got: {string.Join(", ", result.Errors.Select(e => e.ErrorCode))}");
    }

    // ── BR-RO-CIUS ──────────────────────────────────────────────────────────

    [Fact]
    public void BrRoCius_ValidCustomizationId_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-CIUS");
    }

    [Fact]
    public void BrRoCius_WrongCustomizationId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithCustomizationId("urn:wrong").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-CIUS");
    }

    [Fact]
    public void BrRoCius_NullCustomizationId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCustomizationId().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-CIUS");
    }

    // ── BR-RO-010 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("INV-001")]
    [InlineData("1")]
    [InlineData("A1B")]
    [InlineData("2024/001")]
    public void BrRo010_InvoiceNumberContainsDigit_Passes(string invoiceNumber)
    {
        var invoice = InvoiceBuilder.Valid().WithId(invoiceNumber).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-010");
    }

    [Theory]
    [InlineData("INV-ABC")]
    [InlineData("NONNUMERIC")]
    [InlineData("ABC-DEF-GHI")]
    public void BrRo010_InvoiceNumberHasNoDigit_Fails(string invoiceNumber)
    {
        var invoice = InvoiceBuilder.Valid().WithId(invoiceNumber).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-010");
    }

    [Fact]
    public void BrRo010_NullInvoiceId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutId().Build();
        // BR-1 and BR-RO-010 both fire
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-010");
    }

    // ── BR-RO-020 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("380")]
    [InlineData("381")]
    [InlineData("384")]
    [InlineData("389")]
    [InlineData("751")]
    public void BrRo020_AllowedTypeCode_Passes(string typeCode)
    {
        var invoice = InvoiceBuilder.Valid().WithTypeCode(typeCode).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-020");
    }

    [Theory]
    [InlineData("999")]
    [InlineData("382")]
    [InlineData("380X")]
    [InlineData("")]
    [InlineData("INVOICE")]
    public void BrRo020_DisallowedTypeCode_Fails(string typeCode)
    {
        var invoice = InvoiceBuilder.Valid().WithTypeCode(typeCode).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-020");
    }

    // ── BR-RO-030 ───────────────────────────────────────────────────────────

    [Fact]
    public void BrRo030_RonCurrency_RuleDoesNotApply()
    {
        var invoice = InvoiceBuilder.Valid().WithCurrency("RON").Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-030");
    }

    [Fact]
    public void BrRo030_EurCurrencyWithRonVat_Passes()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithCurrency("EUR")
            .WithVatCurrency("RON")
            .Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-030");
    }

    [Fact]
    public void BrRo030_EurCurrencyWithEurVat_Fails()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithCurrency("EUR")
            .WithVatCurrency("EUR")
            .Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-030");
    }

    [Fact]
    public void BrRo030_EurCurrencyWithNoVatCurrency_Fails()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithCurrency("EUR")
            .Build();
        // No TaxCurrencyCode set → HasValidVatCurrency returns false
        ShouldContainErrorCode(Validate(invoice), "BR-RO-030");
    }

    // ── BR-1 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br1_NullId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutId().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-1");
    }

    [Fact]
    public void Br1_EmptyId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithId("").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-1");
    }

    // ── BR-2 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br2_NullIssueDate_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutIssueDate().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-2");
    }

    // ── BR-3 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br3_NullTypeCode_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutTypeCode().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-3");
    }

    // ── BR-5 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br5_NullCurrency_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCurrency().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-5");
    }

    // ── BR-16 ───────────────────────────────────────────────────────────────

    [Fact]
    public void Br16_NoLines_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutLines().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-16");
    }

    [Fact]
    public void Br16_OneOrMoreLines_Passes()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(1).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-16");
    }

    // ── BR-RO-A999 ──────────────────────────────────────────────────────────

    [Fact]
    public void BrRoA999_ExactlyNineNineNineLines_Passes()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(999).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-A999");
    }

    [Fact]
    public void BrRoA999_OneThousandLines_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(1000).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-A999");
    }

    // ── BR-RO-Z2 ────────────────────────────────────────────────────────────

    [Fact]
    public void BrRoZ2_TwoDecimalPlaces_Passes()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100.00m, 119.00m, 119.00m).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-Z2");
    }

    [Fact]
    public void BrRoZ2_ThreeDecimalPlacesOnTaxExclusive_Fails()
    {
        // 100.001m has 3 decimal places
        var invoice = InvoiceBuilder.Valid().WithTotals(100.001m, 119.001m, 119.001m).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-Z2");
    }

    // ── Full valid invoice passes all rules ─────────────────────────────────

    [Fact]
    public void FullValidInvoice_PassesAllRules()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).IsValid.Should().BeTrue();
    }
}
