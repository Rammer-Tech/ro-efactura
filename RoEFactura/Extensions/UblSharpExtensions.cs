using System.Xml;
using System.Xml.Serialization;
using UblSharp;

namespace RoEFactura.Extensions;

/// <summary>
/// Core UblSharp extensions for XML serialization/deserialization
/// </summary>
/// <example>
/// <code>
/// var invoice = UblSharpExtensions.LoadInvoiceFromXml(xmlContent);
/// var xml = invoice?.SaveInvoiceToXml();
/// </code>
/// </example>
public static partial class UblSharpExtensions
{
    /// <summary>
    /// Loads an InvoiceType from XML string.
    /// Throws on deserialization errors -- callers should catch and log with context.
    /// </summary>
    public static InvoiceType? LoadInvoiceFromXml(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return null;

        // Let deserialization exceptions propagate -- callers need to know WHY parsing failed
        XmlSerializer serializer = new XmlSerializer(typeof(InvoiceType));
        using StringReader stringReader = new StringReader(xmlContent);
        using XmlReader xmlReader = XmlReader.Create(stringReader);

        return serializer.Deserialize(xmlReader) as InvoiceType;
    }

    /// <summary>
    /// Saves an InvoiceType to XML string
    /// </summary>
    public static string SaveInvoiceToXml(this InvoiceType invoice)
    {
        if (invoice == null)
            throw new ArgumentNullException(nameof(invoice));

        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(InvoiceType));
            using StringWriter stringWriter = new StringWriter();
            using XmlWriter xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = System.Text.Encoding.UTF8,
                OmitXmlDeclaration = false
            });

            // Add UBL namespaces
            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
            namespaces.Add("", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            namespaces.Add("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            namespaces.Add("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

            serializer.Serialize(xmlWriter, invoice, namespaces);
            return stringWriter.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to serialize invoice to XML: {ex.Message}", ex);
        }
    }
}