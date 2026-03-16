# FULL REPORTING SYSTEM AUDIT

## KasserPro POS — Senior Architect & Financial Auditor Report

**Date:** 2026-03-12  
**Auditor Role:** Senior Software Architect, Financial Systems Auditor, .NET Expert  
**Scope:** All 7 backend report services, ShiftService, OrderService (refund logic), frontend report pages, DB indexes, test coverage

---

## ██ STEP 1 — DATA MODEL ANALYSIS

### Entities & Relationships

| Entity        | Key Financial Fields                                                                                   | Relationships                            |
| ------------- | ------------------------------------------------------------------------------------------------------ | ---------------------------------------- |
| **Order**     | Subtotal, DiscountAmount, TaxAmount, Total, AmountPaid, AmountDue, ChangeAmount, RefundAmount          | → Items[], Payments[], Shift?, Customer? |
| **OrderItem** | UnitPrice, UnitCost (snapshot), Quantity, DiscountAmount, TaxAmount, Subtotal, Total, RefundedQuantity | → Order, Product?                        |
| **Payment**   | Method (enum), Amount                                                                                  | → Order                                  |
| **Product**   | Price, Cost, AverageCost, LastPurchasePrice                                                            | → OrderItems[], Category                 |
| **Customer**  | TotalOrders, TotalSpent, TotalDue (denormalized)                                                       | → Orders[]                               |
| **Shift**     | OpeningBalance, ClosingBalance, ExpectedBalance, Difference, TotalCash, TotalCard, TotalOrders         | → Orders[], User                         |
| **User**      | Name, Role                                                                                             | → Orders[], Shifts[]                     |

### Enums

| Enum              | Values                                                                                    |
| ----------------- | ----------------------------------------------------------------------------------------- |
| **OrderStatus**   | Draft=0, Pending=1, **Completed=2**, Cancelled=3, **Refunded=4**, **PartiallyRefunded=5** |
| **OrderType**     | DineIn=0, Takeaway=1, Delivery=2, **Return=3**                                            |
| **PaymentMethod** | **Cash=0**, **Card=1**, **Fawry=2**, **BankTransfer=3**                                   |

### Financial Snapshot Model

**VERIFIED CORRECT.** OrderItem stores snapshots at order time:

- `UnitPrice` — price snapshot (immutable after order)
- `UnitCost` — cost snapshot (nullable, from Product.Cost at order time)
- `OriginalPrice` — price before discount
- `DiscountAmount`, `TaxAmount`, `Subtotal`, `Total` — all computed and frozen

### Return Order Data Model

**CRITICALLY IMPORTANT for report auditing:**
| Field | Return Order Value |
|-------|-------------------|
| `Order.Total` | **NEGATIVE** (e.g., -50.00) |
| `Order.Subtotal` | **NEGATIVE** |
| `Order.DiscountAmount` | **NEGATIVE** |
| `OrderItem.UnitPrice` | **NEGATIVE** |
| `OrderItem.Quantity` | **POSITIVE** (e.g., 2, NOT -2) |
| `OrderItem.Total` | **NEGATIVE** |
| `Payment.Amount` | **NEGATIVE** (e.g., -50.00) |
| `Order.OrderType` | `Return = 3` |
| `Order.OriginalOrderId` | Points to original order |

### Nullable Fields Risk Assessment

| Field                 | Nullable?          | Risk                                                                    |
| --------------------- | ------------------ | ----------------------------------------------------------------------- |
| `OrderItem.ProductId` | Yes (custom items) | ✅ Reports filter `oi.Product != null` or `oi.ProductId.HasValue`       |
| `OrderItem.UnitCost`  | Yes                | ⚠️ Some products may not have cost data. `?? 0` fallback means COGS = 0 |
| `Order.CompletedAt`   | Yes                | ✅ Reports filter on it; only completed orders have it                  |
| `Order.CustomerId`    | Yes                | ✅ Customer reports filter `o.CustomerId != null`                       |
| `Order.ShiftId`       | Yes                | ⚠️ Orders without a shift won't appear in shift-based reports           |

### Status Transitions (verified from OrderService)

```
Draft → Pending → Completed → PartiallyRefunded → Refunded (terminal)
                            → Refunded (terminal)
                → Cancelled (terminal)
```

**VERIFIED:** `PartiallyRefunded` can transition to `PartiallyRefunded` (another partial) or `Refunded` (final).

---

## ██ STEP 2 — FINANCIAL INVARIANT VALIDATION

### 1️⃣ Revenue Calculation

**Formula expected:**

```
Revenue = SUM(OrderItems.Total)  [which is UnitPrice × Qty - ItemDiscount + Tax]
```

**P&L Report (FinancialReportService):**

```csharp
var grossSales = orders.Sum(o => o.Subtotal);  // SUM of pre-tax/discount subtotals
var totalItemDiscounts = orders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
var totalOrderDiscounts = orders.Sum(o => o.DiscountAmount);
var totalDiscount = totalItemDiscounts + totalOrderDiscounts;
var netSales = grossSales - totalDiscount;
var totalRevenue = orders.Sum(o => o.Total);
```

✅ **PASS** — Both item-level AND order-level discounts are included.

**⚠️ FINDING M-1: Revenue Concept Inconsistency**

The P&L report uses `grossSales = Subtotal` and derives `netSales = Subtotal - allDiscounts`, then separately calculates `totalRevenue = Sum(Total)`. The relationship between these depends on the tax model:

