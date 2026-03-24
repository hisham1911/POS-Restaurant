# 🚨 تحليل شامل: ميزة الدفع الآجل (البيع الآجل) - غير مكتملة

**التاريخ:** 4 مارس 2026  
**الحالة:** ⚠️ **نصف مكتملة** (جزء الإنشاء يعمل، جزء التسديد مفقود بالكامل)

---

## 📋 الملخص التنفيذي

الميزة الحالية تسمح للعملاء بـ**الشراء الآجل** (دفع جزئي وتراكم دين)، لكن **لا توجد آلية لتسديد هذا الدين**. بمجرد تراكم رصيد `TotalDue` على العميل:

- ❌ لا يمكن للعميل أو الموظف تسديد الدين
- ❌ لا يمكن تسجيل تسديدات منفصلة
- ❌ الاسترجاع والإلغاء لا يُنقصان الدين
- ❌ لا توجد تقارير للديون المستحقة

---

## ✅ ما هو مُنفّذ (يعمل بنجاح)

### 1. DATABASE LAYER

**الملف:** [backend/KasserPro.Domain/Entities/Customer.cs](backend/KasserPro.Domain/Entities/Customer.cs)

| الحقل         | النوع     | الوصف                          | القيمة الافتراضية |
| ------------- | --------- | ------------------------------ | ----------------- |
| `TotalDue`    | `decimal` | إجمالي الدين المستحق من العميل | 0                 |
| `CreditLimit` | `decimal` | الحد الأقصى للائتمان المسموح   | 0 (غير محدود)     |

**Migration:** [20260209114810_AddCustomerCreditTracking.cs](backend/KasserPro.Infrastructure/Migrations/20260209114810_AddCustomerCreditTracking.cs)

**جدول Order:**

- `AmountPaid` (decimal) - المبلغ المدفوع فعلاً
- `AmountDue` (decimal) - المبلغ المستحق من هذا الطلب فقط
- `ChangeAmount` (decimal) - المبلغ الزائد (الفكة)

---

### 2. BACKEND - VALIDATION LAYER

