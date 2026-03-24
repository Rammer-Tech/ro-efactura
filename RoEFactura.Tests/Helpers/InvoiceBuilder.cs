using RoEFactura.Validation.Constants;
using UblSharp;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;

namespace RoEFactura.Tests.Helpers;

/// <summary>
/// Fluent builder that starts from a fully-valid RO_CIUS invoice.
/// Every With*/Without* method returns a new builder — the original is not mutated.
/// </summary>
public class InvoiceBuilder
{
    private InvoiceType _invoice;

    private InvoiceBuilder(InvoiceType invoice) => _invoice = invoice;

    /// <summary>Creates a builder pre-loaded with a valid base invoice.</summary>
    public static InvoiceBuilder Valid() => new(BuildBase());

    public InvoiceType Build() => _invoice;

    public InvoiceBuilder WithId(string id)
    {
        _invoice.ID = new IdentifierType { Value = id };
        return this;
    }

    public InvoiceBuilder WithoutId()
    {
        _invoice.ID = null;
        return this;
    }

    public InvoiceBuilder WithCustomizationId(string id)
    {
        _invoice.CustomizationID = new IdentifierType { Value = id };
        return this;
    }

    public InvoiceBuilder WithoutCustomizationId()
    {
        _invoice.CustomizationID = null;
        return this;
    }

    public InvoiceBuilder WithTypeCode(string code)
    {
        _invoice.InvoiceTypeCode = new CodeType { Value = code };
        return this;
    }

    public InvoiceBuilder WithoutTypeCode()
    {
        _invoice.InvoiceTypeCode = null;
        return this;
    }

    public InvoiceBuilder WithoutIssueDate()
    {
        // UblSharp ignores null assignment; use default value to mean "no issue date" (see RoCiusUblValidator.HasValidIssueDate).
        _invoice.IssueDate = new DateType { Value = default };
        return this;
    }

    public InvoiceBuilder WithCurrency(string currency)
    {
        _invoice.DocumentCurrencyCode = new CodeType { Value = currency };
        return this;
    }

    public InvoiceBuilder WithoutCurrency()
    {
        _invoice.DocumentCurrencyCode = null;
        return this;
    }

    public InvoiceBuilder WithVatCurrency(string currency)
    {
        _invoice.TaxCurrencyCode = new CodeType { Value = currency };
        return this;
    }

    public InvoiceBuilder WithoutLines()
    {
        _invoice.InvoiceLine = null;
        return this;
    }

    public InvoiceBuilder WithLineCount(int count)
    {
        _invoice.InvoiceLine = Enumerable.Range(1, count)
            .Select(i => BuildValidLine(i.ToString()))
            .ToList();
        decimal total = count * 100m;
        decimal vat = Math.Round(total * 0.19m, 2);
        _invoice.LegalMonetaryTotal = BuildMonetaryTotal(total, total + vat, total + vat);
        _invoice.TaxTotal = new List<TaxTotalType>
        {
            BuildTaxTotal(total, vat, 19m)
        };
        return this;
    }

    public InvoiceBuilder WithTotals(decimal taxExclusive, decimal taxInclusive, decimal payable)
    {
        _invoice.LegalMonetaryTotal = BuildMonetaryTotal(taxExclusive, taxInclusive, payable);
        return this;
    }

    public InvoiceBuilder WithoutTaxExclusiveAmount()
    {
        _invoice.LegalMonetaryTotal!.TaxExclusiveAmount = null;
        return this;
    }

    public InvoiceBuilder WithoutTaxInclusiveAmount()
    {
        _invoice.LegalMonetaryTotal!.TaxInclusiveAmount = null;
        return this;
    }

    public InvoiceBuilder WithoutPayableAmount()
    {
        _invoice.LegalMonetaryTotal!.PayableAmount = null;
        return this;
    }

    public InvoiceBuilder WithTaxTotalAmount(decimal taxAmount)
    {
        if (_invoice.TaxTotal?.Count > 0)
            _invoice.TaxTotal[0].TaxAmount = new AmountType { Value = taxAmount, currencyID = "RON" };
        return this;
    }

    public InvoiceBuilder WithDocumentPeriod(DateTime start, DateTime end)
    {
        _invoice.InvoicePeriod = new List<PeriodType>
        {
            new PeriodType
            {
                StartDate = new DateType { Value = start },
                EndDate = new DateType { Value = end }
            }
        };
        return this;
    }

    public InvoiceBuilder WithoutSellerName()
    {
        var party = _invoice.AccountingSupplierParty?.Party;
        if (party != null)
        {
            party.PartyLegalEntity = null;
            party.PartyName = null;
        }
        return this;
    }

    public InvoiceBuilder WithoutSellerAddress()
    {
        if (_invoice.AccountingSupplierParty?.Party != null)
            _invoice.AccountingSupplierParty.Party.PostalAddress = null;
        return this;
    }

    public InvoiceBuilder WithoutSellerCompanyId()
    {
        var entity = _invoice.AccountingSupplierParty?.Party?.PartyLegalEntity?.FirstOrDefault();
        if (entity != null) entity.CompanyID = null;
        return this;
    }

    public InvoiceBuilder WithoutBuyerName()
    {
        var party = _invoice.AccountingCustomerParty?.Party;
        if (party != null)
        {
            party.PartyLegalEntity = null;
            party.PartyName = null;
        }
        return this;
    }

    public InvoiceBuilder WithoutBuyerAddress()
    {
        if (_invoice.AccountingCustomerParty?.Party != null)
            _invoice.AccountingCustomerParty.Party.PostalAddress = null;
        return this;
    }

