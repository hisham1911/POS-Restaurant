# KasserPro — تنفيذ كل المهام المتبقية
## Products / Categories / Batches

---

## ⚠️ اقرأ الأول قبل أي سطر كود

**اقرأ هذه الملفات كاملة:**
1. `.kiro/steering/architecture.md`
2. `.kiro/steering/api-contract.md`
3. `.kiro/skills/kasserpro-bestpractices/SKILL.md`

**ملاحظات مهمة قبل البدء:**
- T-15 (sellingPrice + OnHold) **اتعمل بالفعل** في Batch Selection implementation — تحقق أولاً قبل ما تعمله تاني
- T-14 (holdBatch + releaseBatch mutations) اتعمل جزء منه — تحقق من `productBatchApi.ts` أولاً
- نفّذ المهام **بالترتيب** — كل مرحلة تكتمل قبل ما تبدأ التالية
- بعد كل مرحلة اعمل `dotnet build` و `npx tsc --noEmit`

---

## المرحلة 1: إصلاحات حرجة — سلامة البيانات (P0)
### ابدأ بيها — دي الأخطر

---

### T-1: إضافة StockMovement عند إنشاء منتج بمخزون أولي

**الملف:** `backend/KasserPro.Application/Services/Implementations/ProductService.cs`
**الميثود:** `CreateAsync`

**اقرأ الميثود كاملة أولاً** ثم بعد إنشاء `BranchInventory` للفرع الحالي بـ `InitialBranchStock > 0`، أضف:

```csharp
if (dto.InitialBranchStock > 0)
{
    var movement = new StockMovement
    {
        ProductId = product.Id,
        BranchId = _currentUser.BranchId,
        TenantId = _currentUser.TenantId,
        Type = StockMovementType.Receiving,
        Quantity = dto.InitialBranchStock,
        BalanceBefore = 0,
        BalanceAfter = dto.InitialBranchStock,
        ReferenceType = "InitialStock",
        Notes = "مخزون أولي عند إنشاء المنتج",
        CreatedBy = _currentUser.UserId,
        CreatedAt = DateTime.UtcNow
    };
    await _context.StockMovements.AddAsync(movement, ct);
}
```

**Verify:** بعد التنفيذ ابحث عن `StockMovements.AddAsync` في `ProductService.cs` — لازم يظهر في CreateAsync

---

### T-2: إضافة StockMovement في AdjustStockAsync

**الملف:** `ProductService.cs`
**الميثود:** `AdjustStockAsync`

**اقرأ الميثود كاملة أولاً.** احفظ الكمية قبل التعديل ثم بعد تعديل `BranchInventory.Quantity` أضف:

```csharp
var movement = new StockMovement
{
    ProductId = productId,
    BranchId = _currentUser.BranchId,
    TenantId = _currentUser.TenantId,
    Type = StockMovementType.Adjustment,
    Quantity = Math.Abs(dto.QuantityChange),
    BalanceBefore = quantityBefore,
    BalanceAfter = inventory.Quantity,
    ReferenceType = "ManualAdjustment",
    Notes = dto.Reason ?? "تسوية مخزون يدوية",
    CreatedBy = _currentUser.UserId,
    CreatedAt = DateTime.UtcNow
};
await _context.StockMovements.AddAsync(movement, ct);
```

**مهم:** احفظ `quantityBefore = inventory.Quantity` قبل أي تعديل على `inventory.Quantity`

**Verify:** ابحث عن `StockMovements.AddAsync` في ProductService.cs — لازم يظهر في AdjustStockAsync

---

### T-3: تغليف CreateAsync بـ transaction

**الملف:** `ProductService.cs`
**الميثود:** `CreateAsync`

**اقرأ الميثود كاملة أولاً.** غلّف كل العمليات من بداية الإنشاء حتى آخر `SaveChangesAsync`:

