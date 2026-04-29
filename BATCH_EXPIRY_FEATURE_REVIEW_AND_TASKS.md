# مراجعة ميزة الباتش وتاريخ الصلاحية (Batch & Expiry) — النسخة المحدّثة

## الهدف
توثيق مراجعة منطقية لميزة الباتش الحالية، حسم القرارات التصميمية، وتحويل الملاحظات إلى مهام قابلة للتنفيذ. لا يتضمن تنفيذًا فعليًا.

---

## الملخص التنفيذي

الميزة تمتلك **بنية بيانات Backend قوية ومنطق FEFO يعمل فعليًا**، لكنها تعاني من قصور حاد على مستوى الواجهة التشغيلية والتكامل والشفافية. الميزة موجودة كـ Data Model + FEFO Logic + Banner، لكنها **ليست Feature تشغيلية مكتملة**.

---

## ما الموجود حاليًا

### Backend
- `ProductBatch` entity: BatchNumber, ExpiryDate, Quantity, InitialQuantity, CostPrice, Status (Active/Expired/Depleted), PurchaseInvoiceId.
- `Tenant` settings: `ExpiryAlertDays` (default 30), `AllowExpiredSales` (default false).
- `ProductBatchService`: CRUD + alerts + query by product/branch/status.
- `ProductBatchesController`: endpoints protected by `InventoryView` / `InventoryManage`.
- `BatchDecrementStockAsync` in `InventoryService`: real FEFO — deducts from nearest expiry first.
- `PurchaseInvoiceService`: auto-creates `ProductBatch` on invoice confirmation when batch data provided.
- `OrderItem`: stores `BatchId`, `BatchNumber`, `ExpiryDate`.
- `StockMovement`: stores `BatchId` for audit.
- `StockTakingItem`: supports `BatchId` for batch-level counting.

### Frontend
- `productBatchApi.ts`: RTK Query for batch CRUD and expiry alerts.
- `BatchExpiryAlertBanner`: summary banner showing expired + near-expiry counts.
- `PurchaseInvoiceFormPage`: has BatchNumber, ExpiryDate, ProductionDate fields.
- `StockTakingPage`: contains `BatchSelector` for choosing batch during stock taking.
- `POSPage`, `InventoryPage`: host the `BatchExpiryAlertBanner`.

### غير موجود
- ❌ Page to manage/view all batches with filters.
- ❌ Route or Navigation link for batches.
- ❌ Batch display in product/inventory detail pages.
- ❌ Action button in the alert banner (no "view details", no navigation).
- ❌ UI for tenant settings `ExpiryAlertDays` / `AllowExpiredSales`.
- ❌ Batch transparency at POS (user cannot see which batch will be consumed).
- ❌ Page for handling expired batches (write-off, block, transfer).

---

## تحليل الـ Workflows

### 1. الاستلام (الشراء)
```
فاتورة شراء → يُدخل BatchNumber/ExpiryDate (اختياري)
→ عند التأكيد: ينشئ ProductBatch + يزيد BranchInventory + يسجل StockMovement (Receiving)
```
**ملاحظة:** إذا لم تُدخل بيانات باتش، لا يُنشأ ProductBatch. هذا صحيح.

### 2. البيع (FEFO)
```
طلب → BatchDecrementStockAsync
→ BranchInventory -= qty
→ FEFO: يخصم من الباتش الأقرب انتهاءً
→ تحديث ProductBatch.Quantity + Status (Depleted إذا 0)
→ تسجيل StockMovement (Sale) مع BatchId
→ تحديث OrderItem بـ BatchId/BatchNumber/ExpiryDate
```
**ملاحظة:** FEFO يعمل فعليًا في Backend، لكن المستخدم لا يرى أي شيء.

### 3. المرتجع — **مشكلة خطيرة**
```
Refund → IncrementStockAsync
→ BranchInventory += qty
→ StockMovement (Refund)
```
**العيب الكبير:** المرتجع **لا يُعيد الكمية إلى الباتش الأصلي**. النتيجة: `sum(Batch.Quantity) ≠ BranchInventory.Quantity`. هذا يُفسد دقة تتبع الباتش.

### 4. التحويل بين الفروع — **مشكلة**
```
Transfer → خصم/إضافة على BranchInventory فقط
```
**العيب:** لا يُتبع الباتش في التحويل. لا يُحول باتش A من فرع 1 إلى فرع 2.

### 5. الجرد — **مشكلة خطيرة**
```
StockTakingPage تستخدم `p.isBatchTracked` لتظهر BatchSelector
```
**العيب:** لا يوجد خاصية `IsBatchTracked` في كيان `Product` في Backend، ولا في `BranchInventoryDto`. النتيجة: `p.isBatchTracked ?? false` = دائمًا `false`. **BatchSelector لن يظهر تلقائيًا لأي منتج**.

