# 🎯 تحسينات نظام الباتشات (Product Batch System Enhancements)

**تاريخ التنفيذ:** 30 أبريل 2026  
**الحالة:** ✅ مكتمل (Backend فقط)

---

## 📋 ملخص التعديلات

تم تنفيذ 3 تعديلات رئيسية على نظام الباتشات في الباك-اند:

### **1️⃣ Batch Creation إجباري دايماً**

**المشكلة السابقة:**
- الباتش كان بيتعمل بس لو المستخدم دخل `BatchNumber` أو `ExpiryDate`
- لو مدخلش، المخزون بيتحدث بدون batch tracking
- FEFO مش بيشتغل لو مفيش batches

**الحل:**
- ✅ **دايماً** بيتعمل batch لكل منتج `IsBatchTracked = true`
- ✅ لو مفيش `BatchNumber` → يتولّد تلقائي: `AUTO-{InvoiceNumber}-{ProductId}-{yyyyMMdd}`
- ✅ لو مفيش `ExpiryDate` → يتحط default: `DateTime.UtcNow.AddYears(10)`

**الملفات المعدلة:**
- `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`

**الكود:**
```csharp
// ALWAYS create ProductBatch if product is batch-tracked
if (product.IsBatchTracked)
{
    var batchNumber = !string.IsNullOrWhiteSpace(item.BatchNumber)
        ? item.BatchNumber
        : $"AUTO-{invoice.InvoiceNumber}-{product.Id}-{DateTime.UtcNow:yyyyMMdd}";
    
    var expiryDate = item.ExpiryDate ?? DateTime.UtcNow.AddYears(10);
    
    // ... create batch
}
```

---

### **2️⃣ إضافة `SellingPrice` و `OnHold` Status**

#### **A) SellingPrice على مستوى الباتش**

**المشكلة السابقة:**
- الباتش فيه `CostPrice` بس
- السعر بيجي من `Product.Price` (واحد لكل المنتج)
- مفيش طريقة لتحديد سعر بيع مختلف لكل باتش

**الحل:**
- ✅ أضفنا `SellingPrice` (nullable) في `ProductBatch`
- ✅ لو موجود، بيستخدم بدل `Product.Price`
- ✅ لو `null`، بيستخدم `Product.Price` العادي

**الملفات المعدلة:**
- `backend/KasserPro.Domain/Entities/ProductBatch.cs`
- `backend/KasserPro.Application/DTOs/ProductBatchDto.cs`

**الكود:**
```csharp
public class ProductBatch : BaseEntity
{
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }  // ✅ جديد
}
```

#### **B) OnHold Status**

**المشكلة السابقة:**
- الأدمن مش قادر يوقف باتش من البيع يدوياً
- لو باتش فيه مشكلة → لازم يعمل adjustment يخلي `Quantity = 0`

**الحل:**
- ✅ أضفنا `OnHold = 4` في `BatchStatus` enum
- ✅ الباتشات الموقوفة **مش بتتباع** في FEFO logic
- ✅ الأدمن يقدر يوقف ويفعّل الباتش بـ endpoints جديدة

**الملفات المعدلة:**
- `backend/KasserPro.Domain/Enums/BatchStatus.cs`
- `backend/KasserPro.Infrastructure/Services/InventoryService.cs`

**الكود:**
```csharp
public enum BatchStatus
{
    Active = 1,
    Expired = 2,
    Depleted = 3,
    OnHold = 4,  // ✅ جديد
}

// في InventoryService.BatchDecrementStockAsync()
var batchQuery = _context.ProductBatches
    .Where(pb => pb.Status != BatchStatus.Depleted 
              && pb.Status != BatchStatus.OnHold)  // ✅ جديد
    .OrderBy(pb => pb.ExpiryDate);
```

---

### **3️⃣ Endpoints جديدة في ProductBatchesController**

#### **A) Update Batch**
```http
PUT /api/product-batches/{id}
Permission: InventoryManage
Body: UpdateProductBatchDto
```

**يسمح بتعديل:**
- ✅ `BatchNumber`
- ✅ `ExpiryDate`
- ✅ `ProductionDate`
- ✅ `SellingPrice`
- ✅ `Notes`

**ممنوع تعديل:**
- ❌ `Quantity` (لازم يكون عن طريق Adjustment)

#### **B) Hold Batch (إيقاف)**
```http
PATCH /api/product-batches/{id}/hold
Permission: InventoryManage
Body: { "reason": "سبب الإيقاف" }
```

