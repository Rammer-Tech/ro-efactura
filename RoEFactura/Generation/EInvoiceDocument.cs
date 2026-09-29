namespace RoEFactura.Generation;

/// <summary>
/// An invoice to be emitted as CIUS-RO UBL 2.1 XML by <see cref="IEInvoiceXmlGenerator"/>. Always an
/// Invoice with TypeCode 380 (BR-RO-020); a storno invoice is a 380 with negative quantities and a
/// <see cref="BillingReference"/> to the original invoice.
/// </summary>
/// <remarks>
/// v1 limits: currency RON only; VAT categories Standard (S), Exempt (E) and NotSubject (O); no
/// document-level allowances or charges, no prepaid or rounding amount.
/// </remarks>
public sealed record EInvoiceDocument
{
    /// <summary>Invoice number (BT-1). Must contain at least one digit (BR-RO-010), at most 200 characters (BR-RO-L155).</summary>
    public required string Number { get; init; }

    /// <summary>Invoice issue date (BT-2), emitted as <c>yyyy-MM-dd</c> (BR-RO-DT001).</summary>
    public required DateOnly IssueDate { get; init; }

    /// <summary>Seller (BG-4).</summary>
    public required EInvoiceSeller Seller { get; init; }

    /// <summary>Buyer (BG-7).</summary>
    public required EInvoiceBuyer Buyer { get; init; }

    /// <summary>Invoice lines (BG-25); at least one (BR-16).</summary>
    public required IReadOnlyList<EInvoiceLine> Lines { get; init; }

    /// <summary>
    /// Payment due date (BT-9). When the amount due (BT-115) is positive, a due date or
    /// <see cref="PaymentTerms"/> is required (BR-CO-25).
    /// </summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>Invoice currency (BT-5). Only <c>RON</c> is supported in v1.</summary>
    public string CurrencyCode { get; init; } = "RON";

    /// <summary>Invoice notes (BT-22): at most 20 (BR-RO-A020), each at most 300 characters (BR-RO-L302).</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>Optional credit-transfer payment instructions (BG-16).</summary>
    public EInvoicePayment? Payment { get; init; }

    /// <summary>Payment terms (BT-20), at most 300 characters (BR-RO-L301).</summary>
    public string? PaymentTerms { get; init; }

    /// <summary>Preceding invoice reference (BG-3), used by storno invoices.</summary>
    public EInvoiceBillingReference? BillingReference { get; init; }

    /// <summary>
    /// The single VAT exemption reason of the Exempt breakdown (BR-E-01, BR-E-10). Required when any line
    /// is <see cref="EInvoiceVatCategory.Exempt"/>; ignored otherwise.
    /// </summary>
    public EInvoiceVatExemption? VatExemption { get; init; }
}
