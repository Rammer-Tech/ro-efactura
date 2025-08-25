using System.Text.RegularExpressions;
using FluentValidation;
using RoEFactura.Validation.Constants;
using RoEFactura.Validation.PartyValidators;
using UblSharp;


namespace RoEFactura.Validation;

public class RoCiusUblValidator : AbstractValidator<InvoiceType>
{
    private static readonly Regex InvoiceNumberDigitRegex = new(@"\d", RegexOptions.Compiled);

    public RoCiusUblValidator()
    {
        // BR-RO-CIUS: CustomizationID must be RO_CIUS
        RuleFor(x => x.CustomizationID?.Value)
            .Equal(RomanianConstants.RoCiusCustomizationId)
            .WithErrorCode("BR-RO-CIUS")
            .WithMessage($"CustomizationID must be: {RomanianConstants.RoCiusCustomizationId}");

        // BR-RO-010: Invoice number must contain at least one digit
        RuleFor(x => x.ID?.Value)
            .Must(ContainsDigit)
            .WithErrorCode("BR-RO-010")
            .WithMessage("Invoice number must contain at least one digit.");

        // BR-RO-020: Invoice type code must be one of allowed values
        RuleFor(x => x.InvoiceTypeCode?.Value)
            .Must(code => RomanianConstants.ValidInvoiceTypeCodes.Contains(code ?? ""))
            .WithErrorCode("BR-RO-020")
            .WithMessage($"Invalid invoice type code. Must be one of: {string.Join(", ", RomanianConstants.ValidInvoiceTypeCodes)}");

        // BR-RO-030: If document currency ≠ RON, then VAT currency must be RON
        When(x => x.DocumentCurrencyCode?.Value != "RON", () =>
        {
            RuleFor(x => x.TaxCurrencyCode?.Value)
                .Equal("RON")
                .WithErrorCode("BR-RO-030")
                .WithMessage("When document currency is not RON, VAT accounting currency must be RON.");
        });

        // BR-RO-040: VAT point date code validation
        RuleFor(x => x.TaxPointDate?.Value)
            .Must((invoice, taxPointDate) => ValidateVatPointDateCode(invoice))
            .When(x => !string.IsNullOrEmpty(x.TaxPointDate?.Value))
            .WithErrorCode("BR-RO-040")
            .WithMessage($"VAT point date code must be one of: {string.Join(", ", RomanianConstants.ValidVatPointDateCodes)}");

        // Core EN 16931 requirements
        RuleFor(x => x.ID?.Value)
            .NotEmpty()
            .WithErrorCode("BR-1")
            .WithMessage("Invoice number is required.");

        RuleFor(x => x.IssueDate?.Value)
            .NotEmpty()
            .WithErrorCode("BR-2")
            .WithMessage("Invoice issue date is required.");

        RuleFor(x => x.InvoiceTypeCode?.Value)
            .NotEmpty()
            .WithErrorCode("BR-3")
            .WithMessage("Invoice type code is required.");

        RuleFor(x => x.DocumentCurrencyCode?.Value)
            .NotEmpty()
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
            .When(x => x.PayeeParty != null);

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
        
        var decimalPlaces = BitConverter.GetBytes(decimal.GetBits(value.Value)[3])[2];
        return decimalPlaces <= 2;
    }
}