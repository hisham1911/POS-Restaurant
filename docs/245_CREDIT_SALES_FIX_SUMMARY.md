# ✅ إصلاح البيع الآجل - ملخص التغييرات

## 🎯 المشكلة

البيع الآجل (Credit Sales) لا يعمل - الطلبات تفشل بـ 400 عند محاولة الدفع الجزئي.

---

## 🔧 التغييرات المطبقة

### 1. Backend - OrderService.cs ✅

**الملف**: `backend/KasserPro.Application/Services/Implementations/OrderService.cs`

**التغييرات**:
```csharp
// إضافة التحقق من حالة العميل (IsActive)
if (customer == null || !customer.IsActive)
{
    return ApiResponse<OrderDto>.Fail(ErrorCodes.CUSTOMER_NOT_ACTIVE,
        "العميل غير نشط. لا يمكن إتمام البيع الآجل.");
}
```

### 2. Backend - ErrorCodes.cs ✅

**الملف**: `backend/KasserPro.Application/Common/ErrorCodes.cs`

**التغييرات**:
```csharp
// إضافة error codes جديدة
public const string CUSTOMER_NOT_FOUND = "CUSTOMER_NOT_FOUND";
public const string CUSTOMER_NOT_ACTIVE = "CUSTOMER_NOT_ACTIVE";
public const string CUSTOMER_CREDIT_LIMIT_EXCEEDED = "CUSTOMER_CREDIT_LIMIT_EXCEEDED";

// إضافة الرسائل العربية
{ ErrorCodes.CUSTOMER_NOT_FOUND, "العميل غير موجود" },
{ ErrorCodes.CUSTOMER_NOT_ACTIVE, "العميل غير نشط" },
{ ErrorCodes.CUSTOMER_CREDIT_LIMIT_EXCEEDED, "تجاوز حد الائتمان المسموح للعميل" },
```

### 3. Frontend - errorHandler.ts ✅

**الملف**: `frontend/src/utils/errorHandler.ts`

**التغييرات**:
```typescript
// إضافة error codes
CUSTOMER_NOT_FOUND: "CUSTOMER_NOT_FOUND",
CUSTOMER_NOT_ACTIVE: "CUSTOMER_NOT_ACTIVE",
CUSTOMER_CREDIT_LIMIT_EXCEEDED: "CUSTOMER_CREDIT_LIMIT_EXCEEDED",

// إضافة الرسائل
[ERROR_CODES.CUSTOMER_NOT_FOUND]: "العميل غير موجود",
[ERROR_CODES.CUSTOMER_NOT_ACTIVE]: "العميل غير نشط",
[ERROR_CODES.CUSTOMER_CREDIT_LIMIT_EXCEEDED]: "تم تجاوز حد الائتمان للعميل",
```

### 4. Frontend - baseApi.ts ✅

**الملف**: `frontend/src/api/baseApi.ts`

**التغييرات**:
```typescript
// إضافة معالجة خاصة لأخطاء البيع الآجل
else if (errorData?.errorCode === "CUSTOMER_CREDIT_LIMIT_EXCEEDED") {
  toast.error(message, { duration: 6000 });
} else if (errorData?.errorCode === "PAYMENT_INSUFFICIENT") {
  toast.error(message);
} else if (errorData?.errorCode === "PAYMENT_EXCEEDS_DUE") {
  toast.error(message);
} else if (error.status === 409) {
  toast.error(message || "تم تعديل البيانات من مستخدم آخر - يرجى تحديث الصفحة");
  api.dispatch({ type: "api/invalidateTags", payload: ["Orders", "Customers", "Shifts"] });
}
```

### 5. Frontend - useOrders.ts ✅

**الملف**: `frontend/src/hooks/useOrders.ts`

**التغييرات**:
```typescript
// تحسين معالجة الأخطاء لتجنب التكرار
if (!apiError.data?.errorCode && apiError.status !== 400 && apiError.status !== 409) {
  toast.error("فشل في إتمام الطلب");
}
```

### 6. Frontend - PaymentModal.tsx ✅

**الملف**: `frontend/src/components/pos/PaymentModal.tsx`

