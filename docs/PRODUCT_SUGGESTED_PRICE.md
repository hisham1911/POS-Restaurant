# 💰 عرض السعر المقترح للمنتجات ذات الباتشات

> **التاريخ:** 2 مايو 2026  
> **الحالة:** ✅ مكتمل  
> **الهدف:** عرض سعر الباتش المقترح في قائمة المنتجات ونقطة البيع

---

## 📋 المشكلة

- المنتجات التي لها باتشات تعرض السعر الأساسي (`Product.Price`)
- لكن في نقطة البيع، يتم استخدام سعر الباتش المقترح (حسب FEFO)
- هذا يسبب **ارتباك** للمستخدم - السعر المعروض ≠ السعر الفعلي

---

## ✅ الحل

### 1. **إضافة حقل `SuggestedPrice` في Backend**
- حقل جديد في `ProductDto`
- يحتوي على سعر الباتش المقترح إذا كان المنتج له باتشات نشطة
- وإلا يحتوي على السعر الأساسي

### 2. **حساب `SuggestedPrice` تلقائياً**
- في `ProductService.GetAllAsync()`: استخدام LINQ projection
- في `ProductService.GetByIdAsync()`: استخدام query منفصل
- الباتش المقترح = أول باتش نشط مرتب حسب FEFO (الأقرب للانتهاء أولاً)

### 3. **عرض `SuggestedPrice` في Frontend**
- في قائمة المنتجات: عرض السعر المقترح + السعر الأساسي (إذا مختلف)
- في نقطة البيع: عرض السعر المقترح فقط

---

## 🔧 التغييرات التقنية

### Backend

#### 1. **ProductDto.cs**

```csharp
public class ProductDto
{
    public decimal Price { get; set; }
    
    /// <summary>
    /// Suggested selling price - uses next batch price if product has active batches, 
    /// otherwise uses base Price.
    /// This is the price that will actually be used in POS.
    /// </summary>
    public decimal SuggestedPrice { get; set; }
    
    public decimal? Cost { get; set; }
    // ... rest of properties
}
```

---

#### 2. **ProductService.cs - GetAllAsync()**

```csharp
// Get next available batch for each product (for suggested price)
var productBatchesQuery = _unitOfWork.ProductBatches.Query()
    .Where(pb => pb.TenantId == tenantId 
              && pb.BranchId == branchId 
              && pb.Status == BatchStatus.Active
              && pb.Quantity > 0)
    .OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
    .ThenBy(pb => pb.Id);

var projectedQuery = query.Select(p => new ProductDto
{
    // ... other properties
    Price = p.Price,
    SuggestedPrice = p.IsBatchTracked
        ? productBatchesQuery
            .Where(pb => pb.ProductId == p.Id)
            .Select(pb => (decimal?)pb.SellingPrice)
            .FirstOrDefault() ?? p.Price
        : p.Price,
    // ... rest of properties
});
```

**الشرح:**
- إذا كان المنتج `IsBatchTracked = true` → ابحث عن أول باتش نشط
- إذا وُجد باتش → استخدم `SellingPrice` منه
- إذا لم يوجد باتش → استخدم `Price` الأساسي
- إذا كان المنتج `IsBatchTracked = false` → استخدم `Price` الأساسي

---

#### 3. **ProductService.cs - GetByIdAsync()**

```csharp
// Get suggested price from next available batch
decimal suggestedPrice = product.Price;
if (product.IsBatchTracked)
{
    var nextBatch = await _unitOfWork.ProductBatches.Query()
        .Where(pb => pb.TenantId == tenantId 
                  && pb.BranchId == branchId 
                  && pb.ProductId == product.Id
                  && pb.Status == BatchStatus.Active
                  && pb.Quantity > 0)
        .OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
        .ThenBy(pb => pb.Id)
        .FirstOrDefaultAsync();
    
    if (nextBatch != null)
    {
        suggestedPrice = nextBatch.SellingPrice;
    }
}

return ApiResponse<ProductDto>.Ok(new ProductDto
{
    // ... other properties
    Price = product.Price,
    SuggestedPrice = suggestedPrice,
    // ... rest of properties
});
```

---

### Frontend

#### 1. **product.types.ts**

```typescript
export interface Product {
  id: number;
  name: string;
  // ... other properties
  price: number;
  suggestedPrice: number; // السعر المقترح - من الباتش إذا كان المنتج له باتشات، وإلا السعر الأساسي
  cost?: number;
  // ... rest of properties
}
```

---

