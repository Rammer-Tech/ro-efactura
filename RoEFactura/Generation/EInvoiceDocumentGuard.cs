using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using RoEFactura.Validation.Constants;

namespace RoEFactura.Generation;

/// <summary>
/// Validates an <see cref="EInvoiceDocument"/> before any XML is built. Every violation throws an
/// <see cref="ArgumentException"/> whose English message starts with an id in brackets — the official
/// schematron assert id (e.g. <c>[BR-RO-L0502]</c>), or the business term id (e.g. <c>[BT-153]</c>) or
/// decision id when no official rule applies — and whose <c>ParamName</c> names the offending member.
/// Only a null document throws <see cref="ArgumentNullException"/>. Also hosts the normalization helpers
/// the builder uses, so the guard and the emitted XML always agree.
/// </summary>
internal static class EInvoiceDocumentGuard
{
    internal const string RomaniaCountryCode = "RO";
    internal const string SupportedCurrency = "RON";

    private const int MaxInvoiceNumberLength = 200;      // BR-RO-L155
    private const int MaxNotes = 20;                     // BR-RO-A020
    private const int MaxNoteLength = 300;               // BR-RO-L302 / BR-RO-L303
    private const int MaxPaymentTermsLength = 300;       // BR-RO-L301
    private const int MaxPrecedingNumberLength = 200;    // BR-RO-L156
    private const int MaxPartyNameLength = 200;          // BR-RO-L201 / BR-RO-L203
    private const int MaxLegalFormLength = 1000;         // BR-RO-L1000
    private const int MaxStreetLength = 150;             // BR-RO-L151 / BR-RO-L152
    private const int MaxCityLength = 50;                // BR-RO-L0501 / BR-RO-L0502
    private const int MaxPostalCodeLength = 20;          // BR-RO-L0201 / BR-RO-L0202
    private const int MaxItemNameLength = 100;           // BR-RO-L1024
    private const int MaxItemDescriptionLength = 200;    // BR-RO-L212
    private const int MaxExemptionReasonLength = 200;    // BT-120 pass-through limit (micro-taxe E4 parity)
    private const int MaxQuantityDecimals = 3;           // BT-129 (micro-taxe E4 parity)
    private const int MaxPriceDecimals = 4;              // BT-146 (micro-taxe E4 parity)
    private const int MaxRateDecimals = 2;               // BT-152

    /// <summary>XPath 1.0 whitespace (<c>S</c> production of XML 1.0): space, tab, CR, LF — nothing else.</summary>
    private static readonly char[] XmlWhitespace = [' ', '\t', '\r', '\n'];
    private static readonly Regex XmlWhitespaceRun = new("[ \t\r\n]+", RegexOptions.CultureInvariant);
    private static readonly Regex Digit = new("[0-9]", RegexOptions.CultureInvariant);

    /// <summary>Throws on the first rule the document breaks.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">A rule is broken; the message starts with the rule id.</exception>
    public static void Validate(EInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        ValidateHeader(document);
        ValidateSeller(document.Seller);
        ValidateLines(document);
        ValidateBuyer(document);
        ValidateExemption(document);
        ValidatePayment(document);
        ValidateXmlCharacters(document);
    }

