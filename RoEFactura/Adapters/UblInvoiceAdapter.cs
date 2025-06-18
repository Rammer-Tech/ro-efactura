using System.Linq;
using System.Collections.Generic;
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
                PayableAmount = new AmountType { Value = invoice.LegalMonetaryTotal.PayableAmount != 0m ? invoice.LegalMonetaryTotal.PayableAmount : invoice.Total, currencyID = invoice.Currency }
            }
        };

        if (invoice.Notes.Any())
        {
            ubl.Note = invoice.Notes.Select(n => new TextType { Value = n }).ToList();
        }

        if (invoice.PaymentMeans.Any())
        {
            ubl.PaymentMeans = invoice.PaymentMeans.Select(pm => new PaymentMeansType
            {
                PaymentMeansCode = pm.Code != null ? new CodeType { Value = pm.Code } : null,
                PaymentID = string.IsNullOrEmpty(pm.PaymentId) ? null : new[] { new IdentifierType { Value = pm.PaymentId } }
            }).ToList();
        }

        if (invoice.PaymentTerms.Any())
        {
            ubl.PaymentTerms = invoice.PaymentTerms.Select(pt => new PaymentTermsType
            {
                Note = string.IsNullOrEmpty(pt.Note) ? null : new List<TextType> { new TextType { Value = pt.Note } }
            }).ToList();
        }

        if (invoice.Delivery?.ActualDeliveryDate != null)
        {
            ubl.Delivery = new List<DeliveryType>
            {
                new DeliveryType { ActualDeliveryDate = new DateType { Value = invoice.Delivery.ActualDeliveryDate.Value } }
            };
        }

        ubl.DespatchDocumentReference = invoice.DespatchDocumentReferences.Select(d => new DocumentReferenceType { ID = new IdentifierType { Value = d.Id } }).ToList();
        ubl.ProjectReference = invoice.ProjectReferences.Select(p => new ProjectReferenceType { ID = new IdentifierType { Value = p.Id } }).ToList();
        ubl.ReceiptDocumentReference = invoice.ReceiptDocumentReferences.Select(d => new DocumentReferenceType { ID = new IdentifierType { Value = d.Id } }).ToList();
        ubl.StatementDocumentReference = invoice.StatementDocumentReferences.Select(d => new DocumentReferenceType { ID = new IdentifierType { Value = d.Id } }).ToList();
        ubl.OriginatorDocumentReference = invoice.OriginatorDocumentReferences.Select(d => new DocumentReferenceType { ID = new IdentifierType { Value = d.Id } }).ToList();
        ubl.ContractDocumentReference = invoice.ContractDocumentReferences.Select(d => new DocumentReferenceType { ID = new IdentifierType { Value = d.Id } }).ToList();
        ubl.AdditionalDocumentReference = invoice.AdditionalDocumentReferences.Select(d => new DocumentReferenceType { ID = new IdentifierType { Value = d.Id } }).ToList();

        ubl.AllowanceCharge = invoice.AllowanceCharges.Select(a => new AllowanceChargeType
        {
            ChargeIndicator = new IndicatorType { Value = a.ChargeIndicator },
            Amount = new AmountType { Value = a.Amount, currencyID = invoice.Currency }
        }).ToList();

        ubl.TaxTotal = invoice.TaxTotals.Select(t => new TaxTotalType
        {
            TaxAmount = new AmountType { Value = t.Amount, currencyID = invoice.Currency }
        }).ToList();

        if (invoice.TaxExchangeRate?.Rate != null)
        {
            ubl.TaxExchangeRate = new ExchangeRateType
            {
                CalculationRate = new RateType { Value = invoice.TaxExchangeRate.Rate.Value }
            };
        }

        foreach (var line in invoice.Lines)
        {
            var lineType = new InvoiceLineType
            {
                ID = new IdentifierType { Value = line.LineNumber.ToString() },
                InvoicedQuantity = new QuantityType { Value = line.Quantity },
                LineExtensionAmount = new AmountType { Value = line.LineTotal, currencyID = invoice.Currency },
                FreeOfChargeIndicator = new IndicatorType { Value = line.FreeOfChargeIndicator },
                InvoicePeriod = line.InvoicePeriods.Select(p => new PeriodType
                {
                    StartDate = p.StartDate != null ? new DateType { Value = p.StartDate.Value } : null,
                    EndDate = p.EndDate != null ? new DateType { Value = p.EndDate.Value } : null
                }).ToList(),
                OrderLineReference = line.OrderLineReferences.Select(o => new OrderLineReferenceType { LineID = new IdentifierType { Value = o.LineId } }).ToList(),
                DespatchLineReference = line.DespatchLineReferences.Select(d => new LineReferenceType { LineID = new IdentifierType { Value = d.LineId } }).ToList(),
                ReceiptLineReference = line.ReceiptLineReferences.Select(r => new LineReferenceType { LineID = new IdentifierType { Value = r.LineId } }).ToList(),
                AllowanceCharge = line.AllowanceCharges.Select(a => new AllowanceChargeType
                {
                    ChargeIndicator = new IndicatorType { Value = a.ChargeIndicator },
                    Amount = new AmountType { Value = a.Amount, currencyID = invoice.Currency }
                }).ToList(),
                TaxTotal = line.TaxTotals.Select(t => new TaxTotalType { TaxAmount = new AmountType { Value = t.Amount, currencyID = invoice.Currency } }).ToList(),
                Item = new ItemType
                {
                    Description = new List<TextType>
                    {
                        new TextType { Value = line.Item.Description }
                    },
                    Name = string.IsNullOrEmpty(line.Item.Name) ? null : new NameType { Value = line.Item.Name }
                },
                Price = new PriceType
                {
                    PriceAmount = new AmountType { Value = line.UnitPrice, currencyID = invoice.Currency }
                }
            };

            if (line.SubInvoiceLines.Any())
            {
                lineType.SubInvoiceLine = line.SubInvoiceLines.Select(sl => new InvoiceLineType
                {
                    ID = new IdentifierType { Value = sl.LineNumber.ToString() },
                    InvoicedQuantity = new QuantityType { Value = sl.Quantity },
                    LineExtensionAmount = new AmountType { Value = sl.LineTotal, currencyID = invoice.Currency }
                }).ToList();
            }

            if (line.ItemPriceExtension.Amount != 0m)
            {
                lineType.ItemPriceExtension = new PriceExtensionType
                {
                    Amount = new AmountType { Value = line.ItemPriceExtension.Amount, currencyID = invoice.Currency }
                };
            }

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

        invoice.Notes = ubl.Note?.Select(n => n.Value ?? string.Empty).ToList() ?? new List<string>();
        invoice.PaymentMeans = ubl.PaymentMeans?.Select(pm => new PaymentMeans
        {
            Code = pm.PaymentMeansCode?.Value,
            PaymentId = pm.PaymentID?.FirstOrDefault()?.Value
        }).ToList() ?? new List<PaymentMeans>();
        invoice.PaymentTerms = ubl.PaymentTerms?.Select(pt => new PaymentTerms
        {
            Note = pt.Note?.FirstOrDefault()?.Value
        }).ToList() ?? new List<PaymentTerms>();

        if (ubl.Delivery?.Any() == true)
        {
            var del = ubl.Delivery.First();
            invoice.Delivery = new Delivery
            {
                ActualDeliveryDate = del.ActualDeliveryDate
            };
        }

        invoice.DespatchDocumentReferences = ubl.DespatchDocumentReference?.Select(d => new DocumentReference { Id = d.ID?.Value ?? string.Empty }).ToList() ?? new List<DocumentReference>();
        invoice.ProjectReferences = ubl.ProjectReference?.Select(p => new ProjectReference { Id = p.ID?.Value ?? string.Empty }).ToList() ?? new List<ProjectReference>();
        invoice.ReceiptDocumentReferences = ubl.ReceiptDocumentReference?.Select(d => new DocumentReference { Id = d.ID?.Value ?? string.Empty }).ToList() ?? new List<DocumentReference>();
        invoice.StatementDocumentReferences = ubl.StatementDocumentReference?.Select(d => new DocumentReference { Id = d.ID?.Value ?? string.Empty }).ToList() ?? new List<DocumentReference>();
        invoice.OriginatorDocumentReferences = ubl.OriginatorDocumentReference?.Select(d => new DocumentReference { Id = d.ID?.Value ?? string.Empty }).ToList() ?? new List<DocumentReference>();
        invoice.ContractDocumentReferences = ubl.ContractDocumentReference?.Select(d => new DocumentReference { Id = d.ID?.Value ?? string.Empty }).ToList() ?? new List<DocumentReference>();
        invoice.AdditionalDocumentReferences = ubl.AdditionalDocumentReference?.Select(d => new DocumentReference { Id = d.ID?.Value ?? string.Empty }).ToList() ?? new List<DocumentReference>();
        invoice.AllowanceCharges = ubl.AllowanceCharge?.Select(a => new AllowanceCharge { ChargeIndicator = a.ChargeIndicator?.Value ?? false, Amount = a.Amount?.Value ?? 0m }).ToList() ?? new List<AllowanceCharge>();
        invoice.TaxTotals = ubl.TaxTotal?.Select(t => new TaxTotal { Amount = t.TaxAmount?.Value ?? 0m }).ToList() ?? new List<TaxTotal>();
        invoice.TaxExchangeRate = ubl.TaxExchangeRate != null ? new ExchangeRate { Rate = ubl.TaxExchangeRate.CalculationRate } : null;
        invoice.LegalMonetaryTotal = new MonetaryTotal { PayableAmount = ubl.LegalMonetaryTotal?.PayableAmount?.Value ?? 0m, Currency = ubl.DocumentCurrencyCode?.Value ?? "RON" };

        if (ubl.InvoiceLine != null)
        {
            foreach (var line in ubl.InvoiceLine)
            {
                var invLine = new InvoiceLine
                {
                    LineNumber = int.TryParse(line.ID?.Value, out var n) ? n : 0,
                    Quantity = line.InvoicedQuantity?.Value ?? 0m,
                    UnitPrice = line.Price?.PriceAmount?.Value ?? 0m,
                    Item = new Item
                    {
                        Description = line.Item?.Description?.FirstOrDefault()?.Value ?? string.Empty,
                        Name = line.Item?.Name?.Value
                    },
                    FreeOfChargeIndicator = line.FreeOfChargeIndicator?.Value ?? false,
                    InvoicePeriods = line.InvoicePeriod?.Select(p => new Period { StartDate = p.StartDate, EndDate = p.EndDate }).ToList() ?? new List<Period>(),
                    OrderLineReferences = line.OrderLineReference?.Select(o => new OrderLineReference { LineId = o.LineID?.Value }).ToList() ?? new List<OrderLineReference>(),
                    DespatchLineReferences = line.DespatchLineReference?.Select(d => new LineReference { LineId = d.LineID?.Value }).ToList() ?? new List<LineReference>(),
                    ReceiptLineReferences = line.ReceiptLineReference?.Select(r => new LineReference { LineId = r.LineID?.Value }).ToList() ?? new List<LineReference>(),
                    AllowanceCharges = line.AllowanceCharge?.Select(a => new AllowanceCharge { ChargeIndicator = a.ChargeIndicator?.Value ?? false, Amount = a.Amount?.Value ?? 0m }).ToList() ?? new List<AllowanceCharge>(),
                    TaxTotals = line.TaxTotal?.Select(t => new TaxTotal { Amount = t.TaxAmount?.Value ?? 0m }).ToList() ?? new List<TaxTotal>(),
                    SubInvoiceLines = new List<InvoiceLine>(),
                    ItemPriceExtension = new PriceExtension { Amount = line.ItemPriceExtension?.Amount?.Value ?? 0m }
                };

                if (line.SubInvoiceLine != null)
                {
                    foreach (var sub in line.SubInvoiceLine)
                    {
                        invLine.SubInvoiceLines.Add(new InvoiceLine
                        {
                            LineNumber = int.TryParse(sub.ID?.Value, out var sn) ? sn : 0,
                            Quantity = sub.InvoicedQuantity?.Value ?? 0m,
                            UnitPrice = sub.Price?.PriceAmount?.Value ?? 0m
                        });
                    }
                }

                invoice.Lines.Add(invLine);
            }
        }

        return invoice;
    }

    private static PartyType ToUblParty(Party party)
    {
        return new PartyType
        {
            PartyName = new List<PartyNameType>
            {
                new PartyNameType { Name = new NameType { Value = party.Name } }
            },
            PartyTaxScheme = new List<PartyTaxSchemeType>
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
            Name = party.PartyName?.FirstOrDefault()?.Name?.Value ?? string.Empty,
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
