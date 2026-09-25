using System.IO.Compression;
using System.Text;

namespace RoEFactura.Tests.Services.Api;

/// <summary>
/// Fixed ANAF response samples for WP-A contract tests. Intentionally independent of the WP-C
/// validation fixtures under RoEFactura.Tests/Fixtures.
/// </summary>
public static class AnafSamples
{
    /// <summary>A minimal, well-formed UBL invoice (ID <c>SAMPLE-001</c>) that parses successfully.</summary>
    public const string MinimalInvoiceXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:CustomizationID>urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:CIUS-RO:1.0.1</cbc:CustomizationID>
          <cbc:ID>SAMPLE-001</cbc:ID>
          <cbc:IssueDate>2024-01-15</cbc:IssueDate>
          <cbc:InvoiceTypeCode>380</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>RON</cbc:DocumentCurrencyCode>
          <cac:AccountingSupplierParty>
            <cac:Party>
              <cac:PartyName><cbc:Name>SC Vanzator SRL</cbc:Name></cac:PartyName>
              <cac:PostalAddress>
                <cbc:CityName>Cluj-Napoca</cbc:CityName>
                <cbc:CountrySubentity>RO-CJ</cbc:CountrySubentity>
                <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
              </cac:PostalAddress>
              <cac:PartyTaxScheme>
                <cbc:CompanyID>RO12345678</cbc:CompanyID>
                <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
              </cac:PartyTaxScheme>
              <cac:PartyLegalEntity>
                <cbc:RegistrationName>SC Vanzator SRL</cbc:RegistrationName>
                <cbc:CompanyID>J12/100/2020</cbc:CompanyID>
              </cac:PartyLegalEntity>
            </cac:Party>
          </cac:AccountingSupplierParty>
          <cac:AccountingCustomerParty>
            <cac:Party>
              <cac:PartyName><cbc:Name>SC Cumparator SRL</cbc:Name></cac:PartyName>
              <cac:PostalAddress>
                <cbc:CityName>Iasi</cbc:CityName>
                <cbc:CountrySubentity>RO-IS</cbc:CountrySubentity>
                <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
              </cac:PostalAddress>
              <cac:PartyTaxScheme>
                <cbc:CompanyID>RO87654321</cbc:CompanyID>
                <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
              </cac:PartyTaxScheme>
              <cac:PartyLegalEntity>
                <cbc:RegistrationName>SC Cumparator SRL</cbc:RegistrationName>
                <cbc:CompanyID>J40/200/2019</cbc:CompanyID>
              </cac:PartyLegalEntity>
            </cac:Party>
          </cac:AccountingCustomerParty>
          <cac:TaxTotal>
            <cbc:TaxAmount currencyID="RON">19.00</cbc:TaxAmount>
            <cac:TaxSubtotal>
              <cbc:TaxableAmount currencyID="RON">100.00</cbc:TaxableAmount>
              <cbc:TaxAmount currencyID="RON">19.00</cbc:TaxAmount>
              <cac:TaxCategory>
                <cbc:ID>S</cbc:ID>
                <cbc:Percent>19</cbc:Percent>
                <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
              </cac:TaxCategory>
            </cac:TaxSubtotal>
          </cac:TaxTotal>
          <cac:LegalMonetaryTotal>
            <cbc:LineExtensionAmount currencyID="RON">100.00</cbc:LineExtensionAmount>
            <cbc:TaxExclusiveAmount currencyID="RON">100.00</cbc:TaxExclusiveAmount>
            <cbc:TaxInclusiveAmount currencyID="RON">119.00</cbc:TaxInclusiveAmount>
            <cbc:PayableAmount currencyID="RON">119.00</cbc:PayableAmount>
          </cac:LegalMonetaryTotal>
          <cac:InvoiceLine>
            <cbc:ID>1</cbc:ID>
            <cbc:InvoicedQuantity unitCode="C62">1</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount currencyID="RON">100.00</cbc:LineExtensionAmount>
            <cac:Item>
              <cbc:Name>Servicii consultanta</cbc:Name>
              <cac:ClassifiedTaxCategory>
                <cbc:ID>S</cbc:ID>
                <cbc:Percent>19</cbc:Percent>
                <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
              </cac:ClassifiedTaxCategory>
            </cac:Item>
            <cac:Price>
              <cbc:PriceAmount currencyID="RON">100.00</cbc:PriceAmount>
            </cac:Price>
          </cac:InvoiceLine>
        </Invoice>
        """;

    public const string UploadSuccess =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><header xmlns="mfp:anaf:dgti:spv:respUploadFisier:v1" dateResponse="202108051140" ExecutionStatus="0" index_incarcare="3828"/>""";

    public const string UploadError =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><header xmlns="mfp:anaf:dgti:spv:respUploadFisier:v1" dateResponse="202108051140" ExecutionStatus="1"><Errors errorMessage="Eroare 1"/><Errors errorMessage="Eroare 2"/></header>""";

    public const string StatusOk =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><header xmlns="mfp:anaf:dgti:efactura:stareMesajFactura:v1" stare="ok" id_descarcare="1234"/>""";

    public const string StatusNok =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><header xmlns="mfp:anaf:dgti:efactura:stareMesajFactura:v1" stare="nok" id_descarcare="123"/>""";

    public const string StatusInProcessing =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><header xmlns="mfp:anaf:dgti:efactura:stareMesajFactura:v1" stare="in prelucrare"/>""";

    public const string StatusRejected =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><header xmlns="mfp:anaf:dgti:efactura:stareMesajFactura:v1" stare="XML cu erori nepreluat de sistem"/>""";

    public const string StatusErrorsOnly =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><header xmlns="mfp:anaf:dgti:efactura:stareMesajFactura:v1"><Errors errorMessage="Nu aveti dreptul de inteorgare pentru id_incarcare= 18"/></header>""";

    public const string ListEroare =
        """{"eroare":"Nu exista mesaje in ultimele 60 zile","titlu":"Lista Mesaje"}""";

    /// <summary>A real ANAF list error (not the "no messages" case) -- must not be treated as an empty list.</summary>
    public const string ListEroareNoRight =
        """{"eroare":"Nu aveti drept in SPV pentru CIF=8000000000","titlu":"Lista Mesaje"}""";

    public const string DownloadEroare =
        """{"eroare":"Pentru id=21 nu exista inregistrata nici o factura","titlu":"Descarcare mesaj"}""";

    public const string DownloadWindowExpired =
        """{"eroare":"Fisierul nu mai poate fi descarcat pentru ca a trecut perioada de 60 de zile in care este disponibil"}""";

    public const string ValidateOk =
        """{"stare":"ok","trace_id":"abc-123"}""";

    public const string ValidateNok =
        """{"stare":"nok","Messages":[{"message":"E: validari globale SCHEMATRON eroare: [BR-RO-110]-Codul de judet trebuie sa inceapa cu RO-"}],"trace_id":"abc-456"}""";

    /// <summary>
    /// Builds an in-memory ZIP with the given entries, writing entry bytes directly (no
    /// <see cref="StreamWriter"/>) so no UTF-8 BOM is ever emitted.
    /// </summary>
    public static byte[] BuildZip(params (string EntryName, string Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string entryName, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(entryName);
                using Stream entryStream = entry.Open();
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        return stream.ToArray();
    }
}