- If Tax Exclusive: `Total = Subtotal - Discount + Tax`. So `totalRevenue ≠ netSales` and `totalRevenue = netSales + Tax`.
- The report uses `actualTotalRevenue` (Total-based) for net profit margin but `actualNetSales` (Subtotal-based) for gross profit margin. This is **semantically correct** accounting but could confuse if someone expects consistency.

**VERDICT:** Revenue calculation is **CORRECT**.

---

### 2️⃣ Cost of Goods Sold (COGS)

**Requirement:** Must use `OrderItem.UnitCost`, NOT `Product.Cost`.

| Service                | Method     | Uses UnitCost?                     | Status |
| ---------------------- | ---------- | ---------------------------------- | ------ |
| FinancialReportService | P&L        | `i.UnitCost ?? 0` × `i.Quantity`   | ✅     |
| ProductReportService   | Movement   | `oi.UnitCost ?? 0` × `oi.Quantity` | ✅     |
| ProductReportService   | Profitable | `oi.UnitCost ?? 0` × `oi.Quantity` | ✅     |
| ProductReportService   | COGS       | `oi.UnitCost ?? 0` × `oi.Quantity` | ✅     |
| ReportService          | Sales      | `i.UnitCost ?? 0` × `i.Quantity`   | ✅     |

**⚠️ FINDING L-1: SlowMoving Stock Valuation**

`ProductReportService.GetSlowMovingProductsReportAsync` uses:

```csharp
var stockValue = currentStock * (product.Cost ?? product.AverageCost ?? product.Price);
```

This is for **inventory valuation** (not COGS), so using live product cost is acceptable for current stock value. However, it means stock value can change when product cost changes. This is standard practice for inventory reporting.

**VERDICT:** COGS calculation is **CORRECT** everywhere.

---

### 3️⃣ Returns Handling

**Requirement:** `Net Revenue = Sales - Returns` everywhere.

Return orders have **NEGATIVE** amounts (Total, Subtotal, Payment.Amount, OrderItem.UnitPrice, OrderItem.Total). Quantity is **POSITIVE**.

| Service                | Method                   | Separates Returns?                                   | Nets Correctly?                                      | Status |
| ---------------------- | ------------------------ | ---------------------------------------------------- | ---------------------------------------------------- | ------ |
| FinancialReportService | P&L                      | ✅ `OrderType != Return` + separate query            | ✅ `actualNetSales = netSales - refundsAmount`       | ✅     |
| ProductReportService   | Movement                 | ✅ Separate `returnItems` query                      | ✅ `qtySold = sold - Abs(returned)`                  | ✅     |
| ProductReportService   | Profitable               | ✅ `returnItemsProfit` + `returnsByProduct` dict     | ✅ `qty -= ret.Qty; revenue -= ret.Revenue`          | ✅     |
| ProductReportService   | COGS                     | ✅ `returnItemsCogs` + `returnsByCat`                | ✅ `totalRevenue = items.Total - Abs(returns.Total)` | ✅     |
| ReportService          | Daily                    | ✅ `returnOrders` separated                          | ✅ `actualTotalSales = totalSales - totalRefunds`    | ✅     |
| ReportService          | Sales                    | ✅ `returnOrders` separated                          | ✅ `totalSales = grossSales - totalRefunds`          | ✅     |
| EmployeeReportService  | CashierPerf              | ✅ `returnOrdersCashier` separated                   | ✅ `totalRevenue = sales - Abs(returns)`             | ✅     |
| EmployeeReportService  | SalesByEmp               | ✅ `returnOrdersEmp` + `returnsByUser` dict          | ✅ `empRevenue -= empRefunds`                        | ✅     |
| CustomerReportService  | TopCustomers             | ✅ `returnOrdersCustomer` + `returnsByCustomer` dict | ✅ `spent -= custRefunds`                            | ✅     |
| ShiftService           | CalculateShiftFinancials | ✅ `salesOrders` / `returnOrders` separated          | ✅ `totalCash = salesCash - Abs(returnCash)`         | ✅     |

**⚠️ FINDING M-2: Math.Abs() Inconsistency with Negative Amounts**

Since return order amounts are already **NEGATIVE**, using `Math.Abs()` on them converts them to positive for subtraction. This is done consistently, but there's a subtle risk: if a return order ever had a positive Total (a data integrity violation), `Math.Abs()` would hide it. However, this is defensive coding and acceptable.

**⚠️ FINDING H-1: EmployeeReportService.DetailedShifts Does NOT Separate Returns**

In `GetDetailedShiftsReportAsync`, the shift detail payments query includes ALL order payments:

```csharp
var shiftPayments = (s.Orders ?? new List<Order>())
    .Where(o => o.Status == OrderStatus.Completed
             || o.Status == OrderStatus.PartiallyRefunded
             || o.Status == OrderStatus.Refunded)
    .SelectMany(o => o.Payments ?? new List<Payment>())
    .ToList();
var shiftCash = shiftPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
```

Since return order Payment.Amount is **NEGATIVE**, and this `.Sum()` includes all orders (sales + returns), return payments automatically reduce the totals. **This actually works correctly** because negative payments self-subtract. ✅ But it obscures the logic — it's correct by coincidence of data model, not by explicit design.

**Same pattern in ReportService.GetDailyReportAsync (shift summaries):** Also sums all payments including negative return payments. Works correctly due to negative amounts.

**VERDICT:** Returns handling is **CORRECT** across all services.

