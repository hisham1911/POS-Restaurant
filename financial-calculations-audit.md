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
    UnitPrice = unitPrice,
    UnitCost = product.Cost,
    OriginalPrice = product.Price,
    Quantity = item.Quantity,
    DiscountType = NormalizeDiscountType(item.DiscountType),
    DiscountValue = item.DiscountValue,
    DiscountReason = item.DiscountReason,
    // Tax Snapshot - Dynamic from Product or Tenant
    TaxRate = taxRate,
    TaxInclusive = product.TaxInclusive,
    Notes = item.Notes
};
```

**Formula in Plain Math:**
```text
Frontend ItemDiscount = min(LineTotal * Percent, LineTotal) OR min(FixedDiscount, LineTotal)
Backend CreateAsync ItemDiscount = calculated in CalculateItemTotals using mapped DiscountType/DiscountValue
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `item.product.price` | Redux `cart.items[]` | Current POS cart only | Frontend net unit price |
| `item.quantity` | Redux `cart.items[]` | Current POS cart only | Frontend quantity |
| `item.discount.*` | Redux `cart.items[]` | Current POS cart only | Sent from `useOrders` payload |
| `product.Price` | `Products` | `TenantId` + branch context before order creation | Converted to net unit price when `TaxInclusive=true` |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — on the backend item creation path at `backend/KasserPro.Application/Services/Implementations/OrderService.cs:40-69`
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — not applicable at creation time
- [ ] هل الدقة العشرية محمية؟ yes/no — backend yes (`decimal`), frontend yes partially (`Math.round` to 2 decimals)
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ before tax in frontend and backend (`CalculateItemTotals`)
- [ ] المردود: يُطرح من الإجمالي؟ no — not in this line-item preview calculation
- [ ] هل يمكن أن تكون النتيجة سالبة؟ no — frontend clamps with `Math.min` والباك-إند يستخدم `Math.Clamp`
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ mostly yes — مع فروقات طفيفة محتملة بسبب اختلاف أسلوب التقريب

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Both
- Risk Scenario: منطق الخصم متوافق حاليًا، لكن اختلاف التقريب بين JavaScript (`number`) و`decimal` قد ينتج فروقات قروش في الحالات الحدية.

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
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ mostly yes — مع احتمال فروق rounding بسيطة أو اختلافات preview المؤقت قبل تجهيز الطلب

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Both
- Risk Scenario: الأرقام الأساسية متوافقة غالبًا، لكن ممكن تظهر فروقات طفيفة بين preview والرقم النهائي بعد اعتماد الباك-إند.

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

### POS Prepared Payment Draft Lifecycle
**Location:** `frontend/src/hooks/usePreparedPaymentOrder.ts:34-99`; `frontend/src/pages/pos/POSWorkspacePage.tsx:228-238`; `backend/KasserPro.Application/Services/Implementations/OrderService.cs:89-110` — Layer: Both  
**Purpose:** تحويل شاشة الدفع إلى رقم Authoritative من الباك-إند عبر إنشاء Draft Order مسبقًا، بدل الاعتماد على preview فقط.

**Code (exact, copy-paste من الكود الفعلي):**
```typescript
const cartSignature = JSON.stringify({
  customerId: customerId ?? null,
  discountType: discountType ?? null,
  discountValue: discountValue ?? null,
  items: items.map((item) => ({
    id: item.product.id,
    quantity: item.quantity,
    notes: item.notes ?? "",
    discountType: item.discount?.type ?? null,
    discountValue: item.discount?.value ?? null,
    discountReason: item.discount?.reason ?? "",
  })),
});

if (!enabled || items.length === 0) {
  setIsPreparingOrder(false);
  await discardPreparedOrder();
  return;
}

await discardPreparedOrder();
setIsPreparingOrder(true);

const order = await createOrderRef.current(customerId);
```
```typescript
const {
  preparedOrder,
  isPreparingOrder,
  markPreparedOrderCompleted,
} = usePreparedPaymentOrder({
  enabled: activeTab === "payment" && items.length > 0,
  customerId: selectedCustomer?.id,
  createOrder,
  cancelOrder,
  onPrepareFailed: () => setActiveTab("cart"),
});

const paymentTotal = preparedOrder?.total ?? total;
```
```csharp
var order = new Order
{
    TenantId = tenantId,
    BranchId = branchId,
    ShiftId = currentShift?.Id,
    OrderNumber = GenerateOrderNumber(),
    UserId = userId,
    CustomerId = request.CustomerId,
    CustomerName = customerName,
    CustomerPhone = customerPhone,
    Notes = request.Notes,
    Status = OrderStatus.Draft,
    OrderType = request.OrderType,
    BranchName = branch.Name,
    BranchAddress = branch.Address,
    BranchPhone = branch.Phone,
    UserName = user.Name,
    CurrencyCode = branch.CurrencyCode,
    TaxRate = tenantTaxRate,
    DiscountType = NormalizeDiscountType(request.DiscountType),
    DiscountValue = request.DiscountValue
};
```

