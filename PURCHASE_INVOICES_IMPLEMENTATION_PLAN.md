# خطة إصلاح ميزة فواتير الشراء والموردين — KasserPro

## تعليمات للأيجنت

نفّذ **كل التاسكات بالترتيب المحدد** — الترتيب مهم لأن بعض التاسكات تعتمد على بعض.
**لا تتخطى تاسك.**
في الآخر ارفع **تقرير تنفيذ** بالصيغة المحددة في نهاية الملف.

### قواعد ثابتة في كل الكود

- ❌ مفيش AutoMapper — استخدم `.Select()` أو `private static MapToDto()`
- ❌ مفيش FluentValidation — validation يدوي في الـ service
- ✅ Transaction pattern الموجود في المشروع:
  ```csharp
  await using var transaction = await _unitOfWork.BeginTransactionAsync();
  try { ... await _unitOfWork.CommitTransactionAsync(); }
  catch { await _unitOfWork.RollbackTransactionAsync(); throw; }
  ```
- ✅ كل response = `ApiResponse<T>.Success(...)` أو `ApiResponse<T>.Fail(ErrorCodes.X, ErrorMessages.Get(...))`
- ✅ كل query فيها `TenantId` — و`BranchId` لو البيانات branch-scoped
- ✅ كل read query = `AsNoTracking()`
- ✅ `CancellationToken ct` في كل async method جديدة

---

## المرحلة 1 — إصلاح البيانات المالية (الأهم — نفّذها أولاً)

---

### Task B-5 — إصلاح DeletePaymentAsync: عكس CashRegister عند حذف دفعة نقدية

**الملف:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
**الموقع:** method `DeletePaymentAsync`

**المشكلة:** لما بيتحذف دفعة نقدية (Method == Cash)، الـ CashRegister مش بيتعكس — بيفسد رصيد الكاش بصمت.

**المطلوب:** داخل الـ transaction، بعد حذف الدفعة وقبل `SaveChangesAsync`، أضف:

```csharp
// Reverse CashRegister for cash payments
if (payment.Method == PaymentMethod.Cash)
{
    await _cashRegisterService.RecordTransactionAsync(
        CashRegisterTransactionType.SupplierPaymentReversal,
        payment.Amount,
        $"عكس دفعة مورد محذوفة - فاتورة {invoice.InvoiceNumber}",
        payment.CreatedByUserId);
}
```

> لو `CashRegisterTransactionType.SupplierPaymentReversal` مش موجود، أضفه في الـ enum. لو مفيش enum value مناسب، استخدم أقرب value موجود وسمّيه بشكل واضح في comment.

> لو `_cashRegisterService` مش موجود في هذا الـ service، ابحث عن طريقة inject الـ dependency اللي اتستخدمت في `AddPaymentAsync` في نفس الملف واتبع نفس الأسلوب.

**تحقق:** بعد التعديل، الـ method لازم تعكس الكاش لو وبس لو `payment.Method == PaymentMethod.Cash`.

---

### Task B-1/B-2/B-3 — تحديث Supplier stats (TotalPaid, TotalPurchases, LastPurchaseDate)

