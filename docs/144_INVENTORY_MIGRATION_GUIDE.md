# 🔧 دليل إصلاح المخزون

## 📋 المشكلة

منتجات موجودة في النظام وعندها `StockQuantity > 0` لكن لا تظهر في صفحة المخزون.

## 🎯 الحل

تم إضافة أداة إصلاح تلقائية في صفحة الإعدادات.

## 🚀 كيفية الاستخدام

### الطريقة 1: من واجهة الإعدادات (موصى بها)

1. افتح صفحة **الإعدادات** (Settings)
2. انزل لأسفل لقسم **صيانة النظام**
3. اضغط على زر **إصلاح المخزون**
4. انتظر حتى تظهر رسالة النجاح

### الطريقة 2: من API مباشرة

```bash
# Using curl
curl -X POST http://localhost:5243/api/system/migrate-inventory \
  -H "Authorization: Bearer YOUR_TOKEN"

# Using PowerShell
Invoke-RestMethod -Uri "http://localhost:5243/api/system/migrate-inventory" `
  -Method POST `
  -Headers @{ "Authorization" = "Bearer YOUR_TOKEN" }
```

## 📊 ماذا يفعل الإصلاح؟

1. **يفحص** جميع المنتجات النشطة (`IsActive = true`)
2. **يبحث** عن منتجات بدون سجلات في `BranchInventory`
3. **ينشئ** سجل `BranchInventory` لكل فرع:
   - الكمية = `Product.StockQuantity`
   - ReorderLevel = `Product.LowStockThreshold`
4. **يتحقق** من صحة البيانات (Stock Before = Stock After)

## ✅ النتيجة المتوقعة

```json
{
  "success": true,
  "message": "Migration completed successfully",
  "data": {
    "productsMigrated": 50,
    "inventoriesCreated": 150,  // 50 products × 3 branches
    "productsWithStock": 45,
    "totalStockBefore": 1250,
    "totalStockAfter": 1250,
    "durationMs": 234,
    "alreadyMigrated": false
  }
}
```

## 🔍 التحقق من النجاح

بعد تشغيل الإصلاح:

1. افتح صفحة **المخزون**
2. تأكد من ظهور جميع المنتجات
3. تحقق من الكميات صحيحة

## 🛡️ الأمان

- ✅ **Idempotent**: يمكن تشغيله عدة مرات بأمان
- ✅ **Transaction**: كل العمليات في transaction واحدة
- ✅ **Validation**: يتحقق من صحة البيانات قبل الحفظ
- ✅ **Authorization**: Admin/SystemOwner فقط

## 📝 ملاحظات

### إذا كانت النتيجة `alreadyMigrated: true`

معناها أن كل المنتجات عندها سجلات BranchInventory بالفعل. لا حاجة لإعادة التشغيل.

### إذا فشل الإصلاح

1. تحقق من الـ logs في `logs/kasserpro-*.log`
2. تأكد من أن الـ database غير مقفل
3. تأكد من وجود فروع في النظام
4. جرب مرة أخرى

### للمطورين

الكود موجود في:
- Backend: `src/KasserPro.Infrastructure/Data/InventoryDataMigration.cs`
- Controller: `src/KasserPro.API/Controllers/SystemController.cs`
- Frontend: `client/src/pages/settings/SettingsPage.tsx`
- API: `client/src/api/systemApi.ts`

## 🔗 Related Files

- `INVENTORY_VISIBILITY_FIX.md` - شرح المشكلة الأصلية
- `fix-missing-branch-inventory.sql` - SQL script بديل
- `PRODUCT_FORM_ENHANCEMENTS.md` - تحسينات فورم المنتجات

## 🎉 بعد الإصلاح

المنتجات الجديدة ستظهر تلقائياً في المخزون لأن `ProductService.CreateAsync` الآن ينشئ سجلات BranchInventory تلقائياً.
