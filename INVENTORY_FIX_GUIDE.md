# 🔧 دليل إصلاح مشكلة المخزون

## 📋 المشكلة

الكميات في المخزون كانت تبقى صفر بعد إضافة منتجات أو فواتير شراء بسبب:

1. **Backend**: `PurchaseInvoiceService.ConfirmAsync` كان لا يُنشئ `StockMovement` عند إنشاء `BranchInventory` جديد
2. **Backend**: `ProductService` كان يُرجع `Product.StockQuantity` بدلاً من `BranchInventory.Quantity`
3. **Data**: منتجات موجودة بدون سجلات `BranchInventory`

## ✅ الحلول المطبقة

### 1. Backend - PurchaseInvoiceService ✓

**الملف**: `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`

**التغييرات**:
- إصلاح `ConfirmAsync`: الآن يُنشئ `StockMovement` لكل من BranchInventory الجديد والموجود
- إصلاح `CancelAsync`: الآن يُحدّث `BranchInventory` بدلاً من `Product.StockQuantity`

```csharp
// قبل: كان يُنشئ StockMovement فقط للـ BranchInventory الموجود
if (branchInventory == null) {
    // إنشاء BranchInventory بدون StockMovement ❌
} else {
    // إنشاء StockMovement ✓
}

// بعد: يُنشئ StockMovement في كل الحالات
int balanceBefore;
if (branchInventory == null) {
    // إنشاء BranchInventory
    balanceBefore = 0;
} else {
    // تحديث BranchInventory
    balanceBefore = branchInventory.Quantity;
}
// إنشاء StockMovement دائماً ✓
```

### 2. Backend - ProductService ✓

**الملف**: `backend/KasserPro.Application/Services/Implementations/ProductService.cs`

**التغييرات**:
- `GetAllAsync`: الآن يجلب الكميات من `BranchInventory` للفرع الحالي
- `GetByIdAsync`: الآن يُرجع الكمية من `BranchInventory` للفرع الحالي

```csharp
// قبل: كان يُرجع Product.StockQuantity مباشرة ❌
StockQuantity = p.StockQuantity

// بعد: يجلب من BranchInventory للفرع الحالي ✓
var branchInventories = await _unitOfWork.BranchInventories.Query()
    .Where(bi => bi.TenantId == tenantId && bi.BranchId == branchId)
    .ToDictionaryAsync(bi => bi.ProductId, bi => bi.Quantity);

StockQuantity = p.TrackInventory && branchInventories.ContainsKey(p.Id)
    ? branchInventories[p.Id]
    : (p.TrackInventory ? 0 : null);
```

### 3. Database - إصلاح البيانات الموجودة

**الملف**: `backend/KasserPro.API/fix-inventory-data.sql`

**الخطوات**:

1. إنشاء سجلات `BranchInventory` للمنتجات التي لا تملك سجلات
2. تحديث سجلات `BranchInventory` الموجودة من `Product.StockQuantity`
3. التحقق من النتائج

## 🚀 خطوات التطبيق

### 1. إيقاف التطبيق

```bash
# إيقاف Backend
# إيقاف Frontend
```

### 2. تطبيق التعديلات على الكود

التعديلات تمت بالفعل على:
- ✅ `PurchaseInvoiceService.cs`
- ✅ `ProductService.cs`

### 3. إصلاح البيانات في الداتابيس

```bash
# الانتقال لمجلد Backend
cd backend/KasserPro.API

# تطبيق السكريبت على الداتابيس
sqlite3 kasserpro.db < fix-inventory-data.sql
```

أو استخدم أي SQLite client:
- DB Browser for SQLite
- DBeaver
- VS Code SQLite extension

### 4. إعادة تشغيل التطبيق

```bash
# تشغيل Backend
cd backend/KasserPro.API
dotnet run

# تشغيل Frontend (في terminal آخر)
cd frontend
npm run dev
```

## 🧪 التحقق من الإصلاح

### 1. اختبار إضافة منتج جديد

1. افتح صفحة المنتجات
2. أضف منتج جديد بكمية أولية (مثلاً 100)
3. تحقق من أن الكمية تظهر في:
   - صفحة المنتجات
   - صفحة المخزون
   - POS

