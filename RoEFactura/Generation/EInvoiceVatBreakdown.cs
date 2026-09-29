namespace RoEFactura.Generation;

/// <summary>
/// One VAT breakdown group (BG-23), keyed by VAT category and rate.
/// </summary>
/// <param name="Category">VAT category code (BT-118).</param>
/// <param name="Rate">VAT category rate (BT-119); 0 for Exempt and NotSubject (NotSubject emits no rate).</param>
/// <param name="TaxableAmount">VAT category taxable amount (BT-116) = Σ line net amounts of the group.</param>
/// <param name="TaxAmount">VAT category tax amount (BT-117), rounded to 2 decimals away from zero.</param>
public sealed record EInvoiceVatBreakdown(
    EInvoiceVatCategory Category,
    decimal Rate,
    decimal TaxableAmount,
    decimal TaxAmount);
