using FluentAssertions;
using RoEFactura.Generation;
using RoEFactura.Tests.Generation.TestData;
using Xunit;

namespace RoEFactura.Tests.Generation;

/// <summary>
/// Input guards of <see cref="EInvoiceXmlGenerator.Generate"/>: each invalid document throws an exact-type
/// <see cref="ArgumentException"/> whose message carries the official rule id, before any XML is built.
/// </summary>
public class EInvoiceDocumentGuardTests
{
    private static readonly EInvoiceXmlGenerator Generator = new();

    private static ArgumentException AssertRejected(EInvoiceDocument document, string ruleId)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Generator.Generate(document));
        exception.Message.Should().Contain(ruleId);
        exception.ParamName.Should().NotBeNullOrWhiteSpace();
        return exception;
    }

    private static EInvoiceDocument ValidDocument()
    {
        return EInvoiceTestCases.B2BRoVatPayerBuyer();
    }

    private static EInvoiceDocument WithFirstLine(EInvoiceDocument document, Func<EInvoiceLine, EInvoiceLine> change)
    {
        return document with { Lines = [change(document.Lines[0]), .. document.Lines.Skip(1)] };
    }

    private static EInvoiceDocument WithBuyerAddress(EInvoiceDocument document, Func<EInvoiceAddress, EInvoiceAddress> change)
    {
        return document with { Buyer = document.Buyer with { Address = change(document.Buyer.Address) } };
    }

    [Fact]
    public void Generate_NegativeUnitPrice_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { UnitPrice = -1m });

        ArgumentException exception = AssertRejected(document, "[BR-27]");

        exception.Message.Should().Contain("line 1");
        exception.ParamName.Should().Be("Lines[0].UnitPrice");
    }

    [Fact]
    public void Generate_NegativeQuantity_DoesNotThrow()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { Quantity = -1m });

        GeneratedXml xml = GeneratedXml.From(document);

        xml.Value("/inv:Invoice/cac:InvoiceLine[cbc:ID='1']/cbc:InvoicedQuantity").Should().Be("-1");
        xml.Value("/inv:Invoice/cac:InvoiceLine[cbc:ID='1']/cbc:LineExtensionAmount").Should().Be("-200.00");
    }

    [Fact]
    public void Generate_ZeroQuantity_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { Quantity = 0m });

        AssertRejected(document, "[BR-22]");
    }

    [Fact]
    public void Generate_NonRonCurrency_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { CurrencyCode = "EUR" };

        AssertRejected(document, "[decision-4]");
    }

    [Fact]
    public void Generate_NumberWithoutDigit_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { Number = "TST-E-ABC" };

        AssertRejected(document, "[BR-RO-010]");
    }

    [Fact]
    public void Generate_NonVatPayerSellerWithStandardLine_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { Seller = ValidDocument().Seller with { IsVatPayer = false } };

        ArgumentException exception = AssertRejected(document, "[BR-S-02]");

        exception.Message.Should().Contain("BR-O-12");
    }

    [Fact]
    public void Generate_VatPayerSellerWithNotSubjectLine_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(),
            line => line with { VatCategory = EInvoiceVatCategory.NotSubject, VatRate = 0m });

        AssertRejected(document, "[BR-O-02]");
    }

    [Fact]
    public void Generate_ExemptLineWithoutExemptionReason_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(),
            line => line with { VatCategory = EInvoiceVatCategory.Exempt, VatRate = 0m });

        AssertRejected(document, "[BR-E-10]");
        AssertRejected(document with { VatExemption = new EInvoiceVatExemption(" ", null) }, "[BR-E-10]");
    }

    [Fact]
    public void Generate_StandardLineWithZeroRate_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { VatRate = 0m });

        AssertRejected(document, "[BR-S-05]");
    }

    [Fact]
    public void Generate_RoCompanyBuyerWithoutIds_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Buyer = ValidDocument().Buyer with { VatId = null, LegalRegistrationId = "  " }
        };

        AssertRejected(document, "[BR-RO-120]");
    }

    [Fact]
    public void Generate_PositiveTotalWithoutDueDateOrTerms_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { DueDate = null, PaymentTerms = null };

        AssertRejected(document, "[BR-CO-25]");
    }

    [Fact]
    public void Generate_BuyerVatIdWithoutCountryPrefix_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Buyer = ValidDocument().Buyer with { VatId = "876543213" }
        };

        AssertRejected(document, "[BR-CO-09]");
    }

    [Fact]
    public void Generate_InvalidIban_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { Payment = new EInvoicePayment("RO49AAAA1B31007593840001") };

        AssertRejected(document, "[BT-84]");
    }

    [Fact]
    public void Generate_MoreThanTwentyNotes_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Notes = Enumerable.Range(1, 21).Select(i => $"Nota {i}").ToList()
        };

        AssertRejected(document, "[BR-RO-A020]");
    }

    [Fact]
    public void Generate_ItemNameOver100Chars_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { Name = new string('A', 101) });

        // Official assert id BR-RO-L1024 (ANAF message tag [BR-RO-L100]).
        AssertRejected(document, "[BR-RO-L1024]");
    }

    [Fact]
    public void Generate_SellerCuiWithRoPrefix_NormalizesToDigits()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Seller = ValidDocument().Seller with { Cui = " RO 1234567897 " }
        };

        GeneratedXml xml = GeneratedXml.From(document);

        string seller = "/inv:Invoice/cac:AccountingSupplierParty/cac:Party";
        xml.Value($"{seller}/cac:PartyLegalEntity/cbc:CompanyID").Should().Be("1234567897");
        xml.Value($"{seller}/cac:PartyTaxScheme[cac:TaxScheme/cbc:ID='VAT']/cbc:CompanyID").Should().Be("RO1234567897");
    }

    [Fact]
    public void Generate_UnitPriceWithFourDecimals_EmitsPriceVerbatim()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { UnitPrice = 12.3456m });

        GeneratedXml xml = GeneratedXml.From(document);

        xml.Value("/inv:Invoice/cac:InvoiceLine[cbc:ID='1']/cac:Price/cbc:PriceAmount").Should().Be("12.3456");
    }

    [Fact]
    public void Generate_UnitPriceWithFiveDecimals_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { UnitPrice = 12.34567m });

        ArgumentException exception = AssertRejected(document, "BT-146");

        exception.Message.Should().Contain("PriceAmount (BT-146)").And.Contain("line 1");
    }

    [Fact]
    public void Generate_QuantityWithFourDecimals_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { Quantity = 1.2345m });

        AssertRejected(document, "[BT-129]");
    }

    [Fact]
    public void Generate_VatRateWithThreeDecimals_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { VatRate = 21.005m });

        AssertRejected(document, "[BT-152]");
    }

    [Fact]
    public void Generate_ForeignCompanyBuyerWithoutIds_ThrowsArgumentException()
    {
        EInvoiceDocument foreign = EInvoiceTestCases.ForeignBuyer();
        EInvoiceDocument document = foreign with
        {
            Buyer = foreign.Buyer with { IsNaturalPerson = false, VatId = null, LegalRegistrationId = null }
        };

        AssertRejected(document, "BR-RO-120");
    }

    [Fact]
    public void Generate_CityOver50Chars_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithBuyerAddress(ValidDocument(),
            address => address with { County = "Cluj", City = new string('C', 51) });

        AssertRejected(document, "BR-RO-L050");
    }

    [Fact]
    public void Generate_StreetOver150Chars_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithBuyerAddress(ValidDocument(),
            address => address with { Street = new string('S', 151) });

        AssertRejected(document, "BR-RO-L15");
    }

    [Fact]
    public void Generate_PartyNameOver200Chars_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Buyer = ValidDocument().Buyer with { Name = new string('N', 201) }
        };

        AssertRejected(document, "BR-RO-L20");
    }

    [Fact]
    public void Generate_PostalCodeOver20Chars_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithBuyerAddress(ValidDocument(),
            address => address with { PostalCode = new string('1', 21) });

        AssertRejected(document, "BR-RO-L020");
    }

    [Fact]
    public void Generate_BillingReferenceNumberOver200Chars_ThrowsArgumentException()
    {
        EInvoiceDocument document = EInvoiceTestCases.Storno() with
        {
            BillingReference = new EInvoiceBillingReference("1" + new string('X', 200), new DateOnly(2026, 9, 1))
        };

        AssertRejected(document, "BR-RO-L156");
    }

    [Fact]
    public void Generate_UnknownVatIdPrefix_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Buyer = ValidDocument().Buyer with { VatId = "ZZ876543213" }
        };

        AssertRejected(document, "BR-CO-09");
    }

    [Fact]
    public void Generate_UnknownCountryCode_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithBuyerAddress(EInvoiceTestCases.ForeignBuyer(),
            address => address with { CountryCode = "ZZ" });

        AssertRejected(document, "BR-CL-14");
    }

    [Fact]
    public void Generate_InvalidUnitCode_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { UnitCode = "buc" });

        AssertRejected(document, "BR-CL-23");
    }

    [Fact]
    public void Generate_NullLines_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { Lines = null! };

        AssertRejected(document, "[BR-16]");
    }

    [Fact]
    public void Generate_EmptyLines_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { Lines = [] };

        AssertRejected(document, "[BR-16]");
    }

    [Fact]
    public void Generate_NullBuyer_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { Buyer = null! };

        AssertRejected(document, "[BR-07]");
    }

    [Fact]
    public void Generate_NullSeller_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with { Seller = null! };

        AssertRejected(document, "[BR-06]");
    }

    [Fact]
    public void Generate_ExemptionReason200Chars_Emitted()
    {
        string reason = "Scutit conform art. 292 " + new string('x', 176);
        reason.Length.Should().Be(200);
        EInvoiceDocument document = EInvoiceTestCases.MixedRates() with
        {
            VatExemption = new EInvoiceVatExemption(null, reason)
        };

        GeneratedXml xml = GeneratedXml.From(document);

        xml.Value("/inv:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory[cbc:ID='E']/cbc:TaxExemptionReason")
            .Should().Be(reason);
        xml.Text.Should().Contain($"<cbc:TaxExemptionReason>{reason}</cbc:TaxExemptionReason>");
    }

    [Fact]
    public void Generate_NullBuyerAddress_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Buyer = ValidDocument().Buyer with { Address = null! }
        };

        AssertRejected(document, "[BR-10]");
    }

    [Fact]
    public void Generate_NullLineInLines_ThrowsArgumentException()
    {
        EInvoiceDocument valid = ValidDocument();
        EInvoiceDocument document = valid with { Lines = [valid.Lines[0], null!] };

        ArgumentException exception = AssertRejected(document, "[BR-16]");

        exception.Message.Should().Contain("line 2");
    }

    [Fact]
    public void Generate_BlankBuyerName_ThrowsArgumentException()
    {
        EInvoiceDocument document = ValidDocument() with
        {
            Buyer = ValidDocument().Buyer with { Name = "   " }
        };

        AssertRejected(document, "[BR-07]");
    }

    [Fact]
    public void Generate_BlankLineName_ThrowsArgumentException()
    {
        EInvoiceDocument document = WithFirstLine(ValidDocument(), line => line with { Name = " " });

        AssertRejected(document, "[BR-25]");
    }
}
