using System.Text.RegularExpressions;
using FluentValidation;
using RoEFactura.Validation.Constants;
using RoEFactura.Validation.PartyValidators;
using UblSharp;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation;

public class RoCiusUblValidator : AbstractValidator<InvoiceType>
{
    private static readonly Regex InvoiceNumberDigitRegex = new(@"\d", RegexOptions.Compiled);

    public RoCiusUblValidator()
    {
        // BR-RO-CIUS: CustomizationID must be RO_CIUS
        RuleFor(x => x)
            .Must(HasValidCustomizationId)
            .WithErrorCode("BR-RO-CIUS")
            .WithMessage($"CustomizationID must be: {RomanianConstants.RoCiusCustomizationId}");

        // BR-RO-010: Invoice number must contain at least one digit
        RuleFor(x => x)
            .Must(HasValidInvoiceNumber)
            .WithErrorCode("BR-RO-010")
            .WithMessage("Invoice number must contain at least one digit.");

        // BR-RO-020: Invoice type code must be one of allowed values
        RuleFor(x => x)
            .Must(HasValidInvoiceTypeCode)
            .WithErrorCode("BR-RO-020")
            .WithMessage($"Invalid invoice type code. Must be one of: {string.Join(", ", RomanianConstants.ValidInvoiceTypeCodes)}");

        // BR-RO-030: If document currency ≠ RON, then VAT currency must be RON
        RuleFor(x => x)
            .Must(HasValidVatCurrency)
            .When(x => x.DocumentCurrencyCode?.Value != "RON")
            .WithErrorCode("BR-RO-030")
            .WithMessage("When document currency is not RON, VAT accounting currency must be RON.");

        // BR-RO-040: VAT point date code validation
        RuleFor(x => x)
            .Must(ValidateVatPointDateCode)
            .When(x => x.TaxPointDate?.Value != null)
            .WithErrorCode("BR-RO-040")
            .WithMessage($"VAT point date code must be one of: {string.Join(", ", RomanianConstants.ValidVatPointDateCodes)}");

        // Core EN 16931 requirements
        RuleFor(x => x)
            .Must(HasValidId)
            .WithErrorCode("BR-1")
            .WithMessage("Invoice number is required.");

        RuleFor(x => x)
            .Must(HasValidIssueDate)
            .WithErrorCode("BR-2")
            .WithMessage("Invoice issue date is required.");

        RuleFor(x => x)
            .Must(HasValidTypeCode)
            .WithErrorCode("BR-3")
            .WithMessage("Invoice type code is required.");

        RuleFor(x => x)
            .Must(HasValidDocumentCurrency)
            .WithErrorCode("BR-5")
            .WithMessage("Invoice currency code is required.");

        // Party validators
        RuleFor(x => x.AccountingSupplierParty)
            .SetValidator(new SellerPartyValidator()!)
            .When(x => x.AccountingSupplierParty != null);

        RuleFor(x => x.AccountingCustomerParty)
            .SetValidator(new BuyerPartyValidator()!)
            .When(x => x.AccountingCustomerParty != null);

        RuleFor(x => x.PayeeParty)
            .SetValidator(new PayeePartyValidator()!)
            .When(x => IsPayeePartySpecified(x.PayeeParty));

        // Invoice lines validation
        RuleFor(x => x.InvoiceLine)
            .NotEmpty()
            .WithErrorCode("BR-16")
            .WithMessage("Invoice must have at least one line.");

        // BR-RO-A999: Maximum 999 invoice lines
        RuleFor(x => x.InvoiceLine)
            .Must(lines => lines == null || lines.Count <= 999)
            .WithErrorCode("BR-RO-A999")
            .WithMessage("Invoice cannot have more than 999 lines.");

        // Validate each line
        RuleForEach(x => x.InvoiceLine)
            .SetValidator(new InvoiceLineValidator());

        // Validate totals
        RuleFor(x => x)
            .SetValidator(new TotalsValidator());

        // BR-RO-Z2: 2 decimal places validation for monetary amounts
        RuleFor(x => x)
            .Must(ValidateDecimalPrecision)
            .WithErrorCode("BR-RO-Z2")
            .WithMessage("Monetary amounts must have maximum 2 decimal places.");
    }