**الملف:** [backend/KasserPro.Application/Services/Implementations/OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs#L484-L560)

#### دالة `CompleteAsync` - معالجة الدفع الآجل

```csharp
// سطر 524: التحقق من حد الائتمان
if (numericAmount < order.Total)
{
    // دفع جزئي - يتطلب عميل
    if (!order.CustomerId.HasValue)
        return ApiResponse<OrderDto>.Fail("البيع الآجل يتطلب ربط عميل بالطلب");

    // التحقق من حد الائتمان
    var isValid = await _customerService.ValidateCreditLimitAsync(
        order.CustomerId.Value,
        amountDue
    );

    if (!isValid)
        return ApiResponse<OrderDto>.Fail(
            $"تجاوز حد الائتمان. الحد المسموح: {customer?.CreditLimit:F2} ج.م"
        );
}

// سطر 548-551: تحديث Customer.TotalDue
if (order.AmountDue > 0 && order.CustomerId.HasValue)
{
    await _customerService.UpdateCreditBalanceAsync(
        order.CustomerId.Value,
        order.AmountDue
    );
}
```

#### دالة `ValidateCreditLimitAsync`

**الملف:** [backend/KasserPro.Application/Services/Implementations/CustomerService.cs](backend/KasserPro.Application/Services/Implementations/CustomerService.cs#L226-L242)

```csharp
public async Task<bool> ValidateCreditLimitAsync(int customerId, decimal additionalAmount)
{
    var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
    if (customer == null) return false;

    // 0 = بدون حد
    if (customer.CreditLimit == 0)
        return true;

    var newTotalDue = customer.TotalDue + additionalAmount;
    return newTotalDue <= customer.CreditLimit;
}
```

#### دالة `UpdateCreditBalanceAsync`

**الملف:** [backend/KasserPro.Application/Services/Implementations/CustomerService.cs](backend/KasserPro.Application/Services/Implementations/CustomerService.cs#L210-L224)

```csharp
public async Task UpdateCreditBalanceAsync(int customerId, decimal amountDue)
{
    var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
    if (customer == null) return;

    // إضافة إلى الدين (يزيد فقط)
    customer.TotalDue += amountDue;
    await _unitOfWork.SaveChangesAsync();
}
```

---

### 3. BACKEND - DTO LAYER

**الملف:** [backend/KasserPro.Application/DTOs/Customers/CustomerDto.cs](backend/KasserPro.Application/DTOs/Customers/CustomerDto.cs)

```csharp
public class CustomerDto
{
    // ... other fields
    public decimal TotalDue { get; set; }        // عرض الدين
    public decimal CreditLimit { get; set; }     // عرض حد الائتمان
}

public class UpdateCustomerRequest
{
    // ... other fields
    public decimal? CreditLimit { get; set; }     // تعديل حد الائتمان
    // ← لا يوجد TotalDue (صحيح - يجب أن تتحكم فيه النظام فقط)
}
```

---

### 4. FRONTEND - POS PAYMENT FLOW

#### الملف: [frontend/src/pages/pos/POSWorkspacePage.tsx](frontend/src/pages/pos/POSWorkspacePage.tsx)

**سطر 993-1009: Checkbox الدفع الآجل**

```tsx
{
  canTakeCredit && (
    <label className="flex items-center space-x-2">
      <input
        type="checkbox"
        checked={allowPartialPayment}
        onChange={(e) => setAllowPartialPayment(e.target.checked)}
        className="w-4 h-4"
      />
      <span className="text-sm">السماح بالدفع الجزئي (بيع آجل)</span>
      <span className="text-xs text-gray-500">
        يمكن للعميل دفع جزء من المبلغ والباقي يُسجل كدين
      </span>
    </label>
  );
}
```

**سطر 238-245: التحقق من حد الائتمان**

```tsx
if (selectedCustomer && selectedCustomer.creditLimit > 0) {
  const creditLimitExceeded =
    selectedCustomer.totalDue + amountDue > selectedCustomer.creditLimit;

  if (numericAmount < total && creditLimitExceeded) {
    toast.error(
      `تجاوز حد الائتمان المسموح (${formatCurrency(selectedCustomer.creditLimit)})`,
    );
    return;
  }
}
```

#### الملف: [frontend/src/components/pos/PaymentModal.tsx](frontend/src/components/pos/PaymentModal.tsx)

**سطر 192-204: عرض بيانات الائتمان**

```tsx
{
  selectedCustomer && (
    <div className="bg-blue-50 p-3 rounded space-y-2">
      <div className="flex justify-between text-sm">
        <span>رصيد مستحق:</span>
        <span className="text-orange-600 font-semibold">
          {formatCurrency(selectedCustomer.totalDue)}
        </span>
      </div>
      {selectedCustomer.creditLimit > 0 && (
        <div className="flex justify-between text-sm">
          <span>حد الائتمان:</span>
          <span>{formatCurrency(selectedCustomer.creditLimit)}</span>
        </div>
      )}
    </div>
  );
}
```

---

### 5. FRONTEND - CUSTOMER DISPLAYS

#### الملف: [frontend/src/pages/customers/CustomersPage.tsx](frontend/src/pages/customers/CustomersPage.tsx)

**سطر 267-275: جدول مع عمود الدين**

```tsx
<td className="px-6 py-4 text-right">
  {customer.totalDue > 0 && (
    <div className="text-orange-600 font-semibold">
      {formatCurrency(customer.totalDue)}
    </div>
  )}
  {customer.creditLimit > 0 && (
    <div className="text-xs text-gray-500">
      حد: {formatCurrency(customer.creditLimit)}
    </div>
  )}
</td>
```

**سطر 129-135: إجمالي الديون**

```tsx
<div className="text-right">
  <p className="text-2xl font-bold text-orange-600">
    {formatCurrency(customers.reduce((sum, c) => sum + (c.totalDue || 0), 0))}
  </p>
  <p className="text-sm text-gray-600">إجمالي المستحق</p>
</div>
```

#### الملف: [frontend/src/components/customers/CustomerDetailsModal.tsx](frontend/src/components/customers/CustomerDetailsModal.tsx)

**سطر 458-510: شريط تقدم الائتمان**

```tsx
{
  customer.creditLimit > 0 && (
    <div className="space-y-2">
      <div className="flex justify-between text-sm">
        <span>المستحق:</span>
        <span className="font-semibold">
          {formatCurrency(customer.totalDue)}
        </span>
      </div>
      <div className="flex justify-between text-sm">
        <span>المتاح:</span>
        <span className="text-green-600 font-semibold">
          {formatCurrency(customer.creditLimit - customer.totalDue)}
        </span>
      </div>
      <div className="w-full bg-gray-200 rounded h-2">
        <div
          className="bg-orange-500 h-2 rounded"
          style={{
            width: `${(customer.totalDue / customer.creditLimit) * 100}%`,
          }}
        />
      </div>
    </div>
  );
}
```

#### الملف: [frontend/src/components/customers/CustomerFormModal.tsx](frontend/src/components/customers/CustomerFormModal.tsx)

**سطر 252-272: حقل حد الائتمان (تعديل فقط)**

```tsx
{
  isEditing && (
    <div>
      <label className="block text-sm font-medium mb-1">
        حد الائتمان (ج.م)
      </label>
      <input
        type="number"
        value={formData.creditLimit}
        onChange={(e) =>
          setFormData({ ...formData, creditLimit: e.target.value })
        }
        placeholder="0 = بدون حد"
        className="w-full px-3 py-2 border rounded-lg"
      />
    </div>
  );
}
```

---

### 6. CUSTOMER TYPES DEFINITION

**الملف:** [frontend/src/types/customer.types.ts](frontend/src/types/customer.types.ts)

```typescript
export interface Customer {
  id: number;
  name: string;
  phone: string;
  // ... other fields

  // Credit Sales Fields
  totalDue: number; // ✅ موجود في العرض
  creditLimit: number; // ✅ موجود في العرض
}

export interface UpdateCustomerRequest {
  name: string;
  phone: string;
  // ... other fields
  creditLimit?: number; // ✅ يمكن التعديل عليه
  // ⚠️ لا يوجد totalDue (صحيح - للنظام فقط)
}
```

---

## ❌ ما هو مفقود (ثغرات حرجة)

### 🔴 CRITICAL GAPS (يجب الإصلاح فوراً)

#### 1️⃣ لا يوجد API لتسديد الديون

**المشكلة:**

- لا يوجد endpoint مثل `POST /api/customers/{id}/pay-debt`
- `Customer.TotalDue` يزيد فقط، لا يُنقص أبداً
- لا طريقة لقبول تسديد من العميل

**الملفات المفقودة:**

- ❌ `CustomerService.PayDebtAsync()` - لا يوجد
- ❌ `PayDebtRequest` DTO - لا يوجد
- ❌ Endpoint في `CustomersController` - لا يوجد

**التأثير:**

```
عميل اشترى آجل → TotalDue = 500 ج.م → الآن يريد يدفع → ماذا يفعل؟ 🤷
```

---

#### 2️⃣ لا يوجد جدول Audit للتسديدات

**المشكلة:**

- لا يوجد entity `DebtPayment` أو `CustomerPayment`
- لا سجل لمتى ما دفع العميل ولا كم ولا بأي طريقة
- `TotalDue` تتغير بدون أي بصمة audit

**الملفات المفقودة:**

- ❌ `DebtPayment.cs` entity - لا يوجد
- ❌ Migration للجدول - لا يوجد
- ❌ Repository/Service للتسديدات - لا يوجد

**التأثير:**

```
لا توجد إجابة على:
- متى ما دفع؟
- كم دفع؟
- بأي طريقة (نقد/تحويل/شيك)؟
- من استقبل الدفع؟
```

---

#### 3️⃣ لا يوجد UI لتسديد الديون

**المشكلة:**

- لا يوجد زر "تسديد دين" في أي مكان
- لا يوجد modal/form لإدخال مبلغ التسديد
- لا يوجد صفحة لإدارة ديون العملاء

**الملفات المفقودة:**

- ❌ `DebtPaymentModal.tsx` - لا يوجد
- ❌ زر في `CustomerDetailsModal` - لا يوجد
- ❌ صفحة `CustomerDebtsPage.tsx` - لا يوجد

**البحث الذي أُجريت:**

```
grep "تسديد\|سداد\|debt payment\|pay debt" frontend/ → 0 نتائج
```

**التأثير:**

```
الموظف والعميل لا يستطيعان تسديد الديون من الواجهة 🚫
```

---

#### 4️⃣ الاسترجاع (Refund) لا يُنقص الدين

**المشكلة:**
في [OrderService.RefundAsync (سطر 685)](backend/KasserPro.Application/Services/Implementations/OrderService.cs#L685):

```csharp
// ❌ المشكلة: عند استرجاع طلب فيه دين
// الدين الأصلي لا يُنقص

// مثال:
// - الطلب = 500 ج.م
// - المدفوع = 300 ج.م
// - الدين = 200 ج.م (تم إضافته إلى Customer.TotalDue)
// - يتم استرجاع الطلب ✅
// - لكن Customer.TotalDue يبقى = 200 ج.م ❌ (يجب ينقص)

// في الكود الحالي:
// 1. ينشئ return order
// 2. يُسجل refund log
// 3. يُسترجع النقد من الصندوق
// 4. لكن لا يلمس Customer.TotalDue ← ❌❌❌
```

**السطور ذات الصلة:**

- سطر 843-884: تحديث الطلب الأصلي (لا يمس `TotalDue`)
- سطر 930-948: تسجيل في cash register (لا يمس `TotalDue`)

**التأثير:**

```
سيناريو خاطئ:
- عميل اشترى آجل: TotalDue = 200
- تم استرجاع البيع ✅
- لكن TotalDue = 200 ❌ (يجب يصير 0)
→ العميل القديم يبقى عليه دين وهمي
```

---

#### 5️⃣ إلغاء الطلب لا يُنقص الدين

**المشكلة:**
في [OrderService.CancelAsync (سطر 661)](backend/KasserPro.Application/Services/Implementations/OrderService.cs#L661):

```csharp
public async Task<ApiResponse<bool>> CancelAsync(int orderId, string? reason)
{
    var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
    if (order == null)
        return ApiResponse<bool>.Fail(ErrorCodes.ORDER_NOT_FOUND, ...);

    var validationResult = ValidateStateTransition(order.Status, OrderStatus.Cancelled);
    if (!validationResult.Success)
        return ApiResponse<bool>.Fail(...);

    order.Status = OrderStatus.Cancelled;
    order.CancelledAt = DateTime.UtcNow;
    order.CancellationReason = reason;

    await _unitOfWork.SaveChangesAsync();
    return ApiResponse<bool>.Ok(true, "تم إلغاء الطلب");

    // ❌ لا يتم التعامل مع Customer.TotalDue
    // إذا كان الطلب المُلغى له AmountDue > 0، يجب نقص Customer.TotalDue
}
```

**التأثير:**

```
سيناريو:
- طلب آجل (Draft) مع دعموزايت سخيفة
- تم إلغاء الطلب قبل الإتمام ✅
- لكن إذا أضيفت أموزايت خلال غلط → TotalDue قد يكون على العميل ❌
```

---

#### 6️⃣ لا يتم تسجيل تسديد الديون في الصندوق

**المشكلة:**
عند استقبال تسديد دين من عميل:

- لا يوجد استدعاء لـ `_cashRegisterService.RecordTransactionAsync()`
- الدفع لا يظهر في cash register audit trail
- لا يؤثر على رصيد الصندوق اليومي

**الملف المتعلق:** [backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs](backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs)

**التأثير:**

```
الصندوق اليومي لا يعكس ديون العملاء المُسددة
→ عدم توازن في الاغلاق (Shift Close)
```

---

#### 7️⃣ لا يوجد تقرير ديون

**المشكلة:**

- لا يوجد endpoint لجلب العملاء بديون فقط
- لا يوجد endpoint لتقرير الديون المستحقة
- لا يوجد تقرير "أعمار الديون" (Aging Report)

**الملفات المفقودة:**

- ❌ `GET /api/customers/with-debt` - لا يوجد
- ❌ `GET /api/reports/debts` - لا يوجد
- ❌ `DebtsReportPage.tsx` - لا يوجد

**التأثير:**

```
المدير لا يستطيع:
- معرفة من عليهم ديون
- معرفة كم الدين الإجمالي المستحق
- معرفة أيهم أقدم دين
```

---

### 🟡 MEDIUM PRIORITY GAPS

#### 8️⃣ حد الائتمان غير متاح عند الإنشاء

**المشكلة:**
في [CustomerFormModal.tsx (سطر 252)](frontend/src/components/customers/CustomerFormModal.tsx#L252):

```tsx
{isEditing && (  // ← شرط!
  <div>
    <label>حد الائتمان</label>
    <input ... />
  </div>
)}
```

عند إنشاء عميل جديد:

- لا يمكن تعيين حد ائتمان مباشرة
- العميل ينشأ بـ `creditLimit = 0` (غير محدود)
- يجب تعديل العميل بعدها لتحديد الحد

**الحل:**

```tsx
{/* كل من الإنشاء والتعديل */}
<div>
  <label>حد الائتمان (ج.م)</label>
  <input ... />
  <small>0 = بدون حد محدد (ائتمان غير محدود)</small>
</div>
```

---

#### 9️⃣ تكرار كود الفرونت (Code Duplication)

**المشكلة:**
منطق التحقق من حد الائتمان مكرر في مكانين:

1. [POSWorkspacePage.tsx (سطر 238)](frontend/src/pages/pos/POSWorkspacePage.tsx#L238)
2. [PaymentModal.tsx (سطר 56)](frontend/src/components/pos/PaymentModal.tsx#L56)

- نفس المنطق:
  - حساب `canTakeCredit`
  - حساب `creditLimitExceeded`
  - نفس الشروط

**الحل:**

```typescript
// hooks/usePartialPaymentValidation.ts
export const usePartialPaymentValidation = (
  selectedCustomer: Customer | null,
  amountDue: number,
) => {
  const canTakeCredit = selectedCustomer?.creditLimit ?? 0 > 0;
  const creditLimitExceeded =
    (selectedCustomer?.totalDue ?? 0) + amountDue >
    (selectedCustomer?.creditLimit ?? 0);
  return { canTakeCredit, creditLimitExceeded };
};
```

**التأثير:** عيب بسيط، لكن يزيد من احتمال أخطاء في الصيانة

---

## 📊 ملخص الحالة

| الجزء                                | الحالة   | ملاحظات                                |
| ------------------------------------ | -------- | -------------------------------------- |
| **Database Schema**                  | ✅ مكتمل | `TotalDue`, `CreditLimit` موجودة       |
| **Enum for Payment Methods**         | ✅ موجود | Cash, Card, Fawry, BankTransfer        |
| **Backend Validation**               | ✅ يعمل  | Check credit limit، Increment TotalDue |
| **Backend Credit Increase**          | ✅ يعمل  | OrderService.CompleteAsync             |
| **Backend Credit Decrease**          | ❌ مفقود | لا توجد دالة لإنقاص TotalDue           |
| **Backend DebtPayment Entity**       | ❌ مفقود | لا توجد جدول تسجيل التسديدات           |
| **Backend DebtPayment Service**      | ❌ مفقود | لا توجد service                        |
| **Backend DebtPayment API Endpoint** | ❌ مفقود | لا توجد POST/PUT endpoints             |
| **Refund Integration with Debt**     | ❌ مكسور | لا ينقص TotalDue                       |
| **Cancel Integration with Debt**     | ❌ مكسور | لا ينقص TotalDue                       |
| **Cash Register Integration**        | ❌ مفقود | لا يُسجل تسديدات الديون                |
| **Frontend Display (TotalDue)**      | ✅ مكتمل | معروض في 5 أماكن                       |
| **Frontend Display (CreditLimit)**   | ✅ مكتمل | معروض في 3 أماكن                       |
| **Frontend Edit CreditLimit**        | ⚠️ جزئي  | متاح فقط لـ editing، ليس creation      |
| **Frontend Debt Payment UI**         | ❌ مفقود | لا يوجد زر/modal لتسديد دين            |
| **Frontend Debt Payment Modal**      | ❌ مفقود | لا يوجد                                |
| **Frontend Debts Report**            | ❌ مفقود | لا يوجد صفحة تقرير ديون                |
| **Debt History/Audit Trail**         | ❌ مفقود | لا يوجد جدول history                   |

---

## 🎯 الخطوات اللازمة للإكمال

### الأولوية 1 - CORE FUNCTIONALITY (حرج)

1. **إنشاء Entity `DebtPayment`**
   - حقول: `Id`, `CustomerId`, `AmountPaid`, `PaymentDate`, `PaymentMethod`, `UserId`, `Notes`, `ReferenceNumber`
   - Database migration

2. **إضافة خدمة `PayDebtAsync` في `CustomerService`**
   - تقليل `Customer.TotalDue`
   - إنشاء `DebtPayment` أثري
   - التحقق من الصحة (amount > 0, totalDue >= amount, etc)

3. **إضافة Endpoint في `CustomersController`**
   - `POST /api/customers/{id}/pay-debt`
   - Request: `{ amountPaid, paymentMethod, notes }`

4. **تسجيل في Cash Register**
   - استدعاء `CashRegisterService.RecordTransactionAsync()`
   - Type: `DebtPayment` أو جديد

### الأولوية 2 - FIX BROKEN FLOWS (حرج)

5. **إصلاح Refund Flow**
   - عند استرجاع طلب: إنقاص `Customer.TotalDue` بمقدار `order.AmountDue`

6. **إصلاح Cancel Flow**
   - عند إلغاء طلب: نفس الشيء

### الأولوية 3 - FRONTEND (مهم)

7. **إنشاء `DebtPaymentModal.tsx`**
   - حقول: مبلغ، طريقة الدفع، ملاحظات
   - زر تأكيد

8. **إضافة زر في `CustomerDetailsModal`**
   - "تسديد دين" عندما `totalDue > 0`

9. **إنشاء `DebtsReportPage.tsx`**
   - جدول العملاء المدينين
   - إجمالي الديون، الديون القديمة، إلخ

### الأولوية 4 - UX (إضافات)

10. **إتاحة `creditLimit` عند الإنشاء**
11. **تقرير "أعمار الديون"** (Aging Report)
12. **notification عند الاقتراب من حد الائتمان**

---

## 🔗 الملفات المرتبطة

### Backend

- [CustomerService.cs](backend/KasserPro.Application/Services/Implementations/CustomerService.cs) - أضف `PayDebtAsync`
- [OrderService.cs](backend/KasserPro.Application/Services/Implementations/OrderService.cs) - أصلح `RefundAsync` و `CancelAsync`
- [CustomersController.cs](backend/KasserPro.API/Controllers/CustomersController.cs) - أضف endpoint
- [Customer.cs](backend/KasserPro.Domain/Entities/Customer.cs) - موجود بالفعل
- [CashRegisterService.cs](backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs) - للتسجيل

### Frontend

- [POSWorkspacePage.tsx](frontend/src/pages/pos/POSWorkspacePage.tsx) - شاشة الكاشير
- [PaymentModal.tsx](frontend/src/components/pos/PaymentModal.tsx) - modal الدفع
- [CustomersPage.tsx](frontend/src/pages/customers/CustomersPage.tsx) - قائمة العملاء
- [CustomerDetailsModal.tsx](frontend/src/components/customers/CustomerDetailsModal.tsx) - تفاصيل العميل
- [CustomerFormModal.tsx](frontend/src/components/customers/CustomerFormModal.tsx) - نموذج تعديل

---

## 📝 ملاحظات مهمة

1. **`TotalDue` لا يمكن تعديله يدوياً** - يجب أن يكون read-only في الـ API (صحيح حالياً)

2. **التصلح في RefundAsync و CancelAsync ضروري جداً** - قد يكون هناك آلاف العملاء بديون وهمية

3. **Debt Payment يجب أن يكون معاملة صرف نقدي** - يؤثر على الصندوق

4. **الحد الأدنى للتسديد** - لا بأس أن يدفع أقل من الدين الكامل (partial payment)

5. **Refund Schedule** - لا بأس يرجع فقط له متى ما طلب (غير محدد بوقت معين)

---

**آخر تحديث:** 4 مارس 2026