```csharp
await using var transaction = await _unitOfWork.BeginTransactionAsync();
try
{
    // ... كل العمليات الموجودة + إضافة StockMovement من T-1

    await _unitOfWork.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return ApiResponse<ProductDto>.Success(MapToDto(product));
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

**Verify:** ابحث عن `BeginTransactionAsync` في ProductService.cs — لازم يظهر في CreateAsync بـ `await using var`

---

### T-4: تصحيح StockMovementType في BatchService.CreateAsync

**الملف:** `backend/KasserPro.Application/Services/Implementations/ProductBatchService.cs`
**الميثود:** `CreateAsync`

**اقرأ الميثود كاملة أولاً.** ابحث عن السطر اللي فيه إنشاء StockMovement وغيّر:

```csharp
// ❌ قبل
Type = StockMovementType.Adjustment,
ReferenceType = "BatchManual",

// ✅ بعد
Type = StockMovementType.Receiving,
ReferenceType = "BatchManual",
```

**Verify:** تأكد إن `StockMovementType.Receiving` هو النوع المستخدم في إنشاء الدفعة يدوياً

---

### T-5: إضافة unique validation لـ SKU وBarcode

**الملف:** `ProductService.cs`
**الميثودات:** `CreateAsync` و `UpdateAsync`

أضف الـ validation التالي في الاثنين (في UpdateAsync استثني الـ product الحالي بـ `&& p.Id != productId`):

```csharp
// SKU uniqueness
if (!string.IsNullOrWhiteSpace(dto.Sku))
{
    var skuExists = await _context.Products.AnyAsync(p =>
        p.Sku == dto.Sku.Trim() &&
        p.TenantId == _currentUser.TenantId &&
        !p.IsDeleted &&
        p.Id != productId, // 0 في CreateAsync
        ct);
    if (skuExists)
        return ApiResponse<ProductDto>.Fail(
            ErrorCodes.PRODUCT_SKU_DUPLICATE,
            ErrorMessages.Get(ErrorCodes.PRODUCT_SKU_DUPLICATE));
}

// Barcode uniqueness
if (!string.IsNullOrWhiteSpace(dto.Barcode))
{
    var barcodeExists = await _context.Products.AnyAsync(p =>
        p.Barcode == dto.Barcode.Trim() &&
        p.TenantId == _currentUser.TenantId &&
        !p.IsDeleted &&
        p.Id != productId,
        ct);
    if (barcodeExists)
        return ApiResponse<ProductDto>.Fail(
            ErrorCodes.PRODUCT_BARCODE_DUPLICATE,
            ErrorMessages.Get(ErrorCodes.PRODUCT_BARCODE_DUPLICATE));
}
```

أضف في `ErrorCodes.cs`:
```csharp
public const string PRODUCT_SKU_DUPLICATE = "PRODUCT_SKU_DUPLICATE";
public const string PRODUCT_BARCODE_DUPLICATE = "PRODUCT_BARCODE_DUPLICATE";
```

أضف في `ErrorMessages.cs`:
```csharp
{ ErrorCodes.PRODUCT_SKU_DUPLICATE, "كود المنتج (SKU) مستخدم بالفعل" },
{ ErrorCodes.PRODUCT_BARCODE_DUPLICATE, "الباركود مستخدم بالفعل لمنتج آخر" },
```

**Verify بعد المرحلة 1 كاملة:**
```bash
dotnet build backend/KasserPro.API/ -c Release
# EXPECTED: 0 errors

dotnet test backend/KasserPro.Tests/ --logger "console;verbosity=minimal"
# EXPECTED: all pass

