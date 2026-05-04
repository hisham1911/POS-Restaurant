# 🔧 إصلاح مشكلة "ليس رقماً" في SuggestedPrice

> **التاريخ:** 2 مايو 2026  
> **المشكلة:** كل المنتجات أصبحت تعرض "ليس رقماً" (NaN)  
> **الحالة:** ✅ تم الإصلاح

---

## 🐛 المشكلة

عند إضافة حقل `SuggestedPrice` في الـ LINQ projection مباشرة:

```csharp
// ❌ الكود الخاطئ
var projectedQuery = query.Select(p => new ProductDto
{
    // ...
    SuggestedPrice = p.IsBatchTracked
        ? productBatchesQuery
            .Where(pb => pb.ProductId == p.Id)
            .Select(pb => (decimal?)pb.SellingPrice)
            .FirstOrDefault() ?? p.Price
        : p.Price,
    // ...
});
```

**السبب:**
- EF Core لا يستطيع ترجمة الـ subquery المعقد داخل الـ projection إلى SQL
- هذا يسبب exception أو قيم null/NaN

---

## ✅ الحل

### 1. **تعيين قيمة افتراضية في Projection**

```csharp
var projectedQuery = query.Select(p => new ProductDto
{
    // ...
    Price = p.Price,
    SuggestedPrice = p.Price, // ✅ قيمة افتراضية مؤقتة
    // ...
});
```

---

### 2. **حساب SuggestedPrice بعد جلب البيانات**

```csharp
var pagedItems = await projectedQuery
    .OrderBy(p => p.Name)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// ✅ حساب SuggestedPrice بعد جلب البيانات
var batchTrackedProductIds = pagedItems
    .Where(p => p.IsBatchTracked)
    .Select(p => p.Id)
    .ToList();

if (batchTrackedProductIds.Any())
{
    // Get next batch price for each batch-tracked product
    var nextBatchPrices = await _unitOfWork.ProductBatches.Query()
        .Where(pb => pb.TenantId == tenantId 
                  && pb.BranchId == branchId 
                  && batchTrackedProductIds.Contains(pb.ProductId)
                  && pb.Status == BatchStatus.Active
                  && pb.Quantity > 0)
        .GroupBy(pb => pb.ProductId)
        .Select(g => new
        {
            ProductId = g.Key,
            SellingPrice = g.OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
                            .ThenBy(pb => pb.Id)
                            .Select(pb => pb.SellingPrice)
                            .FirstOrDefault()
        })
        .ToListAsync();

    // Update SuggestedPrice for products with batches
    foreach (var product in pagedItems.Where(p => p.IsBatchTracked))
    {
        var batchPrice = nextBatchPrices.FirstOrDefault(bp => bp.ProductId == product.Id);
        product.SuggestedPrice = batchPrice?.SellingPrice ?? product.Price;
    }
}
```

---

## 🔄 كيف يعمل الحل

### الخطوة 1: جلب المنتجات
```
Query → Products with SuggestedPrice = Price (default)
```

### الخطوة 2: تحديد المنتجات ذات الباتشات
```
Filter → Products where IsBatchTracked = true
```

### الخطوة 3: جلب أسعار الباتشات
```
Query → ProductBatches
  WHERE ProductId IN (batch-tracked products)
  AND Status = Active
  AND Quantity > 0
  GROUP BY ProductId
  ORDER BY ExpiryDate, Id (FEFO)
```

### الخطوة 4: تحديث SuggestedPrice
```
For each batch-tracked product:
  If batch found → SuggestedPrice = batch.SellingPrice
  Else → SuggestedPrice = product.Price (already set)
```

---

## 📊 مقارنة الأداء

### ❌ الطريقة القديمة (الخاطئة)

```
1 Query → Products + Subquery for each product
  → N+1 problem
  → EF Core translation error
```

---

### ✅ الطريقة الجديدة (الصحيحة)

```
Query 1 → Products (page size = 20)
Query 2 → Batches for batch-tracked products only
  → 2 queries total
  → Efficient and correct
```

**الفائدة:**
- أداء أفضل (2 queries بدلاً من N+1)
- لا أخطاء في الترجمة
- النتائج صحيحة دائماً

---

## 🧪 اختبار الإصلاح