### 2. اختبار فاتورة شراء

1. افتح صفحة فواتير الشراء
2. أنشئ فاتورة شراء جديدة
3. أضف منتج بكمية (مثلاً 50)
4. احفظ الفاتورة كـ Draft
5. أكّد الفاتورة (Confirm)
6. تحقق من أن الكمية زادت في:
   - صفحة المنتجات
   - صفحة المخزون
   - جدول `BranchInventories`
   - جدول `StockMovements`

### 3. اختبار البيع من POS

1. افتح POS
2. أضف منتج للسلة
3. أكمل الطلب
4. تحقق من أن الكمية نقصت في المخزون

## 📊 التحقق من الداتابيس

```sql
-- عرض BranchInventory لكل المنتجات
SELECT 
    p.Name as ProductName,
    b.Name as BranchName,
    bi.Quantity,
    bi.ReorderLevel,
    bi.LastUpdatedAt
FROM BranchInventories bi
JOIN Products p ON p.Id = bi.ProductId
JOIN Branches b ON b.Id = bi.BranchId
WHERE bi.IsDeleted = 0
ORDER BY p.Name, b.Name;

-- عرض آخر حركات المخزون
SELECT 
    sm.CreatedAt,
    p.Name as ProductName,
    b.Name as BranchName,
    sm.Type,
    sm.Quantity,
    sm.BalanceBefore,
    sm.BalanceAfter,
    sm.Reason
FROM StockMovements sm
JOIN Products p ON p.Id = sm.ProductId
JOIN Branches b ON b.Id = sm.BranchId
ORDER BY sm.CreatedAt DESC
LIMIT 20;

-- التحقق من المنتجات بدون BranchInventory
SELECT 
    p.Id,
    p.Name,
    p.TrackInventory,
    p.StockQuantity
FROM Products p
WHERE p.TrackInventory = 1
  AND p.IsDeleted = 0
  AND NOT EXISTS (
    SELECT 1 FROM BranchInventories bi
    WHERE bi.ProductId = p.Id
  );
```

## 🎯 النتيجة المتوقعة

بعد تطبيق الإصلاحات:

✅ كل منتج Physical له سجل `BranchInventory` لكل فرع
✅ فواتير الشراء تُحدّث `BranchInventory` عند التأكيد
✅ كل تحديث للمخزون يُنشئ `StockMovement`
✅ Products API يُرجع الكمية من `BranchInventory` للفرع الحالي
✅ Frontend يعرض الكميات الصحيحة من BranchInventory

## 📝 ملاحظات مهمة

1. **Multi-Branch**: كل فرع له مخزون منفصل في `BranchInventory`
2. **Product.StockQuantity**: لم يعد يُستخدم، القيمة الصحيحة في `BranchInventory`
3. **Service Products**: لا تتبع المخزون (`TrackInventory = false`)
4. **StockMovement**: يُسجل كل حركة مخزون للتدقيق

## 🔍 استكشاف الأخطاء

### المشكلة: الكمية لا تزال صفر بعد الإصلاح

**الحل**:
1. تحقق من تطبيق سكريبت SQL
2. تحقق من أن Backend يستخدم الكود الجديد
3. امسح cache المتصفح
4. تحقق من logs في `backend/KasserPro.API/logs/`

### المشكلة: فاتورة الشراء لا تُحدّث المخزون

**الحل**:
1. تحقق من أن الفاتورة في حالة Confirmed
2. تحقق من جدول `StockMovements`
3. راجع logs للأخطاء

### المشكلة: المنتجات تظهر بكميات مختلفة في فروع مختلفة

**الحل**:
- هذا سلوك صحيح! كل فرع له مخزون منفصل
- استخدم صفحة Inventory → Product Inventory لرؤية الكميات عبر الفروع

## 📞 الدعم

إذا واجهت أي مشاكل:
1. راجع logs في `backend/KasserPro.API/logs/`
2. تحقق من الداتابيس باستخدام الاستعلامات أعلاه
3. تأكد من تطبيق كل الخطوات بالترتيب