# تأكد من وجود StockMovement في الميثودين
# Search: StockMovements.AddAsync in ProductService.cs → 2 matches
```

---

## المرحلة 2: إصلاحات مهمة — وظائف ناقصة في Backend (P1)

---

### T-6: إصلاح CategoryService.GetAllAsync

**الملف:** `backend/KasserPro.Application/Services/Implementations/CategoryService.cs`
**الميثود:** `GetAllAsync`

**اقرأ الميثود كاملة أولاً.** أضف parameter اختياري:

```csharp
public async Task<ApiResponse<List<CategoryDto>>> GetAllAsync(
    string? search = null,
    bool? isActive = null, // ← parameter جديد (null = الكل)
    CancellationToken ct = default)
{
    var query = _context.Categories
        .Where(c => c.TenantId == _currentUser.TenantId && !c.IsDeleted);

    // فلتر IsActive اختياري
    if (isActive.HasValue)
        query = query.Where(c => c.IsActive == isActive.Value);

    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(c => c.Name.Contains(search) || 
                                  (c.NameEn != null && c.NameEn.Contains(search)));

    // ... باقي الكود
}
```

حدّث `ICategoryService` و `CategoriesController` ليدعموا الـ parameter الجديد.

---

### T-7: إضافة unique validation لـ Category.Name

**الملف:** `CategoryService.cs`
**الميثودات:** `CreateAsync` و `UpdateAsync`

```csharp
// Name uniqueness + length
if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
    return ApiResponse<CategoryDto>.Fail(
        ErrorCodes.CATEGORY_NAME_REQUIRED,
        ErrorMessages.Get(ErrorCodes.CATEGORY_NAME_REQUIRED));

var nameExists = await _context.Categories.AnyAsync(c =>
    c.Name == dto.Name.Trim() &&
    c.TenantId == _currentUser.TenantId &&
    !c.IsDeleted &&
    c.Id != categoryId, // 0 في CreateAsync
    ct);
if (nameExists)
    return ApiResponse<CategoryDto>.Fail(
        ErrorCodes.CATEGORY_NAME_DUPLICATE,
        ErrorMessages.Get(ErrorCodes.CATEGORY_NAME_DUPLICATE));
