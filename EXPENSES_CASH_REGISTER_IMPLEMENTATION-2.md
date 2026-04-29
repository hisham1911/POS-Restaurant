# Expenses & Cash Register — Implementation Plan
> **Status:** Ready for execution  
> **Stack:** React 18 · TypeScript · RTK Query · TailwindCSS · ASP.NET Core  
> **Decisions locked — do not revisit without explicit approval**

---

## ✅ Locked Decisions

| # | Decision | Value |
|---|----------|-------|
| 1 | دفع فاتورة المورّد → من يسجل في الخزينة؟ | **نقدي بس** — البنكي وفودافون كاش لا يأثروا على الخزينة |
| 2 | من يعمل Approve للمصروف؟ | **الأدمن بس** — عبر `HasPermission(ExpensesApprove)` |
| 3 | Transfer بين الفروع | **Backend مكتمل** — المطلوب: UI فقط + تصحيح Authorization |
| 4 | Reconcile | **Backend مكتمل** — المطلوب: UI فقط + تصحيح Authorization |

---

## 🚫 Invariants — Never Break These

```
1. دفع فاتورة المورّد نقداً MUST يسجل CashRegisterTransaction من نوع SupplierPayment
2. البنكي وفودافون كاش لا يلمسوا الخزينة — الخزينة = الدرج النقدي فقط
3. Transfer يخصم من المصدر ويضيف للهدف في نفس DB Transaction واحدة — لا تفصلهم
4. Approve/Reject/Pay للمصروف → HasPermission(ExpensesApprove) — ليس Role
5. Transfer/Reconcile → HasPermission(CashRegisterTransfer/CashRegisterReconcile) — ليس Role
6. كل عملية مالية MUST تحمل X-Idempotency-Key (frontend)
7. Tenant Isolation: Transfer يتحقق إن الفرعين ينتميان لنفس التينانت
8. الـ Backend هو المرجع — Frontend guards للـ UX فقط
```

---

## الـ Cash Register Workflow — المرجع الكامل

```
صباحاً:
  [فتح الشيفت] → يُسجل OpeningBalance

طول اليوم — كل عملية تُسجل CashRegisterTransaction:
  ┌─────────────────────────────────────────────────────┐
  │  العملية               النوع            الأثر       │
  ├─────────────────────────────────────────────────────┤
  │  بيع نقدي              Sale             + رصيد      │
  │  رد بضاعة نقدي         Refund           - رصيد      │
  │  مصروف معتمد ومدفوع   Expense          - رصيد      │
  │  دفع فاتورة نقدي ✅    SupplierPayment  - رصيد      │
  │  دفع فاتورة بنكي ❌    (لا يُسجل)       بدون أثر   │
  │  إيداع يدوي            Deposit          + رصيد      │
  │  سحب يدوي              Withdrawal       - رصيد      │
  │  تحويل صادر            TransferOut      - رصيد      │
  │  تحويل وارد            TransferIn       + رصيد      │
  └─────────────────────────────────────────────────────┘

آخر اليوم:
  [Reconcile / إغلاق الشيفت]
    → المتوقع (حسابي) vs الفعلي (عدّ الكاشير)
    → الفرق يُسجل كـ ReconciliationDifference
    → يُقفل اليوم ✅
```

---

## Phase 1 — تصحيح الأمان (ابدأ هنا)

### Task 1.1 — تصحيح Authorization في ExpensesController

**المشكلة:** Approve/Reject/Pay يستخدموا `[Authorize(Roles="Admin")]`

