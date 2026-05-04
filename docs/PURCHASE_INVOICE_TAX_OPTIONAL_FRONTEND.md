# ✅ تحديث الفرونت-اند - الضريبة الاختيارية في فواتير الشراء

> **التاريخ:** 2 مايو 2026  
> **الحالة:** ✅ مكتمل  
> **المهمة:** تحديث واجهة المستخدم لدعم الضريبة الاختيارية

---

## 📋 التغييرات المُنفذة

### 1. **تحديث TypeScript Types**

#### ملف: `frontend/src/types/purchaseInvoice.types.ts`

```typescript
// ✅ إضافة isTaxEnabled لكل الـ interfaces

export interface PurchaseInvoice {
  // ... existing fields
  taxRate: number;
  isTaxEnabled: boolean;  // ✅ جديد
  taxAmount: number;
  // ... rest of fields
}

export interface PurchaseInvoicePreview {
  subtotal: number;
  taxRate: number;
  isTaxEnabled: boolean;  // ✅ جديد
  taxAmount: number;
  total: number;
}

export interface CreatePurchaseInvoiceRequest {
  supplierId: number;
  invoiceDate: string;
  items: CreatePurchaseInvoiceItemRequest[];
  notes?: string;
  isTaxEnabled?: boolean;  // ✅ جديد (optional, default: true)
}

export interface UpdatePurchaseInvoiceRequest {
  supplierId: number;
  invoiceDate: string;
  items: UpdatePurchaseInvoiceItemRequest[];
  notes?: string;
  isTaxEnabled?: boolean;  // ✅ جديد (optional)
}
```

---

### 2. **تحديث Form Page**

#### ملف: `frontend/src/pages/purchase-invoices/PurchaseInvoiceFormPage.tsx`

#### أ) إضافة State للـ Tax Toggle

```typescript
const [isTaxEnabled, setIsTaxEnabled] = useState<boolean>(true);
```

**الافتراضي:** `true` (الضريبة مفعّلة)

---

#### ب) تحميل البيانات في Edit Mode

```typescript
useEffect(() => {
  if (invoice && isEditMode) {
    setSupplierId(invoice.supplierId);
    setInvoiceDate(invoice.invoiceDate.split("T")[0]);
    setNotes(invoice.notes || "");
    setIsTaxEnabled(invoice.isTaxEnabled);  // ✅ تحميل حالة الضريبة
    setItems(/* ... */);
  }
}, [invoice, isEditMode]);
```

---

#### ج) تحديث Preview Effect

```typescript
useEffect(() => {
  // ... validation

  const requestData = {
    supplierId,
    invoiceDate: new Date(invoiceDate).toISOString(),
    items: items.map(/* ... */),
    notes,
    isTaxEnabled,  // ✅ إرسال حالة الضريبة للباك-اند
  };

  const timer = setTimeout(() => {
    void prepareInvoice(requestData)
      .unwrap()
      .then((response) => {
        setPreview(response.data || null);
      });
  }, 500);

  return () => clearTimeout(timer);
}, [supplierId, invoiceDate, items, notes, isTaxEnabled, prepareInvoice]);
//                                         ^^^^^^^^^^^^^ ✅ dependency
```

---

#### د) تحديث حساب الضريبة المحلي (Fallback)

```typescript
const calculateTaxAmount = () => {
  if (!isTaxEnabled) return 0;  // ✅ لو الضريبة معطّلة → 0
  return roundCurrency(calculateSubtotal() * (purchaseTaxRate / 100));
};
```

**ملاحظة:** الحساب المحلي هو fallback فقط. الحساب الفعلي يأتي من الباك-اند عبر `preview`.

---

#### هـ) إضافة Checkbox في الـ UI

```tsx
<div className="grid grid-cols-1 md:grid-cols-3 gap-4">
  {/* المورد */}
  <div>...</div>

  {/* تاريخ الفاتورة */}
  <div>...</div>

  {/* ملاحظات */}
  <div>...</div>

  {/* ✅ Checkbox للضريبة */}
  <div className="flex items-center">
    <label className="flex items-center gap-2 cursor-pointer">
      <input
        type="checkbox"
        checked={isTaxEnabled}
        onChange={(e) => setIsTaxEnabled(e.target.checked)}
        className="w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
      />
      <span className="text-sm font-medium">
        تطبيق الضريبة ({purchaseTaxRate}%)
      </span>
    </label>
  </div>
</div>
```

