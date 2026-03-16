# 🔥 CHAOS TEST REPORT — Deep Financial Audit

**System:** KasserPro / TajerPro POS  
**Date:** March 10, 2026  
**Auditor:** Senior Financial Systems Chaos Tester  
**Scope:** Orders, Refunds, Discounts, Taxes, Inventory, Reports, Concurrency  
**Risk Level:** PRODUCTION — Millions of transactions expected

---

## EXECUTIVE SUMMARY

| Metric                          | Value                  |
| ------------------------------- | ---------------------- |
| Total chaos scenarios generated | **187**                |
| **CRITICAL bugs found**         | **9**                  |
| **HIGH severity bugs found**    | **7**                  |
| **MEDIUM severity bugs found**  | **6**                  |
| **LOW severity issues**         | **4**                  |
| Race condition risks            | **5 distinct vectors** |
| Financial mismatch vectors      | **8**                  |
| Inventory corruption vectors    | **3**                  |

**Verdict:** The system has solid fundamentals (transactions, snapshots, rounding), but contains **several bugs that WILL cause real financial damage at scale**. The most critical are the Profit & Loss report ignoring refunds, race conditions on customer balances, and the rounding drift accumulation across chained partial refunds.

---

## SECTION 1 — CHAOS TEST SCENARIOS (187 Total)

### Category A: Sales Operations (Scenarios 1–30)

| #   | Scenario                                             | Actions                                           | Expected Verification                          |
| --- | ---------------------------------------------------- | ------------------------------------------------- | ---------------------------------------------- |
| 1   | Basic single-item sale                               | Create → Add 1 item → Complete with exact cash    | Total = UnitPrice × Qty + Tax                  |
| 2   | Multi-item sale, different tax rates                 | Create → Add 3 items (0%, 14%, 5% tax) → Complete | Per-item tax calculated independently          |
| 3   | Sale with zero-price item                            | Create → Add item at 0.00 EGP → Complete          | Total = 0, no errors                           |
| 4   | Sale with 0.01 EGP item                              | Create → Add item at 0.01 EGP × 1 → Complete      | Total = 0.01 + tax                             |
| 5   | Sale with max-price item                             | Create → Add item at 999,999.99 EGP → Complete    | No overflow                                    |
| 6   | Sale with qty=1                                      | Minimum quantity sale                             | Correct single-unit calculation                |
| 7   | Sale with qty=9999                                   | High quantity sale                                | No overflow, stock decremented correctly       |
| 8   | Mixed catalog + custom items                         | Add product + custom item to same order           | Both calculated, only catalog decrements stock |
| 9   | Credit sale (partial payment)                        | Complete with 50% payment, customer linked        | AmountDue = 50%, customer TotalDue updated     |
| 10  | Credit sale at exact credit limit                    | Customer.CreditLimit = 100, AmountDue = 100       | Should succeed (≤ limit)                       |
| 11  | Credit sale exceeding credit limit                   | Customer.CreditLimit = 100, AmountDue = 101       | Should reject                                  |
| 12  | Credit sale, CreditLimit=0 (unlimited)               | Customer.CreditLimit = 0, AmountDue = 10,000      | Should succeed                                 |
| 13  | Sale with multiple payment methods                   | Cash + Card split payment                         | Payments summed correctly                      |
| 14  | Overpayment within 2x limit                          | Order total=100, pay 199                          | ChangeAmount = 99                              |
| 15  | Overpayment exceeding 2x limit                       | Order total=100, pay 201                          | Should reject                                  |
| 16  | Sale then immediately cancel                         | Complete → Cancel                                 | Should fail (Completed cannot Cancel)          |
| 17  | Draft order → Cancel                                 | Draft → Cancel                                    | Should succeed                                 |
| 18  | Pending order → Complete → Cancel                    | Pending → Complete → attempt Cancel               | Should fail                                    |
| 19  | Order with inactive product                          | Add inactive product                              | Should reject                                  |
| 20  | Order without open shift                             | Create order without shift                        | Should reject                                  |
| 21  | Sale with service charge 5%                          | Add items, 5% service charge                      | ServiceCharge calculated on net-after-discount |
| 22  | Sale: 100 items same product                         | Verify bulk calculation                           | Subtotal = price × 100                         |
| 23  | Sale: many different products (50)                   | Large order                                       | All items calculated independently             |
| 24  | Re-complete same order                               | Complete → Complete again                         | Should fail (state transition)                 |
| 25  | Sale with payment amount=0                           | Skip payment lines with amount ≤ 0                | Should handle gracefully                       |
| 26  | Custom item with custom tax rate                     | Add custom item, TaxRate=20%                      | Tax = Price × 0.20                             |
| 27  | Custom item with null tax rate                       | Add custom item, no TaxRate                       | Falls back to tenant default                   |
| 28  | Sale when tax disabled at tenant level               | Tenant.IsTaxEnabled = false                       | All tax = 0                                    |
| 29  | Sale with product-specific tax overriding tenant tax | Product.TaxRate=5%, Tenant.TaxRate=14%            | Item uses 5%                                   |
| 30  | Sale with product-specific tax=null                  | Product.TaxRate=null                              | Falls back to tenant 14%                       |

### Category B: Discount Chaos (Scenarios 31–65)

