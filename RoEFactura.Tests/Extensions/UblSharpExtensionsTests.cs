using FluentAssertions;
using RoEFactura.Extensions;
using RoEFactura.Tests.Helpers;
using Xunit;

namespace RoEFactura.Tests.Extensions;

public class UblSharpExtensionsTests
{
    [Fact]
    public void LoadInvoiceFromXml_WithNull_ReturnsNull()
    {
        UblSharpExtensions.LoadInvoiceFromXml(null!).Should().BeNull();
    }

    [Fact]
    public void LoadInvoiceFromXml_WithEmptyString_ReturnsNull()
    {
        UblSharpExtensions.LoadInvoiceFromXml("   ").Should().BeNull();
    }

    [Fact]
    public void LoadInvoiceFromXml_WithMalformedXml_Throws()
    {
        var act = () => UblSharpExtensions.LoadInvoiceFromXml("<not-valid-ubl><unclosed>");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void LoadInvoiceFromXml_WithValidFixture_ParsesCorrectly()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);

        invoice.Should().NotBeNull();
        invoice!.ID!.Value.Should().Be("INV-2024-001");
        invoice.InvoiceTypeCode!.Value.Should().Be("380");
        invoice.DocumentCurrencyCode!.Value.Should().Be("RON");
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_PreservesInvoiceId()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var original = UblSharpExtensions.LoadInvoiceFromXml(xml)!;

        string reserialized = original.SaveInvoiceToXml();
        var roundTripped = UblSharpExtensions.LoadInvoiceFromXml(reserialized);

        roundTripped.Should().NotBeNull();
        roundTripped!.ID!.Value.Should().Be(original.ID!.Value);
        roundTripped.InvoiceTypeCode!.Value.Should().Be(original.InvoiceTypeCode!.Value);
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_PreservesLineCount()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var original = UblSharpExtensions.LoadInvoiceFromXml(xml)!;

        var roundTripped = UblSharpExtensions.LoadInvoiceFromXml(original.SaveInvoiceToXml())!;

        roundTripped.InvoiceLine.Should().HaveCount(original.InvoiceLine.Count);
    }

    [Fact]
    public void SaveInvoiceToXml_WithNull_ThrowsArgumentNullException()
    {
        var act = () => ((UblSharp.InvoiceType)null!).SaveInvoiceToXml();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SaveInvoiceToXml_DeclaresUtf8Encoding()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml)!;

        string serialized = invoice.SaveInvoiceToXml();

        serialized.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"");
    }

    [Fact]
    public void SaveInvoiceToXmlBytes_WithNull_ThrowsArgumentNullException()
    {
        var act = () => ((UblSharp.InvoiceType)null!).SaveInvoiceToXmlBytes();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SaveInvoiceToXmlBytes_IsUtf8WithoutBom()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml)!;

        byte[] bytes = invoice.SaveInvoiceToXmlBytes();

        // UTF-8 BOM is EF BB BF; the first bytes of a BOM-less UTF-8 XML document are "<?xm".
        bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
        System.Text.Encoding.UTF8.GetString(bytes, 0, 5).Should().Be("<?xml");
    }

    [Fact]
    public void SaveInvoiceToXmlBytes_PreservesRomanianDiacritics()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.InvoiceLine[0].Item!.Name = new UblSharp.UnqualifiedDataTypes.NameType
        {
            Value = "Servicii de consultanță și mentenanță"
        };

        byte[] bytes = invoice.SaveInvoiceToXmlBytes();
        string xml = System.Text.Encoding.UTF8.GetString(bytes);
        var roundTripped = UblSharpExtensions.LoadInvoiceFromXml(xml)!;

        roundTripped.InvoiceLine[0].Item!.Name!.Value.Should().Be("Servicii de consultanță și mentenanță");
    }

    [Theory]
    [InlineData("Valid/valid-381-credit-note.xml", "381")]
    [InlineData("Valid/valid-389-self-billing.xml", "389")]
    [InlineData("Valid/valid-384-corrective.xml", "384")]
    [InlineData("Valid/valid-751-activity.xml", "751")]
    public void LoadInvoiceFromXml_ParsesAllInvoiceTypes(string fixturePath, string expectedTypeCode)
    {
        string xml = ZipBuilder.LoadFixture(fixturePath);
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);

        invoice.Should().NotBeNull();
        invoice!.InvoiceTypeCode!.Value.Should().Be(expectedTypeCode);
    }
}