### 6. التنبيهات
```
Banner يستطلع كل 5 دقائق
→ يُحدّث حالة الباتشات المنتهية إلى Expired تلقائيًا
→ يعرض ملخص رقمي فقط
```
**العيب:** لا Action واضح، والظهور مقصور على `Admin`/`SystemOwner` بالـ role (مخالف لنموذج الصلاحيات).

---

## المشاكل المفصلة

### Backend

#### 1. إنشاء باتش يدوي بدون Transaction
`ProductBatchService.CreateAsync` ينشئ Batch + يعدّل BranchInventory + يسجل StockMovement بدون `await using var transaction`. العملية غير ذرية.

#### 2. حذف الباتش غير محمي
`DeleteAsync` يعمل soft delete مباشرة بدون فحص:
- هل `Quantity > 0`؟
- هل مرتبط بـ `OrderItem`؟
- هل مرتبط بـ `PurchaseInvoiceItem`؟
- هل له `StockMovement`؟

#### 3. المرتجع لا يعيد للباتش
`IncrementStockAsync` يزيد `BranchInventory` فقط. لا يوجد إعادة كمية إلى الباتش المستهلَك أصلًا.

#### 4. إلغاء فاتورة شراء لا يُعيد الباتش
`CancelAsync` في `PurchaseInvoiceService` يخصم من `BranchInventory` لكن لا يُعيد `ProductBatch.Quantity`.

#### 5. لا يوجد `IsBatchTracked` في Product
الواجهة تفترض وجودها لكنها غير موجودة في Backend. لا يوجد طريقة لمعرفة أي منتجات تُتبع بالباتش.

#### 6. لا يوجد فحص BatchExpiryAlertDays > 0
`GetExpiryAlertsAsync` لا يتحقق من أن `ExpiryAlertDays` قيمة منطقية (مثلاً > 0).

### Frontend / UX

#### 1. لا توجد صفحة إدارة الباتشات
API موجود لكن لا يوجد Route أو Page أو Navigation لعرض/فلترة/إدارة الباتشات.

#### 2. التنبيه بلا Action
`BatchExpiryAlertBanner` يعرض أرقامًا فقط بدون:
- زر "عرض التفاصيل"
- انتقال لقائمة الباتشات المتأثرة
- إرشاد للمستخدم لما يفعل

#### 3. Banner مقصور على Role
الظهور مقصور على `Admin` / `SystemOwner` بالـ role. يجب أن يكون بالـ Permission (مثل `InventoryView`).

#### 4. لا شفافية في POS
المستخدم لا يعرف أي باتش سيُستهلك عند البيع. لا يُعرض BatchNumber في:
- ProductGrid
- Cart
- OrderDetails

#### 5. لا توجد صفحة إعدادات الصلاحية
`ExpiryAlertDays` و `AllowExpiredSales` موجودان في `Tenant` لكن لا توجد واجهة لتعديلهما.

### Information Architecture

#### 1. لا يوجد Entry Point واضح
لا Route، لا Navigation Item، لا Link من صفحة المنتج/المخزون.

#### 2. الباتش غير مرئي في أماكن القرار
لا يظهر في:
- صفحة تفاصيل المنتج
- قائمة المخزون
- نتائج البحث
- تفاصيل الطلب (في الواجهة)

---

## التصور الصحيح للميزة

الميزة يجب أن تُبنى على 5 أجزاء:

1. **عرض ومتابعة الباتشات**
   - قائمة بكل الباتشات حسب المنتج/الفرع/الحالة.
   - فلترة حسب قريب الانتهاء/منتهي/نشط/نافد.
   - تفاصيل كاملة لكل باتش.

2. **تنبيهات الصلاحية**
   - تنبيه ملخص + شاشة تفصيلية.
   - أولويات واضحة (critical/warning).
   - Action مباشر من التنبيه.

3. **إجراءات تشغيلية**
   - مراجعة الباتش المنتهي.
   - Write-off (تسوية كمية).
   - حظر/سماح بالبيع حسب إعدادات Tenant.

4. **تكامل مع الشراء والبيع والمخزون**
   - رؤية الباتش عند الشراء.
   - شفافية FEFO في البيع.
   - إظهار الباتش في تفاصيل المنتج والمخزون.

5. **إعدادات الإدارة**
   - `ExpiryAlertDays`.
   - `AllowExpiredSales`.
   - `IsBatchTracked` لكل منتج (أو فئة).

---

## التاسكات المطلوبة

### Phase A — بناء نقطة الدخول

**A1.** إنشاء صفحة `ProductBatchesPage` لعرض/فلترة/إدارة الباتشات.

**A2.** إضافة Route `/product-batches`.

**A3.** إضافة Navigation link ضمن قسم المخزون.

---

### Phase B — تحسين تجربة العرض والمتابعة

**B1.** جدول الباتشات مع:
- BatchNumber, ProductName, Quantity, ExpiryDate, DaysUntilExpiry, Status, BranchName.

**B2.** فلاتر حسب: المنتج، الفرع، الحالة، قريب الانتهاء، منتهي.

