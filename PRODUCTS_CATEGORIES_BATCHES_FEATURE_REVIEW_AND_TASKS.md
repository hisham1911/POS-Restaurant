# مراجعة ميزة المنتجات والتصنيفات والدفعات (Batches)

> آخر تحديث: يونيو 2025

---

## 1. حصر الكود الحالي (Code Inventory)

### 1.1 Backend — Entities

| Entity | ملف | الوصف |
|--------|------|-------|
| `Product` | `Domain\Entities\Product.cs` | المنتج الأساسي — يحمل السعر، النوع (Physical/Service)، TrackInventory، IsBatchTracked، AverageCost، LowStockThreshold |
| `Category` | `Domain\Entities\Category.cs` | تصنيف المنتجات — Name، NameEn، SortOrder، IsActive |
| `ProductBatch` | `Domain\Entities\ProductBatch.cs` | دفعة مخزون — BatchNumber، ExpiryDate، Quantity، InitialQuantity، CostPrice، SellingPrice، Status (Active/Expired/Depleted/OnHold) |
| `BranchInventory` | `Domain\Entities\BranchInventory.cs` | مخزون الفرع — Quantity، ReorderLevel لكل (Product × Branch) |
| `BranchProductPrice` | `Domain\Entities\BranchProductPrice.cs` | سعر مخصص لكل فرع — Price، EffectiveFrom/To |
| `StockMovement` | `Domain\Entities\StockMovement.cs` | سجل حركة المخزون — Type، Quantity، BalanceBefore/After، BatchId |
| `InventoryTransfer` | `Domain\Entities\InventoryTransfer.cs` | نقل المخزون بين الفروع — Status (Pending→Approved→Completed) |
| `SupplierProduct` | `Domain\Entities\SupplierProduct.cs` | ربط المورد بالمنتج — LastPurchasePrice، TotalQuantityPurchased |

### 1.2 Backend — Enums

| Enum | ملف | القيم |
|------|------|-------|
| `ProductType` | `Domain\Enums\ProductType.cs` | Physical=1, Service=2 |
| `BatchStatus` | `Domain\Enums\BatchStatus.cs` | Active=1, Expired=2, Depleted=3, OnHold=4 |
| `StockMovementType` | `Domain\Enums\StockMovementType.cs` | Sale, Refund, Adjustment, Receiving, Damage, Transfer, StockTaking, Expired |

### 1.3 Backend — Services (Interfaces + Implementations)

| Service | Interface | Implementation | الوظائف الرئيسية |
|---------|-----------|----------------|-----------------|
| **ProductService** | `IProductService` | `ProductService.cs` (518 سطر) | GetAll (paginated+filtered), GetById, Create, Update, Delete, AdjustStock, QuickCreate |
| **CategoryService** | `ICategoryService` | `CategoryService.cs` (151 سطر) | GetAll (paginated+search), GetById, Create, Update, Delete |
| **ProductBatchService** | `IProductBatchService` | `ProductBatchService.cs` (332 سطر) | GetAll, GetById, Create, Update, Delete, Hold, Release, GetExpiryAlerts, GetByProduct |
| **InventoryService** | `IInventoryService` | `InventoryService.cs` (Infrastructure) | BranchInventory CRUD, Transfers (Create/Approve/Receive/Cancel), BranchPrices, Legacy stock methods |
| **ProductReportService** | `IProductReportService` | Infrastructure | ProductMovement, ProfitableProducts, SlowMoving, COGS |
| **InventoryReportService** | `IInventoryReportService` | Infrastructure | BranchInventoryReport, UnifiedInventory, TransferHistory, LowStockSummary |

### 1.4 Backend — Controllers

