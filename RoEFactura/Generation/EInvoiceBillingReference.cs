namespace RoEFactura.Generation;

/// <summary>
/// Preceding invoice reference (BG-3): the number (BT-25, BR-55, at most 200 characters per BR-RO-L156)
/// and issue date (BT-26) of the invoice that a storno invoice corrects.
/// </summary>
/// <param name="Number">The preceding invoice number (BT-25).</param>
/// <param name="IssueDate">The preceding invoice issue date (BT-26).</param>
public sealed record EInvoiceBillingReference(string Number, DateOnly IssueDate);
