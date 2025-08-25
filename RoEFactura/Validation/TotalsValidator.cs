using FluentValidation;
using UblSharp;


namespace RoEFactura.Validation;

public class TotalsValidator : AbstractValidator<InvoiceType>
{
    public TotalsValidator()
    {
        // BR-12: Invoice total amount without VAT is required
        RuleFor(x => x)
            .Must(HasValidTaxExclusiveAmount)
            .WithErrorCode("BR-12")
            .WithMessage("Invoice total amount without VAT is required.");

        // BR-14: Invoice total amount with VAT is required
        RuleFor(x => x)
            .Must(HasValidTaxInclusiveAmount)
            .WithErrorCode("BR-14")
            .WithMessage("Invoice total amount with VAT is required.");

        // BR-15: Amount due for payment is required
        RuleFor(x => x)
            .Must(HasValidPayableAmount)
            .WithErrorCode("BR-15")
            .WithMessage("Amount due for payment is required.");

        // BR-CO-10: Sum of invoice line net amounts = invoice total amount without VAT
        RuleFor(x => x)
            .Must(ValidateLineNetAmountSum)
            .WithErrorCode("BR-CO-10")
            .WithMessage("Sum of invoice line net amounts must equal invoice total amount without VAT (within 0.01 tolerance).");

        // BR-CO-11: Invoice total amount with VAT = Invoice total amount without VAT + VAT total amount
        RuleFor(x => x)
            .Must(ValidateVatAmountCalculation)
            .WithErrorCode("BR-CO-11")
            .WithMessage("Invoice total amount with VAT must equal total without VAT plus VAT total amount (within 0.01 tolerance).");

        // BR-CO-12: VAT breakdown tax amount = VAT category taxable amount × (VAT rate / 100)
        RuleFor(x => x)
            .Must(ValidateVatBreakdownCalculations)
            .WithErrorCode("BR-CO-12")
            .WithMessage("VAT breakdown calculations must be correct (within 0.01 tolerance).");

        // BR-CO-13: Invoice total VAT amount = sum of VAT breakdown amounts
        RuleFor(x => x)
            .Must(ValidateVatTotalSum)
            .WithErrorCode("BR-CO-13")
            .WithMessage("Invoice total VAT amount must equal sum of VAT breakdown amounts (within 0.01 tolerance).");

        // Document period validation
        RuleFor(x => x)
            .Must(ValidateDocumentPeriod)
            .When(x => HasDocumentPeriod(x))
            .WithErrorCode("BR-29")
            .WithMessage("Invoice period end date must be greater than or equal to start date.");
    }

    private static bool ValidateLineNetAmountSum(InvoiceType invoice)
    {
        if (invoice.InvoiceLine == null || invoice.LegalMonetaryTotal?.TaxExclusiveAmount?.Value == null)
            return true;

        var lineSum = invoice.InvoiceLine.Sum(line => line.LineExtensionAmount?.Value ?? 0);
        var docTotal = invoice.LegalMonetaryTotal.TaxExclusiveAmount.Value;

        return Math.Abs(lineSum - docTotal) <= 0.01m;
    }

    private static bool ValidateVatAmountCalculation(InvoiceType invoice)
    {
        var totalWithoutVat = invoice.LegalMonetaryTotal?.TaxExclusiveAmount?.Value;
        var totalWithVat = invoice.LegalMonetaryTotal?.TaxInclusiveAmount?.Value;
        var vatTotal = invoice.TaxTotal?.FirstOrDefault()?.TaxAmount?.Value;

        if (totalWithoutVat == null || totalWithVat == null)
            return true;

        var expectedTotalWithVat = totalWithoutVat.Value + (vatTotal ?? 0);
        return Math.Abs(totalWithVat.Value - expectedTotalWithVat) <= 0.01m;
    }

    private static bool ValidateVatBreakdownCalculations(InvoiceType invoice)
    {
        if (invoice.TaxTotal?.FirstOrDefault()?.TaxSubtotal == null)
            return true;

        foreach (var subtotal in invoice.TaxTotal.First().TaxSubtotal)
        {
            var taxableAmount = subtotal.TaxableAmount?.Value;
            var taxAmount = subtotal.TaxAmount?.Value;
            var taxRate = subtotal.TaxCategory?.Percent?.Value;

            if (taxableAmount == null || taxAmount == null || taxRate == null)
                continue;

            var expectedTaxAmount = Math.Round(taxableAmount.Value * taxRate.Value / 100, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(taxAmount.Value - expectedTaxAmount) > 0.01m)
                return false;
        }

        return true;
    }

    private static bool ValidateVatTotalSum(InvoiceType invoice)
    {
        var invoiceVatTotal = invoice.TaxTotal?.FirstOrDefault()?.TaxAmount?.Value;
        var subtotalSum = invoice.TaxTotal?.FirstOrDefault()?.TaxSubtotal?
            .Sum(st => st.TaxAmount?.Value ?? 0);

        if (invoiceVatTotal == null || subtotalSum == null)
            return true;

        return Math.Abs(invoiceVatTotal.Value - subtotalSum.Value) <= 0.01m;
    }

    private static bool HasDocumentPeriod(InvoiceType invoice)
    {
        return invoice.InvoicePeriod?.Any() == true;
    }

    private static bool ValidateDocumentPeriod(InvoiceType invoice)
    {
        var period = invoice.InvoicePeriod?.FirstOrDefault();
        if (period?.StartDate?.Value == null || period?.EndDate?.Value == null)
            return true;

        if (!DateTime.TryParse(period.StartDate.Value.ToString(), out var startDate) ||
            !DateTime.TryParse(period.EndDate.Value.ToString(), out var endDate))
            return true;

        return endDate >= startDate;
    }

    private static bool HasValidTaxExclusiveAmount(InvoiceType invoice)
    {
        return invoice?.LegalMonetaryTotal?.TaxExclusiveAmount?.Value != null;
    }

    private static bool HasValidTaxInclusiveAmount(InvoiceType invoice)
    {
        return invoice?.LegalMonetaryTotal?.TaxInclusiveAmount?.Value != null;
    }

    private static bool HasValidPayableAmount(InvoiceType invoice)
    {
        return invoice?.LegalMonetaryTotal?.PayableAmount?.Value != null;
    }
}