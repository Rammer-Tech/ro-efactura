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

    public InvoiceBuilder WithPrepaidAmount(decimal prepaid)
    {
        _invoice.LegalMonetaryTotal!.PrepaidAmount = new AmountType { Value = prepaid, currencyID = "RON" };
        return this;
    }

    public InvoiceBuilder WithoutLineExtensionAmount()
    {
        // UblSharp AmountType getters never return null; missing monetary amounts use no currencyID.
        _invoice.LegalMonetaryTotal!.LineExtensionAmount = new AmountType { Value = 0m, currencyID = null };
        return this;
    }

    public InvoiceBuilder WithoutTaxExclusiveAmount()
    {
        // UblSharp AmountType getters never return null; missing monetary amounts use no currencyID.
        _invoice.LegalMonetaryTotal!.TaxExclusiveAmount = new AmountType { Value = 0m, currencyID = null };
        return this;
    }

    /// <summary>
    /// Adds a single document-level allowance (S/19%) and recomputes BT-107/BT-109/BT-110/BT-112/BT-115
    /// (BT-106 is left untouched, still equal to the line sum) so the whole invoice stays internally consistent.
    /// </summary>
    public InvoiceBuilder WithDocumentAllowance(decimal amount)
    {
        const decimal vatRate = 19m;
        decimal lineExtension = _invoice.LegalMonetaryTotal?.LineExtensionAmount?.Value ?? 0m;
        decimal taxExclusive = lineExtension - amount;
        decimal vatAmount = Math.Round(taxExclusive * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
        decimal taxInclusive = taxExclusive + vatAmount;

        _invoice.AllowanceCharge = new List<AllowanceChargeType>
        {
            new AllowanceChargeType
            {
                ChargeIndicator = new IndicatorType { Value = false },
                Amount = new AmountType { Value = amount, currencyID = "RON" },
                TaxCategory = new List<TaxCategoryType>
                {
                    new TaxCategoryType
                    {
                        ID = new IdentifierType { Value = "S" },
                        Percent = new PercentType { Value = vatRate },
                        TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
                    }
                }
            }
        };

        _invoice.LegalMonetaryTotal = new MonetaryTotalType
        {
            LineExtensionAmount = new AmountType { Value = lineExtension, currencyID = "RON" },
            TaxExclusiveAmount = new AmountType { Value = taxExclusive, currencyID = "RON" },
            AllowanceTotalAmount = new AmountType { Value = amount, currencyID = "RON" },
            TaxInclusiveAmount = new AmountType { Value = taxInclusive, currencyID = "RON" },
            PayableAmount = new AmountType { Value = taxInclusive, currencyID = "RON" }
        };

        _invoice.TaxTotal = new List<TaxTotalType> { BuildTaxTotal(taxExclusive, vatAmount, vatRate) };

        return this;
    }

    public InvoiceBuilder WithoutTaxInclusiveAmount()
    {
        _invoice.LegalMonetaryTotal!.TaxInclusiveAmount = new AmountType { Value = 0m, currencyID = null };
        return this;
    }

    public InvoiceBuilder WithoutPayableAmount()
    {
        _invoice.LegalMonetaryTotal!.PayableAmount = new AmountType { Value = 0m, currencyID = null };
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

    public InvoiceBuilder WithBillingReference(string precedingInvoiceId, DateTime? issueDate = null)
    {
        _invoice.BillingReference =
        [
            new BillingReferenceType
            {
                InvoiceDocumentReference = new DocumentReferenceType
                {
                    ID = new IdentifierType { Value = precedingInvoiceId },
                    IssueDate = issueDate.HasValue ? new DateType { Value = issueDate.Value } : null
                }
            }
        ];
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
            _invoice.AccountingSupplierParty.Party.PostalAddress = new AddressType();
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
            _invoice.AccountingCustomerParty.Party.PostalAddress = new AddressType();
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

    public InvoiceBuilder WithSellerAddress(string city, string county)
    {
        if (_invoice.AccountingSupplierParty?.Party != null)
            _invoice.AccountingSupplierParty.Party.PostalAddress = BuildRomanianAddress(city, county);
        return this;
    }

    public InvoiceBuilder WithBuyerAddress(string city, string? county, string country = "RO")
    {
        if (_invoice.AccountingCustomerParty?.Party != null)
        {
            _invoice.AccountingCustomerParty.Party.PostalAddress = new AddressType
            {
                CityName = string.IsNullOrEmpty(city) ? null : new NameType { Value = city },
                CountrySubentity = string.IsNullOrEmpty(county) ? null : new TextType { Value = county },
                Country = new CountryType { IdentificationCode = new CodeType { Value = country } }
            };
        }
        return this;
    }

    public InvoiceBuilder WithNotes(int count, int length)
    {
        _invoice.Note = Enumerable.Range(1, count)
            .Select(_ => new TextType { Value = new string('A', length) })
            .ToList();
        return this;
    }

    public InvoiceBuilder WithVatPointDateCode(string code)
    {
        _invoice.InvoicePeriod = new List<PeriodType>
        {
            new PeriodType
            {
                DescriptionCode = new List<CodeType> { new CodeType { Value = code } }
            }
        };
        return this;
    }

    /// <summary>Overrides the (single) invoice line's VAT category/rate. <paramref name="percent"/> null omits BT-152.</summary>
    public InvoiceBuilder WithLineVat(string category, decimal? percent)
    {
        var line = _invoice.InvoiceLine?.FirstOrDefault();
        if (line?.Item != null)
        {
            line.Item.ClassifiedTaxCategory = new List<TaxCategoryType>
            {
                new TaxCategoryType
                {
                    ID = new IdentifierType { Value = category },
                    Percent = percent.HasValue ? new PercentType { Value = percent.Value } : null,
                    TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
                }
            };
        }
        return this;
    }

    /// <summary>Overrides the (single) TaxTotal's first subtotal category/rate/exemption reason.</summary>
    public InvoiceBuilder WithSubtotalVat(string category, decimal? percent, string? reasonText = null, string? reasonCode = null)
    {
        var subtotal = _invoice.TaxTotal?.FirstOrDefault()?.TaxSubtotal?.FirstOrDefault();
        if (subtotal != null)
        {
            subtotal.TaxCategory = new TaxCategoryType
            {
                ID = new IdentifierType { Value = category },
                Percent = percent.HasValue ? new PercentType { Value = percent.Value } : null,
                TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } },
                TaxExemptionReasonCode = reasonCode == null ? null : new CodeType { Value = reasonCode },
                TaxExemptionReason = reasonText == null
                    ? null
                    : new List<TextType> { new TextType { Value = reasonText } }
            };
        }
        return this;
    }

    /// <summary>
    /// Replaces TaxTotal with a document-currency entry (<paramref name="docCurrency"/>, <paramref name="docVat"/>,
    /// single S-rate subtotal) plus a second accounting-currency (RON) entry carrying only a TaxAmount, exercising
    /// <see cref="RoEFactura.Validation.TotalsValidator.GetDocumentCurrencyTaxTotal"/>'s currency-based selection.
    /// </summary>
    public InvoiceBuilder WithDocumentCurrencyTaxTotals(string docCurrency, decimal docVat, decimal accountingVatRon)
    {
        var existingSubtotal = _invoice.TaxTotal?.FirstOrDefault()?.TaxSubtotal?.FirstOrDefault();
        decimal taxableAmount = existingSubtotal?.TaxableAmount?.Value ?? 100m;
        decimal percent = existingSubtotal?.TaxCategory?.Percent?.Value ?? 19m;

        _invoice.TaxTotal = new List<TaxTotalType>
        {
            new TaxTotalType
            {
                TaxAmount = new AmountType { Value = docVat, currencyID = docCurrency },
                TaxSubtotal = new List<TaxSubtotalType>
                {
                    new TaxSubtotalType
                    {
                        TaxableAmount = new AmountType { Value = taxableAmount, currencyID = docCurrency },
                        TaxAmount = new AmountType { Value = docVat, currencyID = docCurrency },
                        TaxCategory = new TaxCategoryType
                        {
                            ID = new IdentifierType { Value = "S" },
                            Percent = new PercentType { Value = percent },
                            TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
                        }
                    }
                }
            },
            new TaxTotalType
            {
                TaxAmount = new AmountType { Value = accountingVatRon, currencyID = "RON" }
            }
        };

        return this;
    }

    private static InvoiceType BuildBase()
    {
        var line = BuildValidLine("1");
        return new InvoiceType
        {
            CustomizationID = new IdentifierType { Value = RomanianConstants.CustomizationId },
            ID = new IdentifierType { Value = "INV-2024-001" },
            IssueDate = new DateType { Value = DateTime.Today },
            InvoiceTypeCode = new CodeType { Value = "380" },
            DocumentCurrencyCode = new CodeType { Value = "RON" },
            AccountingSupplierParty = BuildRomanianSeller("SC Vanzator SRL", "J12/100/2020", "RO12345678", "RO-CJ", "Cluj-Napoca"),
            AccountingCustomerParty = BuildRomanianBuyer("SC Cumparator SRL", "J40/200/2019", "RO87654321", "RO-IS", "Iasi"),
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