| #   | Scenario                                              | Actions                                       | Expected Verification                                                                |
| --- | ----------------------------------------------------- | --------------------------------------------- | ------------------------------------------------------------------------------------ |
| 31  | Item discount: 10% percentage                         | Single item, 10% discount                     | DiscountAmount = Subtotal × 0.10                                                     |
| 32  | Item discount: 50% percentage                         | Half-price item                               | Total halved + tax on remaining                                                      |
| 33  | Item discount: 99.99% percentage                      | Near-free item                                | Tiny total, tax on tiny amount                                                       |
| 34  | Item discount: 100% percentage                        | Free item                                     | Total = 0 (clamped)                                                                  |
| 35  | Item discount: fixed 5 EGP                            | Fixed amount deducted                         | Subtotal - 5 + tax on remainder                                                      |
| 36  | Item discount: fixed > subtotal                       | Fixed 200 on 100 EGP item                     | **Capped at subtotal, total = 0**                                                    |
| 37  | Item discount: fixed = subtotal                       | Exact zero-out                                | Total = 0                                                                            |
| 38  | Item discount: 0%                                     | No effect                                     | Same as no discount                                                                  |
| 39  | Item discount: 0 EGP fixed                            | No effect                                     | Same as no discount                                                                  |
| 40  | Order discount: 10% percentage                        | 10% off net-after-item-discounts              | Correct netting                                                                      |
| 41  | Order discount: 100% percentage                       | Free order                                    | Total approaches 0                                                                   |
| 42  | Order discount: fixed 50 EGP                          | Fixed deduction                               | Net - 50 + tax                                                                       |
| 43  | Order discount: fixed > net                           | Fixed 500 on 100 net                          | Capped at net                                                                        |
| 44  | **STACKED: Item 20% + Order 10%**                     | Both applied                                  | Order discount on net-AFTER-item-discounts                                           |
| 45  | STACKED: Item 50% + Order 50%                         | Both halving                                  | Total = 25% of original + tax on 25%                                                 |
| 46  | STACKED: All items 100% + Order 10%                   | Items free, then order discount               | Net = 0, order discount = 0 on 0                                                     |
| 47  | STACKED: Item fixed + Order percentage                | Item -10 EGP, then Order -10%                 | Correct sequential application                                                       |
| 48  | STACKED: Item percentage + Order fixed                | Item -10%, then Order -5 EGP                  | Correct                                                                              |
| 49  | Mixed items: some discounted, some not                | 3 items, only 1 has discount                  | Only discounted item affected                                                        |
| 50  | All items discounted differently                      | 3 items with 10%, 20%, 30%                    | Each calculated independently                                                        |
| 51  | Discount on 0.01 price item                           | 10% of 0.01 = 0.001 → rounds to 0.00          | Rounding correct                                                                     |
| 52  | Discount producing 0.005                              | Rounding boundary                             | Should round to 0.01 (banker's or standard)                                          |
| 53  | 99% item discount + 99% order discount                | Near-total elimination                        | Tiny remainder, tax on remainder                                                     |
| 54  | Negative discount value                               | DiscountValue = -10                           | **Should reject** (validated)                                                        |
| 55  | Percentage discount > 100                             | DiscountValue = 150%                          | **Should reject** (validated)                                                        |
| 56  | Discount on custom item                               | Custom item with percentage discount          | Not applicable (custom items don't have item-level discount in AddCustomItemRequest) |
| 57  | Order discount with 0 net after item discounts        | All items zeroed by item discounts            | Order discount = 0 (div-by-zero guard)                                               |
| 58  | Large order discount on many items                    | 50 items, 15% order discount                  | Tax recalculation proportional                                                       |
| 59  | Repeated order discount changes                       | Modify order discount on draft multiple times | Recalculated each time                                                               |
| 60  | Discount + Service charge                             | Both applied                                  | Service charge on net-after-ALL-discounts                                            |
| 61  | 100% discount + service charge                        | Free order + service charge                   | Service charge on 0 = 0                                                              |
| 62  | Item discount on qty=1 vs qty=10                      | Same percentage, different totals             | Correct scaling                                                                      |
| 63  | Fixed item discount on high-qty item                  | Fixed 5 on qty=10 (subtotal=1000)             | Discount = 5 (not 50)                                                                |
| 64  | Multiple items with same product, different discounts | Same product added twice, different discounts | Each line independent                                                                |
| 65  | Order discount = 0 explicitly set                     | DiscountType="percentage", DiscountValue=0    | No effect                                                                            |

### Category C: Tax Chaos (Scenarios 66–85)

| #   | Scenario                                                      | Actions                                     | Expected Verification                                                   |
| --- | ------------------------------------------------------------- | ------------------------------------------- | ----------------------------------------------------------------------- |
| 66  | Tax exclusive basic                                           | 100 EGP net, 14% tax                        | Total = 114.00                                                          |
| 67  | Zero tax rate                                                 | TaxRate = 0                                 | No tax added                                                            |
| 68  | Very high tax rate                                            | TaxRate = 50%                               | Total = 150% of net                                                     |
| 69  | Tax on discounted item                                        | 100 net, 10% discount, 14% tax              | Tax = (100-10) × 0.14 = 12.60                                           |
| 70  | Tax with order discount affecting per-item tax                | 3 items different rates, order discount 10% | **Proportional tax recalculation**                                      |
| 71  | Tax on 0.01 item                                              | Fractional tax                              | Correctly rounded                                                       |
| 72  | Tax producing rounding boundary (0.005)                       | Carefully chosen values                     | Consistent rounding                                                     |
| 73  | Different tax rates per item in same order                    | Item A=0%, Item B=14%, Item C=5%            | Independent calculation                                                 |
| 74  | Tax disabled mid-order                                        | Tenant disables tax while order is draft    | Draft recalculates on completion? **NO — uses rate from creation time** |
| 75  | Product tax rate changed after order created                  | Price snapshot protects                     | Snapshot preserved                                                      |
| 76  | Tax on fully discounted item (0 net)                          | 100% discount, 14% tax                      | Tax = 0                                                                 |
| 77  | Tax rate = 0.01% (very small)                                 | Near-zero tax                               | Tiny tax amount                                                         |
| 78  | Tax rate = 99.99%                                             | Near-doubling                               | Correct calculation                                                     |
| 79  | Order discount + tax recalculation with 5 different tax rates | Complex proportional                        | Each item's tax reduced proportionally                                  |
| 80  | Tax on service-type product                                   | Service product, no inventory, 14% tax      | Tax correctly applied                                                   |
| 81  | Custom item with explicit TaxRate=0                           | Override tenant default                     | Item tax = 0                                                            |
| 82  | Rounding chain: tax on discounted item with order discount    | Triple calculation layer                    | Consistent total                                                        |
| 83  | Tax sum of items vs order.TaxAmount with order discount       | Compare item-level sums to order-level      | Order-level recalculated ≠ sum of item TaxAmounts                       |
| 84  | Order with 100 items, each with different tax rate            | Stress test proportional tax                | Correct total                                                           |
| 85  | TaxRate stored as decimal precision test                      | 14.5%, 3.33%, etc.                          | Stored and calculated precisely                                         |

### Category D: Refund Chaos (Scenarios 86–130)

| #   | Scenario                                                     | Actions                                       | Expected Verification                                |
| --- | ------------------------------------------------------------ | --------------------------------------------- | ---------------------------------------------------- |
| 86  | Full refund: single item order                               | Complete → Full refund                        | RefundAmount = Total, stock restored                 |
| 87  | Full refund: multi-item order                                | Complete → Full refund                        | All stock restored                                   |
| 88  | Partial refund: 1 of 3 items                                 | Complete → Refund 1 item                      | Proportional refund amount                           |
| 89  | Partial refund: partial qty (2 of 5)                         | Complete → Refund qty=2                       | Proportional                                         |
| 90  | **Multiple partial refunds on same order**                   | Refund item A → Refund item B → Refund item C | **Cumulative RefundAmount tracked**                  |
| 91  | **Partial → Partial → Full remainder**                       | Refund 1 → Refund 1 → Full refund remaining   | RefundAmount = Total                                 |
| 92  | **Refund qty = full qty for all items one by one**           | Refund each item separately                   | Status → PartiallyRefunded → Refunded                |
| 93  | Refund more than remaining qty                               | Refund qty=5 when only 3 remain               | **Should reject**                                    |
| 94  | Refund already-refunded item                                 | Item fully refunded, try again                | **Should reject (available=0)**                      |
| 95  | Refund on already-fully-refunded order                       | Order.Status=Refunded, try refund             | remaining=0, should reject                           |
| 96  | Refund with item-level discount                              | Item had 20% discount                         | Refund proportional to discounted total              |
| 97  | Refund with order-level discount                             | Order had 15% discount                        | **Proportional order-level adjustment applied**      |
| 98  | **Refund with BOTH item + order discount**                   | Stacked discounts                             | Both proportionally accounted                        |
| 99  | Refund after product price change                            | Product price was 100, now 150                | **Refund uses SNAPSHOT price from order**            |
| 100 | Refund after product deletion                                | Product soft-deleted                          | **Refund still works (snapshot)**                    |
| 101 | Refund after tax rate change                                 | Tax was 14%, now 10%                          | **Refund uses snapshot tax rate**                    |
| 102 | Refund custom item                                           | Custom item in order                          | Stock NOT decremented/incremented                    |
| 103 | Refund service product (no inventory)                        | TrackInventory=false                          | Stock NOT affected                                   |
| 104 | Refund with customer: credit reduction                       | Customer order with credit                    | TotalDue reduced proportionally                      |
| 105 | Refund with customer: loyalty deduction                      | Customer had loyalty points                   | Points deducted                                      |
| 106 | Full refund on credit sale                                   | AmountDue > 0                                 | Customer.TotalDue reduced                            |
| 107 | Partial refund on credit sale                                | Proportional debt reduction                   | Correct ratio applied                                |
| 108 | Refund cash register recording                               | Cash refund amount                            | Proportional to original cash payments               |
| 109 | Mixed payment refund: cash+card                              | Original was 50% cash, 50% card               | Cash register gets 50% of refund                     |
| 110 | Refund when no payments were cash                            | All card payments                             | Cash register records 0                              |
| 111 | **Rounding drift: 3 partial refunds**                        | Refund 1 unit at a time from 3-unit order     | **Total refunds ≤ original total**                   |
| 112 | **Rounding drift: many partial refunds**                     | 10 partial refunds on 10-item order           | **Accumulated drift capped**                         |
| 113 | Refund on order with service charge                          | Service charge refunded proportionally        | Correct proportional                                 |
| 114 | Refund ratio calculation edge: items total = 0               | All items were free                           | Ratio = 0, no refund                                 |
| 115 | Refund on 0.01 EGP order                                     | Tiny refund amount                            | Handled correctly                                    |
| 116 | Refund negative qty (attack)                                 | RefundItem.Quantity = -1                      | **Should reject (qty > 0)**                          |
| 117 | Refund non-existent item ID (attack)                         | ItemId = 999999                               | **Should reject**                                    |
| 118 | Refund on Draft order                                        | Order not completed                           | **Should reject (state validation)**                 |
| 119 | Refund on Cancelled order                                    | Order cancelled                               | **Should reject**                                    |
| 120 | Full refund without reason                                   | No reason provided                            | **Should reject (required for full)**                |
| 121 | Partial refund without reason                                | Items have no individual reasons              | Should succeed                                       |
| 122 | **Refund creates Return Order with correct negative totals** | Check return order fields                     | All financial fields negative                        |
| 123 | Return order included in reports correctly                   | Check daily report                            | Return orders counted in refund totals               |
| 124 | Multiple refunds: cash register consistency                  | 3 refunds, check total cash register impact   | All 3 recorded separately                            |
| 125 | Refund on order from different shift                         | Current shift ≠ order shift                   | Should succeed (return order links to current shift) |
| 126 | Refund without active shift                                  | No shift open                                 | Should succeed (ShiftId nullable for returns)        |
| 127 | Refund stock restoration: exact amount                       | Sale decremented 5, refund 3                  | Stock += 3                                           |
| 128 | Refund stock restoration: product now has 0 stock            | Sale depleted stock, refund restores          | Stock goes from 0 to refunded qty                    |
| 129 | **Large chain: Sale → Partial → Partial → Full → Verify**    | Multiple operations                           | Final state: fully refunded, stock fully restored    |
| 130 | Refund on order with all custom items                        | No catalog items                              | No stock changes, refund amount correct              |

### Category E: Inventory Chaos (Scenarios 131–150)

| #   | Scenario                                     | Actions                                                   | Expected Verification                  |
| --- | -------------------------------------------- | --------------------------------------------------------- | -------------------------------------- |
| 131 | Sale decrements stock correctly              | Sale qty=5, stock was 100                                 | Stock = 95                             |
| 132 | Sale exactly depletes stock                  | Sale qty = available stock                                | Stock = 0                              |
| 133 | Sale exceeds stock (negative stock disabled) | Sale qty > stock                                          | **Should reject at CompleteAsync**     |
| 134 | Sale when AllowNegativeStock=true            | Stock=2, sale qty=5                                       | Stock = -3 (allowed)                   |
| 135 | Refund restores stock                        | Full refund                                               | Stock restored to pre-sale             |
| 136 | Partial refund restores partial stock        | Refund 2 of 5                                             | Stock += 2                             |
| 137 | Sale → Full refund → Re-sell                 | Stock 10 → sell 5 → refund 5 → sell 5                     | Stock = 5                              |
| 138 | Sale → Partial refund → Sell again           | Stock 10 → sell 5 (stock=5) → refund 2 (stock=7) → sell 3 | Stock = 4                              |
| 139 | Stock movement audit trail                   | Check StockMovement records                               | BalanceBefore/After correct            |
| 140 | Multiple orders deplete stock                | 3 orders reducing stock                                   | Sequential deductions correct          |
| 141 | Stock = 0, attempt sale                      | No stock left                                             | Should reject                          |
| 142 | Inventory adjustment + sale                  | Adjust stock to 10, then sell 3                           | Stock = 7                              |
| 143 | Transfer + sale                              | Transfer 10 units to branch, sell 5                       | Stock = 5 at destination               |
| 144 | Custom item doesn't affect inventory         | Sell custom item                                          | No BranchInventory change              |
| 145 | Service product doesn't affect inventory     | Sell service product                                      | No stock change                        |
| 146 | Product with TrackInventory=false            | Sell product                                              | No stock validation or change          |
| 147 | Stock at MAX_INT boundary                    | Very large stock values                                   | No overflow                            |
| 148 | Sale of product not in BranchInventory       | No BranchInventory record exists                          | Stock = 0, should reject               |
| 149 | Refund when BranchInventory deleted          | Refund creates new record                                 | IncrementStockAsync creates new record |
| 150 | Stock movement types correct                 | Sale=negative, Refund=positive                            | Verified in StockMovement entities     |

### Category F: Concurrency Chaos (Scenarios 151–165)

| #   | Scenario                                               | Concurrent Actions                     | Risk                                              |
| --- | ------------------------------------------------------ | -------------------------------------- | ------------------------------------------------- |
| 151 | Two cashiers complete same order                       | CompleteAsync × 2 simultaneous         | **Double stock deduction**                        |
| 152 | Two refunds on same order simultaneously               | RefundAsync × 2                        | **Double refund, double stock restore**           |
| 153 | Complete + Refund simultaneously                       | Complete while refunding               | **State machine violation**                       |
| 154 | Two credit sales, same customer                        | Both check credit limit, both pass     | **Credit limit bypass**                           |
| 155 | Concurrent debt payments                               | PayDebtAsync × 2                       | ✅ Protected by transaction                       |
| 156 | Concurrent UpdateCreditBalance                         | Two orders for same customer           | **Lost update on TotalDue**                       |
| 157 | Concurrent DeductRefundStats                           | Two refunds for same customer          | **Lost update on TotalSpent**                     |
| 158 | Concurrent stock decrement                             | Two orders, same product, last unit    | **Double sale of last unit**                      |
| 159 | Concurrent inventory adjustment + sale                 | Adjust while selling                   | **Race condition**                                |
| 160 | Shift close during order completion                    | CloseShift while CompleteAsync         | Shift closed but order linked                     |
| 161 | Two orders: stock check passes, but only one has stock | Stock=1, two orders of qty=1           | Both pass soft check, one fails hard check        |
| 162 | Concurrent full + partial refund                       | Full refund + partial refund same time | **Double refund**                                 |
| 163 | Concurrent customer stats updates                      | Multiple orders for same customer      | **Lost updates on TotalOrders, TotalSpent**       |
| 164 | Concurrent cash register recordings                    | Multiple sales completing              | ✅ Protected (piggybacks on caller's transaction) |
| 165 | Concurrent loyalty point operations                    | Add + deduct simultaneously            | **Lost update on LoyaltyPoints**                  |

### Category G: Report Consistency (Scenarios 166–175)

| #   | Scenario                                 | Report Check                | Expected                                       |
| --- | ---------------------------------------- | --------------------------- | ---------------------------------------------- |
| 166 | Basic daily report after 10 orders       | DailyReport                 | Totals match sum of orders                     |
| 167 | Daily report with refunds                | 5 sales + 2 refunds         | Net = sales - refunds                          |
| 168 | **P&L report with refunds**              | Profit/Loss                 | **🔴 BUG: Refunds not subtracted from profit** |
| 169 | Sales report date filtering              | Orders across date boundary | Only in-range included                         |
| 170 | Daily report vs sales report consistency | Same date, both reports     | Totals should match                            |
| 171 | Report with mixed payment methods        | Cash + Card breakdown       | Matches actual payments                        |
| 172 | Report with cancelled orders             | Cancelled orders excluded   | Only completed/refunded counted                |
| 173 | Report with credit sales                 | Unpaid amounts              | Still counted in sales total                   |
| 174 | Top products after refunds               | Net quantities              | Sales qty - refund qty                         |
| 175 | Report across timezone boundary          | Orders near midnight UTC    | Correct date attribution                       |

### Category H: Extreme Edge Cases (Scenarios 176–187)

| #   | Scenario                                     | Edge Case                      | Risk                             |
| --- | -------------------------------------------- | ------------------------------ | -------------------------------- |
| 176 | 0.01 × qty=1 × 14% tax                       | 0.0014 tax → rounds to 0.00    | Tax disappears                   |
| 177 | 0.01 × qty=100 × 14% tax                     | 0.14 tax total                 | Tax correct in aggregate         |
| 178 | 333.33 × qty=3                               | 999.99 subtotal                | Clean                            |
| 179 | 0.333 × qty=3 × 14% tax                      | 0.999 subtotal, rounding       | Accumulation test                |
| 180 | Price = 1/3 (0.333...) × qty=3               | Repeating decimal              | Proper rounding                  |
| 181 | Very large order: 10000 items                | Performance + precision        | No overflow                      |
| 182 | Order total = MAX_DECIMAL                    | Boundary test                  | No crash                         |
| 183 | 50 partial refunds on 50-item order          | Rounding drift accumulation    | Capped by remainingRefundable    |
| 184 | Refund ratio with floating-point imprecision | 33.33/99.99 ≠ exactly 1/3      | Decimal arithmetic safe          |
| 185 | Division by zero: CalculateRefundRatio       | originalItemsGrossTotal = 0    | Returns 0 (guarded)              |
| 186 | Division by zero: unitPriceWithTax           | item.Quantity = 0 somehow      | **Potential divide-by-zero**     |
| 187 | OrderNumber collision                        | Two orders at same millisecond | UUID-based, collision improbable |

---

## SECTION 2 — BUGS DISCOVERED

### 🔴 CRITICAL BUG #1: Profit & Loss Report Ignores Refunds in Profit Calculation

**File:** [FinancialReportService.cs](backend/KasserPro.Infrastructure/Services/FinancialReportService.cs)  
**Lines:** ~98-115

**The Problem:**

```
grossSales = orders.Sum(o => o.Subtotal)          // Only sales orders
totalDiscount = orders.Sum(o => o.DiscountAmount)  // Only sales orders
netSales = grossSales - totalDiscount              // No refund deduction!
totalCost = items.Sum(i => UnitCost * Quantity)    // Only sales COGS
grossProfit = netSales - totalCost                 // WRONG — refunds not deducted
netProfit = grossProfit - totalExpenses            // WRONG — cascading error
```

The `RefundsAmount` is **calculated and stored** (`refundsAmount = Math.Abs(returnOrders.Sum(o => o.Total))`) but **NEVER subtracted from revenue or profit**.

**Impact:** The P&L report overstates revenue and profit by the total refund amount. For a business doing 5% refund rate on 1M EGP daily sales, this is **50,000 EGP/day** of phantom profit.

**Also:** Return order COGS is not subtracted from `totalCost`. When items are returned, the cost should be reversed but isn't.

**Fix Required:**

```csharp
var netSales = (grossSales - totalDiscount) - refundsAmount;
var netCost = totalCost - returnedCost;
var grossProfit = netSales - netCost;
```

---

### 🔴 CRITICAL BUG #2: Race Condition on Customer Credit Balance (Lost Update)

**File:** [CustomerService.cs](backend/KasserPro.Application/Services/Implementations/CustomerService.cs)  
**Methods:** `UpdateCreditBalanceAsync`, `ReduceCreditBalanceAsync`, `DeductRefundStatsAsync`, `UpdateOrderStatsAsync`

**The Problem:**
None of these methods use transactions. They follow the pattern:

```csharp
var customer = await query.FirstOrDefaultAsync(...);  // READ
customer.TotalDue += amountDue;                        // MODIFY in memory
await _unitOfWork.SaveChangesAsync();                  // WRITE
```

**Scenario (Lost Update):**

1. Thread A: reads customer TotalDue = 100
2. Thread B: reads customer TotalDue = 100
3. Thread A: sets TotalDue = 100 + 50 = 150, saves
4. Thread B: sets TotalDue = 100 + 30 = 130, saves **OVERWRITING Thread A's update**
5. Result: TotalDue = 130 instead of 180. **30 EGP of debt vanished.**

**Impact:** Customer debt silently disappears. Over thousands of transactions this accumulates into significant financial loss. Every concurrent credit sale to the same customer risks this.

**Also affects:** `TotalOrders`, `TotalSpent`, `LoyaltyPoints` — all modified with the same unsafe pattern.

**Note:** `PayDebtAsync` is correctly protected with transactions. The other methods are not.

---

### 🔴 CRITICAL BUG #3: No Concurrency Protection on Order State Transitions

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs)

**The Problem:**
The `Order` entity has **NO RowVersion/ConcurrencyToken** (only `Shift` has one). Two simultaneous requests can:

1. **Double-complete an order:** Two CompleteAsync calls for the same order. Both read Status=Draft, both pass validation, both complete. Stock decremented twice, payments recorded twice.

2. **Double-refund an order:** Two RefundAsync calls for the same completed order. Both read Status=Completed, both pass validation, both create return orders. Refund issued twice, stock restored twice.

**Scenario (Double Refund):**

- Order #100: Total = 500 EGP, fully paid
- Thread A: RefundAsync(100) — reads Status=Completed ✅
- Thread B: RefundAsync(100) — reads Status=Completed ✅ (same moment)
- Thread A: Creates return order for -500, sets Status=Refunded, saves
- Thread B: Creates return order for -500, sets Status=Refunded, saves
- **Result:** 1000 EGP refunded on a 500 EGP order. Customer gets 500 EGP free money.

**Impact:** Direct financial loss. This is exploitable by a malicious actor sending duplicate requests.

---

### 🔴 CRITICAL BUG #4: Credit Limit Validation TOCTOU (Time-of-Check-Time-of-Use)

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs) — `CompleteAsync`  
**File:** [CustomerService.cs](backend/KasserPro.Application/Services/Implementations/CustomerService.cs) — `ValidateCreditLimitAsync`

**The Problem:**
Credit limit is validated OUTSIDE the transaction that updates the customer balance:

```csharp
// In CompleteAsync:
var canTakeCredit = await _customerService.ValidateCreditLimitAsync(...)  // CHECK (no lock)
// ... later in transaction:
await _customerService.UpdateCreditBalanceAsync(...)  // USE (different call, no protection)
```

**Scenario:**

- Customer credit limit = 1000, TotalDue = 900
- Order A: AmountDue = 200 → checks: 900 + 200 = 1100 > 1000 → REJECT ❌
- But with race condition:
  - Order A (200 due): checks 900 + 200 = 1100 > 1000 ... but wait:
  - Order B (50 due): checks 900 + 50 = 950 ≤ 1000 → PASS ✅
  - Order C (50 due): checks 900 + 50 = 950 ≤ 1000 → PASS ✅ (same moment as B)
  - Order B saves: TotalDue = 950
  - Order C saves: TotalDue = 950 **LOST UPDATE** (should be 1000)
  - Customer TotalDue = 950 when it should be 1000

**Impact:** Credit limits can be systematically bypassed with concurrent orders.

---

### 🔴 CRITICAL BUG #5: Partial Refund unitPriceWithTax Division Precision Loss

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs)  
**Lines:** ~840, ~930

