using FluentAssertions;
using Xunit;

namespace RoEFactura.Tests.Helpers;

public class InvoiceBuilderTests
{
    [Fact]
    public void Build_ReturnsValidBaseInvoice()
    {
        var invoice = InvoiceBuilder.Valid().Build();

        invoice.Should().NotBeNull();
        invoice.ID!.Value.Should().NotBeNullOrWhiteSpace();
        invoice.InvoiceTypeCode!.Value.Should().Be("380");
        invoice.DocumentCurrencyCode!.Value.Should().Be("RON");
        invoice.InvoiceLine.Should().HaveCountGreaterThanOrEqualTo(1);
        invoice.LegalMonetaryTotal.Should().NotBeNull();
    }
}
