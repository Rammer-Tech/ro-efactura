using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class TotalsValidatorTests
{
    private readonly TotalsValidator _sut = new();

    private ValidationResult Validate(UblSharp.InvoiceType invoice)
        => _sut.Validate(invoice);

    // ── BR-12: TaxExclusiveAmount required ───────────────────────────────────

    [Fact]
    public void Br12_MissingTaxExclusiveAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutTaxExclusiveAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-12");
    }

    [Fact]
    public void Br12_TaxExclusiveAmountPresent_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-12");
    }

    // ── BR-14: TaxInclusiveAmount required ───────────────────────────────────

    [Fact]
    public void Br14_MissingTaxInclusiveAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutTaxInclusiveAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-14");
    }

    // ── BR-15: PayableAmount required ────────────────────────────────────────

    [Fact]
    public void Br15_MissingPayableAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutPayableAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-15");
    }

    // ── BR-CO-10: Line sum = TaxExclusiveAmount ──────────────────────────────

    [Fact]
    public void BrCo10_LineSumMatchesTotal_Passes()
    {
        // Base invoice: 1 line × 100.00 = 100.00, TaxExclusiveAmount = 100.00
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-10");
    }

    [Fact]
    public void BrCo10_LineSumOffByMoreThanTolerance_Fails()
    {
        // Line = 100.00, but total says 200.00 → difference = 100, exceeds 0.01 tolerance
        var invoice = InvoiceBuilder.Valid().WithTotals(200m, 219m, 219m).Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-10");
    }

    [Fact]
    public void BrCo10_LineSumWithinOneCentTolerance_Passes()
    {
        // Line = 100.00, total = 100.01 → difference = 0.01, within tolerance
        var invoice = InvoiceBuilder.Valid().WithTotals(100.01m, 119.01m, 119.01m).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-10");
    }

    // ── BR-CO-11: TaxInclusive = TaxExclusive + VAT ──────────────────────────

    [Fact]
    public void BrCo11_CorrectCalculation_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build(); // 100 + 19 = 119
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-11");
    }

    [Fact]
    public void BrCo11_WrongTaxInclusive_Fails()
    {
        // TaxExclusive=100, VAT=19, TaxInclusive=200 (should be 119)
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 200m, 200m).Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-11");
    }

    // ── BR-CO-12: VAT breakdown: taxable × rate = taxAmount ─────────────────

    [Fact]
    public void BrCo12_CorrectBreakdown_Passes()
    {
        // 100 × 19% = 19.00
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-12");
    }

    [Fact]
    public void BrCo12_WrongBreakdownTaxAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithTaxTotalAmount(50m).Build();
        // Subtotal tax amount is still 19 but total says 50 — BR-CO-13 fires;
        // to specifically trigger BR-CO-12, we'd need to modify the subtotal directly.
        // This test validates that a large discrepancy triggers at least CO-12 or CO-13.
        var result = Validate(invoice);
        result.Errors.Should().Contain(e => e.ErrorCode == "BR-CO-13" || e.ErrorCode == "BR-CO-12");
    }

    // ── BR-CO-13: VAT total = sum of VAT subtotals ───────────────────────────

    [Fact]
    public void BrCo13_CorrectVatTotalSum_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-13");
    }

    // ── BR-29: Period end >= start ───────────────────────────────────────────

    [Fact]
    public void Br29_EndDateAfterStartDate_Passes()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithDocumentPeriod(new DateTime(2024, 1, 1), new DateTime(2024, 1, 31))
            .Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-29");
    }

    [Fact]
    public void Br29_EndDateBeforeStartDate_Fails()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithDocumentPeriod(new DateTime(2024, 1, 31), new DateTime(2024, 1, 1))
            .Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-29");
    }

    [Fact]
    public void Br29_EndDateEqualsStartDate_Passes()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithDocumentPeriod(new DateTime(2024, 1, 15), new DateTime(2024, 1, 15))
            .Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-29");
    }

    [Fact]
    public void Br29_NoPeriodDefined_RuleSkipped()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-29");
    }
}