| Controller | ملف | Endpoints |
|-----------|------|-----------|
| `ProductsController` | 100 سطر | GET /, GET /{id}, POST /, PUT /{id}, DELETE /{id}, POST /{id}/adjust-stock, POST /quick-create |
| `CategoriesController` | 69 سطر | GET /, GET /{id}, POST /, PUT /{id}, DELETE /{id} |
| `ProductBatchesController` | 66 سطر | GET /, GET /{id}, GET /product/{productId}, POST /, PUT /{id}, PATCH /{id}/hold, PATCH /{id}/release, DELETE /{id}, GET /alerts/expiry |
| `InventoryController` | 170 سطر | GET /branch/{branchId}, GET /product/{productId}, GET /low-stock, POST /adjust, transfer CRUD, branch-prices CRUD |
| `ProductReportsController` | — | movement, profitable, slow-moving, cogs |
| `InventoryReportsController` | — | branch-inventory, unified, transfer-history, low-stock-summary |

### 1.5 Backend — DTOs

| مجموعة | الملفات |
|--------|---------|
| **Products** | `ProductDto.cs`, `CreateProductRequest.cs`, `UpdateProductRequest.cs`, `QuickCreateProductRequest.cs`, `AdjustStockRequest.cs` |
| **Categories** | `CategoryDto.cs`, `CreateCategoryRequest.cs`, `UpdateCategoryRequest.cs` |
| **ProductBatches** | `ProductBatchDto.cs` (يحتوي: ProductBatchDto, ProductBatchListDto, CreateProductBatchDto, UpdateProductBatchDto, HoldBatchRequest, BatchExpiryAlertDto, BatchExpirySummaryDto) |
| **Inventory** | `BranchInventoryDto.cs`, `BranchProductPriceDto.cs` (+ AdjustInventoryRequest, SetBranchPriceRequest), `InventoryTransferDto.cs` |
| **Reports** | `ProductReportDto.cs` (Movement, Profitable, SlowMoving, COGS), `InventoryReportDto.cs` |

### 1.6 Frontend — API Slices (RTK Query)

| ملف | Hooks الرئيسية |
|------|---------------|
| `productsApi.ts` | useGetProductsQuery, useGetProductQuery, useCreateProductMutation, useUpdateProductMutation, useDeleteProductMutation, useQuickCreateProductMutation |
| `categoriesApi.ts` | useGetCategoriesQuery, useGetCategoryQuery, useCreateCategoryMutation, useUpdateCategoryMutation, useDeleteCategoryMutation |
| `productBatchApi.ts` | useGetProductBatchesQuery, useGetProductBatchByIdQuery, useGetBatchesByProductQuery, useGetExpiryAlertsQuery, useCreateProductBatchMutation, useDeleteProductBatchMutation |
| `inventoryApi.ts` | useGetBranchInventoryQuery, useGetProductInventoryAcrossBranchesQuery, useGetLowStockItemsQuery, useAdjustInventoryMutation, useAdjustProductStockMutation, transfer hooks, branch-price hooks |
| `productReportsApi.ts` | تقارير حركة المنتجات، الأرباح، البطيئة الحركة |
| `inventoryReportsApi.ts` | تقارير المخزون الموحد |

### 1.7 Frontend — Pages

| الصفحة | ملف | الوصف |
|--------|------|-------|
| **ProductsPage** | `pages/products/ProductsPage.tsx` (418 سطر) | جدول المنتجات + بحث + فلترة (تصنيف، نشط، مخزون منخفض) + بطاقات ملخص |
| **CategoriesPage** | `pages/categories/CategoriesPage.tsx` (482 سطر) | بطاقات التصنيفات + إنشاء/تعديل (Modal مدمج) + أيقونات إيموجي + بحث + pagination |
| **ProductBatchesPage** | `pages/inventory/ProductBatchesPage.tsx` (323 سطر) | جدول الدفعات + فلاتر (حالة، فرع، قريب الانتهاء) + حذف |
| **InventoryPage** | `pages/inventory/InventoryPage.tsx` (224 سطر) | تبويبات: مخزون الفرع، تنبيهات، نقل، أسعار الفروع |

### 1.8 Frontend — Components