**الملف:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`

**القاعدة:** الـ fields دي denormalized — بتتحدث في 4 أماكن:

| الـ Field | ConfirmAsync | CancelAsync | AddPaymentAsync | DeletePaymentAsync |
|-----------|-------------|-------------|-----------------|-------------------|
| `TotalPurchases` | `+= invoice.Total` | `-= invoice.Total` (لو wasConfirmed) | — | — |
| `LastPurchaseDate` | `= invoice.InvoiceDate` | لا تغيير | — | — |
| `TotalPaid` | — | — | `+= request.Amount` | `-= payment.Amount` |

**التعديلات المطلوبة:**

**في `ConfirmAsync`** — في الجزء اللي بيجيب الـ supplier وبيزود `TotalDue`، أضف:
```csharp
if (supplier != null)
{
    supplier.TotalDue = Math.Round(supplier.TotalDue + invoice.AmountDue, 2);
    supplier.TotalPurchases = Math.Round(supplier.TotalPurchases + invoice.Total, 2); // ← أضف
    supplier.LastPurchaseDate = invoice.InvoiceDate;                                   // ← أضف
}
```

**في `CancelAsync`** — في الجزء اللي بيعكس `TotalDue`، أضف:
```csharp
if (supplier != null)
{
    supplier.TotalDue = Math.Round(Math.Max(0m, supplier.TotalDue - invoice.AmountDue), 2);
    supplier.TotalPurchases = Math.Round(Math.Max(0m, supplier.TotalPurchases - invoice.Total), 2); // ← أضف
    // ملاحظة: LastPurchaseDate لا تُعكس عند الإلغاء — تبقى كما هي
}
```

**في `AddPaymentAsync`** — في الجزء اللي بيعدل supplier، أضف:
```csharp
supplier.TotalDue = Math.Round(Math.Max(0m, supplier.TotalDue - request.Amount), 2);
supplier.TotalPaid = Math.Round(supplier.TotalPaid + request.Amount, 2); // ← أضف
```

**في `DeletePaymentAsync`** — في الجزء اللي بيعدل supplier، أضف:
```csharp
supplier.TotalDue = Math.Round(supplier.TotalDue + payment.Amount, 2);
supplier.TotalPaid = Math.Round(Math.Max(0m, supplier.TotalPaid - payment.Amount), 2); // ← أضف
```

---

### Task B-11 — تأمين Confirm endpoint بـ permission أقوى

**الملف:** `backend/KasserPro.API/Controllers/PurchaseInvoicesController.cs`
**الموقع:** الـ action method للـ confirm endpoint

**المشكلة:** تأكيد الفاتورة بيزود المخزون ويزود TotalDue على المورد — عملية حساسة لا يجب أن يملكها كل مستخدم عنده `PurchaseInvoicesManage`.

**المطلوب:** غيّر الـ attribute على الـ confirm action من:
```csharp
[HasPermission(Permission.PurchaseInvoicesManage)]
```
إلى:
```csharp
[Authorize(Roles = "Admin")]
[HasPermission(Permission.PurchaseInvoicesManage)]
```

> يعني المستخدم لازم يكون Admin **و** عنده الـ permission. ده نفس النمط المستخدم في `SuppliersController` للعمليات الحساسة.

---

### Task A-1/B-9 — إضافة BranchId filter في SupplierService.GetAllAsync

**الملف:** `backend/KasserPro.Application/Services/Implementations/SupplierService.cs`
**الموقع:** method `GetAllAsync`

**المشكلة:** الموردين scoped بـ `BranchId` في الـ Entity، لكن الـ query بتجيب كل موردي الـ tenant بدون تمييز الفرع.

**المطلوب:** في الـ `Where` clause، أضف `BranchId`:
```csharp
var suppliers = await _context.Suppliers
    .AsNoTracking()
    .Where(s => s.TenantId == _currentUser.TenantId
             && s.BranchId == _currentUser.BranchId  // ← أضف
             && !s.IsDeleted)
    .Select(s => new SupplierDto { ... })
    .ToListAsync(ct);
```

نفس التعديل على `GetByIdAsync` — تأكد إن فيه `BranchId` في الـ filter.

---

### Task B-6/A-2 — إضافة BranchId filter في PurchaseInvoiceService.GetAllAsync

**الملف:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
**الموقع:** method `GetAllAsync` — السطر 34 تقريباً

**المطلوب:** في الـ `Where` clause الرئيسي للـ query، أضف:
```csharp
.Where(pi => pi.TenantId == _currentUser.TenantId
          && pi.BranchId == _currentUser.BranchId  // ← أضف
          && !pi.IsDeleted)
```

---

## المرحلة 2 — إصلاح Backend (data integrity)

---

### Task B-4 — تحديث SupplierProduct عند تأكيد الفاتورة

**الملف:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
**الموقع:** `ConfirmAsync` — داخل الـ `foreach (var item in invoice.Items)` لوب، بعد تحديث الـ product metadata

**المطلوب:** بعد السطر `product.UpdatedAt = DateTime.UtcNow;`، أضف upsert للـ SupplierProduct:

```csharp
// Update SupplierProduct link
var supplierProduct = await _unitOfWork.SupplierProducts.Query()
    .FirstOrDefaultAsync(sp => sp.SupplierId == invoice.SupplierId
                             && sp.ProductId == item.ProductId
                             && sp.TenantId == tenantId);

