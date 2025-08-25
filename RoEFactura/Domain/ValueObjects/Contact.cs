using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RoEFactura.Domain.ValueObjects;

[Owned]
public class Contact
{
    [MaxLength(100)]
    public string? Name { get; set; }
    
    [MaxLength(100)]
    public string? Telephone { get; set; }
    
    [MaxLength(100)]
    [EmailAddress]
    public string? Email { get; set; }
}