using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Generation;
using RoEFactura.Tests.Generation.TestData;
using RoEFactura.Validation.Constants;
using Xunit;

namespace RoEFactura.Tests.Generation;

/// <summary>
/// Output shape of <see cref="EInvoiceXmlGenerator"/> for the seven MTX-131 cases, checked against the
/// local validator and the official CIUS-RO 1.0.9 rules cited per test.
/// </summary>
public class EInvoiceXmlGeneratorTests
{
    /// <summary>§3.3 top-level order (UBL 2.1 Invoice xsd sequence); Note and InvoiceLine may repeat.</summary>
    private static readonly string[] UblTopLevelSequence =
    [
        "CustomizationID", "ID", "IssueDate", "DueDate", "InvoiceTypeCode", "Note", "DocumentCurrencyCode",
        "BillingReference", "AccountingSupplierParty", "AccountingCustomerParty", "PaymentMeans", "PaymentTerms",
        "TaxTotal", "LegalMonetaryTotal", "InvoiceLine"
    ];

    private static readonly string[] RepeatableTopLevelElements = ["Note", "InvoiceLine"];

    private const string Buyer = "/inv:Invoice/cac:AccountingCustomerParty/cac:Party";
    private const string Seller = "/inv:Invoice/cac:AccountingSupplierParty/cac:Party";