| Component | ملف | الوصف |
|-----------|------|-------|
| **ProductFormModal** | `components/products/ProductFormModal.tsx` (763 سطر) | مودال كامل لإنشاء/تعديل منتج — أيقونات، تسعير، ضرائب، أكواد، مخزون، توزيع فروع |
| **CategoryTabs** | `components/pos/CategoryTabs.tsx` | تبويبات التصنيفات في واجهة الكاشير |
| **CategoryChips** | `components/pos/CategoryChips.tsx` | رقائق (chips) التصنيفات في واجهة الكاشير |
| **BatchExpiryAlertBanner** | `components/inventory/BatchExpiryAlertBanner.tsx` | بانر تنبيه الدفعات المنتهية/القريبة في صفحة المخزون |
| **BranchInventoryList** | `components/inventory/BranchInventoryList.tsx` | عرض مخزون الفرع |
| **InventoryTransferForm** | `components/inventory/InventoryTransferForm.tsx` | نموذج طلب نقل مخزون |
| **InventoryTransferList** | `components/inventory/InventoryTransferList.tsx` | قائمة طلبات النقل |
| **ProductQuickCreateModal** | `components/pos/ProductQuickCreateModal.tsx` | إنشاء سريع للمنتج من POS |
| **QuickAddProductModal** | `components/purchase-invoices/QuickAddProductModal.tsx` | إنشاء سريع من فاتورة الشراء |
| **ProductCard/Grid/ListView** | `components/pos/Product*.tsx` | عرض المنتجات في واجهة الكاشير |

### 1.9 Frontend — Types & Utilities

| ملف | المحتوى |
|------|---------|
| `types/product.types.ts` | Product, CreateProductRequest, UpdateProductRequest, ProductsQueryParams, QuickCreateProductRequest, ProductType enum |
| `types/category.types.ts` | Category interface |
| `types/productBatch.types.ts` | ProductBatch, BatchExpiryAlert, BatchExpirySummary, CreateProductBatchRequest, ProductBatchFilters, BatchStatus type |
| `types/inventory.types.ts` | BranchInventory, InventoryTransfer, BranchProductPrice, + request/response types |
| `hooks/useProducts.ts` | useProducts, useCategories, useFilteredProducts hooks |
| `utils/productStock.ts` | buildBranchInventoryStockMap, getProductCurrentStock, getProductAvailableStock, isProductOutOfStock |

---

## 2. تحليل الـ Workflows

### 2.1 إنشاء تصنيف (Category)
```
المستخدم (Admin) → CategoriesPage → Modal → POST /api/categories
  ← CategoryService.CreateAsync → يحفظ في DB → يرجع CategoryDto
```
- **لا يوجد validation للاسم** (تكرار، طول).
- `IsActive = true` افتراضياً عند الإنشاء (صحيح).
- `GetAllAsync` يفلتر `IsActive` فقط → **التصنيفات المعطلة لا تظهر أبداً في القائمة** (مشكلة).

### 2.2 إنشاء منتج (Product)
```
المستخدم (Admin) → ProductsPage → ProductFormModal → POST /api/products
  ← ProductService.CreateAsync:
    1. يتحقق من السعر والتصنيف
    2. ينشئ Product (Type → TrackInventory تلقائياً)
    3. إذا Physical: ينشئ BranchInventory لكل الفروع
       - الفرع الحالي يأخذ InitialBranchStock
       - باقي الفروع = 0
```
- **لا يُنشئ StockMovement** للمخزون الأولي → فجوة في سجل التدقيق.
- **لا يتحقق من تكرار SKU/Barcode** → خطر تعارضات.
- الـ transaction غير مستخدم في `CreateAsync` (فقط في `QuickCreateAsync`).

