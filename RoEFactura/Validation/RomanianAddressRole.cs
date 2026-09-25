namespace RoEFactura.Validation;

/// <summary>
/// Which party an <see cref="RomanianAddressValidator"/> instance is validating. Determines whether
/// county/sector failures are reported as BR-RO-110/BR-RO-100 (Seller) or BR-RO-111/BR-RO-101 (Buyer).
/// </summary>
public enum RomanianAddressRole
{
    Seller,
    Buyer
}