**الموقع:** في قسم "بيانات الفاتورة" بعد حقل الملاحظات

---

#### و) تحديث عرض الضريبة في Totals

```tsx
<div className="flex justify-between mb-2">
  <span className="text-sm text-gray-600">
    الضريبة ({purchaseTaxRate}%):
  </span>
  <span className="text-sm font-medium">
    {isTaxEnabled ? formatCurrency(taxAmount) : "معفاة"}
    {/* ✅ لو معطّلة → "معفاة" بدل 0.00 ج.م */}
  </span>
</div>
```

---

#### ز) إرسال البيانات للباك-اند

```typescript
const handleSubmit = async (e: React.FormEvent) => {
  e.preventDefault();

  // ... validation

  const requestData = {
    supplierId,
    invoiceDate: new Date(invoiceDate).toISOString(),
    items: items.map(/* ... */),
    notes,
    isTaxEnabled,  // ✅ إرسال حالة الضريبة
  };

  try {
    if (isEditMode) {
      await updateInvoice({ id: Number(id), data: requestData }).unwrap();
    } else {
      await createInvoice(requestData).unwrap();
    }
    // ... success handling
  } catch (error) {
    // ... error handling
  }
};
```

---

## 🎨 الواجهة (UI)

### قبل التعديل

```
┌─────────────────────────────────────────────────┐
│ بيانات الفاتورة                                 │
├─────────────────────────────────────────────────┤
│ المورد: [اختر المورد ▼]                         │
│ تاريخ الفاتورة: [2026-05-02]                    │
│ ملاحظات: [ملاحظات اختيارية]                    │
└─────────────────────────────────────────────────┘
```

---

### بعد التعديل

```
┌─────────────────────────────────────────────────┐
│ بيانات الفاتورة                                 │
├─────────────────────────────────────────────────┤
│ المورد: [اختر المورد ▼]                         │
│ تاريخ الفاتورة: [2026-05-02]                    │
│ ملاحظات: [ملاحظات اختيارية]                    │
│ ☑ تطبيق الضريبة (14%)  ← ✅ جديد                │
└─────────────────────────────────────────────────┘
```

---

### عرض الإجماليات

#### مع الضريبة (☑ checked)

```
┌─────────────────────────────────────────────────┐
│ المجموع الفرعي:                    1,000.00 ج.م │
│ الضريبة (14%):                       140.00 ج.م │
│ ─────────────────────────────────────────────── │
│ الإجمالي / المبلغ المستحق:         1,140.00 ج.م │
└─────────────────────────────────────────────────┘
```

---

#### بدون الضريبة (☐ unchecked)

```
┌─────────────────────────────────────────────────┐
│ المجموع الفرعي:                    1,000.00 ج.م │
│ الضريبة (14%):                            معفاة │
│ ─────────────────────────────────────────────── │
│ الإجمالي / المبلغ المستحق:         1,000.00 ج.م │
└─────────────────────────────────────────────────┘
```

---

## 🔄 User Flow

### السيناريو 1: إنشاء فاتورة بضريبة (الافتراضي)

```
1. المستخدم يفتح صفحة إنشاء فاتورة جديدة
   → Checkbox "تطبيق الضريبة" مُفعّل افتراضيًا ✅

2. المستخدم يختار المورد ويضيف المنتجات
   → Preview يُحدّث تلقائيًا كل 500ms
   → الباك-اند يحسب: taxAmount = subtotal * 0.14

3. المستخدم يضغط "حفظ"
   → Request: { ..., isTaxEnabled: true }
   → الفاتورة تُحفظ بضريبة ✅
```

---

### السيناريو 2: إنشاء فاتورة بدون ضريبة

```
1. المستخدم يفتح صفحة إنشاء فاتورة جديدة
   → Checkbox "تطبيق الضريبة" مُفعّل افتراضيًا ✅

2. المستخدم يُلغي تفعيل الـ Checkbox ☐
   → isTaxEnabled = false
   → Preview يُحدّث فورًا
   → الباك-اند يحسب: taxAmount = 0

3. عرض الإجماليات يتغير:
   الضريبة (14%): معفاة  ← بدل 140.00 ج.م

4. المستخدم يضغط "حفظ"
   → Request: { ..., isTaxEnabled: false }
   → الفاتورة تُحفظ بدون ضريبة ✅
```