### 2.3 تعديل منتج (Update Product)
```
ProductFormModal (edit mode) → PUT /api/products/{id}
  ← ProductService.UpdateAsync:
    1. يتحقق من السعر والتصنيف
    2. يحدث كل الحقول
    3. يقرأ BranchInventory للرد
```
- **يسمح بتغيير النوع من Physical→Service** بعد وجود مخزون → خطر ضياع بيانات.
- **CurrentBranchStock في UpdateRequest** مهمل (deprecated) لكن لا يزال يُرسل → فوضى.
- **لا يوجد RowVersion/Concurrency check**.

### 2.4 إنشاء دفعة (Batch) يدوياً
```
ProductBatchesPage (لا يوجد زر إنشاء!) أو من PurchaseInvoice confirm
  ← ProductBatchService.CreateAsync:
    1. يتحقق من المنتج
    2. Transaction:
       a. ينشئ ProductBatch
       b. يحدث BranchInventory (+quantity)
       c. ينشئ StockMovement (Type=Adjustment, Ref=BatchManual)
    3. Commit
```
- **لا يوجد UI لإنشاء دفعة يدوياً** في ProductBatchesPage (الزر مفقود!).
- **StockMovement.Type = Adjustment** بدل **Receiving** → خطأ في نوع الحركة.
- **لا يتحقق من تكرار BatchNumber** للمنتج/الفرع.

### 2.5 إنشاء دفعة من فاتورة الشراء (Purchase Invoice → Confirm)
```
PurchaseInvoiceService.ConfirmAsync:
  لكل InvoiceItem:
    1. يحدث BranchInventory (+quantity)
    2. ينشئ StockMovement (Type=Receiving)
    3. إذا (BatchNumber أو ExpiryDate):
       ينشئ ProductBatch (مع CostPrice, SellingPrice, SupplierName)
    4. يحدث Product.AverageCost (weighted avg)
    5. يحدث SupplierProduct link
```
- هذا هو المسار الأساسي لإنشاء الدفعات → **صحيح ومتكامل**.

### 2.6 تنبيهات الدفعات (Batch Expiry Alerts)
```
GET /api/productbatches/alerts/expiry
  ← ProductBatchService.GetExpiryAlertsAsync:
    1. يجلب كل الدفعات النشطة/المعلقة
    2. يقرأ Tenant.ExpiryAlertDays (افتراضي 30)
    3. يحسب الأيام المتبقية
    4. يحدث Status→Expired تلقائياً للمنتهية
    5. يرجع ملخص + قائمة تنبيهات مرتبة
```
- **Side-effect في GET**: يحدث الحالة أثناء القراءة → غير نظيف.
- **BatchExpiryAlertBanner** يعمل بـ polling كل 5 دقائق → مقبول.

### 2.7 إيقاف/تفعيل دفعة (Hold/Release)
```
PATCH /api/productbatches/{id}/hold  → Status = OnHold
PATCH /api/productbatches/{id}/release → يحدد Status بناءً على الحالة الفعلية
```
- **لا يوجد UI للـ Hold/Release** في ProductBatchesPage (endpoints موجودة لكن غير متصلة بالواجهة).

### 2.8 تعديل المخزون (Stock Adjustment)
```
طريقتان:
1. POST /api/products/{id}/adjust-stock → ProductService.AdjustStockAsync
   (يُستخدم من POS: كمية ± مع سبب)
2. POST /api/inventory/adjust → InventoryService.AdjustInventoryAsync
   (يُستخدم من Inventory page: branchId + productId + quantityChange)
```
- **طريقتان مختلفتان لنفس العملية** → ازدواجية، لكنها مقبولة لأن كل واحدة لها سياق مختلف.
- `AdjustStockAsync` في ProductService **لا ينشئ StockMovement** → فجوة تدقيق خطيرة!

---

## 3. المشاكل والفجوات المكتشفة

### 3.1 مشاكل Backend حرجة (P0)

