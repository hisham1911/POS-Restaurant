# 🔒 Backend Hardening Manifest - Frontend Integration Guide

## ⚠️ CRITICAL: New Reality After 31 Financial Fixes

تم تطبيق 31 إصلاح مالي على Backend. يجب على Frontend التكيف مع القواعد الجديدة.

---

## 📋 New Validation Rules & Constraints

### 1. Credit Sales (البيع الآجل) ✅

| Rule | Validation | Error Code | Frontend Action |
|------|-----------|------------|-----------------|
| **Customer Required** | `order.CustomerId != null` | `PAYMENT_INSUFFICIENT` | Show: "البيع الآجل يتطلب ربط عميل" |
| **Customer Active** | `customer.IsActive == true` | `CUSTOMER_INACTIVE` | Disable credit option |
| **Credit Limit Check** | `customer.TotalDue + amountDue <= customer.CreditLimit` | `CUSTOMER_CREDIT_LIMIT_EXCEEDED` | Show remaining credit |
| **Zero Limit = Unlimited** | `customer.CreditLimit == 0` means NO limit | N/A | Allow any amount |

**Backend Logic:**
```csharp
// In OrderService.CompleteAsync
if (totalPaymentAmount < order.Total)
{
    if (!order.CustomerId.HasValue)
        return Fail("البيع الآجل يتطلب ربط عميل بالطلب");
    
    var amountDue = order.Total - totalPaymentAmount;
    var canTakeCredit = await ValidateCreditLimitAsync(customerId, amountDue);
    
    if (!canTakeCredit)
        return Fail($"تجاوز حد الائتمان. الحد: {CreditLimit}, الرصيد: {TotalDue}");
}
```

**Frontend Must:**
- ✅ Fetch customer data before allowing credit
- ✅ Calculate: `availableCredit = customer.creditLimit - customer.totalDue`
- ✅ Show warning if `orderTotal > availableCredit`
- ✅ Disable "Pay Later" button if limit exceeded

---

### 2. Rounding & Precision 🔢

| Aspect | Rule | Example |
|--------|------|---------|
| **Rounding Mode** | `MidpointRounding.AwayFromZero` | 2.5 → 3, 2.4 → 2 |
| **Decimal Places** | Always 2 decimal places | 10.123 → 10.12 |
| **Tax Calculation** | `TaxAmount = Round(NetTotal * TaxRate / 100, 2)` | 100 * 14% = 14.00 |
| **Total Calculation** | `Total = Round(Subtotal + TaxAmount, 2)` | Never use floating point |

**Backend Implementation:**
```csharp
public static decimal RoundCurrency(decimal value)
{
    return Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

// Tax calculation
item.TaxAmount = RoundCurrency(item.NetTotal * item.TaxRate / 100);
item.TotalAmount = RoundCurrency(item.NetTotal + item.TaxAmount);
```

**Frontend Must:**
- ✅ Use same rounding: `Math.round(value * 100) / 100`
- ✅ Display all amounts with 2 decimals: `.toFixed(2)`
- ✅ Never use `toFixed()` for calculations (only display)

---

### 3. Concurrency Tokens (RowVersion) 🔐

| Entity | Has RowVersion | Purpose |
|--------|---------------|---------|
| Order | ✅ | Prevent double-complete, double-refund |
| Customer | ✅ | Prevent lost updates on TotalDue |
| Shift | ✅ | Prevent concurrent close |

**Backend Behavior:**
```csharp
// Every SaveChangesAsync updates RowVersion
entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();

// On conflict:
catch (DbUpdateConcurrencyException)
{
    return Fail("تم تعديل السجل بواسطة عملية أخرى. يرجى تحديث الصفحة");
}
```

**Frontend Must:**
- ✅ Capture `rowVersion` from GET responses
- ✅ Send `rowVersion` back in PUT/POST requests
- ✅ Handle 409 Conflict: Show "تم التعديل من مستخدم آخر - يرجى التحديث"
- ✅ Refresh data after conflict

**TypeScript Example:**
```typescript
interface Order {
  id: number;
  orderNumber: string;
  total: number;
  rowVersion: string; // Base64 encoded byte[]
  // ... other fields
}

// When updating:
const updateOrder = async (order: Order) => {
  const response = await api.put(`/orders/${order.id}`, {
    ...order,
    rowVersion: order.rowVersion // MUST include
  });
  
  if (response.status === 409) {
    alert('تم تعديل الطلب من مستخدم آخر. يرجى تحديث الصفحة');
    // Refresh order data
  }
};
```

---

### 4. Discount Clamping & Validation 💰

| Rule | Validation | Auto-Fix |
|------|-----------|----------|
| **Percentage Discount** | `0 <= value <= 100` | Clamp to [0, 100] |
| **Fixed Discount** | `value >= 0` | Clamp to 0 minimum |
| **Discount > Total** | `discountAmount <= subtotal` | Reject with error |
| **Negative Values** | Never allowed | Auto-clamp to 0 |

**Backend Logic:**
```csharp
// Percentage discount clamping
if (discountType == "percentage")
{
    discountValue = Math.Clamp(discountValue, 0, 100);
}

// Fixed discount clamping
if (discountType == "fixed")
{
    discountValue = Math.Max(0, discountValue);
}

// Validate discount doesn't exceed subtotal
if (discountAmount > subtotal)
{
    return Fail("قيمة الخصم لا يمكن أن تتجاوز إجمالي الطلب");
}
```

**Frontend Must:**
- ✅ Validate discount input before sending
- ✅ Show warning if discount > 100%
- ✅ Prevent negative discount values
- ✅ Calculate discount amount and show preview

---

### 5. Stock Validation (AllowNegativeStock) 📦

