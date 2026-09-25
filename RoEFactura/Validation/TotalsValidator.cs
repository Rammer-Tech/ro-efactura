using FluentValidation;
using UblSharp;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation;

public class TotalsValidator : AbstractValidator<InvoiceType>
{
    public TotalsValidator()
    {
        // BR-12: An Invoice shall have the Sum of Invoice line net amount (BT-106).
        RuleFor(x => x)
            .Must(HasValidLineExtensionAmount)
            .WithErrorCode("BR-12")
            .WithMessage("Sum of invoice line net amount is required.");

        // BR-13: An Invoice shall have the Invoice total amount without VAT (BT-109).
        RuleFor(x => x)
            .Must(HasValidTaxExclusiveAmount)
            .WithErrorCode("BR-13")
            .WithMessage("Invoice total amount without VAT is required.");

        // BR-14: An Invoice shall have the Invoice total amount with VAT (BT-112).
        RuleFor(x => x)
            .Must(HasValidTaxInclusiveAmount)
            .WithErrorCode("BR-14")
            .WithMessage("Invoice total amount with VAT is required.");

        // BR-15: An Invoice shall have the Amount due for payment (BT-115).
        RuleFor(x => x)
            .Must(HasValidPayableAmount)
            .WithErrorCode("BR-15")
            .WithMessage("Amount due for payment is required.");

        // BR-CO-10: Sum of Invoice line net amount (BT-106) = Σ Invoice line net amount (BT-131).
        RuleFor(x => x)
            .Must(ValidateLineNetAmountSum)
            .WithErrorCode("BR-CO-10")
            .WithMessage("Sum of invoice line net amount must equal the sum of the invoice lines' net amounts (within 0.01 tolerance).");

        // BR-CO-11: Sum of allowances on document level (BT-107) = Σ Document level allowance amount (BT-92).
        RuleFor(x => x)
            .Must(ValidateAllowanceTotal)
            .WithErrorCode("BR-CO-11")
            .WithMessage("Sum of allowances on document level must equal the sum of the document level allowance amounts (within 0.01 tolerance).");

        // BR-CO-12: Sum of charges on document level (BT-108) = Σ Document level charge amount (BT-99).
        RuleFor(x => x)
            .Must(ValidateChargeTotal)
            .WithErrorCode("BR-CO-12")
            .WithMessage("Sum of charges on document level must equal the sum of the document level charge amounts (within 0.01 tolerance).");

        // BR-CO-13: Invoice total amount without VAT (BT-109) = Σ Invoice line net amount (BT-131)
        //           - Sum of allowances on document level (BT-107) + Sum of charges on document level (BT-108).
        RuleFor(x => x)
            .Must(ValidateTaxExclusiveAmount)
            .WithErrorCode("BR-CO-13")
            .WithMessage("Invoice total amount without VAT must equal the line net amount minus allowances plus charges (within 0.01 tolerance).");

        // BR-CO-14: Invoice total VAT amount (BT-110) = Σ VAT category tax amount (BT-117).
        RuleFor(x => x)
            .Must(ValidateDocumentTaxTotalMatchesSubtotals)
            .WithErrorCode("BR-CO-14")
            .WithMessage("Invoice total VAT amount must equal the sum of the VAT breakdown amounts (within 0.01 tolerance).");

        // BR-CO-15: Invoice total amount with VAT (BT-112) = Invoice total amount without VAT (BT-109)
        //           + Invoice total VAT amount (BT-110).
        RuleFor(x => x)
            .Must(ValidateTaxInclusiveAmount)
            .WithErrorCode("BR-CO-15")
            .WithMessage("Invoice total amount with VAT must equal total without VAT plus VAT total amount (within 0.01 tolerance).");

        // BR-CO-16: Amount due for payment (BT-115) = Invoice total amount with VAT (BT-112)
        //           - Paid amount (BT-113) + Rounding amount (BT-114).
        RuleFor(x => x)
            .Must(ValidatePayableAmount)
            .WithErrorCode("BR-CO-16")
            .WithMessage("Amount due for payment must equal total with VAT minus paid amount plus rounding amount (within 0.01 tolerance).");

        // BR-CO-17: VAT category tax amount (BT-117) = VAT category taxable amount (BT-116)
        //           x (VAT category rate (BT-119) / 100), rounded to two decimals -- evaluated per subtotal
        //           of the document-currency TaxTotal, with the official ANAF tolerance window (see
        //           ValidateSubtotalTaxAmount), not the 0.01 tolerance used elsewhere in this validator.
        RuleForEach(x => GetDocumentCurrencySubtotals(x))
            .Must(ValidateSubtotalTaxAmount)
            .WithErrorCode("BR-CO-17")
            .WithMessage("VAT category tax amount must equal the taxable amount multiplied by the VAT rate (within the ANAF-aligned tolerance).")
            .OverridePropertyName("TaxTotal.TaxSubtotal");

        // Document period validation (library-local, not a CIUS-RO/EN16931 rule id).
        RuleFor(x => x)
            .Must(ValidateDocumentPeriod)
            .When(x => HasDocumentPeriod(x))
            .WithErrorCode("BR-29")
            .WithMessage("Invoice period end date must be greater than or equal to start date.");
    }