**Formula in Plain Math:**
```text
PreparedDraft := Backend(CreateOrder(current cart snapshot))
PaymentTotalDisplayed := if PreparedDraft exists then PreparedDraft.Total else FrontendCartTotal
On Leave/Change := Cancel(previous PreparedDraft)
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `cartSignature` | Frontend local + Redux cart | Current POS session | Triggers re-prepare when any financial field changes |
| `preparedOrder.total` | `Orders.Total` | Current tenant/branch + generated draft order | Authoritative total shown on payment step |
| `Status = Draft` | `Orders.Status` | Create-order lifecycle only | Draft persisted before completion |
| `DRAFT_CANCEL_REASON` | Frontend constant | Silent cancel path | Used when abandoning prepared draft |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — create/cancel backend paths are tenant/branch scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — discarded prepared orders are cancelled
- [ ] هل الدقة العشرية محمية؟ yes/no — backend yes (`decimal`), frontend still uses JS number for input and display
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ before tax in the audited order calculator
- [ ] المردود: يُطرح من الإجمالي؟ not in this prepare step (refund is separate flow)
- [ ] هل يمكن أن تكون النتيجة سالبة؟ no for prepared order total
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ yes once `preparedOrder` is available

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Both
- Risk Scenario: أي فشل صامت في `cancelOrder(..., { silent: true })` قد يترك Draft Orders زائدة في النظام، ما يسبب ضوضاء تشغيلية في دورة الطلبات.

### POS Single-Payment UI Vs Multi-Payment Backend
**Location:** `frontend/src/pages/pos/POSWorkspacePage.tsx:491-499`; `frontend/src/components/pos/PaymentModal.tsx:139-147`; `backend/KasserPro.Application/Services/Implementations/OrderService.cs:581-610` — Layer: Both  
**Purpose:** تدقيق توافق payload الدفع بين الواجهة (single tender) والسيرفر (supports multiple tenders).

**Code (exact, copy-paste من الكود الفعلي):**
```typescript
const completedOrder = await completeOrder(preparedOrder.id, {
  payments: [{ method: selectedPaymentMethod, amount: numericAmount }],
});
```
```csharp
decimal totalPaid = 0;
decimal cashPaymentAmount = 0;
foreach (var paymentReq in request.Payments)
{
    if (paymentReq.Amount <= 0)
        continue;

    var payment = new Payment
    {
        TenantId = _currentUser.TenantId,
        BranchId = _currentUser.BranchId,
        OrderId = order.Id,
        Amount = Math.Round(paymentReq.Amount, 2),
        Method = Enum.Parse<PaymentMethod>(paymentReq.Method),
        Reference = paymentReq.Reference
    };
    await _unitOfWork.Payments.AddAsync(payment);
    order.Payments.Add(payment);
    totalPaid += payment.Amount;

    if (payment.Method == PaymentMethod.Cash)
        cashPaymentAmount += payment.Amount;
}

order.AmountPaid = Math.Round(totalPaid, 2);
order.AmountDue = Math.Round(order.Total - totalPaid, 2);
order.ChangeAmount = totalPaid > order.Total ? Math.Round(totalPaid - order.Total, 2) : 0;
```

**Formula in Plain Math:**
```text
TotalPaid = Sum(Payments[i].Amount)
AmountDue = Total - TotalPaid
ChangeAmount = max(TotalPaid - Total, 0)
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `payments[]` | CompleteOrder request payload | Current prepared order only | Frontend currently sends one payment row |
| `order.Payments` | `Payments` table | Current tenant + branch + order | Backend can store multiple rows |
| `order.AmountPaid`, `order.AmountDue`, `order.ChangeAmount` | `Orders` | Current completed order | Persisted settlement numbers |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — payment rows include tenant and branch snapshots
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — completion path validates legal transitions only
- [ ] هل الدقة العشرية محمية؟ yes — backend rounds each payment and totals to 2 decimals
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ before tax in upstream order-total calculation
- [ ] المردود: يُطرح من الإجمالي؟ no — handled in refund workflow
- [ ] هل يمكن أن تكون النتيجة سالبة؟ yes — `AmountDue` can become negative on overpayment because it is not clamped
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ mostly yes for one payment row; split payments are backend-capable but not exposed in POS UI

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Both
- Risk Scenario: النظام يدعم تعدد وسائل الدفع في السيرفر، لكن واجهة POS الحالية لا ترسل إلا سطر دفع واحد، ما يمنع split-tender ويخلق فجوة توقعات تشغيلية.

