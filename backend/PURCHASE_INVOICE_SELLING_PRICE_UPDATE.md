# تحديث فواتير المشتريات - إضافة سعر البيع

## 📋 نظرة عامة

تم تحديث نظام فواتير المشتريات لإضافة حقل **سعر البيع (Selling Price)** بجانب سعر الشراء، مع دعم كامل للمنتجات الخدمية.

## ✅ التحديثات المنفذة

### Backend

#### 1. Entity Updates
- ✅ `PurchaseInvoiceItem.cs` - إضافة `SellingPrice` property
- ✅ Migration SQL لإضافة العمود في قاعدة البيانات

#### 2. DTOs Updates
- ✅ `CreatePurchaseInvoiceItemRequest` - إضافة `SellingPrice`
- ✅ `UpdatePurchaseInvoiceItemRequest` - إضافة `SellingPrice`
- ✅ `PurchaseInvoiceItemDto` - إضافة `SellingPrice`

#### 3. Service Updates
- ✅ `PurchaseInvoiceService.CreateAsync()` - حفظ سعر البيع
- ✅ `PurchaseInvoiceService.UpdateAsync()` - تحديث سعر البيع
- ✅ `PurchaseInvoiceService.MapToDto()` - تضمين سعر البيع في الـ DTO

### Frontend

#### 1. Types Updates
- ✅ `purchaseInvoice.types.ts` - تحديث جميع الـ interfaces

#### 2. Form Updates
- ✅ `PurchaseInvoiceFormPage.tsx`:
  - إضافة state لـ `sellingPrice`
  - تحديث `handleAddItem()` للتحقق من سعر البيع
  - Auto-fill سعر البيع عند اختيار المنتج
  - تحديث الجدول لعرض سعر البيع
  - تحديث grid من 5 أعمدة إلى 6 أعمدة

#### 3. Modal Updates
- ✅ `QuickAddProductModal.tsx`:
  - إضافة dropdown لاختيار نوع المنتج (مادي/خدمة)
  - دعم `ProductType` enum
  - توضيح الفرق بين المنتجات المادية والخدمية

## 🎯 الميزات الجديدة

### 1. سعر البيع في الفاتورة
```typescript
interface PurchaseInvoiceItem {
  purchasePrice: number;  // سعر الشراء (التكلفة)
  sellingPrice: number;   // سعر البيع (للعميل)
  quantity: number;
  total: number;          // الإجمالي = الكمية × سعر الشراء
}
```

### 2. دعم المنتجات الخدمية
- المنتجات المادية: تتتبع المخزون
- الخدمات: لا تتتبع المخزون (مثل: استشارات، صيانة)

### 3. Auto-fill ذكي
- عند اختيار منتج، يتم ملء سعر البيع تلقائياً من سعر المنتج الحالي
- يمكن تعديل السعر حسب الحاجة

## 📊 UI Updates

### قبل التحديث
```
| المنتج | الكمية | سعر الشراء | الإجمالي | ملاحظات | إجراءات |
```

### بعد التحديث
```
| المنتج | الكمية | سعر الشراء | سعر البيع | الإجمالي | ملاحظات | إجراءات |
```

## 🔧 Migration

### ✅ Migration تم تنفيذه بنجاح!

تم إضافة عمود `SellingPrice` إلى جدول `PurchaseInvoiceItems` بنجاح.

#### النتائج:
- ✅ العمود تم إضافته: `SellingPrice REAL NOT NULL DEFAULT 0`
- ✅ تم تحديث 25 سجل موجود بأسعار البيع من جدول المنتجات
- ✅ جميع السجلات الآن لديها قيمة `SellingPrice`

#### Schema الجديد:
```
14|SellingPrice|REAL|1|0|0
```

### إذا أردت تشغيل Migration يدوياً مرة أخرى:
```powershell
cd backend/KasserPro.API
.\run-migration.ps1
```

## ✅ Validation Rules

```typescript
// Frontend Validation
if (sellingPrice <= 0) {
  toast.error('يرجى إدخال سعر البيع');
  return;
}

// Backend Validation
// يتم التحقق من وجود القيمة في DTO
```

## 🧪 Testing Checklist

- [x] ✅ Migration تم تنفيذه بنجاح (25 سجل تم تحديثه)
- [ ] إنشاء فاتورة شراء جديدة مع سعر بيع
- [ ] تعديل فاتورة موجودة وتحديث سعر البيع
- [ ] إضافة منتج مادي جديد من المودال
- [ ] إضافة خدمة جديدة من المودال
- [ ] التحقق من Auto-fill لسعر البيع
- [ ] عرض الفاتورة والتأكد من ظهور سعر البيع

## 📝 ملاحظات مهمة

1. **سعر الشراء vs سعر البيع**:
   - سعر الشراء: التكلفة من المورد
   - سعر البيع: السعر للعميل النهائي
   - الإجمالي: يُحسب من سعر الشراء فقط

2. **المنتجات الخدمية**:
   - لا تتتبع الكمية في المخزون
   - يمكن إضافتها في فواتير الشراء
   - مفيدة للخدمات المشتراة (استشارات، صيانة، إلخ)

3. **Backward Compatibility**:
   - Migration يحدث البيانات القديمة تلقائياً
   - لا يوجد breaking changes

## 🎉 الخلاصة

تم تحديث نظام فواتير المشتريات بنجاح لدعم:
- ✅ سعر البيع بجانب سعر الشراء
- ✅ المنتجات الخدمية
- ✅ Auto-fill ذكي
- ✅ UI محسّن
- ✅ Migration آمن للبيانات الموجودة
