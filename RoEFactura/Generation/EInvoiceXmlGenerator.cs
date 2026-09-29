using System.Globalization;
using System.Xml.Linq;
using RoEFactura.Validation.Constants;

namespace RoEFactura.Generation;

/// <summary>
/// Stateless CIUS-RO UBL 2.1 Invoice generator. <see cref="Generate"/> validates the document, computes
/// the totals with <see cref="EInvoiceTotalsCalculator"/>, builds the XML in UBL 2.1 schema order and
/// returns it as UTF-8 bytes without a byte order mark.
/// </summary>
/// <remarks>
/// The output follows the official CIUS-RO validation artifacts 1.0.9 (specification identifier
/// <see cref="RomanianConstants.CustomizationId"/>): TypeCode 380, currency RON, one TaxTotal, no
/// UBLVersionID/ProfileID, no document-level allowances or charges.
/// </remarks>
public sealed class EInvoiceXmlGenerator : IEInvoiceXmlGenerator
{
    private static readonly XNamespace InvoiceNs = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>BT-3: commercial invoice (BR-RO-020); storno invoices use 380 with negative quantities.</summary>
    private const string InvoiceTypeCode = "380";

    /// <summary>TaxScheme identifier of every VAT category and VAT identifier.</summary>
    private const string VatSchemeId = "VAT";

    /// <summary>BR-O-10: the NotSubject breakdown carries the VATEX code for "not subject to VAT".</summary>
    private const string NotSubjectReasonCode = "VATEX-EU-O";

    /// <summary>BR-O-10: the NotSubject breakdown reason text (BT-120).</summary>
    private const string NotSubjectReasonText = "Neplătitor de TVA";

    private const string AmountFormat = "0.00";
    private const string PriceFormat = "0.00##";
    private const string QuantityFormat = "0.###";
    private const string PercentFormat = "0.##";

    /// <inheritdoc/>
    public byte[] Generate(EInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        EInvoiceDocumentGuard.Validate(document);
        EInvoiceTotals totals = EInvoiceTotalsCalculator.Calculate(document);
        XDocument xml = Build(document, totals);

        return EInvoiceXmlWriter.ToUtf8Bytes(xml);
    }

