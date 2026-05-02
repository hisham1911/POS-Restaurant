# Customers & Balances — Implementation Plan
> **Status:** Ready for execution  
> **Stack:** React 18 · TypeScript · RTK Query · TailwindCSS · ASP.NET Core  
> **Decisions locked — do not revisit without explicit approval**

---

## ✅ Locked Decisions

| # | Decision | Value |
|---|----------|-------|
| 1 | RedeemLoyaltyPoints authorization | `HasPermission(CustomersManage)` — لا permission = ثغرة أمنية |
| 2 | AddLoyaltyPoints authorization | `HasPermission(CustomersManage)` — بدل `Authorize(Roles)` |
| 3 | تسديد الدين في الخزينة | نوع جديد `DebtPayment` — مش `Sale` |
| 4 | عرض الدين في الـ UI | per-branch `AmountDue` + tenant-wide `TotalDue` الاتنين |
| 5 | RowVersion في UpdateCustomer | يتضاف ويتستخدم في optimistic concurrency |
| 6 | BankTransfer في DebtPaymentModal | يتضاف — موجود في Backend ومش موجود في UI |

---

## 🚫 Invariants — Never Break These

```
1. RedeemLoyaltyPoints و AddLoyaltyPoints → HasPermission(CustomersManage) فقط — لا Roles
2. تسديد الدين نقداً → CashRegisterTransactionType.DebtPayment — مش Sale
3. UpdateCustomer → يتحقق من RowVersion — يرفض لو اتغير بـ 409 Conflict
4. PayDebt → وردية مفتوحة مطلوبة دائماً — لا تسديد بدون شيفت
5. حذف عميل → يُرفض لو TotalDue > 0 أو عنده طلبات مفتوحة
6. CreditLimit يُتحقق منه per-branch — مش tenant-wide
7. كل العمليات المالية (PayDebt, UpdateCreditBalance) داخل DB Transaction واحدة
8. Tenant Isolation — لا عميل يظهر لتينانت تاني
```

---

## الـ Workflows المرجعية

### تسديد الدين — بعد التصحيح
```
[DebtPaymentModal] → POST /customers/{id}/pay-debt
    ├── Validate: وردية مفتوحة
    ├── Validate: Amount > 0 && Amount <= BranchBalance.AmountDue
    ├── Validate: Reference مطلوب لغير النقدي
    ├── Transaction starts
    ├── Create DebtPayment (BalanceBefore/After snapshots)
    ├── Customer.TotalDue -= Amount
    ├── CustomerBranchBalance.AmountDue -= Amount
    ├── If Cash → RecordTransactionAsync(DebtPayment, Amount)  ✅ مش Sale
    ├── Commit
    └── Auto-print receipt
```

### عرض الدين في الـ UI — بعد التصحيح
```
CustomerDetailsModal:
    ├── TotalDue (tenant-wide):  "إجمالي الدين على جميع الفروع: 5,000 ج.م"
    └── BranchBalance (per-branch): "دين هذا الفرع: 2,000 ج.م"  ← جديد

DebtPaymentModal:
    └── يعرض BranchBalance — ده اللي الكاشير هيسدده فعلاً
```

---

## Phase 1 — أمان (ابدأ هنا)

### Task 1.1 — إصلاح RedeemLoyaltyPoints (ثغرة أمنية)

```csharp
// backend/KasserPro.API/Controllers/CustomersController.cs

// ❌ قبل: لا يوجد permission check
[HttpPost("{id}/loyalty/redeem")]
public async Task<IActionResult> RedeemLoyaltyPoints(int id, ...)

// ✅ بعد:
[HttpPost("{id}/loyalty/redeem")]
[HasPermission(Permission.CustomersManage)]
public async Task<IActionResult> RedeemLoyaltyPoints(int id, ...)
```

---

### Task 1.2 — إصلاح AddLoyaltyPoints

```csharp
// ❌ قبل:
[HttpPost("{id}/loyalty/add")]
[Authorize(Roles = "Admin,Manager")]
public async Task<IActionResult> AddLoyaltyPoints(int id, ...)

// ✅ بعد:
[HttpPost("{id}/loyalty/add")]
[HasPermission(Permission.CustomersManage)]
public async Task<IActionResult> AddLoyaltyPoints(int id, ...)
```

---

### Task 1.3 — إصلاح PayDebt Controller (B-4)