    /// <summary>
    /// The <see cref="TaxTotalType"/> whose TaxAmount currency matches BT-5 (DocumentCurrencyCode) -- the VAT
    /// breakdown BR-CO-14..17 operate on. An invoice may carry a second <see cref="TaxTotalType"/> in the VAT
    /// accounting currency (BT-6/BT-111, present when BT-5 is not RON); that one is not evaluated by these rules.
    /// </summary>
    internal static TaxTotalType? GetDocumentCurrencyTaxTotal(InvoiceType invoice)
    {
        string? documentCurrency = invoice?.DocumentCurrencyCode?.Value;
        if (invoice?.TaxTotal == null || string.IsNullOrEmpty(documentCurrency))
            return null;

        return invoice.TaxTotal.FirstOrDefault(taxTotal =>
            !string.IsNullOrEmpty(taxTotal?.TaxAmount?.currencyID)
            && string.Equals(taxTotal!.TaxAmount!.currencyID, documentCurrency, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Non-expression-tree helper for <see cref="GetDocumentCurrencyTaxTotal"/>'s subtotals: FluentValidation's
    /// <c>RuleForEach</c> compiles its selector to an <see cref="System.Linq.Expressions.Expression"/>, and C#
    /// forbids null-propagating operators (<c>?.</c>/<c>??</c>) directly inside an expression tree lambda (CS8072).
    /// </summary>
    private static List<TaxSubtotalType> GetDocumentCurrencySubtotals(InvoiceType invoice)
    {
        return GetDocumentCurrencyTaxTotal(invoice)?.TaxSubtotal ?? new List<TaxSubtotalType>();
    }

    private static bool HasValidLineExtensionAmount(InvoiceType invoice)
    {
        // UblSharp AmountType.Value is non-nullable decimal; presence is indicated by currencyID.
        return !string.IsNullOrEmpty(invoice?.LegalMonetaryTotal?.LineExtensionAmount?.currencyID);
    }

    private static bool HasValidTaxExclusiveAmount(InvoiceType invoice)
    {
        return !string.IsNullOrEmpty(invoice?.LegalMonetaryTotal?.TaxExclusiveAmount?.currencyID);
    }

    private static bool HasValidTaxInclusiveAmount(InvoiceType invoice)
    {
        return !string.IsNullOrEmpty(invoice?.LegalMonetaryTotal?.TaxInclusiveAmount?.currencyID);
    }

    private static bool HasValidPayableAmount(InvoiceType invoice)
    {
        return !string.IsNullOrEmpty(invoice?.LegalMonetaryTotal?.PayableAmount?.currencyID);
    }

    private static bool ValidateLineNetAmountSum(InvoiceType invoice)
    {
        if (invoice.InvoiceLine == null || string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.LineExtensionAmount?.currencyID))
            return true;

        decimal lineSum = invoice.InvoiceLine.Sum(line => line.LineExtensionAmount?.Value ?? 0m);
        decimal docTotal = invoice.LegalMonetaryTotal!.LineExtensionAmount!.Value;

        return Math.Abs(lineSum - docTotal) <= 0.01m;
    }

    private static decimal SumDocumentAllowanceCharges(InvoiceType invoice, bool chargeIndicator)
    {
        return invoice.AllowanceCharge?
            .Where(allowanceCharge => allowanceCharge?.ChargeIndicator?.Value == chargeIndicator)
            .Sum(allowanceCharge => allowanceCharge.Amount?.Value ?? 0m) ?? 0m;
    }

    private static bool ValidateAllowanceTotal(InvoiceType invoice)
    {
        bool hasAllowanceTotal = !string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.AllowanceTotalAmount?.currencyID);
        bool hasDocumentAllowances = invoice.AllowanceCharge?.Any(ac => ac?.ChargeIndicator?.Value == false) == true;

        if (hasAllowanceTotal)
        {
            decimal allowanceTotal = invoice.LegalMonetaryTotal!.AllowanceTotalAmount!.Value;
            decimal sumAllowances = SumDocumentAllowanceCharges(invoice, chargeIndicator: false);
            return Math.Abs(allowanceTotal - sumAllowances) <= 0.01m;
        }

        // No AllowanceTotalAmount at all: the rule only fails if document-level allowances exist without it.
        return !hasDocumentAllowances;
    }

    private static bool ValidateChargeTotal(InvoiceType invoice)
    {
        bool hasChargeTotal = !string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.ChargeTotalAmount?.currencyID);
        bool hasDocumentCharges = invoice.AllowanceCharge?.Any(ac => ac?.ChargeIndicator?.Value == true) == true;

        if (hasChargeTotal)
        {
            decimal chargeTotal = invoice.LegalMonetaryTotal!.ChargeTotalAmount!.Value;
            decimal sumCharges = SumDocumentAllowanceCharges(invoice, chargeIndicator: true);
            return Math.Abs(chargeTotal - sumCharges) <= 0.01m;
        }

        return !hasDocumentCharges;
    }

