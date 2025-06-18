using System.Collections.Generic;

namespace RoEFactura.Entities;

public class InvoiceLine
{
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public bool FreeOfChargeIndicator { get; set; }
    public List<Period> InvoicePeriods { get; set; } = new();
    public List<OrderLineReference> OrderLineReferences { get; set; } = new();
    public List<LineReference> DespatchLineReferences { get; set; } = new();
    public List<LineReference> ReceiptLineReferences { get; set; } = new();
    public List<AllowanceCharge> AllowanceCharges { get; set; } = new();
    public List<TaxTotal> TaxTotals { get; set; } = new();
    public Item Item { get; set; } = new();
    public List<InvoiceLine> SubInvoiceLines { get; set; } = new();
    public PriceExtension ItemPriceExtension { get; set; } = new();
    public decimal LineTotal => Quantity * UnitPrice;
}
