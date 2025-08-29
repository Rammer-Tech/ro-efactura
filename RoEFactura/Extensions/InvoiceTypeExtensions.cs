using RoEFactura.Validation.Constants;
using UblSharp;

namespace RoEFactura.Extensions;

/// <summary>
/// Extensions for InvoiceType analysis and data extraction
/// </summary>
public static partial class InvoiceTypeExtensions
{
    /// <summary>
    /// Determines if the invoice is a Romanian invoice
    /// </summary>
    public static bool IsRomanianInvoice(this InvoiceType invoice)
    {
        if (invoice == null) return false;

        // Check customization ID for RO_CIUS
        if (invoice.CustomizationID?.Value == RomanianConstants.RoCiusCustomizationId)
            return true;

        // Check seller country
        string? sellerCountry = invoice.AccountingSupplierParty?.Party?.PostalAddress?.Country?.IdentificationCode?.Value;
        if (sellerCountry == "RO")
            return true;

        // Check buyer country
        string? buyerCountry = invoice.AccountingCustomerParty?.Party?.PostalAddress?.Country?.IdentificationCode?.Value;
        if (buyerCountry == "RO")
            return true;

        return false;
    }

    /// <summary>
    /// Gets the document currency code
    /// </summary>
    public static string GetCurrencyCode(this InvoiceType invoice)
    {
        return invoice?.DocumentCurrencyCode?.Value ?? "";
    }

    /// <summary>
    /// Gets the total amount due for payment
    /// </summary>
    public static decimal GetTotalAmountDue(this InvoiceType invoice)
    {
        return invoice?.LegalMonetaryTotal?.PayableAmount?.Value ?? 0m;
    }

    /// <summary>
    /// Gets a validation summary for the invoice
    /// </summary>
    public static string GetValidationSummary(this InvoiceType invoice)
    {
        if (invoice == null) return "Invalid invoice";

        List<string> summary = new List<string>();

        // Basic validation
        if (string.IsNullOrEmpty(invoice.ID?.Value))
            summary.Add("Missing invoice number");

        if (invoice.IssueDate?.Value == null)
            summary.Add("Missing issue date");

        if (string.IsNullOrEmpty(invoice.InvoiceTypeCode?.Value))
            summary.Add("Missing invoice type code");

        if (string.IsNullOrEmpty(invoice.DocumentCurrencyCode?.Value))
            summary.Add("Missing currency code");

        if (invoice.AccountingSupplierParty == null)
            summary.Add("Missing seller information");

        if (invoice.AccountingCustomerParty == null)
            summary.Add("Missing buyer information");

        if (invoice.InvoiceLine?.Any() != true)
            summary.Add("Missing invoice lines");

        if (invoice.LegalMonetaryTotal == null)
            summary.Add("Missing monetary totals");

        // Romanian specific validation
        if (invoice.IsRomanianInvoice())
        {
            if (invoice.CustomizationID?.Value != RomanianConstants.RoCiusCustomizationId)
                summary.Add("Missing RO_CIUS customization ID");

            string invoiceNumber = invoice.ID?.Value ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(invoiceNumber, @"\d"))
                summary.Add("Invoice number must contain at least one digit (RO requirement)");
        }

        return summary.Any() ? string.Join("; ", summary) : "Valid";
    }

    /// <summary>
    /// Gets the invoice total without VAT
    /// </summary>
    public static decimal GetTotalWithoutVat(this InvoiceType invoice)
    {
        return invoice?.LegalMonetaryTotal?.TaxExclusiveAmount?.Value ?? 0m;
    }

    /// <summary>
    /// Gets the invoice total with VAT
    /// </summary>
    public static decimal GetTotalWithVat(this InvoiceType invoice)
    {
        return invoice?.LegalMonetaryTotal?.TaxInclusiveAmount?.Value ?? 0m;
    }

    /// <summary>
    /// Gets the total VAT amount
    /// </summary>
    public static decimal GetTotalVat(this InvoiceType invoice)
    {
        return invoice?.TaxTotal?.FirstOrDefault()?.TaxAmount?.Value ?? 0m;
    }

    /// <summary>
    /// Gets the sum of all line net amounts
    /// </summary>
    public static decimal GetSumOfLineNet(this InvoiceType invoice)
    {
        return invoice?.LegalMonetaryTotal?.LineExtensionAmount?.Value ?? 0m;
    }
}