```csharp
// backend/KasserPro.API/Controllers/ExpensesController.cs

// ❌ قبل:
[HttpPut("{id}/approve")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Approve(int id) { ... }

[HttpPut("{id}/reject")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Reject(int id, ...) { ... }

[HttpPut("{id}/pay")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Pay(int id, ...) { ... }

// ✅ بعد:
[HttpPut("{id}/approve")]
[HasPermission(Permission.ExpensesApprove)]
public async Task<IActionResult> Approve(int id) { ... }

[HttpPut("{id}/reject")]
[HasPermission(Permission.ExpensesApprove)]
public async Task<IActionResult> Reject(int id, ...) { ... }

[HttpPut("{id}/pay")]
[HasPermission(Permission.ExpensesApprove)]
public async Task<IActionResult> Pay(int id, ...) { ... }
```

---

### Task 1.2 — تصحيح Authorization في CashRegisterController

**المشكلة:** Transfer/Reconcile يستخدموا `[Authorize(Roles="Admin")]`

```csharp
// backend/KasserPro.API/Controllers/CashRegisterController.cs

// ❌ قبل:
[HttpPost("transfer")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Transfer(...) { ... }

[HttpPost("reconcile")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Reconcile(...) { ... }

// ✅ بعد:
[HttpPost("transfer")]
[HasPermission(Permission.CashRegisterTransfer)]
public async Task<IActionResult> Transfer(...) { ... }

[HttpPost("reconcile")]
[HasPermission(Permission.CashRegisterReconcile)]
public async Task<IActionResult> Reconcile(...) { ... }
```

---

### Task 1.3 — إضافة Permissions الجديدة في الـ Enum

```csharp
// backend/KasserPro.Domain/Enums/Permission.cs
// أضف الـ permissions الجديدة:

public enum Permission
{
    // ... existing ...
    
    // Expenses
    ExpensesManage  = 702,
    ExpensesApprove = 703,   // ← جديد: Approve/Reject/Pay للمصروف
    
    // Cash Register
    CashRegisterManage      = 1001,
    CashRegisterTransfer    = 1002,   // ← جديد: تحويل بين الفروع
    CashRegisterReconcile   = 1003,   // ← جديد: إغلاق الشيفت
}
```

**أضف الـ labels العربية في الـ Permission seed/config:**
```csharp
{ Permission.ExpensesApprove,       "اعتماد المصروفات",          true  },
{ Permission.CashRegisterTransfer,  "تحويل نقدي بين الفروع",     true  },
{ Permission.CashRegisterReconcile, "مطابقة وإغلاق الشيفت",      true  },
```

---

## Phase 2 — تسجيل دفعات الموردين في الخزينة (B-3)

### Task 2.1 — تعديل PurchaseInvoiceService

**الملف:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`

**في دالة `AddPaymentAsync` — بعد حفظ الـ PurchaseInvoicePayment:**

```csharp
public async Task<Result> AddPaymentAsync(AddPurchasePaymentDto dto, string userId)
{
    // ... existing validation and PurchaseInvoicePayment creation ...

    // حفظ الدفعة
    await _unitOfWork.SaveChangesAsync();

    // ✅ جديد: سجّل في الخزينة لو الدفع نقدي فقط
    if (dto.PaymentMethod == PaymentMethod.Cash)
    {
        var cashResult = await _cashRegisterService.RecordTransactionAsync(
            new RecordTransactionRequest
            {
                BranchId        = dto.BranchId,
                Type            = CashRegisterTransactionType.SupplierPayment,
                Amount          = dto.Amount,
                ReferenceId     = payment.Id,          // PurchaseInvoicePayment.Id
                ReferenceType   = "PurchaseInvoicePayment",
                Description     = $"دفع فاتورة مورّد: {invoice.SupplierName}",
                TransactionDate = dto.PaymentDate,
                CreatedByUserId = userId,
            });

        // لو فشل تسجيل الخزينة — أرجع خطأ
        // الدفعة اتحفظت في نفس DB transaction — لو فشلنا نعمل rollback
        if (!cashResult.IsSuccess)
            return Result.Fail("CASH_REGISTER_RECORD_FAILED");
    }
    // البنكي وفودافون كاش: لا يتسجلوا في الخزينة — مقصود

    return Result.Ok();
}
```

> ⚠️ **مهم:** الـ `RecordTransactionAsync` والـ `SaveChangesAsync` لازم يكونوا في نفس الـ DB Transaction. لو مش كده — ابدأ `BeginTransactionAsync` قبلهم واعمل `CommitTransactionAsync` بعدهم.

---

### Task 2.2 — تأكد إن CashRegisterTransactionType فيه SupplierPayment

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
    SupplierPayment = 9,   // ← تأكد إنه موجود، لو لا أضفه
}
```

