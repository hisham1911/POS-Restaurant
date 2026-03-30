# 🧹 تنظيف Product.StockQuantity

## 🔍 الوضع الحالي

`Product.StockQuantity` لسه موجود في الكود ولسه بيُستخدم في أماكن معينة!

## 📍 الاستخدامات الموجودة

### 1. ✅ Migrations (صح - ضروري)
**الملفات**:
- `20260311153232_InitialCreate.cs`
- `AppDbContextModelSnapshot.cs`

**الاستخدام**: تعريف الـ Column في الداتابيس
**الحالة**: ✅ صح - لازم يفضل موجود للـ backward compatibility

### 2. ⚠️ ProductReportService (Fallback)
**الملف**: `backend/KasserPro.Infrastructure/Services/ProductReportService.cs`

**الكود**:
```csharp
var branchInv = product.BranchInventories.FirstOrDefault(bi => bi.BranchId == branchId);
var currentStock = branchInv?.Quantity ?? product.StockQuantity ?? 0;
```

**المشكلة**: بيستخدم `StockQuantity` كـ fallback لو `BranchInventory` مش موجود
**الحل**: بعد التعديلات الجديدة، كل منتج لازم يبقى عنده `BranchInventory`، فالـ fallback مش محتاج

### 3. ❌ Seeders (غلط - لازم يتصلح!)
**الملفات**:
- `SupermarketSeeder.cs`
- `RestaurantSeeder.cs`
- `HomeAppliancesSeeder.cs`
- `RealisticDataSeeder.cs`
- `MultiTenantSeeder.cs`

**المشكلة**: الـ Seeders بتحدث `Product.StockQuantity` بدل `BranchInventory`!

**مثال من SupermarketSeeder**:
```csharp
// ❌ غلط
var product = await context.Products.FindAsync(item.ProductId);
if (product != null && product.StockQuantity.HasValue)
{
    product.StockQuantity -= item.Quantity;  // ❌ يحدث الـ deprecated field
    product.LastStockUpdate = order.CompletedAt ?? order.CreatedAt;
}
```

**المفروض**:
```csharp
// ✅ صح
var branchInventory = await context.BranchInventories
    .FirstOrDefaultAsync(bi => bi.ProductId == item.ProductId 
                            && bi.BranchId == order.BranchId);
if (branchInventory != null)
{
    branchInventory.Quantity -= item.Quantity;  // ✅ يحدث BranchInventory
    branchInventory.LastUpdatedAt = DateTime.UtcNow;
}
```

### 4. ✅ DataValidationService (صح)
**الملف**: `backend/KasserPro.Infrastructure/Services/DataValidationService.cs`

**الاستخدام**: التحقق من صحة البيانات في الداتابيس
**الحالة**: ✅ صح - جزء من الـ validation

### 5. ✅ InventoryDataMigration (صح)
**الملف**: `backend/KasserPro.Infrastructure/Data/InventoryDataMigration.cs`

**الاستخدام**: Migration script لنقل البيانات من `StockQuantity` إلى `BranchInventory`
**الحالة**: ✅ صح - هذا هو الغرض منه

## 🎯 الخطة

### المرحلة 1: إصلاح الـ Seeders ✓ (نعملها دلوقتي)
- تحديث كل الـ Seeders لاستخدام `BranchInventory`
- إزالة أي تحديثات على `Product.StockQuantity`

### المرحلة 2: تحديث ProductReportService
- إزالة الـ fallback على `StockQuantity`
- الاعتماد على `BranchInventory` فقط
- إضافة error handling لو `BranchInventory` مش موجود

### المرحلة 3: الإبقاء على الـ Column
- `Product.StockQuantity` يفضل موجود في الداتابيس
- لكن دائماً = 0
- للـ backward compatibility مع أي كود قديم

## 📊 الوضع المستهدف

```
Product Table:
├─ StockQuantity = 0 (always)  ← موجود بس مش مستخدم
└─ TrackInventory = true/false

BranchInventory Table:
├─ Quantity ← المصدر الموثوق الوحيد ✓
└─ LastUpdatedAt

StockMovements Table:
└─ كل حركة مسجلة ✓
```

## ⚠️ ملاحظات مهمة

1. **لا تحذف الـ Column**: حذف `StockQuantity` من الداتابيس يحتاج migration وممكن يكسر كود قديم
2. **دائماً = 0**: بعد التعديلات، `StockQuantity` لازم يكون 0 دائماً
3. **BranchInventory هو المصدر**: أي كود يقرأ الكمية لازم يستخدم `BranchInventory`
4. **Seeders محتاجة تصليح**: الـ Seeders القديمة بتستخدم `StockQuantity` غلط

## 🔧 الإصلاحات المطلوبة

### Priority 1: إصلاح Seeders
- [ ] SupermarketSeeder.cs
- [ ] RestaurantSeeder.cs  
- [ ] HomeAppliancesSeeder.cs
- [ ] RealisticDataSeeder.cs (جزئياً - بيستخدم BranchInventory صح)

### Priority 2: تحديث Reports
- [ ] ProductReportService.cs - إزالة fallback

### Priority 3: Documentation
- [ ] توثيق أن StockQuantity deprecated
- [ ] إضافة تعليقات في الكود