**الوظيفة:**
- يغير Status → `OnHold`
- يضيف السبب في `Notes` مع timestamp
- الباتش مش هيتباع في FEFO

#### **C) Release Batch (تفعيل)**
```http
PATCH /api/product-batches/{id}/release
Permission: InventoryManage
Body: { "reason": "سبب التفعيل" }
```

**الوظيفة:**
- يغير Status من `OnHold` → `Active` (أو `Expired`/`Depleted` حسب الحالة)
- يضيف السبب في `Notes` مع timestamp
- الباتش يرجع يتباع في FEFO

**الملفات المعدلة:**
- `backend/KasserPro.Application/Services/Interfaces/IProductBatchService.cs`
- `backend/KasserPro.Application/Services/Implementations/ProductBatchService.cs`
- `backend/KasserPro.API/Controllers/ProductBatchesController.cs`
- `backend/KasserPro.Application/DTOs/ProductBatchDto.cs`

---

## 🗄️ Database Migration

**Migration Name:** `20260430184900_AddBatchSellingPriceAndOnHoldStatus`

**التغييرات:**
```sql
ALTER TABLE ProductBatches ADD COLUMN SellingPrice TEXT NULL;
```

**ملاحظة:** `BatchStatus.OnHold = 4` مش محتاج migration لأنه enum value جديد بس.

**الحالة:** ✅ تم تطبيقه بنجاح

---

## ✅ الاختبارات المطلوبة

### **1. Batch Creation**
- [ ] إنشاء فاتورة شراء **بدون** `BatchNumber` و `ExpiryDate`
- [ ] التحقق من إنشاء batch تلقائي بـ `AUTO-` prefix
- [ ] التحقق من `ExpiryDate` = 10 سنين من الآن

### **2. SellingPrice**
- [ ] إنشاء batch بـ `SellingPrice` مخصص
- [ ] بيع منتج من الباتش ده
- [ ] التحقق من استخدام `SellingPrice` بدل `Product.Price`

### **3. OnHold Status**
- [ ] إيقاف batch عن طريق `/hold` endpoint
- [ ] محاولة بيع منتج من الباتش الموقوف
- [ ] التحقق من عدم استخدام الباتش في FEFO
- [ ] تفعيل الباتش عن طريق `/release` endpoint
- [ ] التحقق من رجوع الباتش للبيع

### **4. Update Batch**
- [ ] تعديل `BatchNumber` و `ExpiryDate`
- [ ] تعديل `SellingPrice`
- [ ] التحقق من حفظ التعديلات

---

## 📊 الإحصائيات

| المقياس | القيمة |
|---------|--------|
| **الملفات المعدلة** | 8 ملفات |
| **Endpoints جديدة** | 3 endpoints |
| **DTOs جديدة** | 2 DTOs |
| **Migration** | 1 migration |
| **Lines of Code** | ~150 سطر |

---

## 🚀 الخطوات التالية (Frontend)

### **P1 (High Priority)**

1. **تحديث Types:**
   ```typescript
   export interface ProductBatch {
     sellingPrice?: number;  // ✅ جديد
     status: 'Active' | 'Expired' | 'Depleted' | 'OnHold';  // ✅ OnHold جديد
   }
   
   export interface UpdateProductBatchRequest {
     batchNumber: string;
     expiryDate: string;
     productionDate?: string;
     sellingPrice?: number;  // ✅ جديد
     notes?: string;
   }
   ```

2. **إضافة API Endpoints:**
   ```typescript
   // في productBatchApi.ts
   updateBatch: builder.mutation<ApiResponse<ProductBatch>, { id: number; data: UpdateProductBatchRequest }>({...}),
   holdBatch: builder.mutation<ApiResponse<ProductBatch>, { id: number; reason: string }>({...}),
   releaseBatch: builder.mutation<ApiResponse<ProductBatch>, { id: number; reason: string }>({...}),
   ```

3. **UI للإدارة:**
   - صفحة Batch Management
   - زر "إيقاف" و "تفعيل"
   - Modal لتعديل الباتش
   - عرض `SellingPrice` في الجدول

### **P2 (Medium Priority)**

4. **Batch Selection في POS:**
   - Modal لاختيار الباتش عند البيع
   - عرض `ExpiryDate` و `Quantity` و `SellingPrice`
   - السماح للكاشير باختيار باتش معين

---

## 📝 ملاحظات مهمة

1. **Backward Compatibility:**
   - ✅ الباتشات القديمة (بدون `SellingPrice`) هتشتغل عادي
   - ✅ السعر هيجي من `Product.Price` لو `SellingPrice = null`

