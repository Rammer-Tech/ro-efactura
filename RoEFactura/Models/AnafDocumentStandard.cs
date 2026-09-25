namespace RoEFactura.Models;

/// <summary>
/// The document standard passed to ANAF for upload, validate and XML→PDF calls.
/// </summary>
public enum AnafDocumentStandard
{
    /// <summary>UBL invoice. Upload query value <c>UBL</c>; validate/pdf standard <c>FACT1</c>.</summary>
    Ubl,

    /// <summary>UBL credit note. Upload query value <c>CN</c>; validate/pdf standard <c>FCN</c>.</summary>
    CreditNote,

    /// <summary>Cross Industry Invoice. Upload query value <c>CII</c>. Not accepted by validate/pdf.</summary>
    Cii,

    /// <summary>Buyer response message (RASP). Upload query value <c>RASP</c>. Not accepted by validate/pdf.</summary>
    Rasp
}
