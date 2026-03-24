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

    // ── GetTotalWithoutVat / GetTotalWithVat / GetTotalVat ───────────────────

    [Fact]
    public void GetTotalWithoutVat_ReturnsTaxExclusiveAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTotalWithoutVat().Should().Be(100m);
    }

    [Fact]
    public void GetTotalWithVat_ReturnsTaxInclusiveAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTotalWithVat().Should().Be(119m);
    }

    [Fact]
    public void GetTotalVat_ReturnsTaxTotalAmount()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.GetTotalVat().Should().Be(19m);
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
}
