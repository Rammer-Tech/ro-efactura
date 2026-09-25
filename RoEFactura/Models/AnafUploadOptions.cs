namespace RoEFactura.Models;

/// <summary>
/// Options for <see cref="Services.Api.IAnafEInvoiceClient.UploadAsync"/>.
/// </summary>
public sealed class AnafUploadOptions
{
    /// <summary>Document standard (query parameter <c>standard</c>). Defaults to UBL.</summary>
    public AnafDocumentStandard Standard { get; init; } = AnafDocumentStandard.Ubl;

    /// <summary>CIF used to notify the seller of processing errors when the invoice cannot be identified.</summary>
    public required string Cif { get; init; }

    /// <summary>When true, posts to the <c>uploadb2c</c> endpoint instead of <c>upload</c>.</summary>
    public bool IsB2C { get; init; }

    /// <summary>Sets <c>extern=DA</c>: the buyer has no Romanian CUI/NIF.</summary>
    public bool ExternalBuyer { get; init; }

    /// <summary>Sets <c>autofactura=DA</c>: the invoice is issued by the beneficiary on behalf of the supplier.</summary>
    public bool SelfBilling { get; init; }

    /// <summary>Sets <c>executare=DA</c>: the invoice is submitted by the enforcement body on behalf of the debtor.</summary>
    public bool Enforcement { get; init; }
}
