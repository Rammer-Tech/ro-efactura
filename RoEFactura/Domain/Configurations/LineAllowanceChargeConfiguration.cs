using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoEFactura.Domain.Entities;

namespace RoEFactura.Domain.Configurations;

public class LineAllowanceChargeConfiguration : IEntityTypeConfiguration<LineAllowanceCharge>
{
    public void Configure(EntityTypeBuilder<LineAllowanceCharge> builder)
    {
        builder.ToTable("LineAllowanceCharges");
        
        builder.HasKey(l => l.Id);
        
        builder.HasIndex(l => l.InvoiceLineId);
        
        // Configure decimal precision
        builder.Property(l => l.Amount).HasPrecision(18, 2);
        builder.Property(l => l.BaseAmount).HasPrecision(18, 2);
        builder.Property(l => l.Percentage).HasPrecision(5, 2);
        
        // Relationship with InvoiceLine
        builder.HasOne(l => l.InvoiceLine)
            .WithMany(il => il.AllowanceCharges)
            .HasForeignKey(l => l.InvoiceLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}