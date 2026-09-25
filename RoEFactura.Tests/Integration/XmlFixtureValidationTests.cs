using System.Linq;
using FluentAssertions;
using RoEFactura.Extensions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using Xunit;

namespace RoEFactura.Tests.Integration;

/// <summary>
/// End-to-end tests: load XML fixture → deserialize → run full validator → assert outcome.
/// These verify the fixture files themselves are valid/invalid as intended.
/// </summary>
public class XmlFixtureValidationTests
{
    private readonly RoCiusUblValidator _validator = new();

    private UblSharp.InvoiceType LoadAndParse(string relativePath)
    {
        string xml = ZipBuilder.LoadFixture(relativePath);
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);
        invoice.Should().NotBeNull($"Fixture {relativePath} should parse successfully");
        return invoice!;
    }

    // ── Valid fixtures: all should pass ──────────────────────────────────────

    [Theory]
    [InlineData("Valid/valid-380-ron.xml")]
    [InlineData("Valid/valid-381-credit-note.xml")]
    [InlineData("Valid/valid-389-self-billing.xml")]
    [InlineData("Valid/valid-384-corrective.xml")]
    [InlineData("Valid/valid-751-activity.xml")]
    [InlineData("Valid/valid-eur-with-ron-vat.xml")]
    [InlineData("Valid/valid-bucharest-sector3.xml")]
    [InlineData("Valid/valid-credit-note-root.xml")]
    [InlineData("Valid/valid-cius-ro-101-full.xml")]
    [InlineData("Valid/valid-380-storno.xml")]
    public void ValidFixture_PassesFullValidation(string fixturePath)
    {
        var invoice = LoadAndParse(fixturePath);
        var result = _validator.Validate(invoice);

        result.IsValid.Should().BeTrue(
            $"Fixture '{fixturePath}' should be valid but got errors: " +
            string.Join(", ", result.Errors.Select(e => $"{e.ErrorCode}: {e.ErrorMessage}")));
    }

    // ── Invalid fixtures: each should fail with expected error code ──────────

    [Theory]
    [InlineData("Invalid/invalid-br-ro-001.xml", "BR-RO-001")]
    [InlineData("Invalid/invalid-br-ro-010.xml", "BR-RO-010")]
    [InlineData("Invalid/invalid-br-ro-020.xml", "BR-RO-020")]
    [InlineData("Invalid/invalid-br-ro-030.xml", "BR-RO-030")]
    [InlineData("Invalid/invalid-br-ro-120.xml", "BR-RO-120")]
    [InlineData("Invalid/invalid-br-16-no-lines.xml", "BR-16")]
    [InlineData("Invalid/invalid-br-co-10.xml", "BR-CO-10")]
    [InlineData("Invalid/invalid-br-co-15.xml", "BR-CO-15")]
    [InlineData("Invalid/invalid-br-13.xml", "BR-13")]
    [InlineData("Invalid/invalid-br-12.xml", "BR-12")]
    [InlineData("Invalid/invalid-br-2.xml", "BR-2")]
    [InlineData("Invalid/invalid-br-ro-110.xml", "BR-RO-110")]
    [InlineData("Invalid/invalid-br-ro-100.xml", "BR-RO-100")]
    public void InvalidFixture_FailsWithExpectedErrorCode(string fixturePath, string expectedErrorCode)
    {
        var invoice = LoadAndParse(fixturePath);
        var result = _validator.Validate(invoice);

        result.IsValid.Should().BeFalse($"Fixture '{fixturePath}' should fail validation");
        result.Errors.Should().Contain(e => e.ErrorCode == expectedErrorCode,
            $"Expected error code '{expectedErrorCode}' in fixture '{fixturePath}' " +
            $"but got: {string.Join(", ", result.Errors.Select(e => e.ErrorCode))}");
    }

    // ── EUR fixture: specific assertions ────────────────────────────────────

    [Fact]
    public void EurFixture_HasRONTaxCurrencyCode()
    {
        var invoice = LoadAndParse("Valid/valid-eur-with-ron-vat.xml");
        invoice.DocumentCurrencyCode!.Value.Should().Be("EUR");
        invoice.TaxCurrencyCode!.Value.Should().Be("RON");
    }

    // ── Bucharest fixture: sector city name ──────────────────────────────────

    [Fact]
    public void BucharestFixture_HasSectorCityName()
    {
        var invoice = LoadAndParse("Valid/valid-bucharest-sector3.xml");
        var buyerCity = invoice.AccountingCustomerParty!.Party!.PostalAddress!.CityName!.Value;
        buyerCity.Should().Be("SECTOR3");
    }

    // ── All invoice type codes appear in their respective fixtures ───────────

    [Theory]
    [InlineData("Valid/valid-380-ron.xml", "380")]
    [InlineData("Valid/valid-381-credit-note.xml", "381")]
    [InlineData("Valid/valid-389-self-billing.xml", "389")]
    [InlineData("Valid/valid-384-corrective.xml", "384")]
    [InlineData("Valid/valid-751-activity.xml", "751")]
    public void FixtureTypeCode_MatchesExpected(string fixturePath, string expectedCode)
    {
        var invoice = LoadAndParse(fixturePath);
        invoice.InvoiceTypeCode!.Value.Should().Be(expectedCode);
    }

    // ── CIUS-RO 1.0.1 real-shaped fixture (manifest item 9/10) ───────────────

    [Fact]
    public void CiusRo101RealShapedFixture_PassesFullValidation()
    {
        var invoice = LoadAndParse("Valid/valid-cius-ro-101-full.xml");
        var result = _validator.Validate(invoice);

        result.IsValid.Should().BeTrue(
            "the fixture should be a fully valid CIUS-RO 1.0.1 document but got errors: " +
            string.Join(", ", result.Errors.Select(e => $"{e.ErrorCode}: {e.ErrorMessage}")));
    }

    [Fact]
    public void CiusRo101RealShapedFixture_ParsesKeyFields()
    {
        var invoice = LoadAndParse("Valid/valid-cius-ro-101-full.xml");

        invoice.DueDate!.Value.Should().Be(new DateTimeOffset(2026, 10, 15, 0, 0, 0, invoice.DueDate.Value.Offset));
        invoice.Note.Should().ContainSingle();
        invoice.PaymentMeans.Should().ContainSingle();
        invoice.PaymentMeans[0].PaymentMeansCode!.Value.Should().Be("30");
        invoice.InvoiceLine.Should().HaveCount(2);
        invoice.AccountingCustomerParty!.Party!.PostalAddress!.CityName!.Value.Should().Be("SECTOR3");
    }

    // ── Storno fixture: negative quantities + BillingReference ───────────────

    [Fact]
    public void StornoFixture_NegativeQuantitiesWithBillingReference_PassesFullValidation()
    {
        var invoice = LoadAndParse("Valid/valid-380-storno.xml");
        var result = _validator.Validate(invoice);

        result.IsValid.Should().BeTrue(
            "a negative-quantity storno referencing a preceding invoice should validate but got errors: " +
            string.Join(", ", result.Errors.Select(e => $"{e.ErrorCode}: {e.ErrorMessage}")));
        invoice.GetPrecedingInvoiceId().Should().Be("MTX0001");
        invoice.InvoiceLine[0].InvoicedQuantity!.Value.Should().Be(-3m);
    }

    // ── Address fixtures: county/sector failures ─────────────────────────────

    [Fact]
    public void CountyWithoutRoPrefixFixture_FailsBrRo110()
    {
        var invoice = LoadAndParse("Invalid/invalid-br-ro-110.xml");
        var result = _validator.Validate(invoice);

        result.Errors.Should().Contain(e => e.ErrorCode == "BR-RO-110");
    }

    [Fact]
    public void BucharestNonSectorCityFixture_FailsBrRo100()
    {
        var invoice = LoadAndParse("Invalid/invalid-br-ro-100.xml");
        var result = _validator.Validate(invoice);

        result.Errors.Should().Contain(e => e.ErrorCode == "BR-RO-100");
    }
}