---

### السيناريو 3: تعديل فاتورة موجودة

```
1. المستخدم يفتح فاتورة موجودة للتعديل
   → useEffect يُحمّل البيانات من invoice.isTaxEnabled
   → Checkbox يعكس الحالة المحفوظة

2. المستخدم يُغيّر حالة الضريبة
   → Preview يُحدّث تلقائيًا
   → الإجماليات تتغير فورًا

3. المستخدم يضغط "تحديث"
   → Request: { ..., isTaxEnabled: <new_value> }
   → الفاتورة تُحدّث ✅
```

---

## 🧪 اختبار الميزة

### Test Case 1: إنشاء فاتورة بضريبة

```
الخطوات:
1. افتح /purchase-invoices/new
2. اختر مورد
3. أضف منتج: quantity=10, purchasePrice=100
4. تأكد أن Checkbox "تطبيق الضريبة" مُفعّل ✅
5. اضغط "حفظ"

النتيجة المتوقعة:
- Subtotal: 1000.00 ج.م
- Tax: 140.00 ج.م
- Total: 1140.00 ج.م
- الفاتورة تُحفظ بـ isTaxEnabled = true
```

---

### Test Case 2: إنشاء فاتورة بدون ضريبة

```
الخطوات:
1. افتح /purchase-invoices/new
2. اختر مورد
3. أضف منتج: quantity=10, purchasePrice=100
4. ألغِ تفعيل Checkbox "تطبيق الضريبة" ☐
5. تأكد أن عرض الضريبة = "معفاة"
6. اضغط "حفظ"

النتيجة المتوقعة:
- Subtotal: 1000.00 ج.م
- Tax: معفاة
- Total: 1000.00 ج.م
- الفاتورة تُحفظ بـ isTaxEnabled = false
```

---

### Test Case 3: تعديل فاتورة - تغيير حالة الضريبة

```
الخطوات:
1. افتح فاتورة موجودة (isTaxEnabled = true)
2. ألغِ تفعيل Checkbox "تطبيق الضريبة" ☐
3. تأكد أن Preview يتحدث فورًا
4. اضغط "تحديث"
5. أعد فتح الفاتورة

النتيجة المتوقعة:
- الفاتورة تُحدّث بـ isTaxEnabled = false
- الإجماليات تتغير: Total = Subtotal (بدون ضريبة)
- عند إعادة الفتح: Checkbox غير مُفعّل ☐
```

---

### Test Case 4: Preview Real-time Update

```
الخطوات:
1. افتح /purchase-invoices/new
2. اختر مورد وأضف منتج
3. انتظر 500ms → Preview يُحدّث من الباك-اند
4. غيّر حالة Checkbox "تطبيق الضريبة"
5. انتظر 500ms → Preview يُحدّث مرة أخرى

النتيجة المتوقعة:
- كل تغيير في isTaxEnabled يُرسل request جديد للباك-اند
- Preview يعكس الحساب الصحيح من الباك-اند
- الرسالة تظهر: "جاري تحديث المعاينة من الباك إند..."
```

---

## 📊 مقارنة قبل وبعد

### ❌ قبل التعديل

```typescript
// Types
interface CreatePurchaseInvoiceRequest {
  supplierId: number;
  items: CreatePurchaseInvoiceItemRequest[];
  // ❌ لا يوجد isTaxEnabled
}

// State
const [notes, setNotes] = useState<string>("");
// ❌ لا يوجد isTaxEnabled state

// Calculation
const calculateTaxAmount = () => {
  return roundCurrency(calculateSubtotal() * (purchaseTaxRate / 100));
  // ❌ دايمًا يحسب الضريبة
};

// UI
{/* ❌ لا يوجد checkbox */}

// Display
<span>{formatCurrency(taxAmount)}</span>
// ❌ دايمًا يعرض رقم
```

---

### ✅ بعد التعديل

