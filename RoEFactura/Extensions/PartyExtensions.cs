using UblSharp;

namespace RoEFactura.Extensions;

/// <summary>
/// Extensions for party data extraction from invoices
/// </summary>
public static partial class PartyExtensions
{
    /// <summary>
    /// Gets the seller's VAT identifier
    /// </summary>
    public static string? GetSellerVatId(this InvoiceType invoice)
    {
        return invoice?.AccountingSupplierParty?.Party?.PartyTaxScheme?.FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the buyer's VAT identifier
    /// </summary>
    public static string? GetBuyerVatId(this InvoiceType invoice)
    {
        return invoice?.AccountingCustomerParty?.Party?.PartyTaxScheme?.FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the seller's legal registration identifier
    /// </summary>
    public static string? GetSellerLegalId(this InvoiceType invoice)
    {
        return invoice?.AccountingSupplierParty?.Party?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the buyer's legal registration identifier
    /// </summary>
    public static string? GetBuyerLegalId(this InvoiceType invoice)
    {
        return invoice?.AccountingCustomerParty?.Party?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the seller's name
    /// </summary>
    public static string? GetSellerName(this InvoiceType invoice)
    {
        return invoice?.AccountingSupplierParty?.Party?.PartyName?.FirstOrDefault()?.Name?.Value;
    }

    /// <summary>
    /// Gets the buyer's name
    /// </summary>
    public static string? GetBuyerName(this InvoiceType invoice)
    {
        return invoice?.AccountingCustomerParty?.Party?.PartyName?.FirstOrDefault()?.Name?.Value;
    }

    /// <summary>
    /// Gets the seller's country code
    /// </summary>
    public static string? GetSellerCountryCode(this InvoiceType invoice)
    {
        return invoice?.AccountingSupplierParty?.Party?.PostalAddress?.Country?.IdentificationCode?.Value;
    }

    /// <summary>
    /// Gets the buyer's country code
    /// </summary>
    public static string? GetBuyerCountryCode(this InvoiceType invoice)
    {
        return invoice?.AccountingCustomerParty?.Party?.PostalAddress?.Country?.IdentificationCode?.Value;
    }

    /// <summary>
    /// Gets the payee's name (if different from seller)
    /// </summary>
    public static string? GetPayeeName(this InvoiceType invoice)
    {
        return invoice?.PayeeParty?.PartyName?.FirstOrDefault()?.Name?.Value;
    }

    /// <summary>
    /// Gets the payee's VAT identifier
    /// </summary>
    public static string? GetPayeeVatId(this InvoiceType invoice)
    {
        return invoice?.PayeeParty?.PartyTaxScheme?.FirstOrDefault()?.CompanyID?.Value;
    }

    /// <summary>
    /// Gets the payee's legal registration identifier
    /// </summary>
    public static string? GetPayeeLegalId(this InvoiceType invoice)
    {
        return invoice?.PayeeParty?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value;
    }
}