#### 2. **ProductsPage.tsx**

```tsx
<td className="px-4 py-3">
  <div className="flex flex-col">
    <span className="font-semibold text-primary-600">
      {formatCurrency(product.suggestedPrice)}
    </span>
    {product.isBatchTracked && product.suggestedPrice !== product.price && (
      <span className="text-xs text-gray-500">
        الأساسي: {formatCurrency(product.price)}
      </span>
    )}
  </div>
</td>
```

**الشرح:**
- عرض `suggestedPrice` بخط كبير وبارز
- إذا كان المنتج له باتشات والسعر مختلف → عرض السعر الأساسي بخط صغير تحته

---

#### 3. **ProductCard.tsx (POS)**

```typescript
// Use suggested price (from batch if available, otherwise base price)
const displayPrice = cartItems[0]?.product.suggestedPrice ?? product.suggestedPrice;
```

**الشرح:**
- استخدام `suggestedPrice` بدلاً من `price`
- هذا هو السعر الذي سيُستخدم فعلياً في البيع

---

## 🎨 الواجهة

### 1. قائمة المنتجات

#### قبل التعديل

```
┌────────────────────────────────────────────────────────┐
│ المنتج          التصنيف      السعر        الكمية      │
├────────────────────────────────────────────────────────┤
│ منتج أ         مشروبات      100.00 ج.م   50          │
│ منتج ب         طعام         200.00 ج.م   30          │
└────────────────────────────────────────────────────────┘
```

**المشكلة:** السعر المعروض (100 ج.م) قد لا يكون السعر الفعلي!

---

#### بعد التعديل

```
┌────────────────────────────────────────────────────────┐
│ المنتج          التصنيف      السعر        الكمية      │
├────────────────────────────────────────────────────────┤
│ منتج أ         مشروبات      125.00 ج.م   50          │
│                              الأساسي: 100.00 ج.م       │
│ منتج ب         طعام         200.00 ج.م   30          │
└────────────────────────────────────────────────────────┘
```

**الفائدة:**
- السعر المعروض (125 ج.م) هو السعر الفعلي من الباتش
- السعر الأساسي (100 ج.م) معروض بخط صغير للمرجعية

---

### 2. نقطة البيع

#### قبل التعديل

```
┌──────────────────┐
│  منتج أ         │
│  🧃             │
│                 │
│  100.00 ج.م    │ ← السعر الأساسي
└──────────────────┘
```

**المشكلة:** السعر المعروض ≠ السعر الفعلي في الفاتورة!

---

#### بعد التعديل

```
┌──────────────────┐
│  منتج أ         │
│  🧃             │
│                 │
│  125.00 ج.م    │ ← السعر من الباتش
└──────────────────┘
```

**الفائدة:** السعر المعروض = السعر الفعلي ✅

---

## 🔄 كيف يعمل النظام

### السيناريو 1: منتج بدون باتشات

```
المنتج:
- IsBatchTracked = false
- Price = 100 ج.م

Backend يحسب:
- SuggestedPrice = Price = 100 ج.م

Frontend يعرض:
- في قائمة المنتجات: 100.00 ج.م
- في نقطة البيع: 100.00 ج.م
```

---

### السيناريو 2: منتج له باتشات نشطة

```
المنتج:
- IsBatchTracked = true
- Price = 100 ج.م (السعر الأساسي)

الباتشات:
- BATCH-001: SellingPrice = 125 ج.م, ExpiryDate = 2026-06-01, Status = Active
- BATCH-002: SellingPrice = 130 ج.م, ExpiryDate = 2026-12-31, Status = Active

Backend يحسب:
- الباتش المقترح = BATCH-001 (الأقرب للانتهاء حسب FEFO)
- SuggestedPrice = 125 ج.م

Frontend يعرض:
- في قائمة المنتجات:
  * 125.00 ج.م (بخط كبير)
  * الأساسي: 100.00 ج.م (بخط صغير)
- في نقطة البيع: 125.00 ج.م
```

---

### السيناريو 3: منتج له باتشات لكن كلها Depleted

```
المنتج:
- IsBatchTracked = true
- Price = 100 ج.م

الباتشات:
- BATCH-001: Status = Depleted
- BATCH-002: Status = Depleted

Backend يحسب:
- لا يوجد باتش نشط
- SuggestedPrice = Price = 100 ج.م (fallback)

Frontend يعرض:
- في قائمة المنتجات: 100.00 ج.م
- في نقطة البيع: 100.00 ج.م
```

---