---

## Phase 3 — UI المفقود (Frontend)

### Task 3.1 — Types

```typescript
// src/types/cashRegister.types.ts — أضف هذه الـ types

export interface TransferCashDto {
  sourceBranchId: number;
  targetBranchId: number;
  amount: number;
  description?: string;
  transactionDate: string; // ISO date
}

export interface ReconcileDto {
  branchId: number;
  actualCashAmount: number;
  notes?: string;
}

export interface ReconcileResultDto {
  expectedAmount: number;
  actualAmount: number;
  difference: number;       // موجب = زيادة، سالب = عجز
  transactionId: number;
}
```

---

### Task 3.2 — RTK Query Endpoints

```typescript
// src/api/cashRegisterApi.ts — أضف endpoints جديدة

transferCash: builder.mutation<ApiResponse<void>, TransferCashDto>({
  query: (dto) => ({
    url: '/cash-register/transfer',
    method: 'POST',
    body: dto,
    headers: { 'X-Idempotency-Key': crypto.randomUUID() }, // ✅ مالي
  }),
  invalidatesTags: ['CashRegister'],
}),

reconcile: builder.mutation<ApiResponse<ReconcileResultDto>, ReconcileDto>({
  query: (dto) => ({
    url: '/cash-register/reconcile',
    method: 'POST',
    body: dto,
    headers: { 'X-Idempotency-Key': crypto.randomUUID() }, // ✅ مالي
  }),
  invalidatesTags: ['CashRegister', 'Shifts'],
}),
```

**أضف للـ `baseApi.tagTypes`:**
```typescript
tagTypes: [
  // ...existing...
  'CashRegister',
]
```

---

### Task 3.3 — TransferCashModal

