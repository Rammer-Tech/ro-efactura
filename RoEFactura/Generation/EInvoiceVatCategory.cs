namespace RoEFactura.Generation;

/// <summary>
/// VAT category of an invoice line (BT-151) and of a VAT breakdown group (BT-118), coded with
/// UNCL5305 (BR-CL-17/BR-CL-18). Only the categories supported by the v1 generator are listed.
/// </summary>
public enum EInvoiceVatCategory
{
    /// <summary>Standard rated, code <c>S</c>. The VAT rate must be greater than zero (BR-S-05).</summary>
    Standard,

    /// <summary>
    /// Exempt from VAT, code <c>E</c>. The VAT rate is zero (BR-E-05) and the document must carry an
    /// <see cref="EInvoiceDocument.VatExemption"/> reason (BR-E-10).
    /// </summary>
    Exempt,

    /// <summary>
    /// Not subject to VAT, code <c>O</c> — used by a seller that is not VAT-registered. No VAT rate is
    /// emitted (BR-O-05) and no seller/buyer VAT identifier may appear on the invoice (BR-O-02).
    /// </summary>
    NotSubject
}
