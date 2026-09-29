namespace RoEFactura.Generation;

/// <summary>
/// The seller (BG-4). v1 issues invoices for Romanian sellers only, so the address must be in Romania.
/// </summary>
public sealed record EInvoiceSeller
{
    /// <summary>Seller name (BT-27), at most 200 characters (BR-RO-L201).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Seller CUI/CIF. Accepts <c>RO123</c>, <c>123</c> or values with spaces; normalized to 2–10 digits.
    /// Emitted as the legal registration identifier (BT-30) and, for a VAT payer, as <c>RO</c>+digits in
    /// the VAT identifier (BT-31).
    /// </summary>
    public required string Cui { get; init; }

    /// <summary>
    /// Whether the seller is VAT-registered. A VAT payer issues Standard/Exempt lines and emits BT-31; a
    /// non-payer issues NotSubject lines only and emits no VAT identifier (BR-O-02, BR-O-11/12).
    /// </summary>
    public required bool IsVatPayer { get; init; }

    /// <summary>Seller postal address (BG-5); the country must be <c>RO</c> and the county is required.</summary>
    public required EInvoiceAddress Address { get; init; }

    /// <summary>
    /// Optional trade register number or other additional legal information, emitted as
    /// <c>CompanyLegalForm</c> (BT-33), at most 1000 characters (BR-RO-L1000).
    /// </summary>
    public string? TradeRegisterNumber { get; init; }
}