```typescript
// src/components/cashRegister/TransferCashModal.tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';
import { useTransferCashMutation } from '@/api/cashRegisterApi';
import { useGetBranchesQuery } from '@/api/branchesApi';

const schema = z.object({
  sourceBranchId: z.number().min(1, 'اختر الفرع المرسِل'),
  targetBranchId: z.number().min(1, 'اختر الفرع المستقبِل'),
  amount: z.number().min(1, 'المبلغ يجب أن يكون أكبر من صفر'),
  description: z.string().optional(),
  transactionDate: z.string().min(1, 'التاريخ مطلوب'),
}).refine(d => d.sourceBranchId !== d.targetBranchId, {
  message: 'الفرع المرسِل والمستقبِل لا يمكن أن يكونا نفس الفرع',
  path: ['targetBranchId'],
});

type FormData = z.infer<typeof schema>;

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const TransferCashModal = ({ isOpen, onClose }: Props) => {
  const { data: branchesData } = useGetBranchesQuery();
  const [transfer, { isLoading }] = useTransferCashMutation();
  const branches = branchesData?.data ?? [];

  const { register, handleSubmit, formState: { errors }, reset, setError } =
    useForm<FormData>({
      resolver: zodResolver(schema),
      defaultValues: { transactionDate: new Date().toISOString().split('T')[0] },
    });

  const onSubmit = async (data: FormData) => {
    try {
      await transfer(data).unwrap();
      toast.success('تم تحويل النقد بنجاح');
      reset();
      onClose();
    } catch (err) {
      const error = err as { data: { errorCode: string; message: string } };
      switch (error.data?.errorCode) {
        case 'INSUFFICIENT_BALANCE':
          setError('amount', { message: 'رصيد الفرع المرسِل غير كافٍ' });
          break;
        case 'BRANCH_NOT_FOUND':
          toast.error('أحد الفروع غير موجود');
          break;
        case 'TENANT_ISOLATION_VIOLATION':
          toast.error('لا يمكن التحويل بين فروع مختلفة');
          break;
        default:
          toast.error(error.data?.message ?? 'حدث خطأ أثناء التحويل');
      }
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-bold text-gray-800 mb-6">تحويل نقدي بين الفروع</h2>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">الفرع المرسِل</label>
            <select
              {...register('sourceBranchId', { valueAsNumber: true })}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            >
              <option value={0}>اختر فرع</option>
              {branches.map(b => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
            {errors.sourceBranchId && (
              <p className="text-danger-600 text-xs mt-1">{errors.sourceBranchId.message}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">الفرع المستقبِل</label>
            <select
              {...register('targetBranchId', { valueAsNumber: true })}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            >
              <option value={0}>اختر فرع</option>
              {branches.map(b => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
            {errors.targetBranchId && (
              <p className="text-danger-600 text-xs mt-1">{errors.targetBranchId.message}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">المبلغ (جنيه)</label>
            <input
              type="number"
              step="0.01"
              min="0"
              {...register('amount', { valueAsNumber: true })}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              placeholder="0.00"
            />
            {errors.amount && (
              <p className="text-danger-600 text-xs mt-1">{errors.amount.message}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">التاريخ</label>
            <input
              type="date"
              {...register('transactionDate')}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">ملاحظة (اختياري)</label>
            <input
              type="text"
              {...register('description')}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              placeholder="سبب التحويل..."
            />
          </div>

          <div className="flex gap-3 mt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-2 text-sm text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="flex-1 px-4 py-2 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 rounded-lg transition-colors disabled:opacity-60"
            >
              {isLoading ? 'جاري التحويل...' : 'تأكيد التحويل'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
```

---

### Task 3.4 — ReconcileModal

