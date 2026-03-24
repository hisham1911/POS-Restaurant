# P0 HARDENING IMPLEMENTATION GUIDE — KasserPro POS

**Scope:** Single-Branch Local POS · 1–3 concurrent users · SQLite · On-premise  
**Date:** 2026-02-12  
**Status:** Execution-Ready  
**Audience:** Mid-level developer with ASP.NET Core + React experience

---

## Scope Lock

```
✅ IN SCOPE                          ❌ OUT OF SCOPE
─────────────────────────────────    ─────────────────────────────────
Single branch, local network          Multi-tenant SaaS
SQLite single-file database           Redis, PostgreSQL, distributed DB
1–3 concurrent POS terminals          Horizontal scaling
On-premise Windows deployment         Cloud deployment
JWT hardening (existing flow)         OAuth2 / OIDC / refresh tokens
Minimal safe fixes                    Architecture redesign
```

---

## Table of Contents

- [P0-1: JWT Secret Hardening](#p0-1-jwt-secret-hardening)
- [P0-2: Disable Seed & Demo Credentials in Production](#p0-2-disable-seed--demo-credentials-in-production)
- [P0-3: Fix Stock TOCTOU Race Condition](#p0-3-fix-stock-toctou-race-condition)
- [P0-4: Fix Double Tax Calculation](#p0-4-fix-double-tax-calculation)
- [P0-5: Fix SignalR Receipt Broadcast](#p0-5-fix-signalr-receipt-broadcast)
- [P0-6: Secure DeviceTestController](#p0-6-secure-devicetestcontroller)
- [P0-7: Disable Retry on Financial Mutations](#p0-7-disable-retry-on-financial-mutations)
- [P0-8: Cash Register Concurrency Guard](#p0-8-cash-register-concurrency-guard)
- [Final Validation Checklist](#final-validation-checklist)

---

## P0-1: JWT Secret Hardening

### 1) Problem Explanation

The JWT signing key is hardcoded in `src/KasserPro.API/appsettings.json`:

```json
"Jwt": {
    "Key": "YourSuperSecretKeyHere_MustBe32Characters!",
```

Anyone who reads this file (or the Git repo) can forge valid JWT tokens for any user, including Admin.

### 2) Why It Is Dangerous

- An attacker with repo access can create Admin tokens offline
- The key is version-controlled — it exists in every clone and every Git history entry
- The string `"YourSuperSecretKeyHere"` is a common placeholder that security scanners flag

### 3) Files to Modify

| File | Action |
|------|--------|
| `src/KasserPro.API/appsettings.json` | Replace hardcoded key with empty string |
| `src/KasserPro.API/appsettings.example.json` | Fix key path (`JwtSettings` → `Jwt`), add placeholder |
| `src/KasserPro.API/Program.cs` | Add startup guard that rejects missing/short keys |

### 4) Implementation Steps

**Step 1 — `appsettings.json`: Remove the hardcoded secret**

```json
"Jwt": {
    "Key": "",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
}
```

**Step 2 — `appsettings.example.json`: Fix key names to match actual config**

Current file uses `JwtSettings.SecretKey`. The actual config uses `Jwt.Key`. Replace entire content:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Jwt": {
    "Key": "GENERATE_A_RANDOM_32_CHARACTER_KEY_HERE",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=kasserpro.db"
  },
  "ShiftAutoClose": {
    "Enabled": true,
    "HoursThreshold": 12
  }
}
```

**Step 3 — `Program.cs`: Add startup guard**

Add this block right after `var builder = WebApplication.CreateBuilder(args);` (line 14):

```csharp
// P0-1: Fail startup if JWT secret is missing or too short
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "FATAL: JWT Key is missing or too short. " +
        "Set environment variable 'Jwt__Key' to a random string of at least 32 characters. " +
        "Example PowerShell: $env:Jwt__Key = [Convert]::ToBase64String((1..40 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])");
}
```

### 5) Transaction Boundaries

None. This is a startup configuration change.

### 6) Edge Cases

- **Development convenience:** The developer needs to set the env var before running. On Windows: `$env:Jwt__Key = "MyDevelopmentKey_AtLeast32Characters!!"`
- **Existing tokens:** All previously issued tokens become invalid when the key changes. This is expected — users simply log in again.
- **Double-underscore:** `Jwt__Key` is how ASP.NET Core maps `Jwt:Key` from environment variables. The `__` replaces `:`.

### 7) Manual Validation

```powershell
# 1. Start without env var — should crash:
cd src/KasserPro.API
dotnet run
# Expected: InvalidOperationException at startup

# 2. Set env var and start:
$env:Jwt__Key = "TestKeyForDevelopment_AtLeast32Chars!!"
dotnet run
# Expected: App starts normally

# 3. Login works:
curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d '{"email":"admin@kasserpro.com","password":"Admin@123"}'
# Expected: 200 OK with token
```

### 8) Failure Scenario Simulation

1. Stop the server
2. Unset the env var: `Remove-Item Env:Jwt__Key`
3. Run `dotnet run`
4. Verify the app throws `InvalidOperationException` and does NOT start
5. Confirm no HTTP endpoint is reachable

### 9) Definition of Done

- [ ] `appsettings.json` has `"Key": ""`
- [ ] `appsettings.example.json` uses `Jwt.Key` (not `JwtSettings.SecretKey`)
- [ ] App refuses to start when `Jwt:Key` is missing or < 32 chars
- [ ] App starts and login works when `Jwt__Key` env var is set

---

## P0-2: Disable Seed & Demo Credentials in Production

### 1) Problem Explanation

Two issues:

**A)** `Program.cs` calls `ButcherDataSeeder.SeedAsync(context)` on every startup. This seeder wipes all business data and re-creates demo data including users with known passwords. In production, a restart = data loss.

**B)** `LoginPage.tsx` displays demo credentials (`admin@kasserpro.com / Admin@123`) unconditionally in the UI.

### 2) Why It Is Dangerous

- **A)** Production restart erases all real customer orders, inventory, and financial records
- **B)** Anyone who opens the login page sees working admin credentials
- These two combined mean: anyone can log in as Admin to a fresh-wiped system after any restart

### 3) Files to Modify

| File | Action |
|------|--------|
| `src/KasserPro.API/Program.cs` | Gate seeder behind `IsDevelopment()` |
| `client/src/pages/auth/LoginPage.tsx` | Gate demo credentials behind `import.meta.env.DEV` |

### 4) Implementation Steps

**Step 1 — `Program.cs`: Restrict seeder to Development only**

Current code (around line 116):

```csharp
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Apply migrations
        await context.Database.MigrateAsync();
        
        // Seed butcher shop data
        await ButcherDataSeeder.SeedAsync(context);
    }
}
```

Replace with:

```csharp
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Apply migrations
        await context.Database.MigrateAsync();
        
        // P0-2: Only seed demo data in Development environment
        if (app.Environment.IsDevelopment())
        {
            await ButcherDataSeeder.SeedAsync(context);
        }
    }
}
```

**Step 2 — `LoginPage.tsx`: Gate demo credentials behind DEV check**

Current code (around line 77):

```tsx
{/* Demo Credentials */}
<div className="mt-6 p-4 bg-gray-50 rounded-xl text-sm">
  <p className="font-medium text-gray-700 mb-2">بيانات تجريبية:</p>
  <p className="text-gray-600">
    <span className="font-medium">المدير:</span> admin@kasserpro.com / Admin@123
  </p>
  <p className="text-gray-600">
    <span className="font-medium">الكاشير:</span> ahmed@kasserpro.com / 123456
  </p>
</div>
```

Replace with:

```tsx
{/* Demo Credentials — Development Only */}
{import.meta.env.DEV && (
  <div className="mt-6 p-4 bg-gray-50 rounded-xl text-sm">
    <p className="font-medium text-gray-700 mb-2">بيانات تجريبية:</p>
    <p className="text-gray-600">
      <span className="font-medium">المدير:</span> admin@kasserpro.com / Admin@123
    </p>
    <p className="text-gray-600">
      <span className="font-medium">الكاشير:</span> ahmed@kasserpro.com / 123456
    </p>
  </div>
)}
```

### 5) Transaction Boundaries

None.

### 6) Edge Cases

- **First production deployment:** The database will be empty. You need to manually create the first Admin user. Add a one-time setup script or a `--seed-initial` CLI flag.
- **Missing migration data:** `MigrateAsync()` still runs in production — this is correct. Only the demo data seeder is gated.
- **Vite build modes:** `import.meta.env.DEV` is `true` during `vite dev`, `false` during `vite build`. The production bundle physically excludes the demo credentials string.

### 7) Manual Validation

```powershell
# Backend — verify seeder skipped in Production:
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Jwt__Key = "YourProductionSecretKey_32Chars!!"
cd src/KasserPro.API
dotnet run
# Expected: App starts WITHOUT "بدء تحميل بيانات المجزر" console message

# Frontend — verify no credentials in production build:
cd client
npm run build
Select-String -Path dist/assets/*.js -Pattern "Admin@123"
# Expected: 0 matches
```

### 8) Failure Scenario Simulation

1. Set `ASPNETCORE_ENVIRONMENT=Production`
2. Delete `kasserpro.db`
3. Run the app — it should start with an empty database
4. The login page should NOT show any credentials
5. `POST /api/auth/login` with `admin@kasserpro.com / Admin@123` → should fail (user doesn't exist)

### 9) Definition of Done

- [ ] `ButcherDataSeeder.SeedAsync` only executes when `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Production frontend build contains zero instances of `Admin@123`
- [ ] `MigrateAsync()` still runs in all environments

---

## P0-3: Fix Stock TOCTOU Race Condition

### 1) Problem Explanation

Stock is checked in `CreateAsync` (when the draft order is created) but decremented in `CompleteAsync` (when payment is processed). Between these two calls, another cashier can sell the same product, depleting stock. The second cashier's `CompleteAsync` then decrements stock below zero.

Additionally, `CreateAsync` reads `product.StockQuantity` from the `Products` table, but `BatchDecrementStockAsync` decrements `BranchInventory.Quantity` — a completely different table.

### 2) Why It Is Dangerous

- Stock goes negative despite `AllowNegativeStock = false`
- Products are sold that don't physically exist in the store
- Inventory reports become unreliable

### 3) Files to Modify

| File | Action |
|------|--------|
| `src/KasserPro.Application/Services/Implementations/OrderService.cs` | Add stock re-validation inside `CompleteAsync` before decrement |
| `src/KasserPro.Infrastructure/Services/InventoryService.cs` | Add hard rejection in `BatchDecrementStockAsync` |

### 4) Implementation Steps

**Step 1 — `OrderService.cs` / `CompleteAsync`: Re-validate stock inside the transaction**

In `CompleteAsync`, after the line `await _unitOfWork.SaveChangesAsync();` (which saves the order status) and BEFORE `BatchDecrementStockAsync`, add a stock re-check:

Find this block (around line 498–505):

```csharp
            await _unitOfWork.SaveChangesAsync();
            
            // Sellable V1: Decrement stock for all items in the order
            // This runs within the same transaction for data integrity
            var stockItems = order.Items
                .Where(i => i.ProductId > 0)
                .Select(i => (i.ProductId, i.Quantity))
                .ToList();
```

Replace with:

```csharp
            await _unitOfWork.SaveChangesAsync();
            
            // P0-3: Re-validate stock INSIDE the write transaction.
            // This is the authoritative check. The CreateAsync check is just a UX hint.
            // SQLite's write lock guarantees no other writer can change stock between
            // this read and the decrement below.
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentUser.TenantId);
            if (tenant != null && !tenant.AllowNegativeStock)
            {
                foreach (var item in order.Items.Where(i => i.ProductId > 0))
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                    if (product != null && product.TrackInventory)
                    {
                        var branchStock = await _inventoryService.GetAvailableQuantityAsync(
                            item.ProductId, _currentUser.BranchId);
                        if (branchStock < item.Quantity)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
                                $"المخزون تغير أثناء إتمام الطلب. المنتج: {item.ProductName}. " +
                                $"المتاح الآن: {branchStock}، المطلوب: {item.Quantity}");
                        }
                    }
                }
            }
            
            // Decrement stock for all items in the order
            var stockItems = order.Items
                .Where(i => i.ProductId > 0)
                .Select(i => (i.ProductId, i.Quantity))
                .ToList();
```

**Step 2 — `InventoryService.cs` / `BatchDecrementStockAsync`: Add hard rejection**

Find this block (around line 280):

```csharp
            if (inventory != null)
            {
                var balanceBefore = inventory.Quantity;
                inventory.Quantity -= quantity;
                inventory.LastUpdatedAt = DateTime.UtcNow;
```

Replace with:

```csharp
            if (inventory != null)
            {
                var balanceBefore = inventory.Quantity;
                
                // P0-3: Defense-in-depth. If stock somehow passed validation but
                // is insufficient here, throw to abort the entire transaction.
                if (balanceBefore < quantity)
                {
                    throw new InvalidOperationException(
                        $"Stock guard: Product {productId} at Branch {branchId} has {balanceBefore} units, " +
                        $"but {quantity} were requested. Transaction will be rolled back.");
                }
                
                inventory.Quantity -= quantity;
                inventory.LastUpdatedAt = DateTime.UtcNow;
```

**Step 3 — `OrderService.cs` / `CreateAsync`: Switch stock check to BranchInventory**

Find this block (around line 147):

```csharp
            if (product.TrackInventory)
            {
                var currentStock = product.StockQuantity ?? 0;
                if (currentStock < item.Quantity && !tenant.AllowNegativeStock)
                {
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK, 
                        $"المخزون غير كافٍ للمنتج: {product.Name}. المتاح: {currentStock}، المطلوب: {item.Quantity}");
                }
            }
```

Replace with:

```csharp
            if (product.TrackInventory)
            {
                // P0-3: Read from BranchInventory (same table that gets decremented).
                // This is a soft check (UX hint). The hard check is inside CompleteAsync.
                var currentStock = await _inventoryService.GetAvailableQuantityAsync(
                    product.Id, _currentUser.BranchId);
                if (currentStock < item.Quantity && !tenant.AllowNegativeStock)
                {
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK, 
                        $"المخزون غير كافٍ للمنتج: {product.Name}. المتاح: {currentStock}، المطلوب: {item.Quantity}");
                }
            }
```

### 5) Transaction Boundaries

`CompleteAsync` already wraps everything in `await _unitOfWork.BeginTransactionAsync()`. Since SQLite uses a single-writer lock, once this transaction starts writing (the `SaveChangesAsync` call), no other writer can proceed. The stock re-read happens AFTER that write, inside the lock. So the sequence is:

```
T1(Cashier1): BEGIN → write order status → read stock (=3) → decrement (3→1) → COMMIT
T2(Cashier2): BEGIN → write order status → ⏳ BLOCKED by T1's lock
T2(Cashier2): ... unblocked → read stock (=1) → 1 < 2 → REJECT with "المخزون تغير"
```

### 6) Edge Cases

- **Product with `TrackInventory = false`:** Skipped entirely. No stock check.
- **`AllowNegativeStock = true`:** Both the soft check and hard check are skipped. The `BatchDecrementStockAsync` guard still throws — you need to wrap the guard in the same `AllowNegativeStock` check. Modify the guard to:

```csharp
// Only enforce if negative stock is NOT allowed
// (check tenant config — but we don't have it here, so this is a hard safety net)
if (balanceBefore < quantity)
{
    _logger.LogWarning("Stock guard: Product {ProductId} at Branch {BranchId} " +
        "has {Balance} units, requested {Qty}. Proceeding (may be AllowNegativeStock).",
        productId, branchId, balanceBefore, quantity);
    // Don't throw — the service-layer check already allowed it
}
```

Wait — this contradicts the hard rejection. The cleanest approach: pass a `bool rejectNegative` to `BatchDecrementStockAsync`. But that's more invasive. Instead, keep the guard as a log-only warning and rely on the `CompleteAsync` check as the enforcer. Here's the final version:

```csharp
            if (inventory != null)
            {
                var balanceBefore = inventory.Quantity;
                
                // P0-3: Log warning if stock would go negative.
                // The real enforcement is in CompleteAsync's re-validation.
                // This is a defense-in-depth safety net.
                if (balanceBefore < quantity)
                {
                    _logger.LogWarning(
                        "Stock would go negative: Product={ProductId}, Branch={BranchId}, " +
                        "Available={Available}, Requested={Requested}",
                        productId, branchId, balanceBefore, quantity);
                }
                
                inventory.Quantity -= quantity;
                inventory.LastUpdatedAt = DateTime.UtcNow;
```

Actually, given our scope (single-branch, `AllowNegativeStock` may be true for some shops), keep it as a warning. The enforcement point is `CompleteAsync`.

### 7) Manual Validation

**Multi-tab oversell test:**

1. Open two browser tabs, both logged in as cashiers
2. Product X has stock = 1
3. Tab 1: Add 1x Product X to cart, proceed to payment screen (don't pay yet)
4. Tab 2: Add 1x Product X to cart, proceed to payment screen
5. Tab 1: Click Pay → should succeed, stock = 0
6. Tab 2: Click Pay → should fail with "المخزون تغير أثناء إتمام الطلب"
7. Verify in database: `BranchInventory.Quantity = 0` (not -1)

### 8) Failure Scenario Simulation

```powershell
# Using curl, simulate two concurrent completions:
# First, create two draft orders for the same product (stock=1):
$order1 = Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/orders" ...
$order2 = Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/orders" ...

# Complete both simultaneously:
Start-Job { Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/orders/$($order1.data.id)/complete" ... }
Start-Job { Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/orders/$($order2.data.id)/complete" ... }

# One should succeed (200), one should fail (400 with INSUFFICIENT_STOCK)
```

### 9) Definition of Done

- [ ] `CreateAsync` reads stock from `BranchInventory` (not `Product.StockQuantity`)
- [ ] `CompleteAsync` re-validates stock inside transaction, before decrement
- [ ] `BatchDecrementStockAsync` logs a warning if stock would go negative
- [ ] Two simultaneous sales of last-in-stock item → only one succeeds
- [ ] `BranchInventory.Quantity` never goes below 0 when `AllowNegativeStock=false`

---

## P0-4: Fix Double Tax Calculation

### 1) Problem Explanation

Tax is calculated at TWO levels:

1. **`CalculateItemTotals`** computes `item.TaxAmount` from the item's own `TaxRate` (which can be product-specific, e.g., 14% on meat, 0% on bread)
2. **`CalculateOrderTotals`** IGNORES the per-item tax amounts and recalculates `order.TaxAmount` from `order.TaxRate` (the tenant-level default)

This means if Product A has 14% tax and Product B has 0% tax, the order-level calculation applies 14% to the ENTIRE order, overtaxing Product B.

### 2) Why It Is Dangerous

- Customers are overcharged on tax
- Tax reports are incorrect — the business files wrong tax returns
- If audited, the business faces penalties for tax discrepancies

### 3) Files to Modify

| File | Action |
|------|--------|
| `src/KasserPro.Application/Services/Implementations/OrderService.cs` | Fix `CalculateOrderTotals` to sum item-level taxes |

### 4) Implementation Steps

Find `CalculateOrderTotals` (line ~912):

```csharp
    private static void CalculateOrderTotals(Order order)
    {
        // Subtotal = Sum of all item subtotals (Net amounts before tax and before order-level discount)
        order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);
        
        // Apply order-level discount (on subtotal, before tax)
        if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
            order.DiscountAmount = Math.Round(order.Subtotal * (order.DiscountValue.Value / 100m), 2);
        else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
            order.DiscountAmount = Math.Round(order.DiscountValue.Value, 2);
        else
            order.DiscountAmount = 0;
        
        // Ensure discount doesn't exceed subtotal
        if (order.DiscountAmount > order.Subtotal)
            order.DiscountAmount = order.Subtotal;
        
        // Calculate amount after discount (before tax)
        var afterDiscount = order.Subtotal - order.DiscountAmount;
        
        // Calculate tax on the amount after discount
        // Tax Exclusive: Tax is calculated on (Subtotal - Discount)
        order.TaxAmount = Math.Round(afterDiscount * (order.TaxRate / 100m), 2);
        
        // Calculate service charge (on subtotal after discount)
        order.ServiceChargeAmount = Math.Round(afterDiscount * (order.ServiceChargePercent / 100m), 2);
        
        // Total = (Subtotal - Discount) + Tax + Service Charge
        order.Total = Math.Round(afterDiscount + order.TaxAmount + order.ServiceChargeAmount, 2);
        order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
    }
```

Replace the entire method with:

```csharp
    private static void CalculateOrderTotals(Order order)
    {
        // Subtotal = Sum of all item subtotals (net amounts before item-level tax)
        order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);
        
        // Apply order-level discount (on subtotal, before tax)
        if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
            order.DiscountAmount = Math.Round(order.Subtotal * (order.DiscountValue.Value / 100m), 2);
        else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
            order.DiscountAmount = Math.Round(order.DiscountValue.Value, 2);
        else
            order.DiscountAmount = 0;
        
        if (order.DiscountAmount > order.Subtotal)
            order.DiscountAmount = order.Subtotal;
        
        var afterDiscount = order.Subtotal - order.DiscountAmount;
        
        // P0-4: Tax = SUM of per-item taxes (respects product-specific tax rates).
        // If there's an order-level discount, scale item taxes proportionally.
        if (order.DiscountAmount > 0 && order.Subtotal > 0)
        {
            // Each item's tax is reduced proportionally by the discount ratio.
            // Example: 10% order discount → each item's taxable amount is 90% of its subtotal.
            var discountRatio = order.DiscountAmount / order.Subtotal;
            order.TaxAmount = Math.Round(order.Items.Sum(item =>
            {
                var itemAfterDiscount = item.Subtotal * (1m - discountRatio);
                return itemAfterDiscount * (item.TaxRate / 100m);
            }), 2);
        }
        else
        {
            // No order-level discount: tax = simple sum of item.TaxAmount
            order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);
        }
        
        // Service charge on net amount after discount
        order.ServiceChargeAmount = Math.Round(afterDiscount * (order.ServiceChargePercent / 100m), 2);
        
        // Total = (Subtotal - Discount) + Tax + Service Charge
        order.Total = Math.Round(afterDiscount + order.TaxAmount + order.ServiceChargeAmount, 2);
        order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
    }
```

### 5) Transaction Boundaries

None. `CalculateOrderTotals` is a pure static function — it modifies the in-memory `Order` object. It's called before `SaveChangesAsync`.

### 6) Edge Cases

- **All items same tax rate, no order discount:** Behaves identically to old code. `sum(item.TaxAmount)` = `subtotal * (rate/100)`.
- **Mixed tax rates (14% + 0%):** Now correctly taxes only the 14% items. Old code would apply 14% to the 0% items too.
- **Order-level discount with mixed rates:** The discount is distributed proportionally. A 10% discount reduces each item's taxable base by 10%.
- **100% discount:** `discountRatio = 1`, tax = 0. Correct.
- **Service charge:** Unchanged — still calculated on `afterDiscount`.

### 7) Manual Validation

1. Create an order with:
   - Item A: price=100, tax rate=14%
   - Item B: price=100, tax rate=0%
2. **Expected (BEFORE fix):** `order.TaxAmount = 200 * 0.14 = 28.00` ← WRONG
3. **Expected (AFTER fix):** `order.TaxAmount = 100 * 0.14 + 100 * 0 = 14.00` ← CORRECT
4. Verify in the database that `order.TaxAmount = 14.00`

With order-level 10% discount:
- `afterDiscount = 200 - 20 = 180`
- Item A taxable = 100 * 0.9 = 90, tax = 90 * 0.14 = 12.60
- Item B taxable = 100 * 0.9 = 90, tax = 90 * 0 = 0
- `order.TaxAmount = 12.60`

### 8) Failure Scenario Simulation

Create 10 orders with varying tax rates and discounts. For each, manually calculate expected tax. Compare with `Orders.TaxAmount` in the database. All should match.

### 9) Definition of Done

- [ ] `CalculateOrderTotals` uses `sum(item.TaxAmount)` instead of `afterDiscount * (order.TaxRate / 100)`
- [ ] Mixed-rate order (14% + 0%) shows correct tax amount
- [ ] Order with discount distributes tax proportionally
- [ ] Order with uniform tax rate produces same result as before

---

## P0-5: Fix SignalR Receipt Broadcast

### 1) Problem Explanation

In `OrdersController.cs` line 152, when an order is completed, the receipt data is broadcast to ALL connected SignalR clients:

```csharp
await _hubContext.Clients.All.SendAsync("PrintReceipt", printCommand);
```

The receipt contains customer names, order details, amounts, and cashier names. Every connected desktop bridge app — even on different POS terminals — receives everything.

### 2) Why It Is Dangerous

For our scope (single branch), the practical risk is lower than in multi-tenant, but:
- If multiple devices are connected (e.g., a kitchen display and a receipt printer), all receive all receipts
- Any device connected to the hub URL receives full financial data
- The `DeviceTestController` test-print also broadcasts to all

### 3) Files to Modify

| File | Action |
|------|--------|
| `src/KasserPro.API/Hubs/DeviceHub.cs` | Add devices to a branch-based SignalR group on connect |
| `src/KasserPro.API/Controllers/OrdersController.cs` | Send to group instead of `Clients.All` |
| `src/KasserPro.API/Controllers/DeviceTestController.cs` | Send to group instead of `Clients.All` |

### 4) Implementation Steps

**Step 1 — `DeviceHub.cs`: Add group management**

In `OnConnectedAsync`, after the line that stores the device connection, add group assignment:

Find:
```csharp
        // Store device connection
        lock (_deviceConnections)
        {
            _deviceConnections[deviceId] = Context.ConnectionId;
        }

        _logger.LogInformation("Device {DeviceId} connected with connection ID {ConnectionId}", 
            deviceId, Context.ConnectionId);

        await base.OnConnectedAsync();
```

Replace with:
```csharp
        // Store device connection
        lock (_deviceConnections)
        {
            _deviceConnections[deviceId] = Context.ConnectionId;
        }

        // P0-5: Add device to a branch group for targeted receipt delivery.
        // Branch ID comes from the X-Branch-Id header (set by desktop bridge config).
        // Default to "branch-default" if not provided.
        var branchId = httpContext.Request.Headers["X-Branch-Id"].ToString();
        var groupName = !string.IsNullOrEmpty(branchId) ? $"branch-{branchId}" : "branch-default";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation("Device {DeviceId} connected to group {Group} with connection ID {ConnectionId}", 
            deviceId, groupName, Context.ConnectionId);

        await base.OnConnectedAsync();
```

In `PrintCompleted`, change broadcast to the sender's group. But since `PrintCompleted` is called by the desktop app itself (to report success), and we don't easily know the branch here, keep `Clients.All` for this status callback (it's just a status, not financial data). Alternatively, simplify: send to caller only:

Find:
```csharp
        // Notify all web clients that print is complete
        await Clients.All.SendAsync("PrintCompleted", eventDto);
```

Replace with:
```csharp
        // Notify the caller that print is complete (no need to broadcast status)
        await Clients.Caller.SendAsync("PrintCompleted", eventDto);
```

**Step 2 — `OrdersController.cs`: Send to branch group**

Find (line ~152):
```csharp
                await _hubContext.Clients.All.SendAsync("PrintReceipt", printCommand);
```

Replace with:
```csharp
                // P0-5: Send receipt only to devices in this branch's group
                var branchId = User.FindFirst("branchId")?.Value ?? "default";
                await _hubContext.Clients.Group($"branch-{branchId}")
                    .SendAsync("PrintReceipt", printCommand);
```

**Step 3 — `DeviceTestController.cs`: Send to a specific group**

Find (line ~71):
```csharp
            await _hubContext.Clients.All.SendAsync("PrintReceipt", command);
```

Replace with:
```csharp
            // P0-5: Send test print to a specific branch group (or all if no branch specified)
            var branchId = Request.Headers["X-Branch-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(branchId))
            {
                await _hubContext.Clients.Group($"branch-{branchId}")
                    .SendAsync("PrintReceipt", command);
            }
            else
            {
                // Fallback for test: send to default group
                await _hubContext.Clients.Group("branch-default")
                    .SendAsync("PrintReceipt", command);
            }
```

### 5) Transaction Boundaries

None. SignalR operations are fire-and-forget relative to the order transaction.

### 6) Edge Cases

- **Desktop bridge doesn't send `X-Branch-Id`:** Device joins `branch-default` group. If `OrdersController` sends to `branch-1`, the device won't receive it. **Fix:** Update the desktop bridge config to include `X-Branch-Id` header.
- **Single branch:** All devices and all orders share `branch-1`, so behavior is effectively the same as `Clients.All` — but contained to a named group.

### 7) Manual Validation

1. Connect a desktop bridge with `X-Branch-Id: 1`
2. Complete an order (logged in as cashier at branch 1)
3. Verify bridge receives the `PrintReceipt` message
4. Connect a second device with `X-Branch-Id: 2` (or no branch header)
5. Complete another order at branch 1
6. Verify the second device does NOT receive the receipt

### 8) Failure Scenario Simulation

1. Remove the `X-Branch-Id` header from the desktop bridge config
2. Complete an order
3. The bridge should NOT receive the receipt (it's in the `branch-default` group, but the order sends to `branch-1`)
4. This confirms the isolation works. Fix the bridge config to include the header.

### 9) Definition of Done

- [ ] `DeviceHub.OnConnectedAsync` assigns devices to `branch-{id}` groups
- [ ] `OrdersController.Complete` sends to `Clients.Group(...)` not `Clients.All`
- [ ] `DeviceTestController` scoped to group
- [ ] `PrintCompleted` sends to `Clients.Caller` only
- [ ] A device in group `branch-2` does NOT receive receipts from `branch-1`

---

## P0-6: Secure DeviceTestController

### 1) Problem Explanation

`DeviceTestController` has no `[Authorize]` attribute. Anyone who knows the URL can send unlimited print commands to all connected devices.

### 2) Why It Is Dangerous

- Anonymous users can spam printers with test receipts
- Denial-of-service: flood the receipt printer with garbage
- In a local network, any device can trigger prints

### 3) Files to Modify

| File | Action |
|------|--------|
| `src/KasserPro.API/Controllers/DeviceTestController.cs` | Add `[Authorize(Roles = "Admin")]` |

### 4) Implementation Steps

Find:
```csharp
[ApiController]
[Route("api/[controller]")]
public class DeviceTestController : ControllerBase
```

Replace with:
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DeviceTestController : ControllerBase
```

Add the using statement at the top of the file if not present:
```csharp
using Microsoft.AspNetCore.Authorization;
```

### 5) Transaction Boundaries

None.

### 6) Edge Cases

- **`/status` endpoint also locked behind Admin:** This is intentional. Device status is an operational admin concern. If you want cashiers to see device status, split the controller into two: public status + admin-only print test.
- **Swagger testing:** When testing via Swagger, you need to add the JWT Bearer token.

### 7) Manual Validation

```powershell
# Without token — should get 401:
curl -X POST http://localhost:5000/api/devicetest/test-print
# Expected: 401 Unauthorized

# With Cashier token — should get 403:
curl -X POST http://localhost:5000/api/devicetest/test-print -H "Authorization: Bearer $cashierToken"
# Expected: 403 Forbidden

# With Admin token — should succeed:
curl -X POST http://localhost:5000/api/devicetest/test-print -H "Authorization: Bearer $adminToken"
# Expected: 200 OK
```

### 8) Failure Scenario Simulation

1. From a machine on the same network, run `curl -X POST http://<pos-ip>:5000/api/devicetest/test-print`
2. Verify 401 response
3. Verify no print command is sent to any device

### 9) Definition of Done

- [ ] `DeviceTestController` has `[Authorize(Roles = "Admin")]` at class level
- [ ] Unauthenticated request → 401
- [ ] Cashier request → 403
- [ ] Admin request → 200

---

## P0-7: Disable Retry on Financial Mutations

### 1) Problem Explanation

`baseApi.ts` wraps ALL API calls in RTK Query's `retry()` with `maxRetries: 3`. For `500` and `FETCH_ERROR` responses, the retry happens automatically. This means if a payment POST gets a 500 (server processed it but errored on response), the frontend retries — potentially double-charging.

Additionally, the idempotency keys in `ordersApi.ts` include `Date.now()` which changes on each retry attempt, so the idempotency protection is useless:

```typescript
"Idempotency-Key": `order-${Date.now()}-${Math.random().toString(36).substring(7)}`
```

### 2) Why It Is Dangerous

- Order completion: retried POST creates a second completed order → customer charged twice
- Refund: retried POST refunds twice → double cash deducted from register
- Cash register deposit/withdrawal: retried → doubled

### 3) Files to Modify

| File | Action |
|------|--------|
| `client/src/api/baseApi.ts` | Stop retrying mutations (POST/PUT/DELETE) |
| `client/src/api/ordersApi.ts` | Remove `Idempotency-Key` headers (server doesn't enforce them properly, and they change per retry anyway — removing avoids false confidence) |
| `client/src/api/cashRegisterApi.ts` | No changes needed (already has no idempotency keys, but now won't retry) |

### 4) Implementation Steps

**Step 1 — `baseApi.ts`: Don't retry mutations**

Find the `baseQueryWithReauth` function. In the error handling block, BEFORE the `FETCH_ERROR` check, add a mutation detection guard.

Find:
```typescript
const baseQueryWithReauth = retry(
  async (args, api, extraOptions) => {
    const result = await baseQuery(args, api, extraOptions);

    if (result.error) {
      const error = result.error as FetchBaseQueryError;

      // Network error (offline) - retry
      if (error.status === "FETCH_ERROR") {
        toast.error("لا يوجد اتصال بالإنترنت");
        // Retry will happen automatically
        return result;
      }

      // Timeout error - retry
      if (error.status === "TIMEOUT_ERROR") {
        toast.error("انتهت مهلة الاتصال، حاول مرة أخرى");
        // Retry will happen automatically
        return result;
      }
```

Replace with:
```typescript
const baseQueryWithReauth = retry(
  async (args, api, extraOptions) => {
    const result = await baseQuery(args, api, extraOptions);

    if (result.error) {
      const error = result.error as FetchBaseQueryError;

      // P0-7: NEVER retry mutations (POST, PUT, DELETE).
      // Retrying a payment or order completion can cause double-charges.
      // Only GET requests (queries) are safe to retry.
      const isMutation =
        typeof args === "object" &&
        args !== null &&
        "method" in args &&
        typeof (args as Record<string, unknown>).method === "string" &&
        ["POST", "PUT", "DELETE"].includes(
          ((args as Record<string, unknown>).method as string).toUpperCase()
        );

      if (isMutation) {
        // Show error but do NOT retry
        if (error.status === "FETCH_ERROR") {
          toast.error("فشل الاتصال. تحقق من الشبكة وحاول يدوياً.");
        } else if (error.status === 500) {
          toast.error("حدث خطأ في الخادم. لا تكرر العملية — تحقق من البيانات أولاً.");
        }
        retry.fail(error);
        return result;
      }

      // --- Below: only applies to GET queries ---

      // Network error (offline) - retry query
      if (error.status === "FETCH_ERROR") {
        toast.error("لا يوجد اتصال بالإنترنت");
        return result;
      }

      // Timeout error - retry query
      if (error.status === "TIMEOUT_ERROR") {
        toast.error("انتهت مهلة الاتصال، حاول مرة أخرى");
        return result;
      }
```

**Step 2 — `ordersApi.ts`: Remove unstable idempotency keys**

Remove the `headers` objects from all mutations. They provide false confidence since they change every call.

In `createOrder`:
```typescript
      query: (order) => ({
        url: "/orders",
        method: "POST",
        body: order,
      }),
```

In `completeOrder`:
```typescript
      query: ({ orderId, data }) => ({
        url: `/orders/${orderId}/complete`,
        method: "POST",
        body: data,
      }),
```

In `cancelOrder`:
```typescript
      query: ({ orderId, reason }) => ({
        url: `/orders/${orderId}/cancel`,
        method: "POST",
        body: { reason },
      }),
```

In `refundOrder`:
```typescript
      query: ({ orderId, reason, items }) => ({
        url: `/orders/${orderId}/refund`,
        method: "POST",
        body: { reason, items },
      }),
```

### 5) Transaction Boundaries

None. Frontend-only change.

### 6) Edge Cases

- **Server processes request, response lost (network drop):** The user sees an error. They should check the orders list before retrying manually. The error toast now says "لا تكرر العملية — تحقق من البيانات أولاً" (don't repeat — check data first).
- **Actual server failure (500 before processing):** The user sees an error and can safely retry manually — the order was never created.
- **GET requests still retry:** A failing `GET /api/orders` will retry up to 3 times. This is safe.

### 7) Manual Validation

1. Open browser DevTools → Network tab
2. Enable throttling: "Offline" AFTER clicking Pay
3. Wait for the error toast
4. Check that only ONE request was sent (no retries)
5. Go back online, refresh orders list — verify order was or wasn't created

### 8) Failure Scenario Simulation

1. Complete an order successfully
2. With DevTools, add a breakpoint in the payment handler after the API call
3. Kill the backend server while the request is in flight
4. Verify the frontend shows an error toast and does NOT retry
5. Start the server again
6. Check the database — the order should be in one of: completed (if server processed before dying) or draft (if server died before processing)

### 9) Definition of Done

- [ ] `POST`, `PUT`, `DELETE` requests are never auto-retried by RTK Query
- [ ] `GET` requests still retry up to 3 times
- [ ] `Idempotency-Key` headers removed from `ordersApi.ts` mutations
- [ ] Error toast for mutations says "لا تكرر العملية"
- [ ] Double-click or network failure does NOT produce duplicate API calls

---

## P0-8: Cash Register Concurrency Guard

### 1) Problem Explanation

`RecordTransactionAsync` in `CashRegisterService.cs` does:
1. READ the last balance (`GetCurrentBalanceForBranchAsync`)
2. WRITE a new row with the new balance

There is no transaction between the read and write. If two sales complete at the same time, both read the same balance, both compute their new balance independently, and the second write overwrites the first's balance.

Example:
```
Balance = 1000
Cashier 1 reads 1000, calculates 1000 + 100 = 1100, writes
Cashier 2 reads 1000, calculates 1000 + 200 = 1200, writes
Final balance = 1200 (should be 1300)
```

### 2) Why It Is Dangerous

- Cash register balance is permanently wrong
- End-of-day reconciliation fails
- Over time, the error compounds with every concurrent transaction

### 3) Why SQLite's Write Lock Is Sufficient for ≤3 Users

SQLite allows only ONE writer at a time. When `CompleteAsync` calls `await _unitOfWork.BeginTransactionAsync()`, it acquires a write lock. Any other write request blocks until the lock is released.

For our scope (1–3 cashiers, single SQLite file), this means:
- Cashier 1's entire `CompleteAsync` transaction (including `RecordTransactionAsync`) runs atomically
- Cashier 2 waits 50–200ms for the lock, then runs their transaction against the UPDATED balance

The problem today is that `RecordTransactionAsync` does NOT run inside the caller's transaction properly — it calls `SaveChangesAsync()` independently, which could flush intermediate state. Let me verify...

Actually, looking at the code carefully: `RecordTransactionAsync` is called from inside `CompleteAsync`'s transaction. It calls `_unitOfWork.SaveChangesAsync()`, which writes to the DbContext. Because the same `AppDbContext` instance is shared (scoped DI), this write participates in `CompleteAsync`'s transaction. The data is only committed when `CompleteAsync` calls `transaction.CommitAsync()`.

**So the read is the real issue:** `GetCurrentBalanceForBranchAsync` reads the last committed balance. If another transaction committed between our transaction start and this read, we get the correct (updated) balance because SQLite's write lock serializes the transactions.

**BUT**: `RecordTransactionAsync` is also called from `CashRegisterController` standalone operations (deposit, withdrawal) which have their OWN `BeginTransactionAsync()`. If `RecordTransactionAsync` were called outside ANY transaction (some future code path), the read-then-write would be unprotected.

**The fix:** Ensure `RecordTransactionAsync` always operates within a transaction. Since it's always called from within one today, we just need to add defensive code for the standalone path.

### 4) Files to Modify

| File | Action |
|------|--------|
| `src/KasserPro.Application/Services/Implementations/CashRegisterService.cs` | Wrap `RecordTransactionAsync` in a transaction if none exists |
| `src/KasserPro.Application/Common/Interfaces/IUnitOfWork.cs` | Add `HasActiveTransaction` property |
| `src/KasserPro.Infrastructure/Repositories/UnitOfWork.cs` | Implement `HasActiveTransaction` |

### 5) Implementation Steps

**Step 1 — `IUnitOfWork.cs`: Add property**

Find:
```csharp
    Task<int> SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
```

Replace with:
```csharp
    Task<int> SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
    
    /// <summary>
    /// P0-8: True if a database transaction is currently active on this context.
    /// Used by RecordTransactionAsync to avoid nested transaction errors.
    /// </summary>
    bool HasActiveTransaction { get; }
```

**Step 2 — `UnitOfWork.cs`: Implement property**

Find:
```csharp
    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public async Task<IDbContextTransaction> BeginTransactionAsync() 
        => await _context.Database.BeginTransactionAsync();
```

Replace with:
```csharp
    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public async Task<IDbContextTransaction> BeginTransactionAsync() 
        => await _context.Database.BeginTransactionAsync();
    
    // P0-8: Check if a transaction is already in progress
    public bool HasActiveTransaction => _context.Database.CurrentTransaction != null;
```

**Step 3 — `CashRegisterService.cs` / `RecordTransactionAsync`: Add transaction guard**

Replace the entire `RecordTransactionAsync` method:

```csharp
    public async Task RecordTransactionAsync(
        CashRegisterTransactionType type,
        decimal amount,
        string description,
        string? referenceType = null,
        int? referenceId = null,
        int? shiftId = null)
    {
        // P0-8: If we're already inside a caller's transaction (e.g., CompleteAsync),
        // piggyback on it. If not, create our own to ensure read+write atomicity.
        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        IDbContextTransaction? transaction = null;
        
        if (ownsTransaction)
        {
            transaction = await _unitOfWork.BeginTransactionAsync();
        }
        
        try
        {
            // Read current balance — inside transaction, so SQLite write lock protects us
            var currentBalance = await GetCurrentBalanceForBranchAsync(_currentUserService.BranchId);

            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            var transactionNumber = await GenerateTransactionNumberAsync();

            var balanceAfter = type switch
            {
                CashRegisterTransactionType.Sale => currentBalance + amount,
                CashRegisterTransactionType.Deposit => currentBalance + amount,
                CashRegisterTransactionType.Opening => amount,
                CashRegisterTransactionType.Refund => currentBalance - amount,
                CashRegisterTransactionType.Withdrawal => currentBalance - amount,
                CashRegisterTransactionType.Expense => currentBalance - amount,
                CashRegisterTransactionType.SupplierPayment => currentBalance - amount,
                CashRegisterTransactionType.Adjustment => currentBalance + amount,
                _ => currentBalance
            };

            var cashTransaction = new CashRegisterTransaction
            {
                TenantId = _currentUserService.TenantId,
                BranchId = _currentUserService.BranchId,
                TransactionNumber = transactionNumber,
                Type = type,
                Amount = amount,
                BalanceBefore = currentBalance,
                BalanceAfter = balanceAfter,
                TransactionDate = DateTime.UtcNow,
                Description = description,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                ShiftId = shiftId,
                UserId = _currentUserService.UserId,
                UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
            };

            await _unitOfWork.CashRegisterTransactions.AddAsync(cashTransaction);
            await _unitOfWork.SaveChangesAsync();
            
            if (ownsTransaction && transaction != null)
            {
                await transaction.CommitAsync();
            }

            _logger.LogInformation("Cash register transaction recorded: {Type} - {Amount}", type, amount);
        }
        catch (Exception ex)
        {
            if (ownsTransaction && transaction != null)
            {
                await transaction.RollbackAsync();
            }
            _logger.LogError(ex, "Error recording cash register transaction");
            throw;
        }
        finally
        {
            if (ownsTransaction)
            {
                transaction?.Dispose();
            }
        }
    }
```

### 6) Edge Cases

- **Called from `CompleteAsync`:** `HasActiveTransaction = true` → no new transaction created. Read+write happens within `CompleteAsync`'s transaction. SQLite write lock protects the balance read.
- **Called from `CashRegisterController.CreateTransactionAsync`:** That method already creates its own transaction via `_unitOfWork.BeginTransactionAsync()`. So `HasActiveTransaction = true` when `RecordTransactionAsync` is called internally. Wait — `CreateTransactionAsync` does NOT call `RecordTransactionAsync`. It has its own inline logic. So this path doesn't apply.
- **Called standalone (future code):** `HasActiveTransaction = false` → creates own transaction. Safe.
- **3 concurrent cashiers:** SQLite serializes all writes. Max wait time ≈ 3 × 100ms = 300ms. Acceptable for ≤3 users.

### 7) Manual Validation

**Concurrent cash transaction test:**

1. Open two terminals (browser tabs), both logged in
2. Both have the cash register page open
3. Cashier 1 completes a 100 EGP cash sale
4. Cashier 2 completes a 200 EGP cash sale simultaneously
5. Check cash register balance: should be `initial + 300` (not `initial + 200`)

```powershell
# Database check:
sqlite3 kasserpro.db "SELECT BalanceBefore, Amount, BalanceAfter FROM CashRegisterTransactions ORDER BY CreatedAt DESC LIMIT 2;"
# Row 1: Before=X+100, Amount=200, After=X+300
# Row 2: Before=X,     Amount=100, After=X+100
# (or vice versa, depending on who committed first)
```

### 8) Failure Scenario Simulation

1. Add a `Task.Delay(5000)` inside `RecordTransactionAsync` right after reading the balance (temporarily, for testing)
2. Trigger two sales simultaneously
3. Both should complete correctly because SQLite serializes the writes
4. Remove the delay

### 9) Definition of Done

- [ ] `IUnitOfWork` has `HasActiveTransaction` property
- [ ] `UnitOfWork` implements it via `_context.Database.CurrentTransaction != null`
- [ ] `RecordTransactionAsync` creates its own transaction only if none is active
- [ ] Two simultaneous cash sales produce correct cumulative balance
- [ ] `BalanceBefore` of transaction N+1 equals `BalanceAfter` of transaction N

---

## Final Validation Checklist

Run ALL of these tests after implementing all 8 P0 fixes. Each test should pass independently.

### Test 1: Multi-Tab Oversell Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | Set Product X stock to 1 | — |
| 2 | Open Tab A: add 1x Product X, go to payment | Draft created |
| 3 | Open Tab B: add 1x Product X, go to payment | Draft created |
| 4 | Tab A: click Pay | ✅ Order completed, stock = 0 |
| 5 | Tab B: click Pay | ❌ "المخزون تغير أثناء إتمام الطلب" |
| 6 | Check database | `BranchInventory.Quantity = 0`, NOT -1 |

### Test 2: Double-Click Payment Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | Create order for 50 EGP | Draft created |
| 2 | On payment screen, rapidly double-click "اتمام الدفع" | — |
| 3 | Check Network tab in DevTools | Only 1 POST request sent (due to mutation lock) |
| 4 | If 2 requests sent (fast click before response) | Only 1 order completed (no retry on mutation error) |
| 5 | Check database | Exactly 1 completed order, no duplicates |

### Test 3: Server Restart Mid-Operation Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | Create order, go to payment screen | Draft exists in DB |
| 2 | Kill the backend process (`Ctrl+C`) | — |
| 3 | Click Pay on frontend | Error toast: "فشل الاتصال" |
| 4 | Verify NO retry happens (one request only) | — |
| 5 | Restart backend | — |
| 6 | Refresh frontend, check orders | Draft still exists, not completed |
| 7 | Complete the order manually | ✅ Works normally |

### Test 4: Printer Disconnect Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | Disconnect the desktop bridge app (or don't start it) | No device connected |
| 2 | Complete an order | ✅ Order completes successfully |
| 3 | Check logs | "Failed to send print command" logged as warning |
| 4 | Receipt is NOT printed | Expected — no device |
| 5 | Order and payment are persisted in database | ✅ Data safe |

The print failure is caught by the `try/catch` in `OrdersController.Complete`:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to send print command for order {OrderId}", id);
    // Don't fail the request if printing fails
}
```

### Test 5: Concurrent Cash Transaction Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | Note current cash register balance (e.g., 1000) | — |
| 2 | Cashier A: complete 100 EGP cash sale | — |
| 3 | Cashier B: complete 200 EGP cash sale (start before A finishes) | — |
| 4 | Check balance | 1300 EGP (1000 + 100 + 200) |
| 5 | Check transaction chain | B's `BalanceBefore` = A's `BalanceAfter` |

```sql
-- Database verification:
SELECT TransactionNumber, Type, Amount, BalanceBefore, BalanceAfter 
FROM CashRegisterTransactions 
ORDER BY CreatedAt DESC 
LIMIT 3;
```

### Test 6: JWT Secret Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | Unset `Jwt__Key` env var | — |
| 2 | Start backend | ❌ App crashes with clear error message |
| 3 | Set `Jwt__Key` to "short" | ❌ App crashes (< 32 chars) |
| 4 | Set `Jwt__Key` to valid 32+ char string | ✅ App starts |

### Test 7: Production Build Security Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | `cd client && npm run build` | Build succeeds |
| 2 | `grep -r "Admin@123" dist/` (or `Select-String`) | 0 matches |
| 3 | `grep -r "ahmed@kasserpro" dist/` | 0 matches |
| 4 | Set `ASPNETCORE_ENVIRONMENT=Production`, start backend | No seeding happens |

### Test 8: DeviceTestController Auth Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | `POST /api/devicetest/test-print` without token | 401 |
| 2 | `POST /api/devicetest/test-print` with Cashier token | 403 |
| 3 | `POST /api/devicetest/test-print` with Admin token | 200 |

### Test 9: Tax Calculation Accuracy Test

| Step | Action | Expected |
|------|--------|----------|
| 1 | Create product A: price=100, tax=14% | — |
| 2 | Create product B: price=100, tax=0% | — |
| 3 | Create order with 1x A + 1x B | — |
| 4 | Check `order.TaxAmount` | 14.00 (not 28.00) |
| 5 | Check `order.Total` | 214.00 (200 + 14 tax) |

---

## Summary of All Changes

| Fix | Backend Files | Frontend Files | Migration |
|-----|--------------|----------------|-----------|
| P0-1 | `appsettings.json`, `appsettings.example.json`, `Program.cs` | — | No |
| P0-2 | `Program.cs` | `LoginPage.tsx` | No |
| P0-3 | `OrderService.cs`, `InventoryService.cs` | — | No |
| P0-4 | `OrderService.cs` | — | No |
| P0-5 | `DeviceHub.cs`, `OrdersController.cs`, `DeviceTestController.cs` | — | No |
| P0-6 | `DeviceTestController.cs` | — | No |
| P0-7 | — | `baseApi.ts`, `ordersApi.ts` | No |
| P0-8 | `CashRegisterService.cs`, `IUnitOfWork.cs`, `UnitOfWork.cs` | — | No |

**Total:** 10 backend files, 3 frontend files, 0 migrations.

**Estimated effort:** 8–12 hours for implementation + testing.

**Recommended order:**
1. P0-1 (JWT) — everything depends on being able to start the app
2. P0-6 (DeviceTestController) — 2-minute fix
3. P0-2 (Seed/demo creds) — quick, high impact
4. P0-7 (Frontend retry) — frontend-only, low risk
5. P0-4 (Tax) — standalone, testable independently
6. P0-5 (SignalR) — standalone
7. P0-8 (Cash register) — needs IUnitOfWork change
8. P0-3 (Stock TOCTOU) — uses the IUnitOfWork change from P0-8, most complex

---

*End of guide. Each fix is independently implementable. Test after each fix before moving to the next.*
