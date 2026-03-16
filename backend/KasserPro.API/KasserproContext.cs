using System;
using System.Collections.Generic;
using System.IO;
using KasserPro.API.TempModels;
using Microsoft.EntityFrameworkCore;

namespace KasserPro.API;

public partial class KasserproContext : DbContext
{
    public KasserproContext()
    {
    }

    public KasserproContext(DbContextOptions<KasserproContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchInventory> BranchInventories { get; set; }

    public virtual DbSet<BranchProductPrice> BranchProductPrices { get; set; }

    public virtual DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Expense> Expenses { get; set; }

    public virtual DbSet<ExpenseAttachment> ExpenseAttachments { get; set; }

    public virtual DbSet<ExpenseCategory> ExpenseCategories { get; set; }

    public virtual DbSet<InventoryTransfer> InventoryTransfers { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }

    public virtual DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }

    public virtual DbSet<PurchaseInvoicePayment> PurchaseInvoicePayments { get; set; }

    public virtual DbSet<RefundLog> RefundLogs { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<StockMovement> StockMovements { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupplierProduct> SupplierProducts { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
            var dbPath = Path.Combine(AppContext.BaseDirectory, "kasserpro.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_AuditLogs_BranchId");

            entity.HasIndex(e => new { e.EntityType, e.EntityId }, "IX_AuditLogs_EntityType_EntityId");

            entity.HasIndex(e => new { e.TenantId, e.CreatedAt }, "IX_AuditLogs_TenantId_CreatedAt");

            entity.HasIndex(e => e.UserId, "IX_AuditLogs_UserId");

            entity.HasOne(d => d.Branch).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }, "IX_Branches_TenantId_Code").IsUnique();

            entity.Property(e => e.CurrencyCode).HasDefaultValue("");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Branches)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BranchInventory>(entity =>
        {
            entity.HasIndex(e => new { e.BranchId, e.ProductId }, "IX_BranchInventories_BranchId_ProductId").IsUnique();

            entity.HasIndex(e => e.LastUpdatedAt, "IX_BranchInventories_LastUpdatedAt");

            entity.HasIndex(e => e.ProductId, "IX_BranchInventories_ProductId");

            entity.HasIndex(e => e.Quantity, "IX_BranchInventories_Quantity");

            entity.HasIndex(e => new { e.TenantId, e.BranchId }, "IX_BranchInventories_TenantId_BranchId");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchInventories).HasForeignKey(d => d.BranchId);

            entity.HasOne(d => d.Product).WithMany(p => p.BranchInventories).HasForeignKey(d => d.ProductId);

            entity.HasOne(d => d.Tenant).WithMany(p => p.BranchInventories).HasForeignKey(d => d.TenantId);
        });

        modelBuilder.Entity<BranchProductPrice>(entity =>
        {
            entity.HasIndex(e => new { e.BranchId, e.ProductId, e.IsActive }, "IX_BranchProductPrices_BranchId_ProductId_IsActive");

            entity.HasIndex(e => e.EffectiveFrom, "IX_BranchProductPrices_EffectiveFrom");

            entity.HasIndex(e => e.ProductId, "IX_BranchProductPrices_ProductId");

            entity.HasIndex(e => new { e.TenantId, e.BranchId }, "IX_BranchProductPrices_TenantId_BranchId");

            entity.Property(e => e.IsActive).HasDefaultValue(1);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchProductPrices).HasForeignKey(d => d.BranchId);

            entity.HasOne(d => d.Product).WithMany(p => p.BranchProductPrices).HasForeignKey(d => d.ProductId);

            entity.HasOne(d => d.Tenant).WithMany(p => p.BranchProductPrices).HasForeignKey(d => d.TenantId);
        });

        modelBuilder.Entity<CashRegisterTransaction>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_CashRegisterTransactions_BranchId");

            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId }, "IX_CashRegisterTransactions_ReferenceType_ReferenceId");

            entity.HasIndex(e => e.ShiftId, "IX_CashRegisterTransactions_ShiftId");

            entity.HasIndex(e => new { e.TenantId, e.BranchId }, "IX_CashRegisterTransactions_TenantId_BranchId");

            entity.HasIndex(e => e.TransactionDate, "IX_CashRegisterTransactions_TransactionDate");

            entity.HasIndex(e => e.TransactionNumber, "IX_CashRegisterTransactions_TransactionNumber").IsUnique();

            entity.HasIndex(e => e.TransferReferenceId, "IX_CashRegisterTransactions_TransferReferenceId");

            entity.HasIndex(e => e.Type, "IX_CashRegisterTransactions_Type");

            entity.HasIndex(e => e.UserId, "IX_CashRegisterTransactions_UserId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.CashRegisterTransactions)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Shift).WithMany(p => p.CashRegisterTransactions)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Tenant).WithMany(p => p.CashRegisterTransactions)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User).WithMany(p => p.CashRegisterTransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.TenantId, "IX_Categories_TenantId");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Categories)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Phone }, "IX_Customers_TenantId_Phone").IsUnique();

            entity.HasOne(d => d.Tenant).WithMany(p => p.Customers)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasIndex(e => e.ApprovedByUserId, "IX_Expenses_ApprovedByUserId");

            entity.HasIndex(e => e.BranchId, "IX_Expenses_BranchId");

            entity.HasIndex(e => e.CategoryId, "IX_Expenses_CategoryId");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Expenses_CreatedByUserId");

            entity.HasIndex(e => e.ExpenseDate, "IX_Expenses_ExpenseDate");

            entity.HasIndex(e => e.ExpenseNumber, "IX_Expenses_ExpenseNumber").IsUnique();

            entity.HasIndex(e => e.PaidByUserId, "IX_Expenses_PaidByUserId");

            entity.HasIndex(e => e.RejectedByUserId, "IX_Expenses_RejectedByUserId");

            entity.HasIndex(e => e.ShiftId, "IX_Expenses_ShiftId");

            entity.HasIndex(e => e.Status, "IX_Expenses_Status");

            entity.HasIndex(e => new { e.TenantId, e.BranchId }, "IX_Expenses_TenantId_BranchId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.ApprovedByUser).WithMany(p => p.ExpenseApprovedByUsers)
                .HasForeignKey(d => d.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Branch).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Category).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.ExpenseCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.PaidByUser).WithMany(p => p.ExpensePaidByUsers)
                .HasForeignKey(d => d.PaidByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.RejectedByUser).WithMany(p => p.ExpenseRejectedByUsers)
                .HasForeignKey(d => d.RejectedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Shift).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseAttachment>(entity =>
        {
            entity.HasIndex(e => e.ExpenseId, "IX_ExpenseAttachments_ExpenseId");

            entity.HasIndex(e => e.UploadedByUserId, "IX_ExpenseAttachments_UploadedByUserId");

            entity.HasOne(d => d.Expense).WithMany(p => p.ExpenseAttachments).HasForeignKey(d => d.ExpenseId);

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.ExpenseAttachments)
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasIndex(e => e.IsActive, "IX_ExpenseCategories_IsActive");

            entity.HasIndex(e => e.SortOrder, "IX_ExpenseCategories_SortOrder");

            entity.HasIndex(e => e.TenantId, "IX_ExpenseCategories_TenantId");

            entity.HasOne(d => d.Tenant).WithMany(p => p.ExpenseCategories)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryTransfer>(entity =>
        {
            entity.HasIndex(e => e.ApprovedByUserId, "IX_InventoryTransfers_ApprovedByUserId");

            entity.HasIndex(e => e.CreatedAt, "IX_InventoryTransfers_CreatedAt");

            entity.HasIndex(e => e.CreatedByUserId, "IX_InventoryTransfers_CreatedByUserId");

            entity.HasIndex(e => new { e.FromBranchId, e.Status }, "IX_InventoryTransfers_FromBranchId_Status");

            entity.HasIndex(e => e.ProductId, "IX_InventoryTransfers_ProductId");

            entity.HasIndex(e => e.ReceivedByUserId, "IX_InventoryTransfers_ReceivedByUserId");

            entity.HasIndex(e => new { e.TenantId, e.Status }, "IX_InventoryTransfers_TenantId_Status");

            entity.HasIndex(e => new { e.ToBranchId, e.Status }, "IX_InventoryTransfers_ToBranchId_Status");

            entity.HasIndex(e => e.TransferNumber, "IX_InventoryTransfers_TransferNumber").IsUnique();

            entity.HasOne(d => d.ApprovedByUser).WithMany(p => p.InventoryTransferApprovedByUsers)
                .HasForeignKey(d => d.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.InventoryTransferCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.FromBranch).WithMany(p => p.InventoryTransferFromBranches)
                .HasForeignKey(d => d.FromBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryTransfers)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReceivedByUser).WithMany(p => p.InventoryTransferReceivedByUsers)
                .HasForeignKey(d => d.ReceivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.InventoryTransfers).HasForeignKey(d => d.TenantId);

            entity.HasOne(d => d.ToBranch).WithMany(p => p.InventoryTransferToBranches)
                .HasForeignKey(d => d.ToBranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_Orders_BranchId");

            entity.HasIndex(e => e.CustomerId, "IX_Orders_CustomerId");

            entity.HasIndex(e => e.OrderNumber, "IX_Orders_OrderNumber").IsUnique();

            entity.HasIndex(e => e.ShiftId, "IX_Orders_ShiftId");

            entity.HasIndex(e => e.TenantId, "IX_Orders_TenantId");

            entity.HasIndex(e => e.UserId, "IX_Orders_UserId");

            entity.HasOne(d => d.Branch).WithMany(p => p.Orders)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Shift).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(e => e.OrderId, "IX_OrderItems_OrderId");

            entity.HasIndex(e => e.ProductId, "IX_OrderItems_ProductId");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems).HasForeignKey(d => d.OrderId);

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems).HasForeignKey(d => d.ProductId);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_Payments_BranchId");

            entity.HasIndex(e => e.OrderId, "IX_Payments_OrderId");

            entity.HasIndex(e => e.TenantId, "IX_Payments_TenantId");

            entity.HasOne(d => d.Branch).WithMany(p => p.Payments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Order).WithMany(p => p.Payments).HasForeignKey(d => d.OrderId);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Payments)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.CategoryId, "IX_Products_CategoryId");

            entity.HasIndex(e => new { e.TenantId, e.Barcode }, "IX_Products_TenantId_Barcode");

            entity.HasIndex(e => new { e.TenantId, e.Sku }, "IX_Products_TenantId_Sku");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Products)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_PurchaseInvoices_BranchId");

            entity.HasIndex(e => e.ConfirmedByUserId, "IX_PurchaseInvoices_ConfirmedByUserId");

            entity.HasIndex(e => e.CreatedByUserId, "IX_PurchaseInvoices_CreatedByUserId");

            entity.HasIndex(e => e.InvoiceDate, "IX_PurchaseInvoices_InvoiceDate");

            entity.HasIndex(e => e.InvoiceNumber, "IX_PurchaseInvoices_InvoiceNumber").IsUnique();

            entity.HasIndex(e => e.SupplierId, "IX_PurchaseInvoices_SupplierId");

            entity.HasIndex(e => new { e.TenantId, e.BranchId, e.Status }, "IX_PurchaseInvoices_TenantId_BranchId_Status");

            entity.HasIndex(e => new { e.TenantId, e.SupplierId }, "IX_PurchaseInvoices_TenantId_SupplierId");

            entity.HasOne(d => d.Branch).WithMany(p => p.PurchaseInvoices).HasForeignKey(d => d.BranchId);

            entity.HasOne(d => d.ConfirmedByUser).WithMany(p => p.PurchaseInvoiceConfirmedByUsers)
                .HasForeignKey(d => d.ConfirmedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.PurchaseInvoiceCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseInvoices)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.PurchaseInvoices).HasForeignKey(d => d.TenantId);
        });

        modelBuilder.Entity<PurchaseInvoiceItem>(entity =>
        {
            entity.HasIndex(e => e.ProductId, "IX_PurchaseInvoiceItems_ProductId");

            entity.HasIndex(e => e.PurchaseInvoiceId, "IX_PurchaseInvoiceItems_PurchaseInvoiceId");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseInvoiceItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.PurchaseInvoice).WithMany(p => p.PurchaseInvoiceItems).HasForeignKey(d => d.PurchaseInvoiceId);
        });

        modelBuilder.Entity<PurchaseInvoicePayment>(entity =>
        {
            entity.HasIndex(e => e.CreatedByUserId, "IX_PurchaseInvoicePayments_CreatedByUserId");

            entity.HasIndex(e => e.PaymentDate, "IX_PurchaseInvoicePayments_PaymentDate");

            entity.HasIndex(e => e.PurchaseInvoiceId, "IX_PurchaseInvoicePayments_PurchaseInvoiceId");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.PurchaseInvoicePayments)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.PurchaseInvoice).WithMany(p => p.PurchaseInvoicePayments).HasForeignKey(d => d.PurchaseInvoiceId);
        });

        modelBuilder.Entity<RefundLog>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_RefundLogs_BranchId");

            entity.HasIndex(e => e.OrderId, "IX_RefundLogs_OrderId").IsUnique();

            entity.HasIndex(e => e.TenantId, "IX_RefundLogs_TenantId");

            entity.HasIndex(e => e.UserId, "IX_RefundLogs_UserId");

            entity.HasOne(d => d.Branch).WithMany(p => p.RefundLogs)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Order).WithOne(p => p.RefundLog)
                .HasForeignKey<RefundLog>(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.RefundLogs)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User).WithMany(p => p.RefundLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_Shifts_BranchId");

            entity.HasIndex(e => e.ForceClosedByUserId, "IX_Shifts_ForceClosedByUserId");

            entity.HasIndex(e => e.HandedOverFromUserId, "IX_Shifts_HandedOverFromUserId");

            entity.HasIndex(e => e.HandedOverToUserId, "IX_Shifts_HandedOverToUserId");

            entity.HasIndex(e => e.IsClosed, "IX_Shifts_IsClosed");

            entity.HasIndex(e => e.OpenedAt, "IX_Shifts_OpenedAt");

            entity.HasIndex(e => e.ReconciledByUserId, "IX_Shifts_ReconciledByUserId");

            entity.HasIndex(e => new { e.TenantId, e.BranchId }, "IX_Shifts_TenantId_BranchId");

            entity.HasIndex(e => e.UserId, "IX_Shifts_UserId");

            entity.HasIndex(e => e.UserId1, "IX_Shifts_UserId1");

            entity.Property(e => e.ClosingBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Difference).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ExpectedBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.HandoverBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RowVersion).HasDefaultValueSql("X''");
            entity.Property(e => e.TotalCard).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalCash).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ForceClosedByUser).WithMany(p => p.ShiftForceClosedByUsers)
                .HasForeignKey(d => d.ForceClosedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.HandedOverFromUser).WithMany(p => p.ShiftHandedOverFromUsers)
                .HasForeignKey(d => d.HandedOverFromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.HandedOverToUser).WithMany(p => p.ShiftHandedOverToUsers)
                .HasForeignKey(d => d.HandedOverToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReconciledByUser).WithMany(p => p.ShiftReconciledByUsers)
                .HasForeignKey(d => d.ReconciledByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User).WithMany(p => p.ShiftUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.UserId1Navigation).WithMany(p => p.ShiftUserId1Navigations).HasForeignKey(d => d.UserId1);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_StockMovements_BranchId");

            entity.HasIndex(e => new { e.ProductId, e.CreatedAt }, "IX_StockMovements_ProductId_CreatedAt");

            entity.HasIndex(e => e.TenantId, "IX_StockMovements_TenantId");

            entity.HasIndex(e => e.UserId, "IX_StockMovements_UserId");

            entity.HasOne(d => d.Branch).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Product).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_Suppliers_BranchId");

            entity.HasIndex(e => new { e.TenantId, e.Name }, "IX_Suppliers_TenantId_Name");

            entity.HasOne(d => d.Branch).WithMany(p => p.Suppliers)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Suppliers)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplierProduct>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.IsPreferred }, "IX_SupplierProducts_ProductId_IsPreferred");

            entity.HasIndex(e => new { e.SupplierId, e.ProductId }, "IX_SupplierProducts_SupplierId_ProductId").IsUnique();

            entity.HasOne(d => d.Product).WithMany(p => p.SupplierProducts).HasForeignKey(d => d.ProductId);

            entity.HasOne(d => d.Supplier).WithMany(p => p.SupplierProducts).HasForeignKey(d => d.SupplierId);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(e => e.Slug, "IX_Tenants_Slug").IsUnique();

            entity.Property(e => e.Currency).HasDefaultValue("EGP");
            entity.Property(e => e.IsTaxEnabled).HasDefaultValue(1);
            entity.Property(e => e.ReceiptBodyFontSize).HasDefaultValue(9);
            entity.Property(e => e.ReceiptHeaderFontSize).HasDefaultValue(12);
            entity.Property(e => e.ReceiptPaperSize).HasDefaultValue("80mm");
            entity.Property(e => e.ReceiptShowBranchName).HasDefaultValue(1);
            entity.Property(e => e.ReceiptShowCashier).HasDefaultValue(1);
            entity.Property(e => e.ReceiptShowCustomerName).HasDefaultValue(1);
            entity.Property(e => e.ReceiptShowLogo).HasDefaultValue(1);
            entity.Property(e => e.ReceiptShowThankYou).HasDefaultValue(1);
            entity.Property(e => e.ReceiptTotalFontSize).HasDefaultValue(11);
            entity.Property(e => e.TaxRate).HasDefaultValue(14.0m);
            entity.Property(e => e.Timezone).HasDefaultValue("Africa/Cairo");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.BranchId, "IX_Users_BranchId");

            entity.HasIndex(e => e.Email, "IX_Users_Email").IsUnique();

            entity.HasIndex(e => e.TenantId, "IX_Users_TenantId");

            entity.Property(e => e.SecurityStamp).HasDefaultValue("");

            entity.HasOne(d => d.Branch).WithMany(p => p.Users)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Users)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
