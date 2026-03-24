using FluentAssertions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation.PartyValidators;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Validation.PartyValidators;

public class BuyerPartyValidatorTests
{
    private readonly BuyerPartyValidator _sut = new();

    private CustomerPartyType ValidBuyer()
        => InvoiceBuilder.Valid().Build().AccountingCustomerParty!;

    [Fact]
    public void Br7_MissingBuyerName_Fails()
    {
        var buyer = ValidBuyer();
        buyer.Party!.PartyLegalEntity = null;
        buyer.Party.PartyName = null;
        _sut.Validate(buyer).Errors.Should().Contain(e => e.ErrorCode == "BR-7");
    }

    [Fact]
    public void Br10_MissingBuyerAddress_Fails()
    {
        var buyer = ValidBuyer();
        buyer.Party!.PostalAddress = null;
        _sut.Validate(buyer).Errors.Should().Contain(e => e.ErrorCode == "BR-10");
    }

    [Fact]
    public void BrRo120_RomanianBuyerWithNoCuiAndNoVat_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutBuyerIdentifiers().Build();
        var buyer = invoice.AccountingCustomerParty!;
        _sut.Validate(buyer).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void BrRo120_RomanianBuyerWithCuiOnly_Passes()
    {
        var buyer = ValidBuyer();
        // Remove VAT ID but keep company ID
        buyer.Party!.PartyTaxScheme![0].CompanyID = null;
        _sut.Validate(buyer).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void BrRo120_RomanianBuyerWithVatOnly_Passes()
    {
        var buyer = ValidBuyer();
        // Remove company ID but keep VAT ID
        buyer.Party!.PartyLegalEntity![0].CompanyID = null;
        _sut.Validate(buyer).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void BrRo120_NonRomanianBuyerWithNoIdentifiers_Passes()
    {
        var buyer = ValidBuyer();
        buyer.Party!.PostalAddress!.Country!.IdentificationCode =
            new CodeType { Value = "DE" };
        buyer.Party.PartyLegalEntity![0].CompanyID = null;
        buyer.Party.PartyTaxScheme![0].CompanyID = null;
        _sut.Validate(buyer).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void ValidBuyer_PassesAllRules()
    {
        _sut.Validate(ValidBuyer()).IsValid.Should().BeTrue();
    }
}
