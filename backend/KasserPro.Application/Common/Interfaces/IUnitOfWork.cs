namespace KasserPro.Application.Common.Interfaces;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

public interface IUnitOfWork : IDisposable
{
    IRepository<Tenant> Tenants { get; }
    IRepository<Branch> Branches { get; }
    IRepository<User> Users { get; }
    IRepository<Category> Categories { get; }
    IRepository<Product> Products { get; }
    IRepository<Order> Orders { get; }
    IRepository<OrderItem> OrderItems { get; }
    IRepository<Payment> Payments { get; }
    IRepository<Shift> Shifts { get; }
    IRepository<AuditLog> AuditLogs { get; }

    // Sellable V1: New repositories
    IRepository<Customer> Customers { get; }
    IRepository<DebtPayment> DebtPayments { get; }
    IRepository<StockMovement> StockMovements { get; }
    IRepository<RefundLog> RefundLogs { get; }
    IRepository<Supplier> Suppliers { get; }
    IRepository<PurchaseInvoice> PurchaseInvoices { get; }
    IRepository<PurchaseInvoiceItem> PurchaseInvoiceItems { get; }
    IRepository<PurchaseInvoicePayment> PurchaseInvoicePayments { get; }
    IRepository<SupplierProduct> SupplierProducts { get; }

    // Expenses and Cash Register repositories
    IRepository<ExpenseCategory> ExpenseCategories { get; }
    IRepository<Expense> Expenses { get; }
    IRepository<ExpenseAttachment> ExpenseAttachments { get; }
    IRepository<CashRegisterTransaction> CashRegisterTransactions { get; }

    // Multi-Branch Inventory repositories
    IRepository<BranchInventory> BranchInventories { get; }
    IRepository<BranchProductPrice> BranchProductPrices { get; }
    IRepository<InventoryTransfer> InventoryTransfers { get; }

    // User Permissions repository
    IRepository<UserPermission> UserPermissions { get; }

    Task<int> SaveChangesAsync();

    /// <summary>
    /// Begin a database transaction for atomic operations.
    /// If a transaction is already active, returns the existing transaction to prevent nesting.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync();

    /// <summary>
    /// P0-8: True if a database transaction is currently active on this context.
    /// Used by sub-services to avoid nested transaction errors.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Get the current active transaction, or null if none exists.
    /// </summary>
    IDbContextTransaction? CurrentTransaction { get; }

    /// <summary>
    /// Commit the current active transaction.
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Rollback the current active transaction.
    /// </summary>
    Task RollbackTransactionAsync();
}
