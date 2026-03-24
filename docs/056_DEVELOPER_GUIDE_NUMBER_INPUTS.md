# دليل المطور - حقول الأرقام

## 🎯 الهدف

عند إضافة حقل رقم جديد في التطبيق، اتبع هذا الدليل لضمان تجربة مستخدم متسقة.

## ⚠️ لا تفعل هذا

```tsx
// ❌ خطأ - يظهر الصفر كقيمة فعلية
<input
  type="number"
  value={price}
  onChange={(e) => setPrice(Number(e.target.value))}
/>
```

## ✅ افعل هذا

### الطريقة 1: Simple Pattern (موصى بها للحالات البسيطة)

```tsx
// ✅ صحيح - الصفر يظهر كـ placeholder
<input
  type="number"
  value={price === 0 ? "" : price}
  onChange={(e) => setPrice(Number(e.target.value) || 0)}
  placeholder="0.00"
/>
```

### الطريقة 2: Helper Functions (للحالات المعقدة)

```tsx
import { numberToDisplay, displayToNumber } from "@/hooks/useNumberInput";

<input
  type="number"
  value={numberToDisplay(formData.price)}
  onChange={(e) => setFormData({
    ...formData,
    price: displayToNumber(e.target.value)
  })}
  placeholder="0.00"
/>
```

### الطريقة 3: Custom Hook (للحالات المتقدمة)

```tsx
import { useNumberInput } from "@/hooks/useNumberInput";

function MyComponent() {
  const price = useNumberInput(0);
  
  return (
    <input
      type="number"
      value={price.displayValue}
      onChange={(e) => price.handleChange(e.target.value)}
      placeholder="0.00"
    />
  );
}
```

## 📋 القواعد حسب نوع الحقل

### 1. الأسعار والمبالغ المالية

```tsx
<input
  type="number"
  step="0.01"
  min="0"
  value={amount === 0 ? "" : amount}
  onChange={(e) => setAmount(Number(e.target.value) || 0)}
  placeholder="0.00"
/>
```

**متى تستخدم**: حقول السعر، المبلغ، التكلفة، الإيداع، السحب

### 2. الكميات

```tsx
<input
  type="number"
  min="1"
  value={quantity === 1 ? "" : quantity}
  onChange={(e) => setQuantity(Number(e.target.value) || 1)}
  placeholder="1"
/>
```

**متى تستخدم**: حقول الكمية، عدد القطع

### 3. النسب المئوية

```tsx
<input
  type="number"
  min="0"
  max="100"
  step="0.01"
  value={taxRate === 14 ? "" : taxRate}
  onChange={(e) => setTaxRate(Number(e.target.value) || 14)}
  placeholder="14"
/>
```

**متى تستخدم**: الضريبة، نسبة الخصم

### 4. الحقول الاختيارية (nullable)

```tsx
<input
  type="number"
  value={reorderPoint ?? ""}
  onChange={(e) => setReorderPoint(
    e.target.value ? Number(e.target.value) : null
  )}
  placeholder="اختياري"
/>
```

**متى تستخدم**: الحقول التي يمكن أن تكون null

### 5. حقول البحث والفلترة

```tsx
<input
  type="number"
  value={filters.shiftId || ""}
  onChange={(e) => setFilters({
    ...filters,
    shiftId: e.target.value ? Number(e.target.value) : undefined
  })}
  placeholder="رقم الوردية"
/>
```

**متى تستخدم**: حقول البحث والفلاتر

## 🔍 Checklist قبل الـ Commit

عند إضافة أو تعديل حقل رقم، تأكد من:

- [ ] الحقل يستخدم `value={field === defaultValue ? "" : field}`
- [ ] الـ onChange يستخدم `|| defaultValue` لتجنب NaN
- [ ] الـ placeholder يوضح القيمة المتوقعة
- [ ] الحقل له `min` و `max` مناسبين
- [ ] الحقل له `step` مناسب (0.01 للأسعار، 1 للكميات)
- [ ] تم اختبار الحقل في المتصفح

## 🐛 مشاكل شائعة وحلولها

### المشكلة: الحقل يظهر "NaN"

```tsx
// ❌ خطأ
onChange={(e) => setPrice(Number(e.target.value))}

// ✅ صحيح
onChange={(e) => setPrice(Number(e.target.value) || 0)}
```

### المشكلة: الحقل لا يقبل الأرقام العشرية

```tsx
// ✅ أضف step="0.01"
<input type="number" step="0.01" />
```

### المشكلة: المستخدم يمكنه إدخال أرقام سالبة

```tsx
// ✅ أضف min="0"
<input type="number" min="0" />
```

## 📚 أمثلة من الكود الحالي

راجع هذه الملفات كمرجع:
- `components/pos/CustomItemModal.tsx` - مثال كامل
- `components/products/ProductFormModal.tsx` - حالات متعددة
- `pages/purchase-invoices/PurchaseInvoiceFormPage.tsx` - نمط بسيط

## 🤝 المساهمة

عند مراجعة Pull Request، تأكد من أن حقول الأرقام تتبع هذا النمط.
