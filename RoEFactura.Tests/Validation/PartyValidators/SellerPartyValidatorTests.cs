using FluentAssertions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation.PartyValidators;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Validation.PartyValidators;

public class SellerPartyValidatorTests
{
    private readonly SellerPartyValidator _sut = new();

    private SupplierPartyType ValidSeller()
        => InvoiceBuilder.Valid().Build().AccountingSupplierParty!;

    [Fact]
    public void Br6_MissingRegistrationNameAndPartyName_Fails()
    {
        var seller = ValidSeller();
        seller.Party!.PartyLegalEntity = null;
        seller.Party.PartyName = null;
        _sut.Validate(seller).Errors.Should().Contain(e => e.ErrorCode == "BR-6");
    }

    [Fact]
    public void Br6_OnlyPartyNamePresent_Passes()
    {
        var seller = ValidSeller();
        seller.Party!.PartyLegalEntity = null;
        seller.Party.PartyName = new List<PartyNameType>
        {
            new PartyNameType { Name = new NameType { Value = "Test" } }
        };
        _sut.Validate(seller).Errors.Should().NotContain(e => e.ErrorCode == "BR-6");
    }

    [Fact]
    public void Br8_MissingPostalAddress_Fails()
    {
        var seller = ValidSeller();
        seller.Party!.PostalAddress = null;
        _sut.Validate(seller).Errors.Should().Contain(e => e.ErrorCode == "BR-8");
    }

    [Fact]
    public void BrRoSellerId_RomanianSellerWithoutCompanyId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutSellerCompanyId().Build();
        var seller = invoice.AccountingSupplierParty!;
        _sut.Validate(seller).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-SELLER-ID");
    }

    [Fact]
    public void BrRoSellerId_NonRomanianSellerWithoutCompanyId_Passes()
    {
        var seller = ValidSeller();
        seller.Party!.PostalAddress!.Country!.IdentificationCode =
            new CodeType { Value = "DE" };
        seller.Party.PartyLegalEntity![0].CompanyID = null;
        _sut.Validate(seller).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-SELLER-ID");
    }

    [Fact]
    public void ValidSeller_PassesAllRules()
    {
        _sut.Validate(ValidSeller()).IsValid.Should().BeTrue();
    }
}
