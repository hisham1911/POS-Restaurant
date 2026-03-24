# 🚀 DbContext Optimization Guide

## 🎯 Current Issue Analysis
Your `KasserproContext.cs` contains **24 DbSets** with extensive `OnModelCreating` configuration (500+ lines). This is likely a major contributor to slow build times.

## 🔧 Immediate Optimizations

### 1. Split DbContext into Partial Classes

#### Create: `KasserproContext.Core.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using KasserPro.API.TempModels;

namespace KasserPro.API;

public partial class KasserproContext : DbContext
{
    public KasserproContext() { }
    
    public KasserproContext(DbContextOptions<KasserproContext> options) : base(options) { }

    // Core business entities only
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderItem> OrderItems { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Customer> Customers { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
}
```

#### Create: `KasserproContext.Inventory.cs`
```csharp
namespace KasserPro.API;

public partial class KasserproContext
{
    // Inventory-related entities
    public virtual DbSet<BranchInventory> BranchInventories { get; set; }
    public virtual DbSet<StockMovement> StockMovements { get; set; }
    public virtual DbSet<InventoryTransfer> InventoryTransfers { get; set; }
    public virtual DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
    public virtual DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }
    public virtual DbSet<PurchaseInvoicePayment> PurchaseInvoicePayments { get; set; }
}
```

#### Create: `KasserproContext.System.cs`
```csharp
namespace KasserPro.API;

public partial class KasserproContext
{
    // System and admin entities
    public virtual DbSet<Tenant> Tenants { get; set; }
    public virtual DbSet<Branch> Branches { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Shift> Shifts { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<Expense> Expenses { get; set; }
    public virtual DbSet<ExpenseCategory> ExpenseCategories { get; set; }
    public virtual DbSet<ExpenseAttachment> ExpenseAttachments { get; set; }
    public virtual DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }
    public virtual DbSet<RefundLog> RefundLogs { get; set; }
    public virtual DbSet<Supplier> Suppliers { get; set; }
    public virtual DbSet<SupplierProduct> SupplierProducts { get; set; }
    public virtual DbSet<BranchProductPrice> BranchProductPrices { get; set; }
}
```

### 2. Optimize OnModelCreating with Configuration Classes

#### Create: `Configurations/OrderConfiguration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KasserPro.API.TempModels;

namespace KasserPro.API.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasIndex(e => e.BranchId, "IX_Orders_BranchId");
        builder.HasIndex(e => e.CustomerId, "IX_Orders_CustomerId");
        builder.HasIndex(e => e.OrderNumber, "IX_Orders_OrderNumber").IsUnique();
        builder.HasIndex(e => e.ShiftId, "IX_Orders_ShiftId");
        builder.HasIndex(e => e.TenantId, "IX_Orders_TenantId");
        builder.HasIndex(e => e.UserId, "IX_Orders_UserId");

        builder.HasOne(d => d.Branch).WithMany(p => p.Orders)
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Customer).WithMany(p => p.Orders)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Shift).WithMany(p => p.Orders)
            .HasForeignKey(d => d.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Tenant).WithMany(p => p.Orders)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.User).WithMany(p => p.Orders)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### Update: `KasserproContext.Configuration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace KasserPro.API;

public partial class KasserproContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=kasserpro.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Use assembly scanning instead of manual configuration
        // This is much faster for compilation
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
```

## 🚀 Quick Implementation Steps

### Step 1: Backup Current File
```bash
cp backend/KasserPro.API/KasserproContext.cs backend/KasserPro.API/KasserproContext.cs.backup
```

### Step 2: Create Configurations Folder
```bash
mkdir backend/KasserPro.API/Configurations
```

### Step 3: Test Build Time After Each Change
```bash
cd backend
time dotnet build KasserPro.API/KasserPro.API.csproj --no-restore
```

## 🎯 Expected Results

- **Before:** 500+ lines in single file = slow compilation
- **After:** Multiple smaller files = faster parallel compilation
- **Build Time Reduction:** 30-50% improvement expected

## ⚠️ Important Notes

1. **Don't change the original file yet** - test with copies first
2. **Ensure all DbSets are moved** - missing ones will cause runtime errors
3. **Test the application after changes** - ensure EF Core still works correctly
4. **Keep the same namespace** - avoid breaking existing code

## 🔄 Rollback Plan

If issues occur:
```bash
cp backend/KasserPro.API/KasserproContext.cs.backup backend/KasserPro.API/KasserproContext.cs
```

This optimization should significantly reduce build times by allowing the compiler to process smaller compilation units in parallel.