## 📊 مقارنة قبل وبعد

| الجانب | ❌ قبل التعديل | ✅ بعد التعديل |
|--------|----------------|----------------|
| **قائمة المنتجات** | عرض `Price` فقط | عرض `SuggestedPrice` + `Price` (إذا مختلف) |
| **نقطة البيع** | عرض `Price` | عرض `SuggestedPrice` |
| **الدقة** | السعر المعروض قد يكون خاطئ ❌ | السعر المعروض = السعر الفعلي ✅ |
| **الشفافية** | المستخدم لا يعرف السعر الفعلي | المستخدم يرى السعر الفعلي |
| **تجربة المستخدم** | مربكة ❌ | واضحة ✅ |

---

## 🎯 الفوائد

### 1. **دقة السعر**
```
❌ قبل: السعر المعروض في القائمة ≠ السعر في الفاتورة
✅ بعد: السعر المعروض = السعر الفعلي دائماً
```

---

### 2. **شفافية**
```
❌ قبل: المستخدم لا يعرف أي سعر سيُستخدم
✅ بعد: المستخدم يرى السعر الفعلي من الباتش
```

---

### 3. **تجربة مستخدم أفضل**
```
❌ قبل: ارتباك عند اختلاف السعر في الفاتورة
✅ بعد: لا مفاجآت - السعر واضح من البداية
```

---

### 4. **توافق مع نظام FEFO**
```
✅ السعر المعروض يطابق الباتش الذي سيُستخدم فعلياً (الأقرب للانتهاء)
```

---

## 🧪 اختبار الميزة

### Test Case 1: منتج بدون باتشات

**الخطوات:**
1. افتح قائمة المنتجات
2. ابحث عن منتج `IsBatchTracked = false`
3. تحقق من السعر المعروض

**النتيجة المتوقعة:**
- [ ] السعر المعروض = `Price` الأساسي
- [ ] لا يوجد سطر "الأساسي: ..."

---

### Test Case 2: منتج له باتشات نشطة

**الخطوات:**
1. أنشئ منتج `IsBatchTracked = true`, `Price = 100`
2. أنشئ باتش: `SellingPrice = 125`, `Status = Active`
3. افتح قائمة المنتجات
4. تحقق من السعر المعروض

**النتيجة المتوقعة:**
- [ ] السعر المعروض = `125.00 ج.م` (من الباتش)
- [ ] سطر ثاني: "الأساسي: 100.00 ج.م"

---

### Test Case 3: نقطة البيع - السعر الصحيح

**الخطوات:**
1. افتح نقطة البيع
2. أضف منتج له باتش بسعر 125 ج.م
3. تحقق من السعر في الكارت
4. أضف المنتج للسلة
5. تحقق من السعر في الفاتورة

**النتيجة المتوقعة:**
- [ ] السعر في الكارت = 125.00 ج.م
- [ ] السعر في السلة = 125.00 ج.م
- [ ] السعر في الفاتورة = 125.00 ج.م
- [ ] كل الأسعار متطابقة ✅

---

### Test Case 4: تغيير سعر الباتش

**الخطوات:**
1. منتج له باتش بسعر 125 ج.م
2. غيّر سعر الباتش إلى 150 ج.م
3. أعد تحميل قائمة المنتجات
4. تحقق من السعر المعروض

**النتيجة المتوقعة:**
- [ ] السعر المعروض = 150.00 ج.م (السعر الجديد)
- [ ] التحديث فوري ✅

---

### Test Case 5: باتش نفد (Depleted)

**الخطوات:**
1. منتج له باتشين:
   - BATCH-001: `SellingPrice = 125`, `Status = Depleted`
   - BATCH-002: `SellingPrice = 130`, `Status = Active`
2. افتح قائمة المنتجات
3. تحقق من السعر المعروض

**النتيجة المتوقعة:**
- [ ] السعر المعروض = 130.00 ج.م (من BATCH-002)
- [ ] BATCH-001 مُتجاهل لأنه Depleted ✅

---

## 📝 ملاحظات مهمة

### 1. **الترتيب حسب FEFO**

```csharp
.OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
.ThenBy(pb => pb.Id)
```

**الشرح:**
- الباتشات بدون تاريخ انتهاء تُعامل كأنها لا تنتهي (`DateTime.MaxValue`)
- إذا كان تاريخ الانتهاء متساوي → الترتيب حسب `Id` (الأقدم أولاً)

---

### 2. **الباتشات النشطة فقط**

