namespace RoEFactura.Generation;

/// <summary>
/// Document totals computed by <see cref="EInvoiceTotalsCalculator"/>. All amounts are in RON, rounded
/// to 2 decimals away from zero, and may be negative (storno).
/// </summary>
/// <param name="LineNetAmounts">Invoice line net amount (BT-131) per line, in line order.</param>
/// <param name="LineExtensionAmount">Sum of invoice line net amount (BT-106), BR-CO-10.</param>
/// <param name="TaxExclusiveAmount">Invoice total amount without VAT (BT-109), BR-CO-13.</param>
/// <param name="TaxAmount">Invoice total VAT amount (BT-110), BR-CO-14.</param>
/// <param name="TaxInclusiveAmount">Invoice total amount with VAT (BT-112), BR-CO-15.</param>
/// <param name="PayableAmount">Amount due for payment (BT-115), BR-CO-16.</param>
/// <param name="VatBreakdown">VAT breakdown groups (BG-23): Standard by rate descending, then Exempt, then NotSubject.</param>
public sealed record EInvoiceTotals(
    IReadOnlyList<decimal> LineNetAmounts,
    decimal LineExtensionAmount,
    decimal TaxExclusiveAmount,
    decimal TaxAmount,
    decimal TaxInclusiveAmount,
    decimal PayableAmount,
    IReadOnlyList<EInvoiceVatBreakdown> VatBreakdown);