    [Theory]
    [MemberData(nameof(EInvoiceTestCases.All), MemberType = typeof(EInvoiceTestCases))]
    public void Generate_EachCase_PassesLocalValidator(string caseName)
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.Get(caseName));

        ValidationResult result = xml.ValidateLocally();

        result.Errors.Should().BeEmpty(
            $"case {caseName} must pass RoCiusUblValidator, got: " +
            string.Join(", ", result.Errors.Select(e => $"{e.ErrorCode}: {e.ErrorMessage}")));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Generate_AnyCase_FirstLineDeclaresUtf8WithoutBom()
    {
        EInvoiceXmlGenerator generator = new();

        foreach (string caseName in EInvoiceTestCases.Names)
        {
            byte[] bytes = generator.Generate(EInvoiceTestCases.Get(caseName));

            bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, $"case {caseName} must not start with a BOM");
            string firstLine = Encoding.UTF8.GetString(bytes).Split('\n')[0].TrimEnd('\r');
            firstLine.Should().Be("<?xml version=\"1.0\" encoding=\"utf-8\"?>", $"case {caseName}");
        }
    }

    [Fact]
    public void Generate_AnyCase_TopLevelElementsFollowUblSequence()
    {
        foreach (string caseName in EInvoiceTestCases.Names)
        {
            GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.Get(caseName));
            List<string> names = xml.Document.Root!.Elements().Select(e => e.Name.LocalName).ToList();

            int previousIndex = -1;
            string? previousName = null;
            foreach (string name in names)
            {
                int index = Array.IndexOf(UblTopLevelSequence, name);
                index.Should().BeGreaterThanOrEqualTo(0, $"case {caseName}: '{name}' is not in the planned sequence");
                index.Should().BeGreaterThanOrEqualTo(previousIndex, $"case {caseName}: '{name}' after '{previousName}'");
                if (index == previousIndex)
                {
                    RepeatableTopLevelElements.Should().Contain(name, $"case {caseName}: '{name}' must not repeat");
                }

                previousIndex = index;
                previousName = name;
            }

            names.Should().ContainInOrder("CustomizationID", "ID", "IssueDate", "InvoiceTypeCode", "DocumentCurrencyCode",
                "AccountingSupplierParty", "AccountingCustomerParty", "TaxTotal", "LegalMonetaryTotal", "InvoiceLine");
            names.Count(n => n == "TaxTotal").Should().Be(1, $"case {caseName} has exactly one TaxTotal in RON");
        }
    }

    [Fact]
    public void Generate_AnyCase_CustomizationIdIsCiusRo()
    {
        foreach (string caseName in EInvoiceTestCases.Names)
        {
            GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.Get(caseName));

            // BR-RO-001
            xml.Value("/inv:Invoice/cbc:CustomizationID").Should().Be(RomanianConstants.CustomizationId, $"case {caseName}");
            xml.Value("/inv:Invoice/cbc:InvoiceTypeCode").Should().Be("380", $"case {caseName}");
            xml.Element("/inv:Invoice/cbc:UBLVersionID").Should().BeNull();
            xml.Element("/inv:Invoice/cbc:ProfileID").Should().BeNull();
        }
    }

    [Fact]
    public void Generate_B2BRoVatPayerBuyer_EmitsBuyerVatIdAndLegalId()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.B2BRoVatPayerBuyer());

        // BT-48 under the VAT scheme (BR-CO-09) and BT-47 (BR-RO-120).
        xml.Value($"{Buyer}/cac:PartyTaxScheme[cac:TaxScheme/cbc:ID='VAT']/cbc:CompanyID").Should().Be("RO876543213");
        xml.Value($"{Buyer}/cac:PartyLegalEntity/cbc:CompanyID").Should().Be("J12/345/2020");
        xml.Value($"{Buyer}/cac:PartyLegalEntity/cbc:RegistrationName").Should().Be("Client Test SRL");
        // Seller BT-31 = RO + CUI, BT-30 = CUI digits.
        xml.Value($"{Seller}/cac:PartyTaxScheme[cac:TaxScheme/cbc:ID='VAT']/cbc:CompanyID").Should().Be("RO1234567897");
        xml.Value($"{Seller}/cac:PartyLegalEntity/cbc:CompanyID").Should().Be("1234567897");
        xml.Value($"{Seller}/cac:PartyLegalEntity/cbc:CompanyLegalForm").Should().Be("J12/1234/2020");
    }

    [Fact]
    public void Generate_B2BRoVatPayerBuyer_ConvertsBucharestAddress()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.B2BRoVatPayerBuyer());

        // BR-RO-111 county code, BR-RO-101 sector city.
        xml.Value($"{Buyer}/cac:PostalAddress/cbc:CountrySubentity").Should().Be("RO-B");
        xml.Value($"{Buyer}/cac:PostalAddress/cbc:CityName").Should().Be("SECTOR3");
        xml.Value($"{Buyer}/cac:PostalAddress/cac:Country/cbc:IdentificationCode").Should().Be("RO");
        // Seller: Cluj -> RO-CJ (BR-RO-110), city kept.
        xml.Value($"{Seller}/cac:PostalAddress/cbc:CountrySubentity").Should().Be("RO-CJ");
        xml.Value($"{Seller}/cac:PostalAddress/cbc:CityName").Should().Be("Cluj-Napoca");
    }

    [Fact]
    public void Generate_B2BRoNonVatPayerBuyer_EmitsLegalIdWithoutPartyTaxScheme()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.B2BRoNonVatPayerBuyer());

        xml.Elements($"{Buyer}/cac:PartyTaxScheme").Should().BeEmpty();
        xml.Value($"{Buyer}/cac:PartyLegalEntity/cbc:CompanyID").Should().Be("876543213");
        xml.Value($"{Buyer}/cac:PostalAddress/cbc:CountrySubentity").Should().Be("RO-IF");
        xml.Value($"{Buyer}/cac:PostalAddress/cbc:CityName").Should().Be("Voluntari");
    }

    [Fact]
    public void Generate_NaturalPersonWithoutCnp_EmitsThirteenZeros()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.NaturalPersonWithoutCnp());

        XElement? companyId = xml.Element($"{Buyer}/cac:PartyLegalEntity/cbc:CompanyID");
        companyId.Should().NotBeNull();
        companyId!.Value.Should().Be(EInvoiceBuyer.NaturalPersonWithoutCnpId).And.Be("0000000000000");
        companyId.Attribute("schemeID").Should().BeNull();
        xml.Elements($"{Buyer}/cac:PartyTaxScheme").Should().BeEmpty();
        xml.Value($"{Buyer}/cac:PostalAddress/cbc:CountrySubentity").Should().Be("RO-IS");
        xml.Value("/inv:Invoice/cac:PaymentTerms/cbc:Note").Should().Be("Plata la livrare");
    }

    [Fact]
    public void Generate_ForeignBuyer_EmitsCountryAndPrefixedVatIdWithoutCounty()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.ForeignBuyer());

        xml.Value($"{Buyer}/cac:PostalAddress/cac:Country/cbc:IdentificationCode").Should().Be("DE");
        xml.Value($"{Buyer}/cac:PartyTaxScheme[cac:TaxScheme/cbc:ID='VAT']/cbc:CompanyID").Should().Be("DE123456789");
        // BT-47 = 13 zeros: ANAF's validator must identify a buyer CUI (ERRIdentif otherwise).
        xml.Value($"{Buyer}/cac:PartyLegalEntity/cbc:CompanyID").Should().Be("0000000000000");
        xml.Elements($"{Buyer}/cac:PostalAddress/cbc:CountrySubentity").Should().BeEmpty();
        xml.Value($"{Buyer}/cac:PostalAddress/cbc:CityName").Should().Be("Berlin");
        xml.Value($"{Buyer}/cac:PostalAddress/cbc:StreetName").Should().Be("Musterstraße 1");
    }

    [Fact]
    public void Generate_NonVatPayerSeller_EmitsSingleOSubtotalWithoutVatIds()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.NonVatPayerSeller());

        IReadOnlyList<XElement> subtotals = xml.Elements("/inv:Invoice/cac:TaxTotal/cac:TaxSubtotal");
        subtotals.Should().ContainSingle();
        string category = "/inv:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory";
        xml.Value($"{category}/cbc:ID").Should().Be("O");
        xml.Value($"{category}/cbc:TaxExemptionReasonCode").Should().Be("VATEX-EU-O");
        xml.Value($"{category}/cbc:TaxExemptionReason").Should().Be("Neplătitor de TVA");
        xml.Elements($"{category}/cbc:Percent").Should().BeEmpty();
        xml.Value("/inv:Invoice/cac:TaxTotal/cbc:TaxAmount").Should().Be("0.00");

        // BR-O-02: no seller or buyer VAT identifier.
        xml.Elements($"{Seller}/cac:PartyTaxScheme").Should().BeEmpty();
        xml.Elements($"{Buyer}/cac:PartyTaxScheme").Should().BeEmpty();
        xml.Value($"{Seller}/cac:PartyLegalEntity/cbc:CompanyID").Should().Be("1234567897");

        // BR-O-05: line category O without a rate.
        xml.Value("/inv:Invoice/cac:InvoiceLine/cac:Item/cac:ClassifiedTaxCategory/cbc:ID").Should().Be("O");
        xml.Elements("/inv:Invoice/cac:InvoiceLine/cac:Item/cac:ClassifiedTaxCategory/cbc:Percent").Should().BeEmpty();
    }

    [Fact]
    public void Generate_MixedRates_EmitsThreeSubtotalsWithExpectedAmounts()
    {
        EInvoiceDocument document = EInvoiceTestCases.MixedRates();
        EInvoiceTotals totals = EInvoiceTotalsCalculator.Calculate(document);
        GeneratedXml xml = GeneratedXml.From(document);

        IReadOnlyList<XElement> subtotals = xml.Elements("/inv:Invoice/cac:TaxTotal/cac:TaxSubtotal");
        subtotals.Should().HaveCount(3);

        (string Id, string? Percent, string Taxable, string Tax)[] emitted = subtotals
            .Select(s => (
                Id: s.Element(XName.Get("TaxCategory", GeneratedXml.CacNamespace))!.Element(XName.Get("ID", GeneratedXml.CbcNamespace))!.Value,
                Percent: s.Element(XName.Get("TaxCategory", GeneratedXml.CacNamespace))!.Element(XName.Get("Percent", GeneratedXml.CbcNamespace))?.Value,
                Taxable: s.Element(XName.Get("TaxableAmount", GeneratedXml.CbcNamespace))!.Value,
                Tax: s.Element(XName.Get("TaxAmount", GeneratedXml.CbcNamespace))!.Value))
            .ToArray();

        emitted.Should().Equal(
            ("S", "21", "300.00", "63.00"),
            ("S", "11", "15.25", "1.68"),
            ("E", "0", "500.00", "0.00"));

        // Same values as the calculator (BR-S-08/09, BR-E-08/09, BR-CO-17).
        emitted.Select(e => decimal.Parse(e.Taxable, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Equal(totals.VatBreakdown.Select(g => g.TaxableAmount));
        emitted.Select(e => decimal.Parse(e.Tax, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Equal(totals.VatBreakdown.Select(g => g.TaxAmount));

        // BR-E-10: the Exempt group carries the document reason text.
        xml.Value("/inv:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory[cbc:ID='E']/cbc:TaxExemptionReason")
            .Should().Be(EInvoiceTestCases.MixedRatesExemptionReason);
        xml.Elements("/inv:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory[cbc:ID='S']/cbc:TaxExemptionReason")
            .Should().BeEmpty();
        xml.Value("/inv:Invoice/cac:TaxTotal/cbc:TaxAmount").Should().Be("64.68");
        xml.Value("/inv:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount").Should().Be("879.93");
    }

    [Fact]
    public void Generate_Storno_EmitsNegativeQuantitiesAndBillingReference()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.Storno());

        xml.Elements("/inv:Invoice/cac:InvoiceLine/cbc:InvoicedQuantity")
            .Select(q => q.Value).Should().Equal("-1", "-2");
        xml.Value("/inv:Invoice/cac:BillingReference/cac:InvoiceDocumentReference/cbc:ID").Should().Be("TST-E8-0001");
        xml.Value("/inv:Invoice/cac:BillingReference/cac:InvoiceDocumentReference/cbc:IssueDate").Should().Be("2026-09-01");
        xml.Value("/inv:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount").Should().Be("-302.48");
        decimal.Parse(xml.Value("/inv:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount")!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeNegative();
        xml.Value("/inv:Invoice/cbc:InvoiceTypeCode").Should().Be("380");
        xml.Elements("/inv:Invoice/cbc:DueDate").Should().BeEmpty();
        xml.Elements("/inv:Invoice/cac:PaymentMeans").Should().BeEmpty();
        xml.Elements("/inv:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount")
            .Select(p => p.Value).Should().Equal("150.00", "49.99");
    }

    [Fact]
    public void Generate_WithPayment_EmitsCode30AndIban()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.B2BRoVatPayerBuyer());

        xml.Value("/inv:Invoice/cac:PaymentMeans/cbc:PaymentMeansCode").Should().Be("30");
        xml.Value("/inv:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:ID").Should().Be("RO49AAAA1B31007593840000");

        GeneratedXml spaced = GeneratedXml.From(EInvoiceTestCases.B2BRoVatPayerBuyer() with
        {
            Payment = new EInvoicePayment("ro49 aaaa 1b31 0075 9384 0000")
        });
        spaced.Value("/inv:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:ID").Should().Be("RO49AAAA1B31007593840000");
    }

    [Fact]
    public void Generate_NullDocument_ThrowsArgumentNullException()
    {
        EInvoiceXmlGenerator generator = new();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => generator.Generate(null!));

        exception.ParamName.Should().Be("document");
    }

    [Fact]
    public void Generate_MixedRates_EmitsFourDecimalPriceAndThreeDecimalQuantity()
    {
        GeneratedXml xml = GeneratedXml.From(EInvoiceTestCases.MixedRates());

        xml.Text.Should().Contain("<cbc:PriceAmount currencyID=\"RON\">12.3456</cbc:PriceAmount>");
        xml.Elements("/inv:Invoice/cac:InvoiceLine/cbc:InvoicedQuantity")
            .Should().Contain(q => q.Value == "1.235" && (string?)q.Attribute("unitCode") == "KGM");
        // BT-131 = round(1.235 x 12.3456, 2) = 15.25.
        xml.Value("/inv:Invoice/cac:InvoiceLine[cbc:ID='2']/cbc:LineExtensionAmount").Should().Be("15.25");
    }

    [Fact]
    public void LiveCaseBuilder_MixedRates_ExemptionReasonIsBetween120And150Chars()
    {
        EInvoiceTestCases.MixedRatesExemptionReason.Length.Should().BeInRange(120, 150);
        EInvoiceTestCases.MixedRates().VatExemption!.Reason.Should().Be(EInvoiceTestCases.MixedRatesExemptionReason);
    }
}
