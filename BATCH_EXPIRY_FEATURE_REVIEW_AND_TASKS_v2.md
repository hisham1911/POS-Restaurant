# مراجعة ميزة الباتش وتاريخ الصلاحية (Batch & Expiry) — النسخة القابلة للتنفيذ

## الهدف
توثيق مراجعة منطقية لميزة الباتش الحالية مع حسم القرارات التصميمية وتحويل الملاحظات إلى مهام دقيقة قابلة للتنفيذ المباشر بواسطة الأيجنت.

---

## الملخص التنفيذي

الميزة تمتلك **بنية بيانات Backend قوية ومنطق FEFO يعمل فعليًا**، لكنها تعاني من قصور حاد على مستوى الواجهة التشغيلية والتكامل. هناك **3 bugs حقيقية في الباكند** يجب إصلاحها قبل أي توسع في الواجهة.

---

## القرارات الأساسية — محسومة

### القرار 1: الباتش لكل المنتجات أم لبعضها؟
**القرار: `IsBatchTracked = true` افتراضيًا على كل المنتجات.**

**المبرر:**
- الباتش لا يُستخدم فقط لتتبع تاريخ الصلاحية، بل لتتبع **سعر التكلفة لكل دفعة شراء**.
- نفس المنتج ممكن يُشترى بسعرين مختلفين في وقتين مختلفين — الباتش يحافظ على هذا الفرق.
- `ExpiryDate` يظل **اختياريًا** — لو المنتج مالوش صلاحية، يُترك فارغًا.

**الأثر على التنفيذ:**
- إضافة `IsBatchTracked = true` كقيمة افتراضية في `Product` entity.
- `ExpiryDate` في `ProductBatch` يظل nullable.
- الواجهة لا تُجبر المستخدم على إدخال `ExpiryDate`.

---

### القرار 2: المرتجع يرجع فين؟
**القرار: الكمية ترجع للباتش الأصلي إذا كان `OrderItem.BatchId` معروفًا وما زال نشطًا.**

**المبرر:**
- الباتش يتتبع سعر التكلفة — إرجاع الكمية للباتش الصحيح يحافظ على دقة حسابات التكلفة.
- إذا كان الباتش منتهيًا أو محذوفًا: الكمية ترجع للمخزون العام بدون باتش محدد.

**الأثر على التنفيذ:**
- تعديل `IncrementStockAsync` في `InventoryService` لاستقبال `batchId` اختياري.
- عند وجود `batchId` نشط: تحديث `ProductBatch.Quantity += qty`.
- عند غياب الباتش أو انتهائه: تحديث `BranchInventory` فقط كما هو الآن.

---

### القرار 3: حذف الباتش
**القرار: لا حذف إذا `Quantity > 0` أو له حركات. البديل: "تعطيل" (Deactivate).**

---

### القرار 4: من يرى تنبيهات الصلاحية؟
**القرار: أي مستخدم لديه Permission `InventoryView`** — وليس بناءً على Role.

---

### القرار 5: صفحة الباتشات
**القرار: صفحة مستقلة تحت قسم المخزون** — لأنها تحتاج فلاتر وعرضًا مكثفًا.

---

### القرار 6: إنشاء الباتش اليدوي
**القرار: استثناء إداري فقط.** الباتش الطبيعي يُنشأ تلقائيًا من فاتورة الشراء.

---

## ما الموجود حاليًا

### Backend ✓
- `ProductBatch` entity: `BatchNumber`, `ExpiryDate`, `Quantity`, `InitialQuantity`, `CostPrice`, `Status` (Active/Expired/Depleted), `PurchaseInvoiceId`.
- `Tenant` settings: `ExpiryAlertDays` (default 30), `AllowExpiredSales` (default false).
- `ProductBatchService`: CRUD + alerts + query by product/branch/status.
- `BatchDecrementStockAsync`: FEFO حقيقي يخصم من الأقرب انتهاءً أولًا.
- `PurchaseInvoiceService`: ينشئ `ProductBatch` تلقائيًا عند تأكيد الفاتورة.
- `OrderItem`: يحفظ `BatchId`, `BatchNumber`, `ExpiryDate`.
- `StockMovement`: يحفظ `BatchId` للـ audit.
- `StockTakingItem`: يدعم `BatchId` للجرد على مستوى الباتش.

### Frontend ✓
- `productBatchApi.ts`: RTK Query للباتشات.
- `BatchExpiryAlertBanner`: banner ملخص.
- `PurchaseInvoiceFormPage`: حقول `BatchNumber`, `ExpiryDate`, `ProductionDate`.
- `StockTakingPage`: `BatchSelector` لاختيار الباتش أثناء الجرد.

