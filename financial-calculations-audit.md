# Financial Calculations Audit

Reviewed first:
- `.kiro/steering/architecture.md`
- `.kiro/skills/kasserpro-bestpractices/SKILL.md`

Scope notes:
- Audited financial calculations in `backend/KasserPro.Application/Services/Implementations/`, `backend/KasserPro.API/Controllers/`, and client-side financial calculations in `frontend/src/`.
- Controllers were reviewed for direct receipt/report math. Most other controller methods delegate to services.

### POS Item Discount Calculation
**Location:** `frontend/src/store/slices/cartSlice.ts:262-278`; `frontend/src/hooks/useOrders.ts:57-67`; `backend/KasserPro.Application/Services/Implementations/OrderService.cs:163-179` — Layer: Both  
**Purpose:** حساب خصم الصنف داخل السلة ثم إرسال نفس الخصم للسيرفر عند إنشاء الطلب.

**Code (exact, copy-paste من الكود الفعلي):**
```typescript
const calcItemDiscount = (item: CartItem): number => {
  if (!item.discount) return 0;
  const lineTotal = item.product.price * item.quantity;
  if (item.discount.type === "percentage") {
    return Math.min(lineTotal * (item.discount.value / 100), lineTotal);
  }
  return Math.min(item.discount.value, lineTotal);
};

export const selectItemDiscountsTotal = (state: { cart: CartState }) =>
  Math.round(
    state.cart.items.reduce((sum, item) => sum + calcItemDiscount(item), 0) *
      100,
  ) / 100;
```
```typescript
const orderItems = items.map((item) => ({
  productId: item.product.id,
  quantity: item.quantity,
  notes: item.notes,
  ...(item.discount
    ? {
        discountType: item.discount.type,
        discountValue: item.discount.value,
        discountReason: item.discount.reason,
      }
    : {}),
}));
```
```csharp
var orderItem = new OrderItem
{
    ProductId = product.Id,
    // Product Snapshot (on OrderItem entity)
    ProductName = product.Name,
    ProductNameEn = product.NameEn,
    ProductSku = product.Sku,
    ProductBarcode = product.Barcode,
    // Price Snapshot - UnitPrice is NET (excluding tax)
    UnitPrice = product.Price,
    UnitCost = product.Cost,
    OriginalPrice = product.Price,
    Quantity = item.Quantity,
    // Tax Snapshot - Dynamic from Product or Tenant
    TaxRate = taxRate,
    TaxInclusive = false, // Always Tax Exclusive (Additive)
    Notes = item.Notes
};
```

