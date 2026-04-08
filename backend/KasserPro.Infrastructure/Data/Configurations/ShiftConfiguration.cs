namespace KasserPro.Infrastructure.Data.Configurations;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");

        // Primary key
        builder.HasKey(s => s.Id);

        // Indexes
        builder.HasIndex(s => new { s.TenantId, s.BranchId });
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.IsClosed);
        builder.HasIndex(s => s.OpenedAt);

        // Properties
        builder.Property(s => s.OpeningBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.ClosingBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.ExpectedBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.Difference)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.TotalCash)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.TotalCard)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.HandoverBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        // String length constraints
        builder.Property(s => s.ForceCloseReason)
            .HasMaxLength(500);

        builder.Property(s => s.HandoverNotes)
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Branch)
            .WithMany(b => b.Shifts)
            .HasForeignKey(s => s.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.User)
            .WithMany(u => u.Shifts)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Shifts_Users_UserId");

        builder.HasOne(s => s.ReconciledByUser)
            .WithMany()
            .HasForeignKey(s => s.ReconciledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Shifts_Users_ReconciledByUserId");

        builder.HasOne(s => s.ForceClosedByUser)
            .WithMany()
            .HasForeignKey(s => s.ForceClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Shifts_Users_ForceClosedByUserId");

        builder.HasOne(s => s.HandedOverFromUser)
            .WithMany()
            .HasForeignKey(s => s.HandedOverFromUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Shifts_Users_HandedOverFromUserId");

        builder.HasOne(s => s.HandedOverToUser)
            .WithMany()
            .HasForeignKey(s => s.HandedOverToUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Shifts_Users_HandedOverToUserId");

        builder.HasMany(s => s.Orders)
            .WithOne(o => o.Shift)
            .HasForeignKey(o => o.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.Expenses)
            .WithOne(e => e.Shift)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.CashRegisterTransactions)
            .WithOne(t => t.Shift)
            .HasForeignKey(t => t.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