### Refund Lifecycle, Return Order Math, And Cash Impact
**Location:** `backend/KasserPro.Application/Services/Implementations/OrderService.cs:770-1135`; `frontend/src/pages/orders/OrdersPage.tsx:313-318`; `frontend/src/components/orders/OrderDetailsModal.tsx:289-305` — Layer: Both  
**Purpose:** تدقيق معادلات الاسترجاع الجزئي/الكامل، تخزين أثرها، وعرض صافي البيع بعد الاسترجاع.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
var unitPriceWithTax = orderItem.Total / orderItem.Quantity;
var itemRefundAmount = unitPriceWithTax * refundItem.Quantity;
totalRefundAmount += itemRefundAmount;

var returnItem = new OrderItem
{
    ProductId = orderItem.ProductId,
    ProductName = orderItem.ProductName,
    UnitPrice = -orderItem.UnitPrice,
    UnitCost = orderItem.UnitCost,
    OriginalPrice = orderItem.OriginalPrice,
    Quantity = refundItem.Quantity,
    TaxRate = orderItem.TaxRate,
    TaxInclusive = orderItem.TaxInclusive,
    DiscountType = orderItem.DiscountType,
    DiscountValue = orderItem.DiscountValue,
    DiscountAmount = -Math.Round((orderItem.DiscountAmount / orderItem.Quantity) * refundItem.Quantity, 2),
    TaxAmount = -Math.Round((orderItem.TaxAmount / orderItem.Quantity) * refundItem.Quantity, 2),
    Subtotal = -Math.Round((orderItem.Subtotal / orderItem.Quantity) * refundItem.Quantity, 2),
    Total = -Math.Round(itemRefundAmount, 2)
};
```
```csharp
originalOrder.RefundAmount = Math.Round(originalOrder.RefundAmount + totalRefundAmount, 2);
if (originalOrder.RefundAmount > originalOrder.Total)
    originalOrder.RefundAmount = originalOrder.Total;

var cashRefundAmount = isPartialRefund
    ? Math.Round((totalRefundAmount / originalOrder.Total) * originalCashPayments, 2)
    : originalCashPayments;
```
```typescript
const netAmount = o.total - (o.refundAmount || 0);
return sum + netAmount;
```

**Formula in Plain Math:**
```text
PartialItemRefund = (OriginalItemTotal / OriginalItemQty) × RefundQty
ReturnOrderTotals = Negative(Proportional Item Financials)
OriginalOrder.RefundAmount = min(OriginalOrder.RefundAmount + RefundTxn, OriginalOrder.Total)
CashRefund = (RefundTxn / OriginalOrder.Total) × OriginalCashPaid   [partial]
DisplayedOrderNet = Order.Total - Order.RefundAmount
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `orderItem.Total`, `orderItem.Quantity` | `OrderItems` | Original order items only | Base for proportional refund |
| `originalOrder.RefundAmount` | `Orders.RefundAmount` | Original sale order | Cumulative refunded amount |
| `returnOrder.Items[]` | `OrderItems` (Return order) | New return order | Stored as negative financial rows |
| `originalOrder.Payments` | `Payments` | Original order only | Used to derive proportional cash refund |
| `o.total`, `o.refundAmount` | Frontend order list state | Orders page filter set | Displayed net amount |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — refund load path is tenant+branch scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — refunds allowed only for completed/partially-refunded orders
- [ ] هل الدقة العشرية محمية؟ yes — proportional components are rounded to 2 decimals
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ both are proportionally reversed from stored item snapshots
- [ ] المردود: يُطرح من الإجمالي؟ yes — via `RefundAmount` and return-order negative totals
- [ ] هل يمكن أن تكون النتيجة سالبة؟ yes intentionally on return-order item and order totals
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ mostly yes — UI net = `total - refundAmount` matches persisted refund accumulator

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Both
- Risk Scenario: الاسترجاع الجزئي يعتمد على توزيع نسبي مع `Math.Round` لكل عنصر، ما قد يترك فروقات قروش تراكمية بين مجموع العناصر وتاريخ الاسترجاعات المتعددة.

