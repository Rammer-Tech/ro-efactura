using System.Collections.Generic;
using System.Linq;

namespace RoEFactura.Entities;

public class Invoice
{
    public string Id { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public string Currency { get; set; } = "RON";
    public Party Supplier { get; set; } = new();
    public Party Customer { get; set; } = new();
    public List<InvoiceLine> Lines { get; set; } = new();

    public List<string> Notes { get; set; } = new();
    public List<PaymentMeans> PaymentMeans { get; set; } = new();
    public List<PaymentTerms> PaymentTerms { get; set; } = new();
    public List<DocumentReference> DespatchDocumentReferences { get; set; } = new();
    public List<ProjectReference> ProjectReferences { get; set; } = new();
    public List<DocumentReference> ReceiptDocumentReferences { get; set; } = new();
    public List<DocumentReference> StatementDocumentReferences { get; set; } = new();
    public List<DocumentReference> OriginatorDocumentReferences { get; set; } = new();
    public List<DocumentReference> ContractDocumentReferences { get; set; } = new();
    public List<DocumentReference> AdditionalDocumentReferences { get; set; } = new();
    public List<AllowanceCharge> AllowanceCharges { get; set; } = new();
    public List<TaxTotal> TaxTotals { get; set; } = new();
    public ExchangeRate? TaxExchangeRate { get; set; }
    public MonetaryTotal LegalMonetaryTotal { get; set; } = new();
    public Delivery? Delivery { get; set; }

    public decimal Total => Lines.Sum(l => l.LineTotal);
}