```

أضف في `ErrorCodes.cs`:
```csharp
public const string CATEGORY_NAME_REQUIRED = "CATEGORY_NAME_REQUIRED";
public const string CATEGORY_NAME_DUPLICATE = "CATEGORY_NAME_DUPLICATE";
```

أضف في `ErrorMessages.cs`:
```csharp
{ ErrorCodes.CATEGORY_NAME_REQUIRED, "اسم التصنيف مطلوب ولا يتجاوز 100 حرف" },
{ ErrorCodes.CATEGORY_NAME_DUPLICATE, "يوجد تصنيف بنفس الاسم بالفعل" },
```

---

### T-8: منع تغيير Product.Type مع وجود مخزون

**الملف:** `ProductService.cs`
**الميثود:** `UpdateAsync`

**اقرأ الميثود كاملة أولاً.** أضف الـ check قبل تحديث النوع:

```csharp
// منع تغيير النوع لو في مخزون أو دفعات نشطة
if (dto.Type != product.Type)
{
    var hasStock = await _context.BranchInventory.AnyAsync(b =>
        b.ProductId == product.Id &&
        b.TenantId == _currentUser.TenantId &&
        b.Quantity > 0, ct);

    var hasActiveBatches = await _context.ProductBatches.AnyAsync(b =>
        b.ProductId == product.Id &&
        b.TenantId == _currentUser.TenantId &&
        b.Status == BatchStatus.Active, ct);

    if (hasStock || hasActiveBatches)
        return ApiResponse<ProductDto>.Fail(
            ErrorCodes.PRODUCT_TYPE_CANNOT_CHANGE,
            ErrorMessages.Get(ErrorCodes.PRODUCT_TYPE_CANNOT_CHANGE));
}
```

أضف في `ErrorCodes.cs`:
```csharp
public const string PRODUCT_TYPE_CANNOT_CHANGE = "PRODUCT_TYPE_CANNOT_CHANGE";
```

أضف في `ErrorMessages.cs`:
```csharp
{ ErrorCodes.PRODUCT_TYPE_CANNOT_CHANGE, "لا يمكن تغيير نوع المنتج مع وجود مخزون أو دفعات نشطة" },
```

---

### T-9: إضافة unique validation لـ BatchNumber

**الملف:** `ProductBatchService.cs`
**الميثود:** `CreateAsync`

**اقرأ الميثود كاملة أولاً.** أضف قبل الحفظ:

```csharp
if (!string.IsNullOrWhiteSpace(dto.BatchNumber))
{
    var batchExists = await _context.ProductBatches.AnyAsync(b =>
        b.BatchNumber == dto.BatchNumber.Trim() &&
        b.ProductId == dto.ProductId &&
        b.BranchId == _currentUser.BranchId &&
        b.TenantId == _currentUser.TenantId &&
        b.Status != BatchStatus.Depleted, // الدفعات المنتهية لا تُحسب
        ct);
    if (batchExists)
        return ApiResponse<ProductBatchDto>.Fail(
            ErrorCodes.BATCH_NUMBER_DUPLICATE,
            ErrorMessages.Get(ErrorCodes.BATCH_NUMBER_DUPLICATE));
}
```

أضف في `ErrorCodes.cs`:
```csharp
public const string BATCH_NUMBER_DUPLICATE = "BATCH_NUMBER_DUPLICATE";
```

أضف في `ErrorMessages.cs`:
```csharp
{ ErrorCodes.BATCH_NUMBER_DUPLICATE, "رقم الدفعة مستخدم بالفعل لهذا المنتج في هذا الفرع" },
```

---

### T-10: فصل side-effect من GetExpiryAlerts

**الملف:** `ProductBatchService.cs`
**الميثود:** `GetExpiryAlertsAsync`

**اقرأ الميثود كاملة أولاً.** احذف أي كود بيعمل `batch.Status = Expired` أو `SaveChangesAsync` من داخل الميثود دي.

أضف ميثود جديدة منفصلة:
```csharp
public async Task<ApiResponse<int>> UpdateExpiredBatchesStatusAsync(CancellationToken ct = default)
{
    var expiredBatches = await _context.ProductBatches
        .Where(b => b.TenantId == _currentUser.TenantId
                 && b.Status == BatchStatus.Active
                 && b.ExpiryDate.HasValue
                 && b.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
        .ToListAsync(ct);

    foreach (var batch in expiredBatches)
        batch.Status = BatchStatus.Expired;

    await _unitOfWork.SaveChangesAsync(ct);
    return ApiResponse<int>.Success(expiredBatches.Count);
}
```

أضف endpoint في `ProductBatchesController.cs`:
```csharp
[HttpPost("update-expired")]
[HasPermission(Permission.InventoryView)]
public async Task<IActionResult> UpdateExpiredBatches(CancellationToken ct)
    => Ok(await _batchService.UpdateExpiredBatchesStatusAsync(ct));
```

حدّث `IProductBatchService` ليشمل الميثود الجديدة.

---

### T-11: نقل Pagination لـ DB في ProductService.GetAllAsync

**الملف:** `ProductService.cs`
**الميثود:** `GetAllAsync`

**اقرأ الميثود كاملة أولاً.** الهدف إن `Skip` و `Take` يتنفذوا على الـ DB query مش بعد `ToListAsync`.

```csharp
// ❌ قبل (خاطئ — بيجيب كل البيانات أولاً)
var allProducts = await query.ToListAsync(ct);
var totalCount = allProducts.Count;
var items = allProducts.Skip((params.Page - 1) * params.PageSize).Take(params.PageSize);

// ✅ بعد (صح — pagination في DB)
var totalCount = await query.CountAsync(ct);
var items = await query
    .Skip((params.Page - 1) * params.PageSize)
    .Take(params.PageSize)
    .Select(p => new ProductDto { ... })
    .ToListAsync(ct);
```

**مهم:** تأكد إن `CountAsync` يجي قبل `Skip/Take` وإن الـ Select يشمل كل الـ fields المطلوبة.

**Verify بعد المرحلة 2:**
```bash
dotnet build backend/KasserPro.API/ -c Release
# EXPECTED: 0 errors

dotnet test backend/KasserPro.Tests/ --logger "console;verbosity=minimal"
# EXPECTED: all pass
```

---

## المرحلة 3: اكتمال Frontend (P1)

---

### T-12: إضافة زر إنشاء دفعة + ProductBatchFormModal

**الخطوة 1 — أنشئ ملف جديد:**
`frontend/src/components/inventory/ProductBatchFormModal.tsx`

الـ modal ده بيسمح بإنشاء دفعة جديدة. الـ fields المطلوبة:
- `productId` (مطلوب) — select من المنتجات الـ batch-tracked
- `batchNumber` (مطلوب) — نص
- `expiryDate` (اختياري) — date picker
- `quantity` (مطلوب) — رقم موجب
- `costPrice` (اختياري) — رقم
- `sellingPrice` (اختياري) — رقم

```tsx
interface ProductBatchFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  productId?: number; // لو بيتفتح من صفحة منتج محدد
}
```

استخدم `useCreateProductBatchMutation` من `productBatchApi.ts`.

عند الـ error، استخدم `error.data?.errorCode` للـ handling مش `error.message`.

**الخطوة 2 — أضف زر في ProductBatchesPage:**
في `frontend/src/pages/inventory/ProductBatchesPage.tsx`:
- أضف زر `+ إضافة دفعة` في header الصفحة
- اربطه بـ `ProductBatchFormModal`
- بعد نجاح الإنشاء، invalidate الـ cache عشان الجدول يتحدث

---

### T-13 + T-14: إضافة Hold/Release UI + mutations

**الخطوة 1 — أضف mutations في `frontend/src/api/productBatchApi.ts`:**

**تحقق أولاً** إذا كان `holdBatch` و `releaseBatch` موجودين بالفعل من تنفيذ Batch Selection. إذا مش موجودين، أضفهم:

```typescript
updateProductBatch: builder.mutation<
  ApiResponse<ProductBatchDto>,
  { id: number; data: UpdateProductBatchRequest }