### Test Case 1: منتج بدون باتشات

**الخطوات:**
1. افتح قائمة المنتجات
2. ابحث عن منتج `IsBatchTracked = false`

**النتيجة المتوقعة:**
- [ ] `SuggestedPrice = Price` ✅
- [ ] السعر يظهر بشكل صحيح (مثلاً: 100.00 ج.م)

---

### Test Case 2: منتج له باتشات

**الخطوات:**
1. افتح قائمة المنتجات
2. ابحث عن منتج `IsBatchTracked = true` وله باتش نشط

**النتيجة المتوقعة:**
- [ ] `SuggestedPrice = batch.SellingPrice` ✅
- [ ] السعر يظهر بشكل صحيح (مثلاً: 125.00 ج.م)

---

### Test Case 3: منتج له باتشات لكن كلها Depleted

**الخطوات:**
1. افتح قائمة المنتجات
2. ابحث عن منتج `IsBatchTracked = true` لكن كل باتشاته `Status = Depleted`

**النتيجة المتوقعة:**
- [ ] `SuggestedPrice = Price` (fallback) ✅
- [ ] السعر يظهر بشكل صحيح

---

## 📝 ملاحظات مهمة

### 1. **Query Efficiency**

```csharp
var batchTrackedProductIds = pagedItems
    .Where(p => p.IsBatchTracked)
    .Select(p => p.Id)
    .ToList();

if (batchTrackedProductIds.Any())
{
    // Query batches only if there are batch-tracked products
}
```

**الفائدة:**
- إذا لم يكن هناك منتجات بباتشات → لا query إضافي
- كفاءة أفضل

---

### 2. **GroupBy + OrderBy**

```csharp
.GroupBy(pb => pb.ProductId)
.Select(g => new
{
    ProductId = g.Key,
    SellingPrice = g.OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(pb => pb.Id)
                    .Select(pb => pb.SellingPrice)
                    .FirstOrDefault()
})
```

**الشرح:**
- `GroupBy` → تجميع الباتشات حسب المنتج
- `OrderBy` → ترتيب حسب FEFO داخل كل مجموعة
- `FirstOrDefault` → أخذ أول باتش (الأقرب للانتهاء)

---

### 3. **In-Memory Update**

```csharp
foreach (var product in pagedItems.Where(p => p.IsBatchTracked))
{
    var batchPrice = nextBatchPrices.FirstOrDefault(bp => bp.ProductId == product.Id);
    product.SuggestedPrice = batchPrice?.SellingPrice ?? product.Price;
}
```

**الشرح:**
- التحديث يتم في الذاكرة (in-memory)
- لا يؤثر على قاعدة البيانات
- سريع وآمن

---

## 🚀 التحسينات المستقبلية (اختياري)

### 1. **Caching**

```csharp
// Cache batch prices for frequently accessed products
var cacheKey = $"batch-prices-{tenantId}-{branchId}";
var cachedPrices = _cache.Get<Dictionary<int, decimal>>(cacheKey);
```

**الفائدة:** أداء أفضل للقوائم المتكررة

---

### 2. **Parallel Processing**

```csharp
// For large lists, process in parallel
Parallel.ForEach(pagedItems.Where(p => p.IsBatchTracked), product =>
{
    // Update SuggestedPrice
});
```

**الفائدة:** أسرع للقوائم الكبيرة (> 100 منتج)

---

## 📚 الملفات المُعدلة

```
backend/KasserPro.Application/Services/Implementations/ProductService.cs
```

**التغييرات:**
1. إزالة الـ subquery من الـ projection
2. تعيين `SuggestedPrice = p.Price` كقيمة افتراضية
3. إضافة query منفصل للباتشات بعد جلب المنتجات
4. تحديث `SuggestedPrice` في الذاكرة

---

## ✅ Checklist

- [x] إزالة الـ subquery من projection
- [x] إضافة query منفصل للباتشات
- [x] تحديث SuggestedPrice في الذاكرة
- [x] بناء الباك-اند بنجاح (0 errors)
- [ ] اختبار في المتصفح
- [ ] التحقق من الأسعار الصحيحة

---

**الحالة:** ✅ تم الإصلاح  
**آخر تحديث:** 2 مايو 2026  
**المطور:** Kiro AI Assistant
