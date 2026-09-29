namespace RoEFactura.Generation;

/// <summary>
/// Generates a CIUS-RO (1.0.1 specification identifier, validation artifacts 1.0.9) UBL 2.1 Invoice
/// document from an <see cref="EInvoiceDocument"/>.
/// </summary>
public interface IEInvoiceXmlGenerator
{
    /// <summary>
    /// Validates <paramref name="document"/>, computes its totals and returns the UBL 2.1 Invoice XML
    /// (TypeCode 380) as UTF-8 bytes without a byte order mark, starting with
    /// <c>&lt;?xml version="1.0" encoding="utf-8"?&gt;</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The document breaks a CIUS-RO/EN 16931 rule or a v1 limit. The message starts with the rule id in
    /// brackets, e.g. <c>[BR-27]</c>.
    /// </exception>
    byte[] Generate(EInvoiceDocument document);
}