    private static bool ValidateTaxExclusiveAmount(InvoiceType invoice)
    {
        if (string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.LineExtensionAmount?.currencyID)
            || string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.TaxExclusiveAmount?.currencyID))
            return true;

        decimal lineExtension = invoice.LegalMonetaryTotal!.LineExtensionAmount!.Value;
        decimal taxExclusive = invoice.LegalMonetaryTotal!.TaxExclusiveAmount!.Value;
        // Absent AllowanceTotalAmount/ChargeTotalAmount count as 0 in the BR-CO-13 formula.
        decimal allowanceTotal = !string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.AllowanceTotalAmount?.currencyID)
            ? invoice.LegalMonetaryTotal!.AllowanceTotalAmount!.Value
            : 0m;
        decimal chargeTotal = !string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.ChargeTotalAmount?.currencyID)
            ? invoice.LegalMonetaryTotal!.ChargeTotalAmount!.Value
            : 0m;

        decimal expected = lineExtension - allowanceTotal + chargeTotal;
        return Math.Abs(taxExclusive - expected) <= 0.01m;
    }

    private static bool ValidateDocumentTaxTotalMatchesSubtotals(InvoiceType invoice)
    {
        TaxTotalType? docTaxTotal = GetDocumentCurrencyTaxTotal(invoice);
        if (docTaxTotal == null || string.IsNullOrEmpty(docTaxTotal.TaxAmount?.currencyID))
            return true;

        decimal invoiceVatTotal = docTaxTotal.TaxAmount!.Value;
        decimal subtotalSum = docTaxTotal.TaxSubtotal?.Sum(subtotal => subtotal.TaxAmount?.Value ?? 0m) ?? 0m;

        return Math.Abs(invoiceVatTotal - subtotalSum) <= 0.01m;
    }

    private static bool ValidateTaxInclusiveAmount(InvoiceType invoice)
    {
        if (string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.TaxExclusiveAmount?.currencyID)
            || string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.TaxInclusiveAmount?.currencyID))
            return true;

        decimal taxExclusive = invoice.LegalMonetaryTotal!.TaxExclusiveAmount!.Value;
        decimal taxInclusive = invoice.LegalMonetaryTotal!.TaxInclusiveAmount!.Value;
        TaxTotalType? docTaxTotal = GetDocumentCurrencyTaxTotal(invoice);
        decimal vatTotal = !string.IsNullOrEmpty(docTaxTotal?.TaxAmount?.currencyID) ? docTaxTotal!.TaxAmount!.Value : 0m;

        decimal expected = taxExclusive + vatTotal;
        return Math.Abs(taxInclusive - expected) <= 0.01m;
    }

    private static bool ValidatePayableAmount(InvoiceType invoice)
    {
        if (string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.TaxInclusiveAmount?.currencyID)
            || string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.PayableAmount?.currencyID))
            return true;

        decimal taxInclusive = invoice.LegalMonetaryTotal!.TaxInclusiveAmount!.Value;
        decimal payable = invoice.LegalMonetaryTotal!.PayableAmount!.Value;
        decimal prepaid = !string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.PrepaidAmount?.currencyID)
            ? invoice.LegalMonetaryTotal!.PrepaidAmount!.Value
            : 0m;
        decimal rounding = !string.IsNullOrEmpty(invoice.LegalMonetaryTotal?.PayableRoundingAmount?.currencyID)
            ? invoice.LegalMonetaryTotal!.PayableRoundingAmount!.Value
            : 0m;

        decimal expected = taxInclusive - prepaid + rounding;
        return Math.Abs(payable - expected) <= 0.01m;
    }

    /// <summary>
    /// BR-CO-17, translated from the official tolerance window in EN16931-UBL-model.sch's "BR-CO-17" param
    /// (not the 0.01 tolerance used by the other BR-CO-* rules in this class): when the VAT rate rounds to
    /// zero, or is absent, the tax amount must round to zero (to the nearest whole unit); otherwise the tax
    /// amount must fall within one whole currency unit of taxable amount × rate / 100 (rounded to 2 decimals).
    /// This is deliberately wider than 0.01 so the local pre-check never rejects a document ANAF would accept
    /// (orchestrator decision, E1 2026-09-25).
    /// </summary>
    private static bool ValidateSubtotalTaxAmount(TaxSubtotalType subtotal)
    {
        if (subtotal == null
            || string.IsNullOrEmpty(subtotal.TaxableAmount?.currencyID)
            || string.IsNullOrEmpty(subtotal.TaxAmount?.currencyID))
            return true;

        decimal taxableAmount = Math.Abs(subtotal.TaxableAmount!.Value);
        decimal taxAmount = Math.Abs(subtotal.TaxAmount!.Value);
        decimal? percent = subtotal.TaxCategory?.Percent?.Value;

        bool zeroRatedOrAbsent = percent == null || Math.Round(percent.Value, 0, MidpointRounding.AwayFromZero) == 0m;
        if (zeroRatedOrAbsent)
            return Math.Round(taxAmount, 0, MidpointRounding.AwayFromZero) == 0m;

        decimal expected = Math.Round(taxableAmount * percent!.Value / 100m, 2, MidpointRounding.AwayFromZero);
        return Math.Abs(taxAmount - expected) < 1m;
    }

    private static bool HasDocumentPeriod(InvoiceType invoice)
    {
        return invoice.InvoicePeriod?.Any() == true;
    }

    private static bool ValidateDocumentPeriod(InvoiceType invoice)
    {
        PeriodType? period = invoice.InvoicePeriod?.FirstOrDefault();
        if (period?.StartDate?.Value == null || period?.EndDate?.Value == null)
            return true;

        if (!DateTime.TryParse(period.StartDate.Value.ToString(), out DateTime startDate) ||
            !DateTime.TryParse(period.EndDate.Value.ToString(), out DateTime endDate))
            return true;

        return endDate >= startDate;
    }
}
