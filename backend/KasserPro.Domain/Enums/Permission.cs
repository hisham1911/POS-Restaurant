namespace KasserPro.Domain.Enums;

/// <summary>
/// Granular permissions for cashier users.
/// Admins automatically get all permissions.
/// </summary>
public enum Permission
{
    // Point of Sale
    PosSell            = 100,
    PosApplyDiscount   = 101,

    // Orders
    OrdersView         = 200,
    OrdersRefund       = 201,

    // Products
    ProductsView       = 300,
    ProductsManage     = 301,
    ProductsCreateFromPOS = 302,

    // Categories
    CategoriesView     = 400,
    CategoriesManage   = 401,

    // Customers
    CustomersView      = 500,
    CustomersManage    = 501,

    // Reports
    ReportsView        = 600,

    // Expenses
    ExpensesView       = 700,
    ExpensesCreate     = 701,

    // Inventory
    InventoryView      = 800,

    // Shifts
    ShiftsManage       = 900,

    // Cash Register
    CashRegisterView   = 1000,

    // Branches
    BranchesView       = 1100,

    // Suppliers
    SuppliersView      = 1200,
    SuppliersManage    = 1201,
}
