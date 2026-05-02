# 🔍 Batch Cost Tracking Audit Report

> **تاريخ المراجعة:** 2 مايو 2026  
> **الهدف:** التأكد من تسجيل سعر التكلفة (`CostPrice`) بشكل صحيح في نظام الباتشات

---

## ✅ النتيجة النهائية: النظام آمن ودقيق

النظام الحالي **يسجل سعر التكلفة بشكل صحيح** في كل مرحلة من مراحل دورة حياة المنتج.

---

## 📋 مراحل تسجيل سعر التكلفة

### 1️⃣ **عند الشراء (Purchase Invoice Confirmation)**

#### الموقع: `PurchaseInvoiceService.ConfirmAsync()`

```csharp
// ✅ السطر 627-628 في PurchaseInvoiceService.cs
batch = new ProductBatch
{
    TenantId = tenantId,
    BranchId = invoice.BranchId,
    ProductId = product.Id,
    BatchNumber = batchNumber,
    PurchaseDate = invoice.InvoiceDate,
    ExpiryDate = item.ExpiryDate,
    ProductionDate = item.ProductionDate,
    Quantity = item.Quantity,
    InitialQuantity = item.Quantity,
    CostPrice = item.PurchasePrice,  // ✅ سعر التكلفة بيتسجل هنا
    SellingPrice = item.SellingPrice > 0 ? item.SellingPrice : null,
    SupplierName = invoice.SupplierName,
    PurchaseInvoiceId = invoice.Id,
    Status = BatchStatus.Active,
    CreatedAt = DateTime.UtcNow
};
```

**المصدر:** `CreatePurchaseInvoiceItemRequest.PurchasePrice`

**Validation:**
```csharp
// ✅ السطر 133-137 في PurchaseInvoiceService.cs
if (item.PurchasePrice < 0)
{
    return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
        ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE,
        ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE));
}
```

---

### 2️⃣ **عند البيع (Order Creation)**

#### الموقع: `OrderService.CreateAsync()` و `AddItemAsync()`

```csharp
// ✅ في OrderService.cs
var orderItem = new OrderItem
{
    ProductId = product.Id,
    UnitCost = batchCost ?? product.AverageCost ?? product.Cost,  // ✅ Snapshot
    UnitPrice = unitPrice,
    BatchId = batchId,
    BatchNumber = batchNumber,
    ExpiryDate = expiryDate
};
```

**الأولوية:**
1. `batchCost` - من `ProductBatch.CostPrice` (إذا تم اختيار باتش محدد)
2. `product.AverageCost` - متوسط التكلفة المرجح
3. `product.Cost` - سعر التكلفة الافتراضي

**دالة الحصول على سعر الباتش:**
```csharp
// ResolveBatchSaleSnapshotAsync() تُرجع:
// - batchCost: ProductBatch.CostPrice
```

---

### 3️⃣ **في التقارير (Reports)**

#### أ) **تقرير الأرباح والخسائر (Profit & Loss)**

```csharp
// ✅ FinancialReportService.cs - السطر 93-94
var totalCost = Math.Round(orders
    .SelectMany(o => o.Items)
    .Sum(i => (i.UnitCost ?? 0) * i.Quantity), 2);  // ✅ Historical Snapshot
```

#### ب) **تقرير حركة المنتجات (Product Movement)**

```csharp
// ✅ ProductReportService.cs - السطر 113-114
var cost = soldItems.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity)
         - returnedItems.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity));
```

#### ج) **تقرير المنتجات الأكثر ربحية (Profitability)**

```csharp
// ✅ ProductReportService.cs - السطر 234
var cost = g.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity);
```

#### د) **تقرير تكلفة البضاعة المباعة (COGS)**

```csharp
// ✅ ProductReportService.cs - السطر 460-461
var totalCost = Math.Round(
    orderItems.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity)
    - returnItemsCogs.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity)),
    2);
```

---

### 4️⃣ **عند المرتجعات (Refunds)**

```csharp
// ✅ OrderService.RefundAsync() - السطر في return order
var returnItem = new OrderItem
{
    UnitCost = orderItem.UnitCost,  // ✅ نفس التكلفة من الطلب الأصلي
    Quantity = refundItem.Quantity
};
```

---

## 🔒 آليات الحماية (Safety Mechanisms)

### 1. **Snapshot Pattern**
- سعر التكلفة يُسجل في `OrderItem.UnitCost` **وقت البيع**
- التقارير تستخدم القيمة المسجلة، **ليس** السعر الحالي
- هذا يضمن دقة تاريخية 100%

### 2. **Fallback Chain**
```
BatchCost → AverageCost → Product.Cost → 0
```

### 3. **Validation في كل مرحلة**

| المرحلة | Validation |
|---------|-----------|
| Purchase Invoice | `PurchasePrice >= 0` ✅ |
| Order Creation | `UnitCost` يُحسب من مصادر موثوقة ✅ |
| Reports | `(i.UnitCost ?? 0)` - يتعامل مع null بأمان ✅ |