    private static void ValidateHeader(EInvoiceDocument document)
    {
        // BR-02: an invoice shall have an invoice number (BT-1).
        if (string.IsNullOrWhiteSpace(document.Number))
        {
            throw Error("BR-02", "Invoice number (BT-1) is required.", "Number");
        }

        // BR-RO-010: BT-1 must contain at least one digit.
        if (!Digit.IsMatch(document.Number))
        {
            throw Error("BR-RO-010", $"Invoice number (BT-1) must contain at least one digit; got '{document.Number}'.", "Number");
        }

        // BR-RO-L155 (reported by ANAF as BR-RO-L200): BT-1 at most 200 characters.
        if (NormalizedLength(document.Number) > MaxInvoiceNumberLength)
        {
            throw Error("BR-RO-L155", $"Invoice number (BT-1) must not exceed {MaxInvoiceNumberLength} characters.", "Number");
        }

        // Orchestrator decision 4: document currency = VAT currency = RON in v1 (BR-RO-030 branch 4).
        if (!string.Equals(document.CurrencyCode?.Trim(), SupportedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw Error("decision-4", $"Only RON is supported in v1; got CurrencyCode '{document.CurrencyCode}'.", "CurrencyCode");
        }

        IReadOnlyList<string> notes = document.Notes ?? [];

        // BR-RO-A020: at most 20 invoice notes (BG-1).
        if (notes.Count > MaxNotes)
        {
            throw Error("BR-RO-A020", $"An invoice can carry at most {MaxNotes} notes (BT-22); got {notes.Count}.", "Notes");
        }

        for (int i = 0; i < notes.Count; i++)
        {
            // BT-22 must carry text when present.
            if (string.IsNullOrWhiteSpace(notes[i]))
            {
                throw Error("BT-22", $"Invoice note {i + 1} must not be blank.", $"Notes[{i}]");
            }

            // BR-RO-L302: each invoice note at most 300 characters.
            if (NormalizedLength(notes[i]) > MaxNoteLength)
            {
                throw Error("BR-RO-L302", $"Invoice note {i + 1} (BT-22) must not exceed {MaxNoteLength} characters.", $"Notes[{i}]");
            }
        }

        // BR-RO-L301: payment terms (BT-20) at most 300 characters.
        if (NormalizedLength(document.PaymentTerms) > MaxPaymentTermsLength)
        {
            throw Error("BR-RO-L301", $"Payment terms (BT-20) must not exceed {MaxPaymentTermsLength} characters.", "PaymentTerms");
        }

        if (document.BillingReference is { } reference)
        {
            // BR-55: a preceding invoice reference (BG-3) shall contain the preceding invoice number (BT-25).
            if (string.IsNullOrWhiteSpace(reference.Number))
            {
                throw Error("BR-55", "Preceding invoice number (BT-25) is required in BillingReference.", "BillingReference.Number");
            }

            // BR-RO-L156: BT-25 at most 200 characters.
            if (NormalizedLength(reference.Number) > MaxPrecedingNumberLength)
            {
                throw Error("BR-RO-L156", $"Preceding invoice number (BT-25) must not exceed {MaxPrecedingNumberLength} characters.", "BillingReference.Number");
            }
        }
    }

    private static void ValidateSeller(EInvoiceSeller? seller)
    {
        // BR-06/BR-08: the seller (BG-4) with its name and postal address is mandatory.
        if (seller is null)
        {
            throw Error("BR-06", "Seller (BG-4) is required.", "Seller");
        }

        if (string.IsNullOrWhiteSpace(seller.Name))
        {
            throw Error("BR-06", "Seller name (BT-27) is required.", "Seller.Name");
        }

        // BR-RO-L201: seller name at most 200 characters.
        if (NormalizedLength(seller.Name) > MaxPartyNameLength)
        {
            throw Error("BR-RO-L201", $"Seller name (BT-27) must not exceed {MaxPartyNameLength} characters.", "Seller.Name");
        }

        // BR-CO-26: the seller must be identifiable; BT-30 carries the CUI digits.
        if (NormalizeCui(seller.Cui) is null)
        {
            throw Error("BR-CO-26", $"Seller CUI (BT-30) must be 2-10 digits, optionally prefixed by RO; got '{seller.Cui}'.", "Seller.Cui");
        }

        // BR-RO-L1000: seller additional legal information (BT-33) at most 1000 characters.
        if (NormalizedLength(seller.TradeRegisterNumber) > MaxLegalFormLength)
        {
            throw Error("BR-RO-L1000", $"Seller trade register number (BT-33) must not exceed {MaxLegalFormLength} characters.", "Seller.TradeRegisterNumber");
        }

        if (seller.Address is null)
        {
            throw Error("BR-08", "Seller postal address (BG-5) is required.", "Seller.Address");
        }

        ResolvePostalAddress(seller.Address, PartyRole.Seller);

        // v1 scope: the library issues invoices for Romanian sellers only.
        if (NormalizeCountryCode(seller.Address.CountryCode) != RomaniaCountryCode)
        {
            throw Error("scope-v1", $"The seller address must be in Romania (CountryCode 'RO'); got '{seller.Address.CountryCode}'.", "Seller.Address.CountryCode");
        }
    }

    private static void ValidateLines(EInvoiceDocument document)
    {
        // BR-16: an invoice shall have at least one invoice line (BG-25).
        if (document.Lines is null || document.Lines.Count == 0)
        {
            throw Error("BR-16", "The invoice must have at least one line (BG-25).", "Lines");
        }

        bool sellerIsVatPayer = document.Seller.IsVatPayer;

        for (int i = 0; i < document.Lines.Count; i++)
        {
            int number = i + 1;
            string path = $"Lines[{i}]";
            EInvoiceLine? line = document.Lines[i];

            if (line is null)
            {
                throw Error("BR-16", $"Invoice line {number} must not be null.", path);
            }

            // BR-25: item name (BT-153) is mandatory.
            if (string.IsNullOrWhiteSpace(line.Name))
            {
                throw Error("BR-25", $"Item name (BT-153) is required (line {number}).", $"{path}.Name");
            }

            // BR-RO-L1024 (reported by ANAF as BR-RO-L100): item name at most 100 characters.
            if (NormalizedLength(line.Name) > MaxItemNameLength)
            {
                throw Error("BR-RO-L1024", $"Item name (BT-153) must not exceed {MaxItemNameLength} characters (line {number}).", $"{path}.Name");
            }

            // BR-RO-L212: item description (BT-154) at most 200 characters.
            if (NormalizedLength(line.Description) > MaxItemDescriptionLength)
            {
                throw Error("BR-RO-L212", $"Item description (BT-154) must not exceed {MaxItemDescriptionLength} characters (line {number}).", $"{path}.Description");
            }

            // BR-RO-L303: invoice line note (BT-127) at most 300 characters.
            if (NormalizedLength(line.Note) > MaxNoteLength)
            {
                throw Error("BR-RO-L303", $"Invoice line note (BT-127) must not exceed {MaxNoteLength} characters (line {number}).", $"{path}.Note");
            }

            // BR-22: invoiced quantity (BT-129) is mandatory; zero is treated as missing (local validator BR-22).
            if (line.Quantity == 0m)
            {
                throw Error("BR-22", $"Invoiced quantity (BT-129) must not be zero (line {number}).", $"{path}.Quantity");
            }

            // BT-129 precision: at most 3 decimals so the emitted value is exact (micro-taxe E4 parity).
            if (!HasAtMostDecimals(line.Quantity, MaxQuantityDecimals))
            {
                throw Error("BT-129", $"Invoiced quantity (BT-129) must have at most {MaxQuantityDecimals} decimals; got {Invariant(line.Quantity)} (line {number}).", $"{path}.Quantity");
            }

            // BR-27: the item net price (BT-146) shall not be negative.
            if (line.UnitPrice < 0m)
            {
                throw Error("BR-27", $"Unit price must not be negative (line {number}).", $"{path}.UnitPrice");
            }

            // BT-146 precision: at most 4 decimals so the emitted PriceAmount is exact (micro-taxe E4 parity).
            if (!HasAtMostDecimals(line.UnitPrice, MaxPriceDecimals))
            {
                throw Error("BT-146", $"PriceAmount (BT-146) must have at most {MaxPriceDecimals} decimals; got {Invariant(line.UnitPrice)} (line {number}).", $"{path}.UnitPrice");
            }

            // BT-131 = BT-129 x BT-146 must fit System.Decimal (decimal arithmetic throws OverflowException).
            if (!FitsDecimalProduct(line.Quantity, line.UnitPrice, out OverflowException? overflow))
            {
                throw Error("BT-131", $"Line net amount (BT-131) = quantity (BT-129) {Invariant(line.Quantity)} x unit price (BT-146) {Invariant(line.UnitPrice)} is outside the System.Decimal range (line {number}).", path, overflow);
            }

            // BR-23: the unit of measure (BT-130) is mandatory.
            if (string.IsNullOrWhiteSpace(line.UnitCode))
            {
                throw Error("BR-23", $"Unit code (BT-130) is required (line {number}).", $"{path}.UnitCode");
            }

            // BR-CL-23: unit code from UN/ECE Rec 20 with Rec 21 extension.
            if (!OfficialCodeLists.UnitCodes.Contains(line.UnitCode.Trim()))
            {
                throw Error("BR-CL-23", $"Unit code '{line.UnitCode}' is not in the UN/ECE Recommendation 20/21 list (line {number}).", $"{path}.UnitCode");
            }

            ValidateLineVat(line, number, path, sellerIsVatPayer);
        }
    }

    private static void ValidateLineVat(EInvoiceLine line, int number, string path, bool sellerIsVatPayer)
    {
        // BR-CL-18: the line VAT category must be one of the supported UNCL5305 codes (S, E, O).
        if (!Enum.IsDefined(line.VatCategory))
        {
            throw Error("BR-CL-18", $"Unknown VAT category {(int)line.VatCategory} (line {number}).", $"{path}.VatCategory");
        }

        // BT-152 precision: at most 2 decimals.
        if (!HasAtMostDecimals(line.VatRate, MaxRateDecimals))
        {
            throw Error("BT-152", $"VAT rate (BT-152) must have at most {MaxRateDecimals} decimals; got {Invariant(line.VatRate)} (line {number}).", $"{path}.VatRate");
        }

        switch (line.VatCategory)
        {
            case EInvoiceVatCategory.Standard:
                // BR-S-05: a standard rated line has a VAT rate greater than zero.
                if (line.VatRate <= 0m)
                {
                    throw Error("BR-S-05", $"A Standard rated line must have a VAT rate greater than zero (line {number}).", $"{path}.VatRate");
                }

                // BR-S-02: S lines need the seller VAT identifier (BT-31), which a non-payer does not have.
                if (!sellerIsVatPayer)
                {
                    throw Error("BR-S-02", $"Line {number} is Standard rated (S) but the seller is not VAT-registered (IsVatPayer=false); a non-payer issues NotSubject (O) lines only (BR-O-11/BR-O-12).", $"{path}.VatCategory");
                }

                break;

            case EInvoiceVatCategory.Exempt:
                // BR-E-05: an exempt line has VAT rate 0.
                if (line.VatRate != 0m)
                {
                    throw Error("BR-E-05", $"An Exempt line must have VAT rate 0 (line {number}).", $"{path}.VatRate");
                }

                // BR-E-02: E lines need the seller VAT identifier (BT-31), which a non-payer does not have.
                if (!sellerIsVatPayer)
                {
                    throw Error("BR-E-02", $"Line {number} is Exempt (E) but the seller is not VAT-registered (IsVatPayer=false); a non-payer issues NotSubject (O) lines only (BR-O-11/BR-O-12).", $"{path}.VatCategory");
                }

                break;

            case EInvoiceVatCategory.NotSubject:
                // BR-O-05: a not-subject line carries no VAT rate.
                if (line.VatRate != 0m)
                {
                    throw Error("BR-O-05", $"A NotSubject line must not carry a VAT rate (line {number}).", $"{path}.VatRate");
                }

                // BR-O-02: an O invoice carries no seller VAT identifier, which a VAT payer must emit for S/E.
                if (sellerIsVatPayer)
                {
                    throw Error("BR-O-02", $"Line {number} is NotSubject (O) but the seller is VAT-registered (IsVatPayer=true); BR-O-02 forbids the seller VAT identifier (BT-31) on invoices with O lines.", $"{path}.VatCategory");
                }

                break;
        }
    }

    private static void ValidateBuyer(EInvoiceDocument document)
    {
        EInvoiceBuyer? buyer = document.Buyer;

        // BR-07/BR-10: the buyer (BG-7) with its name and postal address is mandatory.
        if (buyer is null)
        {
            throw Error("BR-07", "Buyer (BG-7) is required.", "Buyer");
        }

        if (string.IsNullOrWhiteSpace(buyer.Name))
        {
            throw Error("BR-07", "Buyer name (BT-44) is required.", "Buyer.Name");
        }

        // BR-RO-L203: buyer name at most 200 characters.
        if (NormalizedLength(buyer.Name) > MaxPartyNameLength)
        {
            throw Error("BR-RO-L203", $"Buyer name (BT-44) must not exceed {MaxPartyNameLength} characters.", "Buyer.Name");
        }

        if (buyer.Address is null)
        {
            throw Error("BR-10", "Buyer postal address (BG-8) is required.", "Buyer.Address");
        }

        ResolvePostalAddress(buyer.Address, PartyRole.Buyer);

        bool emitsVatId = EmitsBuyerVatId(document);

        // BR-CO-09: a VAT identifier starts with a prefix from the official list (checked on the trimmed,
        // upper-cased value that is emitted; on NotSubject invoices BR-O-02 omits BT-48).
        if (emitsVatId && !OfficialCodeLists.VatIdPrefixes.Contains(VatIdPrefix(NormalizeVatId(buyer.VatId!))))
        {
            throw Error("BR-CO-09", $"Buyer VAT identifier (BT-48) must start with a country prefix from the BR-CO-09 list (e.g. RO, DE, EL); got '{buyer.VatId}'.", "Buyer.VatId");
        }

        if (buyer.IsNaturalPerson || emitsVatId || BuyerLegalId(document) is not null)
        {
            return;
        }

        // BR-RO-120 (RO16931-rules.sch:409-415): with any S/Z/E/AE/K/G/L/M line, BT-47 or BT-48 must be present,
        // for a buyer in any country. A natural person always gets BT-47 (the CNP or 13 zeros).
        bool anyStandardOrExempt = document.Lines.Any(line =>
            line.VatCategory is EInvoiceVatCategory.Standard or EInvoiceVatCategory.Exempt);
        if (anyStandardOrExempt)
        {
            throw Error("BR-RO-120", "The buyer must have a VAT identifier (BT-48) or a legal registration identifier (BT-47).", "Buyer");
        }

        // NotSubject (O) invoice: BR-O-02 omits BT-48, and only an RO VatId can stand in as BT-47 (its CUI
        // digits). Without an emitted identifier ANAF cannot identify the buyer (ERRIdentif), so BR-RO-120's
        // "BT-47 or BT-48" requirement is applied here as well.
        string given = string.IsNullOrWhiteSpace(buyer.VatId)
            ? "no VatId was given"
            : $"VatId '{buyer.VatId}' is not RO followed by 2-10 digits";
        throw Error("BR-RO-120", $"The buyer has no identifier to emit: BT-48 (VatId) is omitted on NotSubject (O) invoices (BR-O-02), and only an RO VatId is converted to BT-47 (its CUI digits); {given}. Pass the buyer's legal registration identifier (LegalRegistrationId, BT-47).", "Buyer.LegalRegistrationId");
    }

    private static void ValidateExemption(EInvoiceDocument document)
    {
        if (!document.Lines.Any(line => line.VatCategory == EInvoiceVatCategory.Exempt))
        {
            return;
        }

        EInvoiceVatExemption? exemption = document.VatExemption;

        // BR-E-10: the Exempt breakdown carries a reason code (BT-121) and/or text (BT-120).
        if (exemption is null
            || (string.IsNullOrWhiteSpace(exemption.ReasonCode) && string.IsNullOrWhiteSpace(exemption.Reason)))
        {
            throw Error("BR-E-10", "An Exempt (E) line requires the document VatExemption reason code (BT-121) and/or text (BT-120).", "VatExemption");
        }

        // BR-CL-22: the reason code belongs to the CEF VATEX code list.
        if (!string.IsNullOrWhiteSpace(exemption.ReasonCode)
            && !exemption.ReasonCode.Trim().StartsWith("VATEX-", StringComparison.OrdinalIgnoreCase))
        {
            throw Error("BR-CL-22", $"VAT exemption reason code (BT-121) must be a VATEX code; got '{exemption.ReasonCode}'.", "VatExemption.ReasonCode");
        }

        // BT-120 is passed through up to 200 characters (micro-taxe E4 parity).
        if (NormalizedLength(exemption.Reason) > MaxExemptionReasonLength)
        {
            throw Error("BT-120", $"VAT exemption reason text (BT-120) must not exceed {MaxExemptionReasonLength} characters.", "VatExemption.Reason");
        }
    }

    private static void ValidatePayment(EInvoiceDocument document)
    {
        if (document.Payment is { } payment)
        {
            // BR-61: payment means 30 (credit transfer) requires the payment account identifier (BT-84).
            if (string.IsNullOrWhiteSpace(payment.Iban))
            {
                throw Error("BR-61", "The payee IBAN (BT-84) is required for a credit transfer (payment means 30).", "Payment.Iban");
            }

            // BT-84: ISO 13616 IBAN with a valid mod-97 checksum.
            if (!IsValidIban(NormalizeIban(payment.Iban)))
            {
                throw Error("BT-84", $"'{payment.Iban}' is not a valid IBAN (ISO 13616 mod-97 check failed).", "Payment.Iban");
            }
        }

        // BR-CO-25: a positive amount due (BT-115) requires a due date (BT-9) or payment terms (BT-20).
        EInvoiceTotals totals = CalculateTotals(document);
        if (totals.PayableAmount > 0m && document.DueDate is null && string.IsNullOrWhiteSpace(document.PaymentTerms))
        {
            throw Error("BR-CO-25", "A positive amount due (BT-115) requires a due date (BT-9) or payment terms (BT-20).", "DueDate");
        }
    }

    /// <summary>
    /// Computes the totals; an <see cref="OverflowException"/> of the sums (BT-106, BT-110, BT-112, BT-115) or
    /// of a group's VAT (BT-117) becomes a labeled <see cref="ArgumentException"/>.
    /// </summary>
    private static EInvoiceTotals CalculateTotals(EInvoiceDocument document)
    {
        try
        {
            return EInvoiceTotalsCalculator.Calculate(document);
        }
        catch (OverflowException exception)
        {
            throw Error("BT-106", "The invoice totals (BT-106 line sum, BT-117/BT-110 VAT, BT-112/BT-115 totals) are outside the System.Decimal range.", "Lines", exception);
        }
    }

    /// <summary>
    /// Every text the builder emits must consist of XML 1.0 characters (<c>Char</c> production: no C0 controls
    /// other than tab/CR/LF, no U+FFFE/U+FFFF, no lone surrogates); otherwise the XML writer would fail without
    /// a rule id. Checks the emitted (trimmed) form; values reduced to digits, ISO codes or list codes are safe.
    /// </summary>
    private static void ValidateXmlCharacters(EInvoiceDocument document)
    {
        CheckXmlText(document.Number, "BT-1", "Invoice number (BT-1)", "Number");

        IReadOnlyList<string> notes = document.Notes ?? [];
        for (int i = 0; i < notes.Count; i++)
        {
            CheckXmlText(notes[i], "BT-22", $"Invoice note {i + 1} (BT-22)", $"Notes[{i}]");
        }

        CheckXmlText(document.PaymentTerms, "BT-20", "Payment terms (BT-20)", "PaymentTerms");
        CheckXmlText(document.BillingReference?.Number, "BT-25", "Preceding invoice number (BT-25)", "BillingReference.Number");

        EInvoiceSeller seller = document.Seller;
        CheckXmlText(seller.Name, "BT-27", "Seller name (BT-27)", "Seller.Name");
        CheckXmlText(seller.TradeRegisterNumber, "BT-33", "Seller trade register number (BT-33)", "Seller.TradeRegisterNumber");
        CheckXmlAddress(seller.Address, PartyRole.Seller);

        EInvoiceBuyer buyer = document.Buyer;
        CheckXmlText(buyer.Name, "BT-44", "Buyer name (BT-44)", "Buyer.Name");
        CheckXmlText(buyer.LegalRegistrationId, "BT-47", "Buyer legal registration identifier (BT-47)", "Buyer.LegalRegistrationId");
        if (EmitsBuyerVatId(document))
        {
            CheckXmlText(buyer.VatId, "BT-48", "Buyer VAT identifier (BT-48)", "Buyer.VatId");
        }

        CheckXmlAddress(buyer.Address, PartyRole.Buyer);

        if (document.VatExemption is { } exemption
            && document.Lines.Any(line => line.VatCategory == EInvoiceVatCategory.Exempt))
        {
            CheckXmlText(exemption.ReasonCode, "BT-121", "VAT exemption reason code (BT-121)", "VatExemption.ReasonCode");
            CheckXmlText(exemption.Reason, "BT-120", "VAT exemption reason text (BT-120)", "VatExemption.Reason");
        }

        for (int i = 0; i < document.Lines.Count; i++)
        {
            EInvoiceLine line = document.Lines[i];
            string path = $"Lines[{i}]";
            CheckXmlText(line.Note, "BT-127", $"Invoice line note (BT-127) of line {i + 1}", $"{path}.Note");
            CheckXmlText(line.Name, "BT-153", $"Item name (BT-153) of line {i + 1}", $"{path}.Name");
            CheckXmlText(line.Description, "BT-154", $"Item description (BT-154) of line {i + 1}", $"{path}.Description");
        }
    }

    private static void CheckXmlAddress(EInvoiceAddress address, PartyRole role)
    {
        bool seller = role == PartyRole.Seller;
        string prefix = seller ? "Seller.Address" : "Buyer.Address";
        string party = seller ? "Seller" : "Buyer";
        ResolvedAddress resolved = ResolvePostalAddress(address, role);

        CheckXmlText(resolved.Street, seller ? "BT-35" : "BT-50", $"{party} street ({(seller ? "BT-35" : "BT-50")})", $"{prefix}.Street");
        CheckXmlText(resolved.City, seller ? "BT-37" : "BT-52", $"{party} city ({(seller ? "BT-37" : "BT-52")})", $"{prefix}.City");
        CheckXmlText(resolved.PostalCode, seller ? "BT-38" : "BT-53", $"{party} post code ({(seller ? "BT-38" : "BT-53")})", $"{prefix}.PostalCode");
    }

    /// <summary>Throws <c>[termId] … contains U+XXXX, not allowed in XML 1.0</c> for the first non-XML character.</summary>
    private static void CheckXmlText(string? value, string termId, string label, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        string emitted = value.Trim();
        for (int i = 0; i < emitted.Length; i++)
        {
            char c = emitted[i];
            if (XmlConvert.IsXmlChar(c))
            {
                continue;
            }

            // A supplementary-plane character is a high surrogate followed by a low surrogate.
            if (i + 1 < emitted.Length && XmlConvert.IsXmlSurrogatePair(lowChar: emitted[i + 1], highChar: c))
            {
                i++;
                continue;
            }

            throw Error(termId, $"{label} contains U+{(int)c:X4}, not allowed in XML 1.0.", paramName);
        }
    }

    /// <summary>
    /// Validates and converts a postal address to the values the builder emits. Romanian addresses get
    /// the ISO 3166-2:RO county (BR-RO-110/111) and, in Bucharest, the SECTOR-RO city (BR-RO-100/101);
    /// foreign addresses get no country subdivision.
    /// </summary>
    internal static ResolvedAddress ResolvePostalAddress(EInvoiceAddress address, PartyRole role)
    {
        bool seller = role == PartyRole.Seller;
        string prefix = seller ? "Seller.Address" : "Buyer.Address";
        string party = seller ? "Seller" : "Buyer";

        // BR-09/BR-11: the country code (BT-40/BT-55) is mandatory.
        if (string.IsNullOrWhiteSpace(address.CountryCode))
        {
            throw Error(seller ? "BR-09" : "BR-11", $"{party} country code ({(seller ? "BT-40" : "BT-55")}) is required.", $"{prefix}.CountryCode");
        }

        string countryCode = NormalizeCountryCode(address.CountryCode);

        // BR-CL-14: ISO 3166-1 alpha-2 country code.
        if (!OfficialCodeLists.CountryCodes.Contains(countryCode))
        {
            throw Error("BR-CL-14", $"{party} country code '{address.CountryCode}' is not an ISO 3166-1 alpha-2 code.", $"{prefix}.CountryCode");
        }

        // BR-RO-081/082: address line 1 (BT-35/BT-50) is mandatory.
        if (string.IsNullOrWhiteSpace(address.Street))
        {
            throw Error(seller ? "BR-RO-081" : "BR-RO-082", $"{party} street (address line 1) is required.", $"{prefix}.Street");
        }

        // BR-RO-L151/152: address line 1 at most 150 characters.
        if (NormalizedLength(address.Street) > MaxStreetLength)
        {
            throw Error(seller ? "BR-RO-L151" : "BR-RO-L152", $"{party} street (address line 1) must not exceed {MaxStreetLength} characters.", $"{prefix}.Street");
        }

        // BR-RO-091/092: city (BT-37/BT-52) is mandatory.
        if (string.IsNullOrWhiteSpace(address.City))
        {
            throw Error(seller ? "BR-RO-091" : "BR-RO-092", $"{party} city is required.", $"{prefix}.City");
        }

        // BR-RO-L0201/0202: post code at most 20 characters.
        if (NormalizedLength(address.PostalCode) > MaxPostalCodeLength)
        {
            throw Error(seller ? "BR-RO-L0201" : "BR-RO-L0202", $"{party} post code must not exceed {MaxPostalCodeLength} characters.", $"{prefix}.PostalCode");
        }

        string? countySubentity = null;
        string city = address.City.Trim();

        if (countryCode == RomaniaCountryCode)
        {
            // BR-RO-110/111: a Romanian address carries an ISO 3166-2:RO county code.
            if (!RomanianAddressConverter.TryToCountyCode(address.County, out string countyCode))
            {
                string reason = string.IsNullOrWhiteSpace(address.County)
                    ? "a Romanian address requires the county"
                    : $"unknown Romanian county '{address.County}'";
                throw Error(seller ? "BR-RO-110" : "BR-RO-111", $"{party} address: {reason}.", $"{prefix}.County");
            }

            // BR-RO-100/101: a Bucharest address carries SECTOR1..SECTOR6 as its city.
            if (countyCode == RomanianConstants.BucharestCountyCode)
            {
                if (!RomanianAddressConverter.TryToBucharestSector(address.City, out string sector))
                {
                    throw Error(seller ? "BR-RO-100" : "BR-RO-101", $"{party} address: a Bucharest address requires a sector (1-6) in the city; got '{address.City}'.", $"{prefix}.City");
                }

                city = sector;
            }

            countySubentity = countyCode;
        }

        // BR-RO-L0501/0502: city at most 50 characters (measured on the emitted value).
        if (NormalizedLength(city) > MaxCityLength)
        {
            throw Error(seller ? "BR-RO-L0501" : "BR-RO-L0502", $"{party} city must not exceed {MaxCityLength} characters.", $"{prefix}.City");
        }

        return new ResolvedAddress(
            address.Street.Trim(),
            city,
            string.IsNullOrWhiteSpace(address.PostalCode) ? null : address.PostalCode.Trim(),
            countySubentity,
            countryCode);
    }

    /// <summary>BT-48 is emitted only when set and the invoice has no NotSubject line (BR-O-02).</summary>
    internal static bool EmitsBuyerVatId(EInvoiceDocument document)
    {
        return !string.IsNullOrWhiteSpace(document.Buyer.VatId) && document.Seller.IsVatPayer;
    }

    /// <summary>
    /// BT-47: the given id; else, for a company buyer whose BT-48 is not emitted (NotSubject invoice, BR-O-02),
    /// the CUI digits of an <c>RO</c> VatId; else the 13-zero identifier for a natural person without a CNP.
    /// </summary>
    internal static string? BuyerLegalId(EInvoiceDocument document)
    {
        EInvoiceBuyer buyer = document.Buyer;
        if (!string.IsNullOrWhiteSpace(buyer.LegalRegistrationId))
        {
            return buyer.LegalRegistrationId.Trim();
        }

        if (!buyer.IsNaturalPerson
            && !EmitsBuyerVatId(document)
            && !string.IsNullOrWhiteSpace(buyer.VatId)
            && NormalizeVatId(buyer.VatId).StartsWith(RomaniaCountryCode, StringComparison.Ordinal))
        {
            return NormalizeCui(buyer.VatId);
        }

        return buyer.IsNaturalPerson ? EInvoiceBuyer.NaturalPersonWithoutCnpId : null;
    }

    /// <summary>BT-48 as emitted and checked by BR-CO-09: trimmed and upper-cased (<c>ro123</c> → <c>RO123</c>).</summary>
    internal static string NormalizeVatId(string vatId)
    {
        return vatId.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Strips whitespace, upper-cases (as <see cref="NormalizeVatId"/>) and drops a leading RO prefix; returns
    /// the 2-10 digit CUI, or null when invalid.
    /// </summary>
    internal static string? NormalizeCui(string? cui)
    {
        if (string.IsNullOrWhiteSpace(cui))
        {
            return null;
        }

        string compact = string.Concat(cui.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        if (compact.StartsWith(RomaniaCountryCode, StringComparison.Ordinal))
        {
            compact = compact[2..];
        }

        return compact.Length is >= 2 and <= 10 && compact.All(char.IsAsciiDigit) ? compact : null;
    }

    /// <summary>The first two characters of the trimmed VAT identifier, as BR-CO-09's <c>substring(., 1, 2)</c>.</summary>
    private static string VatIdPrefix(string vatId)
    {
        string trimmed = vatId.Trim();
        return trimmed.Length >= 2 ? trimmed[..2] : trimmed;
    }

    internal static string NormalizeIban(string iban)
    {
        return string.Concat(iban.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }

    internal static string NormalizeCountryCode(string countryCode)
    {
        return countryCode.Trim().ToUpperInvariant();
    }

    /// <summary>ISO 13616: 2 letters, 2 check digits, up to 30 alphanumerics; the mod-97 remainder is 1.</summary>
    private static bool IsValidIban(string iban)
    {
        if (iban.Length is < 15 or > 34
            || !char.IsAsciiLetterUpper(iban[0])
            || !char.IsAsciiLetterUpper(iban[1])
            || !char.IsAsciiDigit(iban[2])
            || !char.IsAsciiDigit(iban[3])
            || !iban.All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c)))
        {
            return false;
        }

        string rearranged = iban[4..] + iban[..4];
        BigInteger remainder = BigInteger.Zero;
        foreach (char c in rearranged)
        {
            int value = char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10;
            remainder = (remainder * (value < 10 ? 10 : 100) + value) % 97;
        }

        return remainder == BigInteger.One;
    }

    /// <summary>Value-based decimal check (so 21.000m and 1.20000m pass): Round(v, n) == v.</summary>
    private static bool HasAtMostDecimals(decimal value, int decimals)
    {
        return decimal.Round(value, decimals) == value;
    }

    /// <summary>False when <paramref name="left"/> x <paramref name="right"/> overflows System.Decimal.</summary>
    private static bool FitsDecimalProduct(decimal left, decimal right, out OverflowException? overflow)
    {
        try
        {
            _ = left * right;
            overflow = null;
            return true;
        }
        catch (OverflowException exception)
        {
            overflow = exception;
            return false;
        }
    }

    /// <summary>
    /// Mirrors the schematron's <c>string-length(normalize-space(.))</c> on the value as emitted. The builder
    /// emits <c>value.Trim()</c> (.NET trims every Unicode white space); XPath <c>normalize-space</c> then trims
    /// and collapses only XML white space (<c>[ \t\r\n]+</c>), so a no-break space (U+00A0) or any other
    /// Unicode space inside the value is counted character by character.
    /// </summary>
    private static int NormalizedLength(string? value)
    {
        return string.IsNullOrEmpty(value) ? 0 : NormalizeSpace(value.Trim()).Length;
    }

    /// <summary>XPath 1.0 <c>normalize-space</c>: trims and collapses runs of space, tab, CR and LF only.</summary>
    private static string NormalizeSpace(string value)
    {
        return XmlWhitespaceRun.Replace(value.Trim(XmlWhitespace), " ");
    }

    private static string Invariant(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static ArgumentException Error(string ruleId, string message, string paramName, Exception? innerException = null)
    {
        return new ArgumentException($"[{ruleId}] {message}", paramName, innerException);
    }
}

/// <summary>Which party an address belongs to; selects the seller or buyer rule ids.</summary>
internal enum PartyRole
{
    Seller,
    Buyer
}

/// <summary>A postal address as emitted: trimmed values, ISO county code and SECTOR city for Romania.</summary>
internal sealed record ResolvedAddress(
    string Street,
    string City,
    string? PostalCode,
    string? CountySubentity,
    string CountryCode);
