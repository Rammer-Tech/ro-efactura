using FluentAssertions;
using RoEFactura.Extensions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Utilities;
using Xunit;

namespace RoEFactura.Tests.Utilities;

public class CreditNoteXmlRewriterTests
{
    [Fact]
    public void IsCreditNoteRoot_DetectsCreditNote2Root()
    {
        var xml = ZipBuilder.LoadFixture("Valid/valid-credit-note-root.xml");
        CreditNoteXmlRewriter.IsCreditNoteRoot(xml).Should().BeTrue();
    }

    [Fact]
    public void IsCreditNoteRoot_RejectsInvoiceRoot()
    {
        var xml = ZipBuilder.LoadFixture("Valid/valid-381-credit-note.xml");
        CreditNoteXmlRewriter.IsCreditNoteRoot(xml).Should().BeFalse();
    }

    [Fact]
    public void RewriteToInvoice_ProducesInvoiceRootWithTypeCode381()
    {
        var creditNoteXml = ZipBuilder.LoadFixture("Valid/valid-credit-note-root.xml");
        var rewritten = CreditNoteXmlRewriter.RewriteToInvoice(creditNoteXml);

        rewritten.Should().Contain("<Invoice");
        rewritten.Should().Contain("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
        rewritten.Should().NotContain("<CreditNote");
        rewritten.Should().Contain("InvoiceTypeCode");
        rewritten.Should().Contain(">381<");
        rewritten.Should().Contain("InvoiceLine");
        rewritten.Should().Contain("InvoicedQuantity");
        rewritten.Should().NotContain("CreditNoteLine");
        rewritten.Should().NotContain("CreditedQuantity");
    }

    [Fact]
    public void LoadInvoiceFromXml_HandlesCreditNoteRoot_TransparentToCaller()
    {
        var xml = ZipBuilder.LoadFixture("Valid/valid-credit-note-root.xml");
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);

        invoice.Should().NotBeNull();
        invoice!.ID!.Value.Should().Be("RO26009900291094");
        invoice.InvoiceTypeCode!.Value.Should().Be("381");
        invoice.IssueDate!.Value.ToString("yyyy-MM-dd").Should().Be("2026-05-09");
        invoice.DocumentCurrencyCode!.Value.Should().Be("RON");
        invoice.GetSellerVatId().Should().Be("RO12345678");
        invoice.GetBuyerVatId().Should().Be("RO42223300");
        invoice.GetBuyerName().Should().Be("RAMMER TECH S.R.L.");
        invoice.GetTotalAmountDue().Should().Be(148.75m);
        invoice.GetTotalVat().Should().Be(23.75m);
    }

    [Fact]
    public void LoadInvoiceFromXml_StillHandlesInvoiceRoot_Unchanged()
    {
        var xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);

        invoice.Should().NotBeNull();
        invoice!.InvoiceTypeCode!.Value.Should().Be("380");
    }
}
