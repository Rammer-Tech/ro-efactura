using FluentValidation;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation.PartyValidators;

public class BuyerPartyValidator : AbstractValidator<CustomerPartyType>
{
    public BuyerPartyValidator()
    {
        // BR-RO-120: Romanian buyer must have CUI (Legal Registration ID) or VAT ID
        RuleFor(x => x)
            .Must(HasValidRomanianIdentifier)
            .When(x => IsRomanianParty(x))
            .WithErrorCode("BR-RO-120")
            .WithMessage("Romanian buyer must have either Legal Registration ID (CUI/CIF) or VAT identifier.");

        // Required buyer name (EN 16931 requirement)
        RuleFor(x => x)
            .Must(HasValidBuyerName)
            .WithErrorCode("BR-7")
            .WithMessage("Buyer name is required.");

        // Address validation for Romanian parties (UblSharp never exposes null PostalAddress; check content)
        RuleFor(x => x)
            .Must(x => HasMaterialPostalAddress(x.Party))
            .When(x => IsRomanianParty(x))
            .WithErrorCode("BR-10-ADDRESS")
            .WithMessage("Romanian buyer must have a postal address.");

        // Ensure postal address exists (EN 16931 requirement)
        RuleFor(x => x)
            .Must(x => HasMaterialPostalAddress(x.Party))
            .WithErrorCode("BR-10")
            .WithMessage("Buyer postal address is required.");
    }

    private static bool HasValidBuyerName(CustomerPartyType party)
    {
        // Check RegistrationName (BT-44, mandatory) or PartyName (BT-45, optional)
        var registrationName = party?.Party?.PartyLegalEntity?.FirstOrDefault()?.RegistrationName?.Value;
        var partyName = party?.Party?.PartyName?.FirstOrDefault()?.Name?.Value;
        return !string.IsNullOrEmpty(registrationName) || !string.IsNullOrEmpty(partyName);
    }

    private static bool IsRomanianParty(CustomerPartyType party)
    {
        return party?.Party?.PostalAddress?.Country?.IdentificationCode?.Value == "RO";
    }

    private static bool HasValidRomanianIdentifier(CustomerPartyType party)
    {
        if (!IsRomanianParty(party))
            return true; // Only validate Romanian parties

        string? legalId = party.Party?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value;
        string? vatId = party.Party?.PartyTaxScheme?.FirstOrDefault()?.CompanyID?.Value;

        return !string.IsNullOrWhiteSpace(legalId) || !string.IsNullOrWhiteSpace(vatId);
    }

    private static bool HasMaterialPostalAddress(PartyType? party)
    {
        var addr = party?.PostalAddress;
        if (addr == null) return false;
        return !string.IsNullOrEmpty(addr.Country?.IdentificationCode?.Value)
            || !string.IsNullOrEmpty(addr.CityName?.Value);
    }
}