if (supplierProduct == null)
{
    supplierProduct = new SupplierProduct
    {
        TenantId = tenantId,
        SupplierId = invoice.SupplierId,
        ProductId = item.ProductId,
        IsPreferred = false,
        LastPurchasePrice = item.PurchasePrice,
        LastPurchaseDate = invoice.InvoiceDate,
        TotalQuantityPurchased = item.Quantity,
        TotalAmountSpent = Math.Round(item.Quantity * item.PurchasePrice, 2),
        CreatedAt = DateTime.UtcNow
    };
    await _unitOfWork.SupplierProducts.AddAsync(supplierProduct);
}
else
{
    supplierProduct.LastPurchasePrice = item.PurchasePrice;
    supplierProduct.LastPurchaseDate = invoice.InvoiceDate;
    supplierProduct.TotalQuantityPurchased += item.Quantity;
    supplierProduct.TotalAmountSpent = Math.Round(
        supplierProduct.TotalAmountSpent + (item.Quantity * item.PurchasePrice), 2);
    supplierProduct.UpdatedAt = DateTime.UtcNow;
    _unitOfWork.SupplierProducts.Update(supplierProduct);
}
```

> لو `_unitOfWork.SupplierProducts` مش موجود، ابحث عن الـ DbSet المقابل في `AppDbContext` واستخدمه مباشرة عبر `_context`.

---

### Task B-8 — إضافة IsDeleted check في ConfirmAsync

**الملف:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
**الموقع:** `ConfirmAsync` — داخل الـ loop، السطر اللي بيجيب الـ product:

```csharp
var product = await _unitOfWork.Products.Query()
    .FirstOrDefaultAsync(p => p.Id == item.ProductId);
```

**المطلوب:** أضف `&& !p.IsDeleted`:
```csharp
var product = await _unitOfWork.Products.Query()
    .FirstOrDefaultAsync(p => p.Id == item.ProductId && !p.IsDeleted);
```

---

### Task B-7 — إزالة DataAnnotations من AddPaymentRequest

**الملف:** `backend/KasserPro.Application/DTOs/PurchaseInvoice/AddPaymentRequest.cs`

**المشكلة:** الـ DTO بيستخدم `[Required]`, `[Range]` attributes — inconsistent مع باقي المشروع.

**المطلوب:** احذف كل الـ DataAnnotations attributes من الـ class. الـ validation بيتعمل يدوياً في الـ service — تأكد إن `AddPaymentAsync` بتعمل الـ checks دي يدوياً:
- `Amount > 0`
- `Amount <= invoice.AmountDue`
- `Method` valid enum value
- `ReferenceNumber` required لو Method != Cash

لو الـ service مش بتعملهم، أضفهم بنفس أسلوب الـ validation الموجود في المشروع.

---

### Task B-10 — إصلاح N+1 في SupplierDebtsReportService

**الملف:** `backend/KasserPro.Application/Services/Implementations/SupplierReportService.cs`
**الموقع:** method اللي بتجيب تقرير الديون (غالباً `GetSupplierDebtsAsync`)

**المشكلة:** الكود بيلف على كل مورد ويعمل query منفصلة لأقدم فاتورة غير مدفوعة.

**المطلوب:** حوّل الـ loop لـ batch query باستخدام GroupBy أو subquery:

```csharp
// ✅ batch query بدل N+1
var suppliers = await _context.Suppliers
    .AsNoTracking()
    .Where(s => s.TenantId == _currentUser.TenantId
             && s.BranchId == _currentUser.BranchId
             && !s.IsDeleted
             && s.TotalDue > 0)
    .Select(s => new SupplierDebtsReportDto
    {
        SupplierId = s.Id,
        SupplierName = s.Name,
        Phone = s.Phone,
        TotalDue = s.TotalDue,
        TotalPurchases = s.TotalPurchases,
        OldestUnpaidInvoiceDate = s.PurchaseInvoices
            .Where(pi => !pi.IsDeleted
                      && pi.AmountDue > 0
                      && pi.Status != PurchaseInvoiceStatus.Cancelled)
            .OrderBy(pi => pi.InvoiceDate)
            .Select(pi => (DateTime?)pi.InvoiceDate)
            .FirstOrDefault(),
        OldestUnpaidInvoiceNumber = s.PurchaseInvoices
            .Where(pi => !pi.IsDeleted
                      && pi.AmountDue > 0
                      && pi.Status != PurchaseInvoiceStatus.Cancelled)
            .OrderBy(pi => pi.InvoiceDate)
            .Select(pi => pi.InvoiceNumber)
            .FirstOrDefault()
    })
    .ToListAsync(ct);
