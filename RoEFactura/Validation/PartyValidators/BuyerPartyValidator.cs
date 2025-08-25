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

        // Address validation for Romanian parties
        RuleFor(x => x)
            .Must(x => x.Party?.PostalAddress != null)
            .When(x => IsRomanianParty(x))
            .WithErrorCode("BR-10-ADDRESS")
            .WithMessage("Romanian buyer must have a postal address.");

        // Ensure postal address exists (EN 16931 requirement)
        RuleFor(x => x)
            .Must(x => x.Party?.PostalAddress != null)
            .WithErrorCode("BR-10")
            .WithMessage("Buyer postal address is required.");
    }

    private static bool HasValidBuyerName(CustomerPartyType party)
    {
        return !string.IsNullOrEmpty(party?.Party?.PartyName?.FirstOrDefault()?.Name?.Value);
    }

    private static bool IsRomanianParty(CustomerPartyType party)
    {
        return party?.Party?.PostalAddress?.Country?.IdentificationCode?.Value == "RO";
    }

    private static bool HasValidRomanianIdentifier(CustomerPartyType party)
    {
        if (!IsRomanianParty(party))
            return true; // Only validate Romanian parties

        var legalId = party.Party?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value;
        var vatId = party.Party?.PartyTaxScheme?.FirstOrDefault()?.CompanyID?.Value;

        return !string.IsNullOrWhiteSpace(legalId) || !string.IsNullOrWhiteSpace(vatId);
    }
}