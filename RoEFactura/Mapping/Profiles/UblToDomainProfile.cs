using AutoMapper;
using RoEFactura.Domain.Entities;
using RoEFactura.Domain.ValueObjects;
using UblSharp;
using UblSharp.CommonAggregateComponents;

namespace RoEFactura.Mapping.Profiles;

public class UblToDomainProfile : Profile
{
    public UblToDomainProfile()
    {
        // Invoice mapping
        CreateMap<InvoiceType, Invoice>()
            .ForMember(d => d.Id, opt => opt.Ignore()) // EF will generate
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAt, opt => opt.Ignore())
            .ForMember(d => d.Number, opt => opt.MapFrom(s => s.ID != null ? s.ID.Value : ""))
            .ForMember(d => d.IssueDate, opt => opt.MapFrom(s => ParseDateOnly(s.IssueDate != null ? s.IssueDate.Value : "")))
            .ForMember(d => d.TypeCode, opt => opt.MapFrom(s => s.InvoiceTypeCode != null ? s.InvoiceTypeCode.Value : ""))
            .ForMember(d => d.CurrencyCode, opt => opt.MapFrom(s => s.DocumentCurrencyCode != null ? s.DocumentCurrencyCode.Value : ""))
            .ForMember(d => d.VatCurrencyCode, opt => opt.MapFrom(s => s.TaxCurrencyCode != null ? s.TaxCurrencyCode.Value : null))
            .ForMember(d => d.DueDate, opt => opt.MapFrom(s => ParseDateOnlyNullable(s.DueDate != null ? s.DueDate.Value : null)))
            .ForMember(d => d.PaymentTermsText, opt => opt.MapFrom(s => GetPaymentTermsText(s)))
            .ForMember(d => d.ActualDeliveryDate, opt => opt.MapFrom(s => GetDeliveryDate(s)))
            .ForMember(d => d.DeliveryNoteReference, opt => opt.MapFrom(s => GetDeliveryNoteReference(s)))
            .ForMember(d => d.InvoicingPeriodStart, opt => opt.MapFrom(s => GetInvoicePeriodStart(s)))
            .ForMember(d => d.InvoicingPeriodEnd, opt => opt.MapFrom(s => GetInvoicePeriodEnd(s)))
            .ForMember(d => d.CustomizationId, opt => opt.MapFrom(s => s.CustomizationID != null ? s.CustomizationID.Value : null))
            .ForMember(d => d.SumOfLineNet, opt => opt.MapFrom(s => s.LegalMonetaryTotal != null && s.LegalMonetaryTotal.LineExtensionAmount != null ? s.LegalMonetaryTotal.LineExtensionAmount.Value : 0))
            .ForMember(d => d.TotalWithoutVat, opt => opt.MapFrom(s => s.LegalMonetaryTotal != null && s.LegalMonetaryTotal.TaxExclusiveAmount != null ? s.LegalMonetaryTotal.TaxExclusiveAmount.Value : 0))
            .ForMember(d => d.TotalVat, opt => opt.MapFrom(s => GetTotalVatAmount(s)))
            .ForMember(d => d.TotalWithVat, opt => opt.MapFrom(s => s.LegalMonetaryTotal != null && s.LegalMonetaryTotal.TaxInclusiveAmount != null ? s.LegalMonetaryTotal.TaxInclusiveAmount.Value : 0))
            .ForMember(d => d.AmountDue, opt => opt.MapFrom(s => s.LegalMonetaryTotal != null && s.LegalMonetaryTotal.PayableAmount != null ? s.LegalMonetaryTotal.PayableAmount.Value : 0))
            .ForMember(d => d.SellerId, opt => opt.Ignore()) // Will be set after Party creation
            .ForMember(d => d.BuyerId, opt => opt.Ignore()) // Will be set after Party creation
            .ForMember(d => d.PayeeId, opt => opt.Ignore()) // Will be set after Party creation
            .ForMember(d => d.Seller, opt => opt.MapFrom(s => s.AccountingSupplierParty))
            .ForMember(d => d.Buyer, opt => opt.MapFrom(s => s.AccountingCustomerParty))
            .ForMember(d => d.Payee, opt => opt.MapFrom(s => s.PayeeParty))
            .ForMember(d => d.Lines, opt => opt.MapFrom(s => s.InvoiceLine))
            .ForMember(d => d.VatBreakdowns, opt => opt.MapFrom(s => GetVatBreakdowns(s)));