---

### 4️⃣ Order Status Filtering

**Requirement:** Reports must include `Completed`, `PartiallyRefunded`, AND `Refunded`.

| Service                | Method                   | Filter                                                         | Status |
| ---------------------- | ------------------------ | -------------------------------------------------------------- | ------ |
| FinancialReportService | P&L (sales)              | `Completed \|\| PartiallyRefunded \|\| Refunded` + `!= Return` | ✅     |
| FinancialReportService | P&L (returns)            | Same 3 statuses + `== Return`                                  | ✅     |
| ProductReportService   | Movement (sales)         | Same 3 + `!= Return`                                           | ✅     |
| ProductReportService   | Movement (returns)       | Same 3 + `== Return`                                           | ✅     |
| ProductReportService   | Profitable (sales)       | Same 3 + `!= Return`                                           | ✅     |
| ProductReportService   | Profitable (returns)     | Same 3 + `== Return`                                           | ✅     |
| ProductReportService   | SlowMoving               | Same 3 + `!= Return`                                           | ✅     |
| ProductReportService   | COGS (sales)             | Same 3 + `!= Return`                                           | ✅     |
| ProductReportService   | COGS (returns)           | Same 3 + `== Return`                                           | ✅     |
| ReportService          | Daily                    | Same 3 (from shifts → orders)                                  | ✅     |
| ReportService          | Sales                    | Same 3                                                         | ✅     |
| EmployeeReportService  | CashierPerf              | Same 3 (from shifts → orders)                                  | ✅     |
| EmployeeReportService  | DetailedShifts           | Same 3                                                         | ✅     |
| EmployeeReportService  | SalesByEmp               | Same 3                                                         | ✅     |
| CustomerReportService  | TopCustomers             | Same 3 + `!= Return`                                           | ✅     |
| CustomerReportService  | Activity                 | Same 3 + `!= Return`                                           | ✅     |
| ShiftService           | CalculateShiftFinancials | Same 3                                                         | ✅     |

**❌ FINDING H-2: CustomerDebtsReport Uses Only Completed**

`CustomerReportService.GetCustomerDebtsReportAsync`:

```csharp
var lastOrder = await _context.Orders
    .Where(o => o.TenantId == tenantId
             && o.CustomerId == customer.Id
             && o.Status == OrderStatus.Completed)  // ❌ MISSING PartiallyRefunded, Refunded
    .OrderByDescending(o => o.CompletedAt)
    .FirstOrDefaultAsync();

var oldestUnpaidOrder = await _context.Orders
    .Where(o => o.TenantId == tenantId
             && o.CustomerId == customer.Id
             && o.Status == OrderStatus.Completed  // ❌ MISSING
             && o.AmountDue > 0)
    ...

var unpaidOrdersCount = await _context.Orders
    .CountAsync(o => o.TenantId == tenantId
                  && o.CustomerId == customer.Id
                  && o.Status == OrderStatus.Completed  // ❌ MISSING
                  && o.AmountDue > 0);
```

**Impact:** If a customer has a PartiallyRefunded order with remaining `AmountDue > 0`, it won't be counted in the debt report. This understates customer debt and shows wrong "oldest unpaid" dates.

**Severity: HIGH** — Customer debt aging analysis uses wrong data.

---

### 5️⃣ Payment Method Accuracy

**Requirement:** Payments from Payment table, not inferred.

| Service               | Method                   | Source                         | Status |
| --------------------- | ------------------------ | ------------------------------ | ------ |
| EmployeeReportService | CashierPerf              | `o.Payments` (Payment records) | ✅     |
| EmployeeReportService | DetailedShifts           | `o.Payments` (Payment records) | ✅     |
| ReportService         | Daily                    | `o.Payments` (Payment records) | ✅     |
| ReportService         | Daily shift summaries    | `o.Payments` (Payment records) | ✅     |
| ShiftService          | CalculateShiftFinancials | `o.Payments` (Payment records) | ✅     |

**VERDICT:** Payment methods are **CORRECT** — always from Payment records, never inferred.

---

## ██ STEP 3 — SERVICE-BY-SERVICE AUDIT

### 3.1 FinancialReportService

**GetProfitLossReportAsync:**

- ✅ Status filter: 3 statuses
- ✅ Returns: Separated and netted
- ✅ COGS: Uses `UnitCost`
- ✅ Discounts: Both item-level and order-level
- ✅ Expenses: Separate query with status filter

**⚠️ FINDING M-3: P&L AverageOrderValue Uses Unadjusted Count**

```csharp
AverageOrderValue = orders.Count > 0 ? actualTotalRevenue / orders.Count : 0,
```

`orders.Count` is the count of **sales orders only** (Return orders excluded). But `actualTotalRevenue` has been reduced by refunds. This means:

- 10 orders × 100 EGP each = 1000 EGP.
- 2 orders fully refunded → `actualTotalRevenue = 800 EGP`
- `AverageOrderValue = 800 / 10 = 80 EGP` (should arguably be 800 / 8 = 100 EGP)

The original orders still exist as Completed/Refunded, so the count includes them. This is **debatable** — average could be computed either way. Current behavior produces a lower average. Not a bug per se, but worth being aware of.

**GetExpensesReportAsync:**

- ✅ Clean, no financial integrity issues
- ✅ Expense payment method breakdown is correct

---

### 3.2 ProductReportService

**GetProductMovementReportAsync:**

