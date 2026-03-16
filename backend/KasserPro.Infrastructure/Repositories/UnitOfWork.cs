namespace KasserPro.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore.Storage;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Tenants = new GenericRepository<Tenant>(context);
        Branches = new GenericRepository<Branch>(context);
        Users = new GenericRepository<User>(context);
        Categories = new GenericRepository<Category>(context);
        Products = new GenericRepository<Product>(context);
        Orders = new GenericRepository<Order>(context);
        OrderItems = new GenericRepository<OrderItem>(context);
        Payments = new GenericRepository<Payment>(context);
        Shifts = new GenericRepository<Shift>(context);
        AuditLogs = new GenericRepository<AuditLog>(context);

        // Sellable V1: New repositories
        Customers = new GenericRepository<Customer>(context);
        DebtPayments = new GenericRepository<DebtPayment>(context);
        StockMovements = new GenericRepository<StockMovement>(context);
        RefundLogs = new GenericRepository<RefundLog>(context);
        Suppliers = new GenericRepository<Supplier>(context);
        PurchaseInvoices = new GenericRepository<PurchaseInvoice>(context);
        PurchaseInvoiceItems = new GenericRepository<PurchaseInvoiceItem>(context);
        PurchaseInvoicePayments = new GenericRepository<PurchaseInvoicePayment>(context);
        SupplierProducts = new GenericRepository<SupplierProduct>(context);

        // Multi-Branch Inventory repositories
        BranchInventories = new GenericRepository<BranchInventory>(context);
        BranchProductPrices = new GenericRepository<BranchProductPrice>(context);
        InventoryTransfers = new GenericRepository<InventoryTransfer>(context);

        // Expenses and Cash Register repositories
        ExpenseCategories = new GenericRepository<ExpenseCategory>(context);
        Expenses = new GenericRepository<Expense>(context);
        ExpenseAttachments = new GenericRepository<ExpenseAttachment>(context);
        CashRegisterTransactions = new GenericRepository<CashRegisterTransaction>(context);

        // User Permissions repository
        UserPermissions = new GenericRepository<UserPermission>(context);
    }

    public IRepository<Tenant> Tenants { get; }
    public IRepository<Branch> Branches { get; }
    public IRepository<User> Users { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<Product> Products { get; }
    public IRepository<Order> Orders { get; }
    public IRepository<OrderItem> OrderItems { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<Shift> Shifts { get; }
    public IRepository<AuditLog> AuditLogs { get; }

    // Sellable V1: New repository properties
    public IRepository<Customer> Customers { get; }
    public IRepository<DebtPayment> DebtPayments { get; }
    public IRepository<StockMovement> StockMovements { get; }
    public IRepository<RefundLog> RefundLogs { get; }
    public IRepository<Supplier> Suppliers { get; }
    public IRepository<PurchaseInvoice> PurchaseInvoices { get; }
    public IRepository<PurchaseInvoiceItem> PurchaseInvoiceItems { get; }
    public IRepository<PurchaseInvoicePayment> PurchaseInvoicePayments { get; }
    public IRepository<SupplierProduct> SupplierProducts { get; }

    // Multi-Branch Inventory repository properties
    public IRepository<BranchInventory> BranchInventories { get; }
    public IRepository<BranchProductPrice> BranchProductPrices { get; }
    public IRepository<InventoryTransfer> InventoryTransfers { get; }

    // Expenses and Cash Register repository properties
    public IRepository<ExpenseCategory> ExpenseCategories { get; }
    public IRepository<Expense> Expenses { get; }
    public IRepository<ExpenseAttachment> ExpenseAttachments { get; }
    public IRepository<CashRegisterTransaction> CashRegisterTransactions { get; }

    // User Permissions repository property
    public IRepository<UserPermission> UserPermissions { get; }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    /// <summary>
    /// Begin a database transaction for atomic operations.
    /// CRITICAL FIX: If a transaction is already active, return it instead of creating a nested one.
    /// This prevents "The connection is already in a transaction" errors in SQLite.
    /// </summary>
    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        // Check both our tracked transaction AND EF Core's CurrentTransaction
        var existingTransaction = _currentTransaction ?? _context.Database.CurrentTransaction;

        if (existingTransaction != null)
        {
            // Transaction already active - return it to prevent nesting
            // NOTE: Caller must check if they own this transaction before committing/rolling back
            return existingTransaction;
        }

        // No active transaction - create a new one and track it
        _currentTransaction = await _context.Database.BeginTransactionAsync();
        return _currentTransaction;
    }

    /// <summary>
    /// P0-8: Check if a transaction is already in progress.
    /// Uses dual-check: our tracked transaction AND EF Core's CurrentTransaction.
    /// </summary>
    public bool HasActiveTransaction =>
        _currentTransaction != null || _context.Database.CurrentTransaction != null;

    /// <summary>
    /// Get the current active transaction, or null if none exists.
    /// </summary>
    public IDbContextTransaction? CurrentTransaction =>
        _currentTransaction ?? _context.Database.CurrentTransaction;

    /// <summary>
    /// Commit the current active transaction and clear the reference.
    /// </summary>
    public async Task CommitTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.CommitAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    /// <summary>
    /// Rollback the current active transaction and clear the reference.
    /// </summary>
    public async Task RollbackTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        // Clear our transaction reference when disposing
        _currentTransaction = null;
        _context.Dispose();
    }
}
