using System.ComponentModel.DataAnnotations;
using RoEFactura.Domain.ValueObjects;

namespace RoEFactura.Domain.Entities;

public class Invoice : Entity
{
    // Core fields from UBL
    [Required]
    [MaxLength(30)]
    public string Number { get; set; } = default!;
    
    [Required]
    public DateOnly IssueDate { get; set; }
    
    [Required]
    [MaxLength(3)]
    public string TypeCode { get; set; } = default!; // 380, 381, 384, 389, 751
    
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = default!;
    
    [MaxLength(3)]
    public string? VatCurrencyCode { get; set; }
    
    public DateOnly? DueDate { get; set; }
    
    [MaxLength(300)]
    public string? PaymentTermsText { get; set; }
    
    // Parties
    public Guid SellerId { get; set; }
    public virtual Party Seller { get; set; } = default!;
    
    public Guid BuyerId { get; set; }
    public virtual Party Buyer { get; set; } = default!;
    
    public Guid? PayeeId { get; set; }
    public virtual Party? Payee { get; set; }
    
    // Delivery info
    public DateOnly? ActualDeliveryDate { get; set; }
    
    [MaxLength(200)]
    public string? DeliveryNoteReference { get; set; }
    
    public DateOnly? InvoicingPeriodStart { get; set; }
    public DateOnly? InvoicingPeriodEnd { get; set; }
    
    // Collections
    public virtual ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public virtual ICollection<VatBreakdown> VatBreakdowns { get; set; } = new List<VatBreakdown>();
    
    // Computed/stored totals
    public decimal SumOfLineNet { get; set; }
    public decimal? SumOfAllowances { get; set; }
    public decimal? SumOfCharges { get; set; }
    public decimal TotalWithoutVat { get; set; }
    public decimal? TotalVat { get; set; }
    public decimal TotalWithVat { get; set; }
    public decimal AmountDue { get; set; }
    
    // Store original UBL XML for full fidelity
    public string? UblXml { get; set; }
    
    // Metadata
    [MaxLength(200)]
    public string? CustomizationId { get; set; }
    
    [MaxLength(100)]
    public string? AnafDownloadId { get; set; }
}