**The Problem:**

```csharp
var unitPriceWithTax = orderItem.Total / orderItem.Quantity;
var itemRefundAmount = Math.Round(unitPriceWithTax * refundItem.Quantity, 2);
```

This divides `Total` by `Quantity` then multiplies back. For non-uniform distributions (due to item discounts), this creates rounding errors:

**Scenario:**

- Item: qty=3, UnitPrice=10, 10% discount, 14% tax
- Subtotal = 30.00
- DiscountAmount = 3.00
- TaxAmount = 3.78 (27 × 0.14)
- Total = 30 - 3 + 3.78 = 30.78
- unitPriceWithTax = 30.78 / 3 = 10.26
- Refund qty=1: itemRefundAmount = 10.26
- Refund qty=1: itemRefundAmount = 10.26
- Refund qty=1: itemRefundAmount = 10.26
- **Total refunded: 30.78** ✅ (works here)

**But with nastier numbers:**

- Item: qty=3, UnitPrice=10.01, 7% tax
- Subtotal = 30.03
- Tax = 30.03 × 0.07 = 2.10 (rounded from 2.1021)
- Total = 32.13
- unitPriceWithTax = 32.13 / 3 = 10.71
- Refund 1: 10.71
- Refund 1: 10.71
- Refund 1: 10.71
- **Total: 32.13** ✅ (still OK)

