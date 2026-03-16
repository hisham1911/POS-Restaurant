# Number Input Pattern - نمط حقول الأرقام

## المشكلة

عند استخدام `type="number"` مع `value={0}` أو `value={formData.field}` حيث القيمة الافتراضية صفر، يظهر الصفر كقيمة فعلية في الحقل بدلاً من placeholder، مما يسبب إزعاج للمستخدم.

## الحل

استخدام empty string بدلاً من الصفر عندما تكون القيمة هي القيمة الافتراضية.

### Pattern الموحد

```tsx
// ❌ الطريقة القديمة (خاطئة)
<input
  type="number"
  value={price}
  onChange={(e) => setPrice(Number(e.target.value))}
  placeholder="0.00"
/>

// ✅ الطريقة الصحيحة
<input
  type="number"
  value={price === 0 ? "" : price}
  onChange={(e) => setPrice(Number(e.target.value) || 0)}
  placeholder="0.00"
/>
```

### Helper Functions

للحالات المعقدة، استخدم الـ helper functions:

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

## القواعد

1. **للأسعار والمبالغ**: استخدم `placeholder="0.00"` وأظهر empty string عند القيمة 0
2. **للكميات**: استخدم `placeholder="1"` وأظهر empty string عند القيمة الافتراضية (عادة 1)
3. **للنسب المئوية**: استخدم `placeholder="14"` للضريبة الافتراضية

## أمثلة

### حقل السعر
```tsx
<input
  type="number"
  step="0.01"
  min="0"
  value={price === 0 ? "" : price}
  onChange={(e) => setPrice(Number(e.target.value) || 0)}
  placeholder="0.00"
  required
/>
```

### حقل الكمية
```tsx
<input
  type="number"
  min="1"
  value={quantity === 1 ? "" : quantity}
  onChange={(e) => setQuantity(Number(e.target.value) || 1)}
  placeholder="1"
  required
/>
```

### حقل الضريبة (مع قيمة افتراضية 14%)
```tsx
<input
  type="number"
  step="0.01"
  min="0"
  max="100"
  value={taxRate === 14 ? "" : taxRate}
  onChange={(e) => setTaxRate(Number(e.target.value) || 14)}
  placeholder="14"
/>
```

## الملفات المحدثة

### Core Components
- ✅ `components/pos/CustomItemModal.tsx` - حقول السعر والكمية والضريبة
- ✅ `components/pos/ProductQuickCreateModal.tsx` - حقل السعر والمخزون
- ✅ `components/pos/StockAdjustmentModal.tsx` - حقل الكمية الجديدة
- ✅ `components/products/ProductFormModal.tsx` - جميع حقول الأسعار والمخزون
- ✅ `components/purchase-invoices/QuickAddProductModal.tsx` - حقل السعر
- ✅ `components/purchase-invoices/AddPaymentModal.tsx` - حقل المبلغ المدفوع

### Pages
- ✅ `pages/purchase-invoices/PurchaseInvoiceFormPage.tsx` - حقول الكمية والأسعار
- ✅ `pages/shifts/ShiftPage.tsx` - رصيد الافتتاح والإغلاق
- ✅ `pages/expenses/ExpenseFormPage.tsx` - حقل المبلغ
- ✅ `pages/cash-register/CashRegisterDashboard.tsx` - حقول الإيداع والسحب
- ✅ `pages/pos/POSWorkspacePage.tsx` - حقل الخصم
- ✅ `pages/settings/SettingsPage.tsx` - نسبة الضريبة وعرض الورق

### Shift Management
- ✅ `components/shifts/ForceCloseShiftModal.tsx` - الرصيد الفعلي
- ✅ `components/shifts/HandoverShiftModal.tsx` - الرصيد الحالي

### Inventory
- ✅ `components/inventory/BranchPricingEditor.tsx` - السعر المخصص
- ✅ `components/inventory/InventoryTransferForm.tsx` - حقل الكمية

### Customers
- ✅ `components/customers/DebtPaymentModal.tsx` - المبلغ المدفوع
- ✅ `components/customers/LoyaltyPointsModal.tsx` - عدد النقاط
- ✅ `components/customers/CustomerFormModal.tsx` - حد الائتمان

### Orders
- ✅ `components/orders/RefundModal.tsx` - كمية الاسترجاع

## ملاحظات

- الـ `placeholder` يظهر فقط عندما يكون الحقل فارغاً
- استخدام `|| 0` أو `|| 1` في onChange يضمن عدم حفظ قيم غير صحيحة
- هذا النمط يحسن UX بشكل كبير ويقلل الأخطاء