### غير موجود ✗
- صفحة إدارة الباتشات (Route + Page + Navigation).
- Action button في الـ Banner.
- ظهور الباتش في تفاصيل المنتج والمخزون.
- واجهة إعدادات `ExpiryAlertDays` / `AllowExpiredSales`.
- `IsBatchTracked` في `Product` entity وكل الـ DTOs.

---

## البـags الحرجة — يجب إصلاحها أولًا

### Bug 1: المرتجع لا يُعيد الكمية للباتش
**الموقع:** `InventoryService.IncrementStockAsync`
**المشكلة:** يزيد `BranchInventory` فقط — لا يُعيد `ProductBatch.Quantity`.
**النتيجة:** `sum(Batch.Quantity) ≠ BranchInventory.Quantity` مع مرور الوقت.

### Bug 2: إلغاء فاتورة الشراء لا يُعيد الباتش
**الموقع:** `PurchaseInvoiceService.CancelAsync`
**المشكلة:** يخصم من `BranchInventory` لكن لا يُعيد `ProductBatch.Quantity`.

### Bug 3: CreateAsync بدون Transaction
**الموقع:** `ProductBatchService.CreateAsync`
**المشكلة:** إنشاء Batch + تعديل BranchInventory + StockMovement بدون `await using var transaction`.

### Bug 4: `isBatchTracked` غير موجودة في Backend
**الموقع:** `StockTakingPage` تستخدم `p.isBatchTracked` — لكن الخاصية غير موجودة في `Product` entity أو `BranchInventoryDto`.
**النتيجة:** `BatchSelector` لن يظهر تلقائيًا لأي منتج.

---

## التاسكات — مرتبة حسب الأولوية

---

## Phase D — إصلاح الباكند (أولوية عالية — ابدأ هنا)

### Task D1 — إضافة Transaction على `ProductBatchService.CreateAsync`
**الملف:** `KasserPro.Infrastructure/Services/ProductBatchService.cs`
**المطلوب:**
```
await using var transaction = await _context.Database.BeginTransactionAsync();
try {
    // إنشاء ProductBatch
    // تعديل BranchInventory
    // إنشاء StockMovement
    await transaction.CommitAsync();
} catch {
    await transaction.RollbackAsync();
    throw;
}
```
**معيار الاكتمال:** لو فشل إنشاء StockMovement، لا يُحفظ الباتش ولا يتغير المخزون.

---

### Task D2 — إصلاح المرتجع ليُعيد الكمية للباتش الأصلي
**الملفات:**
- `KasserPro.Infrastructure/Services/InventoryService.cs` → `IncrementStockAsync`
- `KasserPro.Infrastructure/Services/OrderService.cs` → كود المرتجع

**المطلوب:**
1. تعديل signature: `IncrementStockAsync(branchInventoryId, qty, batchId? = null, ...)`
2. إذا `batchId != null`:
   - جلب `ProductBatch` بشرط `Id == batchId && Status == Active && TenantId + BranchId صحيحان`
   - `batch.Quantity += qty`
   - لو `batch.Status == Depleted`: أعده لـ `Active`
3. إذا `batchId == null` أو الباتش غير نشط: السلوك الحالي بدون تغيير.
4. في `OrderService` عند المرتجع: مرر `orderItem.BatchId` لـ `IncrementStockAsync`.

**معيار الاكتمال:** بعد مرتجع، `sum(ActiveBatches.Quantity)` يساوي `BranchInventory.Quantity` للمنتج.

---

### Task D3 — إصلاح إلغاء فاتورة الشراء
**الملف:** `KasserPro.Infrastructure/Services/PurchaseInvoiceService.cs` → `CancelAsync`

**المطلوب:**
1. عند `adjustInventory = true`، بعد خصم `BranchInventory`:
2. جلب الباتشات المرتبطة بـ `PurchaseInvoiceId` الحالية.
3. لكل باتش: `batch.Quantity -= invoiceItem.Quantity` (لا تنزل عن صفر).
4. لو `batch.Quantity == 0`: `batch.Status = Depleted`.
5. تسجيل `StockMovement` من نوع `PurchaseCancellation` لكل باتش.

**معيار الاكتمال:** بعد إلغاء فاتورة شراء، `ProductBatch.Quantity` يعكس الكمية الفعلية.

---

### Task D4 — تقييد حذف الباتش
**الملف:** `KasserPro.Infrastructure/Services/ProductBatchService.cs` → `DeleteAsync`

