using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoEFactura.Domain.Entities;

namespace RoEFactura.Domain.Configurations;

public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("Parties");
        
        builder.HasKey(p => p.Id);
        
        builder.HasIndex(p => p.VatIdentifier);
        builder.HasIndex(p => p.LegalRegistrationId);
        
        // Configure owned value objects
        builder.OwnsOne(p => p.Address, address =>
        {
            address.Property(a => a.Line1).HasMaxLength(150);
            address.Property(a => a.Line2).HasMaxLength(100);
            address.Property(a => a.City).HasMaxLength(50);
            address.Property(a => a.PostalCode).HasMaxLength(20);
            address.Property(a => a.CountrySubdivision).HasMaxLength(50);
            address.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
        });
        
        builder.OwnsOne(p => p.Contact, contact =>
        {
            contact.Property(c => c.Name).HasMaxLength(100);
            contact.Property(c => c.Telephone).HasMaxLength(100);
            contact.Property(c => c.Email).HasMaxLength(100);
        });
    }
}