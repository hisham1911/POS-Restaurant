# POS Reporting System — Deep Audit, Stress Test & Architectural Review

**Audit Date:** March 11, 2026  
**Auditor Role:** Senior Fintech Reporting Auditor & Retail Analytics Architect  
**System:** KasserPro POS (ASP.NET Core + React/TypeScript)  
**Scope:** All 7 report domains, 23 frontend pages, 15+ endpoints

---

## Table of Contents

1. [Best Practices from Industry Research](#1-best-practices-from-industry-research)
2. [Current Reporting System Analysis](#2-current-reporting-system-analysis)
3. [Inconsistencies Found](#3-inconsistencies-found)
4. [Scenarios Where Reports Could Be Misleading](#4-scenarios-where-reports-could-be-misleading)
5. [Architectural Weaknesses](#5-architectural-weaknesses)
6. [Recommendations for Improvement](#6-recommendations-for-improvement)
7. [Suggested Automated Tests](#7-suggested-automated-tests)

---

## 1. Best Practices from Industry Research

### 1.1 Immutable Transaction Snapshots (Square, Stripe, Shopify)

All major POS/commerce platforms store **point-in-time snapshots** of prices, taxes, and discounts on every order item. If a product price changes next week, historical orders must still reflect the original price.

**KasserPro Status:** ✅ **PASS** — `OrderItem` stores `UnitPrice`, `UnitCost`, `OriginalPrice`, `TaxRate`, `DiscountAmount` as immutable snapshots. Branch name, currency, and user name are also snapshotted on orders.

### 1.2 Refund Chain Integrity (Stripe Model)

Refunds must be linked to original transactions with a clear audit trail. Partial refunds must track cumulative amounts to prevent over-refund.

**KasserPro Status:** ✅ **PASS** — `Order.OriginalOrderId` links returns to originals. `OrderItem.RefundedQuantity` tracks cumulative partial refunds. `Order.RefundAmount` caps total refunds at original order total. `RefundLog` provides audit trail with JSON stock change details.

### 1.3 Backend as Single Source of Truth (Industry Standard)

Financial report values must be calculated **exclusively** on the backend. Frontends should display, never recalculate.

**KasserPro Status:** ✅ **PASS** — All 23 frontend report pages use `formatCurrency()` display-only. No client-side recalculation of totals or margins. Percentages for UI visualization (bar chart widths) are cosmetic only.

### 1.4 Atomic Transaction Processing

Order completion, payment recording, stock decrement, and customer balance updates must happen atomically.

**KasserPro Status:** ✅ **PASS** — Orders complete within database transactions. Stock re-checked inside write transaction. Optimistic concurrency via `RowVersion` prevents double-completion.

### 1.5 Report-Transaction Reconciliation

The sum of all detail records must always equal the report totals ("penny test"). For any date range, `SUM(order.Total WHERE Completed) - SUM(return.Total)` must equal the reported net sales.

**KasserPro Status:** ⚠️ **PARTIAL** — See Section 3 for specific reconciliation gaps.

### 1.6 Time Zone Consistency

Reports must use consistent timezones. Mixing UTC storage with local-time filtering causes "missing orders" at day boundaries.

**KasserPro Status:** ⚠️ **RISK** — Backend stores all dates in UTC. Frontend formats in `Africa/Cairo`. Daily reports filter on `ClosedAt.Value.Date` which uses UTC date boundaries — this means a shift closed at 11:30 PM Cairo time (9:30 PM UTC) appears on the correct UTC date, but a shift closed at 1:00 AM Cairo time (11:00 PM UTC the previous day) may appear on the wrong date for Egyptian users.

### 1.7 Materialized Reporting Views (Shopify, Square at Scale)

At scale (>10K orders), real-time aggregation becomes slow. Best practice is pre-computed reporting tables updated on write events.

**KasserPro Status:** ❌ **NOT IMPLEMENTED** — All reports query raw transactional tables. Acceptable for current scale, but will become a bottleneck.

### 1.8 No Report Caching Without Invalidation

Cached reports that don't invalidate on new transactions show stale data.

**KasserPro Status:** ✅ **SAFE** (by absence) — No caching layer on reports. Every report hits the database fresh. This is correct for data accuracy, though it impacts performance.

---

## 2. Current Reporting System Analysis

### 2.1 Architecture Summary

| Component                       | Count                   | Quality                                              |
| ------------------------------- | ----------------------- | ---------------------------------------------------- |
| Backend Report Controllers      | 7                       | Well-structured, tenant-isolated                     |
| Backend Service Interfaces      | 7                       | Clean contracts                                      |
| Backend Service Implementations | 7                       | Comprehensive calculations                           |
| DTO Families                    | 7 (20+ individual DTOs) | Type-safe, well-modeled                              |
| Frontend Report Pages           | 23                      | Display-only, no recalculations                      |
| Frontend API Integration Files  | 7                       | RTK Query with proper typing                         |
| Existing Unit Tests             | 42+                     | Good calculation coverage, weak aggregation coverage |

### 2.2 Report Domain Coverage

| Report                | Backend Service          | Key Calculations                     | Return/Refund Handling                     |
| --------------------- | ------------------------ | ------------------------------------ | ------------------------------------------ |
| Daily Report          | `ReportService`          | Shift-based sales, payment breakdown | ✅ Separate return order tracking          |
| Sales Report          | `ReportService`          | Period sales, daily breakdown, COGS  | ✅ Returns deducted from daily totals      |
| Profit & Loss         | `FinancialReportService` | Revenue, COGS, expenses, margins     | ✅ Returns deducted from sales & COGS      |
| Expenses              | `FinancialReportService` | Category breakdown, payment methods  | N/A (no returns on expenses)               |
| Product Movement      | `ProductReportService`   | Stock flow, turnover, profitability  | ⚠️ **Only Completed status** (see finding) |
| Product Profitability | `ProductReportService`   | Revenue, cost, margins per product   | ⚠️ **Only Completed status** (see finding) |
| Slow Moving Products  | `ProductReportService`   | Days since sale, stock velocity      | ⚠️ **Only Completed status**               |
| COGS                  | `ProductReportService`   | Opening/closing inventory, purchases | ⚠️ **Only Completed status**               |
| Branch Inventory      | `InventoryReportService` | Stock quantities and values          | ✅ Real-time stock levels                  |
| Unified Inventory     | `InventoryReportService` | Cross-branch consolidated view       | ✅ Real-time                               |
| Transfer History      | `InventoryReportService` | Branch-to-branch movements           | ✅ Complete tracking                       |
| Low Stock             | `InventoryReportService` | Below-reorder-level alerts           | ✅ Correct                                 |
| Cashier Performance   | `EmployeeReportService`  | KPIs, scores, shift metrics          | ⚠️ Incomplete (see findings)               |
| Shift Details         | `EmployeeReportService`  | Per-shift financial breakdown        | ⚠️ Missing Fawry (see findings)            |
| Sales by Employee     | `EmployeeReportService`  | Revenue attribution                  | ⚠️ **Only Completed status**               |
| Top Customers         | `CustomerReportService`  | Spending, frequency, value           | ⚠️ **Only Completed status**               |
| Customer Debts        | `CustomerReportService`  | Outstanding balances, aging          | ✅ Uses denormalized TotalDue              |
| Customer Activity     | `CustomerReportService`  | New vs returning, retention          | ⚠️ N+1 query pattern                       |
| Supplier Purchases    | `SupplierReportService`  | Invoice totals, outstanding          | ✅ Correct                                 |
| Supplier Debts        | `SupplierReportService`  | Payables, aging                      | ✅ Uses denormalized TotalDue              |
| Supplier Performance  | `SupplierReportService`  | Payment timeliness, volume           | ✅ Correct                                 |

---

## 3. Inconsistencies Found

### CRITICAL (C) — Will Produce Incorrect Financial Reports

#### C-1: Product Reports Exclude PartiallyRefunded and Refunded Orders

**Location:** `ProductReportService` — all 4 methods  
**Issue:** Product Movement, Profitable Products, Slow Moving, and COGS reports filter orders with:

```csharp
o.Status == OrderStatus.Completed
```

This **excludes** orders with `Status = PartiallyRefunded` or `Status = Refunded`. When an order is partially refunded, its status changes to `PartiallyRefunded` — meaning its original sales are no longer counted in product reports, while the Daily and P&L reports DO include them.

**Impact:** Product revenue, COGS, profit margins, and turnover rates will be **understated** as soon as any refund occurs. The more refunds happen, the larger the discrepancy between product reports and the P&L report.

**Example:** Order for 10 widgets at $5 each ($50 total). Customer returns 2 widgets ($10 refund). Order status changes to `PartiallyRefunded`. P&L correctly shows $40 net revenue. But Product Movement shows $0 revenue for widgets because the order is excluded.

**Fix Required:** Add `|| o.Status == OrderStatus.PartiallyRefunded || o.Status == OrderStatus.Refunded` to all Product report queries, then subtract return quantities.

---

#### C-2: Product Reports Do Not Account for Return Orders

**Location:** `ProductReportService` — all 4 methods  
**Issue:** Even if C-1 is fixed, these reports still don't query `OrderType.Return` orders and subtract returned quantities/revenue. The Daily Report and P&L Report both correctly separate return orders and subtract them, but Product reports ignore returns entirely.

**Impact:** Product revenue and quantity sold will be **overstated** by the returned amounts (once C-1 is fixed). Without both fixes, the system has opposite errors that partially cancel out in unpredictable ways.

---

#### C-3: Sales by Employee Report Excludes Refunded Orders

**Location:** `EmployeeReportService.GetSalesByEmployeeReportAsync()`  
**Issue:** Filters only `OrderStatus.Completed`, missing `PartiallyRefunded` and `Refunded` sales from employee totals.

**Impact:** Employees' total revenue will be understated if any of their orders had refunds.

---

#### C-4: Top Customers Report Excludes Refunded Orders

**Location:** `CustomerReportService.GetTopCustomersReportAsync()`  
**Issue:** Same problem — only counts `Completed` orders, missing `PartiallyRefunded` and `Refunded`.

**Impact:** Customer spending totals will be understated. Customer ranking may be incorrect.

---

#### C-5: COGS Report Uses Current Product Cost, Not Order Snapshot

**Location:** `ProductReportService.GetCogsReportAsync()`  
**Issue:** Uses `oi.Product?.Cost ?? oi.Product?.AverageCost ?? 0` instead of `oi.UnitCost ?? 0`.

```csharp
// CURRENT (incorrect):
var totalCost = orderItems.Sum(oi => oi.Quantity * (oi.Product?.Cost ?? oi.Product?.AverageCost ?? 0));

// SHOULD BE:
var totalCost = orderItems.Sum(oi => oi.Quantity * (oi.UnitCost ?? 0));
```

**Impact:** COGS calculations will change retroactively when product costs are updated via purchase invoices. Historical COGS reports will not match what was actually sold. This violates the snapshot principle and can produce phantom profits or losses.

**Note:** The P&L Report (`FinancialReportService`) correctly uses `i.UnitCost ?? 0` — making the COGS Report inconsistent with P&L.

---

#### C-6: Product Movement Report Uses Current Product Cost, Not Snapshot

**Location:** `ProductReportService.GetProductMovementReportAsync()`  
**Issue:** Same as C-5:

```csharp
var cost = qtySold * (product.Cost ?? product.AverageCost ?? 0);
```

Should use the order item's `UnitCost` snapshot instead.

**Impact:** Product-level profit margins will be incorrect after cost changes. The sum of all product profits will NOT equal the P&L gross profit.

---

#### C-7: Product Profitability Report Uses Current Product Cost

**Location:** `ProductReportService.GetProfitableProductsReportAsync()`  
**Issue:**

```csharp
var cost = g.Sum(oi => oi.Quantity * (oi.Product!.Cost ?? oi.Product.AverageCost ?? 0));
```

Same pattern — uses current product cost instead of order-time `UnitCost`.

---

#### C-8: P&L Discount Calculation Missing Item-Level Discounts

**Location:** `FinancialReportService.GetProfitLossReportAsync()`  
**Issue:** The discount calculation only includes `o.DiscountAmount` (order-level discount):

```csharp
var totalDiscount = orders.Sum(o => o.DiscountAmount);
```

But it does NOT include item-level discounts (`oi.DiscountAmount`). The Daily Report correctly sums both:

```csharp
var totalItemDiscounts = completedOrders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
var totalOrderDiscounts = completedOrders.Sum(o => o.DiscountAmount);
var totalDiscount = totalItemDiscounts + totalOrderDiscounts;
```

**Impact:** P&L report will **understate** total discounts and **overstate** net sales and gross profit whenever item-level discounts are used.

---

### HIGH (H) — May Mislead Under Specific Conditions

#### H-1: Daily Report Shift-Based vs Sales Report Order-Based Date Filtering

**Location:** `ReportService.GetDailyReportAsync()` vs `ReportService.GetSalesReportAsync()`  
**Issue:** The Daily Report queries shifts closed on a date (`s.ClosedAt.Value.Date == reportDate`). The Sales Report queries orders completed in a date range (`o.CompletedAt >= fromDate`). If a shift spans midnight, orders completed before midnight but in a shift that closes after midnight will appear in:

- **Daily Report:** On the shift close date (next day)
- **Sales Report:** On the order completion date (previous day)

**Impact:** If a user runs both reports for the same date, totals may differ. This is conceptually defensible (shift-based vs order-based views) but will confuse business owners.

---

#### H-2: Shift Summary TotalSales = TotalCash + TotalCard (Excludes Fawry)

**Location:** `ReportService.GetDailyReportAsync()` — shift summaries  
**Issue:**

```csharp
TotalSales = s.TotalCash + s.TotalCard
```

Fawry payments are separately computed from `Payment` records, but the `Shift.TotalFawry` is not stored on the Shift entity. The `TotalSales` in shift summaries excludes Fawry and Other payment methods.

**Impact:** Shift-level financial reconciliation will be inaccurate for any business using Fawry mobile payments.

---

#### H-3: Detailed Shift Report — Fawry Always Zero

**Location:** `EmployeeReportService.GetDetailedShiftsReportAsync()`  
**Issue:**

```csharp
TotalFawry = 0,  // Hardcoded!
TotalSales = s.TotalCash + s.TotalCard  // Misses Fawry
```

**Impact:** Shift detail reports will always show Fawry as zero regardless of actual Fawry payments.

---

#### H-4: Employee Sales Breakdown Missing Card/Fawry Split

**Location:** `EmployeeReportService.GetCashierPerformanceReportAsync()`  
**Issue:**

```csharp
var cashSales = completedOrders.Sum(o => o.AmountPaid);
var cardSales = 0m;  // Hardcoded!
var fawrySales = 0m;  // Hardcoded!
```

All sales are attributed to "cash" regardless of actual payment method.

**Impact:** Cashier payment method breakdown is completely non-functional.

---

#### H-5: Customer Activity N+1 Query Performance

**Location:** `CustomerReportService.GetCustomerActivityReportAsync()` and `GetTopCustomersReportAsync()`  
**Issue:** For new customer detection:

```csharp
foreach (var customerId in customerIds)
{
    var firstOrder = await _context.Orders.Where(...).OrderBy(...).FirstOrDefaultAsync();
}
```

This executes a separate database query for EACH customer. With 500 customers, that's 500+ queries.

**Impact:** Report will time out or be extremely slow for businesses with many customers.

---

#### H-6: Opening Inventory Calculation is Estimated, Not Actual

**Location:** `ProductReportService.GetCogsReportAsync()`  
**Issue:**

```csharp
var openingInventoryValue = closingInventoryValue + totalCost - totalPurchases;
```

This is a reverse-engineered estimate, not a recorded snapshot. It assumes all stock changes within the period are captured by `totalCost` and `totalPurchases`, ignoring manual adjustments, transfers, wastage, and theft.

**Impact:** The accounting equation `Opening + Purchases - COGS = Closing` is forced to balance by definition, but the opening value may be fictitious. The COGS figure derived from this equation may not match the item-level COGS sum.

---

#### H-7: Product Movement Opening Stock is Reverse-Calculated

**Location:** `ProductReportService.GetProductMovementReportAsync()`  
**Issue:**

```csharp
OpeningStock = currentStock + qtySold + tOut - purchased - tIn
```

Same reverse-engineering pattern. Does not account for stock adjustments, wastage entries, or other movements.

---

### MEDIUM (M) — Quality/Consistency Issues

#### M-1: Inconsistent Status Filters Across Reports

Different reports include different order statuses:

| Report            | Completed | PartiallyRefunded | Refunded | Returns       |
| ----------------- | --------- | ----------------- | -------- | ------------- |
| Daily Report      | ✅        | ✅                | ✅       | ✅ (separate) |
| Sales Report      | ✅        | ✅                | ✅       | ✅ (separate) |
| P&L Report        | ✅        | ✅                | ✅       | ✅ (separate) |
| Product Reports   | ✅        | ❌                | ❌       | ❌            |
| Employee Sales    | ✅        | ❌                | ❌       | ❌            |
| Top Customers     | ✅        | ❌                | ❌       | ❌            |
| Customer Activity | ✅        | ❌                | ❌       | ❌            |

This inconsistency guarantees cross-report discrepancies.

---

#### M-2: Slow Moving Report Uses Product.Price as Fallback Stock Value

**Location:** `ProductReportService.GetSlowMovingProductsReportAsync()`  
**Issue:**

```csharp
var stockValue = currentStock * (product.Cost ?? product.AverageCost ?? product.Price);
```

Using selling price as a fallback for cost valuation overstates inventory value.

---

#### M-3: Customer Activity Segments Have Wrong TotalOrders

**Location:** `CustomerReportService.GetCustomerActivityReportAsync()`  
**Issue:** Both "New Customers" and "Returning Customers" segments use the same TotalOrders count:

```csharp
TotalOrders = ordersInPeriod.Count(o => customerIds.Contains(o.CustomerId!.Value))
```

This counts ALL orders for both segments instead of segment-specific orders.

---

#### M-4: Thermal Printer Uses toFixed(2) Rounding

**Location:** `DailyReportPage.tsx` — print function  
**Issue:** `const fmt = (n: number) => n.toFixed(2)` can produce different rounding than `formatCurrency()` for edge cases.

**Impact:** Printed receipt may show slightly different values than screen display, though both source the same backend data.

---

#### M-5: P&L AverageOrderValue Uses Total Revenue / Order Count

**Location:** `FinancialReportService.GetProfitLossReportAsync()`  
**Issue:**

```csharp
AverageOrderValue = orders.Count > 0 ? actualTotalRevenue / orders.Count : 0
```

`actualTotalRevenue` has refunds subtracted, but `orders.Count` only counts non-return orders. If there are many refunds, the average will be artificially depressed.

---

## 4. Scenarios Where Reports Could Be Misleading

### Scenario 1: Order with Item Discount + Partial Refund

1. Order: 5 items at $10 each, 20% item discount → Subtotal $50, Discount $10, Net $40
2. Customer returns 2 items
3. **P&L Report:** Shows $24 net (correct: $40 - $16 refund)
4. **Product Report:** Shows $0 for this product (bug C-1: order status = PartiallyRefunded, excluded)
5. **Discrepancy:** P&L says $24 revenue, product reports say $0

### Scenario 2: Product Cost Updated After Sales

1. Sell 100 units at cost=$5, price=$10 → Profit = $500
2. New purchase invoice updates `Product.AverageCost` to $7
3. **P&L Report:** Correctly shows profit = $500 (uses `OrderItem.UnitCost = $5`)
4. **COGS Report:** Shows profit = $300 (uses current `Product.Cost = $7`)
5. **Discrepancy:** $200 phantom loss appears in COGS report

### Scenario 3: High-Volume Refund Day

1. 50 orders completed, 10 full refunds processed
2. **Daily Report:** Shows 40 net orders with correct totals (shift-based)
3. **Product Movement:** Shows 50 orders' worth of sales (missing refund subtraction + excludes refunded orders)
4. Business owner sees product revenue >> total revenue

### Scenario 4: Fawry Payment Business

1. 30% of sales via Fawry mobile payments
2. **Daily Report:** Correctly shows Fawry breakdown at top level
3. **Shift Summaries:** Show TotalSales = Cash + Card (missing 30% of revenue!)
4. **Cashier Performance:** Shows all sales as "cash"
5. Business owner questions why shift totals don't match daily totals

### Scenario 5: Cross-Midnight Shift

1. Shift opened at 6 PM on Jan 15, closed at 2 AM on Jan 16
2. Orders completed between 6 PM–midnight are on Jan 15 (by CompletedAt)
3. **Daily Report for Jan 16:** Shows ALL orders from this shift (closed on Jan 16)
4. **Sales Report for Jan 15:** Shows orders with CompletedAt on Jan 15
5. **Sales Report for Jan 16:** Shows orders with CompletedAt on Jan 16 (shift orders after midnight)
6. Neither Sales Report date fully matches the Daily Report

### Scenario 6: Customer with Multiple Partial Refunds

1. Customer places $200 order
2. Returns $50 → Status: PartiallyRefunded
3. Returns another $30 → Status: PartiallyRefunded
4. **Top Customers Report:** Shows $0 for this customer (excludes PartiallyRefunded orders)
5. **Customer Debts Report:** May show incorrect balance

### Scenario 7: Sub-Penny Rounding Cascade

1. Order: 3 items at $3.33 each with 14% tax
2. Per-item tax = $0.4662, rounded to $0.47
3. Three items: 3 × $0.47 = $1.41
4. But 14% of $9.99 = $1.3986 ≈ $1.40
5. **Drift:** $0.01 per order, accumulates over thousands of orders
6. Tax reported may systematically differ from `TaxRate% × NetSales`

---

## 5. Architectural Weaknesses

### 5.1 No Composite Index for Report Queries (Performance: HIGH)

Every report service queries orders with the pattern:

```sql
WHERE TenantId = @t AND BranchId = @b AND Status IN (...) AND CompletedAt >= @from AND CompletedAt < @to
```

There is **no composite index** on `(TenantId, BranchId, Status, OrderType, CompletedAt)`.

The existing index on `(ShiftId, CreatedAt)` does NOT help these queries. As order volume grows, report queries will perform full table scans.

**Recommendation:** Add:

```sql
CREATE INDEX IX_Orders_Reporting
ON Orders (TenantId, BranchId, Status, OrderType, CompletedAt)
WHERE IsDeleted = 0;
```

### 5.2 In-Memory Data Loading (Scalability: HIGH)

All report services use `.ToListAsync()` and process data in memory using LINQ-to-Objects:

```csharp
var orders = await _context.Orders.Where(...).Include(...).ToListAsync();
var grossSales = orders.Sum(o => o.Subtotal);  // In-memory!
```

For 50K+ orders with their items and payments eagerly loaded, this will:

- Consume hundreds of MB of RAM per report request
- Risk OutOfMemoryException
- Cause GC pressure and latency spikes

**Recommendation:** Push aggregations to the database:

```csharp
var grossSales = await _context.Orders
    .Where(o => ...)
    .SumAsync(o => o.Subtotal);
```

### 5.3 No Report Caching (Performance: MEDIUM)

Reports are requested multiple times by the same user (changing date ranges, refreshing). Each request executes the full query pipeline.

**Recommendation:** Add short-TTL (30-60 second) `IMemoryCache` with composite keys `{TenantId}:{BranchId}:{ReportType}:{DateRange}`. Invalidate on order completion/refund.

### 5.4 N+1 Query Patterns (Performance: HIGH for Customer Reports)

`CustomerReportService` executes a query per customer for "new customer" detection:

```csharp
foreach (var customerId in customerIds)
{
    var firstOrder = await _context.Orders.Where(...).FirstOrDefaultAsync();
}
```

With 1000 customers, this is 1000 separate queries. Same pattern in `CustomerActivityReportAsync`.

**Recommendation:** Use a single query with windowed aggregation:

```csharp
var firstOrders = await _context.Orders
    .Where(o => o.CustomerId != null && o.Status == OrderStatus.Completed)
    .GroupBy(o => o.CustomerId)
    .Select(g => new { CustomerId = g.Key, FirstOrderDate = g.Min(o => o.CompletedAt) })
    .ToListAsync();
```

### 5.5 Missing Data Archival Strategy

All orders, items, and payments are queried from the same tables regardless of age. A 5-year-old business with 500K orders will have all historical data in the hot query path.

### 5.6 No Report Versioning or Checksums

There is no mechanism to verify that a previously generated report would produce the same results if re-run. If a bug is fixed, all historical reports change silently.

**Recommendation:** Store report snapshots (or at minimum a hash of key metrics) for auditing.

---

## 6. Recommendations for Improvement

### Priority 1: Fix Critical Bugs (C-1 through C-8)

| Bug           | Fix Description                                                                          | Estimated Scope |
| ------------- | ---------------------------------------------------------------------------------------- | --------------- |
| C-1, C-2      | Add missing status filters and return order handling to all ProductReportService methods | Medium          |
| C-3           | Add PartiallyRefunded/Refunded status to Sales by Employee                               | Small           |
| C-4           | Add PartiallyRefunded/Refunded status to Customer reports                                | Small           |
| C-5, C-6, C-7 | Replace `product.Cost` with `orderItem.UnitCost` in all Product report COGS              | Small           |
| C-8           | Add item-level discounts to P&L discount calculation                                     | Small           |

### Priority 2: Fix High-Impact Issues (H-1 through H-7)

| Bug      | Fix Description                                                         |
| -------- | ----------------------------------------------------------------------- |
| H-2, H-3 | Add Fawry/Other to Shift.TotalSales or compute from Payments            |
| H-4      | Query Payment records for cashier payment method breakdown              |
| H-5      | Replace N+1 with batch query for customer first-order dates             |
| H-6, H-7 | Record opening inventory snapshots per period, or document as estimates |

### Priority 3: Standardize Order Status Filtering

Create a shared helper:

```csharp
public static class ReportQueryExtensions
{
    public static IQueryable<Order> CompletedSalesOrders(this IQueryable<Order> query)
        => query.Where(o => (o.Status == OrderStatus.Completed
                          || o.Status == OrderStatus.PartiallyRefunded
                          || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return);

    public static IQueryable<Order> ReturnOrders(this IQueryable<Order> query)
        => query.Where(o => (o.Status == OrderStatus.Completed
                          || o.Status == OrderStatus.PartiallyRefunded
                          || o.Status == OrderStatus.Refunded)
                         && o.OrderType == OrderType.Return);
}
```

### Priority 4: Performance Improvements

1. **Add composite reporting index** on `Orders(TenantId, BranchId, Status, OrderType, CompletedAt)`
2. **Push aggregations to SQL** — replace `.ToListAsync()` + in-memory Sum with `.SumAsync()` / `.GroupBy()` at DB level
3. **Add IMemoryCache** with 30-60s TTL and hash-based invalidation
4. **Batch customer queries** — eliminate N+1 patterns

### Priority 5: Architectural Enhancements

1. **Reporting Events:** Publish domain events on order completion/refund. Consider event-sourced reporting table updated asynchronously.
2. **Financial Snapshot Table:** Store daily closing snapshots (total sales, returns, COGS, expenses) that are immutable once generated.
3. **Cross-Report Validation API:** Endpoint that verifies `DailyReport.TotalSales == SalesReport.TotalSales` for the same period, surfacing discrepancies.
4. **Report Audit Log:** Record when reports are generated, by whom, with what parameters, and the checksum of results.
5. **UTC/Local Time Strategy:** Add explicit timezone parameter to report APIs and convert at the query level, or document the UTC boundary behavior.

---

## 7. Suggested Automated Tests

### 7.1 Cross-Report Reconciliation Tests

```
TEST: "P&L NetSales equals SalesReport TotalSales for same period"
  - Generate orders with various statuses
  - Run both reports for same date range
  - Assert P&L.NetSales == SalesReport.TotalSales

TEST: "Sum of all product revenues equals P&L GrossSales"
  - Run ProductMovement report and P&L for same period
  - Assert SUM(product.TotalRevenue) == P&L.GrossSales

TEST: "Daily report total equals sum of daily breakdown in sales report"
  - Run Sales Report for a month
  - Assert TotalSales == SUM(DailySales.Sales)
```

### 7.2 Refund Integrity Tests

```
TEST: "Partial refund correctly adjusts all reports"
  - Create order with 5 items
  - Refund 2 items
  - Assert Daily Report: TotalSales reduced by refund amount
  - Assert P&L: GrossSales reduced, COGS reduced, RefundsAmount matches
  - Assert Product Report: QuantitySold = 3 (not 5, not 0)
  - Assert Employee Report: Revenue reflects net of refund

TEST: "Multiple partial refunds maintain consistency"
  - Create order with 10 items
  - Refund 3 items, then refund 2 more
  - Assert all reports still balance
  - Assert RefundAmount == sum of both refunds

TEST: "Full refund after partial refund"
  - Create order, partial refund, then full remaining refund
  - Assert order shows $0 net across all reports
```

### 7.3 Historical Data Safety Tests

```
TEST: "Changing product price does not affect historical reports"
  - Create and complete 10 orders at price=$10
  - Change product price to $15
  - Run reports for historical period
  - Assert revenue still shows $10 * quantities

TEST: "Changing product cost does not affect historical P&L"
  - Create orders with UnitCost=$5
  - Update product Cost to $8
  - Assert P&L COGS still uses $5

TEST: "Deleting a product does not break reports"
  - Create orders, soft-delete product
  - Assert reports still show historical data via snapshots
```

### 7.4 Edge Case Tests

```
TEST: "Order with $0.01 items and 14% tax"
  - 3 items at $0.01 each
  - Assert tax calculation doesn't produce negative or absurd values
  - Assert rounding is consistent

TEST: "Order with combined item + order discounts"
  - 10% item discount + 5% order discount
  - Assert total discount = correct compounded amount
  - Assert P&L discount includes both levels

TEST: "Very large order (1000 items)"
  - Assert report doesn't timeout or OOM
  - Assert totals are mathematically correct

TEST: "Mixed tax rates in single order"
  - Items with 0%, 5%, and 14% tax
  - Assert tax aggregation correctly sums different rates
```

### 7.5 Performance/Scale Tests

```
TEST: "Report generation with 10K orders completes under 5 seconds"
TEST: "Report generation with 50K orders completes under 15 seconds"
TEST: "Concurrent report generation by 10 users doesn't cause errors"
TEST: "Customer report with 1000+ customers doesn't N+1"
```

### 7.6 Fawry/Payment Method Tests

```
TEST: "Shift summary includes Fawry totals correctly"
TEST: "Employee performance shows correct payment method breakdown"
TEST: "Daily report payment breakdown sums to total sales"
```

---

## Summary of Findings

| Severity          | Count | Description                                                 |
| ----------------- | ----- | ----------------------------------------------------------- |
| **CRITICAL**      | 8     | Report integrity bugs that produce incorrect financial data |
| **HIGH**          | 7     | Issues that mislead under specific conditions               |
| **MEDIUM**        | 5     | Quality and consistency concerns                            |
| **Performance**   | 4     | Scalability bottlenecks                                     |
| **Architectural** | 6     | Long-term reliability improvements                          |

### Top 3 Actions Required Immediately

1. **Fix C-1/C-2:** Add `PartiallyRefunded` and `Refunded` status filtering + return order handling to ALL product reports. Without this fix, product reports are unreliable after any refund.

2. **Fix C-5/C-6/C-7:** Replace `product.Cost` with `orderItem.UnitCost` in all COGS calculations. Without this, profit numbers change retroactively when costs are updated.

3. **Fix C-8:** Add item-level discounts to P&L discount total. Without this, net sales and gross profit are overstated whenever item discounts are used.

### Conclusion

The reporting system has a **solid architectural foundation**: immutable price snapshots, atomic transactions, proper refund chain tracking, and a clean frontend-backend separation. However, **8 critical calculation bugs** prevent the reports from being financially accurate. The most impactful issue is the **inconsistent order status filtering** across report services, which causes cross-report discrepancies that would destroy business owner trust.

After fixing the critical and high-priority issues identified in this audit, the system will be trustworthy for production financial reporting. The performance recommendations become important as the business scales beyond ~10K orders.
