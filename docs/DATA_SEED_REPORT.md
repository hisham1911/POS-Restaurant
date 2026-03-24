# 📊 KasserPro Data Seed Report

**Date:** February 9, 2026  
**Operation:** Complete Data Reset & Realistic Seeding  
**Status:** ✅ Ready for Execution

---

## 🎯 Objective

Replace all existing seed/test/demo data with **realistic, coherent, production-like data** for pre-launch testing.

**This is a DATA-ONLY operation** - no code, schema, or business logic changes.

---

## 📋 Tables Cleared

### Business Data (DELETED)
- ✅ Orders & OrderItems
- ✅ Payments
- ✅ Shifts
- ✅ Products & Categories
- ✅ Customers
- ✅ Suppliers & SupplierProducts
- ✅ PurchaseInvoices & PurchaseInvoiceItems & PurchaseInvoicePayments
- ✅ Expenses & ExpenseCategories & ExpenseAttachments
- ✅ StockMovements
- ✅ InventoryTransfers
- ✅ BranchInventories & BranchProductPrices
- ✅ CashRegisterTransactions
- ✅ AuditLogs
- ✅ RefundLogs

### Preserved Data (KEPT)
- ✅ Users (updated with realistic names)
- ✅ Roles & Permissions
- ✅ Tenants (updated with realistic business info)
- ✅ Branches (updated with realistic addresses)
- ✅ Devices (if any)

---

## 📊 Data Created

### Core Data

| Entity | Count | Description |
|--------|-------|-------------|
| **Tenant** | 1 | "مقهى النخبة" (Elite Café) |
| **Branch** | 1 | Main branch in downtown Cairo |
| **Users** | 3 | 1 Admin + 2 Cashiers |

### Business Data

| Entity | Count | Description |
|--------|-------|-------------|
| **Categories** | 7 | Hot Coffee, Iced Coffee, Tea, Juices, Bakery, Desserts, Snacks |
| **Products** | 35 | Realistic café menu with prices, costs, stock |
| **Customers** | 8 | With loyalty points, order history |
| **Suppliers** | 4 | Coffee, Dairy, Fruits, Bakery suppliers |
| **Expense Categories** | 8 | Salaries, Rent, Electricity, Water, etc. |

### Transactional Data

| Entity | Count | Description |
|--------|-------|-------------|
| **Shifts** | 15 | 14 closed shifts (past 14 days) + 1 open shift (today) |
| **Orders** | ~150 | Completed orders with realistic patterns |
| **Payments** | ~150 | Cash & Card payments linked to orders |
| **Purchase Invoices** | 5 | Stock-in invoices from suppliers |
| **Expenses** | 10 | Various expenses over past 30 days |
| **Cash Register Transactions** | 8 | Cash in/out movements |

---

## 🔄 Seeding Order (Respects Foreign Keys)

1. **Clear Phase** (in reverse dependency order)
   - Payments → OrderItems → Orders → RefundLogs
   - CashRegisterTransactions
   - ExpenseAttachments → Expenses → ExpenseCategories
   - PurchaseInvoicePayments → PurchaseInvoiceItems → PurchaseInvoices
   - SupplierProducts → Suppliers
   - StockMovements, InventoryTransfers, BranchInventories, BranchProductPrices
   - Products → Categories
   - Customers
   - Shifts
   - AuditLogs

2. **Seed Phase** (in dependency order)
   - Tenant (update existing)
   - Branches (use existing)
   - Users (use existing)
   - Categories
   - Products (with initial stock)
   - Customers
   - Suppliers
   - ExpenseCategories
   - Shifts & Orders (with payments)
   - PurchaseInvoices (updates product stock)
   - Expenses
   - CashRegisterTransactions

---

## 💰 Financial Logic (Tax Exclusive)

All financial calculations follow the **Tax Exclusive (Additive)** model:

```
NetTotal = UnitPrice × Quantity
TaxAmount = NetTotal × (TaxRate / 100)
TotalAmount = NetTotal + TaxAmount
```

**Tax Rate:** 14% (Egypt VAT)  
**Tax Model:** Exclusive (tax added on top)

---

## 📦 Product Inventory

All products seeded with:
- ✅ Realistic prices (15 EGP - 55 EGP)
- ✅ Cost prices (40% of selling price)
- ✅ Initial stock quantities (50-300 units)
- ✅ Low stock thresholds (8-40 units)
- ✅ SKU and Barcode
- ✅ Track inventory enabled

**Stock Updates:**
- Purchase invoices ADD stock
- Completed orders DEDUCT stock
- Stock movements tracked with timestamps

---

## 🔐 Test Credentials

| Role | Email | Password | Name |
|------|-------|----------|------|
| **Admin** | admin@kasserpro.com | Admin@123 | أحمد المدير |
| **Cashier** | mohamed@kasserpro.com | 123456 | محمد الكاشير |
| **Cashier** | fatima@kasserpro.com | 123456 | فاطمة الكاشير |