### Supplier Debt Integrity Across Invoices And Reports
**Location:** `backend/KasserPro.Infrastructure/Services/SupplierReportService.cs:59-72`; `backend/KasserPro.Infrastructure/Services/SupplierReportService.cs:107-153`; `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:574-583` — Layer: Backend  
**Purpose:** التحقق هل رقم مديونية المورد موحّد المصدر بين جداول الفواتير وحقل المورد المجمع.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
var totalPurchases = g.Sum(pi => pi.Total);
var totalPaid = g.Sum(pi => pi.AmountPaid);

return new SupplierPurchaseDetailDto
{
    TotalPurchases = totalPurchases,
    TotalPaid = totalPaid,
    Outstanding = totalPurchases - totalPaid,
    LastPurchaseDate = g.Max(pi => pi.InvoiceDate),
};
```
```csharp
var suppliersWithDebt = await _context.Suppliers
    .Where(s => s.TenantId == tenantId
              && s.BranchId == branchId
              && s.IsActive
              && s.TotalDue > 0)
    .ToListAsync();

var unpaidInvoices = await _context.PurchaseInvoices
    .Where(pi => pi.TenantId == tenantId
              && pi.SupplierId == supplier.Id
              && pi.Status != PurchaseInvoiceStatus.Cancelled
              && pi.AmountDue > 0)
    .OrderBy(pi => pi.InvoiceDate)
    .ToListAsync();
```
```csharp
invoice.AmountPaid = Math.Round(invoice.AmountPaid + request.Amount, 2);
invoice.AmountDue = Math.Round(invoice.Total - invoice.AmountPaid, 2);
RecalculateInvoiceStatus(invoice);
invoice.UpdatedAt = DateTime.UtcNow;
```

**Formula in Plain Math:**
```text
OutstandingByInvoices = Sum(PurchaseInvoice.Total) - Sum(PurchaseInvoice.AmountPaid)
OutstandingBySupplierField = Supplier.TotalDue
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `totalPurchases`, `totalPaid`, `Outstanding` | `PurchaseInvoices` | Tenant + branch + non-cancelled + date range (purchases report) | Computed on-the-fly |
| `supplier.TotalDue` | `Suppliers` | Tenant + branch + active suppliers | Used directly in debts report |
| `invoice.AmountPaid`, `invoice.AmountDue` | `PurchaseInvoices` | Tenant + invoice only | Updated in payment add/delete paths |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ no — `GetSupplierDebtsReportAsync` unpaid-invoice and last-payment queries filter by `TenantId` فقط بدون `BranchId`
- [ ] هل يستثني السجلات Cancelled/Voided؟ partially — unpaid invoices exclude cancelled, but branch isolation is still incomplete
- [ ] هل الدقة العشرية محمية؟ yes — invoice payment amounts are rounded to 2 decimals
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ not applicable in supplier debt accumulator
- [ ] المردود: يُطرح من الإجمالي؟ not audited here for purchase-return states
- [ ] هل يمكن أن تكون النتيجة سالبة؟ limited by payment validations per invoice, but cross-source mismatch can still produce contradictory debt views
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ no guaranteed single source — supplier purchases report يعتمد فواتير، بينما supplier debts report يعتمد `Supplier.TotalDue`

**Risk Assessment:**
- Risk Level: HIGH
- Layer of Risk: Backend
- Risk Scenario: وجود مصدرين للمديونية (`Supplier.TotalDue` مقابل تجميع الفواتير) مع غياب تحديث واضح لحقل المورد في مسارات فواتير الشراء والمدفوعات يؤدي لتقارير دين متضاربة.

### Expense Lifecycle And Cash Register Impact
**Location:** `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:385-448`; `backend/KasserPro.Infrastructure/Services/FinancialReportService.cs:90-101`; `frontend/src/pages/reports/ExpensesReportPage.tsx:29-33` — Layer: Both  
**Purpose:** تتبع متى يدخل المصروف فعليًا في التقارير وكيف يؤثر على النقدية.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
expense.Status = ExpenseStatus.Paid;
expense.PaymentMethod = request.PaymentMethod;
expense.PaymentDate = request.PaymentDate;
expense.PaymentReferenceNumber = request.PaymentReferenceNumber;
expense.PaidByUserId = _currentUserService.UserId;
expense.PaidByUserName = user?.Name ?? _currentUserService.Email ?? "Unknown";
expense.PaidAt = DateTime.UtcNow;

