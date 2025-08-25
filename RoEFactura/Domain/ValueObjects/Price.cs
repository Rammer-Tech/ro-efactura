using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RoEFactura.Domain.ValueObjects;

[Owned]
public class Price
{
    public decimal NetUnitPrice { get; set; }
    
    public decimal? PriceDiscount { get; set; }
    
    public decimal? GrossUnitPrice { get; set; }
    
    public decimal? BaseQuantity { get; set; }
    
    [MaxLength(8)]
    public string? BaseUom { get; set; }
}