namespace KasserPro.Infrastructure.Data.Configurations;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(t => t.BalanceBefore)
            .HasColumnType("decimal(18,2)");

        builder.Property(t => t.BalanceAfter)
            .HasColumnType("decimal(18,2)");

        builder.Property(t => t.ReferenceType)
            .HasMaxLength(50);

        builder.Property(t => t.ReferenceNumber)
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.UserName)
            .HasMaxLength(100);

        builder.HasIndex(t => new { t.WalletId, t.CreatedAt });
        builder.HasIndex(t => new { t.TenantId, t.BranchId });
        builder.HasIndex(t => t.ReferenceType);
        builder.HasIndex(t => t.ReferenceId);

        builder.HasOne(t => t.Wallet)
            .WithMany(w => w.Transactions)
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