**المطلوب:**
1. قبل الحذف، تحقق:
   - `batch.Quantity > 0` → throw: "لا يمكن حذف باتش له كمية في المخزون"
   - يوجد `OrderItem` مرتبط → throw: "لا يمكن حذف باتش مرتبط بطلبات"
   - يوجد `StockMovement` مرتبط → throw: "لا يمكن حذف باتش له حركات مخزون"
2. إذا نجح التحقق: Soft delete كما هو.
3. إضافة error codes في `ErrorCodes.cs`:
   - `BATCH_HAS_QUANTITY`
   - `BATCH_HAS_ORDERS`
   - `BATCH_HAS_MOVEMENTS`

**معيار الاكتمال:** محاولة حذف باتش له كمية تُعيد `400 Bad Request` برسالة عربية واضحة.

---

### Task D5 — إضافة `IsBatchTracked` في `Product`
**الملفات:**
- `KasserPro.Domain/Entities/Product.cs`
- `KasserPro.Application/DTOs/Products/ProductDto.cs`
- `KasserPro.Application/DTOs/Inventory/BranchInventoryDto.cs`
- Migration جديدة

**المطلوب:**
1. إضافة `public bool IsBatchTracked { get; set; } = true;` في `Product` entity.
2. إضافة `IsBatchTracked` في `ProductDto` و `BranchInventoryDto`.
3. إضافة Migration: `ALTER TABLE Products ADD IsBatchTracked BIT NOT NULL DEFAULT 1`
4. تحديث `CreateProductRequest` و `UpdateProductRequest` ليقبلا `IsBatchTracked`.

**معيار الاكتمال:** كل المنتجات الحالية تصبح `IsBatchTracked = true` تلقائيًا بعد Migration. خاصية `p.isBatchTracked` في `StockTakingPage` تعمل فعليًا.

---

### Task D6 — إضافة فحص `ExpiryAlertDays > 0`
**الملف:** `ProductBatchService.GetExpiryAlertsAsync`

**المطلوب:**
```csharp
if (tenant.ExpiryAlertDays <= 0) return new List<ProductBatchAlertDto>();
```

**معيار الاكتمال:** إذا `ExpiryAlertDays = 0`، لا تُرجع تنبيهات.

---

## Phase A — صفحة إدارة الباتشات (أولوية عالية)

### Task A1 — إنشاء `ProductBatchesPage`
**الملف الجديد:** `frontend/src/pages/inventory/ProductBatchesPage.tsx`

**المطلوب:**
جدول يعرض الأعمدة:
- رقم الباتش (BatchNumber)
- اسم المنتج
- الكمية الحالية
- سعر التكلفة (CostPrice)
- تاريخ الصلاحية (ExpiryDate) — "غير محدد" إذا null
- الأيام المتبقية (محسوبة من ExpiryDate - اليوم) — فارغة إذا ExpiryDate null
- الحالة (Active / Expired / Depleted) بألوان مميزة
- اسم الفرع

فلاتر:
- حسب الحالة (Active / Expired / Depleted / الكل)
- حسب الفرع
- حسب المنتج (بحث نصي)
- "قريب الانتهاء فقط" (checkbox يُظهر Batches ينتهي خلال `ExpiryAlertDays`)

**معيار الاكتمال:** الصفحة تعرض كل الباتشات مع إمكانية الفلترة بدون أخطاء TypeScript.

---

### Task A2 — إضافة Route
**الملف:** `frontend/src/App.tsx` أو ملف الـ routes

**المطلوب:**
```tsx
<Route path="/product-batches" element={<ProductBatchesPage />} />
```
مع حماية بـ Permission `InventoryView`.

**معيار الاكتمال:** الـ Route يعمل ويُحوّل للصفحة الصحيحة.

---

### Task A3 — إضافة Navigation Link
**الملف:** ملف الـ Navigation/Sidebar

**المطلوب:**
- إضافة "الباتشات" أو "دُفعات المخزون" تحت قسم المخزون.
- ظهوره مشروط بـ Permission `InventoryView`.

**معيار الاكتمال:** الرابط يظهر في الـ Sidebar لأي مستخدم لديه `InventoryView`.

---

## Phase C — تحسين تنبيهات الصلاحية (أولوية متوسطة)

### Task C1 — إضافة Action في `BatchExpiryAlertBanner`
**الملف:** `frontend/src/components/inventory/BatchExpiryAlertBanner.tsx`

**المطلوب:**
1. إضافة زر "عرض الباتشات المنتهية" → ينتقل لـ `/product-batches?status=Expired`.
2. إضافة زر "عرض القريبة من الانتهاء" → ينتقل لـ `/product-batches?status=NearExpiry`.