if (request.PaymentMethod == PaymentMethod.Cash)
{
    await _cashRegisterService.RecordTransactionAsync(
        CashRegisterTransactionType.Expense,
        expense.Amount,
        $"Expense: {expense.Description}",
        "Expense",
        expense.Id,
        expense.ShiftId ?? currentShift.Id);
}
```
```csharp
var expenses = await _context.Expenses
    .Include(e => e.Category)
    .Where(e => e.TenantId == tenantId
             && e.BranchId == branchId
             && e.Status == ExpenseStatus.Paid
             && e.ExpenseDate >= fromDate.Date
             && e.ExpenseDate < toDate.Date.AddDays(1))
    .ToListAsync();
```

**Formula in Plain Math:**
```text
ExpenseIncludedInReport = (Status == Paid) AND (ExpenseDate within report range)
CashRegisterImpact = if PaymentMethod == Cash then BalanceAfter = BalanceBefore - ExpenseAmount
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `expense.Status`, `expense.Amount`, `expense.PaymentMethod` | `Expenses` | Tenant + branch + expense ID | Authoritative lifecycle state |
| `CashRegisterTransaction(Type=Expense)` | `CashRegisterTransactions` | Current tenant/branch/shift | Written only for cash payments |
| `report.totalExpenses` | Financial report API result | Paid expenses by date range | Displayed on report pages |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — expense queries and updates are scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — only `ExpenseStatus.Paid` enters financial reports
- [ ] هل الدقة العشرية محمية؟ yes — backend uses decimal amounts end-to-end
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ not applicable
- [ ] المردود: يُطرح من الإجمالي؟ not applicable in expense flow
- [ ] هل يمكن أن تكون النتيجة سالبة؟ not expected for standard paid-expense amounts
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ yes — page binds directly to backend report totals

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Backend
- Risk Scenario: التقرير يعتمد `ExpenseDate` لا `PaymentDate`، لذلك قد يظهر تأثير المصروف في فترة مختلفة عن حركة الخزينة الفعلية.

### Cash Register Running Balance And Transfer Direction
**Location:** `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs:485-495`; `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs:349-380`; `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs:431-442`; `frontend/src/pages/cash-register/CashRegisterDashboard.tsx:58-67` — Layer: Both  
**Purpose:** تدقيق المعادلة الحاكمة للرصيد بعد كل حركة واتساق تلخيص التحويلات.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
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
```csharp
var withdrawalTransaction = new CashRegisterTransaction
{
    Type = CashRegisterTransactionType.Transfer,
    Amount = request.Amount,
    BalanceBefore = sourceBalance,
    BalanceAfter = sourceBalance - request.Amount,
};

var depositTransaction = new CashRegisterTransaction
{
    Type = CashRegisterTransactionType.Transfer,
    Amount = request.Amount,
    BalanceBefore = targetBalance,
    BalanceAfter = targetBalance + request.Amount,
};
```
```csharp
TotalTransfersIn = transactions.Where(t => t.Type == CashRegisterTransactionType.Transfer && t.Amount > 0).Sum(t => t.Amount),
TotalTransfersOut = transactions.Where(t => t.Type == CashRegisterTransactionType.Transfer && t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
```
```typescript
const incomingTotal = transactions
  .map((t) => t.balanceAfter - t.balanceBefore)
  .filter((delta) => delta > 0)
  .reduce((sum, delta) => sum + delta, 0);
const outgoingTotal = transactions
  .map((t) => t.balanceAfter - t.balanceBefore)
  .filter((delta) => delta < 0)
  .reduce((sum, delta) => sum + Math.abs(delta), 0);
```

