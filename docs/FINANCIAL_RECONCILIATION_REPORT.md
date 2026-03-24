# Financial Reconciliation Report — Mathematical Verification

**Date:** March 11, 2026  
**Role:** Financial Reconciliation Engineer & Fintech Data Integrity Auditor  
**System:** KasserPro POS  
**Method:** Algebraic proof against extracted source-code formulas

---

## Table of Contents

1. [Financial Invariants Definition](#1-financial-invariants-definition)
2. [Cross-Report Formula Comparison](#2-cross-report-formula-comparison)
3. [Transaction Scenario Simulations](#3-transaction-scenario-simulations)
4. [Drift Detection](#4-drift-detection)
5. [Long-Term Consistency Analysis](#5-long-term-consistency-analysis)
6. [Reconciliation Summary](#6-reconciliation-summary)

---

## Notation

Throughout this document, these symbols are used:

| Symbol  | Meaning                                                                                      |
| ------- | -------------------------------------------------------------------------------------------- |
| `O`     | Set of completed sales orders (Completed ∪ PartiallyRefunded ∪ Refunded, OrderType ≠ Return) |
| `R`     | Set of return orders (same statuses, OrderType = Return)                                     |
| `O_c`   | Set of orders with Status = Completed only                                                   |
| `Σ`     | Summation over a set                                                                         |
| `o.T`   | Order.Total                                                                                  |
| `o.S`   | Order.Subtotal                                                                               |
| `o.D`   | Order.DiscountAmount (order-level only)                                                      |
| `o.Tax` | Order.TaxAmount                                                                              |
| `o.SC`  | Order.ServiceChargeAmount                                                                    |
| `i.T`   | OrderItem.Total                                                                              |
| `i.S`   | OrderItem.Subtotal (= UnitPrice × Quantity)                                                  |
| `i.D`   | OrderItem.DiscountAmount                                                                     |
| `i.Tax` | OrderItem.TaxAmount                                                                          |
| `i.UC`  | OrderItem.UnitCost (snapshot)                                                                |
| `p.C`   | Product.Cost (current)                                                                       |
| `p.AC`  | Product.AverageCost (current)                                                                |

### Order Total Identity (from OrderService.CalculateOrderTotals)

```
o.S = Σᵢ(i.S)
o.T = (o.S − Σᵢ(i.D) − o.D) + o.Tax + o.SC
```

Where `o.Tax` is recalculated when `o.D > 0`:

```
If o.D > 0:
  ratio = o.D / (o.S − Σᵢ(i.D))
  o.Tax = Σᵢ((i.S − i.D) × (1 − ratio) × i.TaxRate / 100)
Else:
  o.Tax = Σᵢ(i.Tax)
```

**Important:** `Σᵢ(i.T) ≠ o.T` when `o.D > 0` or `o.SC > 0`.

---

## 1. Financial Invariants Definition

### 1.1 Invariants That MUST Hold

| ID         | Invariant                                | Mathematical Expression                                                                   |
| ---------- | ---------------------------------------- | ----------------------------------------------------------------------------------------- | ---------------- | ----------------- |
| **INV-1**  | Net Sales consistency                    | DailyReport.actualTotalSales == SalesReport.TotalSales (same date range, same data scope) |
| **INV-2**  | P&L Revenue = Net Sales                  | P&L.actualTotalRevenue == Σ(o.T for o ∈ O) −                                              | Σ(r.T for r ∈ R) |                   |
| **INV-3**  | COGS from snapshots                      | P&L.netCost == Σ(i.UC × i.Qty for i ∈ items(O)) − Σ(i.UC ×                                | i.Qty            | for i ∈ items(R)) |
| **INV-4**  | Gross Profit = Revenue − COGS            | P&L.grossProfit == P&L.actualNetSales − P&L.netCost                                       |
| **INV-5**  | Product Revenue = Σ(item revenues)       | Σ(ProductMovement.TotalRevenue) == Σ(i.T for all i in items(O ∪ R))                       |
| **INV-6**  | Product COGS = Σ(item costs at snapshot) | Σ(ProductMovement.TotalCost) == Σ(i.UC × i.Qty for all i in items(O ∪ R))                 |
| **INV-7**  | Discounts complete                       | TotalDiscount == Σᵢ(i.D) + Σₒ(o.D) for all orders                                         |
| **INV-8**  | Payment decomposition                    | TotalCash + TotalCard + TotalFawry + TotalOther == actualTotalSales                       |
| **INV-9**  | Employee Revenue summation               | Σ(EmployeeSales) == TotalSales                                                            |
| **INV-10** | Customer Revenue summation               | Σ(CustomerSpent, customer orders only) == TotalSalesFromCustomerOrders                    |
| **INV-11** | Daily breakdown summation                | Σ(DailySales.Sales) == SalesReport.TotalSales                                             |
| **INV-12** | Shift summation                          | Σ(ShiftSummary.TotalSales) == DailyReport.TotalSales                                      |
| **INV-13** | COGS Report matches P&L COGS             | CogsReport.CostOfGoodsSold == P&L.netCost                                                 |
| **INV-14** | Product Profit summation                 | Σ(ProductProfit) == P&L.GrossProfit                                                       |
| **INV-15** | Inventory movement balance               | OpeningStock + Purchased + TransferIn − Sold − TransferOut == ClosingStock                |

---

## 2. Cross-Report Formula Comparison

### 2.1 Revenue Concept — "Total Sales"

Each report computes "total sales / revenue" using different formulas:

| Report                   | Variable             | Formula                                  | Order Filter              |
| ------------------------ | -------------------- | ---------------------------------------- | ------------------------- | --- | ------------------------------------------ |
| **DailyReport**          | `actualTotalSales`   | `Σ(o.T for o ∈ O) −                      | Σ(r.T for r ∈ R)          | `   | Shift-based: `ClosedAt.Date == reportDate` |
| **SalesReport**          | `totalSales`         | `Σ(o.T for o ∈ O) −                      | Σ(r.T for r ∈ R)          | `   | Date-based: `CompletedAt ∈ [from, to)`     |
| **P&L Report**           | `actualTotalRevenue` | `Σ(o.T for o ∈ O) −                      | Σ(r.T for r ∈ R)          | `   | Date-based: `CompletedAt ∈ [from, to)`     |
| **ProductMovement**      | `Σ(TotalRevenue)`    | `Σ(i.T for i ∈ items(O_c))`              | `Status = Completed ONLY` |
| **ProductProfitability** | `totalRevenue`       | `Σ(i.T for i ∈ items(O_c))`              | `Status = Completed ONLY` |
| **COGS Report**          | `totalRevenue`       | `Σ(i.T for i ∈ items(O_c))`              | `Status = Completed ONLY` |
| **EmployeeSales**        | `totalRevenue`       | `Σ(o.T for o ∈ O_c)`                     | `Status = Completed ONLY` |
| **TopCustomers**         | `totalRevenue`       | `Σ(o.T for o ∈ O_c, CustomerId != null)` | `Status = Completed ONLY` |

#### ❌ INV-1: DailyReport vs SalesReport — CONDITIONAL FAIL

**DailyReport** scopes orders through shifts: `s.ClosedAt.Value.Date == reportDate`  
**SalesReport** scopes orders by: `o.CompletedAt ∈ [fromDate, toDate)`

These scopes diverge when a shift spans midnight.

**Proof by example:**

```
Shift: opened 22:00 UTC Jan 15, closed 02:00 UTC Jan 16
Order A: completed 23:00 UTC Jan 15 (within this shift)
Order B: completed 01:00 UTC Jan 16 (within this shift)

DailyReport(Jan 16): includes Order A + Order B (shift closed on Jan 16)
SalesReport(Jan 16, Jan 16): includes Order B only (CompletedAt on Jan 16)
SalesReport(Jan 15, Jan 15): includes Order A only

Result: DailyReport(Jan 16).TotalSales ≠ SalesReport(Jan 16).TotalSales
        DailyReport(Jan 16).TotalSales = SalesReport(Jan 15).TotalSales + SalesReport(Jan 16).TotalSales (partially)
```

**Verdict:** INV-1 **BREAKS** for cross-midnight shifts.  
**Severity:** HIGH — Business owner comparing daily and sales reports for same date will see different numbers.

---

#### ❌ INV-5: Product Revenue ≠ P&L Revenue — ALWAYS FAILS

**P&L calculation:**

```
P&L.GrossSales = Σ(o.Subtotal for o ∈ O)          // uses Order.Subtotal
P&L.actualTotalRevenue = Σ(o.T for o ∈ O) − |Σ(r.T for r ∈ R)|   // uses Order.Total
```

Filter: `Status ∈ {Completed, PartiallyRefunded, Refunded}, OrderType ≠ Return`

**Product Movement calculation:**

```
ProductMovement.TotalRevenue = Σ(i.T for i ∈ items(O_c))    // uses OrderItem.Total
```

Filter: `Status = Completed ONLY`

**Three separate divergence sources:**

**Source A — Status filter gap:**
Any order with `Status = PartiallyRefunded` or `Refunded` is counted in P&L but excluded from Product reports. Let `D` denote the set of orders with these statuses:

```
Divergence_A = Σ(o.T for o ∈ D) ≥ 0
```

This grows with every refund processed.

**Source B — Return order gap:**
P&L deducts return order amounts. Product reports never query return orders:

```
Divergence_B = |Σ(r.T for r ∈ R)| ≥ 0
```

This grows with every return processed.

**Source C — Order.Total vs Σ(Item.Total):**
When order-level discounts exist: `o.T ≠ Σ(i.T for i ∈ items(o))`  
The difference is the order-level discount + service charge + tax recalculation:

```
Δ = Σ(i.T) − (o.S − Σ(i.D) − o.D + o.Tax_recalc + o.SC)
```

**Net effect:** Product reports will **always** show different revenue than P&L unless zero refunds and zero order-level discounts exist.

**Verdict:** INV-5 **BREAKS** structurally.  
**Severity:** CRITICAL — Product-level P&L cannot be reconciled against top-level P&L.

---

#### ❌ INV-6: Product COGS ≠ P&L COGS — ALWAYS FAILS (different cost sources)

**P&L COGS calculation:**

```
P&L.netCost = Σ(i.UnitCost × i.Qty for i ∈ items(O)) − Σ(i.UnitCost × |i.Qty| for i ∈ items(R))
```

✅ Uses OrderItem.UnitCost (snapshot at order time)

**Product Movement COGS calculation:**

```
ProductMovement.cost = qtySold × (product.Cost ?? product.AverageCost ?? 0)
```

❌ Uses **current** Product.Cost/AverageCost

**COGS Report calculation:**

```
CogsReport.totalCost = Σ(i.Qty × (i.Product.Cost ?? i.Product.AverageCost ?? 0))
```

❌ Uses **current** Product.Cost/AverageCost

**Proof of divergence:**

```
At time T₁: Product.Cost = $5, order placed → OrderItem.UnitCost = $5
At time T₂: Purchase invoice updates Product.AverageCost = $7

P&L.COGS for that order item = $5 × Qty (uses snapshot)
ProductMovement.COGS = $7 × Qty (uses current)
CogsReport.COGS = $7 × Qty (uses current)
```

**Verdict:** INV-6 **BREAKS** after any cost update.  
**Severity:** CRITICAL — Three reports show three different profit numbers for same products.

---

#### ❌ INV-7: P&L Discount ≠ DailyReport Discount — ALWAYS FAILS with item discounts

**DailyReport discount:**

```
totalDiscount = (Σᵢ(i.D) + Σₒ(o.D)) − (|Σᵢᴿ(i.D)| + |Σₒᴿ(o.D)|)
```

✅ Includes BOTH item-level AND order-level discounts, minus returns

**P&L discount:**

```
totalDiscount = Σₒ(o.D)
```

❌ Includes ONLY order-level discounts

**Proof:**

```
Order: 5 items at $10, each with 10% item discount ($1 each = $5 total item discounts)
Plus 5% order discount on $45 net = $2.25

DailyReport.totalDiscount = $5 + $2.25 = $7.25
P&L.totalDiscount = $2.25 (missing $5 of item discounts)
```

**Downstream effect on Net Sales:**

```
DailyReport:  actualNetSales = grossSales − $7.25 − ...
P&L:          actualNetSales = grossSales − $2.25 − refunds
```

P&L **overstates** net sales by exactly the sum of all item discounts.

**Verdict:** INV-7 **BREAKS** whenever item-level discounts are applied.  
**Severity:** CRITICAL — P&L net sales and gross profit are inflated.

---

#### ❌ INV-8: Payment Decomposition — TWO-LAYER BREAK

**Layer 1: DailyReport top-level vs shift summaries**

DailyReport top-level payment breakdown:

```
totalCash = max(0, Σ(cash payments from O) − |Σ(cash payments from R)|)
totalCard = max(0, Σ(card payments from O) − |Σ(card payments from R)|)
totalFawry = max(0, ...)
totalOther = max(0, ...)
```

DailyReport shift summaries:

```
ShiftSummary.TotalCash = Shift.TotalCash  (stored at close time)
ShiftSummary.TotalCard = Shift.TotalCard  (stored at close time)
ShiftSummary.TotalSales = TotalCash + TotalCard
```

But `Shift.TotalCard` is computed as:

```csharp
// From CalculateShiftFinancials:
totalCard = allPayments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount)
```

This means **Shift.TotalCard includes Fawry + BankTransfer + everything non-cash**.

**So:**

```
Σ(ShiftSummary.TotalCash) = Σ(Shift.TotalCash)     ← includes return order payments (negative)
DailyReport.TotalCash = max(0, sales cash − refund cash)  ← different computation

If refunds processed AFTER shift close → Shift.TotalCash doesn't reflect the refund
If refunds processed during shift → Shift.TotalCash includes negative payment amounts
```

**Proof of divergence:**

```
Shift has: 10 cash orders @ $100 = $1,000 cash, 2 Fawry orders @ $50 = $100 Fawry
Shift.TotalCash = $1,000
Shift.TotalCard = $100 (Fawry counted as non-cash)

Refund of 1 cash order ($100) as return order in same shift:
Return payment: -$100 cash
Shift.TotalCash (at close) = $1,000 + (-$100) = $900 ← includes refund
DailyReport.TotalCash = max(0, $1,000 - $100) = $900 ← matches

ShiftSummary.TotalSales = $900 + $100 = $1,000
DailyReport.TotalSales = $900 + $0(card) + $100(fawry) + $0(other) = $1,000 ← but wait...
DailyReport.actualTotalSales = completedOrders.Total - refunds.Total
```

**Layer 2: Fawry is double-counted in Shift.TotalCard**

`Shift.TotalCard` = all non-cash. `ShiftSummary.TotalFawry` is computed separately from Payment records.

If a user sums ShiftSummary columns: `TotalCash + TotalCard + TotalFawry`, Fawry is counted **twice** (once in TotalCard, once standalone).

**Verdict:** INV-8 **BREAKS** in multiple ways.  
**Severity:** CRITICAL — Shift-level payment reconciliation is unreliable; Fawry double-counting inflates totals.

---

#### ❌ INV-9: Employee Revenue ≠ Total Sales — STRUCTURAL FAIL

**SalesByEmployee calculation:**

```
totalRevenue = Σ(o.T for o ∈ {Status=Completed only})
```

**P&L / SalesReport:**

```
totalRevenue = Σ(o.T for o ∈ {Status ∈ Completed,PartiallyRefunded,Refunded}) − |returns|
```

**Proof:**

```
Employee X processes 10 orders.
3 orders are later partially refunded (Status → PartiallyRefunded).

EmployeeSales.totalRevenue = Σ(T for the 7 remaining Completed orders)
P&L.actualTotalRevenue = Σ(T for all 10 orders) − refunds

The 3 partially refunded orders still have the ORIGINAL Total (not reduced).
EmployeeSales excludes all 3 → underreports revenue by their full original total.
P&L includes all 10 then subtracts only the refund amount.

Gap = Total of 3 orders − refund amount on those 3 orders > 0 always (partial refund < total)
```

**Verdict:** INV-9 **BREAKS** after any refund.  
**Severity:** CRITICAL — Cannot reconcile employee-level revenue against total revenue.

---

#### ❌ INV-10: Customer Revenue ≠ Total Customer Sales — STRUCTURAL FAIL

Same issue as INV-9. `TopCustomers` uses `Status = Completed` only.

**Additional deviation:** TopCustomers only includes orders where `CustomerId != null`. Walk-in orders (no customer) are excluded.

**Expected invariant:** `Σ(CustomerSpent) + walk-in sales == P&L.Revenue`  
**Actual:** `Σ(CustomerSpent) ≤ P&L.Revenue` with gap growing per refund.

**Verdict:** INV-10 **BREAKS**.  
**Severity:** HIGH

---

#### ✅ INV-11: Daily Breakdown Sum = SalesReport.TotalSales — HOLDS

The SalesReport correctly combines sales and returns by day:

```
dailySales = salesByDay ∪ returnsByDay
day.Sales = daySales − dayReturns
totalSales = Σ(day.Sales)
```

And `totalSales = grossSales − totalRefunds` where:

```
grossSales = Σ(o.T for o ∈ salesOrders)
totalRefunds = |Σ(r.T for r ∈ returnOrders)|
Σ(day.Sales) = Σ(daySales) − Σ(dayReturns) = grossSales − totalRefunds ✓
```

**Verdict:** INV-11 **HOLDS**.  
**Severity:** N/A

---

#### ❌ INV-12: Shift Sum ≠ DailyReport Total — FAILS

**DailyReport.actualTotalSales:**

```
= Σ(o.T for o ∈ completedOrders) − totalRefunds
```

Where `completedOrders` and `returnOrders` are filtered from shift orders.

**Σ(ShiftSummary.TotalSales):**

```
= Σ(s.TotalCash + s.TotalCard)
```

Where TotalCash/TotalCard were computed at shift close from **all** payments (including return order payments which are negative).

**Divergence scenarios:**

**Scenario A:** Order completed in shift, refunded AFTER shift close (new shift or no shift).

```
At close: Shift.TotalCash includes the sale payment.
But return order is in a different shift (or unshifted).
DailyReport only includes shifts closed on reportDate.
If return is in a shift closed on the same day: DailyReport deducts return.
If return is in a shift closed on a different day: DailyReport doesn't see it for that date.
```

**Scenario B:** Service charge and tax recalculation differences.

```
ShiftSummary.TotalSales = TotalCash + TotalCard = sum of Payment.Amount
But DailyReport.actualTotalSales = sum of Order.Total - refunds

Σ(Payment.Amount) should equal Σ(Order.AmountPaid) for completed orders.
Order.AmountPaid may differ from Order.Total when there's an AmountDue > 0 (credit sale).
```

**Proof:**

```
Order: Total = $100, AmountPaid = $70, AmountDue = $30 (credit sale)
Shift.TotalCash will include $70 (from payments)
DailyReport.actualTotalSales includes $100 (from Order.Total)

Σ(ShiftSummary.TotalSales) = $70
DailyReport.actualTotalSales = $100

Gap = $30 (the credit/unpaid amount)
```

**Verdict:** INV-12 **BREAKS** whenever credit sales exist or refunds cross shift boundaries.  
**Severity:** HIGH — Sum of shifts on the daily report doesn't match the daily total.

---

#### ❌ INV-13: COGS Report ≠ P&L COGS — ALWAYS FAILS

**P&L COGS:**

```
netCost = Σ(i.UnitCost × i.Qty for i ∈ items(O)) − Σ(i.UnitCost × |i.Qty| for i ∈ items(R))
```

Uses UnitCost snapshot. Includes Completed + PartiallyRefunded + Refunded. Deducts returns.

**COGS Report COGS:**

```
totalCost = Σ(i.Qty × (i.Product.Cost ?? i.Product.AverageCost ?? 0)) for items in O_c only
cogs = openingInventory + purchases − closingInventory
     = (closingInventory + totalCost − purchases) + purchases − closingInventory
     = totalCost   (algebraically)
```

**Three divergences:**

1. **Cost source:** UnitCost snapshot vs current Product.Cost
2. **Status filter:** O (all completed states) vs O_c (Completed only)
3. **Returns:** P&L deducts return COGS; COGS Report doesn't account for returns at all

Additionally, the COGS Report's `cogs = opening + purchases − closing` algebraically simplifies to just `totalCost` (the inventory formula is circular), meaning it provides no independent verification.

**Verdict:** INV-13 **BREAKS** on three dimensions.  
**Severity:** CRITICAL

---

#### ❌ INV-14: Product Profit Sum ≠ P&L Gross Profit — ALWAYS FAILS

Follows directly from INV-5 and INV-6 breaking:

```
Σ(ProductProfit) = Σ(ProductRevenue) − Σ(ProductCOGS)
// Both terms differ from P&L equivalents
```

**Verdict:** INV-14 **BREAKS**.  
**Severity:** CRITICAL

---

#### ⚠️ INV-15: Inventory Movement Balance — PARTIAL FAIL

**Product Movement formula:**

```
OpeningStock = currentStock + qtySold + tOut − purchased − tIn
```

This reverse-calculates opening stock from current state. It misses:

- Manual stock adjustments
- Wastage/shrinkage entries
- Inventory count corrections

If any of these exist, the balance won't close:

```
Opening (calculated) + Purchased + TransferIn − Sold − TransferOut ≠ Closing (actual)
```

But **by construction** it always appears balanced because opening is derived.

**Verdict:** INV-15 **APPEARS to hold but is circular** — provides no real verification.  
**Severity:** MEDIUM

---

## 3. Transaction Scenario Simulations

### Scenario S1: Simple Sale (Baseline)

```
Order #1: 3 × Widget @ $10, no discounts, 14% tax
  Item: Subtotal=$30, Discount=$0, Tax=$4.20, Total=$34.20
  Order: Subtotal=$30, Discount=$0, Tax=$4.20, Total=$34.20
  UnitCost=$5 (snapshot)
```

| Report          | Field              | Value  | Correct?                    |
| --------------- | ------------------ | ------ | --------------------------- |
| DailyReport     | actualTotalSales   | $34.20 | ✅                          |
| SalesReport     | totalSales         | $34.20 | ✅                          |
| P&L             | actualTotalRevenue | $34.20 | ✅                          |
| P&L             | netCost            | $15.00 | ✅ (3 × $5 UnitCost)        |
| P&L             | grossProfit        | $15.00 | ✅ ($30 − $15)              |
| ProductMovement | revenue            | $34.20 | ✅ (item.Total)             |
| ProductMovement | cost               | $15.00 | ⚠️ If Product.Cost still $5 |
| EmployeeSales   | totalRevenue       | $34.20 | ✅                          |

**All invariants hold** for a simple sale with no refunds and unchanged costs.

---

### Scenario S2: Item Discount + Order Discount

```
Order #2: 5 × Gadget @ $20, 10% item discount, 5% order discount, 14% tax
  Per item: Subtotal=$20, ItemDiscount=$2, NetAfterItem=$18
  5 items: Σ(Subtotal)=$100, Σ(ItemDiscount)=$10, NetAfterItems=$90
  OrderDiscount (5% of $90): $4.50
  After all discounts: $85.50
  Tax (14%): $11.97
  Total: $97.47
  UnitCost=$8 (snapshot)
```

| Report          | Field         | Formula                     | Value                            |
| --------------- | ------------- | --------------------------- | -------------------------------- |
| **DailyReport** | totalDiscount | `($10 + $4.50) − 0`         | **$14.50** ✅                    |
| **P&L**         | totalDiscount | `$4.50` (order-level only!) | **$4.50** ❌                     |
| **P&L**         | netSales      | `$100 − $4.50`              | **$95.50** ❌ (should be $85.50) |
| **P&L**         | grossProfit   | `$95.50 − $40 − refunds`    | **Inflated by $10** ❌           |

**Invariant INV-7 breaks:** P&L underreports discounts by $10 (the item discounts).  
**Invariant INV-4 consequence:** P&L gross profit is overstated by $10.

---

### Scenario S3: Partial Refund

```
Order #3 (from S1): 3 × Widget @ $10 + tax = $34.20. Customer returns 1 unit.
  Refund ratio: 1/3
  Return item: Total = −$34.20 × (1/3) = −$11.40
  Return order: Total = −$11.40
  Order status → PartiallyRefunded
```

| Report                   | Field              | Value                    | Correct?                              |
| ------------------------ | ------------------ | ------------------------ | ------------------------------------- |
| DailyReport              | actualTotalSales   | $34.20 − $11.40 = $22.80 | ✅                                    |
| SalesReport              | totalSales         | $34.20 − $11.40 = $22.80 | ✅                                    |
| P&L                      | actualTotalRevenue | $34.20 − $11.40 = $22.80 | ✅                                    |
| P&L                      | netCost            | $15.00 − $5.00 = $10.00  | ✅                                    |
| **ProductMovement**      | revenue            | **$0**                   | ❌ Order excluded (PartiallyRefunded) |
| **ProductProfitability** | revenue            | **$0**                   | ❌ Same                               |
| **EmployeeSales**        | totalRevenue       | **$0**                   | ❌ Order excluded (PartiallyRefunded) |
| **TopCustomers**         | totalSpent         | **$0**                   | ❌ Order excluded                     |

**INV-5 breaks:** P&L shows $22.80 revenue; Product reports show $0.  
**INV-9 breaks:** P&L shows $22.80; Employee reports show $0.  
**INV-10 breaks:** P&L shows $22.80; Customer reports show $0 for this customer.

---

### Scenario S4: Multiple Partial Refunds

```
Order #4: 10 × Part @ $15, Tax 14% → Total = $171.00. UnitCost=$6.

Refund 1: Return 3 units → −$51.30, Order status → PartiallyRefunded
Refund 2: Return 2 more units → −$34.20, Order status stays PartiallyRefunded
```

| Report          | Field              | Expected                        | Actual    |
| --------------- | ------------------ | ------------------------------- | --------- |
| P&L             | actualTotalRevenue | $171 − $51.30 − $34.20 = $85.50 | $85.50 ✅ |
| P&L             | netCost            | $60 − $18 − $12 = $30           | $30 ✅    |
| ProductMovement | revenue            | $85.50 (5 net units)            | **$0** ❌ |
| ProductMovement | cost               | $30                             | **$0** ❌ |
| EmployeeSales   | revenue            | $85.50                          | **$0** ❌ |

**Gap grows with each refund.** After 2 refunds, product and employee reports are completely disconnected from P&L.

---

### Scenario S5: Full Refund

```
Order #5: 2 × Item @ $50 + 14% tax = $114.00. Full refund.
  Return order: Total = −$114.00
  Order status → Refunded
```

| Report          | Field              | Expected | Actual                                      |
| --------------- | ------------------ | -------- | ------------------------------------------- |
| P&L             | actualTotalRevenue | $0       | $0 ✅                                       |
| ProductMovement | revenue            | $0       | $0 ✅ (order excluded, but $0 anyway)       |
| EmployeeSales   | revenue            | $0       | $0 ✅ (order excluded, and $0 contribution) |

**Special case:** Full refunds happen to produce correct $0 net because the entire order is excluded from both sides. But the **intermediate state** (before full refund was processed) was wrong if the order had any partial refund first.

---

### Scenario S6: Mixed Payment Methods

```
Order #6: $200 total. Paid: $100 cash + $70 card + $30 Fawry
```

| Report            | Field      | Value                             | Correct?                      |
| ----------------- | ---------- | --------------------------------- | ----------------------------- |
| DailyReport (top) | totalCash  | $100                              | ✅                            |
| DailyReport (top) | totalCard  | $70                               | ✅                            |
| DailyReport (top) | totalFawry | $30                               | ✅                            |
| ShiftSummary      | TotalCash  | $100                              | ✅                            |
| ShiftSummary      | TotalCard  | **$100** (Fawry lumped in!)       | ❌                            |
| ShiftSummary      | TotalFawry | $30 (computed from payments)      | ✅                            |
| ShiftSummary      | TotalSales | $100 + $100 = **$200**            | ✅ (but misleading breakdown) |
| CashierPerf       | cashSales  | **$200** (all attributed to cash) | ❌                            |
| CashierPerf       | cardSales  | **$0**                            | ❌                            |
| CashierPerf       | fawrySales | **$0**                            | ❌                            |

**INV-8 breaks at shift level:** `TotalCash + TotalCard = $200` is correct total, but:

- `TotalCard` ($100) includes $30 Fawry
- If user adds `TotalCash + TotalCard + TotalFawry` = $100 + $100 + $30 = **$230** (Fawry double-counted)

**INV-8 breaks at employee level:** All revenue attributed to cash regardless of actual method.

---

### Scenario S7: Credit Sale (Partial Payment)

```
Order #7: $500 total. Customer pays $300 cash. AmountDue=$200.
```

| Report                     | Field            | Value                              | Correct? |
| -------------------------- | ---------------- | ---------------------------------- | -------- |
| DailyReport                | actualTotalSales | $500 (uses Order.Total)            | ✅       |
| ShiftSummary               | TotalSales       | **$300** (uses Payment.Amount sum) | ❌       |
| Σ(ShiftSummary.TotalSales) |                  | **$300**                           | ❌       |

**INV-12 breaks:** DailyReport total = $500, but shift summaries sum to $300.  
**Gap = $200** = the unpaid credit amount.

---

### Scenario S8: Cost Update After Sale

```
Time T1: Product cost = $5. Sell 100 units.
  OrderItem.UnitCost = $5 (snapshot)
Time T2: New purchase invoice → Product.AverageCost = $7.50
```

| Report               | COGS      | Value                                      | Correct? |
| -------------------- | --------- | ------------------------------------------ | -------- |
| P&L                  | netCost   | $500 (100 × $5 UnitCost)                   | ✅       |
| ProductMovement      | cost      | **$750** (100 × $7.50 Product.AverageCost) | ❌       |
| ProductProfitability | cost      | **$750**                                   | ❌       |
| COGS Report          | totalCost | **$750**                                   | ❌       |

**INV-6 breaks:** $250 phantom cost increase. P&L shows $500 profit improvement over product reports.

---

### Scenario S9: Refund After Price Change

```
Time T1: Sell 5 × Widget @ $10 (UnitCost=$5, Total=$57 with tax)
Time T2: Change Product.Price to $15, Product.Cost to $8
Time T3: Customer returns 2 units
  Return uses OrderItem snapshot: UnitPrice=$10, UnitCost=$5
  Return amount = proportional to original order = $22.80
```

| Report          | Field         | Value                  | Notes                |
| --------------- | ------------- | ---------------------- | -------------------- |
| P&L             | COGS          | $15 (3×$5 snapshot)    | ✅ Correct           |
| ProductMovement | cost          | **$24** (3×$8 current) | ❌ Wrong cost        |
| P&L             | refund amount | $22.80                 | ✅ Based on original |

The refund correctly uses snapshot prices. But the product report uses post-update costs.  
**Gap = $9** ($24 − $15) on just 3 units.

---

### Scenario S10: Deleted Product

```
Product sold in Order #10 (UnitPrice=$20, UnitCost=$8 snapshot on item)
Product soft-deleted (IsActive=false)
```

| Report               | Behavior                                  | Correct?                      |
| -------------------- | ----------------------------------------- | ----------------------------- |
| P&L                  | Includes order via status/type filter     | ✅                            |
| ProductMovement      | Excluded (queries only IsActive products) | ❌                            |
| ProductProfitability | Include via OrderItem → Product nav       | ⚠️ Depends on query           |
| EmployeeSales        | Includes order                            | ⚠️ Status filter is the issue |

**INV-5 breaks:** Deleted product's revenue vanishes from product reports but remains in P&L.

---

## 4. Drift Detection

### 4.1 Revenue Drift Matrix

For the same date range, different reports will show different "total revenue":

| Condition                              | P&L Revenue | Product Revenue | Employee Revenue | Customer Revenue |
| -------------------------------------- | ----------- | --------------- | ---------------- | ---------------- |
| No refunds, no credit, costs unchanged | $X          | $X\*            | $X               | ≤ $X             |
| After 1 partial refund                 | $X−r        | $X−o            | $X−o             | ≤ $X−o           |
| After 10 partial refunds               | $X−Σr       | **$X−Σo**       | **$X−Σo**        | ≤ **$X−Σo**      |

Where:

- `r` = refund amount (partial, < order total)
- `o` = full original order total (entire order excluded from filtered reports)
- `$X*` = only matches if no order-level discounts and Product.Cost unchanged

**Revenue drift grows monotonically** with each refund event.

### 4.2 COGS Drift Over Time

```
Month 1: 1000 orders, AverageCost = $5   → Product COGS = $5,000, P&L COGS = $5,000 ✓
Month 2: Cost update to $7                → Product COGS (for Month 1!) = $7,000, P&L COGS = $5,000 ✗
Month 3: Cost update to $4                → Product COGS (for Month 1!) = $4,000, P&L COGS = $5,000 ✗
```

**Historical product reports retroactively change** with every cost update. P&L remains stable.

### 4.3 Discount Drift

Every order with item-level discounts inflates P&L net sales by the item discount total:

```
After 5,000 orders with avg $2 item discount:
P&L cumulative overstatement = 5,000 × $2 = $10,000 in inflated net sales
```

This directly inflates gross profit margin.

### 4.4 Employee Revenue Drift

```
If 15% of orders eventually have refunds:
Employee report underreports by ≈ 15% × average partially-refunded order total
```

For a business with $100K monthly sales and 15% refund rate:

```
~$15K in orders will have Status ≠ Completed
Employee reports could underreport revenue by up to $15K/month
```

### 4.5 Payment Method Attribution Drift

For every Fawry/non-Cash/non-Card payment:

```
ShiftSummary.TotalCard is inflated by $X (Fawry lumped in)
CashierPerformance.cashSales is inflated by $Y (everything called cash)
CashierPerformance.cardSales and fawrySales remain $0 forever
```

---

## 5. Long-Term Consistency Analysis

### 5.1 Simulation: 10,000 Orders Over 12 Months

**Assumptions:**

- 10,000 completed orders
- 12% partial refund rate (1,200 orders → Status: PartiallyRefunded)
- 3% full refund rate (300 orders → Status: Refunded)
- 30% orders have item-level discounts (avg $3/order)
- 2 cost updates per product over 12 months
- 20% of orders use Fawry
- 5% credit sales (AmountDue > 0)

**Revenue Divergence After 12 Months:**

| Report           | Revenue Shown                  | Divergence from P&L                        |
| ---------------- | ------------------------------ | ------------------------------------------ |
| P&L              | $X (correct)                   | —                                          |
| Product Reports  | $X − (1,500 orders' revenue)   | **−15% to −20%**                           |
| Employee Reports | $X − (1,500 orders' revenue)   | **−15% to −20%**                           |
| Customer Reports | ≤ $X − (1,500 orders' revenue) | **≥ −15%**                                 |
| COGS Report      | Uses current costs             | **±10% to ±30%** depending on cost changes |

**Discount Divergence:**

```
P&L underreports discounts by: 3,000 orders × $3 avg = $9,000
P&L overstates net sales by $9,000
P&L overstates gross profit by $9,000
```

**Shift Payment Divergence:**

```
Credit sales: 500 orders × avg $50 unpaid = $25,000
Σ(ShiftSummary.TotalSales) underreports DailyReport totals by $25,000/year

Fawry double-count risk: 2,000 Fawry orders × avg $30 = $60,000
If user adds Cash+Card+Fawry columns, $60,000 phantom revenue appears
```

### 5.2 Historical Stability Test

**Question:** Does running the P&L report for January 2026 in March 2026 produce the same result as running it in January 2026?

| Report                   | Same Result?           | Why?                                                        |
| ------------------------ | ---------------------- | ----------------------------------------------------------- |
| **P&L**                  | ✅ YES                 | UnitCost snapshot; status filter includes PartiallyRefunded |
| **SalesReport**          | ✅ YES                 | Uses Order.Total (immutable after completion)               |
| **DailyReport**          | ✅ YES                 | Shift-based, closed shifts are immutable                    |
| **ProductMovement**      | ❌ NO                  | Uses current Product.Cost/AverageCost                       |
| **ProductProfitability** | ❌ NO                  | Same issue                                                  |
| **COGS Report**          | ❌ NO                  | Uses current costs + current inventory                      |
| **EmployeeSales**        | ❌ NO (status changes) | Orders refunded after Jan get excluded                      |
| **TopCustomers**         | ❌ NO (status changes) | Same                                                        |

**5 out of 8 report types retroactively change** when run for historical periods.

### 5.3 Invariant Violation Accumulation Rate

| Invariant                | Violation Rate             | 12-Month Cumulative Impact |
| ------------------------ | -------------------------- | -------------------------- |
| INV-5 (Product Revenue)  | Every refund               | −15-20% revenue gap        |
| INV-6 (Product COGS)     | Every cost update          | ±10-30% COGS drift         |
| INV-7 (P&L Discounts)    | Every item discount        | $9K+ overstatement         |
| INV-8 (Payments)         | Every Fawry order          | $60K double-count risk     |
| INV-9 (Employee Revenue) | Every refund               | −15-20% underreport        |
| INV-12 (Shift Sum)       | Every credit sale          | $25K gap                   |
| INV-13 (COGS Report)     | Every cost update + refund | Triple divergence          |

---

## 6. Reconciliation Summary

### 6.1 Invariant Status

| ID     | Invariant                        | Status                                                             | Severity |
| ------ | -------------------------------- | ------------------------------------------------------------------ | -------- |
| INV-1  | DailyReport = SalesReport totals | ❌ FAILS (cross-midnight shifts)                                   | HIGH     |
| INV-2  | P&L Revenue correctness          | ✅ HOLDS                                                           | —        |
| INV-3  | P&L COGS from snapshots          | ✅ HOLDS                                                           | —        |
| INV-4  | P&L Gross Profit formula         | ⚠️ HOLDS algebraically, but input (netSales) is wrong due to INV-7 | CRITICAL |
| INV-5  | Product Revenue = P&L Revenue    | ❌ FAILS (status filter + no returns)                              | CRITICAL |
| INV-6  | Product COGS = P&L COGS          | ❌ FAILS (cost source divergence)                                  | CRITICAL |
| INV-7  | Discount completeness            | ❌ FAILS (P&L missing item discounts)                              | CRITICAL |
| INV-8  | Payment decomposition            | ❌ FAILS (Fawry double-count + employee hardcoded)                 | CRITICAL |
| INV-9  | Employee Revenue = Total Sales   | ❌ FAILS (status filter gap)                                       | CRITICAL |
| INV-10 | Customer Revenue consistency     | ❌ FAILS (status filter gap)                                       | HIGH     |
| INV-11 | Daily breakdown summation        | ✅ HOLDS                                                           | —        |
| INV-12 | Shift sum = Daily total          | ❌ FAILS (credit sales + payment vs total)                         | HIGH     |
| INV-13 | COGS Report = P&L COGS           | ❌ FAILS (3 dimensions)                                            | CRITICAL |
| INV-14 | Product Profit sum = P&L Profit  | ❌ FAILS (follows from INV-5, INV-6)                               | CRITICAL |
| INV-15 | Inventory movement balance       | ⚠️ CIRCULAR (no real verification)                                 | MEDIUM   |

**Score: 3 HOLD, 9 FAIL, 2 CONDITIONAL/CIRCULAR, 1 TAINTED**

### 6.2 Issue Ranking by Business Impact

#### CRITICAL — Reports Produce Contradictory Financial Numbers

| #        | Issue                                                                                                        | Impact                                                                                                      | Affected Reports                                                                           |
| -------- | ------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| **RC-1** | Status filter inconsistency: Product/Employee/Customer reports exclude PartiallyRefunded and Refunded orders | Revenue, COGS, and profit diverge from P&L by the total value of all refunded orders                        | ProductMovement, ProductProfitability, COGS, EmployeeSales, TopCustomers, CustomerActivity |
| **RC-2** | P&L missing item-level discounts in totalDiscount                                                            | Net sales and gross profit overstated by the sum of all item discounts across all orders                    | P&L Report                                                                                 |
| **RC-3** | Product reports use current Product.Cost instead of OrderItem.UnitCost for COGS                              | Product COGS changes retroactively with every purchase invoice; historical reports are unstable             | ProductMovement, ProductProfitability, COGS Report                                         |
| **RC-4** | Fawry double-counting in shift summaries                                                                     | ShiftSummary.TotalCard includes Fawry; separate TotalFawry field exists; summing all columns inflates total | DailyReport shift summaries                                                                |
| **RC-5** | CashierPerformance hardcodes cardSales=0, fawrySales=0                                                       | Employee payment method breakdown is completely nonfunctional                                               | CashierPerformance Report                                                                  |

#### HIGH — Reports Conflict Under Common Conditions

| #        | Issue                                                                          | Impact                                                     |
| -------- | ------------------------------------------------------------------------------ | ---------------------------------------------------------- |
| **RH-1** | DailyReport (shift-based) vs SalesReport (order-based) date scoping            | Cross-midnight shifts cause different totals for same date |
| **RH-2** | ShiftSummary.TotalSales = payments made, DailyReport.TotalSales = order totals | Credit sales create gap equal to unpaid amounts            |
| **RH-3** | Product reports exclude return orders entirely                                 | No return deduction even if status filter is fixed         |
| **RH-4** | Employee/Customer reports become retroactively incorrect                       | Refund processed in March changes January's report         |

#### MEDIUM — Reduced Accuracy

| #        | Issue                                                                       | Impact                                                |
| -------- | --------------------------------------------------------------------------- | ----------------------------------------------------- |
| **RM-1** | Shift detail report hardcodes TotalFawry = 0                                | Fawry payments invisible in shift-level view          |
| **RM-2** | Inventory opening stock is reverse-calculated                               | No independent verification possible; hides shrinkage |
| **RM-3** | P&L AverageOrderValue uses refund-adjusted revenue / non-return order count | Metric is artificially depressed                      |
| **RM-4** | Customer Activity segment TotalOrders counts all orders for both segments   | Misleading segmentation stats                         |

#### LOW — Edge Cases

| #        | Issue                                                  | Impact                                                |
| -------- | ------------------------------------------------------ | ----------------------------------------------------- |
| **RL-1** | Thermal printer uses toFixed(2) vs Intl.NumberFormat   | Penny rounding difference on receipts                 |
| **RL-2** | Slow-moving report uses Product.Price as cost fallback | Overstates stock value for products without cost data |

### 6.3 Mathematical Proof: Current System Cannot Self-Reconcile

Given the current formulas, no set of transactions can satisfy ALL 15 invariants simultaneously when:

- At least 1 order has an item-level discount, AND
- At least 1 order has been partially refunded

**Proof:**

1. Item discount → INV-7 breaks (P&L discount incomplete)
2. Partial refund → INV-5, INV-9, INV-10 break (status filter divergence)
3. Both together → P&L shows inflated Net Sales (INV-7) while Product/Employee/Customer reports show deflated Revenue (INV-5/9/10)
4. The system has **opposite directional errors** in different reports — one set overstates, the other understates
5. No correction factor can resolve this because the errors compound per-transaction

**Conclusion:** Under normal business operations (discounts + refunds), the reporting system is **provably inconsistent** across reports.

### 6.4 Minimum Fix Set Required for Mathematical Consistency

To make all 15 invariants hold simultaneously, the following minimum changes are required:

| Fix                                                                                       | Resolves                     |
| ----------------------------------------------------------------------------------------- | ---------------------------- |
| Add PartiallyRefunded + Refunded to all report status filters                             | INV-5, INV-9, INV-10         |
| Add return order queries and subtraction to Product/Employee/Customer reports             | INV-5, INV-9, INV-10, INV-14 |
| Add item-level discounts to P&L totalDiscount                                             | INV-7, INV-4                 |
| Replace Product.Cost with OrderItem.UnitCost in all Product report COGS                   | INV-6, INV-13, INV-14        |
| Fix Shift.TotalCard to exclude Fawry; or remove TotalFawry from shift summary             | INV-8                        |
| Compute cashier card/fawry from Payment records                                           | INV-8                        |
| Use Order.Total instead of Payment sums for shift TotalSales (or document the difference) | INV-12                       |

**7 code changes** resolve **12 out of 15 invariants**. The remaining 3 (INV-1 cross-midnight, INV-12 credit sales, INV-15 circular inventory) require architectural decisions.
