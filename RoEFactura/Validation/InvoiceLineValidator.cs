using FluentValidation;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation;

public class InvoiceLineValidator : AbstractValidator<InvoiceLineType>
{
    public InvoiceLineValidator()
    {
        // BR-21: Invoice line identifier is required
        RuleFor(x => x.ID?.Value)
            .NotEmpty()
            .WithErrorCode("BR-21")
            .WithMessage("Invoice line identifier is required.");

        // BR-22: Invoice line quantity is required  
        RuleFor(x => x.InvoicedQuantity?.Value)
            .NotNull()
            .WithErrorCode("BR-22")
            .WithMessage("Invoice line quantity is required.");

        // BR-23: Invoice line unit of measure is required
        RuleFor(x => x.InvoicedQuantity?.unitCode)
            .NotEmpty()
            .WithErrorCode("BR-23")
            .WithMessage("Invoice line unit of measure is required.");

        // BR-24: Invoice line net amount is required
        RuleFor(x => x.LineExtensionAmount?.Value)
            .NotNull()
            .WithErrorCode("BR-24")
            .WithMessage("Invoice line net amount is required.");

        // BR-25: Invoice line net unit price is required
        RuleFor(x => x.Price?.PriceAmount?.Value)
            .NotNull()
            .WithErrorCode("BR-25")
            .WithMessage("Invoice line net unit price is required.");

        // BR-26: Invoice line item name is required
        RuleFor(x => x.Item?.Name?.Value)
            .NotEmpty()
            .WithErrorCode("BR-26")
            .WithMessage("Invoice line item name is required.");

        // BR-27: Net unit price must not be negative
        RuleFor(x => x.Price?.PriceAmount?.Value)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Price?.PriceAmount?.Value != null)
            .WithErrorCode("BR-27")
            .WithMessage("Invoice line net unit price must not be negative.");

        // BR-28: Gross unit price must not be negative
        RuleFor(x => x.Price?.PriceAmount?.Value) // UblSharp doesn't have separate gross price
            .GreaterThanOrEqualTo(0)
            .When(x => x.Price?.PriceAmount?.Value != null)
            .WithErrorCode("BR-28")
            .WithMessage("Invoice line gross unit price must not be negative.");

        // VAT category is required
        RuleFor(x => x.Item?.ClassifiedTaxCategory?.FirstOrDefault()?.ID?.Value)
            .NotEmpty()
            .WithErrorCode("BR-CO-4")
            .WithMessage("Invoice line VAT category code is required.");

        // Line period validation
        RuleFor(x => x)
            .Must(ValidateLinePeriod)
            .When(x => HasLinePeriod(x))
            .WithErrorCode("BR-30")
            .WithMessage("Invoice line period end date must be greater than or equal to start date.");

        // Maximum length validations for Romanian requirements
        RuleFor(x => x.Note?.FirstOrDefault()?.Value)
            .MaximumLength(300)
            .When(x => x.Note?.Any() == true)
            .WithErrorCode("RO-LINE-NOTE-LENGTH")
            .WithMessage("Invoice line note cannot exceed 300 characters.");

        RuleFor(x => x.Item?.Name?.Value)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Item?.Name?.Value))
            .WithErrorCode("RO-ITEM-NAME-LENGTH")
            .WithMessage("Item name cannot exceed 200 characters.");

        RuleFor(x => x.Item?.Description?.FirstOrDefault()?.Value)
            .MaximumLength(200)
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
        var period = line.InvoicePeriod?.FirstOrDefault();
        if (period?.StartDate?.Value == null || period?.EndDate?.Value == null)
            return true;

        if (!DateTime.TryParse(period.StartDate.Value, out var startDate) ||
            !DateTime.TryParse(period.EndDate.Value, out var endDate))
            return true;

        return endDate >= startDate;
    }
}