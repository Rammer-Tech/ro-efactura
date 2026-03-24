namespace RoEFactura.Utilities;

/// <summary>
/// ANAF download ZIPs may include XML sidecars for digital signatures; those are not UBL invoice documents.
/// </summary>
internal static class EInvoiceXmlFileFilter
{
    /// <summary>
    /// Returns true when the file name (last segment of a path) should be ignored for invoice parsing.
    /// </summary>
    public static bool IsSemnaturaXmlFileName(string? pathOrFileName)
    {
        if (string.IsNullOrEmpty(pathOrFileName))
            return false;

        var name = Path.GetFileName(pathOrFileName.Replace('\\', '/').TrimEnd('/'));
        return name.Contains("semnatura", StringComparison.OrdinalIgnoreCase);
    }
}