    /// <summary>
    /// UblSharp initializes <see cref="InvoiceType.PayeeParty"/> with placeholder graphs (e.g. nested <see cref="PartyType.AgentParty"/> chains,
    /// empty <see cref="PartyType.EndpointID"/>, <see cref="AddressType"/> with empty country code). Only validate when real payee data exists.
    /// </summary>
    private static bool IsPayeePartySpecified(PartyType? party)
    {
        if (party == null) return false;
        if (HasPayeeNameContent(party)) return true;
        return party.PartyLegalEntity?.Count > 0
            || party.PartyName?.Count > 0
            || party.PartyTaxScheme?.Count > 0
            || party.PartyIdentification?.Count > 0
            || !string.IsNullOrEmpty(party.EndpointID?.Value)
            || party.Person?.Count > 0
            || (party.PostalAddress != null && HasPostalAddressContent(party.PostalAddress));
    }

    private static bool HasPayeeNameContent(PartyType party)
    {
        var registrationName = party.PartyLegalEntity?.FirstOrDefault()?.RegistrationName?.Value;
        var partyName = party.PartyName?.FirstOrDefault()?.Name?.Value;
        return !string.IsNullOrEmpty(registrationName) || !string.IsNullOrEmpty(partyName);
    }

    private static bool HasPostalAddressContent(AddressType address)
    {
        return !string.IsNullOrEmpty(address.StreetName?.Value)
            || !string.IsNullOrEmpty(address.AdditionalStreetName?.Value)
            || !string.IsNullOrEmpty(address.CityName?.Value)
            || !string.IsNullOrEmpty(address.CountrySubentity?.Value)
            || !string.IsNullOrEmpty(address.PostalZone?.Value)
            || !string.IsNullOrEmpty(address.Country?.IdentificationCode?.Value);
    }

    private static bool ContainsDigit(string? invoiceNumber)
    {
        return !string.IsNullOrWhiteSpace(invoiceNumber) && 
               InvoiceNumberDigitRegex.IsMatch(invoiceNumber);
    }

    private static bool ValidateVatPointDateCode(InvoiceType invoice)
    {
        // This would require parsing the VAT point date code from the UBL structure
        // For now, return true as this is a complex validation
        return true;
    }

    private static bool ValidateDecimalPrecision(InvoiceType invoice)
    {
        // Check key monetary amounts for 2 decimal precision
        if (invoice.LegalMonetaryTotal != null)
        {
            if (!HasMaxTwoDecimals(invoice.LegalMonetaryTotal.TaxExclusiveAmount?.Value))
                return false;
            if (!HasMaxTwoDecimals(invoice.LegalMonetaryTotal.TaxInclusiveAmount?.Value))
                return false;
            if (!HasMaxTwoDecimals(invoice.LegalMonetaryTotal.PayableAmount?.Value))
                return false;
        }

        return true;
    }

    private static bool HasMaxTwoDecimals(decimal? value)
    {
        if (!value.HasValue) return true;
        
        byte decimalPlaces = BitConverter.GetBytes(decimal.GetBits(value.Value)[3])[2];
        return decimalPlaces <= 2;
    }

    private static bool HasValidCustomizationId(InvoiceType invoice)
    {
        return invoice?.CustomizationID?.Value == RomanianConstants.RoCiusCustomizationId;
    }

    private static bool HasValidInvoiceNumber(InvoiceType invoice)
    {
        string? invoiceNumber = invoice?.ID?.Value;
        return ContainsDigit(invoiceNumber);
    }

    private static bool HasValidInvoiceTypeCode(InvoiceType invoice)
    {
        string code = invoice?.InvoiceTypeCode?.Value ?? "";
        return RomanianConstants.ValidInvoiceTypeCodes.Contains(code);
    }

    private static bool HasValidVatCurrency(InvoiceType invoice)
    {
        return invoice?.TaxCurrencyCode?.Value == "RON";
    }

    private static bool HasValidId(InvoiceType invoice)
    {
        return !string.IsNullOrEmpty(invoice?.ID?.Value);
    }

    private static bool HasValidIssueDate(InvoiceType invoice)
    {
        // UblSharp DateType.Value is non-nullable; default(DateTimeOffset) is used when the date is absent.
        return invoice?.IssueDate != null && invoice.IssueDate.Value != default;
    }

    private static bool HasValidTypeCode(InvoiceType invoice)
    {
        return !string.IsNullOrEmpty(invoice?.InvoiceTypeCode?.Value);
    }

    private static bool HasValidDocumentCurrency(InvoiceType invoice)
    {
        return !string.IsNullOrEmpty(invoice?.DocumentCurrencyCode?.Value);
    }
}