#### B-1: لا StockMovement عند إنشاء منتج بمخزون أولي
**الملف**: `ProductService.CreateAsync` سطر 206-244
**المشكلة**: عند إنشاء منتج مادي بـ `InitialBranchStock > 0`، يُنشئ `BranchInventory` بدون `StockMovement` → المخزون يظهر بدون أثر في سجل الحركات.
**الأثر**: فجوة في التدقيق المالي. لا يمكن تتبع مصدر المخزون الأولي.
**الإصلاح**: إنشاء `StockMovement` من نوع `Adjustment` أو `Receiving` مع `Reason = "مخزون أولي"`.

#### B-2: لا StockMovement في ProductService.AdjustStockAsync
**الملف**: `ProductService.AdjustStockAsync` سطر 376-415
**المشكلة**: يعدل `BranchInventory.Quantity` مباشرة بدون إنشاء `StockMovement`.
**الأثر**: تعديلات المخزون من POS لا تُسجل في سجل الحركات.
**الإصلاح**: إضافة إنشاء `StockMovement` بعد التعديل.

#### B-3: لا transaction في ProductService.CreateAsync
**الملف**: `ProductService.CreateAsync` سطر 170-263
**المشكلة**: `SaveChangesAsync` يُستدعى مرتين (مرة للمنتج، مرة للمخزون) بدون transaction → إذا فشل الثاني، يبقى المنتج بدون مخزون.
**الإصلاح**: تغليف العملية بـ `BeginTransactionAsync` (كما في `QuickCreateAsync`).

#### B-4: BatchService.CreateAsync يستخدم StockMovementType.Adjustment بدل Receiving
**الملف**: `ProductBatchService.CreateAsync` سطر 117-124
**المشكلة**: نوع الحركة خاطئ. إنشاء دفعة يدوياً يجب أن يكون `Receiving` وليس `Adjustment`.
**الإصلاح**: تغيير `Type = StockMovementType.Receiving` وتحديث `ReferenceType = "BatchManual"`.

#### B-5: لا validation على تكرار SKU/Barcode
**الملف**: `ProductService.CreateAsync` و `UpdateAsync`
**المشكلة**: يمكن إنشاء منتجين بنفس SKU أو Barcode → مشكلة في POS عند البحث بالباركود.
**الإصلاح**: إضافة unique check في الـ service قبل الحفظ.

### 3.2 مشاكل Backend مهمة (P1)

#### B-6: CategoryService.GetAllAsync يفلتر IsActive فقط
**الملف**: `CategoryService.GetAllAsync` سطر 26
**المشكلة**: `Where(c => c.IsActive)` → التصنيفات المعطلة لا تظهر أبداً في صفحة إدارة التصنيفات.
**الأثر**: لا يمكن للأدمن رؤية/إعادة تفعيل التصنيفات المعطلة.
**الإصلاح**: إزالة فلتر `IsActive` من GetAll (أو جعله اختيارياً كما في Products).

#### B-7: لا validation على Category.Name (تكرار + طول)
**الملف**: `CategoryService.CreateAsync` و `UpdateAsync`
**المشكلة**: يمكن إنشاء تصنيفين بنفس الاسم. لا يوجد حد أقصى للطول.
**الإصلاح**: إضافة unique check + length validation.

#### B-8: يسمح بتغيير Product.Type من Physical→Service مع وجود مخزون
**الملف**: `ProductService.UpdateAsync` سطر 296-298
**المشكلة**: تغيير النوع يغير `TrackInventory` تلقائياً → المنتج الذي كان مادياً ولديه مخزون يصبح خدمة ومخزونه يُتجاهل.
**الإصلاح**: منع تغيير النوع إذا `BranchInventory.Quantity > 0` أو `ProductBatch` نشطة موجودة.

#### B-9: لا validation على BatchNumber uniqueness
**الملف**: `ProductBatchService.CreateAsync`
**المشكلة**: يمكن إنشاء دفعتين بنفس `BatchNumber` لنفس المنتج/الفرع.
**الإصلاح**: إضافة unique check (ProductId + BranchId + BatchNumber).

