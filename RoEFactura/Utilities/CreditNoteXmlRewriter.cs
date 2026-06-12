using System.Xml.Linq;

namespace RoEFactura.Utilities;

/// <summary>
///     UBL 2.1 emits Credit Notes with either an <c>&lt;Invoice InvoiceTypeCode="381"&gt;</c> envelope
///     OR a standalone <c>&lt;CreditNote xmlns="…:CreditNote-2"&gt;</c> root. UblSharp 1.1.1 only
///     generates a <c>InvoiceType</c> for the Invoice-2 schema, so CreditNote-rooted documents fail
///     <see cref="System.Xml.Serialization.XmlSerializer"/> deserialization.
///
///     CreditNote-2 and Invoice-2 share identical <c>cac:</c> / <c>cbc:</c> aggregate components
///     (AccountingSupplierParty, LegalMonetaryTotal, TaxTotal, etc.). Only the root element + the
///     type-code element + line-level names differ. Rewriting the root namespace to Invoice-2,
///     remapping <c>CreditNoteTypeCode</c> → <c>InvoiceTypeCode</c> and <c>CreditNoteLine</c> →
///     <c>InvoiceLine</c> (with <c>CreditedQuantity</c> → <c>InvoicedQuantity</c>) lets the existing
///     <c>InvoiceType</c> pipeline parse credit notes losslessly for the fields downstream
///     consumers (CompanySyncJob, validators, party extensions) actually read.
/// </summary>
public static class CreditNoteXmlRewriter
{
    public const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string CreditNoteNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    public const string CbcNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public const string CacNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    /// <summary>
    ///     True when <paramref name="xmlContent"/> has a UBL CreditNote root that needs rewriting
    ///     before passing through the Invoice deserializer.
    /// </summary>
    public static bool IsCreditNoteRoot(string? xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return false;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            return doc.Root != null
                && doc.Root.Name.LocalName == "CreditNote"
                && doc.Root.Name.NamespaceName == CreditNoteNamespace;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Rewrites a UBL CreditNote document into an UBL Invoice document with
    ///     <c>InvoiceTypeCode = 381</c>. Returns the original string when the root is not a
    ///     CreditNote.
    /// </summary>
    public static string RewriteToInvoice(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return xmlContent;

        var doc = XDocument.Parse(xmlContent);
        XElement? root = doc.Root;
        if (root == null || root.Name.LocalName != "CreditNote" || root.Name.NamespaceName != CreditNoteNamespace)
            return xmlContent;

        XNamespace invoiceNs = InvoiceNamespace;
        XNamespace cbc = CbcNamespace;
        XNamespace cac = CacNamespace;

        // Build the rewritten root with explicit Invoice-2 namespace.
        var rewritten = new XElement(invoiceNs + "Invoice");
        foreach (var attribute in root.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
                continue;
            rewritten.Add(new XAttribute(attribute.Name, attribute.Value));
        }

        // Preserve common namespace declarations explicitly so downstream serializers can find cbc:/cac:.
        rewritten.Add(new XAttribute(XNamespace.Xmlns + "cbc", CbcNamespace));
        rewritten.Add(new XAttribute(XNamespace.Xmlns + "cac", CacNamespace));

        bool sawInvoiceTypeCode = false;

        foreach (var child in root.Elements())
        {
            XElement projected = RewriteElement(child, invoiceNs, cbc, cac);

            if (projected.Name == cbc + "InvoiceTypeCode")
                sawInvoiceTypeCode = true;

            rewritten.Add(projected);
        }

        // ANAF SPV credit notes occasionally omit InvoiceTypeCode entirely — add 381 so downstream
        // mapping treats them consistently with type-381 invoices.
        if (!sawInvoiceTypeCode)
        {
            var invoiceTypeCode = new XElement(cbc + "InvoiceTypeCode", "381");

            // Insert near the top, after UBLVersionID/CustomizationID/ProfileID/ID/IssueDate if present.
            var anchor = rewritten.Elements(cbc + "IssueDate").FirstOrDefault()
                         ?? rewritten.Elements(cbc + "ID").FirstOrDefault();
            if (anchor != null)
                anchor.AddAfterSelf(invoiceTypeCode);
            else
                rewritten.AddFirst(invoiceTypeCode);
        }

        var output = new XDocument(doc.Declaration, rewritten);
        return output.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement RewriteElement(XElement source, XNamespace invoiceNs, XNamespace cbc, XNamespace cac)
    {
        XName newName = source.Name;

        // Map CreditNote-specific element names to their Invoice equivalents.
        if (source.Name.Namespace == cbc)
        {
            if (source.Name.LocalName == "CreditNoteTypeCode")
                newName = cbc + "InvoiceTypeCode";
            else if (source.Name.LocalName == "CreditedQuantity")
                newName = cbc + "InvoicedQuantity";
        }
        else if (source.Name.Namespace == cac)
        {
            if (source.Name.LocalName == "CreditNoteLine")
                newName = cac + "InvoiceLine";
        }

        var rewritten = new XElement(newName);

        foreach (var attribute in source.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
                continue;
            rewritten.Add(new XAttribute(attribute.Name, attribute.Value));
        }

        foreach (var node in source.Nodes())
        {
            if (node is XElement childElement)
                rewritten.Add(RewriteElement(childElement, invoiceNs, cbc, cac));
            else
                rewritten.Add(node);
        }

        return rewritten;
    }
}
