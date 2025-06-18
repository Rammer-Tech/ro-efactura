using System.Linq;
using RoEFactura.Entities;
using UblSharp;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;

namespace RoEFactura.Adapters;

public static class UblCreditNoteAdapter
{
    public static CreditNoteType ToUbl(CreditNote creditNote)
    {
        var ubl = new CreditNoteType
        {
            ID = new IdentifierType { Value = creditNote.Id },
            IssueDate = new DateType { Value = creditNote.IssueDate },
            DocumentCurrencyCode = new CodeType { Value = creditNote.Currency },
            AccountingSupplierParty = new SupplierPartyType { Party = ToUblParty(creditNote.Supplier) },
            AccountingCustomerParty = new CustomerPartyType { Party = ToUblParty(creditNote.Customer) },
            LegalMonetaryTotal = new MonetaryTotalType
            {
                PayableAmount = new AmountType { Value = creditNote.Total, currencyID = creditNote.Currency }
            }
        };

        foreach (var line in creditNote.Lines)
        {
            var lineType = new CreditNoteLineType
            {
                ID = new IdentifierType { Value = line.LineNumber.ToString() },
                CreditedQuantity = new QuantityType { Value = line.Quantity },
                LineExtensionAmount = new AmountType { Value = line.LineTotal, currencyID = creditNote.Currency },
                Item = new ItemType
                {
                    Description = [ new TextType { Value = line.Description } ]
                },
                Price = new PriceType
                {
                    PriceAmount = new AmountType { Value = line.UnitPrice, currencyID = creditNote.Currency }
                }
            };

            ubl.CreditNoteLine.Add(lineType);
        }

        return ubl;
    }

    public static CreditNote FromUbl(CreditNoteType ubl)
    {
        var cn = new CreditNote
        {
            Id = ubl.ID?.Value ?? string.Empty,
            IssueDate = ubl.IssueDate ?? DateTime.MinValue,
            Currency = ubl.DocumentCurrencyCode?.Value ?? "RON",
            Supplier = FromUblParty(ubl.AccountingSupplierParty?.Party),
            Customer = FromUblParty(ubl.AccountingCustomerParty?.Party)
        };

        if (ubl.CreditNoteLine != null)
        {
            foreach (var line in ubl.CreditNoteLine)
            {
                cn.Lines.Add(new InvoiceLine
                {
                    LineNumber = int.TryParse(line.ID?.Value, out var n) ? n : 0,
                    Description = line.Item?.Description?.FirstOrDefault()?.Value ?? string.Empty,
                    Quantity = line.CreditedQuantity?.Value ?? 0m,
                    UnitPrice = line.Price?.PriceAmount?.Value ?? 0m
                });
            }
        }

        return cn;
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