```csharp
// backend/KasserPro.API/Controllers/CustomersController.cs

// ❌ قبل: غير آمن
var userId = int.Parse(User.FindFirst("userId")!.Value);

// ✅ بعد: نفس pattern باقي Controllers
var userId = User.GetUserId();
```

---

## Phase 2 — تصحيح الخزينة (B-9)

### Task 2.1 — إضافة DebtPayment لـ CashRegisterTransactionType

```csharp
// backend/KasserPro.Domain/Enums/CashRegisterTransactionType.cs
public enum CashRegisterTransactionType
{
    Sale            = 1,
    Refund          = 2,
    Expense         = 3,
    Deposit         = 4,
    Withdrawal      = 5,
    Transfer        = 6,
    Reconciliation  = 7,
    OpeningBalance  = 8,
    SupplierPayment = 9,
    DebtPayment     = 10,  // ← جديد: تسديد دين عميل
}
```

---

### Task 2.2 — تصحيح CustomerService.PayDebtAsync

```csharp
// backend/KasserPro.Application/Services/Implementations/CustomerService.cs
// في PayDebtAsync — غيّر نوع المعاملة

// ❌ قبل:
await _cashRegisterService.RecordTransactionAsync(
    CashRegisterTransactionType.Sale,   // ← غلط
    payment.Amount,
    ...);

// ✅ بعد:
await _cashRegisterService.RecordTransactionAsync(
    CashRegisterTransactionType.DebtPayment,  // ← صح
    payment.Amount,
    description: $"تسديد دين عميل: {customer.Name}",
    referenceType: "DebtPayment",
    referenceId: debtPaymentRecord.Id,
    shiftId: activeShift.Id,
    branchId: request.BranchId);
```

---

## Phase 3 — RowVersion في UpdateCustomer (B-1)

### Task 3.1 — إضافة RowVersion لـ UpdateCustomerRequest

```csharp
// backend/KasserPro.Application/DTOs/Customers/UpdateCustomerRequest.cs

public class UpdateCustomerRequest
{
    public string? Name        { get; set; }
    public string? Email       { get; set; }
    public string? Address     { get; set; }
    public string? Notes       { get; set; }
    public bool?   IsActive    { get; set; }
    public decimal? CreditLimit { get; set; }
    public byte[]  RowVersion  { get; set; } = [];  // ← أضف هذا
}
```

---

### Task 3.2 — استخدام RowVersion في CustomerService.UpdateAsync

```csharp
// backend/KasserPro.Application/Services/Implementations/CustomerService.cs

public async Task<ApiResponse<CustomerDto>> UpdateAsync(int id, UpdateCustomerRequest request)
{
    var customer = await _unitOfWork.Customers.GetByIdAsync(id);

    if (customer == null)
        return ApiResponse<CustomerDto>.Fail("CUSTOMER_NOT_FOUND");

    // ✅ تحقق من RowVersion
    if (request.RowVersion?.Length > 0 &&
        !customer.RowVersion.SequenceEqual(request.RowVersion))
    {
        return ApiResponse<CustomerDto>.Fail("CONCURRENCY_CONFLICT",
            "تم تعديل بيانات العميل من مكان آخر. أعد تحميل الصفحة وحاول مرة أخرى.");
    }

    // ... باقي الـ update logic
}
```

---

### Task 3.3 — تصحيح Frontend CustomerFormModal

```typescript
// src/components/customers/CustomerFormModal.tsx
// RowVersion موجود في customer.types.ts — تأكد أنه يُرسل في الـ request

const onSubmit = async (data: FormData) => {
  if (customer) {
    // تعديل — أرسل RowVersion
    await updateCustomer({
      id: customer.id,
      ...data,
      rowVersion: customer.rowVersion,  // ✅ تأكد إنه موجود
    }).unwrap();
  } else {
    await createCustomer(data).unwrap();
  }
};
```

**أضف error handling للـ concurrency:**
```typescript
} catch (err) {
  const error = err as { data: { errorCode: string } };
  switch (error.data?.errorCode) {
    case 'CONCURRENCY_CONFLICT':
      toast.error('تم تعديل بيانات العميل من مكان آخر — أعد تحميل الصفحة');
      break;
    // ... باقي الأخطاء
  }
}
```

---

