using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoEFactura.Domain.Entities;

namespace RoEFactura.Domain.Configurations;

public class VatBreakdownConfiguration : IEntityTypeConfiguration<VatBreakdown>
{
    public void Configure(EntityTypeBuilder<VatBreakdown> builder)
    {
        builder.ToTable("VatBreakdowns");
        
        builder.HasKey(v => v.Id);
        
        builder.HasIndex(v => v.InvoiceId);
        
        // Configure decimal precision
        builder.Property(v => v.VatRate).HasPrecision(5, 2);
        builder.Property(v => v.TaxableAmount).HasPrecision(18, 2);
        builder.Property(v => v.TaxAmount).HasPrecision(18, 2);
        
        // Relationship with Invoice
        builder.HasOne(v => v.Invoice)
            .WithMany(i => i.VatBreakdowns)
            .HasForeignKey(v => v.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}