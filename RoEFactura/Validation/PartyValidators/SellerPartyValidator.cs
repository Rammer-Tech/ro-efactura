using FluentValidation;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation.PartyValidators;

public class SellerPartyValidator : AbstractValidator<SupplierPartyType>
{
    public SellerPartyValidator()
    {
        // Required seller name (EN 16931 requirement)
        RuleFor(x => x.Party?.PartyName?.FirstOrDefault()?.Name?.Value)
            .NotEmpty()
            .WithErrorCode("BR-6")
            .WithMessage("Seller name is required.");

        // Address validation for Romanian sellers
        RuleFor(x => x.Party?.PostalAddress)
            .SetValidator(new RomanianAddressValidator()!)
            .When(x => IsRomanianParty(x));

        // Ensure postal address exists (EN 16931 requirement)
        RuleFor(x => x.Party?.PostalAddress)
            .NotNull()
            .WithErrorCode("BR-8")
            .WithMessage("Seller postal address is required.");

        // Romanian sellers should have proper identifiers
        RuleFor(x => x.Party?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value)
            .NotEmpty()
            .When(x => IsRomanianParty(x))
            .WithErrorCode("BR-RO-SELLER-ID")
            .WithMessage("Romanian seller should have legal registration identifier (CUI/CIF).");
    }

    private static bool IsRomanianParty(SupplierPartyType party)
    {
        return party?.Party?.PostalAddress?.Country?.IdentificationCode?.Value == "RO";
    }
}