**Formula in Plain Math:**
```text
BalanceAfter = BalanceBefore ± Amount   (sign decided by TransactionType)
DashboardIncoming/Outgoing = Sum(delta of displayed transactions only)
TransferSummaryOut = Sum(Transfer.Amount where Amount < 0)
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `BalanceBefore`, `BalanceAfter`, `Amount`, `Type` | `CashRegisterTransactions` | Tenant + branch | Authoritative ledger row |
| `transactions` (dashboard) | Cash register API response | Tenant + branch + `pageSize=10` | Dashboard cards are based on last 10 rows only |
| `TotalTransfersIn/Out` | Cash register summary service | Date range + branch | Depends on sign of stored transfer amount |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — service queries are scoped by tenant and branch
- [ ] هل يستثني السجلات Cancelled/Voided؟ not applicable for immutable cash-ledger rows
- [ ] هل الدقة العشرية محمية؟ yes — all ledger values are decimal
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ not applicable
- [ ] المردود: يُطرح من الإجمالي؟ yes — `Refund` type subtracts from cash balance
- [ ] هل يمكن أن تكون النتيجة سالبة؟ yes — withdrawals/expenses/refunds can push balance down (subject to validations)
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ partially — dashboard totals reflect only latest page, not full period totals

**Risk Assessment:**
- Risk Level: HIGH
- Layer of Risk: Backend
- Risk Scenario: `TransferCashAsync` يخزن `Amount` موجبًا في جانبي التحويل، بينما `GetSummaryAsync` يحسب `TotalTransfersOut` على القيم السالبة فقط، ما قد يُظهر تحويلات الخروج = 0 رغم حدوثها.

### Shift Closing And Reconciliation Formulas
**Location:** `backend/KasserPro.Application/Services/Implementations/ShiftService.cs:182-188`; `backend/KasserPro.Application/Services/Implementations/ShiftService.cs:514-538`; `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs:247-262` — Layer: Backend  
**Purpose:** توثيق معادلة الرصيد المتوقع والفروقات عند إغلاق الوردية والتسوية.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
var (totalOrders, totalCash, totalCard, _, _) = CalculateShiftFinancials(shift.Orders);
shift.TotalOrders = totalOrders;
shift.TotalCash = totalCash;
shift.TotalCard = totalCard;

shift.ClosingBalance = Math.Round(request.ClosingBalance, 2);
shift.ExpectedBalance = Math.Round(shift.OpeningBalance + totalCash, 2);
shift.Difference = Math.Round(shift.ClosingBalance - shift.ExpectedBalance, 2);
```
```csharp
var totalCash = Math.Round(
    salesPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
    - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)), 2);
```
```csharp
var expectedBalance = await CalculateExpectedBalanceAsync(shift.BranchId, shift.OpenedAt);
var variance = request.ActualBalance - expectedBalance;

shift.ClosingBalance = request.ActualBalance;
shift.ExpectedBalance = expectedBalance;
shift.Difference = variance;
```

**Formula in Plain Math:**
```text
ShiftCloseExpected = OpeningBalance + NetCashSales
NetCashSales = CashSalesPayments - CashRefundPayments
Difference = ActualClosingBalance - ExpectedBalance

ReconciliationExpected = LastCashRegisterBalanceSinceShiftOpened
Variance = ActualBalance - ReconciliationExpected
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `shift.OpeningBalance`, `shift.ClosingBalance`, `shift.ExpectedBalance`, `shift.Difference` | `Shifts` | Tenant + branch + shift/user context | Persisted shift settlement values |
| `salesPayments`, `returnPayments` | `Payments` via shift orders | Completed/PartiallyRefunded/Refunded orders | Used in `CalculateShiftFinancials` |
| `expectedBalance` (reconcile) | `CashRegisterTransactions` | Tenant + branch + `TransactionDate >= shift.OpenedAt` | Cash-ledger based expected amount |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — close/reconcile shift paths are scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — financials include completed/refunded states only
- [ ] هل الدقة العشرية محمية؟ yes — calculations are rounded to 2 decimals in close path
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ not directly at shift layer (already embedded in order totals)
- [ ] المردود: يُطرح من الإجمالي؟ yes — refund payments are netted in cash totals
- [ ] هل يمكن أن تكون النتيجة سالبة؟ yes — `Difference`/`Variance` can be positive or negative by design
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ yes for stored shift fields, but depends on which backend expected-balance model was used (close vs reconcile)

**Risk Assessment:**
- Risk Level: HIGH
- Layer of Risk: Backend
- Risk Scenario: هناك معادلتان مختلفتان للـ Expected Balance (`Opening + NetCashSales` مقابل رصيد دفتر الخزينة)، ما قد يسبب فروقات تفسيرية لنفس الوردية.

### Daily And Sales Reports Netting Logic
**Location:** `backend/KasserPro.Application/Services/Implementations/ReportService.cs:32-40`; `backend/KasserPro.Application/Services/Implementations/ReportService.cs:109-117`; `backend/KasserPro.Application/Services/Implementations/ReportService.cs:225-236`; `frontend/src/pages/reports/DailyReportPage.tsx:181-206` — Layer: Both  
**Purpose:** تتبّع صافي المبيعات اليومي بعد المرتجعات وأثره على breakdown طرق الدفع.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
var shifts = await _unitOfWork.Shifts.Query()
    .Where(s => s.TenantId == tenantId
             && s.BranchId == branchId
             && s.IsClosed
             && s.ClosedAt >= utcFrom
             && s.ClosedAt < utcTo)
    .ToListAsync();
```
```csharp
var totalCash = Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount) - refundedCash);
var totalCard = Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount) - refundedCard);
var totalFawry = Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount) - refundedFawry);
var totalOther = Math.Max(0, allPayments.Where(p => p.Method != PaymentMethod.Cash
                                      && p.Method != PaymentMethod.Card
                                      && p.Method != PaymentMethod.Fawry).Sum(p => p.Amount) - refundedOther);

var totalRefunds = Math.Abs(returnOrders.Sum(o => o.Total));
var actualTotalSales = totalSales - totalRefunds;
```
```typescript
<div className="row total">
  <span>💰 صافي الإيراد</span>
  <span className="value">${fmt(report.totalSales)} ج.م</span>
</div>
```