**B3.** Modal/Drawer لتفاصيل الباتش مع حركاته المرتبطة.

---

### Phase C — تحويل التنبيهات إلى Workflow

**C1.** إضافة Action في `BatchExpiryAlertBanner`:
- "عرض الباتشات المنتهية".
- "عرض القريبة من الانتهاء".

**C2.** ربط ظهور التنبيه بالـ Permission `InventoryView` بدل الـ role.

**C3.** جعل الإخفاء مؤقتًا على مستوى الجلسة (session) فقط، لا persist.

---

### Phase D — تقوية منطق Backend

**D1.** تغليف `ProductBatchService.CreateAsync` في `await using var transaction`.

**D2.** إضافة قيود على `DeleteAsync`:
- لا حذف إذا `Quantity > 0`.
- لا حذف إذا مرتبط بـ `OrderItem` أو `StockMovement`.
- أو: تحويل الحذف إلى "deactivate" بدل soft delete مباشر.

**D3.** إصلاح المرتجع: عند Refund، إعادة الكمية إلى الباتش المُسجل في `OrderItem.BatchId` (إن وُجد).

**D4.** إصلاح إلغاء فاتورة الشراء: عند `adjustInventory=true`، إعادة `ProductBatch.Quantity` أيضًا.

**D5.** إضافة فحص `ExpiryAlertDays > 0` في `GetExpiryAlertsAsync`.

---

### Phase E — تكامل مع بقية النظام

**E1.** إظهار الباتش في صفحة تفاصيل المنتج (قائمة الباتشات النشطة).

**E2.** إظهار BatchNumber في تفاصيل الطلب (OrderDetailsModal).

**E3.** إضافة `isBatchTracked` في `Product` entity + `BranchInventoryDto` + Frontend types.

**E4.** إنشاء صفحة إعدادات Tenant لـ `ExpiryAlertDays` و `AllowExpiredSales`.

---

### Phase F — ضبط الصلاحيات

**F1.** تحديد من يمكنه:
- رؤية الباتشات (`InventoryView`).
- إنشاء باتش يدوي (`InventoryManage`).
- حذف/تعديل باتش (`InventoryManage`).
- رؤية التنبيهات (`InventoryView`).

**F2.** ربط `BatchExpiryAlertBanner` بالـ Permission بدل role.

---

## أولويات التنفيذ

### أولوية عالية
1. إنشاء صفحة إدارة الباتشات (Phase A + B).
2. تحويل التنبيه إلى Workflow قابل للتنفيذ (Phase C).
3. إصلاح المرتجع وإلغاء فاتورة الشراء (D3 + D4).
4. إضافة Transaction على CreateAsync (D1).

### أولوية متوسطة
1. إضافة `isBatchTracked` (E3).
2. إصلاح ظهور Banner بالـ Permission (C2 + F2).
3. إظهار الباتش في تفاصيل الطلب (E2).

### أولوية لاحقة
1. إعدادات Tenant لـ ExpiryAlertDays / AllowExpiredSales (E4).
2. تفاصيل الباتش مع حركاته (B3).
3. تحسينات أعمق في Analytics و Reporting.

---

## قرارات مطلوبة قبل التنفيذ

### القرار 1
**هل إدارة الباتشات ستكون صفحة مستقلة أم جزء من صفحة المخزون؟**
- التوصية: صفحة مستقلة تحت قسم المخزون، لأنها تحتاج فلاتر وعرضًا مكثفًا.

### القرار 2
**هل إنشاء الباتش اليدوي مطلوب كاستخدام يومي أم استثناء إداري فقط؟**
- التوصية: استثناء إداري فقط. الباتش الطبيعي يُنشأ من فاتورة شراء.

### القرار 3
**ما هي قواعد حذف الباتش المقبولة؟**
- التوصية: لا حذف إذا له مخزون أو حركات. البديل: "تعطيل" أو "تسوية".

### القرار 4
**من يجب أن يرى تنبيهات الصلاحية؟**
- التوصية: أي مستخدم لديه `InventoryView`.

### القرار 5
**هل نضيف `IsBatchTracked` على مستوى المنتج أم الفئة؟**
- التوصية: على مستوى المنتج (أو كلاهما: فئة افتراضية + override على المنتج).

### القرار 6
**ما الذي يحدث عند المرتجع — هل نعيد للباتش الأصلي أم نُنشئ باتش جديد "مرتجع"؟**
- التوصية: نعيد إلى الباتش الأصلي إذا كان `OrderItem.BatchId` معروفًا وما زال نشطًا.

---

## الحالة الحالية
**الميزة تمتلك Data Model قوي وFEFO يعمل، لكنها ناقصة جدًا على مستوى التشغيل والواجهة ونقاط الوصول. هناك مشاكل Backend حقيقية (المرتجع، إلغاء الشراء، حماية الحذف) يجب حسمها قبل التوسع في الواجهة.**