**معيار الاكتمال:** الضغط على الزر يفتح الصفحة مع الفلتر المناسب مُفعَّلًا مسبقًا.

---

### Task C2 — ربط ظهور البانر بالـ Permission
**الملف:** `BatchExpiryAlertBanner.tsx` + أماكن استخدامه

**المطلوب:**
- استبدال فحص `role === 'Admin' || role === 'SystemOwner'` بـ `hasPermission('InventoryView')`.

**معيار الاكتمال:** أي مستخدم لديه `InventoryView` يرى البانر بغض النظر عن الـ Role.

---

## Phase E — تكامل مع بقية النظام (أولوية متوسطة)

### Task E1 — إظهار الباتشات في صفحة تفاصيل المنتج
**الملف:** صفحة/modal تفاصيل المنتج

**المطلوب:**
- إضافة Tab أو Section "الدُفعات النشطة" يعرض:
  - BatchNumber، Quantity، CostPrice، ExpiryDate (أو "غير محدد")، تاريخ الإنشاء.
- Fetch من `/api/product-batches?productId={id}&status=Active`.

**معيار الاكتمال:** من صفحة المنتج، المستخدم يرى قائمة دُفعاته النشطة بكمياتها وأسعارها.

---

### Task E2 — إظهار BatchNumber في تفاصيل الطلب
**الملف:** `OrderDetailsModal` أو مكون تفاصيل الطلب

**المطلوب:**
- في جدول بنود الطلب، إضافة عمود "رقم الدُفعة" يعرض `OrderItem.BatchNumber`.
- إذا `BatchNumber = null`: يعرض "—".

**معيار الاكتمال:** من تفاصيل أي طلب، المستخدم يرى الباتش المستهلك في كل بند.

---

### Task E3 — واجهة إعدادات الصلاحية
**المطلوب:**
- إضافة في صفحة إعدادات الـ Tenant:
  - حقل رقمي: "عدد أيام التنبيه قبل انتهاء الصلاحية" (`ExpiryAlertDays`)
  - Toggle: "السماح ببيع المنتجات المنتهية الصلاحية" (`AllowExpiredSales`)
- Endpoint موجود في Backend — المطلوب فقط الواجهة.

**معيار الاكتمال:** تغيير `ExpiryAlertDays` من الواجهة يؤثر فعليًا على تنبيهات البانر.

---

## Phase F — الصلاحيات (أولوية متوسطة)

### Task F1 — مراجعة وتوحيد صلاحيات الباتش
**المطلوب:**
تحديد وتطبيق هذا الجدول في الـ Controller والـ Frontend:

| العملية | الـ Permission المطلوب |
|---|---|
| عرض قائمة الباتشات | `InventoryView` |
| عرض تفاصيل باتش | `InventoryView` |
| إنشاء باتش يدوي | `InventoryManage` |
| تعديل باتش | `InventoryManage` |
| حذف/تعطيل باتش | `InventoryManage` |
| رؤية التنبيهات | `InventoryView` |

**معيار الاكتمال:** لا يوجد Endpoint أو عنصر UI يعتمد على الـ Role مباشرة في سياق الباتشات.

---

## ترتيب التنفيذ المقترح للأيجنت

```
1. D5 — إضافة IsBatchTracked (Migration) ← الأساس الذي يعتمد عليه الجرد
2. D1 — Transaction على CreateAsync ← تأمين البيانات
3. D2 — إصلاح المرتجع ← Bug حقيقي في البيانات
4. D3 — إصلاح إلغاء فاتورة الشراء ← Bug حقيقي في البيانات
5. D4 — تقييد حذف الباتش ← حماية
6. D6 — فحص ExpiryAlertDays ← بسيط
7. A1 + A2 + A3 — صفحة الباتشات كاملة
8. C1 + C2 — تحسين البانر
9. E1 + E2 — تكامل مع تفاصيل المنتج والطلب
10. E3 — إعدادات الصلاحية
11. F1 — توحيد الصلاحيات
```

---

## قيود معروفة مؤجلة

- **تتبع الباتش في التحويلات بين الفروع:** التحويل الحالي يحرك `BranchInventory` فقط بدون تتبع الباتش. مؤجل للـ Phase 2.
- **شفافية FEFO في POS:** المستخدم لا يرى أي باتش سيُستهلك. مؤجل للـ Phase 2.

---

## الحالة الحالية
**البيانات قوية، FEFO يعمل، لكن 4 bugs حقيقية يجب إصلاحها أولًا قبل أي توسع في الواجهة. ترتيب التنفيذ أعلاه يضمن بناء صحيح من الأساس.**
