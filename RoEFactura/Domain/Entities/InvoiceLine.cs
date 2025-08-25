using System.ComponentModel.DataAnnotations;
using RoEFactura.Domain.ValueObjects;

namespace RoEFactura.Domain.Entities;

public class InvoiceLine : Entity
{
    public Guid InvoiceId { get; set; }
    public virtual Invoice Invoice { get; set; } = default!;
    
    [Required]
    [MaxLength(60)]
    public string LineId { get; set; } = default!;
    
    [MaxLength(300)]
    public string? Note { get; set; }
    
    public decimal Quantity { get; set; }
    
    [Required]
    [MaxLength(8)]
    public string UnitCode { get; set; } = default!;
    
    public decimal LineExtensionAmount { get; set; }
    
    // Period (optional)
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    
    // Price details (owned)
    public Price Price { get; set; } = new();
    
    // VAT info
    [Required]
    [MaxLength(3)]
    public string VatCategory { get; set; } = default!; // S, Z, E, AE, K, G, O
    
    public decimal? VatRate { get; set; }
    
    // Item information
    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = default!;
    
    [MaxLength(200)]
    public string? ItemDescription { get; set; }
    
    [MaxLength(2)]
    public string? OriginCountryCode { get; set; }
    
    // Allowances and charges
    public virtual ICollection<LineAllowanceCharge> AllowanceCharges { get; set; } = new List<LineAllowanceCharge>();
}