2. **FEFO Logic:**
   - ✅ الباتشات الموقوفة (`OnHold`) **مش بتتباع**
   - ✅ الترتيب: `Active` → `Expired` (لو `AllowExpiredSales = true`)

3. **Security:**
   - ✅ كل الـ endpoints محمية بـ `Permission.InventoryManage`
   - ✅ Tenant isolation شغال

4. **Audit Trail:**
   - ✅ كل تغيير في Status بيتسجل في `Notes` مع timestamp
   - ✅ `StatusUpdatedAt` بيتحدث تلقائيًا

---

## 🎉 الخلاصة

تم تنفيذ التعديلات الثلاثة بنجاح على الباك-اند:

1. ✅ **Batch Creation إجباري** — كل منتج batch-tracked بيعمل batch تلقائيًا
2. ✅ **SellingPrice** — كل باتش يقدر يكون ليه سعر بيع مخصص
3. ✅ **OnHold Status** — الأدمن يقدر يوقف ويفعّل الباتشات يدوياً

**الحالة:** جاهز للاختبار والتكامل مع الفرونت-اند! 🚀


---

## 🎯 التحديث الأخير: Price Priority Logic (30 أبريل 2026)

### **4️⃣ تكامل SellingPrice مع نظام البيع (POS)**

**الحالة:** ✅ مكتمل

#### **المشكلة:**
- الباتش فيه `SellingPrice` بس مش بيستخدم في البيع
- الـ POS بيستخدم `Product.Price` دايماً
- مفيش أولوية للسعر (Batch vs Branch vs Product)

#### **الحل:**
تم تنفيذ **Price Priority Logic** في `OrderService`:

```
Priority 1: ProductBatch.SellingPrice (لو موجود ومش null)
    ↓
Priority 2: BranchProductPrice.Price (لو موجود)
    ↓
Priority 3: Product.Price (default)
```

#### **الملفات المعدلة:**

1. **ProductBatchService.cs**
   - ✅ `CreateAsync()` بيحفظ `dto.SellingPrice` دلوقتي

2. **OrderService.cs**
   - ✅ أضفنا method جديدة: `ResolveSellingPriceAsync()`
   - ✅ `CreateAsync()` بيستخدم Price Priority
   - ✅ `AddItemAsync()` بيستخدم Price Priority
   - ✅ `OriginalPrice` في OrderItem بيعكس السعر المحلول

3. **PurchaseInvoiceService.cs** (كان متعمل قبل كده)
   - ✅ بيحفظ `SellingPrice` في الباتش
   - ✅ بيحدّث `Product.Price` بـ `SellingPrice`

#### **الكود الجديد:**

```csharp
/// <summary>
/// Resolves the selling price with priority: 
/// 1) ProductBatch.SellingPrice (if not null)
/// 2) BranchProductPrice.Price (if exists)
/// 3) Product.Price (default)
/// </summary>
private async Task<decimal> ResolveSellingPriceAsync(
    int productId, int branchId, int tenantId, decimal defaultPrice)
{
    var product = await _unitOfWork.Products.GetByIdAsync(productId);
    
    // Priority 1: Check batch-tracked products for SellingPrice
    if (product != null && product.IsBatchTracked)
    {
        var batchWithPrice = await _unitOfWork.ProductBatches.Query()
            .Where(pb => pb.TenantId == tenantId
                && pb.BranchId == branchId
                && pb.ProductId == productId
                && !pb.IsDeleted
                && pb.Status != BatchStatus.Depleted
                && pb.Status != BatchStatus.OnHold
                && pb.Quantity > 0
                && pb.SellingPrice.HasValue)
            .OrderBy(pb => pb.ExpiryDate) // FEFO
            .FirstOrDefaultAsync();

        if (batchWithPrice != null)
            return batchWithPrice.SellingPrice.Value;
    }

    // Priority 2: Branch-specific price
    var branchPrice = await _inventoryService.GetEffectivePriceAsync(productId, branchId);
    if (branchPrice > 0 && branchPrice != defaultPrice)
        return branchPrice;

    // Priority 3: Product default
    return defaultPrice;
}
```

#### **Integration في CreateAsync:**
```csharp
// OLD:
var unitPrice = ResolveNetUnitPrice(product.Price, product.TaxInclusive, taxRate);

// NEW:
var sellingPrice = await ResolveSellingPriceAsync(product.Id, branchId, tenantId, product.Price);
var unitPrice = ResolveNetUnitPrice(sellingPrice, product.TaxInclusive, taxRate);
```

