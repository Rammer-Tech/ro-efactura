using System.Xml;
using System.Xml.Serialization;
using UblSharp;

namespace RoEFactura.Extensions;

/// <summary>
/// Core UblSharp extensions for XML serialization/deserialization
/// </summary>
public static partial class UblSharpExtensions
{
    /// <summary>
    /// Loads an InvoiceType from XML string
    /// </summary>
    public static InvoiceType? LoadInvoiceFromXml(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return null;

        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(InvoiceType));
            using StringReader stringReader = new StringReader(xmlContent);
            using XmlReader xmlReader = XmlReader.Create(stringReader);
            
            InvoiceType? invoice = serializer.Deserialize(xmlReader) as InvoiceType;
            return invoice;
        }
        catch (Exception)
        {
            return null;
        }
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