        // Party mappings
        CreateMap<SupplierPartyType, Party>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAt, opt => opt.Ignore())
            .ForMember(d => d.Name, opt => opt.MapFrom(s => GetPartyName(s.Party)))
            .ForMember(d => d.AdditionalLegalInfo, opt => opt.MapFrom(s => GetLegalInfo(s.Party)))
            .ForMember(d => d.LegalRegistrationId, opt => opt.MapFrom(s => GetLegalRegistrationId(s.Party)))
            .ForMember(d => d.VatIdentifier, opt => opt.MapFrom(s => GetVatIdentifier(s.Party)))
            .ForMember(d => d.Address, opt => opt.MapFrom(s => s.Party != null ? s.Party.PostalAddress : null))
            .ForMember(d => d.Contact, opt => opt.MapFrom(s => s.Party != null ? s.Party.Contact.FirstOrDefault() : null));

        CreateMap<CustomerPartyType, Party>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAt, opt => opt.Ignore())
            .ForMember(d => d.Name, opt => opt.MapFrom(s => GetPartyName(s.Party)))
            .ForMember(d => d.AdditionalLegalInfo, opt => opt.MapFrom(s => GetLegalInfo(s.Party)))
            .ForMember(d => d.LegalRegistrationId, opt => opt.MapFrom(s => GetLegalRegistrationId(s.Party)))
            .ForMember(d => d.VatIdentifier, opt => opt.MapFrom(s => GetVatIdentifier(s.Party)))
            .ForMember(d => d.Address, opt => opt.MapFrom(s => s.Party != null ? s.Party.PostalAddress : null))
            .ForMember(d => d.Contact, opt => opt.MapFrom(s => s.Party != null ? s.Party.Contact.FirstOrDefault() : null));

        CreateMap<PartyType, Party>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAt, opt => opt.Ignore())
            .ForMember(d => d.Name, opt => opt.MapFrom(s => GetPartyName(s)))
            .ForMember(d => d.AdditionalLegalInfo, opt => opt.MapFrom(s => GetLegalInfo(s)))
            .ForMember(d => d.LegalRegistrationId, opt => opt.MapFrom(s => GetLegalRegistrationId(s)))
            .ForMember(d => d.VatIdentifier, opt => opt.MapFrom(s => GetVatIdentifier(s)))
            .ForMember(d => d.Address, opt => opt.MapFrom(s => s.PostalAddress))
            .ForMember(d => d.Contact, opt => opt.MapFrom(s => s.Contact.FirstOrDefault()));

        // Address mapping
        CreateMap<AddressType, Address>()
            .ForMember(d => d.Line1, opt => opt.MapFrom(s => GetAddressLine1(s)))
            .ForMember(d => d.Line2, opt => opt.MapFrom(s => GetAddressLine2(s)))
            .ForMember(d => d.City, opt => opt.MapFrom(s => s.CityName != null ? s.CityName.Value : null))
            .ForMember(d => d.PostalCode, opt => opt.MapFrom(s => s.PostalZone != null ? s.PostalZone.Value : null))
            .ForMember(d => d.CountrySubdivision, opt => opt.MapFrom(s => s.CountrySubentity != null ? s.CountrySubentity.Value : null))
            .ForMember(d => d.CountryCode, opt => opt.MapFrom(s => s.Country != null && s.Country.IdentificationCode != null ? s.Country.IdentificationCode.Value : "RO"));

        // Contact mapping
        CreateMap<ContactType, Contact>()
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name != null ? s.Name.Value : null))
            .ForMember(d => d.Telephone, opt => opt.MapFrom(s => s.Telephone != null ? s.Telephone.Value : null))
            .ForMember(d => d.Email, opt => opt.MapFrom(s => s.ElectronicMail != null ? s.ElectronicMail.Value : null));

        // Invoice line mapping
        CreateMap<InvoiceLineType, InvoiceLine>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAt, opt => opt.Ignore())
            .ForMember(d => d.InvoiceId, opt => opt.Ignore())
            .ForMember(d => d.Invoice, opt => opt.Ignore())
            .ForMember(d => d.LineId, opt => opt.MapFrom(s => s.ID != null ? s.ID.Value : ""))
            .ForMember(d => d.Note, opt => opt.MapFrom(s => GetLineNote(s)))
            .ForMember(d => d.Quantity, opt => opt.MapFrom(s => s.InvoicedQuantity != null ? s.InvoicedQuantity.Value : 0))
            .ForMember(d => d.UnitCode, opt => opt.MapFrom(s => s.InvoicedQuantity != null ? s.InvoicedQuantity.unitCode ?? "" : ""))
            .ForMember(d => d.LineExtensionAmount, opt => opt.MapFrom(s => s.LineExtensionAmount != null ? s.LineExtensionAmount.Value : 0))
            .ForMember(d => d.PeriodStart, opt => opt.MapFrom(s => GetLinePeriodStart(s)))
            .ForMember(d => d.PeriodEnd, opt => opt.MapFrom(s => GetLinePeriodEnd(s)))
            .ForMember(d => d.VatCategory, opt => opt.MapFrom(s => GetLineVatCategory(s)))
            .ForMember(d => d.VatRate, opt => opt.MapFrom(s => GetLineVatRate(s)))
            .ForMember(d => d.ItemName, opt => opt.MapFrom(s => s.Item != null && s.Item.Name != null ? s.Item.Name.Value : ""))
            .ForMember(d => d.ItemDescription, opt => opt.MapFrom(s => GetItemDescription(s)))
            .ForMember(d => d.OriginCountryCode, opt => opt.MapFrom(s => GetOriginCountryCode(s)))
            .ForMember(d => d.Price, opt => opt.MapFrom(s => s.Price));

        // Price mapping
        CreateMap<PriceType, Price>()
            .ForMember(d => d.NetUnitPrice, opt => opt.MapFrom(s => s.PriceAmount != null ? s.PriceAmount.Value : 0))
            .ForMember(d => d.BaseQuantity, opt => opt.MapFrom(s => s.BaseQuantity != null ? s.BaseQuantity.Value : (decimal?)null))
            .ForMember(d => d.BaseUom, opt => opt.MapFrom(s => s.BaseQuantity != null ? s.BaseQuantity.unitCode : null));
    }

    // Helper methods for complex mappings
    private static DateOnly ParseDateOnly(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return default;

        if (DateOnly.TryParse(dateString, out var result))
            return result;

        if (DateTime.TryParse(dateString, out var dateTime))
            return DateOnly.FromDateTime(dateTime);

        return default;
    }

    private static DateOnly? ParseDateOnlyNullable(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        if (DateOnly.TryParse(dateString, out var result))
            return result;

        if (DateTime.TryParse(dateString, out var dateTime))
            return DateOnly.FromDateTime(dateTime);

        return null;
    }

    private static string GetPartyName(PartyType? party)
    {
        return party?.PartyName?.FirstOrDefault()?.Name?.Value ?? "";
    }

    private static string? GetLegalInfo(PartyType? party)
    {
        return party?.PartyLegalEntity?.FirstOrDefault()?.CompanyLegalForm?.Value;
    }

    private static string? GetLegalRegistrationId(PartyType? party)
    {
        return party?.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value;
    }

    private static string? GetVatIdentifier(PartyType? party)
    {
        return party?.PartyTaxScheme?.FirstOrDefault()?.CompanyID?.Value;
    }

    private static string? GetAddressLine1(AddressType address)
    {
        return address?.StreetName?.Value ?? address?.AddressLine?.FirstOrDefault()?.Line?.Value;
    }

    private static string? GetAddressLine2(AddressType address)
    {
        return address?.AdditionalStreetName?.Value ?? address?.AddressLine?.Skip(1).FirstOrDefault()?.Line?.Value;
    }

    private static string? GetPaymentTermsText(InvoiceType invoice)
    {
        return invoice?.PaymentTerms?.FirstOrDefault()?.Note?.FirstOrDefault()?.Value;
    }

    private static DateOnly? GetDeliveryDate(InvoiceType invoice)
    {
        var deliveryDateStr = invoice?.Delivery?.FirstOrDefault()?.ActualDeliveryDate?.Value;
        return ParseDateOnlyNullable(deliveryDateStr);
    }

    private static string? GetDeliveryNoteReference(InvoiceType invoice)
    {
        return invoice?.Delivery?.FirstOrDefault()?.ID?.Value;
    }

    private static DateOnly? GetInvoicePeriodStart(InvoiceType invoice)
    {
        var startDateStr = invoice?.InvoicePeriod?.FirstOrDefault()?.StartDate?.Value;
        return ParseDateOnlyNullable(startDateStr);
    }

    private static DateOnly? GetInvoicePeriodEnd(InvoiceType invoice)
    {
        var endDateStr = invoice?.InvoicePeriod?.FirstOrDefault()?.EndDate?.Value;
        return ParseDateOnlyNullable(endDateStr);
    }

    private static decimal? GetTotalVatAmount(InvoiceType invoice)
    {
        return invoice?.TaxTotal?.FirstOrDefault()?.TaxAmount?.Value;
    }

    private static List<VatBreakdown> GetVatBreakdowns(InvoiceType invoice)
    {
        var breakdowns = new List<VatBreakdown>();
        
        var taxTotal = invoice?.TaxTotal?.FirstOrDefault();
        if (taxTotal?.TaxSubtotal != null)
        {
            foreach (var subtotal in taxTotal.TaxSubtotal)
            {
                breakdowns.Add(new VatBreakdown
                {
                    VatCategory = subtotal.TaxCategory?.ID?.Value ?? "",
                    VatRate = subtotal.TaxCategory?.Percent?.Value,
                    TaxableAmount = subtotal.TaxableAmount?.Value ?? 0,
                    TaxAmount = subtotal.TaxAmount?.Value ?? 0,
                    ExemptionReason = subtotal.TaxCategory?.TaxExemptionReason?.FirstOrDefault()?.Value
                });
            }
        }

        return breakdowns;
    }

    private static string? GetLineNote(InvoiceLineType line)
    {
        return line?.Note?.FirstOrDefault()?.Value;
    }

    private static DateOnly? GetLinePeriodStart(InvoiceLineType line)
    {
        var startDateStr = line?.InvoicePeriod?.FirstOrDefault()?.StartDate?.Value;
        return ParseDateOnlyNullable(startDateStr);
    }

    private static DateOnly? GetLinePeriodEnd(InvoiceLineType line)
    {
        var endDateStr = line?.InvoicePeriod?.FirstOrDefault()?.EndDate?.Value;
        return ParseDateOnlyNullable(endDateStr);
    }

    private static string GetLineVatCategory(InvoiceLineType line)
    {
        return line?.Item?.ClassifiedTaxCategory?.FirstOrDefault()?.ID?.Value ?? "";
    }

    private static decimal? GetLineVatRate(InvoiceLineType line)
    {
        return line?.Item?.ClassifiedTaxCategory?.FirstOrDefault()?.Percent?.Value;
    }

    private static string? GetItemDescription(InvoiceLineType line)
    {
        return line?.Item?.Description?.FirstOrDefault()?.Value;
    }

    private static string? GetOriginCountryCode(InvoiceLineType line)
    {
        return line?.Item?.OriginCountry?.IdentificationCode?.Value;
    }
}