#### B-10: GetExpiryAlerts يعمل side-effect (UPDATE) في GET request
**الملف**: `ProductBatchService.GetExpiryAlertsAsync` سطر 194-195
**المشكلة**: يحدث `batch.Status = Expired` أثناء قراءة التنبيهات → مخالف لمبدأ GET idempotent.
**الإصلاح**: فصل التحديث في background job أو endpoint مستقل.

#### B-11: ProductService.GetAllAsync يجلب كل المنتجات للذاكرة ثم يفلتر
**الملف**: `ProductService.GetAllAsync` سطر 64
**المشكلة**: `ToListAsync()` قبل pagination → يجلب كل المنتجات للذاكرة ثم يقص. مع 10,000+ منتج = مشكلة أداء.
**الأثر**: بطء مع نمو البيانات.
**الإصلاح**: نقل pagination لـ DB query (Skip/Take قبل ToListAsync).

#### B-12: CategoryService.GetAllAsync يعمل sub-query لـ ProductCount داخل Select
**الملف**: `CategoryService.GetAllAsync` سطر 49-50
**المشكلة**: `_unitOfWork.Products.Query().Count(...)` داخل LINQ Select يمكن أن يسبب N+1 queries.
**الإصلاح**: استخدام `GroupJoin` أو `let` clause بدلاً من sub-query.

### 3.3 مشاكل Frontend (P1)

#### F-1: ProductBatchesPage لا يوجد فيها زر إنشاء دفعة
**الملف**: `ProductBatchesPage.tsx`
**المشكلة**: الصفحة عرض فقط + حذف. لا يوجد UI لإنشاء أو تعديل دفعة يدوياً.
**الأثر**: المستخدم لا يمكنه إضافة دفعة إلا من فاتورة شراء.
**الإصلاح**: إضافة زر "+ إضافة دفعة" + `ProductBatchFormModal`.

#### F-2: ProductBatchesPage لا يدعم Hold/Release
**الملف**: `ProductBatchesPage.tsx`
**المشكلة**: الـ API يدعم Hold/Release لكن الواجهة لا تعرض أزرار لذلك.
**الإصلاح**: إضافة dropdown actions (Hold/Release) لكل صف.

#### F-3: productBatchApi.ts لا يوجد فيها updateProductBatch mutation
**الملف**: `productBatchApi.ts`
**المشكلة**: الـ backend يدعم `PUT /productbatches/{id}` لكن الـ frontend لا يعرّف mutation لها.
**الإصلاح**: إضافة `updateProductBatch` mutation + `holdBatch` + `releaseBatch`.

#### F-4: ProductBatch type ينقصه sellingPrice
**الملف**: `types/productBatch.types.ts` سطر 5-22
**المشكلة**: الـ backend يرجع `sellingPrice` في `ProductBatchDto` لكن الـ frontend type لا يحتوي عليه.
**الإصلاح**: إضافة `sellingPrice?: number` للـ interface.

#### F-5: BatchStatus type لا يتضمن OnHold
**الملف**: `types/productBatch.types.ts` سطر 3
**المشكلة**: `type BatchStatus = 'Active' | 'Expired' | 'Depleted'` — ينقصه `'OnHold'`.
**الإصلاح**: إضافة `'OnHold'` للـ union type.

#### F-6: CreateProductBatchRequest ينقصها sellingPrice
**الملف**: `types/productBatch.types.ts` سطر 42-51
**المشكلة**: الـ backend DTO يقبل `SellingPrice` لكن الـ frontend type لا يرسلها.
**الإصلاح**: إضافة `sellingPrice?: number`.

#### F-7: CategoriesPage تستخدم inline form بدل component منفصل
**الملف**: `CategoriesPage.tsx` سطر 297-423
**المشكلة**: نموذج التصنيف مدمج في الصفحة (200+ سطر) بدلاً من component مستقل → صعوبة صيانة.
**الإصلاح**: استخراج `CategoryFormModal` component.

