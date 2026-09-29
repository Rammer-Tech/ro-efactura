namespace RoEFactura.Generation;

/// <summary>
/// The buyer (BG-7): a Romanian or foreign company, or a natural person.
/// </summary>
public sealed record EInvoiceBuyer
{
    /// <summary>
    /// The conventional buyer identifier (BT-47) accepted by ANAF for a natural person without a CNP.
    /// </summary>
    public const string NaturalPersonWithoutCnpId = "0000000000000";

    /// <summary>Buyer name (BT-44), at most 200 characters (BR-RO-L203).</summary>
    public required string Name { get; init; }

    /// <summary>Buyer postal address (BG-8). A Romanian address requires the county.</summary>
    public required EInvoiceAddress Address { get; init; }

    /// <summary>
    /// Whether the buyer is a natural person. A natural person always gets a legal identifier (BT-47):
    /// <see cref="LegalRegistrationId"/> (the CNP) when set, otherwise <see cref="NaturalPersonWithoutCnpId"/>.
    /// </summary>
    public bool IsNaturalPerson { get; init; }

    /// <summary>
    /// Buyer legal registration identifier (BT-47): the CUI of a company or the CNP of a person. ANAF identifies
    /// the buyer from an <c>RO</c>-prefixed <see cref="VatId"/> or from an all-digit, checksum-valid BT-47; a
    /// foreign buyer without a Romanian CUI/NIF passes ANAF's validator only with <c>0000000000000</c> here
    /// (a foreign VAT id or registry id alone is rejected with <c>ERRIdentif</c>; see docs/XML_GENERATION.md).
    /// </summary>
    public string? LegalRegistrationId { get; init; }

    /// <summary>
    /// Buyer VAT identifier (BT-48), prefixed with a country code from the BR-CO-09 list (e.g. <c>RO</c>,
    /// <c>DE</c>, <c>EL</c>); emitted trimmed and upper-cased. Omitted on NotSubject (O) invoices, where BR-O-02
    /// forbids it: there a company buyer without <see cref="LegalRegistrationId"/> gets the CUI digits of an
    /// <c>RO</c> VatId as BT-47, and any other VatId leaves it without an identifier, which the generator
    /// rejects with <c>[BR-RO-120]</c> — pass <see cref="LegalRegistrationId"/> in that case.
    /// </summary>
    public string? VatId { get; init; }
}