```

> ده بيحوّل N+1 queries لـ query واحدة. EF Core هيترجمها لـ correlated subqueries.

---

## المرحلة 3 — Frontend

---

### Task F-1 — إضافة الـ fields المالية لـ SupplierDto وsupplier.types.ts

**الملف 1:** `backend/KasserPro.Application/DTOs/Supplier/SupplierDto.cs`

أضف الـ fields دي:
```csharp
public decimal TotalDue { get; set; }
public decimal TotalPaid { get; set; }
public decimal TotalPurchases { get; set; }
public DateTime? LastPurchaseDate { get; set; }
```

تأكد إنهم بيتمابوا في الـ `.Select()` projection في `SupplierService.GetAllAsync` و`GetByIdAsync`:
```csharp
TotalDue = s.TotalDue,
TotalPaid = s.TotalPaid,
TotalPurchases = s.TotalPurchases,
LastPurchaseDate = s.LastPurchaseDate,
```

**الملف 2:** `frontend/src/types/supplier.types.ts`

أضف للـ `Supplier` interface:
```typescript
totalDue: number;
totalPaid: number;
totalPurchases: number;
lastPurchaseDate?: string;
```

---

### Task F-2 — عرض المعلومات المالية في SuppliersPage

**الملف:** `frontend/src/pages/suppliers/SuppliersPage.tsx`

**المطلوب:**

1. **Summary Cards** — أضف أو عدّل الـ cards الموجودة لتشمل:
   - "إجمالي المستحق للموردين" — مجموع `totalDue` لكل الموردين
   - "إجمالي المدفوع" — مجموع `totalPaid`

2. **الجدول** — أضف أعمدة:
   - "المستحق" → يعرض `totalDue` مع تنسيق العملة. لو `totalDue > 0` اعرضه باللون الأحمر.
   - "آخر شراء" → يعرض `lastPurchaseDate` بصيغة تاريخ مقروء (لو موجود).

استخدم نفس أسلوب تنسيق العملة المستخدم في باقي الصفحات.

---

### Task F-3/F-4 — إضافة BankTransfer كطريقة دفع

**الملف 1:** `frontend/src/types/purchaseInvoice.types.ts`

غيّر:
```typescript
export type PaymentMethod = 'Cash' | 'Card' | 'Fawry';
```
إلى:
```typescript
export type PaymentMethod = 'Cash' | 'Card' | 'Fawry' | 'BankTransfer';
```

**الملف 2:** `frontend/src/pages/purchase-invoices/AddPaymentModal.tsx` (أو أي modal لإضافة الدفعة)

في الـ array اللي بيعدد طرق الدفع، أضف:
```typescript
{ value: 'BankTransfer', label: 'تحويل بنكي' }
```

---

### Task F-5 — عرض batch/expiry في PurchaseInvoiceDetailsPage

**الملف:** `frontend/src/pages/purchase-invoices/PurchaseInvoiceDetailsPage.tsx`
**الموقع:** جدول المنتجات (الـ table اللي بيعرض items)

**المطلوب:** أضف أعمدة في الجدول:
- "رقم الدُفعة" → `item.batchNumber` (لو موجود، وإلا "—")
- "تاريخ الانتهاء" → `item.expiryDate` مع تنسيق تاريخ (لو موجود، وإلا "—")

لو الجدول ضيق، ممكن تعرضهم كـ sub-row أو tooltip — استخدم الأسلوب الأنسب مع الـ design الموجود.

---

### Task F-7 — إضافة زر حذف الدفعة في PurchaseInvoiceDetailsPage

**الملف:** `frontend/src/pages/purchase-invoices/PurchaseInvoiceDetailsPage.tsx`
**الموقع:** جدول الدفعات

**المطلوب:**

1. في كل صف دفعة، أضف زر "حذف" (icon أو نص) — يظهر بس لو الفاتورة مش `Cancelled`.

2. عند الضغط، اعرض confirmation dialog بسيط:
   ```
   "هل تريد حذف هذه الدفعة؟ سيتم إعادة حساب المبالغ تلقائياً."
   ```

3. عند التأكيد، استدعي `useDeletePaymentMutation` (أو الـ hook المقابل في `purchaseInvoiceApi.ts`).

4. لو الـ mutation مش موجود في الـ API slice، أضفه:
```typescript
deletePayment: builder.mutation<void, { invoiceId: number; paymentId: number }>({
  query: ({ invoiceId, paymentId }) => ({
    url: `/purchase-invoices/${invoiceId}/payments/${paymentId}`,
    method: 'DELETE',
  }),
  invalidatesTags: (result, error, { invoiceId }) => [
    { type: 'PurchaseInvoices', id: invoiceId },
  ],
}),
```

---

### Task F-6 — إضافة Returned/PartiallyReturned في statusLabels

**الملف:** `frontend/src/pages/purchase-invoices/PurchaseInvoicesPage.tsx`
**الموقع:** الـ statusLabels map (السطر 69-75 تقريباً)

**المطلوب:** أضف:
```typescript
Returned: { label: 'مُسترد', color: 'orange' },       // أو اللون المناسب
PartiallyReturned: { label: 'مسترد جزئياً', color: 'yellow' },
```

---

### Task F-9 — إضافة debounce على PrepareAsync

**الملف:** `frontend/src/pages/purchase-invoices/PurchaseInvoiceFormPage.tsx`
**الموقع:** الـ `useEffect` اللي بيستدعي `PrepareAsync` (السطر 96-126 تقريباً)

**المشكلة:** بيستدعي الـ API عند كل تغيير — ممكن يعمل عشرات الـ requests في ثانية واحدة.

**المطلوب:** أضف debounce بـ 500ms:

```typescript
useEffect(() => {
  const timer = setTimeout(() => {
    if (items.length > 0) {
      // استدعاء PrepareAsync هنا
    }
  }, 500);
  
  return () => clearTimeout(timer);
}, [items]); // أو أي dependencies موجودة
```

> استبدل الـ `useEffect` الموجود بالأسلوب ده — لا تكسر الـ dependencies الموجودة.

---

### Task الـ inline recalc في DeletePaymentAsync — توحيد الكود

**الملف:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
**الموقع:** `DeletePaymentAsync`

**المشكلة:** بيعمل status recalculation inline بدل ما يستخدم `RecalculateInvoiceStatus()`.

**المطلوب:** استبدل الـ inline if/else بـ:
```csharp
RecalculateInvoiceStatus(invoice);
```

بالضبط زي `AddPaymentAsync`.

---

## تقرير التنفيذ — مطلوب في النهاية

```
## تقرير إصلاح ميزة فواتير الشراء والموردين