## Phase 4 — إضافة BranchBalance للـ UI (A-1 / F-1 / F-4)

### Task 4.1 — إضافة BranchBalance لـ CustomerDto

```csharp
// backend/KasserPro.Application/DTOs/Customers/CustomerDto.cs

public class CustomerDto
{
    // ... existing fields ...

    public decimal TotalDue      { get; set; }  // tenant-wide — موجود
    public decimal BranchAmountDue { get; set; } // per-branch ← جديد
}
```

**في CustomerService.GetByIdAsync — أضف الـ BranchBalance:**
```csharp
// احسب BranchBalance للفرع الحالي
var branchBalance = await _unitOfWork.CustomerBranchBalances
    .FirstOrDefaultAsync(b =>
        b.CustomerId == id &&
        b.BranchId == currentUserBranchId);

dto.BranchAmountDue = branchBalance?.AmountDue ?? 0m;
```

---

### Task 4.2 — تحديث customer.types.ts

```typescript
// src/types/customer.types.ts

export interface CustomerDto {
  // ... existing fields ...
  totalDue: number;        // tenant-wide — موجود
  branchAmountDue: number; // per-branch ← جديد
}
```

---

### Task 4.3 — تحديث CustomerDetailsModal

```typescript
// src/components/customers/CustomerDetailsModal.tsx
// أضف عرض BranchBalance بجانب TotalDue

<div className="grid grid-cols-2 gap-3">

  {/* دين الفرع — الأهم للكاشير */}
  <div className="bg-danger-50 rounded-xl p-3">
    <p className="text-xs text-danger-600 mb-1">دين هذا الفرع</p>
    <p className="text-xl font-bold text-danger-700">
      {customer.branchAmountDue.toLocaleString('ar-EG', { minimumFractionDigits: 2 })} ج.م
    </p>
  </div>

  {/* إجمالي الدين — للمدير */}
  <div className="bg-gray-50 rounded-xl p-3">
    <p className="text-xs text-gray-500 mb-1">إجمالي الدين (كل الفروع)</p>
    <p className="text-xl font-bold text-gray-700">
      {customer.totalDue.toLocaleString('ar-EG', { minimumFractionDigits: 2 })} ج.م
    </p>
  </div>

</div>
```

**إصلاح division by zero (F-7):**
```typescript
// ❌ قبل:
const creditUsagePercent = (customer.totalDue / customer.creditLimit) * 100;

// ✅ بعد:
const creditUsagePercent = customer.creditLimit > 0
  ? Math.min((customer.branchAmountDue / customer.creditLimit) * 100, 100)
  : 0;
```

---

### Task 4.4 — تحديث DebtPaymentModal

```typescript
// src/components/customers/DebtPaymentModal.tsx

// ❌ قبل: يعرض tenant-wide
<p>المبلغ المستحق: {customer.totalDue} ج.م</p>

// ✅ بعد: يعرض per-branch (ده اللي الكاشير هيسدده)
<p>المبلغ المستحق في هذا الفرع: {customer.branchAmountDue} ج.م</p>
<p className="text-xs text-gray-400">
  إجمالي على جميع الفروع: {customer.totalDue} ج.م
</p>
```

**إضافة BankTransfer (F-6):**
```typescript
// ❌ قبل: 3 طرق بس
const paymentMethods = ['Cash', 'Card', 'Fawry'];

// ✅ بعد: 4 طرق متطابقة مع Backend
const paymentMethods = [
  { value: 'Cash',         label: 'نقدي' },
  { value: 'Card',         label: 'بطاقة' },
  { value: 'BankTransfer', label: 'تحويل بنكي' },
  { value: 'Fawry',        label: 'فوري' },
];
```

---

## Phase 5 — إصلاحات P3

### Task 5.1 — توحيد Toast Library

```typescript
// src/components/customers/CustomerQuickCreateModal.tsx

// ❌ قبل:
import toast from 'react-hot-toast';

// ✅ بعد:
import { toast } from 'sonner';
```

---

### Task 5.2 — السماح بـ CreditLimit عند الإنشاء

```typescript
// src/components/customers/CustomerFormModal.tsx
// أضف CreditLimit field في قسم الإنشاء أيضاً (مش بس التعديل)

{/* موجود في التعديل — أضفه في الإنشاء */}
<div>
  <label className="block text-sm font-medium text-gray-700 mb-1">
    حد الائتمان (0 = غير محدود)
  </label>
  <input
    type="number"
    min="0"
    step="1"
    {...register('creditLimit', { valueAsNumber: true })}
    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
    placeholder="0"
  />
</div>
```