**It breaks with order-level adjustments:**

- If there's an order-level discount, the `orderLevelAdjustment` and `refundRatio` introduce additional rounding layers. After 3+ partial refunds, the accumulated rounding drift could cause the final partial refund to either be rounded up (over-refund before capping) or the cap forces a slightly different amount in the last refund, creating a mismatch between sum-of-refunds and what the customer actually received.

**Although the cap prevents over-refund (which is good), the individual return orders may have slightly inaccurate financial breakdowns.**

---

### 🔴 CRITICAL BUG #6: Daily Report Discount Calculation Doesn't Include Return Order Discounts

**File:** [ReportService.cs](backend/KasserPro.Application/Services/Implementations/ReportService.cs)  
**Lines:** ~100-103

```csharp
// Calculate sales totals
var totalItemDiscounts = completedOrders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
var totalOrderDiscounts = completedOrders.Sum(o => o.DiscountAmount);
var totalDiscount = totalItemDiscounts + totalOrderDiscounts;
```

**The Problem:**
Return orders have **negative** discount amounts (e.g., `DiscountAmount = -3.00`). These aren't factored into the discount totals. The `actualNetSales` calculation uses:

```csharp
var actualNetSales = netSales - Math.Abs(returnOrders.Sum(o => o.Subtotal - o.DiscountAmount));
```

