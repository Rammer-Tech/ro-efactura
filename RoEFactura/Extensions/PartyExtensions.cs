using UblSharp;

namespace RoEFactura.Extensions;

/// <summary>
/// Extensions for party data extraction from invoices
/// </summary>
/// <example>
/// <code>
/// var sellerVat = invoice.GetSellerVatId();
/// var buyerName = invoice.GetBuyerName();
/// </code>
/// </example>
public static partial class PartyExtensions
{
    /// <summary>
    /// Gets the seller's VAT identifier
    /// </summary>
    /// <example>
    /// <code>
    /// var vatId = invoice.GetSellerVatId();
    /// </code>
    /// </example>
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
    /// Gets the seller's name.
    /// Tries PartyLegalEntity.RegistrationName (BT-27, mandatory legal name) first,
    /// then falls back to PartyName.Name (BT-28, optional trading name).
    /// </summary>
    public static string? GetSellerName(this InvoiceType invoice)
    {
        return invoice?.AccountingSupplierParty?.Party?.PartyLegalEntity?.FirstOrDefault()?.RegistrationName?.Value
            ?? invoice?.AccountingSupplierParty?.Party?.PartyName?.FirstOrDefault()?.Name?.Value;
    }

    /// <summary>
    /// Gets the buyer's name.
    /// Tries PartyLegalEntity.RegistrationName (BT-44, mandatory legal name) first,
    /// then falls back to PartyName.Name (BT-45, optional trading name).
    /// </summary>
    public static string? GetBuyerName(this InvoiceType invoice)
    {
        return invoice?.AccountingCustomerParty?.Party?.PartyLegalEntity?.FirstOrDefault()?.RegistrationName?.Value
            ?? invoice?.AccountingCustomerParty?.Party?.PartyName?.FirstOrDefault()?.Name?.Value;
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
    /// Gets the payee's name (if different from seller).
    /// Tries PartyLegalEntity.RegistrationName (BT-59, mandatory legal name) first,
    /// then falls back to PartyName.Name (optional trading name).
    /// </summary>
    public static string? GetPayeeName(this InvoiceType invoice)
    {
        return invoice?.PayeeParty?.PartyLegalEntity?.FirstOrDefault()?.RegistrationName?.Value
            ?? invoice?.PayeeParty?.PartyName?.FirstOrDefault()?.Name?.Value;
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

    /// <summary>
    /// Gets the seller's party identification (BT-29).
    /// This is a general identifier field where some systems place the CUI/CIF.
    /// </summary>
    public static string? GetSellerIdentifier(this InvoiceType invoice)
    {
        return invoice?.AccountingSupplierParty?.Party?.PartyIdentification?.FirstOrDefault()?.ID?.Value;
    }

    /// <summary>
    /// Gets the buyer's party identification (BT-46).
    /// This is a general identifier field where some systems place the CUI/CIF.
    /// </summary>
    public static string? GetBuyerIdentifier(this InvoiceType invoice)
    {
        return invoice?.AccountingCustomerParty?.Party?.PartyIdentification?.FirstOrDefault()?.ID?.Value;
    }
}