**Formula in Plain Math:**
```text
Frontend ItemDiscount = min(LineTotal * Percent, LineTotal) OR min(FixedDiscount, LineTotal)
Backend CreateAsync ItemDiscount = 0 in this code path because discount fields are not mapped onto OrderItem
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `item.product.price` | Redux `cart.items[]` | Current POS cart only | Frontend net unit price |
| `item.quantity` | Redux `cart.items[]` | Current POS cart only | Frontend quantity |
| `item.discount.*` | Redux `cart.items[]` | Current POS cart only | Sent from `useOrders` payload |
| `product.Price` | `Products` | `TenantId` + branch context before order creation | Backend ignores item discount fields in audited mapping |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — on the backend item creation path at `backend/KasserPro.Application/Services/Implementations/OrderService.cs:40-69`
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — not applicable at creation time
- [ ] هل الدقة العشرية محمية؟ yes/no — backend yes (`decimal`), frontend yes partially (`Math.round` to 2 decimals)
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ before tax in the frontend cart logic
- [ ] المردود: يُطرح من الإجمالي؟ no — not in this line-item preview calculation
- [ ] هل يمكن أن تكون النتيجة سالبة؟ frontend no بسبب `Math.min(...)`; backend path avoids negative here only because discount values are not mapped
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ no — item-level discount is computed and sent by the frontend but ignored by the audited backend order creation mapping

**Risk Assessment:**
- Risk Level: CRITICAL
- Layer of Risk: Both
- Risk Scenario: الكاشير يرى خصم الصنف داخل السلة والـ UI يرسله، لكن السيرفر ينشئ الطلب بدون تطبيق خصم الصنف نفسه، فيُعرض للمستخدم رقم ويُسجل رقم أعلى.

### Order Totals And Payment Balance
**Location:** `backend/KasserPro.Application/Services/Implementations/OrderService.cs:1218-1260`; `frontend/src/store/slices/cartSlice.ts:284-390`; `frontend/src/components/pos/PaymentModal.tsx:53-72`; `frontend/src/pages/pos/POSWorkspacePage.tsx:472-509` — Layer: Both  
**Purpose:** حساب إجمالي الطلب، الضريبة، المتبقي، وحد الائتمان قبل إتمام البيع.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);

if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
    order.DiscountAmount = Math.Round(order.Subtotal * (order.DiscountValue.Value / 100m), 2);
else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
    order.DiscountAmount = Math.Round(order.DiscountValue.Value, 2);
else
    order.DiscountAmount = 0;

if (order.DiscountAmount > order.Subtotal)
    order.DiscountAmount = order.Subtotal;

var afterDiscount = order.Subtotal - order.DiscountAmount;

if (order.DiscountAmount > 0 && order.Subtotal > 0)
{
    var discountRatio = order.DiscountAmount / order.Subtotal;
    order.TaxAmount = Math.Round(order.Items.Sum(item =>
    {
        var itemAfterDiscount = item.Subtotal * (1m - discountRatio);
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
```
```typescript
export const selectSubtotal = (state: { cart: CartState }) =>
  Math.round(
    state.cart.items.reduce(
      (sum, item) => sum + item.product.price * item.quantity,
      0,
    ) * 100,
  ) / 100;

export const selectDiscountAmount = (state: { cart: CartState }) => {
  if (!state.cart.discountType || !state.cart.discountValue) return 0;

  const subtotal = state.cart.items.reduce(
    (sum, item) => sum + item.product.price * item.quantity,
    0,
  );

  const itemDiscounts = state.cart.items.reduce(
    (sum, item) => sum + calcItemDiscount(item),
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
```
```typescript
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
```

**Formula in Plain Math:**
```text
Subtotal = Sum(UnitPrice × Quantity)
OrderDiscount = min(Percent or Fixed discount, RemainingAmount)
Tax = TaxRate × (Subtotal - ItemDiscounts - OrderDiscount)
Total = Subtotal - ItemDiscounts - OrderDiscount + Tax + ServiceCharge
AmountDue = Total - AmountPaid
Change = AmountPaid - Total
AvailableCredit = CreditLimit - CurrentCustomerDebt
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `order.Items` | `Orders` + `OrderItems` | Current order only | Backend authoritative items |
| `state.cart.items` | Redux `cart` slice | Current POS cart only | Frontend preview state |
| `state.cart.taxRate` | Redux `cart` slice / tenant settings sync | Current tenant settings | Frontend tax input |
| `selectedCustomer.creditLimit`, `selectedCustomer.totalDue` | Customer API state | Selected customer only | Credit validation |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — backend order load/filter uses tenant and branch in create/item paths
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — calculation is for the current open order only
- [ ] هل الدقة العشرية محمية؟ yes/no — backend yes (`decimal` + `Math.Round`); frontend uses JS number + explicit rounding
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ before tax in both audited formulas
- [ ] المردود: يُطرح من الإجمالي؟ no — refund handling is outside this live-sale formula
- [ ] هل يمكن أن تكون النتيجة سالبة؟ order-level discount is capped; payment balance can be negative only as `change`
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ partially — it matches for order-level discount/tax, but diverges when item-level discounts are used because the backend create path ignores them

**Risk Assessment:**
- Risk Level: HIGH
- Layer of Risk: Both
- Risk Scenario: إجمالي السلة يبدو صحيحاً للمستخدم في الـ POS، لكن وجود خصومات أصناف يجعل الـ frontend والـ backend يختلفان على الرقم النهائي المستحق.

### Product Tax-Inclusive Flag Vs Actual Sales Formula
**Location:** `frontend/src/components/products/ProductFormModal.tsx:483-529`; `backend/KasserPro.Application/Services/Implementations/OrderService.cs:171-178`; `backend/KasserPro.Application/Services/Implementations/OrderService.cs:447-455` — Layer: Both  
**Purpose:** تحديد هل سعر المنتج شامل الضريبة أم لا، ثم تطبيق ذلك عند حساب فواتير البيع.

**Code (exact, copy-paste من الكود الفعلي):**
```typescript
<Input
  label="معدل الضريبة (%)"
  type="number"
  min="0"
  max="100"
  step="0.01"
  value={formData.taxRate ?? ""}
  onChange={(e) =>
    setFormData({
      ...formData,
      taxRate: e.target.value ? parseFloat(e.target.value) : null,
    })
  }
  placeholder="استخدام الافتراضي"