#### F-8: ProductFormModal يعالج deprecated currentBranchStock
**الملف**: `ProductFormModal.tsx` سطر 29, 208-211
**المشكلة**: يستخدم `LEGACY_CURRENT_BRANCH_FIELD` لإرسال المخزون → deprecated ومربك.
**الأثر**: خلط بين مسارين مختلفين لتعديل المخزون.
**الإصلاح**: إزالة تعديل المخزون من ProductFormModal وتوجيه المستخدم لصفحة المخزون.

#### F-9: CategoriesPage لا ترسل IsActive عند التعديل
**الملف**: `CategoriesPage.tsx` سطر 107-110
**المشكلة**: `formData` لا يحتوي على `isActive` → عند التعديل، الـ backend يستخدم القيمة الافتراضية `true` → لا يمكن تعطيل تصنيف.
**الإصلاح**: إضافة `isActive` toggle في نموذج التعديل.

#### F-10: ProductsPage لا يدعم pagination (server-side)
**الملف**: `ProductsPage.tsx`
**المشكلة**: الـ backend يرجع `PagedResult` لكن الواجهة لا تعرض pagination controls (لا أزرار صفحات).
**الإصلاح**: إضافة pagination component.

### 3.4 مشاكل معمارية / Information Architecture (P2)

#### A-1: ازدواجية مسار تعديل المخزون
- `POST /products/{id}/adjust-stock` → ProductService (بدون StockMovement!)
- `POST /inventory/adjust` → InventoryService (مع StockMovement)
**القرار المطلوب**: توحيد المسار أو على الأقل ضمان تطابق السلوك.

#### A-2: InventoryService في Infrastructure بدل Application
**الملف**: `KasserPro.Infrastructure\Services\InventoryService.cs`
**المشكلة**: معظم الخدمات في `Application\Services\Implementations` لكن InventoryService في `Infrastructure\Services`.
**الأثر**: عدم اتساق في بنية المشروع.