- ✅ All financial invariants hold
- ✅ Return netting per product
- ✅ Purchase and transfer quantities included

**GetProfitableProductsReportAsync:**

- ✅ All financial invariants hold
- ✅ Return netting via `returnsByProduct` dictionary

**GetSlowMovingProductsReportAsync:**

- ✅ Status filter correct
- ✅ Returns excluded (`OrderType != Return`)
- ⚠️ Uses live `Product.Cost` for stock valuation (acceptable for inventory, see L-1)

**GetCogsReportAsync:**

- ✅ All invariants hold
- ⚠️ FINDING M-4: Opening inventory estimation is approximate

```csharp
var openingInventoryValue = closingInventoryValue + totalCost - totalPurchases;
```

This reverse-engineers opening inventory from closing inventory. If there were stock adjustments, wastage, or transfers outside the period, this formula won't be accurate. This is a known limitation of perpetual inventory estimation.

---

### 3.3 CustomerReportService

**GetTopCustomersReportAsync:**

- ✅ Returns netted per customer
- ⚠️ FINDING M-5: N+1 Query for New Customer Detection

```csharp
foreach (var customerId in customerIds)
{
    var firstOrder = await _context.Orders
        .Where(o => o.TenantId == tenantId && o.CustomerId == customerId ...)
        .OrderBy(o => o.CompletedAt)
        .FirstOrDefaultAsync();
```

For each customer, a separate DB query is issued. If there are 200 customers in the period, that's 200 DB roundtrips.

**GetCustomerDebtsReportAsync:**

- ❌ Bug H-2 identified above (missing statuses)
- ⚠️ FINDING M-6: N+1 Query for Customer Debt Details

```csharp
foreach (var customer in customersWithDebt)
{
    var lastOrder = await _context.Orders...
    var oldestUnpaidOrder = await _context.Orders...
    var unpaidOrdersCount = await _context.Orders.CountAsync(...);
```

Three DB queries per customer with debt. With 50 customers in debt, that's 150 DB roundtrips.

**GetCustomerActivityReportAsync:**

- ✅ Status filter correct
- ✅ Customer segments fixed (newCustomerIds / returningCustomerIds HashSets)
- ⚠️ Same N+1 pattern for firstOrder lookup

---

### 3.4 EmployeeReportService

**GetCashierPerformanceReportAsync:**

- ✅ Payment methods from Payment records
- ✅ Returns separated and netted
- ✅ Status filter correct

**GetDetailedShiftsReportAsync:**

- ✅ Payment methods from Payment records
- ✅ Returns automatically netted via negative Payment amounts
- ⚠️ FINDING M-7: BankTransfer Payments Missing from Total

```csharp
TotalSales = shiftCash + shiftCard + shiftFawry,
```

`BankTransfer` payments are NOT included in `TotalSales`. If a shift has bank transfer payments, the shift total will be understated in this report.

**GetSalesByEmployeeReportAsync:**

- ✅ Returns separated and netted per employee
- ⚠️ FINDING M-8: DailyEmployeeSalesDto Does Not Net Returns

```csharp
var dailySales = g
    .GroupBy(o => o.CompletedAt!.Value.Date)
    .Select(d => new DailyEmployeeSalesDto
    {
        Date = d.Key,
        Orders = d.Count(),
        Revenue = d.Sum(o => o.Total)  // ❌ Sales-only, no returns netted
    })
```

The per-day breakdown within the employee sales report does not subtract returns. The employee-level total IS correctly netted, but the daily breakdown within each employee shows gross sales. This inconsistency means daily sub-totals don't sum to the employee total.

---

### 3.5 SupplierReportService

- ✅ No order/financial data used — works with PurchaseInvoice entities
- ✅ Status filter: Excludes cancelled invoices
- ✅ No financial integrity issues
- ⚠️ FINDING M-9: N+1 Query in SupplierDebtsReport

```csharp
foreach (var supplier in suppliersWithDebt)
{
    var unpaidInvoices = await _context.PurchaseInvoices...
    var lastPayment = await _context.PurchaseInvoices...
```

Two DB queries per supplier with debt.

---

### 3.6 ReportService

**GetDailyReportAsync:**

- ✅ All financial invariants hold
- ✅ Returns separated and netted
- ✅ Payment breakdown from Payment records with refund subtraction
- ✅ Top products net returned quantities

**⚠️ FINDING C-1: DailyReport Based on Shift Close Date, Not Order Date**

```csharp
.Where(s => ... && s.IsClosed && s.ClosedAt!.Value.Date == reportDate)
```

The daily report fetches shifts **closed** on the report date, then gets all orders from those shifts. This means:

- If a shift opens Monday and closes Tuesday, ALL Monday orders appear in Tuesday's report
- If a shift is still open (not closed), its orders appear in **NO daily report**
- If a shift is never closed (abandoned), its orders are permanently missing from daily reports

This is by design (shift-based reporting), but it can cause confusion when comparing with order-date-based reports like Sales Report.

**GetSalesReportAsync:**

- ✅ All financial invariants hold
- ✅ Uses `CompletedAt` date (order-based, not shift-based)
- ✅ Daily breakdown includes returns

---

### 3.7 ShiftService

**CloseAsync:**

- ✅ Uses `CalculateShiftFinancials` helper
- ✅ ExpectedBalance from `CashRegisterService.GetCurrentBalanceAsync`
- ✅ Concurrency control via RowVersion

**ForceCloseAsync:**

