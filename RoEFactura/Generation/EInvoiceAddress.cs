namespace RoEFactura.Generation;

/// <summary>
/// A postal address (BG-5 seller / BG-8 buyer).
/// </summary>
/// <remarks>
/// For a Romanian address (<see cref="CountryCode"/> <c>RO</c>) the <see cref="County"/> is converted to
/// its ISO 3166-2:RO code (BR-RO-110/111) and, for Bucharest, the <see cref="City"/> must carry the sector,
/// converted to <c>SECTOR1</c>..<c>SECTOR6</c> (BR-RO-100/101). See <see cref="RomanianAddressConverter"/>.
/// For a foreign address no country subdivision is emitted.
/// </remarks>
public sealed record EInvoiceAddress
{
    /// <summary>Address line 1 (BT-35/BT-50), required (BR-RO-081/082), at most 150 characters (BR-RO-L151/152).</summary>
    public required string Street { get; init; }

    /// <summary>City (BT-37/BT-52), required (BR-RO-091/092), at most 50 characters (BR-RO-L0501/0502).</summary>
    public required string City { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code (BT-40/BT-55), from the BR-CL-14 list; upper-cased on output.</summary>
    public required string CountryCode { get; init; }

    /// <summary>County name, abbreviation or ISO code; required for Romanian addresses, ignored otherwise.</summary>
    public string? County { get; init; }

    /// <summary>Post code (BT-38/BT-53), at most 20 characters (BR-RO-L0201/0202).</summary>
    public string? PostalCode { get; init; }
}