**التغييرات**:
```typescript
// حساب الائتمان المتاح
const availableCredit = selectedCustomer
  ? selectedCustomer.creditLimit - selectedCustomer.totalDue
  : 0;

// التحقق من حالة العميل
const canTakeCredit =
  selectedCustomer &&
  selectedCustomer.isActive &&
  (selectedCustomer.creditLimit === 0 || amountDue <= availableCredit);

// عرض معلومات الائتمان بشكل تفصيلي
<div className="mt-2 p-2 bg-white rounded border border-gray-200">
  <div className="flex justify-between text-xs mb-1">
    <span>حد الائتمان:</span>
    <span>{formatCurrency(selectedCustomer.creditLimit)}</span>
  </div>
  <div className="flex justify-between text-xs mb-1">
    <span>المستخدم:</span>
    <span>{formatCurrency(selectedCustomer.totalDue)}</span>
  </div>
  <div className="flex justify-between text-xs">
    <span>المتاح:</span>
    <span>{formatCurrency(availableCredit)}</span>
  </div>
  {/* Progress bar */}
</div>
```

### 7. Frontend - POSWorkspacePage.tsx ✅

**الملف**: `frontend/src/pages/pos/POSWorkspacePage.tsx`

**التغييرات**: نفس التحسينات في PaymentModal

### 8. Frontend - Types ✅

**الملفات**:
- `frontend/src/types/shift.types.ts` - إضافة `rowVersion`
- `frontend/src/types/order.types.ts` - `rowVersion` موجود بالفعل
- `frontend/src/types/customer.types.ts` - `rowVersion` موجود بالفعل

### 9. Frontend - Components ✅

**الملفات**:
- `frontend/src/pages/shifts/ShiftPage.tsx` - تمرير `rowVersion` عند إغلاق الوردية
- `frontend/src/components/customers/CustomerFormModal.tsx` - تمرير `rowVersion` عند التحديث

---

## 🧪 خطوات الاختبار

### 1. إعادة تشغيل Backend

```bash
# أوقف البرنامج الحالي أولاً
# ثم
cd backend/KasserPro.API
dotnet run
```

### 2. اختبار البيع الآجل

1. **إنشاء عميل** مع حد ائتمان 1000 ج.م
2. **إضافة منتجات** بإجمالي 1500 ج.م
3. **محاولة البيع الآجل** → يجب أن يظهر خطأ واضح
4. **دفع 600 ج.م نقداً + 400 ج.م آجل** → يجب أن ينجح

### 3. اختبار العميل غير النشط

1. **تعطيل عميل** (IsActive = false)
2. **محاولة البيع الآجل** → يجب أن يظهر "العميل غير نشط"

### 4. اختبار Concurrency

1. **فتح نفس الوردية** في تابين
2. **إغلاق في التاب الأول**
3. **محاولة الإغلاق في التاب الثاني** → يجب أن يظهر خطأ concurrency

---

## 📊 الأسباب المحتملة للمشكلة الحالية

بما أن الطلبات ما زالت تفشل بـ 400، الأسباب المحتملة:

### 1. العميل غير مرتبط بالطلب ❌
```typescript
// تأكد من أن customerId يتم تمريره عند إنشاء الطلب
const order = await createOrder(selectedCustomer?.id);
```

### 2. العميل غير نشط ❌
```sql
-- تحقق من حالة العميل في قاعدة البيانات
SELECT Id, Name, Phone, IsActive, CreditLimit, TotalDue 
FROM Customers 
WHERE Id = [customer_id];
```

### 3. تجاوز حد الائتمان ❌
```
المتاح = CreditLimit - TotalDue
المطلوب = Total - AmountPaid

إذا كان المطلوب > المتاح → خطأ
```

### 4. البرنامج لم يتم إعادة تشغيله ❌
التغييرات في Backend تحتاج إعادة تشغيل البرنامج لتطبيقها.

---

## 🔍 التشخيص

لمعرفة السبب الدقيق، افحص:

### 1. اللوج بعد إعادة التشغيل
```bash
Get-Content "backend/KasserPro.API/logs/kasserpro-20260311.log" -Tail 20
```

### 2. بيانات العميل
```sql
SELECT * FROM Customers WHERE Id = [customer_id];
```

### 3. بيانات الطلب
```sql
SELECT * FROM Orders WHERE Id IN (560, 561, 562);
```

---

## ✅ الخطوات التالية

1. **أوقف البرنامج الحالي** (Ctrl+C في terminal)
2. **أعد تشغيل Backend**: `dotnet run`
3. **جرب البيع الآجل** مرة أخرى
4. **افحص اللوج** لرؤية رسالة الخطأ التفصيلية
5. **شارك رسالة الخطأ** إذا استمرت المشكلة

---

**الحالة**: ⏳ في انتظار إعادة تشغيل Backend  
**التاريخ**: 2026-03-11  
**المهندس**: Kiro AI Assistant