---

## ✅ Data Consistency Rules

### Foreign Key Integrity
- ✅ All Orders linked to valid Shifts
- ✅ All OrderItems linked to valid Products
- ✅ All Payments linked to valid Orders
- ✅ All PurchaseInvoiceItems linked to valid Products & Suppliers
- ✅ All Expenses linked to valid ExpenseCategories

### Financial Consistency
- ✅ Order.Total = Sum(OrderItems.Total)
- ✅ Order.AmountPaid = Sum(Payments.Amount)
- ✅ Shift.TotalCash = Sum(Cash Payments in Shift)
- ✅ Shift.TotalCard = Sum(Card Payments in Shift)
- ✅ Shift.ExpectedBalance = OpeningBalance + TotalCash

### Inventory Consistency
- ✅ Product.StockQuantity reflects all purchases and sales
- ✅ LastStockUpdate timestamp maintained
- ✅ No negative stock (AllowNegativeStock = false)

### Temporal Consistency
- ✅ Shifts span 14 days history + 1 open shift
- ✅ Orders created within shift time ranges
- ✅ Payments created at order completion time
- ✅ Purchase invoices dated over past 30 days
- ✅ Expenses dated over past 30 days

---

## 🚀 How to Execute

### Option 1: PowerShell Script (Recommended)
```powershell
.\reset-data.ps1
```

### Option 2: Manual Steps
```bash
# 1. Delete database
rm src/KasserPro.API/kasserpro.db
rm src/KasserPro.API/kasserpro.db-wal
rm src/KasserPro.API/kasserpro.db-shm

# 2. Run backend (will auto-seed)
cd src/KasserPro.API
dotnet run
```

### Option 3: From Visual Studio
1. Delete `kasserpro.db` file from `src/KasserPro.API/`
2. Press F5 to run
3. Data will be seeded automatically on startup

---

## 🧪 Verification Checklist

After seeding, verify:

### Backend Verification
- [ ] Application starts without errors
- [ ] Database file created: `src/KasserPro.API/kasserpro.db`
- [ ] No migration errors in console
- [ ] Seeding messages show success

### API Verification
- [ ] `GET /api/auth/login` works with test credentials
- [ ] `GET /api/products` returns 35 products
- [ ] `GET /api/categories` returns 7 categories
- [ ] `GET /api/shifts/current` returns open shift
- [ ] `GET /api/orders` returns historical orders
- [ ] `GET /api/customers` returns 8 customers
- [ ] `GET /api/suppliers` returns 4 suppliers

### Frontend Verification
- [ ] Login works with test credentials
- [ ] POS screen loads products immediately
- [ ] Can create and complete new order
- [ ] Inventory shows correct stock levels
- [ ] Reports show historical data
- [ ] Shift management shows 15 shifts

### Data Integrity Verification
- [ ] Order totals match payment amounts
- [ ] Shift totals match order payments
- [ ] Product stock reflects sales
- [ ] No orphan records
- [ ] All foreign keys valid

---

## 📝 Known Limitations

1. **Single Branch Only**
   - Multi-branch features not tested with seed data
   - Branch inventory transfers not seeded

2. **No Refunds**
   - No refunded orders in seed data
   - Refund flow must be tested manually

3. **Limited Order Types**
   - Mostly DineIn orders (70%)
   - Fewer Takeaway/Delivery orders (30%)

4. **No Discounts**
   - Orders created without discounts
   - Discount feature must be tested separately

5. **No Customer Loyalty Redemption**
   - Customers have loyalty points but no redemption history

---

## 🔧 Maintenance

### To Re-seed Data
Simply run the reset script again:
```powershell
.\reset-data.ps1
```

### To Add More Data
Edit `src/KasserPro.Infrastructure/Data/RealisticDataSeeder.cs` and adjust:
- Product counts
- Order counts per shift
- Date ranges
- Customer counts
- etc.

### To Preserve Specific Data
Comment out the relevant `DELETE` statements in `ClearBusinessDataAsync()` method.

---

## 📞 Support

If you encounter issues:

1. **Check Console Output**
   - Look for seeding progress messages
   - Check for any error messages

2. **Verify Database File**
   - Ensure `kasserpro.db` exists
   - Check file size (should be > 1 MB)

3. **Check Migrations**
   - Ensure all migrations applied
   - Run `dotnet ef database update` if needed

4. **Reset Completely**
   - Delete database file
   - Run reset script again

---

## ✅ Sign-Off

**Data Seeder:** RealisticDataSeeder.cs  
**Execution Script:** reset-data.ps1  
**Status:** Ready for Pre-Launch Testing  
**Last Updated:** February 9, 2026

---

**Next Steps:**
1. Run `.\reset-data.ps1`
2. Verify all checklist items
3. Begin pre-launch testing
4. Report any data inconsistencies