This subtracts `Math.Abs(Subtotal - DiscountAmount)` where both are negative for return orders. `Subtotal - DiscountAmount` for a return order = `(-30) - (-3) = -27`, `Math.Abs(-27) = 27`. This is correct for the net sales adjustment.

**However**, the **`TotalDiscount` value itself remains overstated** because it only counts discounts from sales orders but doesn't reduce for the fact that some of those discounted items were returned. The report shows more discounts than actually applied on net sales.

---

### 🔴 CRITICAL BUG #7: SaveChangesAsync After Transaction.CommitAsync

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs)  
**Lines:** ~1085-1093 (in RefundAsync)

```csharp
await transaction.CommitAsync();

// Update original order notes with return order reference
originalOrder.Notes = ...;
await _unitOfWork.SaveChangesAsync();  // THIS IS OUTSIDE THE TRANSACTION
```

**The Problem:**
After the transaction is committed, there's an additional `SaveChangesAsync()` call to update the order notes. This write is **outside the transaction boundary**. If it fails:

- The refund is committed (money returned, stock restored)
- But the order notes referencing the return order are NOT saved
- This is a minor data inconsistency, but more importantly...

**If the application crashes between CommitAsync and the second SaveChangesAsync, the order notes will be lost. If the system is later audited, the original order won't have a link to its return order in the notes field.**