>({
  query: ({ id, data }) => ({
    url: `/productbatches/${id}`,
    method: 'PUT',
    body: data,
  }),
  invalidatesTags: (result, error, { id }) => [
    { type: 'ProductBatch', id },
    'ProductBatch',
  ],
}),

holdBatch: builder.mutation<ApiResponse<ProductBatchDto>, number>({
  query: (id) => ({
    url: `/productbatches/${id}/hold`,
    method: 'PATCH',
  }),
  invalidatesTags: (result, error, id) => [
    { type: 'ProductBatch', id },
    'ProductBatch',
  ],
}),

releaseBatch: builder.mutation<ApiResponse<ProductBatchDto>, number>({
  query: (id) => ({
    url: `/productbatches/${id}/release`,
    method: 'PATCH',
  }),
  invalidatesTags: (result, error, id) => [
    { type: 'ProductBatch', id },
    'ProductBatch',
  ],
}),
```

Export الـ hooks الجديدة.

**الخطوة 2 — أضف Hold/Release UI في `ProductBatchesPage.tsx`:**

في الجدول، أضف column "الإجراءات" بـ dropdown menu لكل صف:
- لو الدفعة `Active` → اعرض زر "تعليق (Hold)"
- لو الدفعة `OnHold` → اعرض زر "تفعيل (Release)"
- زر "حذف" للحالتين

```tsx
// مثال على الـ actions column
<td>
  <div className="flex gap-2">
    {batch.status === 'Active' && (
      <button
        onClick={() => handleHold(batch.id)}
        className="text-warning-600 hover:text-warning-800 text-sm"
      >
        تعليق
      </button>
    )}
    {batch.status === 'OnHold' && (
      <button
        onClick={() => handleRelease(batch.id)}
        className="text-success-600 hover:text-success-800 text-sm"
      >
        تفعيل
      </button>
    )}
    <button
      onClick={() => handleDelete(batch.id)}
      className="text-error-600 hover:text-error-800 text-sm"
    >
      حذف
    </button>
  </div>
</td>
```

**Handlers:**
```typescript
const [holdBatch] = useHoldBatchMutation();
const [releaseBatch] = useReleaseBatchMutation();

const handleHold = async (id: number) => {
  try {
    await holdBatch(id).unwrap();
    toast.success('تم تعليق الدفعة بنجاح');
  } catch (err) {
    const error = err as { data: ApiResponse<null> };
    toast.error(error.data?.message ?? 'حدث خطأ أثناء تعليق الدفعة');
  }
};

