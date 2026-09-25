using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class TotalsValidatorTests
{
    private readonly TotalsValidator _sut = new();

    private ValidationResult Validate(UblSharp.InvoiceType invoice)
        => _sut.Validate(invoice);

    // ── BR-12: LineExtensionAmount required ──────────────────────────────────

    [Fact]
    public void Br12_MissingLineExtensionAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutLineExtensionAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-12");
    }

    // ── BR-13: TaxExclusiveAmount required ───────────────────────────────────

    [Fact]
    public void Br13_MissingTaxExclusiveAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutTaxExclusiveAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-13");
    }

    [Fact]
    public void Br13_TaxExclusiveAmountPresent_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-13");
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

    // ── BR-CO-10: Line sum = LineExtensionAmount ─────────────────────────────

    [Fact]
    public void BrCo10_LineSumMatchesTotal_Passes()
    {
        // Base invoice: 1 line × 100.00 = 100.00, LineExtensionAmount = 100.00
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

    [Fact]
    public void BrCo10_WithDocumentLevelAllowance_ComparesLineSumToBt106_Passes()
    {
        // Lines still sum to 100 (BT-106); the allowance only affects BT-107/BT-109/BT-112, not BT-106.
        var invoice = InvoiceBuilder.Valid().WithDocumentAllowance(10m).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-10");
    }

    [Fact]
    public void BrCo10_WithDocumentLevelAllowance_Bt106EqualToBt109_Fails()
    {
        // Same as above, but BT-106 is corrupted to equal BT-109 (90) instead of the real line sum (100).
        var invoice = InvoiceBuilder.Valid().WithDocumentAllowance(10m).Build();
        invoice.LegalMonetaryTotal!.LineExtensionAmount = new AmountType { Value = 90m, currencyID = "RON" };

        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-10");
    }

    // ── BR-CO-11: AllowanceTotalAmount = Σ document-level allowances ────────

    [Fact]
    public void BrCo11_AllowanceTotalDiffersFromAllowances_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithDocumentAllowance(10m).Build();
        invoice.LegalMonetaryTotal!.AllowanceTotalAmount = new AmountType { Value = 5m, currencyID = "RON" };

        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-11");
    }

    // ── BR-CO-12: ChargeTotalAmount = Σ document-level charges ──────────────

    [Fact]
    public void BrCo12_ChargeTotalDiffersFromCharges_Fails()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.AllowanceCharge = new List<AllowanceChargeType>
        {
            new AllowanceChargeType
            {
                ChargeIndicator = new IndicatorType { Value = true },
                Amount = new AmountType { Value = 5m, currencyID = "RON" }
            }
        };
        invoice.LegalMonetaryTotal!.ChargeTotalAmount = new AmountType { Value = 10m, currencyID = "RON" };

        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-12");
    }

    // ── BR-CO-13: TaxExclusive = LineExtension - Allowances + Charges ───────

    [Fact]
    public void BrCo13_TaxExclusiveNotEqualLineSumMinusAllowancesPlusCharges_Fails()
    {
        // WithDocumentAllowance(10) leaves TaxExclusiveAmount = 90; corrupt it to 95.
        var invoice = InvoiceBuilder.Valid().WithDocumentAllowance(10m).Build();
        invoice.LegalMonetaryTotal!.TaxExclusiveAmount = new AmountType { Value = 95m, currencyID = "RON" };

        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-13");
    }

    // ── BR-CO-14: doc TaxTotal.TaxAmount = Σ subtotal TaxAmount ─────────────

    [Fact]
    public void BrCo14_CorrectVatTotalSum_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-14");
    }

    [Fact]
    public void BrCo14_TaxTotalDiffersFromSubtotals_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithTaxTotalAmount(50m).Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-14");
    }

    // ── BR-CO-15: TaxInclusive = TaxExclusive + VAT ──────────────────────────

    [Fact]
    public void BrCo15_CorrectCalculation_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build(); // 100 + 19 = 119
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-15");
    }

    [Fact]
    public void BrCo15_WrongTaxInclusive_Fails()
    {
        // TaxExclusive=100, VAT=19, TaxInclusive=200 (should be 119)
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 200m, 200m).Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-15");
    }

    // ── BR-CO-16: Payable = TaxInclusive - Prepaid + Rounding ────────────────

    [Fact]
    public void BrCo16_PayableEqualsTaxInclusiveMinusPrepaid_Passes()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithTotals(100m, 119m, 19m)
            .WithPrepaidAmount(100m)
            .Build();

        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-16");
    }

    [Fact]
    public void BrCo16_PayableIgnoresPrepaid_Fails()
    {
        // Payable should reflect the 100 prepayment (expected 19) but still shows the full 119.
        var invoice = InvoiceBuilder.Valid()
            .WithTotals(100m, 119m, 119m)
            .WithPrepaidAmount(100m)
            .Build();

        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-16");
    }

    // ── BR-CO-17: subtotal TaxAmount = TaxableAmount × Percent / 100 ─────────

    [Fact]
    public void BrCo17_CorrectBreakdown_Passes()
    {
        // 100 × 19% = 19.00
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-17");
    }

    [Fact]
    public void BrCo17_WrongSubtotalTaxAmount_Fails()
    {
        // 100 × 19% = 19.00; 25.00 is a 6.00 delta, outside the official ±1 currency-unit BR-CO-17 window.
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.TaxTotal![0].TaxSubtotal![0].TaxAmount = new AmountType { Value = 25m, currencyID = "RON" };

        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-17");
    }

    // ── Multi-currency: BR-CO-14..17 use the document-currency TaxTotal ─────

    [Fact]
    public void MultiCurrency_UsesDocumentCurrencyTaxTotal_Passes()
    {
        // Accounting-currency (RON) figure is deliberately inconsistent (999.99) to prove BR-CO-14..17
        // evaluate only the EUR (document-currency) TaxTotal.
        var invoice = InvoiceBuilder.Valid()
            .WithCurrency("EUR")
            .WithVatCurrency("RON")
            .WithTotals(100m, 119m, 119m)
            .WithDocumentCurrencyTaxTotals("EUR", 19.00m, 999.99m)
            .Build();

        Validate(invoice).IsValid.Should().BeTrue();
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
