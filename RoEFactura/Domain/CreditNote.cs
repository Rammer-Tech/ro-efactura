using System.Collections.Generic;
using System.Linq;

namespace RoEFactura.Domain;

public class CreditNote
{
    public string Id { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public string Currency { get; set; } = "RON";
    public Party Supplier { get; set; } = new();
    public Party Customer { get; set; } = new();
    public List<InvoiceLine> Lines { get; set; } = new();

    public decimal Total => Lines.Sum(l => l.LineTotal);
}
