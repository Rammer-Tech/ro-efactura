using AutoMapper;
using RoEFactura.Domain.Entities;
using RoEFactura.Domain.ValueObjects;
using RoEFactura.Validation.Constants;
using UblSharp;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;

namespace RoEFactura.Mapping.Profiles;

public class DomainToUblProfile : Profile
{
    public DomainToUblProfile()
    {
        // Invoice mapping
        CreateMap<Invoice, InvoiceType>()
            .ForMember(d => d.UBLVersionID, opt => opt.MapFrom(src => new UBLVersionIDType { Value = "2.1" }))
            .ForMember(d => d.CustomizationID, opt => opt.MapFrom(src => new CustomizationIDType { Value = src.CustomizationId ?? RomanianConstants.RoCiusCustomizationId }))
            .ForMember(d => d.ID, opt => opt.MapFrom(src => new IDType { Value = src.Number }))
            .ForMember(d => d.IssueDate, opt => opt.MapFrom(src => new IssueDateType { Value = src.IssueDate.ToString("yyyy-MM-dd") }))
            .ForMember(d => d.InvoiceTypeCode, opt => opt.MapFrom(src => new InvoiceTypeCodeType { Value = src.TypeCode }))
            .ForMember(d => d.DocumentCurrencyCode, opt => opt.MapFrom(src => new DocumentCurrencyCodeType { Value = src.CurrencyCode }))
            .ForMember(d => d.TaxCurrencyCode, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.VatCurrencyCode) 
                ? new TaxCurrencyCodeType { Value = src.VatCurrencyCode } : null))
            .ForMember(d => d.DueDate, opt => opt.MapFrom(src => src.DueDate.HasValue 
                ? new DueDateType { Value = src.DueDate.Value.ToString("yyyy-MM-dd") } : null))
            .ForMember(d => d.AccountingSupplierParty, opt => opt.MapFrom(src => CreateSupplierParty(src.Seller)))
            .ForMember(d => d.AccountingCustomerParty, opt => opt.MapFrom(src => CreateCustomerParty(src.Buyer)))
            .ForMember(d => d.PayeeParty, opt => opt.MapFrom(src => src.Payee))
            .ForMember(d => d.InvoiceLine, opt => opt.MapFrom(src => src.Lines))
            .ForMember(d => d.TaxTotal, opt => opt.MapFrom(src => CreateTaxTotals(src)))
            .ForMember(d => d.LegalMonetaryTotal, opt => opt.MapFrom(src => CreateLegalMonetaryTotal(src)))
            .ForMember(d => d.PaymentTerms, opt => opt.MapFrom(src => CreatePaymentTerms(src)))
            .ForMember(d => d.Delivery, opt => opt.MapFrom(src => CreateDelivery(src)))
            .ForMember(d => d.InvoicePeriod, opt => opt.MapFrom(src => CreateInvoicePeriod(src)));

        // Party mappings
        CreateMap<Party, PartyType>()
            .ForMember(d => d.PartyName, opt => opt.MapFrom(src => new List<PartyNameType> 
                { new() { Name = new NameType1 { Value = src.Name } } }))
            .ForMember(d => d.PostalAddress, opt => opt.MapFrom(src => src.Address))
            .ForMember(d => d.Contact, opt => opt.MapFrom(src => src.Contact != null ? new List<ContactType> { src.Contact } : new List<ContactType>()))
            .ForMember(d => d.PartyLegalEntity, opt => opt.MapFrom(src => CreatePartyLegalEntity(src)))
            .ForMember(d => d.PartyTaxScheme, opt => opt.MapFrom(src => CreatePartyTaxScheme(src)));

        // Address mapping
        CreateMap<Address, AddressType>()
            .ForMember(d => d.StreetName, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Line1) 
                ? new StreetNameType { Value = src.Line1 } : null))
            .ForMember(d => d.AdditionalStreetName, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Line2) 
                ? new AdditionalStreetNameType { Value = src.Line2 } : null))
            .ForMember(d => d.CityName, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.City) 
                ? new CityNameType { Value = src.City } : null))
            .ForMember(d => d.PostalZone, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.PostalCode) 
                ? new PostalZoneType { Value = src.PostalCode } : null))
            .ForMember(d => d.CountrySubentity, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.CountrySubdivision) 
                ? new CountrySubentityType { Value = src.CountrySubdivision } : null))
            .ForMember(d => d.Country, opt => opt.MapFrom(src => new CountryType 
                { IdentificationCode = new IdentificationCodeType { Value = src.CountryCode } }));

        // Contact mapping
        CreateMap<Contact, ContactType>()
            .ForMember(d => d.Name, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Name) 
                ? new NameType1 { Value = src.Name } : null))
            .ForMember(d => d.Telephone, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Telephone) 
                ? new TelephoneType { Value = src.Telephone } : null))
            .ForMember(d => d.ElectronicMail, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Email) 
                ? new ElectronicMailType { Value = src.Email } : null));

        // Invoice line mapping
        CreateMap<InvoiceLine, InvoiceLineType>()
            .ForMember(d => d.ID, opt => opt.MapFrom(src => new IDType { Value = src.LineId }))
            .ForMember(d => d.Note, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Note) 
                ? new List<NoteType> { new() { Value = src.Note } } : new List<NoteType>()))
            .ForMember(d => d.InvoicedQuantity, opt => opt.MapFrom(src => new QuantityType 
                { Value = src.Quantity, unitCode = src.UnitCode }))
            .ForMember(d => d.LineExtensionAmount, opt => opt.MapFrom(src => new AmountType 
                { Value = src.LineExtensionAmount }))
            .ForMember(d => d.Price, opt => opt.MapFrom(src => src.Price))
            .ForMember(d => d.Item, opt => opt.MapFrom(src => CreateItem(src)));

        // Price mapping
        CreateMap<Price, PriceType>()
            .ForMember(d => d.PriceAmount, opt => opt.MapFrom(src => new AmountType { Value = src.NetUnitPrice }))
            .ForMember(d => d.BaseQuantity, opt => opt.MapFrom(src => src.BaseQuantity.HasValue 
                ? new QuantityType { Value = src.BaseQuantity.Value, unitCode = src.BaseUom } : null));
    }

    // Helper methods
    private static SupplierPartyType CreateSupplierParty(Party party)
    {
        return new SupplierPartyType
        {
            Party = new PartyType
            {
                PartyName = new List<PartyNameType> { new() { Name = new NameType1 { Value = party.Name } } },
                PostalAddress = CreateAddressType(party.Address),
                Contact = party.Contact != null ? new List<ContactType> { CreateContactType(party.Contact) } : new List<ContactType>(),
                PartyLegalEntity = CreatePartyLegalEntityList(party),
                PartyTaxScheme = CreatePartyTaxSchemeList(party)
            }
        };
    }

    private static CustomerPartyType CreateCustomerParty(Party party)
    {
        return new CustomerPartyType
        {
            Party = new PartyType
            {
                PartyName = new List<PartyNameType> { new() { Name = new NameType1 { Value = party.Name } } },
                PostalAddress = CreateAddressType(party.Address),
                Contact = party.Contact != null ? new List<ContactType> { CreateContactType(party.Contact) } : new List<ContactType>(),
                PartyLegalEntity = CreatePartyLegalEntityList(party),
                PartyTaxScheme = CreatePartyTaxSchemeList(party)
            }
        };
    }

    private static AddressType CreateAddressType(Address address)
    {
        return new AddressType
        {
            StreetName = !string.IsNullOrEmpty(address.Line1) ? new StreetNameType { Value = address.Line1 } : null,
            AdditionalStreetName = !string.IsNullOrEmpty(address.Line2) ? new AdditionalStreetNameType { Value = address.Line2 } : null,
            CityName = !string.IsNullOrEmpty(address.City) ? new CityNameType { Value = address.City } : null,
            PostalZone = !string.IsNullOrEmpty(address.PostalCode) ? new PostalZoneType { Value = address.PostalCode } : null,
            CountrySubentity = !string.IsNullOrEmpty(address.CountrySubdivision) ? new CountrySubentityType { Value = address.CountrySubdivision } : null,
            Country = new CountryType { IdentificationCode = new IdentificationCodeType { Value = address.CountryCode } }
        };
    }

    private static ContactType CreateContactType(Contact contact)
    {
        return new ContactType
        {
            Name = !string.IsNullOrEmpty(contact.Name) ? new NameType1 { Value = contact.Name } : null,
            Telephone = !string.IsNullOrEmpty(contact.Telephone) ? new TelephoneType { Value = contact.Telephone } : null,
            ElectronicMail = !string.IsNullOrEmpty(contact.Email) ? new ElectronicMailType { Value = contact.Email } : null
        };
    }

    private static List<PartyLegalEntityType> CreatePartyLegalEntity(Party party)
    {
        if (string.IsNullOrEmpty(party.LegalRegistrationId))
            return new List<PartyLegalEntityType>();

        return new List<PartyLegalEntityType>
        {
            new()
            {
                CompanyID = new CompanyIDType { Value = party.LegalRegistrationId }
            }
        };
    }

    private static List<PartyTaxSchemeType> CreatePartyTaxScheme(Party party)
    {
        if (string.IsNullOrEmpty(party.VatIdentifier))
            return new List<PartyTaxSchemeType>();

        return new List<PartyTaxSchemeType>
        {
            new()
            {
                CompanyID = new CompanyIDType { Value = party.VatIdentifier },
                TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } }
            }
        };
    }

    private static List<PartyLegalEntityType> CreatePartyLegalEntityList(Party party)
    {
        if (string.IsNullOrEmpty(party.LegalRegistrationId))
            return new List<PartyLegalEntityType>();

        return new List<PartyLegalEntityType>
        {
            new()
            {
                CompanyID = new CompanyIDType { Value = party.LegalRegistrationId }
            }
        };
    }

    private static List<PartyTaxSchemeType> CreatePartyTaxSchemeList(Party party)
    {
        if (string.IsNullOrEmpty(party.VatIdentifier))
            return new List<PartyTaxSchemeType>();

        return new List<PartyTaxSchemeType>
        {
            new()
            {
                CompanyID = new CompanyIDType { Value = party.VatIdentifier },
                TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } }
            }
        };
    }

    private static List<TaxTotalType> CreateTaxTotals(Invoice invoice)
    {
        if (!invoice.VatBreakdowns.Any())
            return new List<TaxTotalType>();

        var taxTotal = new TaxTotalType
        {
            TaxAmount = new AmountType { Value = invoice.TotalVat ?? 0 },
            TaxSubtotal = invoice.VatBreakdowns.Select(vb => new TaxSubtotalType
            {
                TaxableAmount = new AmountType { Value = vb.TaxableAmount },
                TaxAmount = new AmountType { Value = vb.TaxAmount },
                TaxCategory = new TaxCategoryType
                {
                    ID = new IDType { Value = vb.VatCategory },
                    Percent = vb.VatRate.HasValue ? new PercentType1 { Value = vb.VatRate.Value } : null,
                    TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } },
                    TaxExemptionReason = !string.IsNullOrEmpty(vb.ExemptionReason) 
                        ? new List<TaxExemptionReasonType> { new() { Value = vb.ExemptionReason } } 
                        : new List<TaxExemptionReasonType>()
                }
            }).ToList()
        };

        return new List<TaxTotalType> { taxTotal };
    }

    private static MonetaryTotalType CreateLegalMonetaryTotal(Invoice invoice)
    {
        return new MonetaryTotalType
        {
            LineExtensionAmount = new AmountType { Value = invoice.SumOfLineNet },
            TaxExclusiveAmount = new AmountType { Value = invoice.TotalWithoutVat },
            TaxInclusiveAmount = new AmountType { Value = invoice.TotalWithVat },
            PayableAmount = new AmountType { Value = invoice.AmountDue },
            AllowanceTotalAmount = invoice.SumOfAllowances.HasValue ? new AmountType { Value = invoice.SumOfAllowances.Value } : null,
            ChargeTotalAmount = invoice.SumOfCharges.HasValue ? new AmountType { Value = invoice.SumOfCharges.Value } : null
        };
    }

    private static List<PaymentTermsType>? CreatePaymentTerms(Invoice invoice)
    {
        if (string.IsNullOrEmpty(invoice.PaymentTermsText))
            return null;

        return new List<PaymentTermsType>
        {
            new()
            {
                Note = new List<NoteType> { new() { Value = invoice.PaymentTermsText } }
            }
        };
    }

    private static List<DeliveryType>? CreateDelivery(Invoice invoice)
    {
        if (!invoice.ActualDeliveryDate.HasValue && string.IsNullOrEmpty(invoice.DeliveryNoteReference))
            return null;

        var delivery = new DeliveryType();
        
        if (invoice.ActualDeliveryDate.HasValue)
        {
            delivery.ActualDeliveryDate = new ActualDeliveryDateType 
            { 
                Value = invoice.ActualDeliveryDate.Value.ToString("yyyy-MM-dd") 
            };
        }
        
        if (!string.IsNullOrEmpty(invoice.DeliveryNoteReference))
        {
            delivery.ID = new IDType { Value = invoice.DeliveryNoteReference };
        }

        return new List<DeliveryType> { delivery };
    }

    private static List<PeriodType>? CreateInvoicePeriod(Invoice invoice)
    {
        if (!invoice.InvoicingPeriodStart.HasValue && !invoice.InvoicingPeriodEnd.HasValue)
            return null;

        var period = new PeriodType();
        
        if (invoice.InvoicingPeriodStart.HasValue)
        {
            period.StartDate = new StartDateType 
            { 
                Value = invoice.InvoicingPeriodStart.Value.ToString("yyyy-MM-dd") 
            };
        }
        
        if (invoice.InvoicingPeriodEnd.HasValue)
        {
            period.EndDate = new EndDateType 
            { 
                Value = invoice.InvoicingPeriodEnd.Value.ToString("yyyy-MM-dd") 
            };
        }

        return new List<PeriodType> { period };
    }

    private static ItemType CreateItem(InvoiceLine line)
    {
        return new ItemType
        {
            Name = new NameType1 { Value = line.ItemName },
            Description = !string.IsNullOrEmpty(line.ItemDescription) 
                ? new List<DescriptionType> { new() { Value = line.ItemDescription } } 
                : new List<DescriptionType>(),
            OriginCountry = !string.IsNullOrEmpty(line.OriginCountryCode) 
                ? new CountryType { IdentificationCode = new IdentificationCodeType { Value = line.OriginCountryCode } } 
                : null,
            ClassifiedTaxCategory = new List<TaxCategoryType>
            {
                new()
                {
                    ID = new IDType { Value = line.VatCategory },
                    Percent = line.VatRate.HasValue ? new PercentType1 { Value = line.VatRate.Value } : null,
                    TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } }
                }
            }
        };
    }
}