/>
```
```typescript
<input
  type="radio"
  checked={formData.taxInclusive}
  onChange={() =>
    setFormData({ ...formData, taxInclusive: true })
  }
  className="w-4 h-4 text-primary-600"
/>
```
```csharp
// Price Snapshot - UnitPrice is NET (excluding tax)
UnitPrice = product.Price,
UnitCost = product.Cost,
OriginalPrice = product.Price,
Quantity = item.Quantity,
// Tax Snapshot - Dynamic from Product or Tenant
TaxRate = taxRate,
TaxInclusive = false, // Always Tax Exclusive (Additive)
```

**Formula in Plain Math:**
```text
UI captures: TaxInclusive = true or false
Backend order math uses: Price = NetPrice, Total = NetPrice + Tax
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `formData.taxInclusive` | Product edit form state | Current product form only | User-facing option |
| `product.Price` | `Products` | Product loaded for order creation | Treated as net price |
| `TaxInclusive = false` | Order item snapshot | Hard-coded in order service | Ignores product flag during sale calculation |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — product load is tenant-scoped in audited backend create path
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — not applicable here
- [ ] هل الدقة العشرية محمية؟ yes — backend uses decimal
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ before tax in audited sale flow
- [ ] المردود: يُطرح من الإجمالي؟ no — not part of this formula
- [ ] هل يمكن أن تكون النتيجة سالبة؟ no
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ no clear guarantee — the UI exposes tax-inclusive pricing, while the audited order-service sale math always treats selling price as tax-exclusive/additive

**Risk Assessment:**
- Risk Level: HIGH
- Layer of Risk: Both
- Risk Scenario: مسؤول الإعدادات يفعّل "السعر شامل الضريبة" على المنتج، بينما حساب البيع الفعلي يضيف الضريبة فوق السعر، فيظهر السعر أو الإجمالي أعلى من المتوقع.

### Purchase Invoice Totals And Frontend Preview
**Location:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:208-228`; `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:295-315`; `frontend/src/pages/purchase-invoices/PurchaseInvoiceFormPage.tsx:130-135` — Layer: Both  
**Purpose:** حساب إجماليات فاتورة الشراء والمبلغ المستحق للمورد.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
var itemTotal = itemRequest.Quantity * itemRequest.PurchasePrice;
subtotal += itemTotal;

invoice.Items.Add(new PurchaseInvoiceItem
{
    ProductId = product.Id,
    ProductName = product.Name,
    ProductSku = product.Sku,
    Quantity = itemRequest.Quantity,
    PurchasePrice = itemRequest.PurchasePrice,
    SellingPrice = itemRequest.SellingPrice,
    Total = itemTotal,
    Notes = itemRequest.Notes
});

invoice.Subtotal = subtotal;
invoice.TaxAmount = subtotal * (taxRate / 100);
invoice.Total = invoice.Subtotal + invoice.TaxAmount;
invoice.AmountDue = invoice.Total;
```
```typescript
const calculateSubtotal = () => {
  return items.reduce(
    (sum, item) => sum + item.quantity * item.purchasePrice,
    0,
  );
};
```

