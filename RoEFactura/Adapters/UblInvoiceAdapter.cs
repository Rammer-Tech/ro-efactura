using System.Linq;
using RoEFactura.Entities;
using UblSharp;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;

namespace RoEFactura.Adapters;

public static class UblInvoiceAdapter
{
    public static InvoiceType ToUbl(Invoice invoice)
    {
        var ubl = new InvoiceType
        {
            ID = new IdentifierType { Value = invoice.Id },
            IssueDate = new DateType { Value = invoice.IssueDate },
            DocumentCurrencyCode = new CodeType { Value = invoice.Currency },
            AccountingSupplierParty = new SupplierPartyType { Party = ToUblParty(invoice.Supplier) },
            AccountingCustomerParty = new CustomerPartyType { Party = ToUblParty(invoice.Customer) },
            LegalMonetaryTotal = new MonetaryTotalType
            {
                PayableAmount = new AmountType { Value = invoice.Total, currencyID = invoice.Currency }
            }
        };

        foreach (var line in invoice.Lines)
        {
            var lineType = new InvoiceLineType
            {
                ID = new IdentifierType { Value = line.LineNumber.ToString() },
                InvoicedQuantity = new QuantityType { Value = line.Quantity },
                LineExtensionAmount = new AmountType { Value = line.LineTotal, currencyID = invoice.Currency },
                Item = new ItemType
                {
                    Description = [ new TextType { Value = line.Description } ]
                },
                Price = new PriceType
                {
                    PriceAmount = new AmountType { Value = line.UnitPrice, currencyID = invoice.Currency }
                }
            };

            ubl.InvoiceLine.Add(lineType);
        }

        return ubl;
    }

    public static Invoice FromUbl(InvoiceType ubl)
    {
        var invoice = new Invoice
        {
            Id = ubl.ID?.Value ?? string.Empty,
            IssueDate = ubl.IssueDate ?? DateTime.MinValue,
            Currency = ubl.DocumentCurrencyCode?.Value ?? "RON",
            Supplier = FromUblParty(ubl.AccountingSupplierParty?.Party),
            Customer = FromUblParty(ubl.AccountingCustomerParty?.Party)
        };

        if (ubl.InvoiceLine != null)
        {
            foreach (var line in ubl.InvoiceLine)
            {
                invoice.Lines.Add(new InvoiceLine
                {
                    LineNumber = int.TryParse(line.ID?.Value, out var n) ? n : 0,
                    Description = line.Item?.Description?.FirstOrDefault()?.Value ?? string.Empty,
                    Quantity = line.InvoicedQuantity?.Value ?? 0m,
                    UnitPrice = line.Price?.PriceAmount?.Value ?? 0m
                });
            }
        }

        return invoice;
    }

    private static PartyType ToUblParty(Party party)
    {
        return new PartyType
        {
            PartyName = new PartyNameType { Name = new NameType { Value = party.Name } },
            PartyTaxScheme = new[]
            {
                new PartyTaxSchemeType { CompanyID = new IdentifierType { Value = party.VatId } }
            },
            PostalAddress = new AddressType
            {
                StreetName = new NameType { Value = party.Address.Street },
                CityName = new NameType { Value = party.Address.City },
                PostalZone = new TextType { Value = party.Address.PostalCode },
                Country = new CountryType
                {
                    IdentificationCode = new CodeType { Value = party.Address.CountryCode }
                }
            }
        };
    }

    private static Party FromUblParty(PartyType? party)
    {
        if (party == null) return new Party();

        return new Party
        {
            Name = party.PartyName?.Name?.Value ?? string.Empty,
            VatId = party.PartyTaxScheme?.FirstOrDefault()?.CompanyID?.Value ?? string.Empty,
            Address = new Address
            {
                Street = party.PostalAddress?.StreetName?.Value ?? string.Empty,
                City = party.PostalAddress?.CityName?.Value ?? string.Empty,
                PostalCode = party.PostalAddress?.PostalZone?.Value ?? string.Empty,
                CountryCode = party.PostalAddress?.Country?.IdentificationCode?.Value ?? string.Empty
            }
        };
    }
}