- ✅ Uses same `CalculateShiftFinancials`
- ✅ ExpectedBalance = OpeningBalance + TotalCash

**CalculateShiftFinancials:**

- ✅ Returns separated from sales
- ✅ Payment methods from Payment records
- ✅ Each method (Cash, Card, Fawry, BankTransfer) computed independently

**⚠️ FINDING M-10: ForceClose ExpectedBalance vs Close ExpectedBalance Differ**

- `CloseAsync`: `shift.ExpectedBalance = currentCashBalance` (from CashRegisterService)
- `ForceCloseAsync`: `shift.ExpectedBalance = shift.OpeningBalance + totalCash`

These use different formulas. The CashRegisterService balance includes manual adjustments (deposits, withdrawals, expense payments). ForceClose ignores those. If a cashier withdrew cash during the shift, ForceClose will show wrong expected balance.

**MapToDto:**

- ✅ Uses `CalculateShiftFinancials` for open shifts (live calculation)
- ✅ Uses stored values for closed shifts (immutable)
- ⚠️ FINDING L-2: `TotalFawry` and `TotalBankTransfer` are **always recomputed** even for closed shifts, because the Shift entity doesn't store them. If orders are deleted/modified after shift close, these values could change. Not a risk if orders are immutable post-close.

---

## ██ STEP 4 — SHIFT FINANCIAL LOGIC (Deep Audit)

**CalculateShiftFinancials helper:**

```csharp
var salesPayments = salesOrders.SelectMany(o => o.Payments).ToList();
var returnPayments = returnOrders.SelectMany(o => o.Payments).ToList();

var totalCash = salesPayments.Where(Cash).Sum() - Abs(returnPayments.Where(Cash).Sum());
var totalCard = salesPayments.Where(Card).Sum() - Abs(returnPayments.Where(Card).Sum());
var totalFawry = salesPayments.Where(Fawry).Sum() - Abs(returnPayments.Where(Fawry).Sum());
var totalBankTransfer = salesPayments.Where(BankTransfer).Sum() - Abs(returnPayments.Where(BankTransfer).Sum());

return (salesOrders.Count, totalCash, totalCard, totalFawry, totalBankTransfer);
```

**Verification:**

✅ `TotalCash = Cash sales - |Cash refunds|` — CORRECT  
✅ `TotalCard = Card-only payments - |Card refunds|` — CORRECT  
✅ `TotalFawry = Fawry payments - |Fawry refunds|` — CORRECT  
✅ `ExpectedBalance = OpeningBalance + TotalCash` (for ForceClose) — CORRECT (only cash affects drawer)  
✅ Returns subtracted per payment method — CORRECT

**⚠️ FINDING M-11: TotalOrders = salesOrders.Count (excludes cancelled)**

`CalculateShiftFinancials` returns `salesOrders.Count` which only counts completed sales orders (not cancelled, not returns). However, `Shift.TotalOrders` is set to this value at close time. Meanwhile, the ShiftDto shows an `Orders` list that includes ALL orders. This count mismatch between `TotalOrders` and `Orders.Length` could confuse users. Not a financial bug.

---

## ██ STEP 5 — HISTORICAL STABILITY

**Requirement:** Changing `Product.Cost` must NOT change historical reports.

| Report                       | Historical Source                      | Uses Live Data? | Status     |
| ---------------------------- | -------------------------------------- | --------------- | ---------- |
| P&L COGS                     | `OrderItem.UnitCost`                   | No              | ✅ STABLE  |
| Product Movement COGS        | `OrderItem.UnitCost`                   | No              | ✅ STABLE  |
| Product Profitability COGS   | `OrderItem.UnitCost`                   | No              | ✅ STABLE  |
| COGS Report                  | `OrderItem.UnitCost`                   | No              | ✅ STABLE  |
| Sales Report COGS            | `OrderItem.UnitCost`                   | No              | ✅ STABLE  |
| SlowMoving StockValue        | `Product.Cost ?? AverageCost ?? Price` | **YES**         | ⚠️ MUTABLE |
| COGS Report ClosingInventory | `Product.Cost ?? AverageCost ?? Price` | **YES**         | ⚠️ MUTABLE |

**SlowMoving** and **COGS closing inventory** use live product costs for **current** inventory valuation. This is expected behavior (you want to know current stock value), not a historical stability bug.

**VERDICT:** Historical stability is **CORRECT** for all COGS calculations. ✅

---

## ██ STEP 6 — PERFORMANCE AUDIT

### Indexes Verified

| Index                                             | Exists?                                | Status   |
| ------------------------------------------------- | -------------------------------------- | -------- |
| `Orders(TenantId, BranchId, Status, CompletedAt)` | ✅ Composite with `IsDeleted=0` filter | Good     |
| `Orders(ShiftId, CreatedAt)`                      | ✅ With `IsDeleted=0` filter           | Good     |
| `Payments(OrderId, Method)`                       | ✅ Composite                           | Good     |
| `OrderItems(ProductId)`                           | ✅ Single column (from FK)             | Adequate |
| `Shifts(UserId, IsClosed, OpenedAt)`              | ✅ Composite with filter               | Good     |

### N+1 Query Problems

