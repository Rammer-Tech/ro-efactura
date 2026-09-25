using FluentValidation;
using UblSharp.CommonAggregateComponents;


namespace RoEFactura.Validation.PartyValidators;

public class PayeePartyValidator : AbstractValidator<PartyType>
{
    // BR-RO-130 (forced execution Payee identity) is not implemented here: it depends on the
    // `executare=DA` upload flag, which is not present in the XML being validated -- ANAF enforces
    // it at upload time based on that flag, not from document content alone.

    public PayeePartyValidator()
    {
        // EN 16931: If payee exists and is different from seller, name is required
        RuleFor(x => x)
            .Must(HasValidPayeeName)
            .WithErrorCode("BR-17")
            .WithMessage("Payee name is required when payee is specified.");
    }

    private static bool HasValidPayeeName(PartyType party)
    {
        // Check RegistrationName (BT-59, mandatory) or PartyName (optional)
        var registrationName = party?.PartyLegalEntity?.FirstOrDefault()?.RegistrationName?.Value;
        var partyName = party?.PartyName?.FirstOrDefault()?.Name?.Value;
        return !string.IsNullOrEmpty(registrationName) || !string.IsNullOrEmpty(partyName);
    }
}