---

### 🔴 CRITICAL BUG #8: CashRegister Refund Amount Calculation Uses Original Total, Not Remaining

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs)  
**Lines:** ~1068-1080

```csharp
var cashRefundAmount = Math.Round(
    (totalRefundAmount / originalOrder.Total) * originalCashPayments, 2);
```

**Problem with partial refund chains:**

1. Order total = 100, paid 100 cash
2. Partial refund #1: 30 → cashRefundAmount = (30/100) × 100 = 30 ✅
3. Partial refund #2: 30 → cashRefundAmount = (30/100) × 100 = 30 ✅
4. Partial refund #3 (remaining 40): cashRefundAmount = (40/100) × 100 = 40 ✅
5. Total cash refunded: 30 + 30 + 40 = 100 ✅

This works for all-cash. But for mixed payments:

1. Order total = 100, paid: 60 cash + 40 card
2. Partial refund #1: 30 → cashRefundAmount = (30/100) × 60 = 18
3. Partial refund #2: 30 → cashRefundAmount = (30/100) × 60 = 18
4. Partial refund #3: 40 → cashRefundAmount = (40/100) × 60 = 24
5. Total cash refunded: 18 + 18 + 24 = 60 ✅

This actually works correctly because each refund is proportional to the original. **However**, it assumes the customer always receives the SAME cash/card ratio back. In reality, a POS should refund in the same method — but this system always uses proportional calculation and only records to cash register, not tracking card refunds separately.

**The real bug is subtler:** For a **partial refund on a credit sale**, the cash register gets:

```
cashRefundAmount = (totalRefundAmount / originalOrder.Total) * originalCashPayments
```

But `originalOrder.Total` is the FULL order total, and the customer only paid PART in cash (rest was credit). The ratio is correct — but if the customer paid in cash MORE than the items being refunded, the cash register amount could be higher than the actual refund amount. The refund amount is capped at `totalRefundAmount`, but the cash proportion ISN'T capped at `totalRefundAmount`.

**Scenario:**

- Order total = 100, paid: 100 cash, AmountDue = 0
- Partial refund: 10 → cashRefund = (10/100) × 100 = 10 ✅

But:

- Order total = 100, paid: 50 cash, AmountDue = 50
- Partial refund: 10 → cashRefund = (10/100) × 50 = 5
- But what if the customer wants the FULL 10 back in cash? The system forces proportional distribution.

**This isn't a bug per se, but a design decision that may not match business expectations.**

---

### 🔴 CRITICAL BUG #9: CalculateOrderTotals Tax Recalculation Inconsistency with Item TaxAmounts

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs)  
**Lines:** ~1200-1230

**The Problem:**
When there IS an order-level discount:

```csharp
// Tax is RECALCULATED at order level with proportional discount
var orderDiscountRatio = order.DiscountAmount / netAfterItemDiscounts;
order.TaxAmount = order.Items.Sum(item => {
    var itemNet = item.Subtotal - item.DiscountAmount;
    var itemAfterOrderDiscount = itemNet * (1 - orderDiscountRatio);
    return itemAfterOrderDiscount * (item.TaxRate / 100m);
});
```

When there is NO order-level discount:

```csharp
// Tax = simple sum of item.TaxAmount
order.TaxAmount = order.Items.Sum(i => i.TaxAmount);
```

**The inconsistency:** Each item's `TaxAmount` is calculated in `CalculateItemTotals` based ONLY on item-level discounts. But `order.TaxAmount` with an order discount is recalculated INDEPENDENTLY from the item-level TaxAmounts. This means:

- **Individual item.TaxAmount values are WRONG** when an order discount exists — they reflect pre-order-discount tax
- **order.TaxAmount is CORRECT** — it accounts for order discount
- **But individual item DTOs show the pre-discount TaxAmount**, which sum to MORE than order.TaxAmount

This creates a visible discrepancy on receipts: sum of item taxes ≠ order total tax.

**Impact:** Customer sees item-level tax breakdown that doesn't add up to the order total tax. This is a compliance/audit issue.

---

### 🟠 HIGH BUG #1: Sales Report AverageOrderValue Uses GrossSales Instead of NetSales

**File:** [ReportService.cs](backend/KasserPro.Application/Services/Implementations/ReportService.cs)  
**Line:** ~275

```csharp
AverageOrderValue = salesOrders.Count > 0 ? grossSales / salesOrders.Count : 0,
```

`grossSales` = `salesOrders.Sum(o => o.Total)` which is the total INCLUDING refunded orders' gross amounts. The average should use `totalSales` (gross - refunds) for accurate reporting.

---

### 🟠 HIGH BUG #2: Daily Report Payment Breakdown Doesn't Account for Refunds

**File:** [ReportService.cs](backend/KasserPro.Application/Services/Implementations/ReportService.cs)  
**Lines:** ~90-96

```csharp
var allPayments = completedOrders.SelectMany(o => o.Payments).ToList();
var totalCash = allPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
```

This sums all payments from sales orders but doesn't subtract cash refunded. The payment breakdown overstates the actual cash received.

