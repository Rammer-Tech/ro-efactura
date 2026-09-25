using System.Text.RegularExpressions;
using FluentValidation;
using RoEFactura.Validation.Constants;
using RoEFactura.Validation.PartyValidators;
using UblSharp;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;


namespace RoEFactura.Validation;

public class RoCiusUblValidator : AbstractValidator<InvoiceType>
{
    private static readonly Regex InvoiceNumberDigitRegex = new(@"\d", RegexOptions.Compiled);

    public RoCiusUblValidator()
    {
        // BR-RO-001: CustomizationID must be the current CIUS-RO 1.0.1 identifier.
        RuleFor(x => x)
            .Must(HasValidCustomizationId)
            .WithErrorCode(RoCiusRuleIds.CustomizationId)
            .WithMessage($"CustomizationID must be: {RomanianConstants.CustomizationId}");

        // BR-RO-010: Invoice number must contain at least one digit
        RuleFor(x => x)
            .Must(HasValidInvoiceNumber)
            .WithErrorCode(RoCiusRuleIds.InvoiceNumberDigit)
            .WithMessage("Invoice number must contain at least one digit.");

        // BR-RO-020: Invoice type code must be one of allowed values
        RuleFor(x => x)
            .Must(HasValidInvoiceTypeCode)
            .WithErrorCode(RoCiusRuleIds.InvoiceTypeCode)
            .WithMessage($"Invalid invoice type code. Must be one of: {string.Join(", ", RomanianConstants.ValidInvoiceTypeCodes)}");

        // BR-RO-030: If document currency ≠ RON, then VAT currency must be RON
        RuleFor(x => x)
            .Must(HasValidVatCurrency)
            .When(x => x.DocumentCurrencyCode?.Value != "RON")
            .WithErrorCode(RoCiusRuleIds.VatAccountingCurrency)
            .WithMessage("When document currency is not RON, VAT accounting currency must be RON.");

        // BR-RO-040: every non-blank InvoicePeriod[*].DescriptionCode[*] (BT-8) must be 3, 35 or 432.
        // No DescriptionCode present at all (no InvoicePeriod, or none carrying one) skips the rule.
        RuleFor(x => x)
            .Must(HasValidVatPointDateCodes)
            .When(HasAnyVatPointDateCode)
            .WithErrorCode(RoCiusRuleIds.VatPointDateCode)
            .WithMessage($"VAT point date code must be one of: {string.Join(", ", RomanianConstants.ValidVatPointDateCodes)}");

        // BR-RO-L200 (BT-1 length): only evaluated when the invoice number is present (BR-1 covers absence).
        RuleFor(x => x)
            .Must(HasValidInvoiceNumberLength)
            .When(x => !string.IsNullOrEmpty(x.ID?.Value))
            .WithErrorCode(RoCiusRuleIds.MaxLength200)
            .WithMessage($"Invoice number cannot exceed {RomanianConstants.InvoiceNumberMaxLength} characters.");

        // BR-RO-A020: at most 20 Invoice note (BG-1) occurrences.
        RuleFor(x => x)
            .Must(x => (x.Note?.Count ?? 0) <= RomanianConstants.MaxInvoiceNotes)
            .WithErrorCode(RoCiusRuleIds.MaxInvoiceNotes)
            .WithMessage($"Invoice cannot have more than {RomanianConstants.MaxInvoiceNotes} notes.");

        // BR-RO-L300 (BT-22 length): each Invoice note.
        RuleForEach(x => x.Note ?? new List<TextType>())
            .Must(note => NormalizedLength(note?.Value) <= RomanianConstants.InvoiceNoteMaxLength)
            .WithErrorCode(RoCiusRuleIds.MaxLength300)
            .WithMessage($"Invoice note cannot exceed {RomanianConstants.InvoiceNoteMaxLength} characters.")
            .OverridePropertyName("Note");

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

        // Validate each line
        RuleForEach(x => x.InvoiceLine)
            .SetValidator(new InvoiceLineValidator());

        // Validate totals
        RuleFor(x => x)
            .SetValidator(new TotalsValidator());

        // BR-E-10 / BR-AE-10 / BR-IC-10 / BR-G-10 / BR-O-10: VAT exemption reason, evaluated against the
        // document-currency TaxTotal's subtotals (the same set BR-CO-14..17 use).
        RuleForEach(x => GetDocumentCurrencySubtotals(x))
            .SetValidator(new VatBreakdownValidator())
            .OverridePropertyName("TaxTotal.TaxSubtotal");

        // BR-RO-Z2: 2 decimal places validation for monetary amounts
        RuleFor(x => x)
            .Must(ValidateDecimalPrecision)
            .WithErrorCode(RoCiusRuleIds.TwoDecimals)
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

    private static IEnumerable<string> GetVatPointDateCodes(InvoiceType invoice)
    {
        return invoice.InvoicePeriod?
            .SelectMany(period => period?.DescriptionCode ?? new List<CodeType>())
            .Select(code => code?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            ?? Enumerable.Empty<string>();
    }

    private static bool HasAnyVatPointDateCode(InvoiceType invoice)
    {
        return GetVatPointDateCodes(invoice).Any();
    }

    /// <summary>
    /// Non-expression-tree helper: FluentValidation's <c>RuleForEach</c> compiles its selector to an
    /// <see cref="System.Linq.Expressions.Expression"/>, and C# forbids null-propagating operators
    /// (<c>?.</c>/<c>??</c>) directly inside an expression tree lambda (CS8072).
    /// </summary>
    private static List<TaxSubtotalType> GetDocumentCurrencySubtotals(InvoiceType invoice)
    {
        return TotalsValidator.GetDocumentCurrencyTaxTotal(invoice)?.TaxSubtotal ?? new List<TaxSubtotalType>();
    }

    private static bool HasValidVatPointDateCodes(InvoiceType invoice)
    {
        return GetVatPointDateCodes(invoice).All(code => RomanianConstants.ValidVatPointDateCodes.Contains(code.Trim()));
    }

    private static bool HasValidInvoiceNumberLength(InvoiceType invoice)
    {
        return NormalizedLength(invoice?.ID?.Value) <= RomanianConstants.InvoiceNumberMaxLength;
    }

    /// <summary>Mirrors the schematron's <c>string-length(normalize-space(.))</c>: trims and collapses internal whitespace.</summary>
    private static int NormalizedLength(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        return Regex.Replace(value.Trim(), @"\s+", " ").Length;
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
        return invoice?.CustomizationID?.Value == RomanianConstants.CustomizationId;
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
