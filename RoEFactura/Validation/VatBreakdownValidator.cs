using FluentValidation;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation;

/// <summary>
/// BR-E-10 / BR-AE-10 / BR-IC-10 / BR-G-10 / BR-O-10: a VAT breakdown (BG-23) whose category is
/// Exempt from VAT (E), Reverse charge (AE), Intra-community supply (K), Export outside the EU (G)
/// or Not subject to VAT (O) must carry a VAT exemption reason code (BT-121) and/or a VAT exemption
/// reason text (BT-120). Wired by <see cref="RoCiusUblValidator"/> against the document-currency
/// TaxTotal's subtotals (<see cref="TotalsValidator.GetDocumentCurrencyTaxTotal"/>).
/// </summary>
public class VatBreakdownValidator : AbstractValidator<TaxSubtotalType>
{
    public VatBreakdownValidator()
    {
        AddExemptionReasonRule("E", "BR-E-10");
        AddExemptionReasonRule("AE", "BR-AE-10");
        AddExemptionReasonRule("K", "BR-IC-10");
        AddExemptionReasonRule("G", "BR-G-10");
        AddExemptionReasonRule("O", "BR-O-10");
    }

    private void AddExemptionReasonRule(string categoryId, string errorCode)
    {
        RuleFor(x => x)
            .Must(HasExemptionReason)
            .When(x => IsCategory(x, categoryId))
            .WithErrorCode(errorCode)
            .WithMessage("A VAT breakdown with this VAT category code must have a VAT exemption reason code and/or text.");
    }

    private static bool IsCategory(TaxSubtotalType subtotal, string categoryId)
    {
        return string.Equals(subtotal?.TaxCategory?.ID?.Value, categoryId, StringComparison.Ordinal);
    }

    private static bool HasExemptionReason(TaxSubtotalType subtotal)
    {
        string? reasonCode = subtotal?.TaxCategory?.TaxExemptionReasonCode?.Value;
        bool hasReasonText = subtotal?.TaxCategory?.TaxExemptionReason?
            .Any(reason => !string.IsNullOrWhiteSpace(reason?.Value)) == true;
        return !string.IsNullOrWhiteSpace(reasonCode) || hasReasonText;
    }
}