```csharp
&& pb.Status == BatchStatus.Active
&& pb.Quantity > 0
```

**الشرح:**
- فقط الباتشات `Active` تُستخدم
- الباتشات بكمية صفر مُتجاهلة

---

### 3. **Fallback للسعر الأساسي**

```csharp
SuggestedPrice = p.IsBatchTracked
    ? productBatchesQuery
        .Where(pb => pb.ProductId == p.Id)
        .Select(pb => (decimal?)pb.SellingPrice)
        .FirstOrDefault() ?? p.Price  // ← fallback
    : p.Price;
```

**الشرح:**
- إذا لم يوجد باتش نشط → استخدام `Price` الأساسي
- هذا يضمن أن `SuggestedPrice` دائماً له قيمة

---

### 4. **عرض السعر الأساسي في القائمة**

```tsx
{product.isBatchTracked && product.suggestedPrice !== product.price && (
  <span className="text-xs text-gray-500">
    الأساسي: {formatCurrency(product.price)}
  </span>
)}
```

**الشرح:**
- السعر الأساسي يُعرض فقط إذا:
  1. المنتج له باتشات (`isBatchTracked = true`)
  2. السعر المقترح ≠ السعر الأساسي
- هذا يتجنب عرض معلومات مكررة

---

## 🚀 التحسينات المستقبلية (اختياري)

### 1. **عرض معلومات الباتش في الكارت**

```tsx
{product.isBatchTracked && (
  <p className="text-xs text-gray-500">
    من الباتش: {nextBatch.batchNumber}
  </p>
)}
```

**الفائدة:** شفافية أكبر للكاشير

---

### 2. **تنبيه عند اختلاف السعر**

```tsx
{product.suggestedPrice > product.price && (
  <span className="text-xs text-green-600">
    ↑ أعلى من الأساسي
  </span>
)}
```

**الفائدة:** لفت انتباه المستخدم للفرق

---

### 3. **Cache للباتشات**

```csharp
// Cache batch prices for better performance
var batchPrices = await _unitOfWork.ProductBatches.Query()
    .Where(pb => pb.TenantId == tenantId && pb.BranchId == branchId)
    .GroupBy(pb => pb.ProductId)
    .Select(g => new { ProductId = g.Key, Price = g.Min(pb => pb.SellingPrice) })
    .ToDictionaryAsync(x => x.ProductId, x => x.Price);
```

**الفائدة:** أداء أفضل للقوائم الكبيرة

---

## 📚 الملفات المُعدلة

### Backend

```
backend/KasserPro.Application/DTOs/Products/ProductDto.cs
backend/KasserPro.Application/Services/Implementations/ProductService.cs
```

**التغييرات:**
1. إضافة `SuggestedPrice` property في `ProductDto`
2. إضافة query للباتشات في `GetAllAsync()`
3. حساب `SuggestedPrice` في projection
4. إضافة logic لحساب `SuggestedPrice` في `GetByIdAsync()`

---

### Frontend

```
frontend/src/types/product.types.ts
frontend/src/pages/products/ProductsPage.tsx
frontend/src/components/pos/ProductCard.tsx
```

**التغييرات:**
1. إضافة `suggestedPrice` في `Product` interface
2. عرض `suggestedPrice` في قائمة المنتجات
3. عرض السعر الأساسي بخط صغير (إذا مختلف)
4. استخدام `suggestedPrice` في نقطة البيع

---

## ✅ Checklist النهائي

### التطوير
- [x] إضافة `SuggestedPrice` في `ProductDto`
- [x] حساب `SuggestedPrice` في `GetAllAsync()`
- [x] حساب `SuggestedPrice` في `GetByIdAsync()`
- [x] إضافة `suggestedPrice` في Frontend types
- [x] عرض `suggestedPrice` في قائمة المنتجات
- [x] عرض `suggestedPrice` في نقطة البيع

### الاختبار
- [ ] Test Case 1: منتج بدون باتشات
- [ ] Test Case 2: منتج له باتشات نشطة
- [ ] Test Case 3: نقطة البيع - السعر الصحيح
- [ ] Test Case 4: تغيير سعر الباتش
- [ ] Test Case 5: باتش نفد (Depleted)

### التوثيق
- [x] كتابة وثيقة شاملة
- [x] شرح المشكلة والحل
- [x] أمثلة مرئية
- [x] Test cases

---

**الحالة:** ✅ تم التنفيذ بنجاح  
**آخر تحديث:** 2 مايو 2026  
**المطور:** Kiro AI Assistant
