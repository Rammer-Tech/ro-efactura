using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RoEFactura.Generation;

/// <summary>
/// Serializes a generated document to bytes with the same settings as
/// <see cref="Extensions.UblSharpExtensions.SaveInvoiceToXmlBytes"/>: UTF-8 without a byte order mark,
/// two-space indentation and the declaration <c>&lt;?xml version="1.0" encoding="utf-8"?&gt;</c> on the first line.
/// </summary>
internal static class EInvoiceXmlWriter
{
    internal static byte[] ToUtf8Bytes(XDocument document)
    {
        XmlWriterSettings settings = new()
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false
        };

        using MemoryStream stream = new();
        using (XmlWriter writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }
}
