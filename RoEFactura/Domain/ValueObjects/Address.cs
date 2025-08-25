using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RoEFactura.Domain.ValueObjects;

[Owned]
public class Address
{
    [MaxLength(150)]
    public string? Line1 { get; set; }
    
    [MaxLength(100)]
    public string? Line2 { get; set; }
    
    [MaxLength(50)]
    public string? City { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
    [MaxLength(50)]
    public string? CountrySubdivision { get; set; } // For RO: ISO 3166-2:RO codes
    
    [Required]
    [MaxLength(2)]
    public string CountryCode { get; set; } = "RO";
    
    public bool IsRomanian => CountryCode == "RO";
    
    public bool IsBucharest => CountrySubdivision == "B";
}