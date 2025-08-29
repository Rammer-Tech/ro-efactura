using FluentValidation;
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

        // Line period validation
        RuleFor(x => x)
            .Must(ValidateLinePeriod)
            .When(x => HasLinePeriod(x))
            .WithErrorCode("BR-30")
            .WithMessage("Invoice line period end date must be greater than or equal to start date.");

        // Maximum length validations for Romanian requirements
        RuleFor(x => x)
            .Must(HasValidNoteLengthLimit)
            .When(x => x.Note?.Any() == true)
            .WithErrorCode("RO-LINE-NOTE-LENGTH")
            .WithMessage("Invoice line note cannot exceed 300 characters.");

        RuleFor(x => x)
            .Must(HasValidItemNameLengthLimit)
            .When(x => !string.IsNullOrEmpty(x.Item?.Name?.Value))
            .WithErrorCode("RO-ITEM-NAME-LENGTH")
            .WithMessage("Item name cannot exceed 200 characters.");

        RuleFor(x => x)
            .Must(HasValidItemDescriptionLengthLimit)
            .When(x => x.Item?.Description?.Any() == true)
            .WithErrorCode("RO-ITEM-DESC-LENGTH")
            .WithMessage("Item description cannot exceed 200 characters.");
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
        return line?.InvoicedQuantity?.Value != null;
    }

    private static bool HasValidUnitCode(InvoiceLineType line)
    {
        return !string.IsNullOrEmpty(line?.InvoicedQuantity?.unitCode);
    }

    private static bool HasValidLineExtensionAmount(InvoiceLineType line)
    {
        return line?.LineExtensionAmount?.Value != null;
    }

    private static bool HasValidPriceAmount(InvoiceLineType line)
    {
        return line?.Price?.PriceAmount?.Value != null;
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

    private static bool HasValidNoteLengthLimit(InvoiceLineType line)
    {
        string? note = line?.Note?.FirstOrDefault()?.Value;
        return string.IsNullOrEmpty(note) || note.Length <= 300;
    }

    private static bool HasValidItemNameLengthLimit(InvoiceLineType line)
    {
        string? name = line?.Item?.Name?.Value;
        return string.IsNullOrEmpty(name) || name.Length <= 200;
    }

    private static bool HasValidItemDescriptionLengthLimit(InvoiceLineType line)
    {
        string? description = line?.Item?.Description?.FirstOrDefault()?.Value;
        return string.IsNullOrEmpty(description) || description.Length <= 200;
    }
}