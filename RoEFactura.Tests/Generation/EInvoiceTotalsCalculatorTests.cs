using FluentAssertions;
using RoEFactura.Generation;
using RoEFactura.Tests.Generation.TestData;
using Xunit;

namespace RoEFactura.Tests.Generation;

/// <summary>
/// EN 16931 totals (BR-CO-10..17) computed by <see cref="EInvoiceTotalsCalculator"/>: 2-decimal rounding
/// away from zero, VAT per (category, rate) group rounded after summing the group.
/// </summary>
public class EInvoiceTotalsCalculatorTests
{
    private static EInvoiceLine Line(decimal quantity, decimal unitPrice, EInvoiceVatCategory category, decimal rate = 0m)
    {
        return new EInvoiceLine
        {
            Name = "Articol",
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatCategory = category,
            VatRate = rate
        };
    }

    private static EInvoiceDocument WithLines(params EInvoiceLine[] lines)
    {
        return EInvoiceTestCases.MixedRates() with { Lines = lines };
    }

    [Fact]
    public void Calculate_LineNet_RoundsAwayFromZero()
    {
        // 0.5 x 0.25 = 0.125: AwayFromZero gives 0.13, ToEven would give 0.12.
        EInvoiceTotals totals = EInvoiceTotalsCalculator.Calculate(
            WithLines(Line(0.5m, 0.25m, EInvoiceVatCategory.Standard, 21m)));

        totals.LineNetAmounts.Should().Equal(0.13m);
        totals.LineExtensionAmount.Should().Be(0.13m);
    }

    [Fact]
    public void Calculate_MixedRates_GroupsByCategoryAndRate()
    {
        // Three S21 lines of 0.03: per-line VAT would be 0.01 x 3 = 0.03, the group VAT is round(0.09 x 21%) = 0.02.
        EInvoiceTotals totals = EInvoiceTotalsCalculator.Calculate(WithLines(
            Line(1m, 0.03m, EInvoiceVatCategory.Standard, 21m),
            Line(1m, 100.00m, EInvoiceVatCategory.Exempt),
            Line(1m, 0.03m, EInvoiceVatCategory.Standard, 21m),
            Line(2m, 10.00m, EInvoiceVatCategory.Standard, 11m),
            Line(1m, 0.03m, EInvoiceVatCategory.Standard, 21.00m)));

        totals.VatBreakdown.Should().Equal(
            new EInvoiceVatBreakdown(EInvoiceVatCategory.Standard, 21m, 0.09m, 0.02m),
            new EInvoiceVatBreakdown(EInvoiceVatCategory.Standard, 11m, 20.00m, 2.20m),
            new EInvoiceVatBreakdown(EInvoiceVatCategory.Exempt, 0m, 100.00m, 0m));
        totals.TaxAmount.Should().Be(2.22m);
    }

    [Fact]
    public void Calculate_MixedRates_TotalsSatisfyBrCo10To16()
    {
        EInvoiceTotals totals = EInvoiceTotalsCalculator.Calculate(EInvoiceTestCases.MixedRates());

        totals.LineNetAmounts.Should().Equal(300.00m, 15.25m, 500.00m);
        totals.LineExtensionAmount.Should().Be(totals.LineNetAmounts.Sum()).And.Be(815.25m);          // BR-CO-10
        totals.TaxExclusiveAmount.Should().Be(totals.LineExtensionAmount);                             // BR-CO-13
        totals.TaxAmount.Should().Be(totals.VatBreakdown.Sum(g => g.TaxAmount)).And.Be(64.68m);       // BR-CO-14
        totals.TaxInclusiveAmount.Should().Be(totals.TaxExclusiveAmount + totals.TaxAmount).And.Be(879.93m); // BR-CO-15
        totals.PayableAmount.Should().Be(totals.TaxInclusiveAmount);                                   // BR-CO-16
        totals.VatBreakdown.Sum(g => g.TaxableAmount).Should().Be(totals.TaxExclusiveAmount);
    }

    [Fact]
    public void Calculate_Storno_ProducesNegativeTotals()
    {
        EInvoiceTotals totals = EInvoiceTotalsCalculator.Calculate(EInvoiceTestCases.Storno());

        totals.LineNetAmounts.Should().Equal(-150.00m, -99.98m);
        totals.LineExtensionAmount.Should().Be(-249.98m);
        totals.TaxAmount.Should().Be(-52.50m);
        totals.TaxInclusiveAmount.Should().Be(-302.48m);
        totals.PayableAmount.Should().Be(-302.48m);
        totals.VatBreakdown.Should().ContainSingle()
            .Which.Should().Be(new EInvoiceVatBreakdown(EInvoiceVatCategory.Standard, 21m, -249.98m, -52.50m));
    }

    [Fact]
    public void Calculate_ExemptAndNotSubject_TaxIsZero()
    {
        EInvoiceTotals exempt = EInvoiceTotalsCalculator.Calculate(WithLines(
            Line(3m, 33.33m, EInvoiceVatCategory.Exempt)));
        EInvoiceTotals notSubject = EInvoiceTotalsCalculator.Calculate(EInvoiceTestCases.NonVatPayerSeller());

        exempt.VatBreakdown.Should().ContainSingle()
            .Which.Should().Be(new EInvoiceVatBreakdown(EInvoiceVatCategory.Exempt, 0m, 99.99m, 0m));
        exempt.TaxAmount.Should().Be(0m);
        exempt.PayableAmount.Should().Be(99.99m);

        notSubject.VatBreakdown.Should().ContainSingle()
            .Which.Should().Be(new EInvoiceVatBreakdown(EInvoiceVatCategory.NotSubject, 0m, 750.00m, 0m));
        notSubject.TaxAmount.Should().Be(0m);
        notSubject.PayableAmount.Should().Be(750.00m);
    }
}
