using System.Text.RegularExpressions;
using FluentValidation;
using RoEFactura.Validation.Constants;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation;

public class InvoiceLineValidator : AbstractValidator<InvoiceLineType>
{
    public InvoiceLineValidator()
    {
        // BR-21: Invoice line identifier is required
        RuleFor(x => x)
            .Must(HasValidLineId)
            .WithErrorCode("BR-21")
            .WithMessage("Invoice line identifier is required.");

        // BR-22: Invoice line quantity is required
        RuleFor(x => x)
            .Must(HasValidQuantity)
            .WithErrorCode("BR-22")
            .WithMessage("Invoice line quantity is required.");

        // BR-23: Invoice line unit of measure is required
        RuleFor(x => x)
            .Must(HasValidUnitCode)
            .WithErrorCode("BR-23")
            .WithMessage("Invoice line unit of measure is required.");

        // BR-24: Invoice line net amount is required
        RuleFor(x => x)
            .Must(HasValidLineExtensionAmount)
            .WithErrorCode("BR-24")
            .WithMessage("Invoice line net amount is required.");

        // BR-25: Invoice line net unit price is required
        RuleFor(x => x)
            .Must(HasValidPriceAmount)
            .WithErrorCode("BR-25")
            .WithMessage("Invoice line net unit price is required.");

        // BR-26: Invoice line item name is required
        RuleFor(x => x)
            .Must(HasValidItemName)
            .WithErrorCode("BR-26")
            .WithMessage("Invoice line item name is required.");

        // BR-27: Net unit price must not be negative
        RuleFor(x => x)
            .Must(HasNonNegativePriceAmount)
            .When(x => x.Price?.PriceAmount?.Value != null)
            .WithErrorCode("BR-27")
            .WithMessage("Invoice line net unit price must not be negative.");

        // BR-28: Gross unit price must not be negative (same as net in UblSharp)
        RuleFor(x => x)
            .Must(HasNonNegativePriceAmount)
            .When(x => x.Price?.PriceAmount?.Value != null)
            .WithErrorCode("BR-28")
            .WithMessage("Invoice line gross unit price must not be negative.");

        // VAT category is required
        RuleFor(x => x)
            .Must(HasValidVatCategory)
            .WithErrorCode("BR-CO-4")
            .WithMessage("Invoice line VAT category code is required.");

        // BR-S-05: Standard rated ⇒ VAT rate must be greater than zero.
        RuleFor(x => x)
            .Must(HasPositiveVatRate)
            .When(x => IsLineVatCategory(x, "S"))
            .WithErrorCode("BR-S-05")
            .WithMessage("In an invoice line where the VAT category code is 'Standard rated', the VAT rate shall be greater than zero.");

        // BR-Z-05 / BR-E-05 / BR-AE-05 / BR-IC-05 / BR-G-05: Zero rated / Exempt / Reverse charge /
        // Intra-community supply / Export outside the EU ⇒ VAT rate must be zero. Absence cannot be
        // reliably distinguished from zero in UblSharp (non-null placeholder quirk), so the rule is
        // lenient: it only fails when a rate is present and is not zero.
        RuleFor(x => x)
            .Must(HasZeroOrAbsentVatRate)
            .When(x => IsLineVatCategory(x, "Z"))
            .WithErrorCode("BR-Z-05")
            .WithMessage("In an invoice line where the VAT category code is 'Zero rated', the VAT rate shall be 0 (zero).");

        RuleFor(x => x)
            .Must(HasZeroOrAbsentVatRate)
            .When(x => IsLineVatCategory(x, "E"))
            .WithErrorCode("BR-E-05")
            .WithMessage("In an invoice line where the VAT category code is 'Exempt from VAT', the VAT rate shall be 0 (zero).");

        RuleFor(x => x)
            .Must(HasZeroOrAbsentVatRate)
            .When(x => IsLineVatCategory(x, "AE"))
            .WithErrorCode("BR-AE-05")
            .WithMessage("In an invoice line where the VAT category code is 'Reverse charge', the VAT rate shall be 0 (zero).");

        RuleFor(x => x)
            .Must(HasZeroOrAbsentVatRate)
            .When(x => IsLineVatCategory(x, "K"))
            .WithErrorCode("BR-IC-05")
            .WithMessage("In an invoice line where the VAT category code is 'Intra-community supply', the VAT rate shall be 0 (zero).");

        RuleFor(x => x)
            .Must(HasZeroOrAbsentVatRate)
            .When(x => IsLineVatCategory(x, "G"))
            .WithErrorCode("BR-G-05")
            .WithMessage("In an invoice line where the VAT category code is 'Export outside the EU', the VAT rate shall be 0 (zero).");

        // BR-O-05: Not subject to VAT ⇒ the invoice line shall not contain a VAT rate.
        // Same UblSharp leniency: a rate that is absent or zero passes; only a non-zero rate fails.
        RuleFor(x => x)
            .Must(HasZeroOrAbsentVatRate)
            .When(x => IsLineVatCategory(x, "O"))
            .WithErrorCode("BR-O-05")
            .WithMessage("An invoice line where the VAT category code is 'Not subject to VAT' shall not contain a VAT rate.");

        // Line period validation
        RuleFor(x => x)
            .Must(ValidateLinePeriod)
            .When(x => HasLinePeriod(x))
            .WithErrorCode("BR-30")
            .WithMessage("Invoice line period end date must be greater than or equal to start date.");

        // Maximum length validations for Romanian requirements (RO16931-rules.sch length rules)
        RuleFor(x => x)
            .Must(HasValidNoteLengthLimit)
            .When(x => x.Note?.Any() == true)
            .WithErrorCode(RoCiusRuleIds.MaxLength300)
            .WithMessage($"Invoice line note cannot exceed {RomanianConstants.LineNoteMaxLength} characters.");

        RuleFor(x => x)
            .Must(HasValidItemNameLengthLimit)
            .When(x => !string.IsNullOrEmpty(x.Item?.Name?.Value))
            .WithErrorCode(RoCiusRuleIds.MaxLength100)
            .WithMessage($"Item name cannot exceed {RomanianConstants.ItemNameMaxLength} characters.");

        RuleFor(x => x)
            .Must(HasValidItemDescriptionLengthLimit)
            .When(x => x.Item?.Description?.Any() == true)
            .WithErrorCode(RoCiusRuleIds.MaxLength200)
            .WithMessage($"Item description cannot exceed {RomanianConstants.ItemDescriptionMaxLength} characters.");
    }