**Formula in Plain Math:**
```text
ItemTotal = Quantity × PurchasePrice
Subtotal = Sum(ItemTotal)
TaxAmount = Subtotal × TaxRate / 100
Total = Subtotal + TaxAmount
AmountDue = Total - AmountPaid
Frontend preview = Subtotal only
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `itemRequest.Quantity`, `itemRequest.PurchasePrice` | Purchase invoice request DTO | Current invoice only | Backend create/update input |
| `taxRate` | `Tenants` | Current tenant | Backend purchase tax source |
| `items[]` | Purchase invoice form local state | Current UI form only | Frontend preview ignores tax/total |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — backend supplier/product/invoice lookups are tenant-scoped and invoice carries current branch
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — create/update work on the current invoice draft only
- [ ] هل الدقة العشرية محمية؟ yes/no — backend uses `decimal` but does not round tax/total explicitly here; frontend uses JS number without explicit rounding in preview
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ no discount in audited purchase-invoice formula
- [ ] المردود: يُطرح من الإجمالي؟ no — cancellation/returns are handled elsewhere
- [ ] هل يمكن أن تكون النتيجة سالبة؟ no under audited validations
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ no — the form preview shows subtotal only while the server persists subtotal + tax + total

**Risk Assessment:**
- Risk Level: HIGH
- Layer of Risk: Both
- Risk Scenario: المستخدم يرى قيمة أولية أقل في شاشة إنشاء الفاتورة ثم يحفظ فاتورة بإجمالي أعلى بسبب الضريبة التي لم يعرضها الـ UI في نفس preview.

### Purchase Invoice Outstanding Balance
**Location:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:548-575`; `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:611-617`; `frontend/src/components/purchase-invoices/AddPaymentModal.tsx:31-49` — Layer: Both  
**Purpose:** تحديث الرصيد المدفوع والمتبقي عند إضافة أو حذف دفعة من فاتورة شراء.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
if (request.Amount <= 0)
    return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PAYMENT_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.PAYMENT_INVALID_AMOUNT));

if (request.Amount > invoice.AmountDue)
    return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PAYMENT_EXCEEDS_DUE, ErrorMessages.Get(ErrorCodes.PAYMENT_EXCEEDS_DUE));

invoice.AmountPaid += request.Amount;
invoice.AmountDue = invoice.Total - invoice.AmountPaid;
invoice.UpdatedAt = DateTime.UtcNow;
```
```csharp
invoice.AmountPaid -= payment.Amount;
invoice.AmountDue = invoice.Total - invoice.AmountPaid;
invoice.UpdatedAt = DateTime.UtcNow;
```
```typescript
const numAmount = Number(amount) || 0;
if (numAmount <= 0) {
  toast.error('المبلغ يجب أن يكون أكبر من صفر');
  return;
}

if (numAmount > amountDue) {
  toast.error(`المبلغ يتجاوز المبلغ المستحق (${formatCurrency(amountDue)})`);
  return;
}
```

**Formula in Plain Math:**
```text
After Add Payment:
AmountPaid = AmountPaid + PaymentAmount
AmountDue = Total - AmountPaid