#### **FEFO Integration:**
- ✅ السعر بيجي من أول باتش (FEFO order)
- ✅ بس الباتشات اللي:
  - Active (مش Depleted ومش OnHold)
  - Quantity > 0
  - SellingPrice موجود (مش null)

#### **سيناريوهات الاختبار:**

**Scenario 1: Batch with SellingPrice**
```
Product "Coca Cola" → Price = 10 EGP
Batch "BATCH-001" → SellingPrice = 12 EGP
Result: Order uses 12 EGP ✅
```

**Scenario 2: Batch without SellingPrice**
```
Product "Pepsi" → Price = 10 EGP
Batch "BATCH-002" → SellingPrice = NULL
BranchProductPrice = 11 EGP
Result: Order uses 11 EGP ✅
```

**Scenario 3: No Batch, No Branch Price**
```
Product "Water" → Price = 5 EGP
No batches, No BranchProductPrice
Result: Order uses 5 EGP ✅
```

**Scenario 4: OnHold Batch Excluded**
```
Product "Juice" → Price = 15 EGP
Batch "BATCH-003" → SellingPrice = 18 EGP, Status = OnHold
Result: Order uses 15 EGP (OnHold ignored) ✅
```

---

## 📊 الإحصائيات النهائية

| المقياس | القيمة |
|---------|--------|
| **الملفات المعدلة** | 10 ملفات |
| **Endpoints جديدة** | 3 endpoints |
| **DTOs جديدة/محدثة** | 4 DTOs |
| **Migrations** | 1 migration |
| **Methods جديدة** | 4 methods |
| **Lines of Code** | ~250 سطر |
| **Build Status** | ✅ 0 Warnings, 0 Errors |

---

## ✅ Checklist النهائي

### Backend Implementation
- [x] Batch creation إجباري لكل `IsBatchTracked` products
- [x] Auto-generate `BatchNumber` لو مش موجود
- [x] Default `ExpiryDate` = 10 years
- [x] `SellingPrice` field في ProductBatch entity
- [x] `OnHold` status في BatchStatus enum
- [x] FEFO logic يستثني OnHold batches
- [x] Update/Hold/Release endpoints
- [x] `SellingPrice` في CreateProductBatchDto
- [x] `SellingPrice` في UpdateProductBatchDto
- [x] `ProductBatchService.CreateAsync()` يحفظ SellingPrice
- [x] `PurchaseInvoiceService.ConfirmAsync()` يحفظ SellingPrice
- [x] `ResolveSellingPriceAsync()` method جديدة
- [x] Price priority في `OrderService.CreateAsync()`
- [x] Price priority في `OrderService.AddItemAsync()`
- [x] FEFO integration مع price resolution
- [x] Migration applied successfully
- [x] Build succeeds with 0 warnings

### Testing (Pending)
- [ ] Test batch creation without BatchNumber
- [ ] Test batch creation without ExpiryDate
- [ ] Test SellingPrice in POS sales
- [ ] Test price priority (Batch → Branch → Product)
- [ ] Test OnHold batch exclusion from sales
- [ ] Test Hold/Release endpoints
- [ ] Test Update batch endpoint
- [ ] Test FEFO with multiple batches with different prices

### Frontend (Not Started)
- [ ] Update ProductBatch types
- [ ] Add RTK Query endpoints (update/hold/release)
- [ ] Batch management UI
- [ ] Display SellingPrice in batch list
- [ ] Show price source in POS

---

## 🎉 النتيجة النهائية

تم تنفيذ **4 تعديلات رئيسية** بنجاح:

1. ✅ **Batch Creation إجباري** — كل منتج batch-tracked بيعمل batch تلقائيًا
2. ✅ **SellingPrice + OnHold** — كل باتش يقدر يكون ليه سعر بيع مخصص ويتوقف يدوياً
3. ✅ **Update/Hold/Release Endpoints** — إدارة كاملة للباتشات
4. ✅ **Price Priority Logic** — السعر بيجي من الباتش الأول (FEFO) لو موجود

**الحالة:** ✅ جاهز للاختبار والتكامل مع الفرونت-اند!  
**Build Status:** ✅ Success (0 warnings, 0 errors)  
**Documentation:** ✅ Complete

للتفاصيل الكاملة، راجع: `BATCH_SYSTEM_PRICE_PRIORITY_IMPLEMENTATION.md`
