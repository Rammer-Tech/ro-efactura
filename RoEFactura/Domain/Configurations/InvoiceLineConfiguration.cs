using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoEFactura.Domain.Entities;

namespace RoEFactura.Domain.Configurations;

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        
        builder.HasKey(l => l.Id);
        
        builder.HasIndex(l => new { l.InvoiceId, l.LineId }).IsUnique();
        
        // Configure decimal precision
        builder.Property(l => l.Quantity).HasPrecision(18, 6);
        builder.Property(l => l.LineExtensionAmount).HasPrecision(18, 2);
        builder.Property(l => l.VatRate).HasPrecision(5, 2);
        
        // Configure owned Price value object
        builder.OwnsOne(l => l.Price, price =>
        {
            price.Property(p => p.NetUnitPrice).HasPrecision(18, 6);
            price.Property(p => p.PriceDiscount).HasPrecision(18, 2);
            price.Property(p => p.GrossUnitPrice).HasPrecision(18, 6);
            price.Property(p => p.BaseQuantity).HasPrecision(18, 6);
            price.Property(p => p.BaseUom).HasMaxLength(8);
        });
        
        // Relationship with Invoice
        builder.HasOne(l => l.Invoice)
            .WithMany(i => i.Lines)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}