After Delete Payment:
AmountPaid = AmountPaid - DeletedPaymentAmount
AmountDue = Total - AmountPaid
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `invoice.AmountPaid`, `invoice.AmountDue`, `invoice.Total` | `PurchaseInvoices` | Current tenant + invoice ID | Backend authoritative balances |
| `request.Amount` / `payment.Amount` | `PurchaseInvoicePayments` / request DTO | Current invoice only | Payment delta |
| `amountDue` | AddPayment modal prop | Loaded purchase invoice DTO | Frontend validation only |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — invoice load is tenant-scoped; payment load is invoice-scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ no — `DeletePaymentAsync` has no audited status-state protection
- [ ] هل الدقة العشرية محمية؟ yes — backend uses `decimal`; frontend uses JS number parsing
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ not applicable
- [ ] المردود: يُطرح من الإجمالي؟ not applicable
- [ ] هل يمكن أن تكون النتيجة سالبة؟ backend add path prevents it; delete path can drive workflow inconsistencies because status is not recalculated
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ mostly yes for amount validation, but no for lifecycle state because payment changes do not update invoice status on the backend

**Risk Assessment:**
- Risk Level: HIGH
- Layer of Risk: Backend
- Risk Scenario: دفعة شراء تُحذف لاحقاً فيتغير `AmountDue` رقمياً، لكن حالة الفاتورة workflow-wise لا تُعاد تسويتها بشكل منضبط.

### Customer Debt Balance
**Location:** `backend/KasserPro.Application/Services/Implementations/CustomerService.cs:365-425`; `frontend/src/components/customers/DebtPaymentModal.tsx:38-45`; `frontend/src/components/customers/DebtPaymentModal.tsx:283-294` — Layer: Both  
**Purpose:** تخفيض مديونية العميل وإظهار الرصيد المتبقي بعد السداد.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
if (request.Amount > customer.TotalDue)
{
    await _unitOfWork.RollbackTransactionAsync();
    return ApiResponse<PayDebtResponse>.Fail("AMOUNT_EXCEEDS_DEBT",
        $"المبلغ ({request.Amount:F2}) أكبر من الدين المستحق ({customer.TotalDue:F2})");
}

var balanceBefore = customer.TotalDue;
var balanceAfter = balanceBefore - request.Amount;

customer.TotalDue = balanceAfter;
```
```typescript
const numAmount = Number(formData.amount) || 0;
if (!formData.amount || numAmount <= 0) {
  newErrors.amount = "المبلغ يجب أن يكون أكبر من صفر";
}

if (numAmount > customer.totalDue) {
  newErrors.amount = `المبلغ أكبر من الدين المستحق (${customer.totalDue.toFixed(2)} ج.م)`;
}
```
```typescript
const numAmount = Number(formData.amount) || 0;
return (
  numAmount > 0 &&
  numAmount <= customer.totalDue && (
    <div className="bg-gradient-to-br from-green-50 to-green-100 p-4 rounded-xl border border-green-200">
      <div className="flex justify-between items-center">
        <span className="text-sm text-gray-600">
          المتبقي بعد الدفع
        </span>
        <span className="text-2xl font-bold text-green-600">
          {formatCurrency(customer.totalDue - numAmount)}
        </span>
      </div>
```

**Formula in Plain Math:**
```text
BalanceBefore = CurrentTotalDue
BalanceAfter = BalanceBefore - PaymentAmount
RemainingPreview = CustomerTotalDue - EnteredAmount
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `customer.TotalDue` | `Customers` | Current tenant + customer | Backend authoritative debt |
| `request.Amount` | PayDebt request DTO | Current payment only | Payment amount |
| `customer.totalDue` | DebtPayment modal prop | Selected customer only | Frontend preview |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — backend customer/user/shift queries are tenant/branch scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — not applicable to direct debt payment rows
- [ ] هل الدقة العشرية محمية؟ yes/no — backend yes (`decimal`); frontend uses JS numbers
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ not applicable
- [ ] المردود: يُطرح من الإجمالي؟ not applicable
- [ ] هل يمكن أن تكون النتيجة سالبة؟ backend prevents overpayment; frontend also blocks overpayment
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ yes for the previewed remaining balance

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Frontend
- Risk Scenario: المعادلة نفسها متطابقة، لكن الواجهة لا تربط السداد بوجود وردية مفتوحة، فيبدو الرصيد صحيحاً بينما السياق المالي ناقص.
