namespace RoEFactura.Generation;

/// <summary>
/// The VAT exemption reason of the document's Exempt (<c>E</c>) VAT breakdown: a reason code (BT-121)
/// and/or a reason text (BT-120). At least one must be non-blank (BR-E-10). A document carries at most
/// one exemption reason because an invoice has exactly one <c>E</c> breakdown group (BR-E-01).
/// </summary>
/// <param name="ReasonCode">
/// Optional VATEX code (BT-121), e.g. <c>VATEX-EU-132-1I</c>; when set it must start with <c>VATEX-</c> (BR-CL-22).
/// </param>
/// <param name="Reason">Optional reason text (BT-120), passed through up to 200 characters.</param>
public sealed record EInvoiceVatExemption(string? ReasonCode, string? Reason);
