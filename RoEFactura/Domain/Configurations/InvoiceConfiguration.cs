using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoEFactura.Domain.Entities;

namespace RoEFactura.Domain.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        
        builder.HasKey(i => i.Id);
        
        builder.HasIndex(i => i.Number).IsUnique();
        builder.HasIndex(i => i.IssueDate);
        builder.HasIndex(i => i.AnafDownloadId);
        
        // Configure decimal precision
        builder.Property(p => p.SumOfLineNet).HasPrecision(18, 2);
        builder.Property(p => p.SumOfAllowances).HasPrecision(18, 2);
        builder.Property(p => p.SumOfCharges).HasPrecision(18, 2);
        builder.Property(p => p.TotalWithoutVat).HasPrecision(18, 2);
        builder.Property(p => p.TotalVat).HasPrecision(18, 2);
        builder.Property(p => p.TotalWithVat).HasPrecision(18, 2);
        builder.Property(p => p.AmountDue).HasPrecision(18, 2);
        
        // Relationships
        builder.HasOne(i => i.Seller)
            .WithMany(p => p.InvoicesAsSeller)
            .HasForeignKey(i => i.SellerId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(i => i.Buyer)
            .WithMany(p => p.InvoicesAsBuyer)
            .HasForeignKey(i => i.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(i => i.Payee)
            .WithMany(p => p.InvoicesAsPayee)
            .HasForeignKey(i => i.PayeeId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Store UBL XML as NVARCHAR(MAX)
        builder.Property(i => i.UblXml).HasColumnType("nvarchar(max)");
    }
}