using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using RoEFactura.Validation.Constants;
using UblSharp.CommonAggregateComponents;
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

    // ── BR-RO-001 ───────────────────────────────────────────────────────────

    [Fact]
    public void BrRo001_CurrentCiusRoCustomizationId_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-001");
    }

    [Fact]
    public void BrRo001_WrongCustomizationId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithCustomizationId("urn:wrong").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-001");
    }

    [Fact]
    public void BrRo001_NullCustomizationId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCustomizationId().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-001");
    }

    [Fact]
    public void BrRo001_LegacyRoCius2021CustomizationId_Fails()
    {
        // The legacy identifier is accepted by IsRomanianInvoice() for detection, but it is not the
        // exact CIUS-RO 1.0.1 value BR-RO-001 requires.
        var invoice = InvoiceBuilder.Valid().WithCustomizationId(RomanianConstants.LegacyCustomizationIds[0]).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-001");
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

    // ── BR-RO-040 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("3")]
    [InlineData("35")]
    [InlineData("432")]
    public void BrRo040_AllowedVatPointDateCode_Passes(string code)
    {
        var invoice = InvoiceBuilder.Valid().WithVatPointDateCode(code).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-040");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("5")]
    [InlineData("29")]
    public void BrRo040_DisallowedVatPointDateCode_Fails(string code)
    {
        var invoice = InvoiceBuilder.Valid().WithVatPointDateCode(code).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-040");
    }

    [Fact]
    public void BrRo040_NoVatPointDateCode_RuleSkipped()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-040");
    }

    // ── BR-RO-100/101/110/111 wiring (Seller vs Buyer) ───────────────────────

    [Fact]
    public void SellerCountyWithoutRoPrefix_FailsBrRo110()
    {
        var invoice = InvoiceBuilder.Valid().WithSellerAddress("Cluj-Napoca", "CJ").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-110");
    }

    [Fact]
    public void BuyerCountyWithoutRoPrefix_FailsBrRo111()
    {
        var invoice = InvoiceBuilder.Valid().WithBuyerAddress("Iasi", "IS").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-111");
    }

    [Fact]
    public void SellerBucharestNonSectorCity_FailsBrRo100()
    {
        var invoice = InvoiceBuilder.Valid().WithSellerAddress("Bucuresti", "RO-B").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-100");
    }

    [Fact]
    public void BuyerBucharestNonSectorCity_FailsBrRo101()
    {
        var invoice = InvoiceBuilder.Valid().WithBuyerAddress("Bucuresti", "RO-B").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-101");
    }

    [Fact]
    public void ForeignBuyerWithoutCounty_PassesAddressRules()
    {
        var invoice = InvoiceBuilder.Valid().WithBuyerAddress("Berlin", null, "DE").Build();
        string[] addressCodes = ["BR-RO-110", "BR-RO-111", "BR-RO-100", "BR-RO-101"];

        Validate(invoice).Errors.Should().NotContain(e => addressCodes.Contains(e.ErrorCode));
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

    // ── BR-RO-A999 removed (absent from RO16931-rules.sch 1.0.9 -- eliminated in 1.0.8) ─────

    [Fact]
    public void LineCount_999Lines_DoesNotEmitRemovedBrRoA999()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(999).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-A999");
    }

    [Fact]
    public void LineCount_1000Lines_DoesNotEmitRemovedBrRoA999()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(1000).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-A999");
    }

    // ── BR-RO-A020 / BR-RO-L200 / BR-RO-L300 (invoice number / note lengths) ─

    [Fact]
    public void InvoiceNumber_OverMaxLength_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithId("1" + new string('A', 200)).Build(); // 201 chars, has a digit
        ShouldContainErrorCode(Validate(invoice), "BR-RO-L200");
    }

    [Fact]
    public void InvoiceNotes_MoreThanMaxCount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithNotes(21, 10).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-A020");
    }

    [Fact]
    public void InvoiceNote_OverMaxLength_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithNotes(1, 301).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-L300");
    }

    // ── VAT exemption reason (BR-E-10 etc.), wired via VatBreakdownValidator ──

    [Fact]
    public void ExemptSubtotalWithoutReason_FailsViaFullValidator()
    {
        var invoice = InvoiceBuilder.Valid().WithSubtotalVat("E", 0m).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-E-10");
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

    // ── Sparse / empty documents must never throw ────────────────────────────

    [Fact]
    public void Validate_EmptyOrSparseInvoice_DoesNotThrow()
    {
        Action act1 = () => _sut.Validate(new UblSharp.InvoiceType());
        act1.Should().NotThrow();

        var invoiceWithEmptyTaxTotal = InvoiceBuilder.Valid().Build();
        invoiceWithEmptyTaxTotal.TaxTotal = new List<TaxTotalType>();
        Action act2 = () => _sut.Validate(invoiceWithEmptyTaxTotal);
        act2.Should().NotThrow();

        var invoiceWithoutParties = InvoiceBuilder.Valid().Build();
        invoiceWithoutParties.AccountingSupplierParty = null;
        invoiceWithoutParties.AccountingCustomerParty = null;
        Action act3 = () => _sut.Validate(invoiceWithoutParties);
        act3.Should().NotThrow();
    }

    // ── Full valid invoice passes all rules ─────────────────────────────────

    [Fact]
    public void FullValidInvoice_PassesAllRules()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).IsValid.Should().BeTrue();
    }
}