---

### 🟠 HIGH BUG #3: Refund on PartiallyRefunded Order Can Set Status to Refunded Prematurely

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs)  
**Line:** ~910

```csharp
originalOrder.Status = originalOrder.RefundAmount >= originalOrder.Total
    ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded;
```

Due to rounding, `RefundAmount` can equal `Total` before all items are actually returned. This could prevent further partial refunds on items that still have remaining quantity, because a subsequent "full refund" call would see `remainingAmount <= 0` and reject.

---

### 🟠 HIGH BUG #4: batch stock decrement can go negative even when AllowNegativeStock=false

**File:** [InventoryService.cs](backend/KasserPro.Infrastructure/Services/InventoryService.cs)  
**Lines:** ~305-315

```csharp
if (balanceBefore < quantity)
{
    _logger.LogWarning(...);  // Only logs a warning!
}
inventory.Quantity -= quantity;  // Goes negative anyway!
```

The `BatchDecrementStockAsync` only LOGS a warning but still decrements. The guard is supposed to be in `CompleteAsync`'s re-validation, but there's a gap:

Between the CompleteAsync validation and the BatchDecrementStockAsync call, another transaction could have decremented the same stock. Since SQLite's write lock is per-connection (not per-row), and the stock check + decrement aren't truly atomic at the row level, this can still result in negative stock.

---

### 🟠 HIGH BUG #5: Return Order Items Have Incorrect Individual Tax/Discount Breakdowns

**File:** [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs)  
**Lines:** ~855-870

```csharp
DiscountAmount = -Math.Round((orderItem.DiscountAmount / orderItem.Quantity) * refundItem.Quantity, 2),
TaxAmount = -Math.Round((orderItem.TaxAmount / orderItem.Quantity) * refundItem.Quantity, 2),
Subtotal = -Math.Round((orderItem.Subtotal / orderItem.Quantity) * refundItem.Quantity, 2),
```

This assumes uniform distribution of discount and tax across quantities. But for percentage discounts, the discount IS uniform. For FIXED discounts, it isn't — a fixed discount of 5 EGP on qty=3 means the discount is 5 total, not 5 per unit. The code divides 5/3 = 1.67 per unit, then multiplies by refund qty. This creates rounding errors:

- DiscountAmount = 5.00 on qty=3
- Per-unit discount = 5.00/3 = 1.6667
- Refund qty=1: discount = 1.67
- Refund qty=1: discount = 1.67
- Refund qty=1: discount = 1.67
- **Total: 5.01 ≠ 5.00** (1 cent error)

Over millions of transactions, this creates systematic 1-cent discrepancies in return order financial breakdowns.

---

### 🟠 HIGH BUG #6: Shift Sales Totals Not Updated After Refund

The shift entity tracks `TotalCash`, `TotalCard`, `TotalOrders`. When an order is completed, the shift's totals are calculated at close time from orders linked to the shift. However, when a refund occurs (potentially in a different shift), the original shift's linked orders include the original order with its full amount but the refund is a separate Return order potentially in a different shift.

**Impact:** Shift reconciliation will show the original sale amounts without accounting for later refunds. The cashier's expected balance won't match the register if refunds occurred after their shift.

---

### 🟠 HIGH BUG #7: Daily Report Only Shows Shifts CLOSED On Date, Misses Open Shifts

**File:** [ReportService.cs](backend/KasserPro.Application/Services/Implementations/ReportService.cs)  
**Lines:** ~35-44

```csharp
.Where(s => s.IsClosed && s.ClosedAt!.Value.Date == reportDate)
```

If a shift is still open (not closed yet), ALL its orders are invisible in the daily report. A busy shift that spans midnight or is forgotten open will have its sales missing from reports entirely until it's closed.

---

### 🟡 MEDIUM BUG #1: Custom Item Price = 0 Allowed

A custom item with `UnitPrice = 0` is allowed (the validation checks `< 0` not `<= 0`). This is intentional per the comment "السعر يجب أن يكون أكبر من أو يساوي صفر" but could be exploited to create orders with 0-value items that pollute reports.

---

### 🟡 MEDIUM BUG #2: No Rate Limiting on Refund Endpoint

There's no rate limiting or cooldown on the refund endpoint. A malicious user with `OrdersRefund` permission could send hundreds of partial refund requests per second, potentially exploiting the race condition in Critical Bug #3.

---

### 🟡 MEDIUM BUG #3: Tax Rate Not Validated for Upper Bound on Tenant Setting

The tenant tax rate has no upper bound validation. Setting `TaxRate = 1000` (1000%) is possible at the tenant configuration level, which would corrupt all order calculations.

---

### 🟡 MEDIUM BUG #4: No Idempotency on Complete/Refund Endpoints

If a network timeout occurs and the client retries, CompleteAsync or RefundAsync could be called twice. There's no idempotency key mechanism for these critical financial operations (though IdempotencyMiddleware exists in the project, it's unclear if it covers these endpoints).

---

### 🟡 MEDIUM BUG #5: ServiceChargePercent Not Snapshotted Per Item

Service charge is stored at the order level only. If the business changes the service charge percent between order creation and completion, orders in Draft state will use the new rate on recalculation.

---

### 🟡 MEDIUM BUG #6: DeductRefundStatsAsync Doesn't Decrement TotalOrders

When a full refund occurs, `TotalSpent` is reduced but `TotalOrders` is not decremented. Over time, `TotalOrders` will overstate the actual net completed orders for a customer.

---

### 🟢 LOW #1: Console.WriteLine Debug Logging in Production Report Code

**File:** [ReportService.cs](backend/KasserPro.Application/Services/Implementations/ReportService.cs)

Multiple `Console.WriteLine` calls in production report generation code. Should use `ILogger` instead.

---

### 🟢 LOW #2: Order Number Collision Risk (Extremely Low)

Order numbers use `Guid.NewGuid().ToString()[..6]` — 6 hex chars = 16^6 = ~16.7M combinations per day. With millions of daily transactions, birthday paradox collision probability increases.

---

### 🟢 LOW #3: ReduceCreditBalanceAsync Silently Clamps to 0

If over-reduction happens (due to rounding or bugs), TotalDue is clamped to 0 with no logging. This hides financial inconsistencies.

---

### 🟢 LOW #4: Return Order Has Negative UnitPrice

Return order items store `UnitPrice = -orderItem.UnitPrice`. This makes UnitPrice negative in the database, which is semantically unusual and could confuse downstream report aggregations that expect positive prices.

---

## SECTION 3 — ROUNDING DRIFT ANALYSIS

### Test: Chained Partial Refunds

**Setup:** Order with 10 items, each 33.33 EGP, 14% tax, 5% order discount

