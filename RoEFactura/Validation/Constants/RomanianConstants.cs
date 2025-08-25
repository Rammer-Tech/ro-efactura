namespace RoEFactura.Validation.Constants;

public static class RomanianConstants
{
    public static readonly string[] ValidCountyCodes = new[]
    {
        "AB", "AR", "AG", "B", "BC", "BH", "BN", "BT", "BV", "BR", "BZ", 
        "CS", "CL", "CJ", "CT", "CV", "DB", "DJ", "GL", "GR", "GJ", 
        "HR", "HD", "IL", "IS", "IF", "MM", "MH", "MS", "NT", "OT", 
        "PH", "SM", "SJ", "SB", "SV", "TR", "TM", "TL", "VS", "VL", "VN"
    };

    public static readonly string[] ValidInvoiceTypeCodes = new[] { "380", "389", "384", "381", "751" };
    
    public static readonly string[] ValidVatPointDateCodes = new[] { "3", "35", "432" };
    
    public static readonly string RoCiusCustomizationId = 
        "urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:RO_CIUS:1.0.0.2021";
}