const handleRelease = async (id: number) => {
  try {
    await releaseBatch(id).unwrap();
    toast.success('تم تفعيل الدفعة بنجاح');
  } catch (err) {
    const error = err as { data: ApiResponse<null> };
    toast.error(error.data?.message ?? 'حدث خطأ أثناء تفعيل الدفعة');
  }
};
```

---

### T-15: مزامنة Types (تحقق أولاً — قد تكون اتعملت)

**الملف:** `frontend/src/types/productBatch.types.ts`

افتح الملف وتحقق:

**إذا لم تكن موجودة، أضفها:**
```typescript
export interface ProductBatch {
  // ... existing fields
  sellingPrice?: number;     // ← تأكد موجود
  isRecommended?: boolean;   // ← تأكد موجود
}

// BatchStatus — تأكد OnHold موجود
export type BatchStatus = 'Active' | 'Expired' | 'Depleted' | 'OnHold';

export interface CreateProductBatchRequest {
  // ... existing fields
  sellingPrice?: number;   // ← تأكد موجود
}
```

**إذا كانت موجودة بالفعل من Batch Selection implementation — تجاوز هذه الخطوة.**

---

### T-16: إضافة isActive toggle في CategoriesPage

**الملف:** `frontend/src/pages/categories/CategoriesPage.tsx`

**اقرأ الملف أولاً.** ابحث عن `formData` وتأكد إنه بيشيل `isActive`.

في نموذج التعديل، أضف toggle:
```tsx
<div className="flex items-center justify-between">
  <label className="text-sm font-medium text-gray-700">
    حالة التصنيف
  </label>
  <button
    type="button"
    onClick={() => setFormData(prev => ({ ...prev, isActive: !prev.isActive }))}
    className={clsx(
      'relative inline-flex h-6 w-11 items-center rounded-full transition-colors',
      formData.isActive ? 'bg-primary-600' : 'bg-gray-300'
    )}
  >
    <span
      className={clsx(
        'inline-block h-4 w-4 transform rounded-full bg-white transition-transform',
        formData.isActive ? 'translate-x-6' : 'translate-x-1'
      )}
    />
  </button>
  <span className="text-sm text-gray-500">
    {formData.isActive ? 'نشط' : 'معطل'}
  </span>
</div>
```

تأكد إن الـ `formData` initial state بيشيل `isActive: category.isActive` لما تفتح edit mode.

تأكد إن الـ mutation بيبعت `isActive` في الـ request body.

---

### T-17: إضافة Pagination controls في ProductsPage

**الملف:** `frontend/src/pages/products/ProductsPage.tsx`

**اقرأ الملف أولاً.** ابحث عن `useGetProductsQuery` وشوف الـ params اللي بيبعتها.

**الخطوة 1 — أضف pagination state:**
```typescript
const [currentPage, setCurrentPage] = useState(1);
const PAGE_SIZE = 20;
```

**الخطوة 2 — أضف للـ query params:**
```typescript
const { data, isLoading } = useGetProductsQuery({
  // ... existing filters
  page: currentPage,
  pageSize: PAGE_SIZE,
});

