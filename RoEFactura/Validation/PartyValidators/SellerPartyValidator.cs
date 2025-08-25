using FluentValidation;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation.PartyValidators;

public class SellerPartyValidator : AbstractValidator<SupplierPartyType>
{
    public SellerPartyValidator()
    {
        // Required seller name (EN 16931 requirement)
        RuleFor(x => x)
            .Must(HasValidSellerName)
            .WithErrorCode("BR-6")
            .WithMessage("Seller name is required.");

        // Address validation for Romanian sellers
        RuleFor(x => x)
            .Must(x => x.Party?.PostalAddress != null)
            .When(x => IsRomanianParty(x))
            .WithErrorCode("BR-8-ADDRESS")
            .WithMessage("Romanian seller must have a postal address.");

        // Ensure postal address exists (EN 16931 requirement)
        RuleFor(x => x)
            .Must(x => x.Party?.PostalAddress != null)
            .WithErrorCode("BR-8")
            .WithMessage("Seller postal address is required.");

        // Romanian sellers should have proper identifiers
        RuleFor(x => x)
            .Must(HasValidLegalIdentifier)
            .When(x => IsRomanianParty(x))
            .WithErrorCode("BR-RO-SELLER-ID")
            .WithMessage("Romanian seller should have legal registration identifier (CUI/CIF).");
    }

    private static bool HasValidSellerName(SupplierPartyType party)
    {
        return !string.IsNullOrEmpty(party?.Party?.PartyName?.FirstOrDefault()?.Name?.Value);
    }

    private static bool HasValidLegalIdentifier(SupplierPartyType party)
    {
        return !string.IsNullOrEmpty(party?.Party?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value);
    }

    private static bool IsRomanianParty(SupplierPartyType party)
    {
        return party?.Party?.PostalAddress?.Country?.IdentificationCode?.Value == "RO";
    }
}