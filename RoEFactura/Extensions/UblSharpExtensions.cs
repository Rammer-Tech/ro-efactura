using System.Xml;
using System.Xml.Linq;
using RoEFactura.Validation.Constants;
using UblSharp;

namespace RoEFactura.Extensions;

public static class UblSharpExtensions
{
    /// <summary>
    /// Checks if the invoice has Romanian CIUS customization
    /// </summary>
    public static bool IsRomanianInvoice(this InvoiceType invoice)
    {
        return invoice.CustomizationID?.Value?.Contains("RO_CIUS") == true;
    }

    /// <summary>
    /// Gets the seller VAT identifier
    /// </summary>
    public static string? GetSellerVatId(this InvoiceType invoice)
    {
        return invoice.AccountingSupplierParty?.Party?.PartyTaxScheme?
            .FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the seller legal registration ID (CUI/CIF)
    /// </summary>
    public static string? GetSellerLegalId(this InvoiceType invoice)
    {
        return invoice.AccountingSupplierParty?.Party?.PartyLegalEntity?
            .FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the buyer VAT identifier
    /// </summary>
    public static string? GetBuyerVatId(this InvoiceType invoice)
    {
        return invoice.AccountingCustomerParty?.Party?.PartyTaxScheme?
            .FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the buyer legal registration ID (CUI/CIF)
    /// </summary>
    public static string? GetBuyerLegalId(this InvoiceType invoice)
    {
        return invoice.AccountingCustomerParty?.Party?.PartyLegalEntity?
            .FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the total amount due for payment
    /// </summary>
    public static decimal GetTotalAmountDue(this InvoiceType invoice)
    {
        return invoice.LegalMonetaryTotal?.PayableAmount?.Value ?? 0;
    }

    /// <summary>
    /// Gets the total VAT amount
    /// </summary>
    public static decimal GetTotalVatAmount(this InvoiceType invoice)
    {
        return invoice.TaxTotal?.FirstOrDefault()?.TaxAmount?.Value ?? 0;
    }

    /// <summary>
    /// Gets the invoice total without VAT
    /// </summary>
    public static decimal GetTotalWithoutVat(this InvoiceType invoice)
    {
        return invoice.LegalMonetaryTotal?.TaxExclusiveAmount?.Value ?? 0;
    }

    /// <summary>
    /// Gets the invoice total with VAT
    /// </summary>
    public static decimal GetTotalWithVat(this InvoiceType invoice)
    {
        return invoice.LegalMonetaryTotal?.TaxInclusiveAmount?.Value ?? 0;
    }

    /// <summary>
    /// Checks if the invoice is of Romanian type (one of the allowed codes)
    /// </summary>
    public static bool IsValidRomanianInvoiceType(this InvoiceType invoice)
    {
        return RomanianConstants.ValidInvoiceTypeCodes.Contains(invoice.InvoiceTypeCode?.Value ?? "");
    }

    /// <summary>
    /// Checks if the party is Romanian based on address
    /// </summary>
    public static bool IsRomanianParty(this UblSharp.CommonAggregateComponents.PartyType party)
    {
        return party?.PostalAddress?.Country?.IdentificationCode?.Value == "RO";
    }

    /// <summary>
    /// Gets the party name safely
    /// </summary>
    public static string GetPartyName(this UblSharp.CommonAggregateComponents.PartyType party)
    {
        return party?.PartyName?.FirstOrDefault()?.Name?.Value ?? "";
    }

    /// <summary>
    /// Loads an invoice from XML string with error handling
    /// </summary>
    public static InvoiceType? LoadInvoiceFromXml(string xmlContent)
    {
        try
        {
            return UblDocument.Load<InvoiceType>(xmlContent);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Invalid UBL XML: {ex.Message}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Failed to load UBL document: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves an invoice to XML string with proper formatting
    /// </summary>
    public static string SaveInvoiceToXml(this InvoiceType invoice)
    {
        try
        {
            var xmlDocument = UblDocument.Save(invoice);
            
            // Format the XML for readability
            var doc = XDocument.Parse(xmlDocument);
            return doc.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save UBL document: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the invoice currency code
    /// </summary>
    public static string GetCurrencyCode(this InvoiceType invoice)
    {
        return invoice.DocumentCurrencyCode?.Value ?? "";
    }

    /// <summary>
    /// Gets the VAT currency code
    /// </summary>
    public static string? GetVatCurrencyCode(this InvoiceType invoice)
    {
        return invoice.TaxCurrencyCode?.Value;
    }

    /// <summary>
    /// Checks if VAT currency rules are satisfied (BR-RO-030)
    /// </summary>
    public static bool IsVatCurrencyValid(this InvoiceType invoice)
    {
        var docCurrency = invoice.GetCurrencyCode();
        var vatCurrency = invoice.GetVatCurrencyCode();

        // If document currency is not RON, VAT currency must be RON
        if (docCurrency != "RON" && vatCurrency != "RON")
            return false;

        return true;
    }

    /// <summary>
    /// Gets all line extension amounts
    /// </summary>
    public static decimal[] GetLineExtensionAmounts(this InvoiceType invoice)
    {
        return invoice.InvoiceLine?
            .Select(line => line.LineExtensionAmount?.Value ?? 0)
            .ToArray() ?? Array.Empty<decimal>();
    }

    /// <summary>
    /// Calculates the sum of all line net amounts
    /// </summary>
    public static decimal CalculateLineNetSum(this InvoiceType invoice)
    {
        return invoice.GetLineExtensionAmounts().Sum();
    }

    /// <summary>
    /// Gets the issue date as DateTime
    /// </summary>
    public static DateTime? GetIssueDateTime(this InvoiceType invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.IssueDate?.Value))
            return null;

        if (DateTime.TryParse(invoice.IssueDate.Value, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Gets the due date as DateTime
    /// </summary>
    public static DateTime? GetDueDateTime(this InvoiceType invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.DueDate?.Value))
            return null;

        if (DateTime.TryParse(invoice.DueDate.Value, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Checks if the invoice has the correct Romanian CIUS customization ID
    /// </summary>
    public static bool HasValidRoCiusCustomization(this InvoiceType invoice)
    {
        return invoice.CustomizationID?.Value == RomanianConstants.RoCiusCustomizationId;
    }

    /// <summary>
    /// Gets validation summary for quick checks
    /// </summary>
    public static string GetValidationSummary(this InvoiceType invoice)
    {
        var issues = new List<string>();

        if (!invoice.HasValidRoCiusCustomization())
            issues.Add("Invalid RO_CIUS customization");

        if (!invoice.IsValidRomanianInvoiceType())
            issues.Add("Invalid invoice type for Romania");

        if (!invoice.IsVatCurrencyValid())
            issues.Add("Invalid VAT currency");

        if (string.IsNullOrWhiteSpace(invoice.ID?.Value))
            issues.Add("Missing invoice number");

        if (invoice.GetIssueDateTime() == null)
            issues.Add("Invalid issue date");

        return issues.Any() ? string.Join(", ", issues) : "No issues detected";
    }
}