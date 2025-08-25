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
        RuleFor(x => x.Party?.PartyName?.FirstOrDefault()?.Name?.Value)
            .NotEmpty()
            .WithErrorCode("BR-7")
            .WithMessage("Buyer name is required.");

        // Address validation for Romanian parties
        RuleFor(x => x.Party?.PostalAddress)
            .SetValidator(new RomanianAddressValidator()!)
            .When(x => IsRomanianParty(x));

        // Ensure postal address exists (EN 16931 requirement)
        RuleFor(x => x.Party?.PostalAddress)
            .NotNull()
            .WithErrorCode("BR-10")
            .WithMessage("Buyer postal address is required.");
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