**أضف creditLimit لـ CreateCustomerRequest:**
```csharp
// backend/KasserPro.Application/DTOs/Customers/CreateCustomerRequest.cs
public class CreateCustomerRequest
{
    public string  Phone       { get; set; } = "";
    public string? Name        { get; set; }
    public string? Email       { get; set; }
    public string? Address     { get; set; }
    public string? Notes       { get; set; }
    public decimal CreditLimit { get; set; } = 0; // ← أضف
}
```

---

### Task 5.3 — إصلاح GetOrCreateByPhoneAsync (A-3)

```csharp
// backend/KasserPro.Application/Services/Implementations/CustomerService.cs

public async Task<GetOrCreateCustomerResult> GetOrCreateByPhoneAsync(
    string phone, string? name, int tenantId)
{
    var existing = await _unitOfWork.Customers
        .FirstOrDefaultAsync(c =>
            c.Phone == phone &&
            c.TenantId == tenantId &&
            c.IsActive &&          // ✅ أضف هذا الشرط
            !c.IsDeleted);

    if (existing != null)
        return new GetOrCreateCustomerResult { Customer = existing, WasCreated = false };

    // ... create new customer
}
```

---

## Execution Order

```
Step 1  → Task 1.1  إضافة HasPermission لـ RedeemLoyaltyPoints  ← أهم خطوة
Step 2  → Task 1.2  تصحيح AddLoyaltyPoints Authorization
Step 3  → Task 1.3  تصحيح PayDebt userId (GetUserId)
Step 4  → Task 2.1  إضافة DebtPayment لـ CashRegisterTransactionType enum
Step 5  → Task 2.2  تصحيح PayDebtAsync (Sale → DebtPayment)
Step 6  → Task 3.1  إضافة RowVersion لـ UpdateCustomerRequest
Step 7  → Task 3.2  استخدام RowVersion في CustomerService.UpdateAsync
Step 8  → Task 3.3  تصحيح CustomerFormModal (RowVersion + error handling)
Step 9  → Task 4.1  إضافة BranchAmountDue لـ CustomerDto (Backend)
Step 10 → Task 4.2  تحديث customer.types.ts (Frontend)
Step 11 → Task 4.3  تحديث CustomerDetailsModal (عرض BranchBalance + إصلاح division by zero)
Step 12 → Task 4.4  تحديث DebtPaymentModal (BranchBalance + BankTransfer)
Step 13 → Task 5.1  توحيد Toast في CustomerQuickCreateModal
Step 14 → Task 5.2  إضافة CreditLimit في إنشاء العميل
Step 15 → Task 5.3  إصلاح GetOrCreateByPhoneAsync
```

---

## Pre-Commit Checklist

```bash
cd frontend && npx tsc --noEmit    # 0 errors
cd backend  && dotnet build        # 0 errors, 0 warnings
```

- [ ] `RedeemLoyaltyPoints` عنده `HasPermission(CustomersManage)`
- [ ] `AddLoyaltyPoints` عنده `HasPermission(CustomersManage)` — مش `Authorize(Roles)`
- [ ] `PayDebtAsync` يسجل `CashRegisterTransactionType.DebtPayment` — مش `Sale`
- [ ] `UpdateCustomerRequest` فيها `RowVersion`
- [ ] `CustomerDto` فيها `BranchAmountDue`
- [ ] `DebtPaymentModal` بيعرض `branchAmountDue` — مش `totalDue`
- [ ] Division by zero محلول في progress bar
- [ ] `BankTransfer` موجود في `DebtPaymentModal`
- [ ] Toast من `sonner` في كل ملفات الـ Customers
- [ ] `GetOrCreateByPhoneAsync` بيتحقق من `IsActive`

---

## Out of Scope (لا تبنيها دلوقتي)

- Auto-earn loyalty points عند إتمام الطلب (A-4)
- تقرير دفعات الديون per-branch (A-2)
- عرض العملاء المحذوفين/غير النشطين (B-5)
- InactiveCustomers في تقرير النشاط (B-8)
- توحيد CustomerReportService مع IUnitOfWork (B-7)
```
