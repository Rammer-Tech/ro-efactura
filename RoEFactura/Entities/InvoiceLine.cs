namespace RoEFactura.Entities;

public class InvoiceLine
{
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}