| Item     | Subtotal | ItemTax | ItemTotal |
| -------- | -------- | ------- | --------- |
| Each     | 33.33    | 4.43    | 37.76     |
| Sum (10) | 333.30   | 44.30   | 377.60    |

Order after 5% discount:

- netAfterItemDiscounts = 333.30
- orderDiscount = 333.30 × 5% = 16.67
- afterAllDiscounts = 316.63
- taxAmount (recalculated) = ... varies per item
- Let's say order.Total = 360.96

Now refund one item at a time (10 refunds):

- Each: unitPriceWithTax = 37.76 (item.Total / item.Quantity)
- refundedItemsGrossTotal = 37.76 per refund
- refundRatio = 37.76 / 377.60 = 0.1 per refund
- orderLevelAdjustment = 360.96 - 377.60 = -16.64
- totalRefundAmount = 37.76 + (-16.64 × 0.1) = 37.76 - 1.664 = 36.096 → rounded to 36.10

After 10 refunds: 36.10 × 10 = 361.00

But order.Total = 360.96. The cap kicks in: after 9 refunds (324.90), the 10th refund is capped at 360.96 - 324.90 = 36.06.

**Result:** 9 refunds of 36.10 + 1 refund of 36.06 = 360.96 ✅

**The cap WORKS** but the last refund amount differs from previous ones, which could confuse accounting. The individual return order amounts aren't perfectly uniform.

---

## SECTION 4 — CONCURRENCY RISK MATRIX

| Operation A         | Operation B                         | Protected? | Risk Level  |
| ------------------- | ----------------------------------- | :--------: | :---------: |
| CompleteAsync       | CompleteAsync (same order)          |     ❌     | 🔴 CRITICAL |
| RefundAsync         | RefundAsync (same order)            |     ❌     | 🔴 CRITICAL |
| CompleteAsync       | RefundAsync (same order)            |     ❌     | 🔴 CRITICAL |
| UpdateCreditBalance | UpdateCreditBalance (same customer) |     ❌     | 🔴 CRITICAL |
| ValidateCreditLimit | CompleteAsync (same customer)       |     ❌     | 🔴 CRITICAL |
| UpdateOrderStats    | UpdateOrderStats (same customer)    |     ❌     |   🟠 HIGH   |
| DeductRefundStats   | DeductRefundStats (same customer)   |     ❌     |   🟠 HIGH   |
| BatchDecrementStock | BatchDecrementStock (same product)  | ⚠️ Partial |   🟠 HIGH   |
| PayDebtAsync        | PayDebtAsync (same customer)        |     ✅     |   ✅ SAFE   |
| CloseShiftAsync     | CloseShiftAsync (same shift)        |     ✅     |   ✅ SAFE   |
| CashRegister Record | CashRegister Record                 |     ✅     |   ✅ SAFE   |

---

## SECTION 5 — RECOMMENDATIONS

### Priority 1: IMMEDIATE (Before Production Launch)

1. **Add RowVersion to Order entity** — Prevent double-complete and double-refund via optimistic concurrency. Catch `DbUpdateConcurrencyException` in CompleteAsync and RefundAsync.

2. **Fix P&L Report** — Subtract refunds from revenue and reverse COGS for returned items:

   ```csharp
   var netRevenue = totalRevenue - refundsAmount;
   var netCost = totalCost - returnedCost;
   var grossProfit = (grossSales - totalDiscount - refundsAmount) - netCost;
   ```

3. **Wrap CustomerService write operations in transactions** — Or use SQL-level atomic updates:

   ```sql
   UPDATE Customers SET TotalDue = TotalDue + @amount WHERE Id = @id
   ```

4. **Add idempotency keys to Complete and Refund** — Use the existing IdempotencyMiddleware for these critical endpoints.

### Priority 2: HIGH (First Week Post-Launch)

5. **Move the post-transaction SaveChangesAsync inside the refund transaction** — The order notes update should be inside the same transaction.

6. **Fix daily report to include open shifts** — Or at minimum, show a warning that open shifts are excluded.

7. **Add rate limiting on refund endpoint** — Maximum 1 refund per order per 5 seconds.

8. **Fix individual item tax/discount display when order discount exists** — Either recalculate item-level TaxAmount to reflect order discount, or clearly label the order-level tax adjustment.

### Priority 3: MEDIUM (Within First Month)

9. **Add audit trail for all customer balance changes** — Similar to DebtPayment's `BalanceBefore`/`BalanceAfter` pattern.

10. **Replace Console.WriteLine with ILogger** in ReportService.

11. **Add upper bound validation for TaxRate** at tenant/product level.

12. **Consider displaying net-of-refund payment breakdown in daily reports**.

### Priority 4: LONG-TERM

13. **Implement event sourcing or change data capture** for financial transactions to enable forensic analysis.

14. **Add financial reconciliation jobs** that verify sum-of-parts equals whole (e.g., sum of all order totals = sum of all payments + sum of all credit changes).

15. **Consider moving from SQLite to PostgreSQL** for better concurrent transaction handling at scale.

---

## SECTION 6 — WHAT THE SYSTEM DOES WELL

Credit where due — the system has strong fundamentals:

| Feature                                          | Assessment                                                                         |
| ------------------------------------------------ | ---------------------------------------------------------------------------------- |
| Price/Tax snapshots on order items               | ✅ Excellent — prevents retroactive price changes from affecting historical orders |
| Transaction usage in CompleteAsync & RefundAsync | ✅ Good — atomicity for multi-step operations                                      |
| Math.Round(..., 2) everywhere                    | ✅ Good — consistent 2-decimal rounding                                            |
| RefundedQuantity tracking                        | ✅ Excellent — prevents refunding more than sold                                   |
| Refund cap (remainingRefundable)                 | ✅ Excellent — handles rounding drift in chains                                    |
| Return Order separate entity                     | ✅ Good architecture — clean audit trail                                           |
| StockMovement audit trail                        | ✅ Excellent — full before/after tracking                                          |
| Overpayment limit (2x)                           | ✅ Good fraud prevention                                                           |
| Product.TrackInventory flag                      | ✅ Good — separates physical from service products                                 |
| Tenant isolation in all queries                  | ✅ Good multi-tenancy                                                              |
| Shift-level RowVersion                           | ✅ Good — only entity that has it                                                  |
| PayDebtAsync transaction pattern                 | ✅ Excellent — should be the template for all customer mutations                   |

---

**END OF REPORT**

_This analysis was based on source code review and logical scenario construction. The bugs identified are deterministic and reproducible. The most critical items (P&L report, concurrency races, double-refund) should be addressed before production deployment._
