# 🔧 إصلاح مشكلة ظهور المنتجات في المخزون

## 📋 المشكلة الفعلية

منتجات موجودة وعندها `StockQuantity > 0` في جدول Products، لكن **لا تظهر في صفحة المخزون** (Branch Inventory).

## 🔍 السبب الجذري

النظام فيه **نظامين للمخزون**:

1. **Product.StockQuantity** - المخزون القديم (Global/Legacy)
2. **BranchInventory.Quantity** - المخزون الجديد (Per Branch)

### المشكلة:
عند إنشاء منتج جديد في `ProductService.CreateAsync`:
- ✅ بيتم حفظ `StockQuantity` في جدول Products
- ❌ **لكن مش بيتم إنشاء سجل في BranchInventory**

النتيجة: المنتج عنده كمية لكن مش بيظهر في صفحة المخزون لأن صفحة المخزون بتقرأ من `BranchInventory` مش من `Product.StockQuantity`!

## ✅ الحل المطبق

### 1. تعديل ProductService.CreateAsync

عند إنشاء منتج جديد، الآن بيتم:

```csharp
// 1. Create Product with StockQuantity = 0
var product = new Product {
    // ... other fields
    StockQuantity = 0, // Set to 0, actual stock in BranchInventory
};

await _unitOfWork.Products.AddAsync(product);
await _unitOfWork.SaveChangesAsync();

// 2. Create BranchInventory records for ALL branches
var branches = await _unitOfWork.Branches.Query()
    .Where(b => b.TenantId == _currentUser.TenantId)
    .ToListAsync();

foreach (var branch in branches)
{
    var branchInventory = new BranchInventory
    {
        TenantId = _currentUser.TenantId,
        BranchId = branch.Id,
        ProductId = product.Id,
        Quantity = request.StockQuantity, // Use requested quantity
        ReorderLevel = request.LowStockThreshold,
        LastUpdatedAt = DateTime.UtcNow
    };
    
    await _unitOfWork.BranchInventories.AddAsync(branchInventory);
}

await _unitOfWork.SaveChangesAsync();
```

### 2. إضافة فلتر IsActive في Inventory APIs

تم إضافة فلتر `Product.IsActive` في 3 endpoints لـ consistency:

- `GetBranchInventoryAsync` - عرض مخزون الفرع
- `GetProductInventoryAcrossBranchesAsync` - عرض مخزون منتج عبر الفروع  
- `GetLowStockItemsAsync` - المنتجات منخفضة المخزون

```csharp
// Example: GetBranchInventoryAsync
.Where(i => i.TenantId == _currentUserService.TenantId && 
           i.BranchId == branchId &&
           i.Product.IsActive) // Only show active products
```

### 3. SQL Script لإصلاح المنتجات الموجودة

تم إنشاء `fix-missing-branch-inventory.sql` لإصلاح المنتجات القديمة:

```sql
-- Creates BranchInventory records for products missing them
INSERT INTO BranchInventories (TenantId, BranchId, ProductId, Quantity, ReorderLevel, LastUpdatedAt, CreatedAt, UpdatedAt)
SELECT 
    p.TenantId,
    b.Id as BranchId,
    p.Id as ProductId,
    COALESCE(p.StockQuantity, 0) as Quantity,
    COALESCE(p.LowStockThreshold, 10) as ReorderLevel,
    datetime('now') as LastUpdatedAt,
    datetime('now') as CreatedAt,
    datetime('now') as UpdatedAt
FROM Products p
CROSS JOIN Branches b
WHERE p.IsActive = 1
  AND p.TrackInventory = 1
  AND p.TenantId = b.TenantId
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id AND bi.BranchId = b.Id
  );
```

## 📊 التأثير

- ✅ المنتجات الجديدة ستظهر تلقائياً في صفحة المخزون
- ✅ كل منتج سيكون له سجل في BranchInventory لكل فرع
- ✅ Consistency بين Product.StockQuantity و BranchInventory.Quantity
- ✅ المنتجات غير النشطة لن تظهر في المخزون

## 🧪 Testing

### Test Case 1: Create New Product
```
1. POST /api/products with StockQuantity = 50
2. Check Products table → StockQuantity = 0
3. Check BranchInventories table → Quantity = 50 for each branch
4. Open inventory page → Product should appear with Quantity = 50
```

### Test Case 2: Multi-Branch Scenario
```
1. Tenant has 3 branches
2. Create product with StockQuantity = 100
3. Check BranchInventories → Should have 3 records (one per branch)
4. Each branch inventory page → Product appears with Quantity = 100
```

### Test Case 3: Fix Existing Products
```
1. Run fix-missing-branch-inventory.sql
2. Check products that had StockQuantity but no BranchInventory
3. Verify BranchInventory records created
4. Open inventory page → Previously missing products now appear
```

## 📝 ملاحظات إضافية

### نظام المخزون الجديد:

- **Product.StockQuantity** → Legacy field (يُفضل تركه = 0)
- **BranchInventory.Quantity** → المصدر الفعلي للمخزون
- كل منتج له سجل منفصل لكل فرع
- يسمح بأسعار وكميات مختلفة لكل فرع

### خطوات تشغيل الـ SQL Script:

```bash
# 1. Backup database first
sqlite3 kasserpro.db ".backup backup.db"

# 2. Run the fix script
sqlite3 kasserpro.db < fix-missing-branch-inventory.sql

# 3. Verify results
sqlite3 kasserpro.db "SELECT COUNT(*) FROM BranchInventories;"
```

### TrackInventory Behavior:

- منتجات بـ `TrackInventory = false` لا تحتاج سجلات BranchInventory
- مناسبة للخدمات أو المنتجات غير المادية
- الـ POS يخفيها من شاشة المخزون تلقائياً

## 🔗 Related Files

- `src/KasserPro.Application/Services/Implementations/ProductService.cs` - تم تعديل CreateAsync
- `src/KasserPro.Infrastructure/Services/InventoryService.cs` - تم إضافة فلتر IsActive
- `fix-missing-branch-inventory.sql` - SQL script لإصلاح المنتجات الموجودة
- `client/src/components/inventory/BranchInventoryList.tsx` - Frontend inventory display
