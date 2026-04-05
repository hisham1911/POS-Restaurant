namespace KasserPro.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KasserPro.Domain.Entities;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.NameEn)
            .HasMaxLength(200);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.LogoUrl)
            .HasMaxLength(500);

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("EGP");

        builder.Property(t => t.Timezone)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Africa/Cairo");

        // Tax Settings
        builder.Property(t => t.TaxRate)
            .HasPrecision(5, 2)
            .HasDefaultValue(14.0m);

        builder.Property(t => t.IsTaxEnabled)
            .HasDefaultValue(true);

        // Receipt Settings
        builder.Property(t => t.ReceiptPaperSize)
            .HasMaxLength(10)
            .HasDefaultValue("80mm");

        builder.Property(t => t.ReceiptCustomWidth)
            .IsRequired(false);

        builder.Property(t => t.ReceiptHeaderFontSize)
            .HasDefaultValue(12);

        builder.Property(t => t.ReceiptBodyFontSize)
            .HasDefaultValue(9);

        builder.Property(t => t.ReceiptTotalFontSize)
            .HasDefaultValue(11);

        builder.Property(t => t.ReceiptShowBranchName)
            .HasDefaultValue(true);

        builder.Property(t => t.ReceiptShowCashier)
            .HasDefaultValue(true);

        builder.Property(t => t.ReceiptShowThankYou)
            .HasDefaultValue(true);

        builder.Property(t => t.ReceiptFooterMessage)
            .HasMaxLength(500);

        builder.Property(t => t.ReceiptPhoneNumber)
            .HasMaxLength(20);

        builder.Property(t => t.ReceiptShowCustomerName)
            .HasDefaultValue(true);

        builder.Property(t => t.ReceiptShowLogo)
            .HasDefaultValue(true);

        // Print Routing Settings
        builder.Property(t => t.PrintRoutingMode)
            .HasMaxLength(30)
            .HasDefaultValue("BranchWithFallback");

        builder.Property(t => t.AutoPrintOnSale)
            .HasDefaultValue(true);

        builder.Property(t => t.AutoPrintOnDebtPayment)
            .HasDefaultValue(true);

        builder.Property(t => t.AutoPrintDailyReports)
            .HasDefaultValue(false);
    }
}
