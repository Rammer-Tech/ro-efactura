using FluentValidation;
using UblSharp.CommonAggregateComponents;

namespace RoEFactura.Validation.PartyValidators;

public class PayeePartyValidator : AbstractValidator<PartyType>
{
    public PayeePartyValidator()
    {
        // BR-RO-130: In forced execution, Payee must have name and legal registration ID
        RuleFor(x => x.PartyName?.FirstOrDefault()?.Name?.Value)
            .NotEmpty()
            .When(x => IsForcedExecution(x))
            .WithErrorCode("BR-RO-130")
            .WithMessage("In forced execution, Payee name is required and must be the execution authority name.");

        RuleFor(x => x.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value)
            .NotEmpty()
            .When(x => IsForcedExecution(x))
            .WithErrorCode("BR-RO-130")
            .WithMessage("In forced execution, Payee legal registration identifier is required.");

        // EN 16931: If payee exists and is different from seller, name is required
        RuleFor(x => x.PartyName?.FirstOrDefault()?.Name?.Value)
            .NotEmpty()
            .WithErrorCode("BR-17")
            .WithMessage("Payee name is required when payee is specified.");
    }

    private static bool IsForcedExecution(PartyType party)
    {
        // This would need to be determined by business logic - 
        // perhaps by checking invoice type or other indicators
        // For now, we'll assume it's not forced execution
        return false;
    }
}