const totalCount = data?.data?.totalCount ?? 0;
const totalPages = Math.ceil(totalCount / PAGE_SIZE);
```

**الخطوة 3 — أضف Pagination component أسفل الجدول:**
```tsx
{totalPages > 1 && (
  <div className="flex items-center justify-between px-4 py-3 border-t border-gray-200">
    <p className="text-sm text-gray-700">
      عرض {((currentPage - 1) * PAGE_SIZE) + 1} - {Math.min(currentPage * PAGE_SIZE, totalCount)} من {totalCount} منتج
    </p>
    <div className="flex gap-2">
      <button
        onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
        disabled={currentPage === 1}
        className="px-3 py-1 rounded border text-sm disabled:opacity-50"
      >
        السابق
      </button>
      <span className="px-3 py-1 text-sm">
        {currentPage} / {totalPages}
      </span>
      <button
        onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
        disabled={currentPage === totalPages}
        className="px-3 py-1 rounded border text-sm disabled:opacity-50"
      >
        التالي
      </button>
    </div>
  </div>
)}
```

**مهم:** لما يتغير أي filter، ارجع لـ `setCurrentPage(1)`.

---

### T-18: استخراج CategoryFormModal

**الخطوة 1 — أنشئ ملف جديد:**
`frontend/src/components/categories/CategoryFormModal.tsx`

```tsx
interface CategoryFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  category?: Category; // undefined = create mode, Category = edit mode
}
```

**الخطوة 2 — انقل الـ form logic من CategoriesPage:**
- انقل state الـ form وكل الـ handlers المتعلقة بيه
- استخدم `useCreateCategoryMutation` و `useUpdateCategoryMutation`
- كل الـ fields: اسم، اسم انجليزي، أيقونة، ترتيب، isActive toggle (من T-16)

**الخطوة 3 — حدّث CategoriesPage:**
- احذف الـ inline form code
- استخدم `<CategoryFormModal>` بدلاً منه
- مرر الـ props المطلوبة

**Verify بعد المرحلة 3:**
```bash
npx tsc --noEmit
# من frontend/ directory
# EXPECTED: 0 errors
```

---

## المرحلة 4: تحسينات (P2)

---

### T-19: إزالة deprecated currentBranchStock من update flow

**الملف:** `frontend/src/components/products/ProductFormModal.tsx`

**اقرأ الملف أولاً.** ابحث عن `LEGACY_CURRENT_BRANCH_FIELD` أو `currentBranchStock` في الـ update request.

احذف إرسال `currentBranchStock` من الـ update request. لو في UI elements بتعرض الـ field ده في edit mode، غيّرها لرسالة توجيهية:

```tsx
{isEditMode && (
  <div className="rounded-lg bg-blue-50 p-3 text-sm text-blue-700">
    لتعديل الكمية في المخزون، استخدم صفحة المخزون أو التسوية اليدوية
  </div>
)}
```

---

### T-20: نقل ProductBatchDto لمجلد مستقل

**الملف:** `backend/KasserPro.Application/DTOs/ProductBatchDto.cs`

**اقرأ الملف أولاً.**

1. أنشئ مجلد جديد: `backend/KasserPro.Application/DTOs/ProductBatches/`
2. أنشئ ملف جديد: `ProductBatches/ProductBatchDto.cs`
3. انقل كل الـ DTOs المتعلقة بالدفعات: `ProductBatchDto`, `ProductBatchListDto`, `CreateProductBatchDto`, `UpdateProductBatchDto`, `HoldBatchRequest`, `BatchExpiryAlertDto`, `BatchExpirySummaryDto`
4. غيّر الـ namespace لـ `KasserPro.Application.DTOs.ProductBatches`
5. احذف الملف القديم
6. أضف `using KasserPro.Application.DTOs.ProductBatches;` في أي ملف بيستخدم هذه الـ DTOs

**Verify:**
```bash
dotnet build backend/KasserPro.API/ -c Release
# EXPECTED: 0 errors
```

---

### T-21 + T-22: إصلاح N+1 وإضافة ProductCount في CategoryService

**الملف:** `CategoryService.cs`
**الميثودات:** `GetAllAsync` و `GetByIdAsync`

**اقرأ الميثودين أولاً.**

**T-21 — إصلاح N+1 في GetAllAsync:**
```csharp
// ❌ قبل (N+1 potential)
var categories = await _context.Categories
    .Where(...)
    .Select(c => new CategoryDto
    {
        ProductCount = _context.Products.Count(p => p.CategoryId == c.Id && !p.IsDeleted)
    })
    .ToListAsync(ct);

// ✅ بعد (join واحد)
var categories = await _context.Categories
    .Where(c => c.TenantId == _currentUser.TenantId && !c.IsDeleted)
    .GroupJoin(
        _context.Products.Where(p => !p.IsDeleted),
        c => c.Id,
        p => p.CategoryId,
        (c, products) => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            // ... باقي الـ fields
            ProductCount = products.Count()
        })
    .ToListAsync(ct);
```

**T-22 — إضافة ProductCount في GetByIdAsync:**
```csharp
var productCount = await _context.Products
    .CountAsync(p => p.CategoryId == id && 
                     p.TenantId == _currentUser.TenantId && 
                     !p.IsDeleted, ct);