**Formula in Plain Math:**
```text
DailyScopeOrders = Orders from shifts closed in selected day
NetPaymentByMethod = max(SalesPaymentsByMethod - RefundPaymentsByMethod, 0)
ActualTotalSales = SalesOrdersTotal - |ReturnOrdersTotal|
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `shifts` | `Shifts` | Tenant + branch + `IsClosed` + closed date range | Entry gate for daily report |
| `allPayments`, `returnPayments` | `Payments` via orders inside selected shifts | Completed/refunded statuses | Payment-method breakdown basis |
| `actualTotalSales` | Derived in report service | Same daily scope | Returned to frontend report page |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — daily report query is scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — sales calculations use completed/refunded states only
- [ ] هل الدقة العشرية محمية؟ yes — backend uses decimal arithmetic
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ discount is already embedded in stored order numbers used by report
- [ ] المردود: يُطرح من الإجمالي؟ yes — explicit subtraction via return orders/payments
- [ ] هل يمكن أن تكون النتيجة سالبة؟ payment method outputs are clamped to zero; total sales can shrink heavily with high returns
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ yes — frontend renders backend `report.totalSales`

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Both
- Risk Scenario: التقرير اليومي يعتمد فقط الورديات المغلقة؛ أثناء اليوم أو مع ورديات لم تُغلق بعد قد يظهر رقم أقل من الواقع التشغيلي اللحظي.

### Profit And Loss Formula (Revenue, COGS, Expenses)
**Location:** `backend/KasserPro.Infrastructure/Services/FinancialReportService.cs:61-87`; `backend/KasserPro.Infrastructure/Services/FinancialReportService.cs:90-118`; `frontend/src/pages/reports/ProfitLossReportPage.tsx:116-170` — Layer: Both  
**Purpose:** تدقيق اشتقاق صافي الربح من المبيعات والمرتجعات والتكلفة والمصروفات.

**Code (exact, copy-paste من الكود الفعلي):**
```csharp
var grossSales = orders.Sum(o => o.Subtotal);
var totalItemDiscounts = orders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
var totalOrderDiscounts = orders.Sum(o => o.DiscountAmount);
var totalDiscount = totalItemDiscounts + totalOrderDiscounts;
var netSales = grossSales - totalDiscount;
var totalRevenue = orders.Sum(o => o.Total);
var refundsAmount = Math.Abs(returnOrders.Sum(o => o.Total));

var actualNetSales = netSales - refundsAmount;
var actualTotalRevenue = totalRevenue - refundsAmount;
```
```csharp
var totalCost = orders
    .SelectMany(o => o.Items)
    .Sum(i => (i.UnitCost ?? 0) * i.Quantity);

var returnedCost = returnOrders
    .SelectMany(o => o.Items)
    .Sum(i => (i.UnitCost ?? 0) * Math.Abs(i.Quantity));
var netCost = totalCost - returnedCost;

var grossProfit = actualNetSales - netCost;
var netProfit = grossProfit - totalExpenses;
```
```csharp
var expenses = await _context.Expenses
    .Include(e => e.Category)
    .Where(e => e.TenantId == tenantId
             && e.BranchId == branchId
             && e.Status == ExpenseStatus.Paid
             && e.ExpenseDate >= fromDate.Date
             && e.ExpenseDate < toDate.Date.AddDays(1))
    .ToListAsync();