### 4. **تحذير للمستخدم**

```csharp
// ✅ في CogsReportDto
public int ProductsWithNoCostCount { get; set; }
```

التقرير يُظهر عدد المنتجات التي **ليس لها سعر تكلفة**، مما يُنبه المستخدم لمشكلة محتملة.

---

## ⚠️ السيناريوهات الحرجة

### السيناريو 1: باتش بدون `CostPrice`

**الحالة:**
```csharp
ProductBatch.CostPrice = null
Product.AverageCost = null
Product.Cost = null
```

**النتيجة:**
```csharp
OrderItem.UnitCost = null
// في التقارير:
(i.UnitCost ?? 0) = 0  // ❌ التكلفة = 0، الربح مُبالغ فيه
```

**الحل:**
- التأكد من تسجيل `PurchasePrice` في كل فاتورة شراء ✅ (موجود)
- تحديث `Product.AverageCost` بعد كل شراء ✅ (موجود)

### السيناريو 2: منتج بدون باتشات

**الحالة:**
```csharp
Product.IsBatchTracked = false
// لا يوجد ProductBatch
```

**النتيجة:**
```csharp
OrderItem.UnitCost = product.AverageCost ?? product.Cost
```

**الحل:** النظام يستخدم `AverageCost` تلقائيًا ✅

---

## 📊 تحديث `AverageCost` (Weighted Average)

```csharp
// ✅ السطر 651-659 في PurchaseInvoiceService.cs
var oldStock = balanceBefore;
var oldAvgCost = product.AverageCost ?? product.Cost ?? 0m;
var newStock = balanceBefore + item.Quantity;
if (newStock > 0)
{
    var totalOldValue = oldStock * oldAvgCost;
    var totalNewValue = item.Quantity * item.PurchasePrice;
    product.AverageCost = Math.Round((totalOldValue + totalNewValue) / newStock, 4);
}
```

**مثال:**
```
المخزون القديم: 10 وحدات × 50 ج.م = 500 ج.م
الشراء الجديد: 5 وحدات × 60 ج.م = 300 ج.م
المتوسط الجديد: (500 + 300) / (10 + 5) = 53.33 ج.م
```

---

## ✅ الخلاصة

### ما هو موجود ويعمل بشكل صحيح:

1. ✅ **تسجيل `CostPrice` في الباتش** - السطر 627 في `PurchaseInvoiceService.cs`
2. ✅ **Snapshot في `OrderItem.UnitCost`** - يُسجل وقت البيع
3. ✅ **التقارير تستخدم Historical Cost** - دقة 100%
4. ✅ **Weighted Average Cost** - يُحدث تلقائيًا بعد كل شراء
5. ✅ **Validation للأسعار** - `PurchasePrice >= 0`
6. ✅ **Fallback Chain** - `BatchCost → AverageCost → Cost → 0`
7. ✅ **تحذير للمستخدم** - `ProductsWithNoCostCount` في تقرير COGS

### ما يحتاج مراقبة:

⚠️ **التأكد من إدخال `PurchasePrice` في كل فاتورة شراء**
- الـ validation موجود (`PurchasePrice >= 0`)
- لكن يُسمح بـ `PurchasePrice = 0` (قد يكون هدية أو عينة)

---

## 🎯 التوصيات

### 1. إضافة Warning للمستخدم

```csharp
// في PrepareAsync() أو CreateAsync()
if (item.PurchasePrice == 0)
{
    // تحذير: سعر التكلفة = 0، هل هذا صحيح؟
}
```

### 2. تقرير مخصص للباتشات

إضافة endpoint جديد:
```
GET /api/product-reports/batch-cost-analysis
```

يُظهر:
- الباتشات المتاحة لكل منتج
- `CostPrice` لكل باتش
- `SellingPrice` لكل باتش
- الكمية المتبقية
- هامش الربح المتوقع

### 3. Dashboard Alert

```
⚠️ تنبيه: 5 منتجات بدون سعر تكلفة
```

---

## 📝 ملاحظات إضافية

### الفرق بين `Cost` و `AverageCost`

| الحقل | الاستخدام |
|-------|-----------|
| `Product.Cost` | سعر التكلفة الافتراضي (يدوي) |
| `Product.AverageCost` | متوسط التكلفة المرجح (تلقائي) |
| `ProductBatch.CostPrice` | سعر التكلفة لباتش محدد |
| `OrderItem.UnitCost` | Snapshot وقت البيع |

### أولوية الاستخدام في البيع:

```
1. ProductBatch.CostPrice (إذا تم اختيار باتش)
2. Product.AverageCost (الأفضل - يُحدث تلقائيًا)
3. Product.Cost (fallback يدوي)
4. 0 (آخر حل)
```

---

**المراجع:** Principal Software Architect  
**الحالة:** ✅ Approved - النظام آمن ودقيق  
**آخر تحديث:** 2 مايو 2026
