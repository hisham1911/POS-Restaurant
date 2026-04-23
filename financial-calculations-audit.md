# Financial Calculations Audit

Generated from source code on 2026-04-23.

## Table of Contents

- [Phase 1 - Audit Completion](#phase-1---audit-completion)
  - [Cart Item Net Pricing](#cart-item-net-pricing)
  - [Cart Item Discount Amount](#cart-item-discount-amount)
  - [Cart Order Discount Tax and Total](#cart-order-discount-tax-and-total)
  - [POS Payment Due Change and Credit Availability](#pos-payment-due-change-and-credit-availability)
  - [Order Item Totals](#order-item-totals)
  - [Order Header Totals](#order-header-totals)
  - [Order Settlement and Side Effects](#order-settlement-and-side-effects)
  - [Refund Preview and Refund Execution](#refund-preview-and-refund-execution)
  - [Receipt Totals and Discount Rendering](#receipt-totals-and-discount-rendering)
  - [Purchase Invoice Totals](#purchase-invoice-totals)
  - [Purchase Invoice Costing and Payment Status](#purchase-invoice-costing-and-payment-status)
  - [Customer Debt Balance and Payment](#customer-debt-balance-and-payment)
  - [Expense Payment and Cash Deduction](#expense-payment-and-cash-deduction)
  - [Cash Register Running Balance and Summary](#cash-register-running-balance-and-summary)
  - [Shift Totals and Expected Balance](#shift-totals-and-expected-balance)
  - [Daily Sales Report](#daily-sales-report)
  - [Sales Report](#sales-report)
  - [Profit and Loss Report](#profit-and-loss-report)
  - [Expenses Report](#expenses-report)
  - [Branch Inventory Valuation](#branch-inventory-valuation)
  - [Unified Inventory Valuation](#unified-inventory-valuation)
  - [Low Stock Summary](#low-stock-summary)
  - [Transfer History Totals](#transfer-history-totals)
  - [Product Movement Report](#product-movement-report)
  - [Profitable Products Report](#profitable-products-report)
  - [COGS Report](#cogs-report)
  - [Top Customers Report](#top-customers-report)
  - [Customer Debts Report](#customer-debts-report)
  - [Customer Activity Report](#customer-activity-report)
  - [Supplier Purchases Report](#supplier-purchases-report)
  - [Supplier Debts Report](#supplier-debts-report)
  - [Supplier Performance Report](#supplier-performance-report)
  - [Cashier Performance Report](#cashier-performance-report)
  - [Shift Details Report](#shift-details-report)
  - [Sales By Employee Report](#sales-by-employee-report)
  - [Orders Page Summary](#orders-page-summary)
  - [Page-Local Totals](#page-local-totals)
  - [Duplicate Calculation Detected](#duplicate-calculation-detected)
  - [Special Cases](#special-cases)
- [Phase 2 - Unified Financial Diagram](#phase-2---unified-financial-diagram)
- [Phase 3 - Critical Financial Risks](#phase-3---critical-financial-risks)

## Phase 1 - Audit Completion

### Cart Item Net Pricing

Location:
- `frontend/src/utils/cartPricing.ts`
- `frontend/src/components/pos/CartItem.tsx`
- `frontend/src/components/pos/CustomItemModal.tsx`

Layer:
- Frontend

Purpose:
- Calculate the pre-tax unit price and pre-tax line subtotal shown in the cart and custom-item preview.

Code:
```ts
const round2 = (value: number): number => Math.round(value * 100) / 100;
const round4 = (value: number): number => Math.round(value * 10000) / 10000;

export const getProductNetUnitPrice = (
  product: Product,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number => {
  const taxRate = getProductEffectiveTaxRate(
    product,
    fallbackTaxRate,
    isTaxEnabled,
  );

  if (product.taxInclusive && taxRate > 0) {
    return round4(product.price / (1 + taxRate / 100));
  }

  return product.price;
};

export const getCartItemSubtotal = (
  item: CartPricingItem,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number =>
  round2(
    getProductNetUnitPrice(item.product, fallbackTaxRate, isTaxEnabled) *
      item.quantity,
  );

const unitPrice = getProductNetUnitPrice(product, taxRate, isTaxEnabled);
const subtotal = getCartItemSubtotal(item, taxRate, isTaxEnabled);
```

Formula in Plain Math:

- `effectiveTaxRate = isTaxEnabled ? (product.taxRate ?? fallbackTaxRate) : 0`
- `netUnitPrice = taxInclusive ? round4(price / (1 + effectiveTaxRate / 100)) : price`
- `lineSubtotal = round2(netUnitPrice * quantity)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `product.price` | Product payload in cart state | None in selector | May be tax-inclusive or tax-exclusive |
| `product.taxRate` | Product payload | None in selector | Falls back to tenant tax rate |
| `fallbackTaxRate` | `cart.taxRate` | None in selector | Tenant-level rate pushed into cart state |
| `product.taxInclusive` | Product payload | None in selector | Controls net-price extraction |
| `item.quantity` | Cart item state | None in selector | Used directly without additional validation here |

Potential Issues:
- [ ] Tenant filter? no - pure frontend state calculation; no direct tenant query happens here.
- [ ] Branch filter? no - pure frontend state calculation; branch isolation must already be reflected in loaded product data.
- [ ] Cancelled excluded? no - cart preview has no order-status dimension.
- [ ] Rounding safe? yes - uses `round4` for tax-inclusive normalization and `round2` for displayed subtotal.
- [ ] Discount before/after tax? no - this stage is before discount.
- [ ] Refund handled? no - refund logic is outside cart pricing.
- [ ] Negative possible? yes - malformed negative product price or quantity would flow through.
- [ ] FE vs BE mismatch? no - backend `ResolveNetUnitPrice(...)` uses the same tax-inclusive extraction logic.

Risk Assessment:
- Level: Medium
- Layer: Frontend
- Scenario: If product price or tax mode is edited client-side before checkout, the cart preview can be wrong until backend re-creates the authoritative order.

### Cart Item Discount Amount

Location:
- `frontend/src/utils/cartPricing.ts`
- `frontend/src/components/pos/ItemDiscountModal.tsx`
- `frontend/src/components/pos/DiscountModal.tsx`

Layer:
- Frontend

Purpose:
- Calculate item-level and preview discount values before order creation.

Code:
```ts
export const getCartItemDiscountAmount = (
  item: CartPricingItem,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number => {
  if (!item.discount) {
    return 0;
  }

  const lineSubtotal = getCartItemSubtotal(item, fallbackTaxRate, isTaxEnabled);

  if (item.discount.type === "percentage") {
    return round2(
      Math.min(lineSubtotal * (item.discount.value / 100), lineSubtotal),
    );
  }

  return round2(Math.min(item.discount.value, lineSubtotal));
};

if (discountType === "percentage") {
  previewDiscount = itemTotal * (numericValue / 100);
} else {
  previewDiscount = numericValue;
}
previewDiscount = Math.min(previewDiscount, itemTotal);
const previewTotal = itemTotal - previewDiscount;
```

Formula in Plain Math:

- `itemDiscountAmount = 0` if no discount
- Percentage mode: `round2(min(lineSubtotal * discountPercent / 100, lineSubtotal))`
- Fixed mode: `round2(min(fixedDiscount, lineSubtotal))`
- Preview total: `lineSubtotal - itemDiscountAmount`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `item.discount.type` | Cart item state | None | `"percentage"` or `"fixed"` |
| `item.discount.value` | Cart item state / modal input | None | Parsed from UI input |
| `lineSubtotal` | `getCartItemSubtotal(...)` | None | Pre-tax base |
| `numericValue` | Modal numeric input | None | Preview-only, not authoritative |

Potential Issues:
- [ ] Tenant filter? no - local UI and selector logic only.
- [ ] Branch filter? no - local UI and selector logic only.
- [ ] Cancelled excluded? no - preview has no persisted order status.
- [ ] Rounding safe? yes - selector rounds to 2 decimals; modal preview uses unclamped JS math until display.
- [ ] Discount before/after tax? yes - item discount is taken from subtotal before tax.
- [ ] Refund handled? no - refunds are separate logic.
- [ ] Negative possible? no - selector caps discount at subtotal and returns zero when discount is absent.
- [ ] FE vs BE mismatch? no - backend `CalculateItemTotals(...)` clamps percentage/fixed discount the same way.

Risk Assessment:
- Level: Low
- Layer: Frontend
- Scenario: Preview values are safe for normal input, but malformed state injected into the cart could still display unexpected negative behavior until backend validation runs.

### Cart Order Discount Tax and Total

Location:
- `frontend/src/store/slices/cartSlice.ts`
- `frontend/src/components/pos/OrderSummary.tsx`
- `frontend/src/components/pos/DiscountModal.tsx`

Layer:
- Frontend

Purpose:
- Compute order-level discount, tax after discount allocation, and the cart grand total shown before checkout.

Code:
```ts
export const selectDiscountAmount = (state: { cart: CartState }) => {
  if (!state.cart.discountType || !state.cart.discountValue) return 0;

  const subtotal = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemSubtotal(item, state.cart.taxRate, state.cart.isTaxEnabled),
    0,
  );

  const itemDiscounts = state.cart.items.reduce(
    (sum, item) =>
      sum +
      getCartItemDiscountAmount(
        item,
        state.cart.taxRate,
        state.cart.isTaxEnabled,
      ),
    0,
  );
  const afterItemDiscounts = subtotal - itemDiscounts;

  let discountAmount = 0;
  if (state.cart.discountType === "Percentage") {
    discountAmount = afterItemDiscounts * (state.cart.discountValue / 100);
  } else {
    discountAmount = state.cart.discountValue;
  }

  return Math.round(Math.min(discountAmount, afterItemDiscounts) * 100) / 100;
};

export const selectTaxAmount = (state: { cart: CartState }) => {
  if (!state.cart.isTaxEnabled) return 0;
  ...
  if (orderDiscount > 0 && afterItemDiscounts > 0) {
    const discountRatio = orderDiscount / afterItemDiscounts;
    return (
      Math.round(
        state.cart.items.reduce((sum, item) => {
          const itemNetAfterItemDiscount = getCartItemNetAfterDiscount(
            item,
            state.cart.taxRate,
            state.cart.isTaxEnabled,
          );
          const itemTaxRate = getProductEffectiveTaxRate(
            item.product,
            state.cart.taxRate,
            state.cart.isTaxEnabled,
          );

          return (
            sum +
            itemNetAfterItemDiscount * (1 - discountRatio) * (itemTaxRate / 100)
          );
        }, 0) * 100,
      ) / 100
    );
  }
  ...
};

export const selectTotal = (state: { cart: CartState }) => {
  ...
  const afterAllDiscounts = afterItemDiscounts - orderDiscount;

  if (!state.cart.isTaxEnabled) {
    return Math.round(afterAllDiscounts * 100) / 100;
  }

  const taxAmount = selectTaxAmount(state);
  return Math.round((afterAllDiscounts + taxAmount) * 100) / 100;
};
```

Formula in Plain Math:

- `subtotal = sum(item subtotals)`
- `itemDiscounts = sum(item discount amounts)`
- `afterItemDiscounts = subtotal - itemDiscounts`
- `orderDiscount = min(percentage or fixed discount, afterItemDiscounts)`
- `discountRatio = orderDiscount / afterItemDiscounts` when order discount exists
- `taxAmount = sum(itemNetAfterItemDiscount * (1 - discountRatio) * itemTaxRate)`
- `total = round2(afterItemDiscounts - orderDiscount + taxAmount)` when tax is enabled

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `state.cart.items` | Redux cart state | None | Order preview basket |
| `discountType` / `discountValue` | Redux cart state | None | `"Percentage"` or `"Fixed"` |
| `taxRate` | Redux cart state | None | Tenant-level fallback rate |
| `isTaxEnabled` | Redux cart state | None | Tax kill switch for preview |

Potential Issues:
- [ ] Tenant filter? no - local cart selector only.
- [ ] Branch filter? no - local cart selector only.
- [ ] Cancelled excluded? no - cart preview has no status filtering.
- [ ] Rounding safe? yes - final discount, tax, and total are rounded to 2 decimals.
- [ ] Discount before/after tax? yes - both item and order discounts reduce the taxable base before tax is summed.
- [ ] Refund handled? no - no refund state is modeled in cart selectors.
- [ ] Negative possible? no - order discount is capped at the available base.
- [ ] FE vs BE mismatch? yes - backend `CalculateOrderTotals(...)` also adds `ServiceChargeAmount`, while frontend cart selectors do not model service charge at all.

Risk Assessment:
- Level: Medium
- Layer: Frontend
- Scenario: If service charge is enabled in backend configuration or introduced through another client, the cart total shown in POS will be lower than the persisted order total.

### POS Payment Due Change and Credit Availability

Location:
- `frontend/src/components/pos/PaymentModal.tsx`
- `frontend/src/pages/pos/POSWorkspacePage.tsx`
- `frontend/src/components/customers/CustomerDetailsModal.tsx`

Layer:
- Frontend

Purpose:
- Calculate the amount due, cash change, available customer credit, and credit-utilization bars shown before completion.

Code:
```ts
const total = preparedOrder?.total ?? 0;
const numericAmount = parseFloat(amountPaid) || 0;
const change = numericAmount - total;
const amountDue = total - numericAmount;
const availableCredit = selectedCustomer
  ? selectedCustomer.creditLimit - selectedCustomer.totalDue
  : 0;
const canTakeCredit =
  selectedCustomer &&
  selectedCustomer.isActive &&
  (selectedCustomer.creditLimit === 0 ||
    amountDue <= availableCredit);

{formatCurrency(customer.creditLimit - customer.totalDue)}
{(
  (customer.totalDue / customer.creditLimit) *
  100
).toFixed(0)}%
width: `${Math.min(
  (customer.totalDue / customer.creditLimit) *
    100,
  100,
)}%`,
```

Formula in Plain Math:

- `paymentTotal = backendPreparedOrderTotal if available else frontendCartTotal`
- `change = amountPaid - paymentTotal`
- `amountDue = paymentTotal - amountPaid`
- `availableCredit = creditLimit - currentTotalDue`
- `canTakeCredit = activeCustomer AND (unlimitedCredit OR amountDue <= availableCredit)`
- `creditUsagePercent = min((currentTotalDue / creditLimit) * 100, 100)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `preparedOrder.total` | Backend-prepared order | Per customer and current cart signature | Authoritative total in payment step |
| `amountPaid` | Cashier input | None | Parsed with `parseFloat` |
| `selectedCustomer.creditLimit` | Customer payload | None in UI | `0` means unlimited in this flow |
| `selectedCustomer.totalDue` | Customer payload | None in UI | Existing customer debt |

Potential Issues:
- [ ] Tenant filter? no - frontend display logic only.
- [ ] Branch filter? no - frontend display logic only.
- [ ] Cancelled excluded? no - payment screen works before order completion, not after status transitions.
- [ ] Rounding safe? no - `parseFloat` and subtraction are not explicitly rounded before comparisons.
- [ ] Discount before/after tax? no - the section consumes the already computed order total.
- [ ] Refund handled? no - this screen is sale-settlement only.
- [ ] Negative possible? yes - `change` can be negative and `availableCredit` can already be below zero.
- [ ] FE vs BE mismatch? no - backend `ValidateCreditLimitAsync(...)` and settlement logic use the same due-vs-credit concept.

Risk Assessment:
- Level: Medium
- Layer: Frontend
- Scenario: A cent-level floating-point drift can make the UI temporarily show credit-limit exceeded or allowed at the boundary while backend later decides the final answer.

### Order Item Totals

Location:
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs`

Layer:
- Backend

Purpose:
- Calculate the authoritative subtotal, discount, tax, and total for each persisted order item.

Code:
```cs
private static void CalculateItemTotals(OrderItem item)
{
    var grossSubtotal = Math.Round(item.UnitPrice * item.Quantity, 2);
    item.Subtotal = grossSubtotal;
    item.DiscountType = NormalizeDiscountType(item.DiscountType);

    if (item.DiscountType == "percentage" && item.DiscountValue.HasValue)
    {
        var percentageDiscount = Math.Clamp(item.DiscountValue.Value, 0m, 100m);
        item.DiscountAmount = Math.Round(grossSubtotal * (percentageDiscount / 100m), 2);
    }
    else if (item.DiscountType == "fixed" && item.DiscountValue.HasValue)
    {
        var fixedDiscount = Math.Clamp(item.DiscountValue.Value, 0m, grossSubtotal);
        item.DiscountAmount = Math.Round(fixedDiscount, 2);
    }
    else
        item.DiscountAmount = 0;

    var netAfterDiscount = Math.Round(grossSubtotal - item.DiscountAmount, 2);
    item.TaxAmount = Math.Round(netAfterDiscount * (item.TaxRate / 100m), 2);
    item.Total = Math.Round(netAfterDiscount + item.TaxAmount, 2);
}
```

Formula in Plain Math:

- `grossSubtotal = round2(unitPrice * quantity)`
- `discountAmount = round2(clamped percentage or fixed amount)`
- `netAfterDiscount = round2(grossSubtotal - discountAmount)`
- `taxAmount = round2(netAfterDiscount * taxRate / 100)`
- `itemTotal = round2(netAfterDiscount + taxAmount)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `item.UnitPrice` | Normalized order item | Comes from product snapshot / request | Already net of tax if tax-inclusive product |
| `item.Quantity` | Order request | Validated earlier | Positive in normal sales flow |
| `item.DiscountType` / `DiscountValue` | Order request | Normalized in method | Lower-cased before use |
| `item.TaxRate` | Product or tenant tax setup | `tenant.IsTaxEnabled` may zero it earlier | Stored per item |

Potential Issues:
- [ ] Tenant filter? no - pure calculation method after item loading.
- [ ] Branch filter? no - pure calculation method after item loading.
- [ ] Cancelled excluded? no - this method is reused before final status exists.
- [ ] Rounding safe? yes - every monetary stage is rounded to 2 decimals.
- [ ] Discount before/after tax? yes - discount is applied before tax.
- [ ] Refund handled? no - refund items are created separately with negative snapshots.
- [ ] Negative possible? no - discount inputs are clamped and standard sale quantities are positive.
- [ ] FE vs BE mismatch? no - frontend line pricing matches this method for the same inputs.

Risk Assessment:
- Level: Low
- Layer: Backend
- Scenario: The method is stable, but any future extra line-level charge added only in backend will immediately desynchronize POS previews.

### Order Header Totals

Location:
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs`

Layer:
- Backend

Purpose:
- Calculate the authoritative order subtotal, total discounts, tax, service charge, total, and amount due.

Code:
```cs
private static void CalculateOrderTotals(Order order)
{
    order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);
    var itemDiscountsTotal = Math.Round(order.Items.Sum(i => i.DiscountAmount), 2);
    var netAfterItemDiscounts = Math.Round(order.Subtotal - itemDiscountsTotal, 2);
    ...
    if (order.DiscountAmount > 0 && netAfterItemDiscounts > 0)
    {
        var discountRatio = order.DiscountAmount / netAfterItemDiscounts;
        order.TaxAmount = Math.Round(order.Items.Sum(item =>
        {
            var itemNetAfterItemDiscount = item.Subtotal - item.DiscountAmount;
            var itemAfterDiscount = itemNetAfterItemDiscount * (1m - discountRatio);
            return itemAfterDiscount * (item.TaxRate / 100m);
        }), 2);
    }
    else
    {
        order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);
    }

    order.ServiceChargeAmount = Math.Round(afterDiscount * (order.ServiceChargePercent / 100m), 2);
    order.Total = Math.Round(afterDiscount + order.TaxAmount + order.ServiceChargeAmount, 2);
    order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
}
```

Formula in Plain Math:

- `subtotal = round2(sum(item.Subtotal))`
- `itemDiscountsTotal = round2(sum(item.DiscountAmount))`
- `netAfterItemDiscounts = round2(subtotal - itemDiscountsTotal)`
- `orderDiscount = percentage or fixed discount on netAfterItemDiscounts`
- `taxAmount = round2(sum(itemNetAfterItemDiscount * (1 - discountRatio) * itemTaxRate))`
- `serviceChargeAmount = round2(afterDiscount * serviceChargePercent / 100)`
- `orderTotal = round2(afterDiscount + taxAmount + serviceChargeAmount)`
- `amountDue = round2(orderTotal - amountPaid)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `order.Items` | Built sales order lines | Parent order already tenant/branch scoped | Uses item snapshots |
| `order.DiscountType` / `DiscountValue` | Order request | None in helper | Normalized earlier |
| `order.ServiceChargePercent` | Order entity | None in helper | Not represented in frontend cart |
| `order.AmountPaid` | Payment settlement stage | None in helper | Can be zero during draft/preparation |

Potential Issues:
- [ ] Tenant filter? no - pure calculation helper after order loading.
- [ ] Branch filter? no - pure calculation helper after order loading.
- [ ] Cancelled excluded? no - total is computed before status transitions.
- [ ] Rounding safe? yes - subtotal, discounts, tax, service charge, and total are rounded.
- [ ] Discount before/after tax? yes - item and order discounts reduce tax base before tax is aggregated.
- [ ] Refund handled? no - refund adjustments are outside this helper.
- [ ] Negative possible? yes - return orders or future negative adjustments can yield negative totals.
- [ ] FE vs BE mismatch? yes - backend includes `ServiceChargeAmount`; current POS cart selector does not.

Risk Assessment:
- Level: High
- Layer: Backend
- Scenario: Any non-zero service charge produces a persisted total that the POS cart never showed, creating cashier disputes and receipt discrepancies.

### Order Settlement and Side Effects

Location:
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs`
- `backend/KasserPro.Application/Services/Implementations/CustomerService.cs`

Layer:
- Backend

Purpose:
- Finalize payment, compute amount due/change, post customer debt, loyalty points, and cash-register sale movement.

Code:
```cs
decimal totalPaymentAmount = request.Payments.Sum(p => p.Amount);
if (totalPaymentAmount < order.Total)
{
    if (!order.CustomerId.HasValue)
    {
        return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_INSUFFICIENT,
            $"المبلغ المدفوع ({totalPaymentAmount:F2}) أقل من إجمالي الطلب ({order.Total:F2}). البيع الآجل يتطلب ربط عميل بالطلب.");
    }

    var amountDue = order.Total - totalPaymentAmount;
    var canTakeCredit = await _customerService.ValidateCreditLimitAsync(order.CustomerId.Value, amountDue);
    ...
}

order.AmountPaid = Math.Round(totalPaid, 2);
order.AmountDue = Math.Round(order.Total - totalPaid, 2);
order.ChangeAmount = totalPaid > order.Total ? Math.Round(totalPaid - order.Total, 2) : 0;

int loyaltyPoints = (int)Math.Floor(order.Total);
await _customerService.UpdateOrderStatsAsync(order.CustomerId.Value, order.Total, loyaltyPoints);
if (order.AmountDue > 0)
{
    await _customerService.UpdateCreditBalanceAsync(order.CustomerId.Value, order.AmountDue);
}

if (cashPaymentAmount > 0)
{
    await _cashRegisterService.RecordTransactionAsync(
        type: CashRegisterTransactionType.Sale,
        amount: cashPaymentAmount,
        description: $"مبيعات - طلب #{order.OrderNumber}",
        referenceType: "Order",
        referenceId: order.Id,
        shiftId: order.ShiftId ?? currentShift.Id
    );
}

customer.TotalDue += amountDue;
var newTotalDue = customer.TotalDue + additionalAmount;
return newTotalDue <= customer.CreditLimit;
```

Formula in Plain Math:

- `totalPaymentAmount = sum(payment amounts)`
- `amountDue = orderTotal - totalPaid`
- `changeAmount = max(totalPaid - orderTotal, 0)`
- `loyaltyPointsEarned = floor(orderTotal)`
- `customerTotalDue = previousCustomerTotalDue + amountDue` for credit sales
- `cashRegisterSaleAmount = sum(cash payments only)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `request.Payments` | Completion request | Order already tenant/branch scoped | Supports multi-payment payload |
| `order.Total` | Backend-calculated order | Same order | Authoritative sale amount |
| `customer.TotalDue` | Customer entity | Tenant only in `CustomerService` | Not branch-partitioned |
| `cashPaymentAmount` | Order payments filtered by method | Same order | Only cash posts to cash register |

Potential Issues:
- [ ] Tenant filter? yes - order and customer lookups enforce current tenant.
- [ ] Branch filter? yes - order and shift lookups are branch-scoped; customer debt itself is not branch-scoped.
- [ ] Cancelled excluded? yes - completion runs only on active order settlement, not cancelled orders.
- [ ] Rounding safe? yes - paid, due, and change are rounded to 2 decimals.
- [ ] Discount before/after tax? yes - settlement consumes the already discounted and taxed backend total.
- [ ] Refund handled? no - refunds are separate workflow.
- [ ] Negative possible? yes - `ChangeAmount` is prevented from going negative, but customer total due can accumulate across branches.
- [ ] FE vs BE mismatch? no - payment UI intentionally switches to `preparedOrder.total` from backend before settlement.

Risk Assessment:
- Level: High
- Layer: Backend
- Scenario: Customer debt is stored tenant-wide, so branch-isolated credit governance can be bypassed or over-restricted when the same customer buys across branches.

### Refund Preview and Refund Execution

Location:
- `frontend/src/components/orders/RefundModal.tsx`
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs`

Layer:
- Frontend + Backend

Purpose:
- Preview the refund amount in UI and create the authoritative return order, negative item snapshots, and debt/cash reversals in backend.

Code:
```ts
const [refundItems, setRefundItems] = useState<RefundItemState[]>(() =>
  order.items
    .filter((item) => item.quantity - (item.refundedQuantity || 0) > 0)
    .map((item) => ({
      itemId: item.id,
      maxQuantity: item.quantity - (item.refundedQuantity || 0),
      refundQuantity: 0,
      unitPrice: item.total / item.quantity,
      productName: item.productName,
      discountAmount: item.discountAmount,
      discountType: item.discountType,
      discountValue: item.discountValue,
    })),
);
const remainingRefundableAmount = Math.max(
  0,
  order.total - (order.refundAmount || 0),
);
const totalRefundAmount = useMemo(() => {
  if (refundType === "full") {
    return remainingRefundableAmount;
  }
  return refundItems.reduce(
    (sum, item) => sum + item.refundQuantity * item.unitPrice,
    0,
  );
}, [refundType, refundItems, remainingRefundableAmount]);
```

```cs
var originalOrder = await _unitOfWork.Orders.Query()
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .FirstOrDefaultAsync(o => o.Id == orderId
        && o.TenantId == _currentUser.TenantId
        && o.BranchId == _currentUser.BranchId);

var remainingRefundableAmount = Math.Round(originalOrder.Total - originalOrder.RefundAmount, 2);
...
var unitPriceWithTax = orderItem.Total / orderItem.Quantity;
var itemRefundAmount = unitPriceWithTax * refundItem.Quantity;
totalRefundAmount += itemRefundAmount;
...
DiscountAmount = -Math.Round((orderItem.DiscountAmount / orderItem.Quantity) * refundItem.Quantity, 2),
TaxAmount = -Math.Round((orderItem.TaxAmount / orderItem.Quantity) * refundItem.Quantity, 2),
Subtotal = -Math.Round((orderItem.Subtotal / orderItem.Quantity) * refundItem.Quantity, 2),
Total = -Math.Round(itemRefundAmount, 2),
...
var debtToReduce = isPartialRefund
    ? Math.Round((totalRefundAmount / originalOrder.Total) * originalOrder.AmountDue, 2)
    : originalOrder.AmountDue;
var cashRefundAmount = isPartialRefund
    ? Math.Round((totalRefundAmount / originalOrder.Total) * originalCashPayments, 2)
    : originalCashPayments;
```

Formula in Plain Math:

- UI max refundable quantity per line: `maxQuantity = soldQuantity - refundedQuantity`
- UI remaining refundable amount: `max(0, orderTotal - alreadyRefundedAmount)`
- UI partial refund preview: `sum(refundQuantity * (item.total / item.quantity))`
- Backend partial line refund: `refundLineTotal = (originalLineTotal / originalQty) * refundQty`
- Backend negative snapshots:
  - `refundSubtotal = -round2((originalSubtotal / originalQty) * refundQty)`
  - `refundTax = -round2((originalTax / originalQty) * refundQty)`
  - `refundDiscount = -round2((originalDiscount / originalQty) * refundQty)`
- Debt reversal ratio: `(refundAmount / originalOrderTotal) * originalAmountDue`
- Cash refund ratio: `(refundAmount / originalOrderTotal) * originalCashPayments`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `order.total` / `order.refundAmount` | Order details payload | Loaded order only | UI uses already-loaded order |
| `item.total` / `item.quantity` | Order item payload | Loaded order only | UI derives unit refund price from tax-inclusive line total |
| `originalOrder.Total` | Backend order entity | Tenant + branch | Authoritative refund ceiling |
| `originalOrder.AmountDue` | Backend order entity | Tenant + branch | Used to reduce customer debt proportionally |
| `originalCashPayments` | Original order payments | Tenant + branch | Used to determine cash-out movement |

Potential Issues:
- [ ] Tenant filter? yes - backend refund query enforces current tenant; frontend preview does not query.
- [ ] Branch filter? yes - backend refund query enforces current branch; frontend preview does not query.
- [ ] Cancelled excluded? yes - backend allows refund only for `Completed` or `PartiallyRefunded` orders.
- [ ] Rounding safe? no - frontend preview does not round per item, while backend rounds line snapshots and ratios.
- [ ] Discount before/after tax? yes - refund inherits previously discounted, tax-inclusive lines and reverses discount/tax proportionally.
- [ ] Refund handled? yes - both remaining refundable amount and prior refund amount are explicitly considered.
- [ ] Negative possible? yes - backend creates negative return lines and negative order totals by design.
- [ ] FE vs BE mismatch? yes - frontend preview uses `item.total / item.quantity` without proportional rounding of discount/tax components, so cents can drift from backend return order totals.

Risk Assessment:
- Level: High
- Layer: Frontend + Backend
- Scenario: Cashier previews a partial refund for 3.33, backend books 3.32 or 3.34 after proportional rounding, and the customer disputes the printed return total.

### Receipt Totals and Discount Rendering

Location:
- `backend/KasserPro.API/Controllers/OrdersController.cs`
- `backend/KasserPro.API/Controllers/CustomersController.cs`
- `frontend/src/utils/browserReceiptPrinter.ts`

Layer:
- Backend + Frontend

Purpose:
- Build printed receipt totals for orders and debt payments; browser fallback reconstructs discount when API print DTO is not used.

Code:
```cs
ItemDiscountsTotal = order.Items.Sum(i => i.DiscountAmount),
DiscountType = order.DiscountType,
DiscountValue = order.DiscountValue,
DiscountAmount = order.DiscountAmount,
NetTotal = order.Subtotal,
TaxAmount = order.TaxAmount,
TotalAmount = order.Total,
AmountPaid = order.AmountPaid,
ChangeAmount = order.ChangeAmount,
AmountDue = order.AmountDue,
```

```cs
NetTotal = payment.BalanceBefore,
TaxAmount = 0,
TotalAmount = payment.BalanceBefore,
AmountPaid = payment.Amount,
ChangeAmount = 0,
AmountDue = payment.BalanceAfter,
```

```ts
const netTotal = Math.abs(order.subtotal || 0);
const taxAmount = Math.abs(order.taxAmount || 0);
const totalAmount = Math.abs(order.total || 0);
const amountPaid = Math.abs(order.amountPaid || 0);
const changeAmount = Math.abs(order.changeAmount || 0);
const amountDue = Math.abs(order.amountDue || 0);
const discountAmount = Math.abs(netTotal - totalAmount + taxAmount);
```

Formula in Plain Math:

- Order print DTO uses stored backend values directly.
- Debt payment receipt uses:
  - `receiptNetTotal = debtBalanceBefore`
  - `receiptAmountDue = debtBalanceAfter`
- Browser fallback reconstructs discount as:
  - `abs(subtotal - total + taxAmount)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `order.Subtotal`, `TaxAmount`, `Total` | Persisted order | Order already scoped in controller | Authoritative when server print endpoint is used |
| `payment.BalanceBefore`, `BalanceAfter` | Customer debt payment row | Customer already scoped in controller | Debt receipt is balance-based, not invoice-based |
| `order.subtotal`, `order.total`, `order.taxAmount` | Browser fallback input | None in utility | May come from UI DTO, not necessarily print DTO |

Potential Issues:
- [ ] Tenant filter? yes - backend controllers resolve entities in scoped APIs; frontend fallback does not query.
- [ ] Branch filter? yes - backend order and customer APIs are scoped; fallback does not query.
- [ ] Cancelled excluded? no - receipt utility itself does not inspect status.
- [ ] Rounding safe? no - browser fallback reconstructs discount from already rounded fields, which can compound drift.
- [ ] Discount before/after tax? yes - reconstructed discount assumes discount reduced subtotal before tax.
- [ ] Refund handled? yes - browser fallback wraps values with `Math.abs(...)` for returns and debt receipts.
- [ ] Negative possible? no - browser fallback converts all displayed amounts to absolute values.
- [ ] FE vs BE mismatch? yes - browser fallback ignores explicit backend discount fields and can misstate discount whenever service charge or other adjustments exist.

Risk Assessment:
- Level: High
- Layer: Frontend
- Scenario: Receipt shows an invented discount because fallback derived it from subtotal and total, while the true delta was partly service charge rather than discount.

### Purchase Invoice Totals

Location:
- `frontend/src/pages/purchase-invoices/PurchaseInvoiceFormPage.tsx`
- `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`

Layer:
- Frontend + Backend

Purpose:
- Calculate draft purchase invoice subtotal, tax, and total in the form preview and persist the invoice header totals.

Code:
```ts
const roundCurrency = (value: number) => Number(value.toFixed(2));

const calculateSubtotal = () => {
  return roundCurrency(
    items.reduce((sum, item) => sum + item.quantity * item.purchasePrice, 0),
  );
};

const calculateTaxAmount = () => {
  return roundCurrency(calculateSubtotal() * (purchaseTaxRate / 100));
};

const calculateTotal = () => {
  return roundCurrency(calculateSubtotal() + calculateTaxAmount());
};
```

```cs
var itemTotal = itemRequest.Quantity * itemRequest.PurchasePrice;
subtotal += itemTotal;
...
invoice.Subtotal = subtotal;
invoice.TaxAmount = subtotal * (taxRate / 100);
invoice.Total = invoice.Subtotal + invoice.TaxAmount;
invoice.AmountDue = invoice.Total;
...
invoice.Subtotal = subtotal;
invoice.TaxAmount = subtotal * (invoice.TaxRate / 100);
invoice.Total = invoice.Subtotal + invoice.TaxAmount;
invoice.AmountDue = invoice.Total - invoice.AmountPaid;
```

Formula in Plain Math:

- Line total: `quantity * purchasePrice`
- Frontend subtotal: `round2(sum(line totals))`
- Frontend tax: `round2(subtotal * taxRate / 100)`
- Frontend total: `round2(subtotal + tax)`
- Backend subtotal: `sum(line totals)`
- Backend tax: `subtotal * taxRate / 100`
- Backend total: `subtotal + tax`
- Backend amount due:
  - create: `total`
  - update: `total - amountPaid`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `item.quantity`, `item.purchasePrice` | Purchase form state / request | Product must be purchasable | Service products are blocked in backend |
| `purchaseTaxRate` | Form state | None in UI | Backend uses tenant tax rate on create, stored invoice tax rate on update |
| `invoice.AmountPaid` | Persisted invoice | Invoice already tenant scoped | Only affects update path |

Potential Issues:
- [ ] Tenant filter? yes - backend invoice and supplier/product lookups are tenant-scoped; frontend preview is local only.
- [ ] Branch filter? yes - backend invoice persists branch ID; frontend preview is local only.
- [ ] Cancelled excluded? yes - update path only allows draft invoices.
- [ ] Rounding safe? no - frontend rounds every stage to 2 decimals, backend stores raw decimal sums and taxes until later payment rounding.
- [ ] Discount before/after tax? no - purchase invoice flow has no discount concept in current implementation.
- [ ] Refund handled? no - purchase returns are not part of this calculation.
- [ ] Negative possible? no - backend rejects negative purchase price and non-positive quantity.
- [ ] FE vs BE mismatch? yes - frontend preview can differ from backend persisted totals when purchase prices carry more than 2 decimal places.

Risk Assessment:
- Level: Medium
- Layer: Frontend + Backend
- Scenario: Supplier invoice preview shows 114.29 while backend persists 114.28 because the frontend rounded subtotal first and backend rounded only when later amounts were displayed.

### Purchase Invoice Costing and Payment Status

Location:
- `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
- `frontend/src/components/purchase-invoices/AddPaymentModal.tsx`

Layer:
- Backend + Frontend

Purpose:
- Update weighted average cost on invoice confirmation and maintain invoice payment balances/status after supplier payments.

Code:
```cs
var oldStock = balanceBefore;
var oldAvgCost = product.AverageCost ?? product.Cost ?? 0m;
var newStock = balanceBefore + item.Quantity;
if (newStock > 0)
{
    var totalOldValue = oldStock * oldAvgCost;
    var totalNewValue = item.Quantity * item.PurchasePrice;
    product.AverageCost = (totalOldValue + totalNewValue) / newStock;
}
```

```cs
invoice.AmountPaid = Math.Round(invoice.AmountPaid + request.Amount, 2);
invoice.AmountDue = Math.Round(invoice.Total - invoice.AmountPaid, 2);
RecalculateInvoiceStatus(invoice);
...
invoice.AmountPaid = Math.Round(Math.Max(0m, invoice.AmountPaid - payment.Amount), 2);
invoice.AmountDue = Math.Round(invoice.Total - invoice.AmountPaid, 2);
if (invoice.AmountPaid == 0)
{
    invoice.Status = PurchaseInvoiceStatus.Confirmed;
}
else if (invoice.AmountPaid > 0 && invoice.AmountDue > 0)
{
    invoice.Status = PurchaseInvoiceStatus.PartiallyPaid;
}
else if (invoice.AmountDue <= 0)
{
    invoice.Status = PurchaseInvoiceStatus.Paid;
}
```

```ts
const [amount, setAmount] = useState<string>(String(amountDue));
...
const numAmount = Number(amount) || 0;
if (numAmount > amountDue) {
  toast.error(`المبلغ يتجاوز المبلغ المستحق (${formatCurrency(amountDue)})`);
  return;
}
```

Formula in Plain Math:

- Weighted average cost:
  - `newAverageCost = ((oldStock * oldAverageCost) + (receivedQty * purchasePrice)) / (oldStock + receivedQty)`
- Supplier payment:
  - `amountPaid = round2(previousAmountPaid + paymentAmount)`
  - `amountDue = round2(invoiceTotal - amountPaid)`
- Status:
  - `Paid` if `amountDue <= 0`
  - `PartiallyPaid` if `amountPaid > 0` and `amountDue > 0`
  - `Confirmed` if no payment remains after deletion rollback
- UI max payment: `paymentAmount <= amountDue`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `balanceBefore` | BranchInventory before receipt | Tenant + branch | Authoritative branch stock |
| `product.AverageCost` | Product entity | Tenant product | Falls back to `Cost` then `0` |
| `request.Amount` | Supplier payment request | Invoice tenant scope | Must be `> 0` and `<= amountDue` |
| `amountDue` | Invoice entity / modal prop | Current invoice | Displayed in add-payment modal |

Potential Issues:
- [ ] Tenant filter? yes - invoice, product, and inventory lookups are tenant-scoped.
- [ ] Branch filter? yes - inventory update is branch-scoped; supplier debt itself is not updated here.
- [ ] Cancelled excluded? yes - payment add/delete blocks cancelled and invalid invoice states.
- [ ] Rounding safe? yes - payment balances are rounded to 2 decimals; average cost is stored as raw decimal division.
- [ ] Discount before/after tax? no - purchase invoice flow has no discount step.
- [ ] Refund handled? no - returned/partially returned purchase invoice flows are not covered in these calculations.
- [ ] Negative possible? no - add-payment path blocks payment above due and weighted average only runs when `newStock > 0`.
- [ ] FE vs BE mismatch? no - add-payment modal uses the server-provided due amount as its upper bound.

Risk Assessment:
- Level: High
- Layer: Backend
- Scenario: Purchase invoices can be fully paid while `Supplier.TotalDue` stays stale because runtime code updates invoice balances but does not update supplier debt aggregates.

### Customer Debt Balance and Payment

Location:
- `backend/KasserPro.Application/Services/Implementations/CustomerService.cs`
- `frontend/src/components/customers/DebtPaymentModal.tsx`
- `frontend/src/components/customers/CustomerDetailsModal.tsx`

Layer:
- Backend + Frontend

Purpose:
- Accumulate customer credit debt from unpaid sales, validate limits, accept debt payments, and preview the remaining balance in UI.

Code:
```cs
customer.TotalDue += amountDue;
...
var newTotalDue = customer.TotalDue + additionalAmount;
return newTotalDue <= customer.CreditLimit;
...
var balanceBefore = customer.TotalDue;
var balanceAfter = balanceBefore - request.Amount;
...
customer.TotalDue = balanceAfter;
...
customer.TotalDue -= amountToReduce;
if (customer.TotalDue < 0)
    customer.TotalDue = 0;
```

```ts
if (numAmount > customer.totalDue) {
  newErrors.amount = `المبلغ أكبر من الدين المستحق (${customer.totalDue.toFixed(2)} ج.م)`;
}
...
setFormData({ ...formData, amount: customer.totalDue / 2 })
...
{formatCurrency(customer.totalDue - numAmount)}
...
{formatCurrency(customer.creditLimit - customer.totalDue)}
{((customer.totalDue / customer.creditLimit) * 100).toFixed(0)}%
```

Formula in Plain Math:

- Debt accumulation: `customerTotalDue = previousTotalDue + saleAmountDue`
- Credit validation: `allowed = (creditLimit == 0) OR (previousTotalDue + requestedDue <= creditLimit)`
- Debt payment:
  - `balanceBefore = currentTotalDue`
  - `balanceAfter = balanceBefore - paymentAmount`
  - persisted `customerTotalDue = balanceAfter`
- Refund debt reduction: `customerTotalDue = max(currentTotalDue - amountToReduce, 0)`
- UI remaining balance preview: `customerTotalDue - enteredPayment`
- UI available credit: `creditLimit - customerTotalDue`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `customer.TotalDue` | Customer entity | Tenant scoped only | Shared across branches |
| `amountDue` | Unpaid sale amount | From order settlement | Posted only when credit sale occurs |
| `request.Amount` | Debt payment form | Customer scoped | Must be positive and not exceed due |
| `customer.creditLimit` | Customer entity | Customer scoped | `0` means unlimited credit in sale flow |

Potential Issues:
- [ ] Tenant filter? yes - customer service queries are tenant-scoped.
- [ ] Branch filter? no - customer debt is not partitioned by branch in service or report logic.
- [ ] Cancelled excluded? yes - debt is added on successful settlement and reduced on refund flow, not on cancelled orders.
- [ ] Rounding safe? no - debt payment preview in frontend uses raw subtraction without explicit rounding.
- [ ] Discount before/after tax? yes - debt reflects the final backend order total after discount and tax.
- [ ] Refund handled? yes - `ReduceDebtFromRefundAsync` floors balance at zero.
- [ ] Negative possible? no - backend floors refunded debt at zero and payment validation blocks overpayment.
- [ ] FE vs BE mismatch? no - frontend payment modal respects `customer.totalDue` and backend rejects overpayment.

Risk Assessment:
- Level: High
- Layer: Backend + Frontend
- Scenario: A customer can consume credit in Branch A and be blocked or misreported in Branch B because debt is global at tenant level, not branch level.

### Expense Payment and Cash Deduction

Location:
- `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs`
- `frontend/src/pages/expenses/ExpensesPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Persist expense amount, pay approved expenses, and deduct cash-register balance when payment method is cash.

Code:
```cs
var expense = new Expense
{
    ...
    Amount = request.Amount,
    ...
    Status = ExpenseStatus.Draft,
    ...
};
```

```cs
expense.Status = ExpenseStatus.Paid;
expense.PaymentMethod = request.PaymentMethod;
expense.PaymentDate = request.PaymentDate;
...
if (request.PaymentMethod == PaymentMethod.Cash)
{
    var cashBalanceResponse = await _cashRegisterService.GetCurrentBalanceAsync(_currentUserService.BranchId);
    ...
    if (cashBalanceResponse.Data!.CurrentBalance < expense.Amount)
    {
        ...
    }

    await _cashRegisterService.RecordTransactionAsync(
        CashRegisterTransactionType.Expense,
        expense.Amount,
        $"Expense: {expense.Description}",
        "Expense",
        expense.Id,
        expense.ShiftId ?? currentShift.Id);
}
```

```ts
const totalAmount = expenses.reduce(
  (sum, expense) => sum + expense.amount,
  0,
);
```

Formula in Plain Math:

- Expense draft amount: `expense.Amount = request.Amount`
- Cash payment validation: `currentCashBalance >= expense.Amount`
- Cash deduction: `cashRegisterBalanceAfter = cashRegisterBalanceBefore - expense.Amount`
- Expenses page header total: `sum(current page expense amounts)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `request.Amount` | Expense create/pay request | Tenant + branch expense scope | No tax/discount concept exists here |
| `cashBalanceResponse.Data.CurrentBalance` | Cash register service | Current tenant + branch | Checked only for cash expenses |
| `expenses` | Current paged expenses array | Current UI filters only | Not global total for all matching rows |

Potential Issues:
- [ ] Tenant filter? yes - expense queries are tenant-scoped.
- [ ] Branch filter? yes - expense queries and cash balance deduction are branch-scoped.
- [ ] Cancelled excluded? yes - only approved expenses can be paid.
- [ ] Rounding safe? no - expense amount is persisted directly; no service-layer rounding is applied on create/pay.
- [ ] Discount before/after tax? no - expense flow has no discount/tax calculation.
- [ ] Refund handled? no - there is no expense reversal workflow in the inspected code.
- [ ] Negative possible? yes - service does not visibly reject negative `request.Amount` on create/update in the shown paths.
- [ ] FE vs BE mismatch? yes - expenses page total is page-local only, while backend expenses report aggregates the whole filtered dataset.

Risk Assessment:
- Level: High
- Layer: Backend + Frontend
- Scenario: A negative or malformed expense amount could reverse cash unintentionally, and the page header may understate total spend because it sums only the current page.

### Cash Register Running Balance and Summary

Location:
- `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs`
- `frontend/src/pages/cash-register/CashRegisterDashboard.tsx`

Layer:
- Backend + Frontend

Purpose:
- Maintain running cash balance per branch, summarize register movements, and display dashboard inflow/outflow based on recent transactions.

Code:
```cs
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
    CashRegisterTransactionType.ShiftClose => amount,
    _ => currentBalance
};
```

```cs
OpeningBalance = firstTransaction?.BalanceBefore ?? 0;
ClosingBalance = lastTransaction?.BalanceAfter ?? openingBalance;
TotalTransfersIn = transactions.Where(t => t.Type == CashRegisterTransactionType.Transfer && t.Amount > 0).Sum(t => t.Amount),
TotalTransfersOut = transactions.Where(t => t.Type == CashRegisterTransactionType.Transfer && t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
...
return transactions.Last().BalanceAfter;
```

```ts
const incomingTotal = transactions
  .map((t) => t.balanceAfter - t.balanceBefore)
  .filter((delta) => delta > 0)
  .reduce((sum, delta) => sum + delta, 0);
const outgoingTotal = transactions
  .map((t) => t.balanceAfter - t.balanceBefore)
  .filter((delta) => delta < 0)
  .reduce((sum, delta) => sum + Math.abs(delta), 0);
```

Formula in Plain Math:

- Running balance:
  - increase on `Sale`, `Deposit`, `Adjustment`
  - decrease on `Refund`, `Withdrawal`, `Expense`, `SupplierPayment`
  - `Opening` and `ShiftClose` overwrite the stored balance snapshot
- Summary:
  - `openingBalance = firstTransaction.balanceBefore`
  - `closingBalance = lastTransaction.balanceAfter`
  - `totalTransfersIn = sum(transfer amounts > 0)`
  - `totalTransfersOut = sum(abs(transfer amounts < 0))`
- Dashboard cards:
  - `incomingTotal = sum(positive deltas among recent transactions)`
  - `outgoingTotal = sum(abs(negative deltas among recent transactions))`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `currentBalance` | Latest transaction for branch | Tenant + branch | Used as running base |
| `amount` | Cash register transaction request | Tenant + branch | Stored as positive amount in transfer rows |
| `transactions` | Summary query / dashboard recent list | Tenant + branch and optional date range | Dashboard uses only the loaded recent list |
| `balanceBefore`, `balanceAfter` | Persisted cash transaction snapshots | Same branch | Dashboard derives deltas from snapshots |

Potential Issues:
- [ ] Tenant filter? yes - balance and summary queries enforce current tenant.
- [ ] Branch filter? yes - balance and summary queries enforce branch.
- [ ] Cancelled excluded? yes - cash register works from transaction rows, not cancelled sales rows.
- [ ] Rounding safe? no - amounts are not explicitly rounded inside the switch or dashboard aggregates.
- [ ] Discount before/after tax? no - register consumes already settled payment amounts.
- [ ] Refund handled? yes - refund transaction type reduces balance.
- [ ] Negative possible? yes - balances and deltas can go negative after refunds, withdrawals, or adjustments.
- [ ] FE vs BE mismatch? yes - backend `TotalTransfersOut` expects negative transfer amounts, but transfer rows are stored as positive amounts, and dashboard inflow/outflow is only over the currently loaded recent transactions.

Risk Assessment:
- Level: High
- Layer: Backend + Frontend
- Scenario: Transfer-out totals show zero in cash-register summary while real branch cash moved out, causing reconciliation and inter-branch cash control failures.

### Shift Totals and Expected Balance

Location:
- `backend/KasserPro.Application/Services/Implementations/ShiftService.cs`
- `frontend/src/pages/shifts/ShiftPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Compute per-shift sales/payments and the expected closing cash balance used in shift close and shift dashboards.

Code:
```cs
private static (int TotalOrders, decimal TotalCash, decimal TotalCard, decimal TotalFawry, decimal TotalBankTransfer) CalculateShiftFinancials(
    IEnumerable<Order> orders)
{
    var completedOrders = orders.Where(o =>
        o.Status == OrderStatus.Completed
        || o.Status == OrderStatus.PartiallyRefunded
        || o.Status == OrderStatus.Refunded).ToList();

    var salesOrders = completedOrders.Where(o => o.OrderType != OrderType.Return).ToList();
    var returnOrders = completedOrders.Where(o => o.OrderType == OrderType.Return).ToList();
    ...
    var totalCash = Math.Round(
        salesPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
        - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)), 2);
    var totalCard = Math.Round(
        salesPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)
        - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)), 2);
    var totalFawry = Math.Round(
        salesPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)
        - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)), 2);
    var totalBankTransfer = Math.Round(
        salesPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount)
        - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount)), 2);
```

```cs
shift.ExpectedBalance = Math.Round(shift.OpeningBalance + totalCash, 2);
shift.Difference = Math.Round(shift.ClosingBalance - shift.ExpectedBalance, 2);
```

```tsx
{formatCurrency(
  currentShift.totalCash + currentShift.totalCard,
)}
...
{formatCurrency(
  currentShift.totalCard -
    currentShift.totalFawry -
    currentShift.totalBankTransfer,
)}
...
{formatCurrency(
  currentShift.openingBalance + currentShift.totalCash,
)}
```

Formula in Plain Math:

- `totalOrders = count(non-return completed sales orders)`
- `totalCash = salesCashPayments - abs(returnCashPayments)`
- `totalCard = salesCardPayments - abs(returnCardPayments)`
- `totalFawry = salesFawryPayments - abs(returnFawryPayments)`
- `totalBankTransfer = salesBankTransferPayments - abs(returnBankTransferPayments)`
- `expectedBalance = openingBalance + totalCash`
- `difference = closingBalance - expectedBalance`
- Shift page display:
  - `displayedTotalSales = totalCash + totalCard`
  - `displayedCardOnly = totalCard - totalFawry - totalBankTransfer`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `orders` | Shift order collection | Shift-scoped | Includes refunds and return orders |
| `Payments` | Payment records under shift orders | Same shift | Drives all payment breakdowns |
| `OpeningBalance`, `ClosingBalance` | Shift entity | Same shift | Used for cash expectation only |
| `currentShift.totalCard` | API shift DTO | Same shift | In UI it is treated as electronic aggregate |

Potential Issues:
- [ ] Tenant filter? yes - shift close/load paths are tenant-scoped.
- [ ] Branch filter? yes - shift and its orders belong to current branch.
- [ ] Cancelled excluded? yes - only `Completed`, `PartiallyRefunded`, and `Refunded` orders are included.
- [ ] Rounding safe? yes - shift payment totals and differences are rounded to 2 decimals.
- [ ] Discount before/after tax? no - shift totals are payment-based, not discount-calculation-based.
- [ ] Refund handled? yes - return-order payments are subtracted from sales payments.
- [ ] Negative possible? yes - net payment buckets can go negative if returns exceed same-method sales.
- [ ] FE vs BE mismatch? yes - frontend labels `totalCash + totalCard` as total sales and treats `totalCard` as an electronic aggregate even though backend `totalCard` is card-only and excludes Fawry/bank transfer.

Risk Assessment:
- Level: High
- Layer: Backend + Frontend
- Scenario: Shift UI understates or mislabels electronic collections, and expected cash can disagree with cash-register reconciliation because one model uses net cash sales while another uses branch register balance.

### Daily Sales Report

Location:
- `backend/KasserPro.Application/Services/Implementations/ReportService.cs`
- `backend/KasserPro.API/Controllers/ReportsController.cs`
- `frontend/src/pages/reports/DailyReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Build the shift-based daily report shown in UI and printed as a daily summary.

Code:
```cs
var totalCash = Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount) - refundedCash);
var totalCard = Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount) - refundedCard);
var totalFawry = Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount) - refundedFawry);
var totalOther = Math.Max(0, allPayments.Where(p => p.Method != PaymentMethod.Cash
                                          && p.Method != PaymentMethod.Card
                                          && p.Method != PaymentMethod.Fawry).Sum(p => p.Amount) - refundedOther);

var grossSales = completedOrders.Sum(o => o.Subtotal);
var totalItemDiscounts = completedOrders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
var totalOrderDiscounts = completedOrders.Sum(o => o.DiscountAmount);
var returnItemDiscounts = Math.Abs(returnOrders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount));
var returnOrderDiscounts = Math.Abs(returnOrders.Sum(o => o.DiscountAmount));
var totalDiscount = (totalItemDiscounts + totalOrderDiscounts) - (returnItemDiscounts + returnOrderDiscounts);
var totalTax = completedOrders.Sum(o => o.TaxAmount);
var totalSales = completedOrders.Sum(o => o.Total);
var netSales = grossSales - totalDiscount;
var totalRefunds = Math.Abs(returnOrders.Sum(o => o.Total));
var actualGrossSales = grossSales - Math.Abs(returnOrders.Sum(o => o.Subtotal));
var actualTotalTax = totalTax - Math.Abs(returnOrders.Sum(o => o.TaxAmount));
var actualTotalSales = totalSales - totalRefunds;
var actualNetSales = netSales - Math.Abs(returnOrders.Sum(o => o.Subtotal - o.DiscountAmount));
```

```cs
var totalPaid = report.TotalCash + report.TotalCard + report.TotalFawry + report.TotalOther;
var totalDeferred = report.TotalSales - totalPaid;
```

```cs
var shiftPayments = (s.Orders ?? new List<Domain.Entities.Order>())
    .Where(o => o.Status == OrderStatus.Completed
        || o.Status == OrderStatus.PartiallyRefunded
        || o.Status == OrderStatus.Refunded)
    .SelectMany(o => o.Payments ?? new List<Domain.Entities.Payment>())
    .ToList();

var shiftCash = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount), 2);
var shiftCard = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount), 2);
var shiftFawry = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount), 2);
var shiftOther = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount), 2);
...
TotalSales = shiftCash + shiftCard + shiftFawry + shiftOther,
```

```ts
const total = report?.totalSales || 1;
const percentage = (item.value / total) * 100;
```

Formula in Plain Math:

- Payment buckets:
  - `cash = max(0, completedCashPayments - refundedCashPayments)`
  - `card = max(0, completedCardPayments - refundedCardPayments)`
  - `fawry = max(0, completedFawryPayments - refundedFawryPayments)`
  - `other = max(0, completedOtherPayments - refundedOtherPayments)`
- Sales buckets:
  - `grossSales = sum(non-return order subtotals)`
  - `totalDiscount = (sales item discounts + sales order discounts) - (return item discounts + return order discounts)`
  - `actualTotalTax = sales tax - abs(return tax)`
  - `actualTotalSales = sales totals - abs(return totals)`
  - `actualNetSales = (grossSales - totalDiscount) - abs(return subtotal - return discount)`
- Print-only deferred amount:
  - `totalDeferred = totalSales - (cash + card + fawry + other)`
- Daily report shift rows:
  - `shiftTotalSales = shiftCash + shiftCard + shiftFawry + shiftOther`
- UI payment-share bar:
  - `paymentSharePercent = paymentBucket / totalSales * 100`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `shifts` | Closed shifts on report date | Tenant + branch + closed-at range | Egypt-local day boundaries |
| `completedOrders` | Shift orders excluding returns | Completed/PartiallyRefunded/Refunded only | Cancelled orders excluded |
| `returnOrders` | Shift orders where `OrderType == Return` | Same status filter | Used as refund source |
| `allPayments` | Payment records from completed sales | Same report day | Used for collection breakdown |
| `shiftPayments` | Payments under each closed shift | Same report day | Used for per-shift daily report cards |
| `report.totalSales` | Final daily report DTO | UI only | Used as bar-chart denominator |

Potential Issues:
- [ ] Tenant filter? yes - report queries enforce current tenant.
- [ ] Branch filter? yes - report queries enforce current branch.
- [ ] Cancelled excluded? yes - cancelled orders never enter completed/return sets.
- [ ] Rounding safe? no - aggregated sums are not explicitly rounded at every stage before DTO assignment.
- [ ] Discount before/after tax? yes - net sales are based on subtotal minus discounts before tax.
- [ ] Refund handled? yes - return orders and refund payments are subtracted explicitly.
- [ ] Negative possible? yes - net sales metrics can become negative on a day dominated by returns, though payment buckets are clamped at zero.
- [ ] FE vs BE mismatch? yes - UI payment-share percentages use `totalSales` as denominator, while the print controller separately derives `totalDeferred`; collections and sales totals intentionally diverge when credit sales exist.

Risk Assessment:
- Level: High
- Layer: Backend + Frontend
- Scenario: Management sees strong sales and low collections on the same day, but without understanding `totalDeferred`, mistakes credit exposure for cash leakage.

### Sales Report

Location:
- `backend/KasserPro.Application/Services/Implementations/ReportService.cs`
- `frontend/src/pages/reports/SalesReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Summarize net sales, cost, gross profit, average order value, and day-by-day sales activity over a date range.

Code:
```cs
var grossSales = salesOrders.Sum(o => o.Total);
var totalRefunds = Math.Abs(returnOrders.Sum(o => o.Total));
var totalSales = grossSales - totalRefunds;
var totalCost = salesOrders.SelectMany(o => o.Items)
    .Sum(i => (i.UnitCost ?? 0) * i.Quantity);
var returnedCost = returnOrders.SelectMany(o => o.Items)
    .Sum(i => (i.UnitCost ?? 0) * Math.Abs(i.Quantity));
var netCost = totalCost - returnedCost;
...
AverageOrderValue = salesOrders.Count > 0 ? totalSales / salesOrders.Count : 0,
```

```ts
const maxSales = Math.max(
  ...report.dailySales.map((d) => d.sales),
);
const percentage =
  maxSales > 0 ? (day.sales / maxSales) * 100 : 0;
```

Formula in Plain Math:

- `grossSales = sum(non-return order totals)`
- `totalRefunds = abs(sum(return order totals))`
- `totalSales = grossSales - totalRefunds`
- `totalCost = sum(sold item unitCost * quantity)`
- `returnedCost = sum(abs(return item quantity) * unitCost)`
- `grossProfit = totalSales - (totalCost - returnedCost)`
- `averageOrderValue = totalSales / salesOrderCount`
- UI daily bar width: `day.sales / max(day.sales across range) * 100`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `allOrders` | Orders in selected date range | Tenant + branch + completed statuses | Excludes cancelled/draft |
| `salesOrders` | `OrderType != Return` | Same | Revenue side |
| `returnOrders` | `OrderType == Return` | Same | Refund side |
| `UnitCost` | Stored order-item snapshot | Same | Historical cost basis |
| `report.dailySales` | Sales report DTO | UI only | Used for chart scaling |

Potential Issues:
- [ ] Tenant filter? yes - report queries enforce current tenant.
- [ ] Branch filter? yes - report queries enforce current branch.
- [ ] Cancelled excluded? yes - only completed/refunded statuses are included.
- [ ] Rounding safe? no - report aggregates use raw decimal division for averages and totals.
- [ ] Discount before/after tax? no - report works from final order totals, not from pre-tax discount stages.
- [ ] Refund handled? yes - return orders and returned cost are netted out.
- [ ] Negative possible? yes - net sales or gross profit can go negative if returns exceed sales.
- [ ] FE vs BE mismatch? no - frontend chart normalization does not change monetary values, only bar widths.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: A period with many returns can produce negative average order economics, which is valid but may surprise users if they expected sales-only counting.

### Profit and Loss Report

Location:
- `backend/KasserPro.Infrastructure/Services/FinancialReportService.cs`
- `frontend/src/pages/reports/ProfitLossReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Produce management P&L metrics: sales, discounts, tax, COGS, expenses, gross profit, net profit, and profit margins.

Code:
```cs
var grossSales = orders.Sum(o => o.Subtotal);
var totalItemDiscounts = orders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
var totalOrderDiscounts = orders.Sum(o => o.DiscountAmount);
var totalDiscount = totalItemDiscounts + totalOrderDiscounts;
var netSales = grossSales - totalDiscount;
var totalTax = orders.Sum(o => o.TaxAmount);
var totalRevenue = orders.Sum(o => o.Total);
var refundsAmount = Math.Abs(returnOrders.Sum(o => o.Total));

var actualNetSales = netSales - refundsAmount;
var actualTotalRevenue = totalRevenue - refundsAmount;
...
var grossProfit = actualNetSales - netCost;
var grossProfitMargin = actualNetSales > 0 ? (grossProfit / actualNetSales) * 100 : 0;
...
var netProfit = grossProfit - totalExpenses;
var netProfitMargin = actualTotalRevenue > 0 ? (netProfit / actualTotalRevenue) * 100 : 0;
...
AverageOrderValue = orders.Count(o => o.Status != OrderStatus.Refunded) > 0
    ? actualTotalRevenue / orders.Count(o => o.Status != OrderStatus.Refunded)
    : 0,
```

Formula in Plain Math:

- `grossSales = sum(non-return order subtotals)`
- `totalDiscount = sum(item discounts) + sum(order discounts)`
- `netSales = grossSales - totalDiscount`
- `totalRevenue = sum(non-return order totals)`
- `refundsAmount = abs(sum(return order totals))`
- `actualNetSales = netSales - refundsAmount`
- `actualTotalRevenue = totalRevenue - refundsAmount`
- `grossProfit = actualNetSales - netCost`
- `grossProfitMargin = grossProfit / actualNetSales * 100`
- `netProfit = grossProfit - totalExpenses`
- `netProfitMargin = netProfit / actualTotalRevenue * 100`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `orders` | Completed non-return orders | Tenant + branch + selected range | Revenue basis |
| `returnOrders` | Return orders | Tenant + branch + selected range | Refund basis |
| `expenses` | Expenses in selected range | Tenant + branch | Used for net profit only |
| `UnitCost` | Order-item cost snapshot | Same order set | COGS basis |
| `expensesByCategory` | Expense grouping | Same expense range | UI shows category percentages from backend |

Potential Issues:
- [ ] Tenant filter? yes - order and expense queries enforce current tenant.
- [ ] Branch filter? yes - order and expense queries enforce current branch.
- [ ] Cancelled excluded? yes - only completed/refunded statuses are included.
- [ ] Rounding safe? no - report calculations rely on raw aggregated decimals and percentage division.
- [ ] Discount before/after tax? yes - `netSales` uses subtotal minus discounts before tax, but later mixes with tax-inclusive refunds.
- [ ] Refund handled? yes - refunds are subtracted, but the basis is inconsistent.
- [ ] Negative possible? yes - gross or net profit can be negative.
- [ ] FE vs BE mismatch? no - frontend renders backend metrics directly.

Risk Assessment:
- Level: High
- Layer: Backend
- Scenario: `actualNetSales` subtracts tax-inclusive refunds from pre-tax `netSales`, understating sales and gross profit whenever tax is enabled and returns exist.

### Expenses Report

Location:
- `backend/KasserPro.Infrastructure/Services/FinancialReportService.cs`
- `frontend/src/pages/reports/ExpensesReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Summarize expense totals, counts, averages, payment-method totals, and category percentages.

Code:
```cs
var totalExpenses = expenses.Sum(e => e.Amount);
var totalExpenseCount = expenses.Count;
var averageExpenseAmount = totalExpenseCount > 0 ? totalExpenses / totalExpenseCount : 0;
...
Percentage = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
...
var cashExpenses = expenses.Where(e => e.PaymentMethod == PaymentMethod.Cash).Sum(e => e.Amount);
var cardExpenses = expenses.Where(e => e.PaymentMethod == PaymentMethod.Card).Sum(e => e.Amount);
var otherExpenses = expenses.Where(e => e.PaymentMethod != PaymentMethod.Cash
                                     && e.PaymentMethod != PaymentMethod.Card).Sum(e => e.Amount);
```

Formula in Plain Math:

- `totalExpenses = sum(expense amounts)`
- `totalExpenseCount = count(expenses)`
- `averageExpenseAmount = totalExpenses / totalExpenseCount`
- `categoryPercentage = categoryAmount / totalExpenses * 100`
- `cashExpenses = sum(cash expense amounts)`
- `cardExpenses = sum(card expense amounts)`
- `otherExpenses = sum(non-cash non-card expense amounts)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `expenses` | Expense query result | Tenant + branch + date range | Dataset for report |
| `PaymentMethod` | Expense entity | Same dataset | Null/non-cash-card falls into other |
| `g.Sum(e => e.Amount)` | Category group aggregate | Same dataset | Used for category bars and percentages |

Potential Issues:
- [ ] Tenant filter? yes - expense query enforces current tenant.
- [ ] Branch filter? yes - expense query enforces current branch.
- [ ] Cancelled excluded? yes - only expenses that match the selected report filter enter totals.
- [ ] Rounding safe? no - averages and percentages use raw decimal division.
- [ ] Discount before/after tax? no - expense report has no discount/tax logic.
- [ ] Refund handled? no - there is no expense reversal path in the inspected reporting code.
- [ ] Negative possible? yes - negative expense rows would directly reduce totals if they exist.
- [ ] FE vs BE mismatch? no - frontend renders backend aggregates directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: A corrective negative expense can silently reduce total spend and distort category percentages because the report treats all amounts symmetrically.

### Branch Inventory Valuation

Location:
- `backend/KasserPro.Infrastructure/Services/InventoryReportService.cs`
- `frontend/src/pages/reports/BranchInventoryReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Show branch-level stock quantity, low-stock counts, and inventory value based on average cost.

Code:
```cs
AverageCost = bi.Product.AverageCost,
TotalValue = bi.Quantity * (bi.Product.AverageCost ?? 0),
...
TotalQuantity = items.Sum(i => i.Quantity),
LowStockCount = items.Count(i => i.IsLowStock),
TotalValue = items.Sum(i => i.TotalValue ?? 0),
```

Formula in Plain Math:

- Per item:
  - `itemValue = quantity * averageCost`
  - `isLowStock = quantity <= reorderLevel`
- Report header:
  - `totalQuantity = sum(item quantities)`
  - `lowStockCount = count(low-stock items)`
  - `totalValue = sum(item inventory values)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `BranchInventory.Quantity` | Branch inventory table | Tenant + branch | Source of truth for stock |
| `Product.AverageCost` | Product entity | Same tenant | Falls back to zero here |
| `ReorderLevel` | Branch inventory row | Same branch | Low-stock threshold |

Potential Issues:
- [ ] Tenant filter? yes - report uses tenant-scoped branch inventory.
- [ ] Branch filter? yes - this report is branch-specific.
- [ ] Cancelled excluded? yes - inventory comes from stock state, not order rows.
- [ ] Rounding safe? no - inventory value uses raw multiplication without explicit rounding.
- [ ] Discount before/after tax? no - stock valuation uses cost only.
- [ ] Refund handled? yes - branch inventory already reflects net movements after returns/adjustments.
- [ ] Negative possible? yes - branch quantity can be negative if stock controls allow it elsewhere.
- [ ] FE vs BE mismatch? no - page renders report totals directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: Inventory value can be understated at zero when `AverageCost` is null even though physical stock exists.

### Unified Inventory Valuation

Location:
- `backend/KasserPro.Infrastructure/Services/InventoryReportService.cs`
- `frontend/src/pages/reports/UnifiedInventoryReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Aggregate stock quantities and value across branches for each product and then sum them again in the page header.

Code:
```cs
var totalQuantity = productInventories.Sum(bi => bi.Quantity);
var lowStockBranchCount = productInventories.Count(bi => bi.Quantity <= bi.ReorderLevel);
...
TotalValue = totalQuantity * (product.AverageCost ?? 0),
```

```ts
const totalProducts = reports.length;
const totalQuantity = reports.reduce((sum, r) => sum + r.totalQuantity, 0);
const totalValue = reports.reduce((sum, r) => sum + (r.totalValue || 0), 0);
const lowStockProducts = reports.filter(
  (r) => r.lowStockBranchCount > 0,
).length;
```

Formula in Plain Math:

- Per product across branches:
  - `totalQuantity = sum(branch quantities)`
  - `lowStockBranchCount = count(branches where quantity <= reorderLevel)`
  - `totalValue = totalQuantity * averageCost`
- Page header:
  - `totalProducts = row count`
  - `headerQuantity = sum(report.totalQuantity)`
  - `headerValue = sum(report.totalValue)`
  - `lowStockProducts = count(rows with lowStockBranchCount > 0)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `productInventories` | Branch inventory rows per product | Tenant, optional category/low-stock filters | Multi-branch aggregate |
| `product.AverageCost` | Product entity | Same tenant | Used once per product for total value |
| `reports` | API response in page | Same report filters | Header totals are page-wide over returned dataset |

Potential Issues:
- [ ] Tenant filter? yes - backend report is tenant-scoped.
- [ ] Branch filter? no - unified report intentionally spans branches.
- [ ] Cancelled excluded? yes - valuation uses inventory state, not order rows.
- [ ] Rounding safe? no - report uses raw multiplication and frontend raw reduction.
- [ ] Discount before/after tax? no - stock valuation uses cost only.
- [ ] Refund handled? yes - inventory state should already include net returns/adjustments.
- [ ] Negative possible? yes - negative branch quantities propagate into unified totals.
- [ ] FE vs BE mismatch? no - page header simply re-sums backend rows.

Risk Assessment:
- Level: Medium
- Layer: Backend + Frontend
- Scenario: Negative stock in one branch can offset real stock elsewhere and understate unified inventory value for the entire product.

### Low Stock Summary

Location:
- `backend/KasserPro.Infrastructure/Services/InventoryReportService.cs`
- `frontend/src/pages/reports/LowStockSummaryReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Quantify shortage versus reorder levels and estimate restock cost/value.

Code:
```cs
Shortage = Math.Max(0, bi.ReorderLevel - bi.Quantity),
...
var totalQuantity = branchDetails.Sum(bd => bd.Quantity);
var totalReorderLevel = branchDetails.Sum(bd => bd.ReorderLevel);
var shortage = Math.Max(0, totalReorderLevel - totalQuantity);
...
EstimatedRestockCost = shortage * (product.AverageCost ?? 0),
...
EstimatedRestockValue = items.Sum(i => i.EstimatedRestockCost ?? 0),
```

Formula in Plain Math:

- Branch shortage: `max(0, reorderLevel - quantity)`
- Product shortage across branches: `max(0, sum(reorderLevels) - sum(quantities))`
- Estimated restock cost per product: `shortage * averageCost`
- Report total restock value: `sum(product estimated restock cost)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `Quantity` | Branch inventory rows | Tenant-wide with filters | Current stock by branch |
| `ReorderLevel` | Branch inventory rows | Same rows | Branch threshold |
| `AverageCost` | Product entity | Same tenant | Falls back to zero |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? no - summary intentionally rolls across branches.
- [ ] Cancelled excluded? yes - based on inventory state, not order status.
- [ ] Rounding safe? no - restock value uses raw multiplication.
- [ ] Discount before/after tax? no - no sales pricing dimension here.
- [ ] Refund handled? yes - current inventory reflects net stock after returns.
- [ ] Negative possible? no - shortage is floored at zero.
- [ ] FE vs BE mismatch? no - page renders backend values directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: Products with null average cost show zero restock value, hiding the actual cash required to replenish low stock.

### Transfer History Totals

Location:
- `backend/KasserPro.Infrastructure/Services/InventoryReportService.cs`
- `frontend/src/pages/reports/TransferHistoryReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Count transfers, total completed transferred quantity, and net branch movement.

Code:
```cs
var totalQuantityTransferred = transfers
    .Where(t => t.Status == InventoryTransferStatus.Completed)
    .Sum(t => t.Quantity);
...
NetChange = quantityReceived - quantitySent
```

Formula in Plain Math:

- `totalTransfers = count(transfer rows)`
- `totalQuantityTransferred = sum(completed transfer quantities)`
- `netChange = quantityReceived - quantitySent`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `transfers` | Inventory transfer rows | Tenant-scoped, optional branch/date filters | Header total basis |
| `Status` | Transfer entity | Same rows | Only completed rows affect quantity transferred |
| `quantityReceived`, `quantitySent` | Per-branch transfer aggregations | Same dataset | Used for branch net change |

Potential Issues:
- [ ] Tenant filter? yes - transfer report is tenant-scoped.
- [ ] Branch filter? yes - transfer detail can be branch-filtered.
- [ ] Cancelled excluded? yes - quantity-transferred total includes completed transfers only.
- [ ] Rounding safe? yes - quantity-only calculation, no currency arithmetic.
- [ ] Discount before/after tax? no - inventory transfer has no pricing logic.
- [ ] Refund handled? no - not applicable to stock transfer domain.
- [ ] Negative possible? yes - net branch change can be negative.
- [ ] FE vs BE mismatch? no - frontend shows backend totals directly.

Risk Assessment:
- Level: Low
- Layer: Backend
- Scenario: A branch with consistent outbound transfers can display negative net change, which is correct but requires context to avoid false shrinkage alarms.

### Product Movement Report

Location:
- `backend/KasserPro.Infrastructure/Services/ProductReportService.cs`
- `frontend/src/pages/reports/ProductMovementReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Show sold quantity, revenue, cost, opening/closing stock reconstruction, turnover, and days to sell out per product.

Code:
```cs
var qtySold = soldItems.Sum(oi => oi.Quantity) - Math.Abs(returnedItems.Sum(oi => oi.Quantity));
var revenue = soldItems.Sum(oi => oi.Total) - Math.Abs(returnedItems.Sum(oi => oi.Total));
var cost = soldItems.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity)
         - returnedItems.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity));
...
OpeningStock = currentStock + qtySold + tOut - purchased - tIn,
ClosingStock = currentStock,
TurnoverRate = currentStock > 0 ? Math.Round((decimal)qtySold / currentStock, 2) : 0,
DaysToSellOut = qtySold > 0
    ? (int)Math.Ceiling((decimal)currentStock / ((decimal)qtySold / Math.Max(1, (decimal)(toDate - fromDate).TotalDays)))
    : 999
```

Formula in Plain Math:

- `qtySold = soldQty - returnedQty`
- `revenue = soldRevenue - returnedRevenue`
- `cost = soldCost - returnedCost`
- `grossProfit = revenue - cost`
- `openingStock = currentStock + qtySold + transfersOut - purchases - transfersIn`
- `turnoverRate = qtySold / currentStock`
- `daysToSellOut = currentStock / averageDailySales`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `soldItems` | Order items from non-return orders | Tenant + branch + date range | Revenue basis |
| `returnedItems` | Order items from return orders | Same scope | Return basis |
| `purchaseItems` | Purchase invoice items | Same scope | Used for opening-stock reconstruction |
| `currentStock` | Current branch inventory | Same branch | Used as closing stock |

Potential Issues:
- [ ] Tenant filter? yes - report queries are tenant-scoped.
- [ ] Branch filter? yes - movement report is branch-scoped.
- [ ] Cancelled excluded? yes - report uses completed/refunded orders only.
- [ ] Rounding safe? yes - turnover margin outputs are rounded where exposed.
- [ ] Discount before/after tax? no - revenue uses line totals after tax/discount as stored.
- [ ] Refund handled? yes - returned quantity, revenue, and cost are explicitly netted.
- [ ] Negative possible? yes - revenue, qty sold, or gross profit can go negative.
- [ ] FE vs BE mismatch? no - frontend renders backend DTOs directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: Opening stock is reconstructed rather than snapshotted, so historical inaccuracies in purchases or transfers will distort turnover analysis.

### Profitable Products Report

Location:
- `backend/KasserPro.Infrastructure/Services/ProductReportService.cs`
- `frontend/src/pages/reports/ProfitableProductsReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Rank products by revenue, cost, profit, and profit margin.

Code:
```cs
ProfitMargin = revenue > 0 ? Math.Round(profit / revenue * 100, 2) : 0,
AverageSellingPrice = qty > 0 ? Math.Round(revenue / qty, 2) : 0,
AverageCost = qty > 0 ? Math.Round(cost / qty, 2) : 0
...
AverageProfitMargin = totalRevenue > 0 ? Math.Round(totalProfit / totalRevenue * 100, 2) : 0,
```

Formula in Plain Math:

- `profit = revenue - cost`
- `profitMargin = profit / revenue * 100`
- `averageSellingPrice = revenue / quantity`
- `averageCost = cost / quantity`
- `averageProfitMargin = totalProfit / totalRevenue * 100`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `revenue` | Net product revenue after returns | Tenant + branch + date range | Derived in report service |
| `cost` | Net product cost after returns | Same scope | Uses order-item `UnitCost` |
| `qty` | Net product quantity | Same scope | Used for averages |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - report is branch-scoped.
- [ ] Cancelled excluded? yes - built from completed/refunded order sets.
- [ ] Rounding safe? yes - margin and averages are explicitly rounded.
- [ ] Discount before/after tax? no - profitability uses stored line totals.
- [ ] Refund handled? yes - returns are netted by product.
- [ ] Negative possible? yes - low-margin or over-returned products can have negative profit and margin.
- [ ] FE vs BE mismatch? no - frontend renders backend metrics directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: Products with tiny or zero revenue but non-zero returns can swing ranking sharply, making margin-based sorting volatile.

### COGS Report

Location:
- `backend/KasserPro.Infrastructure/Services/ProductReportService.cs`
- `frontend/src/pages/reports/CogsReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Estimate opening inventory, purchases, closing inventory, COGS, revenue, and gross profit for the selected period.

Code:
```cs
var totalPurchases = purchases.Sum(p => p.Total);
var totalRevenue = orderItems.Sum(oi => oi.Total) - Math.Abs(returnItemsCogs.Sum(oi => oi.Total));
var totalCost = orderItems.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity)
              - returnItemsCogs.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity));
...
var closingInventoryValue = currentInventory.Sum(bi =>
    bi.Quantity * (bi.Product.Cost ?? bi.Product.AverageCost ?? bi.Product.Price));
var openingInventoryValue = closingInventoryValue + totalCost - totalPurchases;
var cogs = openingInventoryValue + totalPurchases - closingInventoryValue;
var grossProfit = totalRevenue - cogs;
...
OpeningInventoryValue = Math.Max(0, openingInventoryValue),
CostOfGoodsSold = Math.Max(0, cogs),
```

Formula in Plain Math:

- `totalPurchases = sum(purchase invoice totals)`
- `totalRevenue = sold line totals - returned line totals`
- `closingInventoryValue = sum(currentQty * fallbackCost)`
- `openingInventoryValue = closingInventoryValue + totalCost - totalPurchases`
- `COGS = openingInventoryValue + totalPurchases - closingInventoryValue`
- `grossProfit = totalRevenue - COGS`
- Output floors:
  - `openingInventoryValue = max(0, openingInventoryValue)`
  - `COGS = max(0, COGS)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `purchases` | Purchase invoices in range | Tenant + branch | Uses invoice totals, not line net cost only |
| `orderItems` | Non-return order items | Same scope | Revenue and cost basis |
| `currentInventory` | Current branch inventory | Same branch | Used for closing inventory estimate |
| `Product.Cost ?? AverageCost ?? Price` | Product entity | Same tenant | Can fall back to selling price |

Potential Issues:
- [ ] Tenant filter? yes - report queries are tenant-scoped.
- [ ] Branch filter? yes - report is branch-scoped.
- [ ] Cancelled excluded? yes - sales side uses completed/refunded order sets.
- [ ] Rounding safe? no - COGS is calculated from raw decimal aggregates and then floored.
- [ ] Discount before/after tax? no - revenue uses stored line totals.
- [ ] Refund handled? yes - return lines are subtracted from revenue and cost.
- [ ] Negative possible? no - final exposed opening value and COGS are clamped to zero.
- [ ] FE vs BE mismatch? no - frontend displays DTO directly.

Risk Assessment:
- Level: High
- Layer: Backend
- Scenario: Falling back to selling price for inventory value can materially overstate inventory and understate COGS when cost fields are missing.

### Top Customers Report

Location:
- `backend/KasserPro.Infrastructure/Services/CustomerReportService.cs`
- `frontend/src/pages/reports/TopCustomersReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Rank customers by net spend and expose average order value and current outstanding balance.

Code:
```cs
var totalRefundsForCustomers = returnsByCustomer.Values.Sum();
var totalRevenue = ordersWithCustomers.Sum(o => o.Total) - totalRefundsForCustomers;
var averageCustomerValue = totalCustomers > 0 ? totalRevenue / totalCustomers : 0;
...
TotalSpent = spent,
AverageOrderValue = g.Count() > 0 ? spent / g.Count() : 0,
OutstandingBalance = g.First().Customer!.TotalDue
```

Formula in Plain Math:

- `totalRevenue = sum(customer order totals) - total customer refunds`
- `averageCustomerValue = totalRevenue / customerCount`
- Per customer:
  - `totalSpent = net customer spend`
  - `averageOrderValue = totalSpent / orderCount`
  - `outstandingBalance = current Customer.TotalDue`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `ordersWithCustomers` | Orders linked to customers | Tenant + branch + date range | Customer-only orders |
| `returnsByCustomer` | Return orders grouped by customer | Same scope | Used to net spend |
| `Customer.TotalDue` | Customer entity | Tenant scope | Current balance, not period-only |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - spending side is branch-scoped.
- [ ] Cancelled excluded? yes - report uses completed/refunded orders only.
- [ ] Rounding safe? no - averages use raw division.
- [ ] Discount before/after tax? no - spend is based on stored order totals.
- [ ] Refund handled? yes - returns are netted from spend.
- [ ] Negative possible? yes - net spend can be negative for heavy refund customers.
- [ ] FE vs BE mismatch? no - frontend displays backend values directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: A customer's outstanding balance can look unrelated to the selected branch/date range because it uses current customer debt, not period-specific branch debt.

### Customer Debts Report

Location:
- `backend/KasserPro.Infrastructure/Services/CustomerReportService.cs`
- `frontend/src/pages/reports/CustomerDebtsReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Show total outstanding customer debt, over-limit debtors, aging brackets, and customer-level credit exposure.

Code:
```cs
var customersWithDebt = await _context.Customers
    .Where(c => c.TenantId == tenantId
             && c.IsActive
             && c.TotalDue > 0)
    .ToListAsync();
...
IsOverLimit = customer.CreditLimit > 0 && customer.TotalDue > customer.CreditLimit
...
bracket.Percentage = totalOutstandingAmount > 0
    ? Math.Round((bracket.TotalAmount / totalOutstandingAmount) * 100, 2)
    : 0;
```

Formula in Plain Math:

- `totalOutstandingAmount = sum(active customer totalDue)`
- `isOverLimit = creditLimit > 0 AND totalDue > creditLimit`
- Aging bracket percentage: `bracketAmount / totalOutstandingAmount * 100`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `Customers.TotalDue` | Customer table | Tenant only | No branch filter present |
| `CreditLimit` | Customer table | Same customers | Used for over-limit flag |
| `bracket.TotalAmount` | Report bracket aggregate | Same debt dataset | Used for aging percentages |

Potential Issues:
- [ ] Tenant filter? yes - customer query is tenant-scoped.
- [ ] Branch filter? no - debt report does not filter by branch.
- [ ] Cancelled excluded? yes - debt is current-balance based, not open-order status based.
- [ ] Rounding safe? yes - bracket percentages are rounded to 2 decimals.
- [ ] Discount before/after tax? no - report consumes current debt balances only.
- [ ] Refund handled? yes - refund debt reductions already feed `Customer.TotalDue`.
- [ ] Negative possible? no - source query requires `TotalDue > 0`.
- [ ] FE vs BE mismatch? no - frontend displays backend totals directly.

Risk Assessment:
- Level: High
- Layer: Backend
- Scenario: Branch managers can see tenant-wide customer debt instead of branch-specific exposure, breaking branch isolation and skewing collections priorities.

### Customer Activity Report

Location:
- `backend/KasserPro.Infrastructure/Services/CustomerReportService.cs`
- `frontend/src/pages/reports/CustomerActivityReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Compare new vs returning customer revenue and compute retention/churn style metrics.

Code:
```cs
var customerRevenue = customerOrders.Sum(o => o.Total);
...
var averageNewCustomerValue = newCustomers > 0 ? newCustomerRevenue / newCustomers : 0;
var averageReturningCustomerValue = returningCustomers > 0 ? returningCustomerRevenue / returningCustomers : 0;
var retentionRate = totalCustomers > 0 ? (decimal)returningCustomers / totalCustomers * 100 : 0;
var churnRate = 100 - retentionRate;
```

Formula in Plain Math:

- `customerRevenue = sum(customer order totals)`
- `averageNewCustomerValue = newCustomerRevenue / newCustomerCount`
- `averageReturningCustomerValue = returningCustomerRevenue / returningCustomerCount`
- `retentionRate = returningCustomers / totalCustomers * 100`
- `churnRate = 100 - retentionRate`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `customerOrders` | Orders in range | Tenant + branch + date range | Revenue basis |
| `newCustomers`, `returningCustomers` | Customer segmentation logic | Same dataset | Used for averages and rates |
| `totalCustomers` | Customers with activity in scope | Same dataset | Denominator for retention |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - report is branch-scoped.
- [ ] Cancelled excluded? yes - activity is based on completed/refunded orders.
- [ ] Rounding safe? no - rates and averages are raw divisions without final rounding everywhere.
- [ ] Discount before/after tax? no - activity revenue uses stored order totals.
- [ ] Refund handled? no - report does not net customer revenue by returns in the shown formula.
- [ ] Negative possible? yes - revenue and averages can be overstated or effectively negative after heavy refund activity outside this report.
- [ ] FE vs BE mismatch? no - frontend renders backend values directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: A returning customer with large refunds still looks highly valuable because this report sums order totals without explicit refund netting.

### Supplier Purchases Report

Location:
- `backend/KasserPro.Infrastructure/Services/SupplierReportService.cs`
- `frontend/src/pages/reports/SupplierPurchasesReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Summarize purchase totals, paid totals, outstanding balances, and invoice counts per supplier.

Code:
```cs
var totalPurchases = g.Sum(pi => pi.Total);
var totalPaid = g.Sum(pi => pi.AmountPaid);
...
Outstanding = totalPurchases - totalPaid,
...
TotalPurchases = supplierDetails.Sum(s => s.TotalPurchases),
TotalPaid = supplierDetails.Sum(s => s.TotalPaid),
TotalOutstanding = supplierDetails.Sum(s => s.Outstanding),
```

Formula in Plain Math:

- Per supplier:
  - `totalPurchases = sum(invoice totals)`
  - `totalPaid = sum(invoice amountPaid)`
  - `outstanding = totalPurchases - totalPaid`
- Report totals:
  - `totalPurchases = sum(supplier totalPurchases)`
  - `totalPaid = sum(supplier totalPaid)`
  - `totalOutstanding = sum(supplier outstanding)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `PurchaseInvoices` | Purchase invoice dataset | Tenant + branch + date range | Source for supplier aggregates |
| `AmountPaid` | Invoice entity | Same scope | Posted payments only |
| `Total` | Invoice entity | Same scope | Includes tax |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - supplier purchase report is branch-scoped.
- [ ] Cancelled excluded? yes - report dataset excludes irrelevant statuses by query design.
- [ ] Rounding safe? no - outstanding is raw subtraction on aggregated decimals.
- [ ] Discount before/after tax? no - purchase invoices have no discount logic in current implementation.
- [ ] Refund handled? no - purchase returns are not netted in this report.
- [ ] Negative possible? yes - overpayment could make outstanding negative if invoice integrity drifts.
- [ ] FE vs BE mismatch? no - frontend renders backend values directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: Supplier outstanding exposure can be wrong if returned or cancelled purchase documents are not normalized the same way as paid invoices in reporting filters.

### Supplier Debts Report

Location:
- `backend/KasserPro.Infrastructure/Services/SupplierReportService.cs`
- `frontend/src/pages/reports/SupplierDebtsReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Report suppliers with debt, total outstanding amount, overdue debt, and unpaid invoice counts.

Code:
```cs
var suppliersWithDebt = await _context.Suppliers
    .Where(s => s.TenantId == tenantId
              && s.BranchId == branchId
              && s.IsActive
              && s.TotalDue > 0)
    .ToListAsync();
...
TotalDue = supplier.TotalDue,
UnpaidInvoicesCount = unpaidInvoices.Count,
...
TotalOutstandingAmount = totalOutstanding,
TotalOverdueAmount = overdueInvoices.Sum(s => s.TotalDue),
```

Formula in Plain Math:

- `supplierDebtIncluded = supplier.TotalDue > 0`
- `totalOutstandingAmount = sum(supplier.TotalDue)`
- `totalOverdueAmount = sum(overdue supplier debt totals)`
- `unpaidInvoicesCount = count(unpaid purchase invoices for supplier)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `Supplier.TotalDue` | Supplier table | Tenant + branch + active | Runtime source appears stale |
| `unpaidInvoices` | Purchase invoice query | Same supplier | Used for counts and last-payment metadata |
| `overdueInvoices` | Derived supplier debt list | Same report | Used for overdue totals |

Potential Issues:
- [ ] Tenant filter? yes - supplier debt query is tenant-scoped.
- [ ] Branch filter? yes - supplier debt query is branch-scoped.
- [ ] Cancelled excluded? yes - unpaid invoice lookup filters to open unpaid invoices.
- [ ] Rounding safe? yes - direct stored balances, no extra division.
- [ ] Discount before/after tax? no - purchase debt report uses stored invoice balances only.
- [ ] Refund handled? no - supplier debt relies on aggregate `Supplier.TotalDue`, not recomputed invoice netting.
- [ ] Negative possible? no - source query requires `TotalDue > 0`.
- [ ] FE vs BE mismatch? no - frontend renders backend values directly.

Risk Assessment:
- Level: High
- Layer: Backend
- Scenario: Report can show zero supplier debt even when invoices remain unpaid because runtime services inspected do not update `Supplier.TotalDue`.

### Supplier Performance Report

Location:
- `backend/KasserPro.Infrastructure/Services/SupplierReportService.cs`
- `frontend/src/pages/reports/SupplierPerformanceReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Measure supplier invoice volume, purchase value, average invoice value, unique products, payment timeliness, and UI summary cards.

Code:
```cs
var totalInvoices = g.Count();
var totalValue = g.Sum(pi => pi.Total);
var avgInvoiceValue = totalInvoices > 0 ? Math.Round(totalValue / totalInvoices, 2) : 0;
var uniqueProducts = g.SelectMany(pi => pi.Items).Select(i => i.ProductId).Distinct().Count();
var paidInvoices = g.Where(pi => pi.AmountDue <= 0).Count();
var onTimeRate = totalInvoices > 0 ? Math.Round((decimal)paidInvoices / totalInvoices * 100, 2) : 0;
...
avgPaymentDelay = (int)paidInvoicesList.Average(pi =>
{
    var lastPay = pi.Payments.Max(p => p.CreatedAt);
    return (lastPay - pi.InvoiceDate).TotalDays;
});
```

```ts
{report?.supplierPerformance?.reduce(
  (s, p) => s + p.totalInvoices,
  0,
) || 0}
...
{formatCurrency(
  report?.supplierPerformance?.reduce(
    (s, p) => s + p.totalPurchaseValue,
    0,
  ) || 0,
)}
```

Formula in Plain Math:

- `totalInvoices = count(supplier invoices)`
- `totalPurchaseValue = sum(invoice totals)`
- `averageInvoiceValue = totalPurchaseValue / totalInvoices`
- `uniqueProductsSupplied = count(distinct product IDs)`
- `onTimePaymentRate = paidInvoices / totalInvoices * 100`
- `daysAveragePaymentDelay = average(lastPaymentDate - invoiceDate)`
- UI card totals:
  - `invoiceCountCard = sum(row.totalInvoices)`
  - `purchaseValueCard = sum(row.totalPurchaseValue)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `g` | Supplier-grouped purchase invoices | Tenant + branch + date range | Basis for all supplier KPIs |
| `Payments` | Purchase invoice payments | Same invoices | Used for payment delay |
| `supplierPerformance` | API response rows | UI only | Re-summed for header cards |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - report is branch-scoped.
- [ ] Cancelled excluded? yes - performance is based on invoices in active reporting scope.
- [ ] Rounding safe? yes - average invoice value and on-time rate are rounded.
- [ ] Discount before/after tax? no - supplier performance uses invoice totals only.
- [ ] Refund handled? no - supplier returns are not reflected in the shown formula.
- [ ] Negative possible? no - invoice totals and counts are expected non-negative.
- [ ] FE vs BE mismatch? no - frontend cards simply re-sum backend rows.

Risk Assessment:
- Level: Medium
- Layer: Backend + Frontend
- Scenario: A supplier with one large returned purchase still looks strong because the report measures invoice totals without explicit return netting.

### Cashier Performance Report

Location:
- `backend/KasserPro.Infrastructure/Services/EmployeeReportService.cs`
- `frontend/src/pages/reports/CashierPerformanceReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Measure cashier sales, orders, average order value, payment-method mix, and a composite performance score.

Code:
```cs
var totalRevenue = salesOrders.Sum(o => o.Total) - Math.Abs(returnOrdersCashier.Sum(o => o.Total));
var totalOrders = salesOrders.Count;
var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
...
var cashSales = Math.Max(0,
    salesPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
    - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)));
...
var performanceScore = Math.Min(100, Math.Max(0,
    (ordersPerHour > 0 ? 30 : 0) +
    (avgOrderValue > 0 ? 25 : 0) +
    (cancellationRate < 5 ? 25 : cancellationRate < 10 ? 15 : 5) +
    (completedShifts > 0 ? 20 : 0)));
```

Formula in Plain Math:

- `totalRevenue = sales totals - return totals`
- `avgOrderValue = totalRevenue / completed sales order count`
- `cashSales = max(0, cash payments - refunded cash payments)`
- `performanceScore = min(100, max(0, score components sum))`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `salesOrders` | Orders by cashier | Tenant + branch + date range | Non-return orders |
| `returnOrdersCashier` | Return orders by cashier | Same scope | Refund basis |
| `salesPayments`, `returnPayments` | Payment rows | Same scope | Payment breakdown |
| `cancellationRate`, `ordersPerHour`, `completedShifts` | Derived operational KPIs | Same cashier scope | Score components |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - report is branch-scoped.
- [ ] Cancelled excluded? yes - revenue/order totals use completed order sets and cancellation rate separately.
- [ ] Rounding safe? no - average order value and score inputs are not all explicitly rounded before final display.
- [ ] Discount before/after tax? no - revenue uses final order totals.
- [ ] Refund handled? yes - return orders and refund payments are netted.
- [ ] Negative possible? yes - revenue can be negative after returns.
- [ ] FE vs BE mismatch? no - frontend displays backend KPIs directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: A cashier with a few high-value returns can show negative revenue but still keep a decent performance score because the score weights are mostly binary thresholds.

### Shift Details Report

Location:
- `backend/KasserPro.Infrastructure/Services/EmployeeReportService.cs`
- `frontend/src/pages/reports/ShiftDetailsReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Summarize shift-level payment collections, closing balance, and variance by employee shift.

Code:
```cs
var shiftCash = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount), 2);
var shiftCard = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount), 2);
var shiftFawry = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount), 2);
var shiftBankTransfer = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount), 2);
...
TotalSales = shiftCash + shiftCard + shiftFawry + shiftBankTransfer,
AverageShiftRevenue = shifts.Count > 0 ? Math.Round(totalRevenue / shifts.Count, 2) : 0,
```

Formula in Plain Math:

- `shiftCash = round2(sum(cash payments in shift))`
- `shiftCard = round2(sum(card payments in shift))`
- `shiftFawry = round2(sum(fawry payments in shift))`
- `shiftBankTransfer = round2(sum(bank transfer payments in shift))`
- `shiftTotalSales = shiftCash + shiftCard + shiftFawry + shiftBankTransfer`
- `averageShiftRevenue = totalRevenue / shiftCount`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `shiftPayments` | Payments under shift orders | Tenant + branch + date range | Payment-side shift totals |
| `shifts` | Shift dataset | Same filters | Used for average revenue |
| `variance` | Shift entity | Same shift | Closing minus expected balance |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - report is branch-scoped.
- [ ] Cancelled excluded? yes - payment rows come from completed/refunded order set.
- [ ] Rounding safe? yes - payment buckets and average shift revenue are rounded.
- [ ] Discount before/after tax? no - report is payment-based.
- [ ] Refund handled? no - shown snippet sums shift payments directly and may include signed payment rows as stored rather than explicit return netting.
- [ ] Negative possible? yes - shift totals can be reduced by refund-linked negative payment rows if present.
- [ ] FE vs BE mismatch? no - page renders backend results directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: If return payments are stored as positive absolute values in a different flow than expected, shift detail revenue will overstate collections.

### Sales By Employee Report

Location:
- `backend/KasserPro.Infrastructure/Services/EmployeeReportService.cs`
- `frontend/src/pages/reports/SalesByEmployeeReportPage.tsx`

Layer:
- Backend + Frontend

Purpose:
- Compare employees by revenue, order count, average order value, and contribution share.

Code:
```cs
var returnsByUser = returnOrdersEmp
    .GroupBy(o => o.UserId)
    .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(o => o.Total)));
...
var totalRevenue = orders.Sum(o => o.Total) - totalRefunds;
...
AverageOrderValue = empOrders > 0 ? Math.Round(empRevenue / empOrders, 2) : 0,
RevenuePercentage = totalRevenue > 0 ? Math.Round(empRevenue / totalRevenue * 100, 2) : 0,
```

Formula in Plain Math:

- `employeeRevenue = employee sales totals - employee refund totals`
- `averageOrderValue = employeeRevenue / employeeOrderCount`
- `revenuePercentage = employeeRevenue / reportTotalRevenue * 100`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `orders` | Employee sales orders | Tenant + branch + date range | Non-return orders |
| `returnOrdersEmp` | Employee return orders | Same scope | Used to net revenue |
| `empOrders` | Employee order count | Same employee | Denominator for average |

Potential Issues:
- [ ] Tenant filter? yes - report is tenant-scoped.
- [ ] Branch filter? yes - report is branch-scoped.
- [ ] Cancelled excluded? yes - report uses completed/refunded order sets.
- [ ] Rounding safe? yes - average order value and revenue share are rounded.
- [ ] Discount before/after tax? no - revenue uses stored order totals.
- [ ] Refund handled? yes - employee refunds are subtracted.
- [ ] Negative possible? yes - employee revenue percentage can be negative for high-refund employees.
- [ ] FE vs BE mismatch? no - frontend renders backend values directly.

Risk Assessment:
- Level: Medium
- Layer: Backend
- Scenario: One employee processing many returns can show negative contribution share, which is mathematically valid but operationally hard to interpret without context.

### Orders Page Summary

Location:
- `frontend/src/pages/orders/OrdersPage.tsx`
- `frontend/src/components/orders/OrderDetailsModal.tsx`

Layer:
- Frontend

Purpose:
- Compute local summary cards for completed orders, returned orders, displayed net sales, and displayed returns in the orders UI.

Code:
```ts
const completedOrders = filteredOrders.filter(
  (o) =>
    (o.status === "Completed" || o.status === "PartiallyRefunded") &&
    o.orderType !== "Return",
).length;
const returnedOrders = filteredOrders.filter(
  (o) => o.status === "Refunded" || o.orderType === "Return",
).length;
...
filteredOrders
  .filter(
    (o) =>
      (o.status === "Completed" ||
        o.status === "PartiallyRefunded" ||
        o.status === "Refunded") &&
      o.orderType !== "Return",
  )
  .reduce((sum, o) => {
    const netAmount = o.total - (o.refundAmount || 0);
    return sum + netAmount;
  }, 0),
...
Math.abs(
  filteredOrders
    .filter((o) => o.orderType === "Return")
    .reduce((sum, o) => sum + o.total, 0),
),
```

Formula in Plain Math:

- `completedOrdersCard = count(non-return completed or partially refunded orders)`
- `returnedOrdersCard = count(refunded orders OR return orders)`
- `displayedNetSales = sum(non-return order total - refundAmount)`
- `displayedReturns = abs(sum(return order totals))`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `filteredOrders` | Current local list after search/filter/page selection | Current UI state only | Not guaranteed full dataset |
| `o.refundAmount` | Order DTO | Same row | Original-order refund accumulator |
| `o.total` | Order DTO | Same row | Return orders hold negative totals |

Potential Issues:
- [ ] Tenant filter? no - local UI list only.
- [ ] Branch filter? no - local UI list only.
- [ ] Cancelled excluded? yes - sales card explicitly filters to completed/refunded states.
- [ ] Rounding safe? yes - sums are performed over already-rounded DTO totals.
- [ ] Discount before/after tax? no - page consumes final order totals.
- [ ] Refund handled? yes - original-order `refundAmount` and separate return orders are both surfaced.
- [ ] Negative possible? no - displayed return card takes absolute value.
- [ ] FE vs BE mismatch? yes - the page can visually double-frame refund impact by netting `refundAmount` on original orders while also showing return-order totals in a separate card.

Risk Assessment:
- Level: Medium
- Layer: Frontend
- Scenario: The manager reads both a lower net-sales card and a positive returns card and adds them mentally twice, overstating the real refund hit.

### Page-Local Totals

Location:
- `frontend/src/pages/customers/CustomersPage.tsx`
- `frontend/src/pages/expenses/ExpensesPage.tsx`
- `frontend/src/pages/purchase-invoices/PurchaseInvoicesPage.tsx`

Layer:
- Frontend

Purpose:
- Compute list-header totals from the currently loaded page only, not the full filtered dataset.

Code:
```ts
const totalDue = customers.reduce((sum, c) => sum + c.totalDue, 0);
const totalSpent = customers.reduce((sum, c) => sum + c.totalSpent, 0);
```

```ts
const totalAmount = expenses.reduce(
  (sum, expense) => sum + expense.amount,
  0,
);
```

```ts
const totalAmount = invoices.reduce((sum, inv) => sum + inv.total, 0);
const paidCount = invoices.filter((inv) => inv.status === "Paid").length;
```

Formula in Plain Math:

- Customers page:
  - `headerTotalDue = sum(current page customer due)`
  - `headerTotalSpent = sum(current page customer spent)`
- Expenses page:
  - `headerExpenseAmount = sum(current page expense amounts)`
- Purchase invoices page:
  - `headerInvoiceAmount = sum(current page invoice totals)`
  - `paidCount = count(current page invoices where status == Paid)`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `customers` | Current paged customers array | Current page only | Not all matching rows |
| `expenses` | Current paged expenses array | Current page only | Not all matching rows |
| `invoices` | Current paged purchase invoices array | Current page only | Not all matching rows |

Potential Issues:
- [ ] Tenant filter? no - local UI totals over already-fetched page data.
- [ ] Branch filter? no - local UI totals over already-fetched page data.
- [ ] Cancelled excluded? no - depends entirely on what the page query returned.
- [ ] Rounding safe? yes - sums use already-shaped DTO amounts.
- [ ] Discount before/after tax? no - these pages consume final stored values.
- [ ] Refund handled? no - no explicit refund normalization is done locally.
- [ ] Negative possible? yes - any negative row value flows directly into the page header.
- [ ] FE vs BE mismatch? yes - page headers read like totals but only cover the current paginated slice.

Risk Assessment:
- Level: Medium
- Layer: Frontend
- Scenario: Finance staff exports page 1 mentally by reading the header card as a report total, while rows on later pages are excluded from the displayed aggregate.

## Duplicate Calculation Detected

⚠️ Duplicate Calculation Detected

Frontend:
- `frontend/src/utils/cartPricing.ts`
- `frontend/src/store/slices/cartSlice.ts`

Backend:
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs`

Differences:
- Item and order pricing logic intentionally mirrors backend, but backend also applies `ServiceChargeAmount`, which the frontend cart does not model.

Risk:
- Checkout preview can differ from persisted order total when service charge is non-zero.

⚠️ Duplicate Calculation Detected

Frontend:
- `frontend/src/components/orders/RefundModal.tsx`

Backend:
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs`

Differences:
- Frontend preview uses `item.total / item.quantity` and multiplies by refund quantity.
- Backend creates proportional negative subtotal, tax, and discount rows with line rounding.

Risk:
- Partial refund previews can drift by cents from the posted return order and printed refund receipt.

⚠️ Duplicate Calculation Detected

Frontend:
- `frontend/src/components/pos/PaymentModal.tsx`
- `frontend/src/pages/pos/POSWorkspacePage.tsx`

Backend:
- `backend/KasserPro.Application/Services/Implementations/CustomerService.cs`
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs`

Differences:
- UI calculates `availableCredit`, `amountDue`, and credit-limit eligibility locally.
- Backend revalidates credit using persisted customer balance and authoritative order total.

Risk:
- Boundary cases can look valid in UI and still be rejected by backend after authoritative recalculation.

⚠️ Duplicate Calculation Detected

Frontend:
- `frontend/src/pages/shifts/ShiftPage.tsx`

Backend:
- `backend/KasserPro.Application/Services/Implementations/ShiftService.cs`
- `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs`

Differences:
- Shift page computes displayed sales as `totalCash + totalCard`.
- Shift service calculates four buckets (`Cash`, `Card`, `Fawry`, `BankTransfer`).
- Cash-register reconciliation uses register balance movement since shift open, not only `openingBalance + totalCash`.

Risk:
- Shift close, shift dashboard, and cash reconciliation can each show a different "expected" number for the same shift.

⚠️ Duplicate Calculation Detected

Frontend:
- `frontend/src/pages/purchase-invoices/PurchaseInvoiceFormPage.tsx`

Backend:
- `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`

Differences:
- Frontend rounds subtotal and tax at every preview step.
- Backend sums raw decimal line totals and calculates tax without matching staged rounding.

Risk:
- Purchase invoice preview can differ from persisted totals when prices have more than 2 decimals.

⚠️ Duplicate Calculation Detected

Frontend:
- `frontend/src/utils/browserReceiptPrinter.ts`

Backend:
- `backend/KasserPro.API/Controllers/OrdersController.cs`

Differences:
- Backend provides explicit discount fields.
- Browser fallback reconstructs discount from subtotal, total, and tax.

Risk:
- Receipt discount can be wrong when service charge or any future adjustment affects the total delta.

## Special Cases

### Installments

Location:
- Code search: no installment schedule, installment ledger, or installment payment service found.

Layer:
- Cross-system

Purpose:
- Scheduled split settlement of a sale across multiple future payments.

Code:
`Status: NOT IMPLEMENTED`

Formula in Plain Math:

- N/A

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| N/A | N/A | N/A | No implementation found |

Potential Issues:
- [ ] Tenant filter? no - feature does not exist.
- [ ] Branch filter? no - feature does not exist.
- [ ] Cancelled excluded? no - feature does not exist.
- [ ] Rounding safe? no - feature does not exist.
- [ ] Discount before/after tax? no - feature does not exist.
- [ ] Refund handled? no - feature does not exist.
- [ ] Negative possible? no - feature does not exist.
- [ ] FE vs BE mismatch? no - feature does not exist.

Risk Assessment:
- Level: Medium
- Layer: Product
- Scenario: Teams may simulate installments using multiple manual debt payments, but there is no contract-level schedule, aging, or default tracking.

### Currency Exchange

Location:
- Code search: no multi-currency pricing, exchange-rate table, or FX settlement path found.

Layer:
- Cross-system

Purpose:
- Convert between transaction currency and base ledger currency.

Code:
`Status: NOT IMPLEMENTED`

Formula in Plain Math:

- N/A

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| N/A | N/A | N/A | No implementation found |

Potential Issues:
- [ ] Tenant filter? no - feature does not exist.
- [ ] Branch filter? no - feature does not exist.
- [ ] Cancelled excluded? no - feature does not exist.
- [ ] Rounding safe? no - feature does not exist.
- [ ] Discount before/after tax? no - feature does not exist.
- [ ] Refund handled? no - feature does not exist.
- [ ] Negative possible? no - feature does not exist.
- [ ] FE vs BE mismatch? no - feature does not exist.

Risk Assessment:
- Level: Medium
- Layer: Product
- Scenario: Any foreign-currency sale or purchase would have to be entered as if it were EGP, hiding FX gain/loss and true settlement value.

### Withholding Tax

Location:
- Code search: no withholding-tax rate, deduction logic, or payable/receivable withholding ledger found.

Layer:
- Cross-system

Purpose:
- Deduct tax at source from supplier/customer settlements.

Code:
`Status: NOT IMPLEMENTED`

Formula in Plain Math:

- N/A

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| N/A | N/A | N/A | No implementation found |

Potential Issues:
- [ ] Tenant filter? no - feature does not exist.
- [ ] Branch filter? no - feature does not exist.
- [ ] Cancelled excluded? no - feature does not exist.
- [ ] Rounding safe? no - feature does not exist.
- [ ] Discount before/after tax? no - feature does not exist.
- [ ] Refund handled? no - feature does not exist.
- [ ] Negative possible? no - feature does not exist.
- [ ] FE vs BE mismatch? no - feature does not exist.

Risk Assessment:
- Level: Medium
- Layer: Compliance
- Scenario: Statutory withholding obligations would be processed offline, leaving the system's payable and tax positions incomplete.

### Year Closing

Location:
- Code search: no fiscal close workflow, retained-earnings roll-forward, or period lock logic found.

Layer:
- Cross-system

Purpose:
- Close a fiscal year and lock balances for subsequent periods.

Code:
`Status: NOT IMPLEMENTED`

Formula in Plain Math:

- N/A

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| N/A | N/A | N/A | No implementation found |

Potential Issues:
- [ ] Tenant filter? no - feature does not exist.
- [ ] Branch filter? no - feature does not exist.
- [ ] Cancelled excluded? no - feature does not exist.
- [ ] Rounding safe? no - feature does not exist.
- [ ] Discount before/after tax? no - feature does not exist.
- [ ] Refund handled? no - feature does not exist.
- [ ] Negative possible? no - feature does not exist.
- [ ] FE vs BE mismatch? no - feature does not exist.

Risk Assessment:
- Level: High
- Layer: Governance
- Scenario: Historical periods remain mutable, so late operational edits can silently rewrite prior-year management reporting.

### FIFO / LIFO Inventory

Location:
- `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`

Layer:
- Backend

Purpose:
- Cost inventory using recognized cost-flow assumptions.

Code:
```cs
var oldStock = balanceBefore;
var oldAvgCost = product.AverageCost ?? product.Cost ?? 0m;
var newStock = balanceBefore + item.Quantity;
if (newStock > 0)
{
    var totalOldValue = oldStock * oldAvgCost;
    var totalNewValue = item.Quantity * item.PurchasePrice;
    product.AverageCost = (totalOldValue + totalNewValue) / newStock;
}
```

Formula in Plain Math:

- Current implemented method: weighted average cost
- Requested special case status:
  - FIFO: `Status: NOT IMPLEMENTED`
  - LIFO: `Status: NOT IMPLEMENTED`

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| `product.AverageCost` | Product entity | Tenant + branch inventory receipt context | Weighted average only |
| `item.PurchasePrice` | Purchase invoice line | Same context | Used in average-cost recomputation |

Potential Issues:
- [ ] Tenant filter? yes - current weighted-average implementation is tenant-scoped through purchase flow.
- [ ] Branch filter? yes - stock receipt is branch-scoped.
- [ ] Cancelled excluded? yes - costing update runs on invoice confirmation, not cancellation.
- [ ] Rounding safe? no - average cost division is not rounded in the shown method.
- [ ] Discount before/after tax? no - purchase cost uses raw purchase price.
- [ ] Refund handled? no - FIFO/LIFO layers do not exist to reverse.
- [ ] Negative possible? no - weighted average runs only when `newStock > 0`.
- [ ] FE vs BE mismatch? no - no frontend counterpart exists.

Risk Assessment:
- Level: Medium
- Layer: Inventory Accounting
- Scenario: Businesses requiring FIFO/LIFO valuation for audit or tax purposes cannot derive those numbers from the current weighted-average stock model.

### Bank Reconciliation

Location:
- Code search: cash register reconciliation exists, but no bank statement import or bank-ledger reconciliation workflow was found.

Layer:
- Cross-system

Purpose:
- Match recorded card/bank transactions against external bank statements.

Code:
`Status: NOT IMPLEMENTED`

Formula in Plain Math:

- N/A

Variable Sources:
| Variable | Source | Filters | Notes |
| --- | --- | --- | --- |
| N/A | N/A | N/A | No implementation found |

Potential Issues:
- [ ] Tenant filter? no - feature does not exist.
- [ ] Branch filter? no - feature does not exist.
- [ ] Cancelled excluded? no - feature does not exist.
- [ ] Rounding safe? no - feature does not exist.
- [ ] Discount before/after tax? no - feature does not exist.
- [ ] Refund handled? no - feature does not exist.
- [ ] Negative possible? no - feature does not exist.
- [ ] FE vs BE mismatch? no - feature does not exist.

Risk Assessment:
- Level: High
- Layer: Treasury
- Scenario: Electronic sales can be reported internally but never reconciled to bank settlement files, hiding gateway fees, delays, and missing deposits.

## Phase 2 - Unified Financial Diagram

```mermaid
flowchart TD
    subgraph FE[Frontend]
        FE1[Cart Pricing\ncartPricing.ts]
        FE2[Cart Totals\ncartSlice.ts]
        FE3[Payment UI\nPaymentModal / POSWorkspacePage]
        FE4[Refund Preview\nRefundModal]
        FE5[Purchase Preview\nPurchaseInvoiceFormPage]
        FE6[Reports UI\nPages + local reduce()]
    end

    subgraph API[API / Controllers]
        API1[OrdersController]
        API2[CustomersController]
        API3[ReportsController]
    end

    subgraph BE[Backend Services]
        BE1[OrderService\nCalculateItemTotals / CalculateOrderTotals]
        BE2[CustomerService\nTotalDue / Credit Validation]
        BE3[PurchaseInvoiceService\nTotals / Payments / Average Cost]
        BE4[ExpenseService]
        BE5[CashRegisterService]
        BE6[ShiftService]
        BE7[ReportService]
        BE8[FinancialReportService]
        BE9[Inventory / Customer / Supplier / Employee Report Services]
    end

    subgraph DB[Persistent State]
        DB1[(Orders)]
        DB2[(OrderItems)]
        DB3[(Payments)]
        DB4[(Customers.TotalDue)]
        DB5[(PurchaseInvoices)]
        DB6[(PurchaseInvoicePayments)]
        DB7[(Products.AverageCost)]
        DB8[(BranchInventory.Quantity)]
        DB9[(CashRegisterTransactions)]
        DB10[(Shifts)]
        DB11[(Expenses)]
        DB12[(Suppliers.TotalDue)]
    end

    FE1 --> FE2 --> FE3
    FE2 --> API1
    FE3 --> API1
    FE4 --> API1
    FE5 --> BE3
    FE6 --> API2
    FE6 --> API3

    API1 --> BE1
    API1 --> BE2
    API2 --> BE2
    API3 --> BE7
    API3 --> BE8
    API3 --> BE9

    BE1 --> DB1
    BE1 --> DB2
    BE1 --> DB3
    BE1 --> BE2
    BE1 --> BE5
    BE2 --> DB4
    BE3 --> DB5
    BE3 --> DB6
    BE3 --> DB7
    BE3 --> DB8
    BE4 --> DB11
    BE4 --> BE5
    BE5 --> DB9
    BE6 --> DB10
    BE7 --> DB10
    BE7 --> DB1
    BE8 --> DB1
    BE8 --> DB11
    BE9 --> DB1
    BE9 --> DB5
    BE9 --> DB8
    BE9 --> DB12
```

### Reports Map

| Report | Inputs | Formula | Output |
| --- | --- | --- | --- |
| Daily Sales Report | Closed shifts, completed orders, return orders, payments | Sales subtotals/tax/discounts net of return orders; payment buckets net of refund payments | `TotalSales`, `NetSales`, `TotalRefunds`, payment mix, shift summaries |
| Sales Report | Completed sales orders, return orders, order-item cost snapshots | `NetSales = SalesTotals - ReturnTotals`; `GrossProfit = NetSales - NetCost` | `TotalSales`, `TotalCost`, `GrossProfit`, `AverageOrderValue` |
| Profit & Loss | Sales orders, return orders, expenses | `NetProfit = (NetSales - NetCost) - Expenses` | `NetProfit`, `NetProfitMargin`, category expense mix |
| Expenses Report | Expense rows | Sum by amount, count, payment method, category percentage | `TotalExpenses`, averages, category bars |
| Branch Inventory Report | `BranchInventory`, `Products.AverageCost` | `Value = Quantity * AverageCost` | Branch quantity/value/low-stock counts |
| Unified Inventory Report | All branch inventories by product | `TotalValue = SumQty * AverageCost`; page re-sums DTO rows | Cross-branch quantity/value and low-stock products |
| Customer Debts Report | `Customers.TotalDue`, `CreditLimit` | Sum debt, over-limit flags, bracket percentages | Outstanding debt and aging |
| Supplier Debts Report | `Suppliers.TotalDue`, unpaid invoices | Sum supplier debt and overdue balances | Supplier liabilities dashboard |
| Cash Register Summary | Cash-register transactions | Opening/closing balance plus movement buckets | Register summary and reconciliation base |
| Shift Closing | Shift opening balance, payment rows | `Expected = Opening + NetCashSales`; `Difference = Closing - Expected` | Shift expected balance and variance |
| Tax Views | Order/item tax amounts in orders and reports | Sum persisted tax values after discount allocation | `TotalTax` on orders, daily report, and P&L |

### How to Read

1. Frontend cart math is only a preview. The authoritative sale total is recalculated in `OrderService`.
2. Once an order is completed, money fans out into three state buckets:
   - `Orders / Payments`
   - `Customers.TotalDue` for unpaid balances
   - `CashRegisterTransactions` for cash-only movements
3. Reports do not all read the same base:
   - Sales and P&L read orders
   - Expense reports read expenses
   - Inventory reports read stock state plus historical movement
   - Supplier debt report reads `Supplier.TotalDue`, which is materially riskier than invoice-level recomputation
4. Any number marked below as `🔴 High Risk` should be treated as management-report sensitive until corrected.

### Gaps and Recommendations

- 🔴 Align one authoritative "collected amount" definition across `ShiftService`, `CashRegisterService`, and daily-report payment summaries.
- 🔴 Replace all supplier-debt reporting that depends on `Supplier.TotalDue` with invoice-level recomputation, or maintain the aggregate consistently in runtime services.
- 🔴 Fix P&L refund basis so pre-tax and tax-inclusive numbers are not mixed in the same subtraction.
- 🟡 Decide whether customer debt must be branch-isolated; current implementation is tenant-wide.
- 🟡 Promote page-local UI totals to explicit labels like "current page total" or fetch a true aggregate from backend.
- 🟡 Remove receipt fallback discount reconstruction and prefer explicit backend print DTO fields only.
- 🟢 Cart/item pricing parity between frontend and backend is generally strong aside from service charge omission.

## Phase 3 - Critical Financial Risks

## 🚨 Critical Financial Risks

### 1. Supplier Debt Aggregate Is Not Reliably Maintained
- Problem:
  Supplier debt reports depend on `Supplier.TotalDue`, but inspected runtime purchase-invoice services update invoice balances without updating supplier aggregates.
- Impact:
  Supplier liabilities can be materially understated or stale in dashboards and debt reports.
- Example Scenario:
  A supplier invoice is confirmed and partially paid; the invoice shows an outstanding balance, but the supplier debt report still shows zero because `Supplier.TotalDue` was never incremented.
- Recommendation:
  Recompute supplier debt from open purchase invoices or add audited aggregate-maintenance logic in every invoice/payment/cancellation path.

### 2. Cash Register Transfer-Out Summary Is Broken
- Problem:
  `CashRegisterService.GetSummaryAsync()` calculates transfer-out totals only from negative transfer amounts, while transfer rows are stored with positive `Amount`.
- Impact:
  Register summaries can understate outbound cash movement between branches.
- Example Scenario:
  Branch A transfers 5,000 to Branch B; the summary shows transfer-in correctly somewhere else but transfer-out remains zero in Branch A's summary.
- Recommendation:
  Mark transfer direction explicitly in the transaction model or compute transfer-out from reference/context rather than amount sign.

### 3. Shift Expected Cash Uses a Different Basis Than Cash Reconciliation
- Problem:
  `ShiftService` uses `OpeningBalance + NetCashSales`, while `CashRegisterService.ReconcileAsync()` uses register transactions since shift open.
- Impact:
  Two official workflows can produce different "expected balance" numbers for the same shift.
- Example Scenario:
  A shift has a cash expense or deposit during the day; shift close expects one number, reconciliation expects another.
- Recommendation:
  Adopt one canonical expected-balance formula and reuse it in shift close, dashboards, and reconciliation.

### 4. Profit and Loss Report Mixes Pre-Tax and Tax-Inclusive Refund Bases
- Problem:
  `FinancialReportService` subtracts `refundsAmount` (tax-inclusive return totals) from `netSales` (pre-tax subtotal minus discounts).
- Impact:
  Net sales and gross profit are understated when tax is enabled and returns exist.
- Example Scenario:
  A taxed order is returned; the refund subtracts tax from pre-tax net sales a second time at the P&L layer.
- Recommendation:
  Subtract refund net-sales basis from `netSales`, and refund total-revenue basis from `totalRevenue`, using separate formulas.

### 5. Customer Debt Is Tenant-Wide, Not Branch-Isolated
- Problem:
  `Customer.TotalDue` is maintained at customer level without branch partitioning, while the project rules require branch isolation.
- Impact:
  Branchs can block or allow credit based on debt created elsewhere, and branch debt reports are distorted.
- Example Scenario:
  A customer buys on credit in Branch A, then attempts a sale in Branch B and is rejected due to tenant-wide debt even if Branch B should manage its own exposure.
- Recommendation:
  Introduce branch-level customer balance tracking or a dedicated customer-branch credit ledger.

### 6. Browser Receipt Discount Reconstruction Can Print the Wrong Discount
- Problem:
  Browser fallback derives discount as `subtotal - total + tax`, ignoring explicit backend discount fields and any service charge.
- Impact:
  Printed receipts can misstate the commercial terms of the sale.
- Example Scenario:
  A service charge is added in backend; fallback receipt prints the delta as discount even though no discount was granted.
- Recommendation:
  Remove derived discount reconstruction and render only backend-provided print DTO values.
