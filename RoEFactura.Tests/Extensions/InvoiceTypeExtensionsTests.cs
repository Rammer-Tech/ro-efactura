using FluentAssertions;
using RoEFactura.Extensions;
using RoEFactura.Tests.Helpers;
using UblSharp;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Extensions;

public class InvoiceTypeExtensionsTests
{
    // ── IsRomanianInvoice ────────────────────────────────────────────────────

    [Fact]
    public void IsRomanianInvoice_WithRoCiusCustomizationId_ReturnsTrue()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.IsRomanianInvoice().Should().BeTrue();
    }

    [Fact]
    public void IsRomanianInvoice_WithRoSellerCountry_ReturnsTrue()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCustomizationId().Build();
        invoice.IsRomanianInvoice().Should().BeTrue(); // seller address is RO
    }

    [Fact]
    public void IsRomanianInvoice_WhenNull_ReturnsFalse()
    {
        ((InvoiceType)null!).IsRomanianInvoice().Should().BeFalse();
    }

    [Fact]
    public void IsRomanianInvoice_WithForeignInvoice_ReturnsFalse()
    {
        var invoice = new InvoiceType
        {
            CustomizationID = new IdentifierType { Value = "urn:cen.eu:en16931:2017" }
        };
        invoice.IsRomanianInvoice().Should().BeFalse();
    }

    // ── GetCurrencyCode ──────────────────────────────────────────────────────

    [Fact]
    public void GetCurrencyCode_ReturnsDocumentCurrencyCode()
    {
        var invoice = InvoiceBuilder.Valid().WithCurrency("EUR").Build();
        invoice.GetCurrencyCode().Should().Be("EUR");
    }

    [Fact]
    public void GetCurrencyCode_WhenMissing_ReturnsEmptyString()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCurrency().Build();
        invoice.GetCurrencyCode().Should().BeEmpty();
    }

    // ── GetTotalAmountDue ────────────────────────────────────────────────────

    [Fact]
    public void GetTotalAmountDue_ReturnPayableAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTotalAmountDue().Should().Be(119m);
    }

    [Fact]
    public void GetTotalAmountDue_WhenNoTotals_ReturnsZero()
    {
        var invoice = new InvoiceType();
        invoice.GetTotalAmountDue().Should().Be(0m);
    }

    // ── GetTaxInclusiveAmount / GetTaxExclusiveAmount / GetTotalVat ──────────

    [Fact]
    public void GetTaxExclusiveAmount_ReturnsTaxExclusiveAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTaxExclusiveAmount().Should().Be(100m);
        invoice.GetTotalWithoutVat().Should().Be(100m);
    }

    [Fact]
    public void GetTaxInclusiveAmount_ReturnsTaxInclusiveAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTaxInclusiveAmount().Should().Be(119m);
        invoice.GetTotalWithVat().Should().Be(119m);
    }

    [Fact]
    public void GetTotalVat_ReturnsTaxTotalAmount()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.GetTotalVat().Should().Be(19m);
    }

    [Fact]
    public void GetTaxInclusiveAmount_WhenFullyPaid_PayableZero_ReturnsGrossNotPayable()
    {
        // PayableAmount=0 (fully prepaid) but TaxInclusive reflects the real invoice value.
        var invoice = InvoiceBuilder.Valid()
            .WithTotals(17438.02m, 21100m, 0m)
            .WithPrepaidAmount(21100m)
            .WithTaxTotalAmount(3661.98m)
            .Build();

        invoice.GetTotalAmountDue().Should().Be(0m);
        invoice.GetTaxInclusiveAmount().Should().Be(21100m);
        invoice.GetTaxExclusiveAmount().Should().Be(17438.02m);
        invoice.GetTotalVat().Should().Be(3661.98m);
    }

    [Fact]
    public void GetTaxInclusiveAmount_WhenPartiallyPrepaid_ReturnsFullGrossRegardlessOfPayable()
    {
        // Vodafone-like: partial prepayment reduces PayableAmount but not TaxInclusive.
        var invoice = InvoiceBuilder.Valid()
            .WithTotals(757.45m, 916.51m, 813.25m)
            .WithPrepaidAmount(103.26m)
            .WithTaxTotalAmount(159.06m)
            .Build();

        invoice.GetTotalAmountDue().Should().Be(813.25m);
        invoice.GetTaxInclusiveAmount().Should().Be(916.51m);
        invoice.GetTaxExclusiveAmount().Should().Be(757.45m);
        invoice.GetTotalVat().Should().Be(159.06m);
    }

    [Fact]
    public void GetTaxInclusiveAmount_WhenUnpaid_MatchesPayableAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();

        invoice.GetTaxInclusiveAmount().Should().Be(invoice.GetTotalAmountDue());
    }

    // ── GetValidationSummary ─────────────────────────────────────────────────

    [Fact]
    public void GetValidationSummary_ForValidInvoice_ReturnsValid()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.GetValidationSummary().Should().Be("Valid");
    }

    [Fact]
    public void GetValidationSummary_ForNullInvoice_ReturnsInvalidMessage()
    {
        ((InvoiceType)null!).GetValidationSummary().Should().Be("Invalid invoice");
    }

    [Fact]
    public void GetValidationSummary_MissingId_ContainsMissingInvoiceNumber()
    {
        var invoice = InvoiceBuilder.Valid().WithoutId().Build();
        invoice.GetValidationSummary().Should().Contain("Missing invoice number");
    }

    [Fact]
    public void GetValidationSummary_MissingLines_ContainsMissingLines()
    {
        var invoice = InvoiceBuilder.Valid().WithoutLines().Build();
        invoice.GetValidationSummary().Should().Contain("Missing invoice lines");
    }

    [Fact]
    public void GetValidationSummary_RomanianInvoiceMissingRoCiusId_ContainsRoCiusMessage()
    {
        // Seller is RO → still considered Romanian, but customization ID is wrong
        var invoice = InvoiceBuilder.Valid().WithCustomizationId("wrong").Build();
        invoice.GetValidationSummary().Should().Contain("RO_CIUS");
    }

    // ── GetPrecedingInvoiceId / GetPrecedingInvoiceIssueDate ─────────────────

    [Fact]
    public void GetPrecedingInvoiceId_WithBillingReference_ReturnsReferencedInvoiceId()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithBillingReference("ORIG-2024-001", new DateTime(2024, 6, 15))
            .Build();

        invoice.GetPrecedingInvoiceId().Should().Be("ORIG-2024-001");
    }

    [Fact]
    public void GetPrecedingInvoiceIssueDate_WithBillingReference_ReturnsReferencedIssueDate()
    {
        var issueDate = new DateTime(2024, 6, 15);
        var invoice = InvoiceBuilder.Valid()
            .WithBillingReference("ORIG-2024-001", issueDate)
            .Build();

        invoice.GetPrecedingInvoiceIssueDate().Should().Be(new DateTimeOffset(issueDate));
    }

    [Fact]
    public void GetPrecedingInvoiceId_WithoutBillingReference_ReturnsNull()
    {
        var invoice = InvoiceBuilder.Valid().Build();

        invoice.GetPrecedingInvoiceId().Should().BeNull();
        invoice.GetPrecedingInvoiceIssueDate().Should().BeNull();
    }

    [Fact]
    public void GetPrecedingInvoiceIssueDate_WhenIssueDateIsMinValue_ReturnsNull()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithBillingReference("ORIG-2024-001")
            .Build();
        invoice.BillingReference![0].InvoiceDocumentReference!.IssueDate =
            new DateType { Value = default };

        invoice.GetPrecedingInvoiceIssueDate().Should().BeNull();
    }
}
