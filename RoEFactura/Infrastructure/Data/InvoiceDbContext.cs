using Microsoft.EntityFrameworkCore;
using RoEFactura.Domain.Configurations;
using RoEFactura.Domain.Entities;

namespace RoEFactura.Infrastructure.Data;

public class InvoiceDbContext : DbContext
{
    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : base(options)
    {
    }

    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Party> Parties { get; set; }
    public DbSet<InvoiceLine> InvoiceLines { get; set; }
    public DbSet<VatBreakdown> VatBreakdowns { get; set; }
    public DbSet<LineAllowanceCharge> LineAllowanceCharges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new PartyConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceLineConfiguration());
        modelBuilder.ApplyConfiguration(new VatBreakdownConfiguration());
        modelBuilder.ApplyConfiguration(new LineAllowanceChargeConfiguration());

        // Global configurations
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Configure DateTime properties to be stored as UTC
            var dateTimeProperties = entityType.ClrType.GetProperties()
                .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));

            foreach (var property in dateTimeProperties)
            {
                modelBuilder.Entity(entityType.Name).Property(property.Name)
                    .HasConversion(
                        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
                        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
            }
        }
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entities = ChangeTracker.Entries()
            .Where(x => x.Entity is Entity && (x.State == EntityState.Added || x.State == EntityState.Modified));

        foreach (var entity in entities)
        {
            var now = DateTime.UtcNow;
            
            if (entity.State == EntityState.Added)
            {
                ((Entity)entity.Entity).CreatedAt = now;
            }
            
            ((Entity)entity.Entity).UpdatedAt = now;
        }
    }
}