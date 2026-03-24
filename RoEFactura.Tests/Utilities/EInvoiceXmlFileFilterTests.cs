using FluentAssertions;
using RoEFactura.Utilities;
using Xunit;

namespace RoEFactura.Tests.Utilities;

public class EInvoiceXmlFileFilterTests
{
    [Theory]
    [InlineData("semnatura.xml", true)]
    [InlineData("SEMNATURA.xml", true)]
    [InlineData("12345_semnatura_factura.xml", true)]
    [InlineData("sEMnAtUrA.xml", true)]
    [InlineData("path/to/semnatura.xml", true)]
    [InlineData(@"C:\invoices\semnatura.xml", true)]
    public void IsSemnaturaXmlFileName_ReturnsTrue_ForSignatureSidecars(string path, bool expected)
    {
        EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("invoice.xml", false)]
    [InlineData("factura1.xml", false)]
    [InlineData("12345678.xml", false)]
    [InlineData("path/to/invoice.xml", false)]
    [InlineData("", false)]
    public void IsSemnaturaXmlFileName_ReturnsFalse_ForInvoiceFiles(string path, bool expected)
    {
        EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(path).Should().Be(expected);
    }

    [Fact]
    public void IsSemnaturaXmlFileName_ReturnsFalse_ForNull()
    {
        EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(null).Should().BeFalse();
    }
}