    private static bool HasLinePeriod(InvoiceLineType line)
    {
        return line.InvoicePeriod?.Any() == true;
    }

    private static bool ValidateLinePeriod(InvoiceLineType line)
    {
        PeriodType? period = line.InvoicePeriod?.FirstOrDefault();
        if (period?.StartDate?.Value == null || period?.EndDate?.Value == null)
            return true;

        if (!DateTime.TryParse(period.StartDate.Value.ToString(), out DateTime startDate) ||
            !DateTime.TryParse(period.EndDate.Value.ToString(), out DateTime endDate))
            return true;

        return endDate >= startDate;
    }

    private static bool HasValidLineId(InvoiceLineType line)
    {
        return !string.IsNullOrEmpty(line?.ID?.Value);
    }

    private static bool HasValidQuantity(InvoiceLineType line)
    {
        // UblSharp QuantityType.Value is non-nullable decimal; default instance has Value 0.
        return line?.InvoicedQuantity?.Value != 0;
    }

    private static bool HasValidUnitCode(InvoiceLineType line)
    {
        return !string.IsNullOrEmpty(line?.InvoicedQuantity?.unitCode);
    }

    private static bool HasValidLineExtensionAmount(InvoiceLineType line)
    {
        return !string.IsNullOrEmpty(line?.LineExtensionAmount?.currencyID);
    }

    private static bool HasValidPriceAmount(InvoiceLineType line)
    {
        return !string.IsNullOrEmpty(line?.Price?.PriceAmount?.currencyID);
    }

    private static bool HasValidItemName(InvoiceLineType line)
    {
        return !string.IsNullOrEmpty(line?.Item?.Name?.Value);
    }

    private static bool HasNonNegativePriceAmount(InvoiceLineType line)
    {
        decimal? price = line?.Price?.PriceAmount?.Value;
        return price >= 0;
    }

    private static bool HasValidVatCategory(InvoiceLineType line)
    {
        return !string.IsNullOrEmpty(line?.Item?.ClassifiedTaxCategory?.FirstOrDefault()?.ID?.Value);
    }

    private static TaxCategoryType? GetLineTaxCategory(InvoiceLineType line)
    {
        return line?.Item?.ClassifiedTaxCategory?.FirstOrDefault();
    }

    private static bool IsLineVatCategory(InvoiceLineType line, string categoryId)
    {
        return string.Equals(GetLineTaxCategory(line)?.ID?.Value, categoryId, StringComparison.Ordinal);
    }

    private static bool HasPositiveVatRate(InvoiceLineType line)
    {
        decimal? percent = GetLineTaxCategory(line)?.Percent?.Value;
        return (percent ?? 0m) > 0m;
    }

    private static bool HasZeroOrAbsentVatRate(InvoiceLineType line)
    {
        decimal? percent = GetLineTaxCategory(line)?.Percent?.Value;
        return percent == null || percent.Value == 0m;
    }

    private static bool HasValidNoteLengthLimit(InvoiceLineType line)
    {
        string? note = line?.Note?.FirstOrDefault()?.Value;
        return NormalizedLength(note) <= RomanianConstants.LineNoteMaxLength;
    }

    private static bool HasValidItemNameLengthLimit(InvoiceLineType line)
    {
        string? name = line?.Item?.Name?.Value;
        return NormalizedLength(name) <= RomanianConstants.ItemNameMaxLength;
    }

    private static bool HasValidItemDescriptionLengthLimit(InvoiceLineType line)
    {
        string? description = line?.Item?.Description?.FirstOrDefault()?.Value;
        return NormalizedLength(description) <= RomanianConstants.ItemDescriptionMaxLength;
    }

    /// <summary>
    /// Mirrors the schematron's <c>string-length(normalize-space(.))</c>: trims and collapses internal
    /// whitespace runs before measuring, so the local pre-check never rejects what ANAF accepts.
    /// </summary>
    private static int NormalizedLength(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        return Regex.Replace(value.Trim(), @"\s+", " ").Length;
    }
}