    private static XDocument Build(EInvoiceDocument document, EInvoiceTotals totals)
    {
        string currency = EInvoiceDocumentGuard.SupportedCurrency;

        // Root children follow the UBL 2.1 Invoice xsd sequence.
        XElement root = new(InvoiceNs + "Invoice",
            new XAttribute("xmlns", InvoiceNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
            // BT-24 (BR-01, BR-RO-001).
            CbcElement("CustomizationID", RomanianConstants.CustomizationId),
            // BT-1 (BR-02, BR-RO-010).
            CbcElement("ID", document.Number.Trim()),
            // BT-2 (BR-03), yyyy-MM-dd (BR-RO-DT001).
            CbcElement("IssueDate", FormatDate(document.IssueDate)),
            // BT-9 (BR-CO-25), yyyy-MM-dd (BR-RO-DT003).
            document.DueDate is { } dueDate ? CbcElement("DueDate", FormatDate(dueDate)) : null,
            // BT-3 (BR-04, BR-CL-01, BR-RO-020).
            CbcElement("InvoiceTypeCode", InvoiceTypeCode),
            // BT-22 (BR-RO-A020, BR-RO-L302).
            (document.Notes ?? []).Select(note => CbcElement("Note", note.Trim())),
            // BT-5 (BR-05, BR-CL-04); no BT-6, so BR-RO-030 is met by its RON-only branch.
            CbcElement("DocumentCurrencyCode", currency),
            BuildBillingReference(document.BillingReference),
            BuildSeller(document.Seller),
            BuildBuyer(document),
            BuildPaymentMeans(document.Payment),
            BuildPaymentTerms(document.PaymentTerms),
            BuildTaxTotal(totals, document.VatExemption),
            BuildLegalMonetaryTotal(totals),
            document.Lines.Select((line, index) => BuildLine(line, index + 1, totals.LineNetAmounts[index])));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement? BuildBillingReference(EInvoiceBillingReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        // BG-3: BT-25 (BR-55, BR-RO-L156) and BT-26 (BR-RO-DT001).
        return CacElement("BillingReference",
            CacElement("InvoiceDocumentReference",
                CbcElement("ID", reference.Number.Trim()),
                CbcElement("IssueDate", FormatDate(reference.IssueDate))));
    }

    private static XElement BuildSeller(EInvoiceSeller seller)
    {
        string cui = EInvoiceDocumentGuard.NormalizeCui(seller.Cui)!;
        ResolvedAddress address = EInvoiceDocumentGuard.ResolvePostalAddress(seller.Address, PartyRole.Seller);

        return CacElement("AccountingSupplierParty",
            CacElement("Party",
                BuildPostalAddress(address),
                // BT-31 for a VAT payer (BR-S-02/BR-E-02, BR-CO-09, BR-RO-065); omitted for a non-payer (BR-O-02).
                seller.IsVatPayer ? BuildVatPartyTaxScheme(EInvoiceDocumentGuard.RomaniaCountryCode + cui) : null,
                // BT-27 (BR-06, BR-RO-L201), BT-30 (BR-CO-26), BT-33 (BR-RO-L1000).
                CacElement("PartyLegalEntity",
                    CbcElement("RegistrationName", seller.Name.Trim()),
                    CbcElement("CompanyID", cui),
                    string.IsNullOrWhiteSpace(seller.TradeRegisterNumber)
                        ? null
                        : CbcElement("CompanyLegalForm", seller.TradeRegisterNumber.Trim()))));
    }

    private static XElement BuildBuyer(EInvoiceDocument document)
    {
        EInvoiceBuyer buyer = document.Buyer;
        ResolvedAddress address = EInvoiceDocumentGuard.ResolvePostalAddress(buyer.Address, PartyRole.Buyer);
        string? legalId = EInvoiceDocumentGuard.BuyerLegalId(document);

        return CacElement("AccountingCustomerParty",
            CacElement("Party",
                BuildPostalAddress(address),
                // BT-48 trimmed and upper-cased (BR-CO-09, BR-RO-120); omitted on NotSubject invoices (BR-O-02).
                EInvoiceDocumentGuard.EmitsBuyerVatId(document)
                    ? BuildVatPartyTaxScheme(EInvoiceDocumentGuard.NormalizeVatId(buyer.VatId!))
                    : null,
                // BT-44 (BR-07, BR-RO-L203), BT-47 (BR-RO-120; 13 zeros for a natural person without CNP;
                // the CUI digits of an RO VatId when BR-O-02 omits BT-48).
                CacElement("PartyLegalEntity",
                    CbcElement("RegistrationName", buyer.Name.Trim()),
                    legalId is null ? null : CbcElement("CompanyID", legalId))));
    }

    private static XElement BuildPostalAddress(ResolvedAddress address)
    {
        // BT-35/50 (BR-RO-081/082), BT-37/52 (BR-RO-091/092, SECTORn per BR-RO-100/101), BT-38/53,
        // BT-39/54 (ISO 3166-2:RO per BR-RO-110/111, Romania only), BT-40/55 (BR-09/BR-11, BR-CL-14).
        return CacElement("PostalAddress",
            CbcElement("StreetName", address.Street),
            CbcElement("CityName", address.City),
            address.PostalCode is null ? null : CbcElement("PostalZone", address.PostalCode),
            address.CountySubentity is null ? null : CbcElement("CountrySubentity", address.CountySubentity),
            CacElement("Country", CbcElement("IdentificationCode", address.CountryCode)));
    }

    private static XElement BuildVatPartyTaxScheme(string vatId)
    {
        return CacElement("PartyTaxScheme",
            CbcElement("CompanyID", vatId),
            CacElement("TaxScheme", CbcElement("ID", VatSchemeId)));
    }

    private static XElement? BuildPaymentMeans(EInvoicePayment? payment)
    {
        if (payment is null)
        {
            return null;
        }

        // BT-81 = 30 credit transfer (BR-49, BR-CL-16); BT-84 IBAN (BR-50, BR-61).
        return CacElement("PaymentMeans",
            CbcElement("PaymentMeansCode", EInvoicePayment.CreditTransferPaymentMeansCode),
            CacElement("PayeeFinancialAccount",
                CbcElement("ID", EInvoiceDocumentGuard.NormalizeIban(payment.Iban))));
    }

    private static XElement? BuildPaymentTerms(string? paymentTerms)
    {
        // BT-20 (BR-CO-25, BR-RO-L301, UBL-SR-05: a single note).
        return string.IsNullOrWhiteSpace(paymentTerms)
            ? null
            : CacElement("PaymentTerms", CbcElement("Note", paymentTerms.Trim()));
    }

    private static XElement BuildTaxTotal(EInvoiceTotals totals, EInvoiceVatExemption? exemption)
    {
        // Exactly one TaxTotal in the document currency (BR-CO-15); BT-110 = sum of BT-117 (BR-CO-14).
        return CacElement("TaxTotal",
            Amount("TaxAmount", totals.TaxAmount),
            totals.VatBreakdown.Select(group => CacElement("TaxSubtotal",
                // BT-116 (BR-45, BR-S-08/BR-E-08/BR-O-08), BT-117 (BR-46, BR-S-09/BR-E-09/BR-O-09, BR-CO-17).
                Amount("TaxableAmount", group.TaxableAmount),
                Amount("TaxAmount", group.TaxAmount),
                BuildSubtotalCategory(group, exemption))));
    }

    private static XElement BuildSubtotalCategory(EInvoiceVatBreakdown group, EInvoiceVatExemption? exemption)
    {
        string? reasonCode = null;
        string? reasonText = null;
        XElement? percent = null;

        switch (group.Category)
        {
            case EInvoiceVatCategory.Standard:
                // BT-119 > 0 (BR-S-06 context, BR-48); no exemption reason (BR-S-10).
                percent = CbcElement("Percent", FormatPercent(group.Rate));
                break;

            case EInvoiceVatCategory.Exempt:
                // BT-119 = 0 (BR-48, BR-E-05 family); BT-121/BT-120 from the document (BR-E-10, BR-CL-22).
                percent = CbcElement("Percent", FormatPercent(0m));
                reasonCode = string.IsNullOrWhiteSpace(exemption?.ReasonCode) ? null : exemption.ReasonCode.Trim().ToUpperInvariant();
                reasonText = string.IsNullOrWhiteSpace(exemption?.Reason) ? null : exemption.Reason.Trim();
                break;

            case EInvoiceVatCategory.NotSubject:
                // No BT-119 (BR-48 exempts O); VATEX-EU-O plus text (BR-O-10, BR-CL-22).
                reasonCode = NotSubjectReasonCode;
                reasonText = NotSubjectReasonText;
                break;
        }

        // BT-118 (BR-47, BR-CL-17); UBL-SR-32: at most one reason text.
        return CacElement("TaxCategory",
            CbcElement("ID", CategoryCode(group.Category)),
            percent,
            reasonCode is null ? null : CbcElement("TaxExemptionReasonCode", reasonCode),
            reasonText is null ? null : CbcElement("TaxExemptionReason", reasonText),
            CacElement("TaxScheme", CbcElement("ID", VatSchemeId)));
    }

    private static XElement BuildLegalMonetaryTotal(EInvoiceTotals totals)
    {
        // BT-106 (BR-12, BR-CO-10), BT-109 (BR-13, BR-CO-13), BT-112 (BR-14, BR-CO-15), BT-115 (BR-15, BR-CO-16);
        // all with at most 2 decimals (BR-DEC-09/12/14/18).
        return CacElement("LegalMonetaryTotal",
            Amount("LineExtensionAmount", totals.LineExtensionAmount),
            Amount("TaxExclusiveAmount", totals.TaxExclusiveAmount),
            Amount("TaxInclusiveAmount", totals.TaxInclusiveAmount),
            Amount("PayableAmount", totals.PayableAmount));
    }

    private static XElement BuildLine(EInvoiceLine line, int number, decimal lineNet)
    {
        return CacElement("InvoiceLine",
            // BT-126 (BR-21).
            CbcElement("ID", number.ToString(CultureInfo.InvariantCulture)),
            // BT-127 (BR-RO-L303).
            string.IsNullOrWhiteSpace(line.Note) ? null : CbcElement("Note", line.Note.Trim()),
            // BT-129/BT-130 (BR-22, BR-23, BR-CL-23); negative for storno; exact thanks to the 3-decimal guard.
            new XElement(Cbc + "InvoicedQuantity",
                new XAttribute("unitCode", line.UnitCode.Trim()),
                line.Quantity.ToString(QuantityFormat, CultureInfo.InvariantCulture)),
            // BT-131 (BR-24, BR-DEC-23).
            Amount("LineExtensionAmount", lineNet),
            CacElement("Item",
                // BT-154 (BR-RO-L212).
                string.IsNullOrWhiteSpace(line.Description) ? null : CbcElement("Description", line.Description.Trim()),
                // BT-153 (BR-25, BR-RO-L1024).
                CbcElement("Name", line.Name.Trim()),
                // BT-151 (BR-CO-04, BR-CL-18); BT-152: S = rate (BR-S-05), E = 0 (BR-E-05), O = none (BR-O-05).
                CacElement("ClassifiedTaxCategory",
                    CbcElement("ID", CategoryCode(line.VatCategory)),
                    line.VatCategory switch
                    {
                        EInvoiceVatCategory.Standard => CbcElement("Percent", FormatPercent(line.VatRate)),
                        EInvoiceVatCategory.Exempt => CbcElement("Percent", FormatPercent(0m)),
                        _ => null
                    },
                    CacElement("TaxScheme", CbcElement("ID", VatSchemeId)))),
            // BT-146 (BR-26, BR-27 >= 0), up to 4 decimals emitted verbatim.
            CacElement("Price",
                new XElement(Cbc + "PriceAmount",
                    new XAttribute("currencyID", EInvoiceDocumentGuard.SupportedCurrency),
                    line.UnitPrice.ToString(PriceFormat, CultureInfo.InvariantCulture))));
    }

    /// <summary>UNCL5305 code (BR-CL-17/BR-CL-18).</summary>
    private static string CategoryCode(EInvoiceVatCategory category)
    {
        return category switch
        {
            EInvoiceVatCategory.Standard => "S",
            EInvoiceVatCategory.Exempt => "E",
            EInvoiceVatCategory.NotSubject => "O",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "[BR-CL-18] Unsupported VAT category.")
        };
    }

    /// <summary>An amount in RON with exactly 2 decimals (BR-DEC-*, BR-RO-Z2; BR-CL-03 currencyID).</summary>
    private static XElement Amount(string name, decimal value)
    {
        return new XElement(Cbc + name,
            new XAttribute("currencyID", EInvoiceDocumentGuard.SupportedCurrency),
            value.ToString(AmountFormat, CultureInfo.InvariantCulture));
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(decimal rate)
    {
        return rate.ToString(PercentFormat, CultureInfo.InvariantCulture);
    }

    private static XElement CbcElement(string name, string value)
    {
        return new XElement(Cbc + name, value);
    }

    private static XElement CacElement(string name, params object?[] content)
    {
        return new XElement(Cac + name, content);
    }
}