// أضف ProductCount للـ CategoryDto المرتجع
```

---

### T-23: توحيد سلوك مساري تعديل المخزون

**المشكلة:** مساران لتعديل المخزون:
- `POST /api/products/{id}/adjust-stock` → ProductService (بعد T-2 صار ينشئ StockMovement ✅)
- `POST /api/inventory/adjust` → InventoryService (بالفعل ينشئ StockMovement ✅)

**بعد تنفيذ T-2، الاثنان بيسجلان StockMovement.** المطلوب فقط التأكد إن نوع الـ StockMovement متسق:

```csharp
// ProductService.AdjustStockAsync → StockMovementType.Adjustment ✅
// InventoryService.AdjustInventoryAsync → تحقق إنه كمان Adjustment ✅
```

لو في فرق في الـ ReferenceType أو الـ Notes، وحّدهم.

**هذه المهمة ممكن تكون "مراجعة فقط" لو T-2 حلّها بالفعل.**

---

## Verification النهائي — بعد كل المراحل

```bash
# 1. Backend build
dotnet build backend/KasserPro.API/ -c Release
# EXPECTED: 0 errors, 0 warnings

# 2. Backend tests
dotnet test backend/KasserPro.Tests/ --logger "console;verbosity=minimal"
# EXPECTED: all pass

# 3. Frontend TypeScript
cd frontend && npx tsc --noEmit
# EXPECTED: 0 errors

# 4. Static checks
# تحقق من وجود StockMovements.AddAsync في 2 أماكن في ProductService
# تحقق من عدم وجود ToListAsync قبل Skip/Take في ProductService.GetAllAsync
# تحقق من عدم وجود SaveChangesAsync في GetExpiryAlertsAsync
```

---

## التقرير النهائي

```
## KasserPro — تقرير تنفيذ المهام

### المرحلة 1 — P0 (سلامة البيانات):
T-1 StockMovement عند الإنشاء: [DONE/SKIP/ISSUE]
T-2 StockMovement في AdjustStock: [DONE/SKIP/ISSUE]
T-3 Transaction في CreateAsync: [DONE/SKIP/ISSUE]
T-4 تصحيح StockMovementType: [كان X، صار Receiving]
T-5 Unique validation SKU/Barcode: [DONE/SKIP/ISSUE]

### المرحلة 2 — P1 Backend:
T-6 CategoryService.GetAllAsync: [DONE/SKIP/ISSUE]
T-7 Unique validation Category.Name: [DONE/SKIP/ISSUE]
T-8 منع تغيير Product.Type: [DONE/SKIP/ISSUE]
T-9 Unique validation BatchNumber: [DONE/SKIP/ISSUE]
T-10 فصل side-effect GetExpiryAlerts: [DONE/SKIP/ISSUE]
T-11 Pagination في DB: [DONE/SKIP/ISSUE]

### المرحلة 3 — P1 Frontend:
T-12 ProductBatchFormModal + زر الإنشاء: [DONE/SKIP/ISSUE]
T-13 Hold/Release UI: [DONE/SKIP/ISSUE]
T-14 mutations في API slice: [DONE/كان موجود/ISSUE]
T-15 types sync: [DONE/كان موجود/ISSUE]
T-16 isActive toggle في Categories: [DONE/SKIP/ISSUE]
T-17 Pagination في ProductsPage: [DONE/SKIP/ISSUE]
T-18 CategoryFormModal: [DONE/SKIP/ISSUE]

### المرحلة 4 — P2:
T-19 إزالة deprecated currentBranchStock: [DONE/SKIP/ISSUE]
T-20 نقل ProductBatchDto لمجلد: [DONE/SKIP/ISSUE]
T-21 إصلاح N+1: [DONE/SKIP/ISSUE]
T-22 ProductCount في GetById: [DONE/SKIP/ISSUE]
T-23 توحيد مساري المخزون: [DONE/مراجعة فقط]

### النتائج:
Build: [PASS/FAIL]
Tests: [N/N pass]
TypeScript: [PASS/FAIL]

### الملفات المعدلة: [list]
### مشاكل لم تُحل (إن وجدت): [list]
```
