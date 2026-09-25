using FluentAssertions;
using RoEFactura.Validation.PartyValidators;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Validation.PartyValidators;

public class PayeePartyValidatorTests
{
    private readonly PayeePartyValidator _sut = new();

    private static PartyType ValidPayee() => new PartyType
    {
        PartyName = new List<PartyNameType>
        {
            new PartyNameType { Name = new NameType { Value = "Payee Entity SRL" } }
        },
        PartyLegalEntity = new List<PartyLegalEntityType>
        {
            new PartyLegalEntityType
            {
                RegistrationName = new NameType { Value = "Payee Entity SRL" },
                CompanyID = new IdentifierType { Value = "J12/500/2021" }
            }
        }
    };

    [Fact]
    public void Br17_PayeeWithName_Passes()
    {
        _sut.Validate(ValidPayee()).Errors.Should().NotContain(e => e.ErrorCode == "BR-17");
    }

    [Fact]
    public void Br17_PayeeWithoutName_Fails()
    {
        var payee = ValidPayee();
        payee.PartyLegalEntity![0].RegistrationName = null;
        payee.PartyName = null;
        _sut.Validate(payee).Errors.Should().Contain(e => e.ErrorCode == "BR-17");
    }

    [Fact]
    public void ValidPayee_PassesAllRules()
    {
        _sut.Validate(ValidPayee()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void PayeeWithoutLegalRegistrationId_DoesNotEmitBrRo130()
    {
        // BR-RO-130 depends on the executare=DA upload flag, which is not decidable from the XML;
        // the local validator must never emit it, regardless of the payee's legal registration id.
        var payee = ValidPayee();
        payee.PartyLegalEntity![0].CompanyID = null;

        _sut.Validate(payee).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-130");
    }
}
