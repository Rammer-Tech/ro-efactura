using FluentValidation;
using RoEFactura.Validation.Constants;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation;

/// <summary>
/// Validates the Romanian-specific address rules (BR-RO-100/101/110/111) plus two library-local
/// completeness checks (BR-RO-CITY-REQUIRED, BR-RO-COUNTRY-CODE). Constructed once per role (Seller/Buyer)
/// by <see cref="PartyValidators.SellerPartyValidator"/> and <see cref="PartyValidators.BuyerPartyValidator"/>.
/// </summary>
public class RomanianAddressValidator : AbstractValidator<AddressType>
{
    /// <summary>Defaults to <see cref="RomanianAddressRole.Seller"/> (dependency injection resolves this ctor).</summary>
    public RomanianAddressValidator() : this(RomanianAddressRole.Seller)
    {
    }

    public RomanianAddressValidator(RomanianAddressRole role)
    {
        string countyErrorCode = role == RomanianAddressRole.Buyer
            ? RoCiusRuleIds.BuyerCounty
            : RoCiusRuleIds.SellerCounty;
        string sectorErrorCode = role == RomanianAddressRole.Buyer
            ? RoCiusRuleIds.BuyerBucharestSector
            : RoCiusRuleIds.SellerBucharestSector;

        // BR-RO-110 / BR-RO-111: country RO ⇒ country subdivision must be a valid ISO 3166-2:RO code.
        RuleFor(x => x)
            .Must(HasValidRomanianCounty)
            .When(x => IsRomanianAddress(x))
            .WithErrorCode(countyErrorCode)
            .WithMessage("Invalid Romanian county code. Must be a valid ISO 3166-2:RO code.");

        // BR-RO-100 / BR-RO-101: country RO and county RO-B ⇒ city must be coded SECTOR1..SECTOR6.
        RuleFor(x => x)
            .Must(HasValidBucharestSector)
            .When(x => IsBucharestAddress(x))
            .WithErrorCode(sectorErrorCode)
            .WithMessage("București addresses must specify SECTOR1 through SECTOR6 as city name.");

        // Library-local completeness checks (not a CIUS-RO/EN16931 rule id).
        RuleFor(x => x)
            .Must(HasValidCityName)
            .When(x => IsRomanianAddress(x))
            .WithErrorCode("BR-RO-CITY-REQUIRED")
            .WithMessage("City name is required for Romanian addresses.");

        RuleFor(x => x)
            .Must(HasValidCountryCode)
            .When(x => IsRomanianAddress(x))
            .WithErrorCode("BR-RO-COUNTRY-CODE")
            .WithMessage("Country code must be 'RO' for Romanian addresses.");
    }

    private static bool IsRomanianAddress(AddressType address)
    {
        return address?.Country?.IdentificationCode?.Value == "RO";
    }

    private static bool IsBucharestAddress(AddressType address)
    {
        return IsRomanianAddress(address)
            && string.Equals(address?.CountrySubentity?.Value?.Trim(), RomanianConstants.BucharestCountyCode, StringComparison.Ordinal);
    }

    private static bool HasValidRomanianCounty(AddressType address)
    {
        string? countyCode = address?.CountrySubentity?.Value;
        if (string.IsNullOrWhiteSpace(countyCode))
            return false;

        // RO16931-rules.sch compares normalize-space(CountrySubentity) against the ISO code list exactly
        // (case-sensitive); do not uppercase the input, or a lowercase county would pass locally but fail at ANAF.
        return RomanianConstants.ValidCountyCodes.Contains(countyCode.Trim());
    }

    private static bool HasValidBucharestSector(AddressType address)
    {
        string? city = address?.CityName?.Value;
        if (string.IsNullOrWhiteSpace(city))
            return false;

        return RomanianConstants.BucharestSectorCodes.Contains(city.Trim());
    }

    private static bool HasValidCityName(AddressType address)
    {
        return !string.IsNullOrEmpty(address?.CityName?.Value);
    }

    private static bool HasValidCountryCode(AddressType address)
    {
        return address?.Country?.IdentificationCode?.Value == "RO";
    }
}