```typescript
// Types
interface CreatePurchaseInvoiceRequest {
  supplierId: number;
  items: CreatePurchaseInvoiceItemRequest[];
  isTaxEnabled?: boolean;  // ✅ جديد
}

// State
const [isTaxEnabled, setIsTaxEnabled] = useState<boolean>(true);
// ✅ state للتحكم في الضريبة

// Calculation
const calculateTaxAmount = () => {
  if (!isTaxEnabled) return 0;  // ✅ يتحقق من الحالة
  return roundCurrency(calculateSubtotal() * (purchaseTaxRate / 100));
};

// UI
<input
  type="checkbox"
  checked={isTaxEnabled}
  onChange={(e) => setIsTaxEnabled(e.target.checked)}
/>
// ✅ checkbox للتحكم

// Display
<span>
  {isTaxEnabled ? formatCurrency(taxAmount) : "معفاة"}
</span>
// ✅ يعرض "معفاة" لو معطّلة
```

---

## 🎯 القواعد

### 1. **القيمة الافتراضية**

```typescript
const [isTaxEnabled, setIsTaxEnabled] = useState<boolean>(true);
```

- الفواتير الجديدة: **بضريبة افتراضيًا** ✅
- يطابق الباك-اند: `public bool IsTaxEnabled { get; set; } = true;`

---

### 2. **Real-time Preview**

```typescript
useEffect(() => {
  // ... validation

  const timer = setTimeout(() => {
    void prepareInvoice(requestData).unwrap();
  }, 500);  // ✅ debounce 500ms

  return () => clearTimeout(timer);
}, [supplierId, invoiceDate, items, notes, isTaxEnabled, prepareInvoice]);
//                                         ^^^^^^^^^^^^^ ✅ dependency
```

- كل تغيير في `isTaxEnabled` → request جديد للباك-اند
- Debounce 500ms لتقليل الـ requests

---

### 3. **Fallback Calculation**

```typescript
const subtotal = preview?.subtotal ?? calculateSubtotal();
const taxAmount = preview?.taxAmount ?? calculateTaxAmount();
const total = preview?.total ?? calculateTotal();
```

- **الأولوية:** `preview` من الباك-اند
- **Fallback:** حساب محلي لو الـ preview مش موجود
- **الحساب المحلي:** يحترم `isTaxEnabled`

---

### 4. **UI Feedback**

```tsx
{isTaxEnabled ? formatCurrency(taxAmount) : "معفاة"}
```

- لو `isTaxEnabled = true` → عرض المبلغ: `140.00 ج.م`
- لو `isTaxEnabled = false` → عرض نص: `معفاة`
- **أفضل من:** عرض `0.00 ج.م` (أوضح للمستخدم)

---

## 📝 ملاحظات مهمة

### 1. **Type Safety**

```typescript
// ✅ كل الـ types متزامنة مع الباك-اند
interface CreatePurchaseInvoiceRequest {
  isTaxEnabled?: boolean;  // optional, default: true
}

interface PurchaseInvoice {
  isTaxEnabled: boolean;  // required في الـ response
}
```

---

### 2. **Backward Compatibility**

```typescript
isTaxEnabled?: boolean;  // optional في الـ request
```

- لو مش موجود في الـ request → الباك-اند يستخدم `true` (default)
- الفواتير الموجودة: `isTaxEnabled = true` (من Migration)

---

### 3. **Edit Mode**

```typescript
useEffect(() => {
  if (invoice && isEditMode) {
    setIsTaxEnabled(invoice.isTaxEnabled);  // ✅ تحميل من الفاتورة
  }
}, [invoice, isEditMode]);
```

- الـ Checkbox يعكس الحالة المحفوظة
- المستخدم يمكنه تغيير الحالة وحفظها

---

## 🚀 الفوائد

1. ✅ **مرونة أكبر** - المستخدم يتحكم في الضريبة لكل فاتورة
2. ✅ **UI واضح** - Checkbox بسيط وسهل الاستخدام
3. ✅ **Real-time Preview** - التحديث فوري من الباك-اند
4. ✅ **Type Safety** - كل الـ types متزامنة
5. ✅ **Backward Compatible** - الفواتير الموجودة لا تتأثر

---

## 📚 الملفات المُعدلة

```
frontend/src/types/purchaseInvoice.types.ts
frontend/src/pages/purchase-invoices/PurchaseInvoiceFormPage.tsx
```

---

**الحالة:** ✅ تم التنفيذ بنجاح  
**آخر تحديث:** 2 مايو 2026  
**المطور:** Kiro AI Assistant