| Tenant Setting | Behavior |
|---------------|----------|
| `AllowNegativeStock = false` | **STRICT**: Reject if stock < quantity |
| `AllowNegativeStock = true` | **PERMISSIVE**: Allow negative stock |

**Backend Checks:**
```csharp
// Soft check (UX hint) - in CreateAsync
if (currentStock < quantity && !tenant.AllowNegativeStock)
{
    return Fail($"المخزون غير كافٍ. المتاح: {currentStock}, المطلوب: {quantity}");
}

// Hard check (authoritative) - in CompleteAsync
// Inside transaction, after write lock acquired
if (branchStock < item.Quantity && !tenant.AllowNegativeStock)
{
    await transaction.RollbackAsync();
    return Fail("المخزون تغير أثناء إتمام الطلب");
}
```

**Frontend Must:**
- ✅ Fetch tenant settings on app load
- ✅ Show stock availability before adding to cart
- ✅ Disable "Add" button if stock insufficient
- ✅ Handle "المخزون تغير" error gracefully

---

### 6. Payment Validation 💳

| Rule | Validation | Error Code |
|------|-----------|------------|
| **Minimum Payment** | `totalPayment >= order.Total` OR customer linked | `PAYMENT_INSUFFICIENT` |
| **Overpayment Limit** | `totalPayment <= order.Total * 2` | `PAYMENT_OVERPAYMENT_LIMIT` |
| **Positive Amounts** | `payment.Amount > 0` | Auto-skip |
| **Change Calculation** | `change = totalPaid - order.Total` | Auto-calculated |

**Backend Logic:**
```csharp
// Overpayment protection (anti-money laundering)
decimal maxAllowedPayment = order.Total * 2;
if (totalPaymentAmount > maxAllowedPayment)
{
    return Fail($"المبلغ المدفوع ({totalPaymentAmount}) يتجاوز الحد المسموح ({maxAllowedPayment})");
}

// Change calculation
order.ChangeAmount = totalPaid > order.Total 
    ? Round(totalPaid - order.Total, 2) 
    : 0;
```

**Frontend Must:**
- ✅ Validate payment amount before submit
- ✅ Show warning if payment > 2x total
- ✅ Calculate and display change amount
- ✅ Disable submit if payment < total (unless customer linked)

---

### 7. Order Status Transitions 🔄

| From Status | To Status | Allowed? | Condition |
|------------|-----------|----------|-----------|
| Draft | Completed | ✅ | Always |
| Draft | Cancelled | ✅ | Always |
| Completed | Refunded | ✅ | Full refund only |
| Completed | Completed | ❌ | Idempotency violation |
| Refunded | Any | ❌ | Final state |
| Cancelled | Any | ❌ | Final state |

**Backend Validation:**
```csharp
private (bool Success, string? Message) ValidateStateTransition(
    OrderStatus from, OrderStatus to)
{
    if (from == to)
        return (false, "الطلب في هذه الحالة بالفعل");
    
    if (from == OrderStatus.Completed && to != OrderStatus.Refunded)
        return (false, "لا يمكن تعديل طلب مكتمل");
    
    if (from == OrderStatus.Refunded || from == OrderStatus.Cancelled)
        return (false, "لا يمكن تعديل طلب ملغي أو مسترجع");
    
    return (true, null);
}
```

**Frontend Must:**
- ✅ Disable actions based on current status
- ✅ Show appropriate buttons per status
- ✅ Handle state transition errors gracefully

---

## 🚨 Common Error Codes

| Error Code | Arabic Message | Frontend Action |
|-----------|---------------|-----------------|
| `PAYMENT_INSUFFICIENT` | "المبلغ المدفوع أقل من الإجمالي" | Show credit option or request full payment |
| `CUSTOMER_CREDIT_LIMIT_EXCEEDED` | "تجاوز حد الائتمان" | Show remaining credit, disable credit option |
| `INSUFFICIENT_STOCK` | "المخزون غير كافٍ" | Show available quantity, reduce cart quantity |
| `ORDER_INVALID_STATE_TRANSITION` | "لا يمكن تعديل الطلب في هذه الحالة" | Refresh order, disable action |
| `PAYMENT_OVERPAYMENT_LIMIT` | "المبلغ المدفوع يتجاوز الحد المسموح" | Show warning, limit input |
| `PRODUCT_INACTIVE` | "المنتج غير متاح للبيع" | Remove from cart, show message |
| `NO_OPEN_SHIFT` | "يجب فتح وردية قبل إنشاء طلب" | Redirect to shift management |

---

## 📊 Frontend Checklist

### Before Submitting Order
- [ ] Validate all item quantities > 0
- [ ] Check stock availability (if AllowNegativeStock = false)
- [ ] Validate discount values (0-100% or >= 0 fixed)
- [ ] Calculate totals with proper rounding
- [ ] If credit sale: validate customer and credit limit
- [ ] Include RowVersion in request

### After Receiving Response
- [ ] Handle 400 errors with specific error codes
- [ ] Handle 409 conflicts (concurrency)
- [ ] Update RowVersion from response
- [ ] Show appropriate user messages
- [ ] Refresh data if needed

### Display Requirements
- [ ] Show all amounts with 2 decimals (.toFixed(2))
- [ ] Display customer credit info (limit, used, available)
- [ ] Show stock availability per product
- [ ] Indicate order status clearly
- [ ] Show change amount if overpaid

---

## 🧪 Testing Scenarios

See `backend/test-api.ps1` for automated API tests covering:
1. Valid credit sale
2. Over-limit credit sale
3. Invalid refund (over quantity)
4. Concurrency conflict
5. Stock validation
6. Discount clamping
7. Payment validation

---

**Last Updated**: 2026-03-11  
**Backend Version**: Post-Nuclear-Reset (31 fixes applied)  
**Status**: ✅ Ready for Frontend Integration
