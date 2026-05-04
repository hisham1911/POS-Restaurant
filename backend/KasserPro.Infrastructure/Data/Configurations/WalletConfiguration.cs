namespace KasserPro.Infrastructure.Data.Configurations;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.AccountNumber)
            .HasMaxLength(100);

        builder.Property(w => w.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(w => w.CurrentBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(w => w.Notes)
            .HasMaxLength(500);

        builder.HasIndex(w => new { w.TenantId, w.BranchId, w.IsActive });

        builder.HasOne(w => w.Tenant)
            .WithMany()
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Branch)
            .WithMany()
            .HasForeignKey(w => w.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
