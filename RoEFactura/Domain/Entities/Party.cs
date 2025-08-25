using System.ComponentModel.DataAnnotations;
using RoEFactura.Domain.ValueObjects;

namespace RoEFactura.Domain.Entities;

public class Party : Entity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = default!;
    
    [MaxLength(1000)]
    public string? AdditionalLegalInfo { get; set; }
    
    [MaxLength(50)]
    public string? LegalRegistrationId { get; set; } // CUI/CIF for Romanian parties
    
    [MaxLength(20)]
    public string? VatIdentifier { get; set; }
    
    // Owned value objects
    public Address Address { get; set; } = new();
    public Contact? Contact { get; set; }
    
    // Navigation properties
    public virtual ICollection<Invoice> InvoicesAsSeller { get; set; } = new List<Invoice>();
    public virtual ICollection<Invoice> InvoicesAsBuyer { get; set; } = new List<Invoice>();
    public virtual ICollection<Invoice> InvoicesAsPayee { get; set; } = new List<Invoice>();
}