### المرحلة 1 — البيانات المالية (الأعلى أولوية)
- [ ] B-5:  عكس CashRegister عند حذف دفعة نقدية
- [ ] B-1/2/3: تحديث TotalPaid/TotalPurchases/LastPurchaseDate في 4 methods
- [ ] B-11: تأمين Confirm endpoint بـ Admin role
- [ ] A-1/B-9: BranchId filter في SupplierService.GetAllAsync
- [ ] B-6/A-2: BranchId filter في PurchaseInvoiceService.GetAllAsync

### المرحلة 2 — Backend (data integrity)
- [ ] B-4:  تحديث SupplierProduct في ConfirmAsync
- [ ] B-8:  إضافة !IsDeleted check على Product في ConfirmAsync
- [ ] B-7:  إزالة DataAnnotations من AddPaymentRequest
- [ ] B-10: إصلاح N+1 في SupplierDebtsReportService
- [ ] توحيد RecalculateInvoiceStatus في DeletePaymentAsync

### المرحلة 3 — Frontend
- [ ] F-1:  إضافة fields المالية لـ SupplierDto + supplier.types.ts
- [ ] F-2:  عرض المعلومات المالية في SuppliersPage
- [ ] F-3/4: إضافة BankTransfer كطريقة دفع
- [ ] F-5:  عرض batch/expiry في PurchaseInvoiceDetailsPage
- [ ] F-7:  إضافة زر حذف الدفعة
- [ ] F-6:  إضافة Returned/PartiallyReturned في statusLabels
- [ ] F-9:  إضافة debounce على PrepareAsync

### ملفات تم تعديلها:
(اذكر كل ملف اتعدّل)

### مشاكل واجهتها:
(أي حاجة مش موجودة أو اضطريت تختار بديل)

### حاجات محتاج تأكيد من المطوّر:
(أي قرار معماري أو سلوك مش واضح)
```
