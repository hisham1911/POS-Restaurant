namespace KasserPro.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KasserPro.Domain.Entities;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);

        // Composite index for payment method breakdown in reports
        builder.HasIndex(p => new { p.OrderId, p.Method })
            .HasDatabaseName("IX_Payments_OrderId_Method");
    }
}