| Service               | Method       | N+1 Pattern                           | Severity                               |
| --------------------- | ------------ | ------------------------------------- | -------------------------------------- |
| CustomerReportService | TopCustomers | `foreach customer → firstOrder query` | **HIGH** (200 customers = 200 queries) |
| CustomerReportService | Debts        | `foreach customer → 3 queries`        | **HIGH** (50 × 3 = 150 queries)        |
| CustomerReportService | Activity     | `foreach customer → firstOrder query` | **HIGH**                               |
| SupplierReportService | Debts        | `foreach supplier → 2 queries`        | **MEDIUM** (10 × 2 = 20 queries)       |

### Client-Side Evaluation Risks

All services use `.ToListAsync()` before LINQ grouping/projection. This ensures EF Core doesn't fall back to client-side evaluation. **No client-side eval risk detected.** ✅

### Memory Pressure

`ProductReportService.GetProductMovementReportAsync` loads ALL products + ALL order items + ALL return items + ALL purchase items + ALL transfers into memory, then joins them in C#. For a large catalog (10,000 products, 100,000 order items), this could cause significant memory pressure.

**Severity: MEDIUM** — Scales with data volume.

---

## ██ STEP 7 — TEST COVERAGE

### Existing Tests (107 total)

| Category                        | Tests | Pass Rate                   |
| ------------------------------- | ----- | --------------------------- |
| Unit: OrderFinancialTests       | ~30   | 100% ✅                     |
| Unit: AdversarialFinancialTests | ~59   | 100% ✅                     |
| Integration: ShiftLifecycle     | 4     | 0% (require running server) |
| Integration: OrderCreation      | 2     | 0% (require running server) |

### What IS Tested

- ✅ Tax calculations (Tax Exclusive model, 14% VAT)
- ✅ Discount stacking (item + order level)
- ✅ Refund ratio and proportional calculations
- ✅ Rounding (2 decimal places, MidpointRounding.AwayFromZero)
- ✅ Edge cases (zero amounts, max discounts, full refunds)

### What is NOT Tested ❌

| Missing Test                               | Risk                        | Priority     |
| ------------------------------------------ | --------------------------- | ------------ |
| Report revenue calculations                | Financial misreporting      | **CRITICAL** |
| Report return netting                      | Under/over-stated revenue   | **CRITICAL** |
| Report COGS using UnitCost vs Product.Cost | Historical drift            | **HIGH**     |
| Shift financial calculations               | Cash drawer mismatch        | **HIGH**     |
| Payment method breakdown                   | Wrong payment totals        | **HIGH**     |
| Customer debt with refunded orders         | Overstated/understated debt | **MEDIUM**   |
| Employee daily sales sub-totals            | UI inconsistency            | **LOW**      |

### Suggested Test Cases

```csharp
// 1. P&L Revenue includes PartiallyRefunded orders
[Fact] ProfitLoss_IncludesPartiallyRefundedOrders_RevenueCorrect()

// 2. Product Movement nets returns correctly
[Fact] ProductMovement_WithReturns_NetsQuantityAndRevenue()

// 3. COGS uses snapshot cost, not live
[Fact] CogsReport_ProductCostChanged_UsesSnapshotCost()

// 4. Shift financials separate Card from Fawry
[Fact] ShiftFinancials_MixedPayments_CardExcludesFawry()

// 5. Customer debt report includes PartiallyRefunded orders
[Fact] CustomerDebts_PartiallyRefundedWithDue_IncludedInDebt()

// 6. Employee daily breakdown matches employee total
[Fact] EmployeeSales_DailySumMatchesTotal_WithReturns()
```

---

## ██ STEP 8 — FRONTEND VALIDATION

### Currency Formatting

- ✅ `formatCurrency()` uses `Intl.NumberFormat("ar-EG")` with exactly 2 decimal places
- ✅ `formatPrice()` uses `.toFixed(2)` with "ج.م" suffix
- ✅ Consistent across all report pages

### Number Recalculation

| Page                   | Recalculates? | Source                                               | Status |
| ---------------------- | ------------- | ---------------------------------------------------- | ------ |
| DailyReportPage        | No            | Backend values directly                              | ✅     |
| ProfitLossReportPage   | No            | Backend values directly                              | ✅     |
| SalesReportPage        | No            | Backend values directly                              | ✅     |
| ExpensesReportPage     | No            | Backend values directly                              | ✅     |
| ShiftDetailsReportPage | No            | Backend values directly                              | ✅     |
| OrderSummary (POS)     | **YES**       | Redux selectors, `Math.round(x * 100) / 100`         | ⚠️     |
| CashRegisterDashboard  | Partial       | Backend balance + client-side incoming/outgoing sums | ⚠️     |

### POS Cart Calculations

The cart uses frontend calculations for **live display only**. When the order is submitted to the backend, the backend recalculates independently. The frontend values are for UX preview.

**⚠️ FINDING M-12: Frontend-Backend Rounding Divergence Risk**

Frontend: `Math.round(x * 100) / 100` (IEEE 754 double precision)  
Backend: `Math.Round(x, 2, MidpointRounding.AwayFromZero)` (.NET decimal)

For amounts like `2.675`:

