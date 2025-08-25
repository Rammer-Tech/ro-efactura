using System.Text.RegularExpressions;
using FluentValidation;
using RoEFactura.Validation.Constants;
using UblSharp;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation;

public class RomanianAddressValidator : AbstractValidator<AddressType>
{
    private static readonly Regex BucharestSectorRegex = new(@"^Sector [1-6]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RomanianAddressValidator()
    {
        // Validate that Romanian addresses have proper county codes
        RuleFor(x => x.CountrySubentity?.Value)
            .Must(BeValidRomanianCounty)
            .When(x => IsRomanianAddress(x))
            .WithErrorCode("BR-RO-COUNTY")
            .WithMessage("Invalid Romanian county code. Must be valid ISO 3166-2:RO code.");

        // Special validation for București (B) - city must be "Sector 1" through "Sector 6"
        RuleFor(x => x.CityName?.Value)
            .Must(city => BucharestSectorRegex.IsMatch(city ?? ""))
            .When(x => IsBucharestAddress(x))
            .WithErrorCode("BR-RO-BUCHAREST")
            .WithMessage("București addresses must specify 'Sector 1' through 'Sector 6' as city name.");

        // Required fields for Romanian addresses
        RuleFor(x => x.CityName?.Value)
            .NotEmpty()
            .When(x => IsRomanianAddress(x))
            .WithErrorCode("BR-RO-CITY-REQUIRED")
            .WithMessage("City name is required for Romanian addresses.");

        RuleFor(x => x.Country?.IdentificationCode?.Value)
            .Equal("RO")
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
        return IsRomanianAddress(address) && address?.CountrySubentity?.Value == "B";
    }

    private static bool BeValidRomanianCounty(string? countyCode)
    {
        if (string.IsNullOrWhiteSpace(countyCode))
            return false;

        return RomanianConstants.ValidCountyCodes.Contains(countyCode.ToUpperInvariant());
    }
}