    public InvoiceBuilder WithoutBuyerIdentifiers()
    {
        var party = _invoice.AccountingCustomerParty?.Party;
        if (party != null)
        {
            var entity = party.PartyLegalEntity?.FirstOrDefault();
            if (entity != null) entity.CompanyID = null;
            if (party.PartyTaxScheme?.Count > 0)
                party.PartyTaxScheme[0].CompanyID = null;
        }
        return this;
    }

    private static InvoiceType BuildBase()
    {
        var line = BuildValidLine("1");
        return new InvoiceType
        {
            CustomizationID = new IdentifierType { Value = RomanianConstants.RoCiusCustomizationId },
            ID = new IdentifierType { Value = "INV-2024-001" },
            IssueDate = new DateType { Value = DateTime.Today },
            InvoiceTypeCode = new CodeType { Value = "380" },
            DocumentCurrencyCode = new CodeType { Value = "RON" },
            AccountingSupplierParty = BuildRomanianSeller("SC Vanzator SRL", "J12/100/2020", "RO12345678", "CJ", "Cluj-Napoca"),
            AccountingCustomerParty = BuildRomanianBuyer("SC Cumparator SRL", "J40/200/2019", "RO87654321", "IS", "Iasi"),
            TaxTotal = new List<TaxTotalType> { BuildTaxTotal(100m, 19m, 19m) },
            LegalMonetaryTotal = BuildMonetaryTotal(100m, 119m, 119m),
            InvoiceLine = new List<InvoiceLineType> { line }
        };
    }

    public static InvoiceLineType BuildValidLine(string id, decimal amount = 100m)
    {
        return new InvoiceLineType
        {
            ID = new IdentifierType { Value = id },
            InvoicedQuantity = new QuantityType { Value = 1m, unitCode = "C62" },
            LineExtensionAmount = new AmountType { Value = amount, currencyID = "RON" },
            Item = new ItemType
            {
                Name = new NameType { Value = "Servicii consultanta" },
                ClassifiedTaxCategory = new List<TaxCategoryType>
                {
                    new TaxCategoryType
                    {
                        ID = new IdentifierType { Value = "S" },
                        Percent = new PercentType { Value = 19m },
                        TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
                    }
                }
            },
            Price = new PriceType
            {
                PriceAmount = new AmountType { Value = amount, currencyID = "RON" }
            }
        };
    }

    private static SupplierPartyType BuildRomanianSeller(string name, string companyId, string vatId, string county, string city)
    {
        return new SupplierPartyType
        {
            Party = new PartyType
            {
                PartyLegalEntity = new List<PartyLegalEntityType>
                {
                    new PartyLegalEntityType
                    {
                        RegistrationName = new NameType { Value = name },
                        CompanyID = new IdentifierType { Value = companyId }
                    }
                },
                PartyTaxScheme = new List<PartyTaxSchemeType>
                {
                    new PartyTaxSchemeType
                    {
                        CompanyID = new IdentifierType { Value = vatId },
                        TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
                    }
                },
                PostalAddress = BuildRomanianAddress(city, county)
            }
        };
    }

    private static CustomerPartyType BuildRomanianBuyer(string name, string companyId, string vatId, string county, string city)
    {
        return new CustomerPartyType
        {
            Party = new PartyType
            {
                PartyLegalEntity = new List<PartyLegalEntityType>
                {
                    new PartyLegalEntityType
                    {
                        RegistrationName = new NameType { Value = name },
                        CompanyID = new IdentifierType { Value = companyId }
                    }
                },
                PartyTaxScheme = new List<PartyTaxSchemeType>
                {
                    new PartyTaxSchemeType
                    {
                        CompanyID = new IdentifierType { Value = vatId },
                        TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
                    }
                },
                PostalAddress = BuildRomanianAddress(city, county)
            }
        };
    }

    public static AddressType BuildRomanianAddress(string city, string county)
    {
        return new AddressType
        {
            CityName = new NameType { Value = city },
            CountrySubentity = new TextType { Value = county },
            Country = new CountryType
            {
                IdentificationCode = new CodeType { Value = "RO" }
            }
        };
    }

    private static MonetaryTotalType BuildMonetaryTotal(decimal taxExclusive, decimal taxInclusive, decimal payable)
    {
        return new MonetaryTotalType
        {
            LineExtensionAmount = new AmountType { Value = taxExclusive, currencyID = "RON" },
            TaxExclusiveAmount = new AmountType { Value = taxExclusive, currencyID = "RON" },
            TaxInclusiveAmount = new AmountType { Value = taxInclusive, currencyID = "RON" },
            PayableAmount = new AmountType { Value = payable, currencyID = "RON" }
        };
    }

    private static TaxTotalType BuildTaxTotal(decimal taxableAmount, decimal taxAmount, decimal rate)
    {
        return new TaxTotalType
        {
            TaxAmount = new AmountType { Value = taxAmount, currencyID = "RON" },
            TaxSubtotal = new List<TaxSubtotalType>
            {
                new TaxSubtotalType
                {
                    TaxableAmount = new AmountType { Value = taxableAmount, currencyID = "RON" },
                    TaxAmount = new AmountType { Value = taxAmount, currencyID = "RON" },
                    TaxCategory = new TaxCategoryType
                    {
                        ID = new IdentifierType { Value = "S" },
                        Percent = new PercentType { Value = rate },
                        TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
                    }
                }
            }
        };
    }
}
