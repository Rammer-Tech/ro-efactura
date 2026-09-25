namespace RoEFactura.Validation.Constants;

/// <summary>
/// Centralized CIUS-RO / RO16931 national rule identifiers, cross-checked (2026-09-25) against the official
/// schematron shipped in ro16931-ubl-1.0.9 (cius-ro/RO16931-rules.sch). These are the codes ANAF echoes back
/// in <c>validare</c> responses (the bracketed text in the assert message), which is not always identical to
/// the schematron assert's own @id attribute -- see docs/VALIDATION_RULES.md for the full mapping.
/// BR-RO-A999 (max 999 invoice lines) does not exist in the official schematron (removed in 1.0.8) and is
/// deliberately not represented here.
/// </summary>
public static class RoCiusRuleIds
{
    /// <summary>BT-24 CustomizationID must equal <see cref="RomanianConstants.CustomizationId"/>.</summary>
    public const string CustomizationId = "BR-RO-001";

    /// <summary>BT-1 Invoice number must contain at least one digit.</summary>
    public const string InvoiceNumberDigit = "BR-RO-010";

    /// <summary>BT-3 Invoice type code must be one of the allowed UNTDID 1001 codes.</summary>
    public const string InvoiceTypeCode = "BR-RO-020";

    /// <summary>BT-6 VAT accounting currency must be RON when BT-5 (document currency) is not RON.</summary>
    public const string VatAccountingCurrency = "BR-RO-030";

    /// <summary>BT-8 VAT point date code must be one of 3, 35, 432 (UNTDID 2005).</summary>
    public const string VatPointDateCode = "BR-RO-040";

    /// <summary>BT-39 = RO-B ⇒ BT-37 (Seller city) must be a SECTOR-RO code.</summary>
    public const string SellerBucharestSector = "BR-RO-100";

    /// <summary>BT-54 = RO-B ⇒ BT-52 (Buyer city) must be a SECTOR-RO code.</summary>
    public const string BuyerBucharestSector = "BR-RO-101";

    /// <summary>BT-40 = RO ⇒ BT-39 (Seller country subdivision) must be a valid ISO 3166-2:RO code.</summary>
    public const string SellerCounty = "BR-RO-110";

    /// <summary>BT-55 = RO ⇒ BT-54 (Buyer country subdivision) must be a valid ISO 3166-2:RO code.</summary>
    public const string BuyerCounty = "BR-RO-111";

    /// <summary>Romanian buyer must have a legal registration ID (BT-47) and/or a VAT identifier (BT-48).</summary>
    public const string BuyerIdentifier = "BR-RO-120";

    /// <summary>Fields limited to 100 characters (e.g. BT-153 Item name).</summary>
    public const string MaxLength100 = "BR-RO-L100";

    /// <summary>Fields limited to 200 characters (e.g. BT-1 Invoice number, BT-154 Item description).</summary>
    public const string MaxLength200 = "BR-RO-L200";

    /// <summary>Fields limited to 300 characters (e.g. BT-22 Invoice note, BT-127 Invoice line note).</summary>
    public const string MaxLength300 = "BR-RO-L300";

    /// <summary>Maximum 20 occurrences of BG-1 (Invoice note).</summary>
    public const string MaxInvoiceNotes = "BR-RO-A020";

    /// <summary>Monetary amounts must have at most 2 decimal places.</summary>
    public const string TwoDecimals = "BR-RO-Z2";
}
