# إصلاح خطأ حفظ المصروف - أسماء الحقول ✅

## المشكلة

عند محاولة حفظ مصروف جديد، يظهر خطأ: "حدث خطأ أثناء حفظ المصروف"

## السبب

عدم تطابق أسماء الحقول بين الـ Frontend والـ Backend:

| Frontend (خطأ) | Backend (صحيح) |
|---------------|----------------|
| `receiptNumber` | `referenceNumber` |
| `vendorName` | `beneficiary` |

## الحل

تم تصحيح أسماء الحقول في الـ Frontend لتطابق الـ Backend.

## التغييرات

### 1. `client/src/types/expense.types.ts`

```typescript
// قبل (خطأ)
export interface CreateExpenseRequest {
  receiptNumber?: string;
  vendorName?: string;
}

// بعد (صحيح)
export interface CreateExpenseRequest {
  referenceNumber?: string;
  beneficiary?: string;
}
```

### 2. `client/src/pages/expenses/ExpenseFormPage.tsx`

تم تغيير:
- `receiptNumber` → `referenceNumber`
- `vendorName` → `beneficiary`
- تسمية الحقل من "اسم المورد" إلى "المستفيد"
- تسمية الحقل من "رقم الإيصال" إلى "رقم المرجع"

## الحقول الصحيحة الآن

### CreateExpenseRequest (Frontend → Backend)

```typescript
{
  categoryId: number;           // ✅ مطلوب
  amount: number;               // ✅ مطلوب
  description: string;          // ✅ مطلوب
  expenseDate: string;          // ✅ مطلوب
  notes?: string;               // اختياري
  referenceNumber?: string;     // اختياري (رقم المرجع/الإيصال)
  beneficiary?: string;         // اختياري (المستفيد)
}
```

## اختبار الإصلاح

1. **افتح صفحة إنشاء مصروف جديد**
2. **املأ البيانات**:
   - التصنيف: اختر أي تصنيف (مثلاً: رواتب)
   - المبلغ: 1000
   - تاريخ المصروف: اليوم
   - الوصف: "اختبار المصروف"
   - المستفيد: "محمد أحمد" (اختياري)
   - رقم المرجع: "INV-001" (اختياري)
3. **اضغط حفظ**
4. **يجب أن يتم الحفظ بنجاح** وتنتقل إلى صفحة تفاصيل المصروف

## الملفات المعدلة

- ✅ `client/src/types/expense.types.ts`
- ✅ `client/src/pages/expenses/ExpenseFormPage.tsx`

## ملاحظات

### الحقول المطلوبة
- ✅ `categoryId` - يجب اختيار تصنيف
- ✅ `amount` - يجب أن يكون أكبر من 0
- ✅ `description` - يجب إدخال وصف
- ✅ `expenseDate` - يجب تحديد التاريخ

### الحقول الاختيارية
- `notes` - ملاحظات إضافية
- `referenceNumber` - رقم الإيصال أو الفاتورة
- `beneficiary` - اسم المستفيد أو الجهة

### الحالة الافتراضية
- عند إنشاء مصروف جديد، يكون في حالة **Draft** (مسودة)
- يمكن للـ Admin اعتماده (Approve) أو رفضه (Reject)
- بعد الاعتماد، يمكن دفعه (Pay)

---

**الحالة**: ✅ تم الإصلاح - جاهز للاختبار
