using System.ComponentModel.DataAnnotations;

namespace RoEFactura.Domain.Entities;

public class VatBreakdown : Entity
{
    public Guid InvoiceId { get; set; }
    public virtual Invoice Invoice { get; set; } = default!;
    
    [Required]
    [MaxLength(3)]
    public string VatCategory { get; set; } = default!; // S, Z, E, AE, K, G, O
    
    public decimal? VatRate { get; set; }
    
    public decimal TaxableAmount { get; set; }
    
    public decimal TaxAmount { get; set; }
    
    [MaxLength(100)]
    public string? ExemptionReason { get; set; }
}