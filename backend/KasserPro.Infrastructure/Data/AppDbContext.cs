namespace KasserPro.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using KasserPro.Domain.Entities;

public class AppDbContext : DbContext
{
    private readonly int _currentTenantId;
    private readonly int? _currentBranchId;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _currentTenantId = 1; // Default tenant - will be set via middleware later
        _currentBranchId = 1; // Default branch
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Sellable V1: New entities
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<RefundLog> RefundLogs => Set<RefundLog>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    // Purchase Invoice entities
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<PurchaseInvoicePayment> PurchaseInvoicePayments => Set<PurchaseInvoicePayment>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();

    // Expense and Cash Register entities
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<CashRegisterTransaction> CashRegisterTransactions => Set<CashRegisterTransaction>();

    // Branch Inventory entities
    public DbSet<BranchInventory> BranchInventories => Set<BranchInventory>();
    public DbSet<BranchProductPrice> BranchProductPrices => Set<BranchProductPrice>();
    public DbSet<InventoryTransfer> InventoryTransfers => Set<InventoryTransfer>();

    // User Permissions
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Soft delete filters - must be consistent across related entities
        modelBuilder.Entity<Tenant>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Branch>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Shift>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => !e.IsDeleted);

        // Sellable V1: Soft delete filters for new entities
        modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DebtPayment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RefundLog>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => !e.IsDeleted);

        // Purchase Invoice: Soft delete filters
        modelBuilder.Entity<PurchaseInvoice>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PurchaseInvoiceItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PurchaseInvoicePayment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SupplierProduct>().HasQueryFilter(e => !e.IsDeleted);

        // Expense and Cash Register: Soft delete filters
        modelBuilder.Entity<ExpenseCategory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ExpenseAttachment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CashRegisterTransaction>().HasQueryFilter(e => !e.IsDeleted);

        // Branch Inventory: Soft delete filters
        modelBuilder.Entity<BranchInventory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BranchProductPrice>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<InventoryTransfer>().HasQueryFilter(e => !e.IsDeleted);

        // User Permissions: Soft delete filter
        modelBuilder.Entity<UserPermission>().HasQueryFilter(e => !e.IsDeleted);

        // Tenant relationships
        modelBuilder.Entity<Branch>()
            .HasOne(b => b.Tenant)
            .WithMany(t => t.Branches)
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Branch)
            .WithMany(b => b.Users)
            .HasForeignKey(u => u.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Category>()
            .HasOne(c => c.Tenant)
            .WithMany(t => t.Categories)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Tenant)
            .WithMany(t => t.Products)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Tenant)
            .WithMany()
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Branch)
            .WithMany(b => b.Orders)
            .HasForeignKey(o => o.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Return Order → Original Order self-referencing FK
        modelBuilder.Entity<Order>()
            .HasOne(o => o.OriginalOrder)
            .WithMany()
            .HasForeignKey(o => o.OriginalOrderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Shift configuration is in ShiftConfiguration.cs

        // ═══════════════════════════════════════════════════════════════════════
        // CONCURRENCY TOKENS: SQLite-compatible implementation
        // ═══════════════════════════════════════════════════════════════════════
        // SQLite does NOT support SQL Server's ROWVERSION (byte[] auto-increment).
        // We configure RowVersion as a concurrency token that is NOT database-generated.
        // The application must manually update it in SaveChangesAsync.
        
        modelBuilder.Entity<Shift>()
            .Property(s => s.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // NOT database-generated

        modelBuilder.Entity<Order>()
            .Property(o => o.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // NOT database-generated

        modelBuilder.Entity<Customer>()
            .Property(c => c.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // NOT database-generated

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Branch)
            .WithMany()
            .HasForeignKey(p => p.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.Tenant)
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.Branch)
            .WithMany()
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        modelBuilder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        modelBuilder.Entity<Branch>().HasIndex(b => new { b.TenantId, b.Code }).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.CreatedAt });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.EntityType, a.EntityId });

        // ═══════════════════════════════════════════════════════════════════════
        // SELLABLE V1: Customer, StockMovement, RefundLog configurations
        // ═══════════════════════════════════════════════════════════════════════

        // Customer relationships
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Customer indexes (phone lookup is primary use case)
        modelBuilder.Entity<Customer>()
            .HasIndex(c => new { c.TenantId, c.Phone })
            .IsUnique();

        // DebtPayment relationships
        modelBuilder.Entity<DebtPayment>()
            .HasOne(dp => dp.Tenant)
            .WithMany()
            .HasForeignKey(dp => dp.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DebtPayment>()
            .HasOne(dp => dp.Branch)
            .WithMany()
            .HasForeignKey(dp => dp.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DebtPayment>()
            .HasOne(dp => dp.Customer)
            .WithMany()
            .HasForeignKey(dp => dp.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DebtPayment>()
            .HasOne(dp => dp.RecordedByUser)
            .WithMany()
            .HasForeignKey(dp => dp.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DebtPayment>()
            .HasOne(dp => dp.Shift)
            .WithMany()
            .HasForeignKey(dp => dp.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        // DebtPayment indexes (customer history queries)
        modelBuilder.Entity<DebtPayment>()
            .HasIndex(dp => new { dp.CustomerId, dp.CreatedAt });

        modelBuilder.Entity<DebtPayment>()
            .HasIndex(dp => new { dp.TenantId, dp.CreatedAt });

        // StockMovement relationships
        modelBuilder.Entity<StockMovement>()
            .HasOne(sm => sm.Tenant)
            .WithMany()
            .HasForeignKey(sm => sm.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
            .HasOne(sm => sm.Branch)
            .WithMany()
            .HasForeignKey(sm => sm.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
            .HasOne(sm => sm.Product)
            .WithMany(p => p.StockMovements)
            .HasForeignKey(sm => sm.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
            .HasOne(sm => sm.User)
            .WithMany()
            .HasForeignKey(sm => sm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // StockMovement indexes (history queries)
        modelBuilder.Entity<StockMovement>()
            .HasIndex(sm => new { sm.ProductId, sm.CreatedAt });

        // RefundLog relationships
        modelBuilder.Entity<RefundLog>()
            .HasOne(rl => rl.Tenant)
            .WithMany()
            .HasForeignKey(rl => rl.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefundLog>()
            .HasOne(rl => rl.Branch)
            .WithMany()
            .HasForeignKey(rl => rl.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefundLog>()
            .HasOne(rl => rl.Order)
            .WithOne(o => o.RefundLog)
            .HasForeignKey<RefundLog>(rl => rl.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefundLog>()
            .HasOne(rl => rl.User)
            .WithMany()
            .HasForeignKey(rl => rl.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // RefundLog indexes
        modelBuilder.Entity<RefundLog>()
            .HasIndex(rl => rl.OrderId)
            .IsUnique();

        // Product indexes for barcode/SKU lookup (POS scanning)
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.TenantId, p.Barcode });

        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.TenantId, p.Sku });

        // Supplier relationships
        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Branch)
            .WithMany()
            .HasForeignKey(s => s.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supplier indexes
        modelBuilder.Entity<Supplier>()
            .HasIndex(s => new { s.TenantId, s.Name });

        // ═══════════════════════════════════════════════════════════════════════
        // USER PERMISSIONS: Configuration
        // ═══════════════════════════════════════════════════════════════════════

        // UserPermission relationships and constraints
        modelBuilder.Entity<UserPermission>(entity =>
        {
            // Unique index to prevent duplicate permissions for same user
            entity.HasIndex(e => new { e.UserId, e.Permission })
                  .IsUnique();

            // Relationship with User (cascade delete - if user deleted, permissions deleted)
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Permissions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Store Permission enum as integer in database
            entity.Property(e => e.Permission)
                  .HasConversion<int>();
        });

        // ═══════════════════════════════════════════════════════════════════════
        // PRODUCTION: Performance-critical indexes
        // ═══════════════════════════════════════════════════════════════════════

        // Orders by Shift (high frequency in reports)
        modelBuilder.Entity<Order>()
            .HasIndex(o => new { o.ShiftId, o.CreatedAt })
            .HasFilter("IsDeleted = 0");

        // Products by Category (POS page filtering)
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.CategoryId, p.IsActive })
            .HasFilter("IsDeleted = 0");

        // Shifts by User and Status (active shift lookup)
        modelBuilder.Entity<Shift>()
            .HasIndex(s => new { s.UserId, s.IsClosed, s.OpenedAt })
            .HasFilter("IsDeleted = 0");

        // Cash Register Transactions by Shift (shift summary)
        modelBuilder.Entity<CashRegisterTransaction>()
            .HasIndex(c => new { c.ShiftId, c.Type, c.CreatedAt })
            .HasFilter("IsDeleted = 0");

        // Inventory by Branch (critical for multi-branch queries)
        modelBuilder.Entity<BranchInventory>()
            .HasIndex(bi => new { bi.BranchId, bi.ProductId })
            .IsUnique()
            .HasFilter("IsDeleted = 0");

        // Purchase Invoices by Supplier (supplier history)
        modelBuilder.Entity<PurchaseInvoice>()
            .HasIndex(pi => new { pi.SupplierId, pi.InvoiceDate })
            .HasFilter("IsDeleted = 0");

        // Expenses by Category and Status (approval workflow)
        modelBuilder.Entity<Expense>()
            .HasIndex(e => new { e.CategoryId, e.Status, e.ExpenseDate })
            .HasFilter("IsDeleted = 0");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps for all BaseEntity entities
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Only set CreatedAt if it's still the default value (not manually set)
                    if (entry.Entity.CreatedAt == default || entry.Entity.CreatedAt.Year < 2020)
                    {
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONCURRENCY TOKEN UPDATE: Manual RowVersion increment for SQLite
        // ═══════════════════════════════════════════════════════════════════════
        // SQLite doesn't auto-increment byte[] RowVersion like SQL Server.
        // We manually update it here to ensure concurrency checks work.
        
        // Update RowVersion for Shift entities
        foreach (var entry in ChangeTracker.Entries<Shift>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                // Generate new RowVersion: use Guid bytes for uniqueness
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        // Update RowVersion for Order entities
        foreach (var entry in ChangeTracker.Entries<Order>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                // Generate new RowVersion: use Guid bytes for uniqueness
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        // Update RowVersion for Customer entities
        foreach (var entry in ChangeTracker.Entries<Customer>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                // Generate new RowVersion: use Guid bytes for uniqueness
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
