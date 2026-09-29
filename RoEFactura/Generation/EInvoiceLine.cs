namespace RoEFactura.Generation;

/// <summary>
/// An invoice line (BG-25).
/// </summary>
public sealed record EInvoiceLine
{
    /// <summary>Item name (BT-153), required (BR-25), at most 100 characters (BR-RO-L1024).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Invoiced quantity (BT-129): non-zero, at most 3 decimals; negative for storno.
    /// </summary>
    public required decimal Quantity { get; init; }

    /// <summary>Item net price (BT-146): not negative (BR-27), at most 4 decimals.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Invoiced item VAT category (BT-151).</summary>
    public required EInvoiceVatCategory VatCategory { get; init; }

    /// <summary>UN/ECE Rec 20/21 unit code (BT-130) from the BR-CL-23 list; defaults to <c>H87</c> (piece).</summary>
    public string UnitCode { get; init; } = "H87";

    /// <summary>
    /// Invoiced item VAT rate (BT-152), at most 2 decimals: greater than zero for Standard (BR-S-05),
    /// zero for Exempt (BR-E-05) and NotSubject (not emitted, BR-O-05).
    /// </summary>
    public decimal VatRate { get; init; }

    /// <summary>Item description (BT-154), at most 200 characters (BR-RO-L212).</summary>
    public string? Description { get; init; }

    /// <summary>Invoice line note (BT-127), at most 300 characters (BR-RO-L303).</summary>
    public string? Note { get; init; }
}