```

**Formula in Plain Math:**
```text
TotalDiscount = ItemDiscounts + OrderDiscounts
ActualNetSales = (GrossSales - TotalDiscount) - Refunds
NetCOGS = SoldItemsCost - ReturnedItemsCost
GrossProfit = ActualNetSales - NetCOGS
NetProfit = GrossProfit - PaidExpenses
NetProfitMargin = NetProfit / ActualTotalRevenue
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| `orders`, `returnOrders` | `Orders` + `OrderItems` | Tenant + branch + completed/refunded statuses + date range | Revenue/discount/COGS base |
| `totalExpenses` | `Expenses` | Tenant + branch + `Status=Paid` + expense date range | Operating expense base |
| `netProfit`, `netProfitMargin` | Financial report DTO | Backend computed fields | Rendered directly on frontend |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — all core report queries are scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — order filters include completed/refunded states only
- [ ] هل الدقة العشرية محمية؟ yes — backend decimal calculations with explicit rounding for margins
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ discounts are aggregated from stored order/item snapshots before net-profit derivation
- [ ] المردود: يُطرح من الإجمالي؟ yes — refunds and returned COGS are netted explicitly
- [ ] هل يمكن أن تكون النتيجة سالبة؟ yes — net profit and margins can be negative
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ yes — page displays backend DTO numbers

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Backend
- Risk Scenario: نطاق التواريخ للمبيعات يعتمد تحويل UTC، بينما المصروفات تعتمد `ExpenseDate` المحلي مباشرة؛ هذا قد يخلق انحرافًا زمنيًا على حدود الأيام.

### Frontend Vs Backend Precision And Rounding
**Location:** `frontend/src/store/slices/cartSlice.ts:269-333`; `frontend/src/store/slices/cartSlice.ts:451-458`; `backend/KasserPro.Application/Services/Implementations/OrderService.cs:1226-1259`; `backend/KasserPro.Application/Services/Implementations/OrderService.cs:1333-1337` — Layer: Both  
**Purpose:** مقارنة سياسة التقريب في JS preview مقابل decimal backend.

**Code (exact, copy-paste من الكود الفعلي):**
```typescript
export const selectTotal = (state: { cart: CartState }) => {
  const subtotal = state.cart.items.reduce((sum, item) =>
    sum + getCartItemSubtotal(item, state.cart.taxRate, state.cart.isTaxEnabled), 0,
  );

  const afterAllDiscounts = afterItemDiscounts - orderDiscount;

  if (!state.cart.isTaxEnabled) {
    return Math.round(afterAllDiscounts * 100) / 100;
  }

  const taxAmount = selectTaxAmount(state);
  return Math.round((afterAllDiscounts + taxAmount) * 100) / 100;
};
```
```csharp
order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);
order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);
order.Total = Math.Round(afterDiscount + order.TaxAmount + order.ServiceChargeAmount, 2);
```
```csharp
private static decimal ResolveNetUnitPrice(decimal configuredPrice, bool taxInclusive, decimal taxRate)
{
    if (taxInclusive && taxRate > 0)
        return Math.Round(configuredPrice / (1m + (taxRate / 100m)), 4);

    return configuredPrice;
}
```

**Formula in Plain Math:**
```text
FrontendPreview = round2( subtotal - discounts + tax )
BackendAuthoritative = round2( sum(item financials) + order adjustments )
TaxInclusiveNetUnit = round4( GrossPrice / (1 + TaxRate/100) )
```

**Variable Sources:**
| Variable | Source Table / Store | Filter Applied | Notes |
|----------|---------------------|----------------|-------|
| Frontend subtotal/discount/tax | Redux cart selectors | Current cart state | JS `number` arithmetic + round2 |
| Backend item/order totals | `Orders` + `OrderItems` | Current order transaction | Decimal arithmetic + round2 |
| Net unit price conversion | Product snapshot during order creation | Product + tax settings | Round4 pre-step before item totals |

**Potential Issues:**
- [ ] هل يُفلتر بـ TenantId AND BranchId؟ yes — backend authoritative writes are scoped
- [ ] هل يستثني السجلات Cancelled/Voided؟ yes — calculation paths apply to active create/complete flows
- [ ] هل الدقة العشرية محمية؟ yes/no — backend strongly; frontend partially due IEEE floating-point
- [ ] الخصم: يُطبَّق قبل الضريبة أم بعدها؟ before tax in audited flows
- [ ] المردود: يُطرح من الإجمالي؟ refund is outside live cart total and handled later
- [ ] هل يمكن أن تكون النتيجة سالبة؟ typically no for sale totals; edge display differences can still occur in cents
- [ ] *(للفرونت-إند)* هل الرقم المعروض للمستخدم يتطابق مع ما يحسبه الباك-إند؟ mostly yes, with potential penny-level drift in edge cases

**Risk Assessment:**
- Risk Level: MEDIUM
- Layer of Risk: Both
- Risk Scenario: اختلاف round points (round4 في تحويل السعر الصافي + round2 في مراحل مختلفة) بين FE وBE قد ينتج فروق قروش عند حالات حدودية أو كميات كبيرة.