#### A-3: ProductBatchDto في ملف مستقل بدل مجلد
**الملف**: `DTOs\ProductBatchDto.cs` (في root DTOs)
**المشكلة**: باقي DTOs في مجلدات فرعية (Products/, Categories/, Inventory/) لكن ProductBatch DTOs في ملف واحد في root.
**الإصلاح**: نقل لمجلد `DTOs\ProductBatches\`.

#### A-4: CategoryDto لا يرجع ProductCount في GetById
**الملف**: `CategoryService.GetByIdAsync` سطر 65-74
**المشكلة**: `GetAllAsync` يحسب `ProductCount` لكن `GetByIdAsync` لا يحسبه.

#### A-5: ProductDto.IsLowStock computed property
**الملف**: `ProductDto.cs` سطر 53
**المشكلة**: `IsLowStock` هي computed property في DTO → منطق business في DTO.
**الإصلاح**: نقل الحساب للـ service أو تركه كـ read-only (مقبول بشروط).

---

## 4. المهام المقترحة (بالأولوية)

### المرحلة 1: إصلاحات حرجة (P0) — سلامة البيانات

| # | المهمة | التأثير | الملفات |
|---|--------|---------|---------|
| T-1 | إضافة StockMovement عند إنشاء منتج بمخزون أولي | سلامة التدقيق | `ProductService.CreateAsync` |
| T-2 | إضافة StockMovement في ProductService.AdjustStockAsync | سلامة التدقيق | `ProductService.AdjustStockAsync` |
| T-3 | تغليف CreateAsync بـ transaction | سلامة البيانات | `ProductService.CreateAsync` |
| T-4 | تصحيح StockMovementType في BatchService.CreateAsync | صحة التصنيف | `ProductBatchService.CreateAsync` |
| T-5 | إضافة unique validation لـ SKU/Barcode | منع التعارضات | `ProductService.CreateAsync`, `UpdateAsync` |

### المرحلة 2: إصلاحات مهمة (P1) — وظائف ناقصة

| # | المهمة | التأثير | الملفات |
|---|--------|---------|---------|
| T-6 | إصلاح CategoryService.GetAllAsync (إزالة فلتر IsActive أو جعله اختيارياً) | إدارة التصنيفات | `CategoryService.cs` |
| T-7 | إضافة unique validation لـ Category.Name | منع التكرار | `CategoryService.cs` |
| T-8 | منع تغيير Product.Type مع وجود مخزون | سلامة البيانات | `ProductService.UpdateAsync` |
| T-9 | إضافة unique validation لـ BatchNumber (per product+branch) | منع التكرار | `ProductBatchService.CreateAsync` |
| T-10 | فصل side-effect من GetExpiryAlerts | نظافة الكود | `ProductBatchService.cs` |
| T-11 | نقل pagination لـ DB في ProductService.GetAllAsync | أداء | `ProductService.GetAllAsync` |

### المرحلة 3: اكتمال Frontend (P1)

| # | المهمة | التأثير | الملفات |
|---|--------|---------|---------|
| T-12 | إضافة زر + مودال إنشاء دفعة في ProductBatchesPage | وظيفة مفقودة | `ProductBatchesPage.tsx`, جديد: `ProductBatchFormModal.tsx` |
| T-13 | إضافة Hold/Release UI في ProductBatchesPage | وظيفة مفقودة | `ProductBatchesPage.tsx`, `productBatchApi.ts` |
| T-14 | إضافة updateProductBatch + holdBatch + releaseBatch في API slice | اكتمال API | `productBatchApi.ts` |
| T-15 | مزامنة types: إضافة sellingPrice + OnHold للـ frontend types | اتساق | `productBatch.types.ts` |
| T-16 | إضافة isActive toggle في CategoriesPage form | وظيفة مفقودة | `CategoriesPage.tsx` |
| T-17 | إضافة pagination controls في ProductsPage | UX | `ProductsPage.tsx` |
| T-18 | استخراج CategoryFormModal component | صيانة | `CategoriesPage.tsx` → `CategoryFormModal.tsx` |

### المرحلة 4: تحسينات (P2)

| # | المهمة | التأثير | الملفات |
|---|--------|---------|---------|
| T-19 | إزالة deprecated currentBranchStock من update flow | نظافة | `ProductFormModal.tsx`, `UpdateProductRequest` |
| T-20 | نقل ProductBatchDto لمجلد مستقل | تنظيم | `DTOs\ProductBatchDto.cs` |
| T-21 | إصلاح N+1 في CategoryService.GetAllAsync (ProductCount) | أداء | `CategoryService.cs` |
| T-22 | إضافة ProductCount في CategoryService.GetByIdAsync | اكتمال | `CategoryService.cs` |
| T-23 | توحيد سلوك مساري تعديل المخزون (A-1) | اتساق | `ProductService`, `InventoryService` |

---

## 5. ملخص

| البعد | الحالة | التقييم |
|-------|--------|---------|
| **Entities & Schema** | جيد | البنية سليمة. Product→Category→BranchInventory→ProductBatch مترابطة بشكل صحيح |
| **Business Logic** | يحتاج إصلاح | StockMovement ناقص في مسارين حرجين. validation ناقص (SKU, Barcode, BatchNumber) |
| **API Design** | جيد | RESTful سليم. Permissions محددة بشكل صحيح |
| **Frontend Types** | يحتاج مزامنة | BatchStatus ناقص OnHold. SellingPrice غير موجود. Update/Hold/Release mutations مفقودة |
| **Frontend UX** | يحتاج تحسين | لا إنشاء/تعديل يدوي للدفعات. لا pagination للمنتجات. لا تعطيل تصنيف من الواجهة |
| **Code Quality** | مقبول | بعض الازدواجية (stock adjustment paths). DTO file placement inconsistent |
| **Performance** | يحتاج تحسين | GetAllAsync يجلب كل المنتجات للذاكرة. CategoryService N+1 potential |

**الأولوية القصوى**: T-1 إلى T-5 (سلامة بيانات المخزون وسجل التدقيق).