```typescript
// src/components/cashRegister/ReconcileModal.tsx
import { useState } from 'react';
import { toast } from 'sonner';
import { useReconcileMutation } from '@/api/cashRegisterApi';
import type { ReconcileResultDto } from '@/types/cashRegister.types';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  branchId: number;
  expectedAmount: number; // من الـ Dashboard
}

export const ReconcileModal = ({ isOpen, onClose, branchId, expectedAmount }: Props) => {
  const [actualAmount, setActualAmount] = useState<string>('');
  const [notes, setNotes] = useState('');
  const [result, setResult] = useState<ReconcileResultDto | null>(null);
  const [reconcile, { isLoading }] = useReconcileMutation();

  const actual = parseFloat(actualAmount) || 0;
  const difference = actual - expectedAmount;

  const handleReconcile = async () => {
    if (!actualAmount) {
      toast.error('أدخل المبلغ الفعلي في الدرج');
      return;
    }

    try {
      const res = await reconcile({
        branchId,
        actualCashAmount: actual,
        notes: notes || undefined,
      }).unwrap();

      setResult(res.data ?? null);
      toast.success('تم مطابقة الخزينة وإغلاق الشيفت');
    } catch (err) {
      const error = err as { data: { errorCode: string; message: string } };
      switch (error.data?.errorCode) {
        case 'NO_OPEN_SHIFT':
          toast.error('لا يوجد شيفت مفتوح');
          break;
        default:
          toast.error(error.data?.message ?? 'حدث خطأ');
      }
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-bold text-gray-800 mb-6">مطابقة الخزينة وإغلاق الشيفت</h2>

        {!result ? (
          <div className="flex flex-col gap-4">
            {/* المبلغ المتوقع */}
            <div className="bg-gray-50 rounded-xl p-4">
              <p className="text-sm text-gray-500 mb-1">الرصيد المتوقع (حسابي)</p>
              <p className="text-2xl font-bold text-gray-800">
                {expectedAmount.toLocaleString('ar-EG', { minimumFractionDigits: 2 })} ج.م
              </p>
            </div>

            {/* المبلغ الفعلي */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                المبلغ الفعلي في الدرج
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                value={actualAmount}
                onChange={e => setActualAmount(e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-lg font-semibold"
                placeholder="0.00"
                autoFocus
              />
            </div>

            {/* معاينة الفرق */}
            {actualAmount && (
              <div className={`rounded-xl p-3 text-sm font-medium ${
                difference === 0
                  ? 'bg-success-50 text-success-700'
                  : difference > 0
                    ? 'bg-warning-50 text-warning-700'
                    : 'bg-danger-50 text-danger-700'
              }`}>
                {difference === 0 && '✅ لا يوجد فرق — الخزينة متطابقة'}
                {difference > 0 && `⚠️ زيادة: +${difference.toFixed(2)} ج.م`}
                {difference < 0 && `❌ عجز: ${difference.toFixed(2)} ج.م`}
              </div>
            )}

            {/* ملاحظات */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">ملاحظة (اختياري)</label>
              <textarea
                value={notes}
                onChange={e => setNotes(e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none"
                rows={2}
                placeholder="سبب الفرق إن وجد..."
              />
            </div>

            <div className="flex gap-3">
              <button
                onClick={onClose}
                className="flex-1 px-4 py-2 text-sm text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg"
              >
                إلغاء
              </button>
              <button
                onClick={handleReconcile}
                disabled={isLoading || !actualAmount}
                className="flex-1 px-4 py-2 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 rounded-lg disabled:opacity-60"
              >
                {isLoading ? 'جاري الإغلاق...' : 'تأكيد وإغلاق الشيفت'}
              </button>
            </div>
          </div>
        ) : (
          /* نتيجة المطابقة */
          <div className="flex flex-col gap-4 text-center">
            <div className="text-4xl">
              {result.difference === 0 ? '✅' : result.difference > 0 ? '⚠️' : '❌'}
            </div>
            <p className="font-bold text-gray-800">تم إغلاق الشيفت</p>
            <div className="bg-gray-50 rounded-xl p-4 text-start">
              <div className="flex justify-between text-sm mb-2">
                <span className="text-gray-500">المتوقع</span>
                <span className="font-medium">{result.expectedAmount.toFixed(2)} ج.م</span>
              </div>
              <div className="flex justify-between text-sm mb-2">
                <span className="text-gray-500">الفعلي</span>
                <span className="font-medium">{result.actualAmount.toFixed(2)} ج.م</span>
              </div>
              <div className={`flex justify-between text-sm font-bold ${
                result.difference === 0 ? 'text-success-700' :
                result.difference > 0 ? 'text-warning-700' : 'text-danger-700'
              }`}>
                <span>الفرق</span>
                <span>{result.difference >= 0 ? '+' : ''}{result.difference.toFixed(2)} ج.م</span>
              </div>
            </div>
            <button
              onClick={onClose}
              className="w-full px-4 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg"
            >
              إغلاق
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
```

---

### Task 3.5 — ربط الـ Modals بـ CashRegisterDashboard

```typescript
// src/pages/cashRegister/CashRegisterDashboard.tsx
// أضف state للـ modals وزرين

import { useState } from 'react';
import { usePermission } from '@/hooks/usePermission';
import { TransferCashModal } from '@/components/cashRegister/TransferCashModal';
import { ReconcileModal } from '@/components/cashRegister/ReconcileModal';

// داخل الـ component:
const { hasPermission } = usePermission();
const [showTransfer, setShowTransfer] = useState(false);
const [showReconcile, setShowReconcile] = useState(false);

// في الـ JSX — أضف الزرين بجانب بعض في الـ header:
{hasPermission('CashRegisterTransfer') && (
  <button
    onClick={() => setShowTransfer(true)}
    className="px-4 py-2 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 rounded-lg"
  >
    تحويل نقدي
  </button>
)}

{hasPermission('CashRegisterReconcile') && (
  <button
    onClick={() => setShowReconcile(true)}
    className="px-4 py-2 text-sm font-medium text-white bg-success-600 hover:bg-success-700 rounded-lg"
  >
    إغلاق الشيفت
  </button>
)}

{/* الـ Modals */}
<TransferCashModal
  isOpen={showTransfer}
  onClose={() => setShowTransfer(false)}
/>

<ReconcileModal
  isOpen={showReconcile}
  onClose={() => setShowReconcile(false)}
  branchId={currentBranch.id}
  expectedAmount={dashboardData?.currentBalance ?? 0}
/>
```

