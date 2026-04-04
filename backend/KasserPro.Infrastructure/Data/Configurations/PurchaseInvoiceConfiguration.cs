namespace KasserPro.Infrastructure.Data.Configurations;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        // Indexes
        builder.HasIndex(e => new { e.TenantId, e.InvoiceNumber }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.BranchId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.SupplierId });
        builder.HasIndex(e => e.InvoiceDate);

        // Relationships
        builder.HasOne(e => e.Supplier)
            .WithMany(s => s.PurchaseInvoices)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ConfirmedByUser)
            .WithMany()
            .HasForeignKey(e => e.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Decimal precision
        builder.Property(e => e.Subtotal).HasPrecision(18, 2);
        builder.Property(e => e.TaxRate).HasPrecision(5, 2);
        builder.Property(e => e.TaxAmount).HasPrecision(18, 2);
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.AmountPaid).HasPrecision(18, 2);
        builder.Property(e => e.AmountDue).HasPrecision(18, 2);
    }
}
