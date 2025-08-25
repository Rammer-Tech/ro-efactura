using System.ComponentModel.DataAnnotations;

namespace RoEFactura.Domain.Entities;

public class LineAllowanceCharge : Entity
{
    public Guid InvoiceLineId { get; set; }
    public virtual InvoiceLine InvoiceLine { get; set; } = default!;
    
    public bool IsCharge { get; set; } // true = Charge, false = Allowance
    
    public decimal Amount { get; set; }
    
    public decimal? BaseAmount { get; set; }
    
    public decimal? Percentage { get; set; }
    
    [MaxLength(4)]
    public string? ReasonCode { get; set; }
    
    [MaxLength(100)]
    public string? ReasonText { get; set; }
}