---

### Task 3.6 — تصحيح shiftId في TransactionsPage

```typescript
// src/pages/cashRegister/CashRegisterTransactionsPage.tsx

// ❌ قبل: input number يدوي
<input type="number" value={shiftId} onChange={...} />

// ✅ بعد: dropdown من الشيفتات الفعلية
import { useGetShiftsQuery } from '@/api/shiftsApi';

const { data: shiftsData } = useGetShiftsQuery({ branchId: currentBranch.id });
const shifts = shiftsData?.data ?? [];

<select
  value={selectedShiftId ?? ''}
  onChange={e => setSelectedShiftId(Number(e.target.value) || null)}
  className="border border-gray-300 rounded-lg px-3 py-2 text-sm"
>
  <option value="">كل الشيفتات</option>
  {shifts.map(shift => (
    <option key={shift.id} value={shift.id}>
      {new Date(shift.openedAt).toLocaleDateString('ar-EG')} — {shift.openedByName}
    </option>
  ))}
</select>
```

---

## Execution Order

```
Step 1  → Task 1.1  تصحيح Authorize في ExpensesController
Step 2  → Task 1.2  تصحيح Authorize في CashRegisterController
Step 3  → Task 1.3  إضافة Permissions الجديدة في الـ Enum + Seed
Step 4  → Task 2.2  التأكد من SupplierPayment في TransactionType enum
Step 5  → Task 2.1  تعديل PurchaseInvoiceService لتسجيل الدفع النقدي
Step 6  → Task 3.1  إضافة TypeScript types الجديدة
Step 7  → Task 3.2  إضافة RTK Query endpoints (Transfer + Reconcile)
Step 8  → Task 3.3  TransferCashModal
Step 9  → Task 3.4  ReconcileModal
Step 10 → Task 3.5  ربط الـ Modals بالـ Dashboard
Step 11 → Task 3.6  تصحيح shiftId dropdown
```

---

## Pre-Commit Checklist

```bash
cd frontend && npx tsc --noEmit    # 0 errors required
cd backend  && dotnet build        # 0 errors, 0 warnings required
```

- [ ] `[Authorize(Roles="Admin")]` مش موجود في ExpensesController و CashRegisterController
- [ ] `HasPermission` موجود على Approve/Reject/Pay/Transfer/Reconcile
- [ ] `SupplierPayment` موجود في الـ Enum
- [ ] PurchaseInvoiceService يتحقق من `PaymentMethod.Cash` قبل الـ RecordTransaction
- [ ] Transfer و Reconcile RTK mutations عندهم `X-Idempotency-Key`
- [ ] `baseApi.tagTypes` يحتوي على `'CashRegister'`
- [ ] الـ Modals بتظهر بس للـ users اللي عندهم الـ Permission الصح
- [ ] RTL-aware Tailwind classes مستخدمة (`ms-*`, `text-start`)
- [ ] Error handling يستخدم `errorCode` مش `message`
- [ ] Toasts من `sonner`

---

## Out of Scope (Phase 2 — لا تبنيها دلوقتي)

- صفحة ExpenseCategories (CRUD)
- Dashboard مالي موحد (Financial Overview)
- TransferReferenceId link في تفاصيل المعاملة
- تقرير Audit للمصروفات
```