- Frontend: `Math.round(267.5) = 268` → `2.68` (IEEE 754 banker's rounding can vary)
- Backend: `Math.Round(2.675m, 2, AwayFromZero) = 2.68`

In most cases these agree, but edge rounding cases could show a 0.01 difference between the displayed total and the final order total. The backend is authoritative, so no financial impact.

### Percentage Display

```typescript
const percentage = (item.value / total) * 100;
```

Uses JavaScript division. If `total = 0`, this produces `Infinity`. But:

```csharp
var percentage = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
```

Backend guards against division by zero. Frontend percentage is display-only and doesn't feed back to backend.

⚠️ **FINDING L-3: Frontend Division by Zero**

In `DailyReportPage.tsx`:

```typescript
const total = report?.totalSales || 1; // Fallback to 1 prevents Infinity
```

This is a workaround — if `totalSales = 0`, percentages will divide by 1 instead of 0. But this means "0% total cash" becomes a non-zero percentage. Minor display issue.

---

## ██ STEP 9 — SECURITY & MANIPULATION RISKS

### 9.1 Timezone Manipulation

**Backend uses `DateTime.UtcNow`** consistently for:

- `CreatedAt` (BaseEntity default)
- `CompletedAt` (set during order completion)
- `OpenedAt`, `ClosedAt` (shift operations)

**Report date filters use `.Date` (midnight) and `.AddDays(1)` (next midnight)**:

```csharp
o.CompletedAt >= fromDate.Date && o.CompletedAt < toDate.Date.AddDays(1)
```

**⚠️ FINDING M-13: UTC vs Local Time Mismatch**

Egypt timezone is UTC+2. An order completed at 11 PM Egypt time (21:00 UTC on March 11) would be stored as `2026-03-11T21:00:00Z`. When the user requests "March 11 report", the filter is:

```
CompletedAt >= 2026-03-11T00:00:00 AND CompletedAt < 2026-03-12T00:00:00
```

Since `fromDate.Date` strips time to midnight but doesn't specify timezone, and `CompletedAt` is UTC, the 21:00 UTC order IS included. But an order at 11 PM Egypt time on March 11 (22:00 UTC) is also included. However, an order at 1 AM Egypt time on March 12 (23:00 UTC on March 11) would appear in March 11's report, not March 12's.

This is a **2-hour window** where orders are attributed to the wrong day from the user's perspective. Frontend converts dates with `Africa/Cairo` timezone for display, but the backend filter operates on UTC.

**Severity: MEDIUM** — Reports are internally consistent (always UTC-based), but the "March 11" report isn't truly "March 11 Egypt time".

### 9.2 Status Change Manipulation

**Verified:** `OrderService` enforces valid state transitions:

```csharp
{ Completed, [Refunded, PartiallyRefunded] }
{ PartiallyRefunded, [Refunded, PartiallyRefunded] }
{ Cancelled, [] }  // Terminal
{ Refunded, [] }    // Terminal
```

An order cannot go back from Refunded to Completed. Cannot skip states. ✅

### 9.3 Negative Values

- Return orders have negative amounts by design
- `Math.Abs()` used consistently when netting
- `Math.Max(0, ...)` used in DailyReport to prevent negative payment breakdown display
- No path for a user to create a regular order with negative Total

**⚠️ FINDING L-4: Negative Quantity Not Validated**

`OrderItem.Quantity` is declared as `int` (signed). The OrderService validates `Quantity > 0` during creation:

```csharp
if (item.Quantity <= 0) return ApiResponse<OrderDto>.Fail(...);
```

But `RefundedQuantity` is also `int` and could theoretically go negative if there's a bug in refund logic. Currently guarded by:

```csharp
var availableForRefund = orderItem.Quantity - orderItem.RefundedQuantity;
if (refundItem.Quantity > availableForRefund) return Fail(...);
```

This check prevents over-refunding. ✅

### 9.4 Deleted Orders

- Global soft delete filter: `entity.HasQueryFilter(e => !e.IsDeleted)` on ALL entities
- This means deleted orders are **automatically excluded** from ALL EF Core queries
- Reports cannot include deleted data unless explicitly using `.IgnoreQueryFilters()` (which no report does)
- ✅ Safe

### 9.5 Missing Tenant/Branch Filters

Every report service query includes:

```csharp
o.TenantId == tenantId && o.BranchId == branchId
```

Values come from `ICurrentUserService` (JWT claims). Cannot be overridden by user input. ✅

**⚠️ FINDING L-5: BranchId Header Override**

From `OrderCreationFlowTests`, the API supports `X-Branch-Id` header to override branch. If this header is accepted for report endpoints, a user could view reports from another branch. Would need controller-level verification.

### 9.6 Split Payment Edge Case

An order can have multiple Payment records (split payment):

- Cash: 50 EGP
- Card: 50 EGP

Reports sum all payments per method, which correctly handles this. ✅

When this order is refunded, payments are split proportionally:

```csharp
var methodRatio = paymentGroup.Sum() / originalTotalPaid;
var refundForMethod = totalRefundAmount * methodRatio;
```

✅ Proportional split is correct.

### 9.7 Shift Crossing Midnight

**DailyReport**: Based on `Shift.ClosedAt.Date`, not order creation time. A shift opened Monday at 8 AM and closed Tuesday at 2 AM will have ALL its orders in Tuesday's daily report.

**SalesReport**: Based on `Order.CompletedAt`. Independent of shift timing.

This creates a **divergence** between DailyReport and SalesReport totals for the same date range. Not a bug — different perspectives (shift-based vs order-based). But can confuse users.

---

## ██ STEP 10 — FINAL AUDIT REPORT

### 1️⃣ Critical Financial Bugs

| ID  | Bug        | Location | Impact                                          |
| --- | ---------- | -------- | ----------------------------------------------- |
| —   | None found | —        | All critical financial calculations are correct |

### 2️⃣ Data Integrity Issues

| ID  | Issue                                               | Location                             | Impact                                                                | Severity |
| --- | --------------------------------------------------- | ------------------------------------ | --------------------------------------------------------------------- | -------- |
| H-2 | CustomerDebtsReport filters only `Completed` status | CustomerReportService:1025,1032,1038 | PartiallyRefunded orders with AmountDue > 0 excluded from debt report | **HIGH** |

### 3️⃣ Incorrect Calculations

| ID  | Issue                                           | Location                             | Impact                                                 | Severity   |
| --- | ----------------------------------------------- | ------------------------------------ | ------------------------------------------------------ | ---------- |
| M-7 | DetailedShifts TotalSales excludes BankTransfer | EmployeeReportService DetailedShifts | Shift total understated if BankTransfer payments exist | **MEDIUM** |
| M-8 | Employee daily breakdown doesn't net returns    | EmployeeReportService SalesByEmp     | Daily sub-totals don't sum to employee total           | **MEDIUM** |

### 4️⃣ Performance Problems

| ID  | Issue                                   | Location                    | Queries         | Severity   |
| --- | --------------------------------------- | --------------------------- | --------------- | ---------- |
| M-5 | N+1: New customer detection             | CustomerReport TopCustomers | 1 per customer  | **MEDIUM** |
| M-6 | N+1: Customer debt details              | CustomerReport Debts        | 3 per customer  | **MEDIUM** |
| M-9 | N+1: Supplier debt details              | SupplierReport Debts        | 2 per supplier  | **MEDIUM** |
| —   | All Products + Items loaded into memory | ProductReport Movement      | Memory pressure | **LOW**    |

### 5️⃣ Security Risks

| ID   | Risk                                           | Location                  | Severity                     |
| ---- | ---------------------------------------------- | ------------------------- | ---------------------------- |
| M-13 | UTC vs Egypt time 2-hour window                | All date-filtered reports | **MEDIUM**                   |
| L-5  | X-Branch-Id header could access other branches | Controller level          | **LOW** (needs verification) |

### 6️⃣ Missing Tests

| Missing Test Area                     | Risk Level   |
| ------------------------------------- | ------------ |
| Report service financial calculations | **CRITICAL** |
| Report return netting                 | **CRITICAL** |
| Report COGS using UnitCost            | **HIGH**     |
| Shift financial helper                | **HIGH**     |
| Customer debt with refunded orders    | **MEDIUM**   |

### 7️⃣ Best Practice Violations

| ID   | Violation                                                     | Severity             |
| ---- | ------------------------------------------------------------- | -------------------- | --- | ------- |
| M-3  | P&L AverageOrderValue denominator includes refunded orders    | **LOW**              |
| M-4  | COGS opening inventory is reverse-estimated                   | **LOW**              |
| M-10 | ForceClose vs Close use different ExpectedBalance formulas    | **MEDIUM**           |
| M-11 | TotalOrders count vs Orders list length mismatch              | **LOW**              |
| M-12 | Frontend/Backend rounding method difference                   | **LOW**              |
| L-3  | Frontend division-by-zero workaround (`                       |                      | 1`) | **LOW** |
| C-1  | DailyReport shift-based vs SalesReport order-based divergence | **INFO** (by design) |

---

## ██ RISK SCORE

### Scoring Breakdown

| Category                  | Max | Score  | Notes                                                                            |
| ------------------------- | --- | ------ | -------------------------------------------------------------------------------- |
| Financial Correctness     | 30  | **27** | ✅ All core financial calculations correct. -3 for customer debt filter bug      |
| Data Integrity            | 15  | **13** | ✅ Snapshots, soft delete, concurrency control. -2 for debt report status filter |
| Returns/Refund Handling   | 15  | **14** | ✅ Comprehensive netting everywhere. -1 for employee daily breakdown             |
| Status Filter Consistency | 10  | **9**  | ✅ Consistent across 6/7 services. -1 for CustomerDebts                          |
| Payment Accuracy          | 10  | **9**  | ✅ From Payment records. -1 for BankTransfer missing in DetailedShifts total     |
| Historical Stability      | 5   | **5**  | ✅ All COGS use OrderItem.UnitCost                                               |
| Performance               | 5   | **3**  | ⚠️ Multiple N+1 queries, memory pressure                                         |
| Security                  | 5   | **4**  | ✅ Tenant isolation, soft delete. -1 for timezone edge case                      |
| Test Coverage             | 5   | **1**  | ❌ No report-specific tests exist                                                |

### 🎯 FINAL RISK SCORE: **85 / 100**

| Range  | Rating                       |
| ------ | ---------------------------- |
| 0-30   | Broken                       |
| 30-50  | Risky                        |
| 50-70  | Needs Work                   |
| 70-85  | **Good — Production Viable** |
| 85-100 | Enterprise Grade             |

### Assessment

The reporting system is **production-ready** with strong financial foundations. The core financial invariants (revenue, COGS, returns, payment methods, status filters) are correctly implemented across almost all services. The system uses proper snapshot isolation (OrderItem.UnitCost), comprehensive return netting, and correct payment source (Payment records).

**Primary concerns:**

1. **Customer debt report** has a status filter bug (HIGH)
2. **No report-specific tests** exist (CRITICAL for ongoing maintenance)
3. **N+1 queries** will cause performance degradation as data grows
4. **Timezone mismatch** (2-hour UTC offset) causes subtle date attribution issues

The system would reach 90+ with: status filter fix for CustomerDebts, report service test coverage, and N+1 query resolution.
