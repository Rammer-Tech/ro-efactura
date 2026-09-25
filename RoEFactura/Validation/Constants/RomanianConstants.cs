namespace RoEFactura.Validation.Constants;

public static class RomanianConstants
{
    /// <summary>
    /// BT-24 value required by CIUS-RO 1.0.1 (RO_CIUS-rules.sch, RO-CIUS-ID / RO-MAJOR-MINOR-PATCH-VERSION = '1.0.1').
    /// </summary>
    public const string CustomizationId =
        "urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:CIUS-RO:1.0.1";

    /// <summary>
    /// Prior CustomizationID values that identify a document as Romanian for detection purposes only
    /// (e.g. <see cref="Extensions.InvoiceTypeExtensions.IsRomanianInvoice"/>); they are not valid for BR-RO-001.
    /// </summary>
    public static readonly IReadOnlyList<string> LegacyCustomizationIds =
    [
        "urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:RO_CIUS:1.0.0.2021"
    ];

    [Obsolete("Legacy RO_CIUS 1.0.0.2021 identifier; use CustomizationId.")]
    public static readonly string RoCiusCustomizationId = LegacyCustomizationIds[0];

    /// <summary>
    /// ISO 3166-2:RO county codes (with the "RO-" prefix), as used by BT-39/BT-54 and validated by
    /// BR-RO-110/BR-RO-111 (RO16931-rules.sch, $ISO-3166-RO-CODES).
    /// </summary>
    public static readonly IReadOnlyList<string> ValidCountyCodes =
    [
        "RO-AB", "RO-AR", "RO-AG", "RO-B", "RO-BC", "RO-BH", "RO-BN", "RO-BT", "RO-BV", "RO-BR", "RO-BZ",
        "RO-CS", "RO-CL", "RO-CJ", "RO-CT", "RO-CV", "RO-DB", "RO-DJ", "RO-GL", "RO-GR", "RO-GJ",
        "RO-HR", "RO-HD", "RO-IL", "RO-IS", "RO-IF", "RO-MM", "RO-MH", "RO-MS", "RO-NT", "RO-OT",
        "RO-PH", "RO-SM", "RO-SJ", "RO-SB", "RO-SV", "RO-TR", "RO-TM", "RO-TL", "RO-VS", "RO-VL", "RO-VN"
    ];

    /// <summary>Country subdivision code for Bucharest (BT-39/BT-54 = "RO-B"), used by BR-RO-100/BR-RO-101.</summary>
    public const string BucharestCountyCode = "RO-B";

    /// <summary>SECTOR-RO code list (RO16931-rules.sch, $SECTOR-RO-CODES) used by BR-RO-100/BR-RO-101.</summary>
    public static readonly IReadOnlyList<string> BucharestSectorCodes =
        ["SECTOR1", "SECTOR2", "SECTOR3", "SECTOR4", "SECTOR5", "SECTOR6"];

    public static readonly string[] ValidInvoiceTypeCodes = ["380", "389", "384", "381", "751"];

    public static readonly string[] ValidVatPointDateCodes = ["3", "35", "432"];

    /// <summary>BT-1 (Invoice number) maximum length, reported as BR-RO-L200 (schematron assert id BR-RO-L155).</summary>
    public const int InvoiceNumberMaxLength = 200;

    /// <summary>BT-22 (Invoice note) maximum length, reported as BR-RO-L300 (schematron assert id BR-RO-L302).</summary>
    public const int InvoiceNoteMaxLength = 300;

    /// <summary>Maximum number of BG-1 Invoice note occurrences (BR-RO-A020).</summary>
    public const int MaxInvoiceNotes = 20;

    /// <summary>BT-127 (Invoice line note) maximum length, reported as BR-RO-L300 (schematron assert id BR-RO-L303).</summary>
    public const int LineNoteMaxLength = 300;

    /// <summary>BT-153 (Item name) maximum length, reported as BR-RO-L100 (schematron assert id BR-RO-L1024).</summary>
    public const int ItemNameMaxLength = 100;

    /// <summary>BT-154 (Item description) maximum length, reported as BR-RO-L200 (schematron assert id BR-RO-L212).</summary>
    public const int ItemDescriptionMaxLength = 200;
}
