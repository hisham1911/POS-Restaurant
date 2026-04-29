namespace KasserPro.Infrastructure.Data.Configurations;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PurchaseInvoiceItemConfiguration : IEntityTypeConfiguration<PurchaseInvoiceItem>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceItem> builder)
    {
        // Indexes
        builder.HasIndex(e => e.PurchaseInvoiceId);
        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.BatchId);

        // Relationships
        builder.HasOne(e => e.Product)
            .WithMany(p => p.PurchaseInvoiceItems)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Batch)
            .WithMany()
            .HasForeignKey(e => e.BatchId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Decimal precision
        builder.Property(e => e.PurchasePrice).HasPrecision(18, 2);
        builder.Property(e => e.Total).HasPrecision(18, 2);
    }
}
