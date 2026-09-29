namespace RoEFactura.Generation;

/// <summary>
/// Computes the line net amounts, VAT breakdown and document totals of an <see cref="EInvoiceDocument"/>
/// with the EN 16931 formulas (the same ones micro-taxe uses for its issued invoices). Every amount is
/// rounded to 2 decimals with <see cref="MidpointRounding.AwayFromZero"/>; negative values (storno) are
/// allowed. The calculator does not validate business rules — <see cref="EInvoiceXmlGenerator"/> does.
/// </summary>
public static class EInvoiceTotalsCalculator
{
    /// <summary>Computes the totals of <paramref name="document"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">The document has no line list or contains a null line.</exception>
    public static EInvoiceTotals Calculate(EInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Lines is null)
        {
            throw new ArgumentException("[BR-16] The document has no invoice lines.", nameof(document));
        }

        List<decimal> lineNets = new(document.Lines.Count);
        for (int i = 0; i < document.Lines.Count; i++)
        {
            EInvoiceLine line = document.Lines[i]
                ?? throw new ArgumentException($"[BR-16] Invoice line {i + 1} is null.", nameof(document));

            // BT-131 = BT-129 x BT-146, rounded to 2 decimals (BR-DEC-23).
            lineNets.Add(RoundAmount(line.Quantity * line.UnitPrice));
        }

        // BG-23 groups keyed by (category, rate); Exempt and NotSubject always have rate 0.
        List<EInvoiceVatBreakdown> breakdown = document.Lines
            .Select((line, index) => (Key: (line.VatCategory, Rate: GroupRate(line)), Net: lineNets[index]))
            .GroupBy(item => item.Key)
            .Select(group =>
            {
                // BR-S-08 / BR-E-08 / BR-O-08: taxable amount = sum of the group's line net amounts.
                decimal taxable = group.Sum(item => item.Net);

                // BR-S-09 / BR-CO-17: tax = taxable x rate / 100, rounded after summing the group.
                // BR-E-09 / BR-O-09: tax is 0 for Exempt and NotSubject.
                decimal tax = group.Key.VatCategory == EInvoiceVatCategory.Standard
                    ? RoundAmount(taxable * group.Key.Rate / 100m)
                    : 0m;

                return new EInvoiceVatBreakdown(group.Key.VatCategory, group.Key.Rate, taxable, tax);
            })
            .OrderBy(group => CategoryOrder(group.Category))
            .ThenByDescending(group => group.Rate)
            .ToList();

        decimal lineExtension = lineNets.Sum();                      // BT-106, BR-CO-10
        decimal taxExclusive = lineExtension;                        // BT-109, BR-CO-13 (no allowances/charges)
        decimal taxTotal = breakdown.Sum(group => group.TaxAmount);  // BT-110, BR-CO-14
        decimal taxInclusive = taxExclusive + taxTotal;              // BT-112, BR-CO-15
        decimal payable = taxInclusive;                              // BT-115, BR-CO-16 (no prepaid/rounding)

        return new EInvoiceTotals(
            lineNets,
            lineExtension,
            taxExclusive,
            taxTotal,
            taxInclusive,
            payable,
            breakdown);
    }

    private static decimal GroupRate(EInvoiceLine line)
    {
        return line.VatCategory == EInvoiceVatCategory.Standard ? line.VatRate : 0m;
    }

    private static int CategoryOrder(EInvoiceVatCategory category)
    {
        return category switch
        {
            EInvoiceVatCategory.Standard => 0,
            EInvoiceVatCategory.Exempt => 1,
            _ => 2
        };
    }

    private static decimal RoundAmount(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
