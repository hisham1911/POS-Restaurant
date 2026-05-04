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
    PosCreditSale      = 102,
    PosEditPrice       = 103,
    PosDeleteItem      = 104,
    PosCancelOrder     = 105,
    PosChangeBatch     = 106,

    // Orders
    OrdersView         = 200,
    OrdersRefund       = 201,
    OrdersCreate       = 202,

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
    ExpensesManage     = 702,
    ExpensesApprove    = 703,

    // Inventory
    InventoryView      = 800,
    InventoryManage    = 801,
    InventoryTransfer  = 802,

    // Shifts
    ShiftsManage       = 900,

    // Cash Register
    CashRegisterView      = 1000,
    CashRegisterManage    = 1001,
    CashRegisterTransfer  = 1002,
    CashRegisterReconcile = 1003,

    // Branches
    BranchesView       = 1100,

    // Suppliers
    SuppliersView      = 1200,
    SuppliersManage    = 1201,

    // Purchase Invoices
    PurchaseInvoicesView   = 1300,
    PurchaseInvoicesManage = 1301,

    // Users
    UsersView      = 1400,
    UsersManage    = 1401,

    // Settings
    SettingsManage = 1500,

    // Delivery
    DeliveryView   = 1600,
    DeliveryManage = 1601,

    // Wallet
    WalletView   = 1700,
    WalletManage = 1701,
}
