# 🔧 شرح الكود من الداتابيز للباك إند

## 📊 1. الداتابيز (SQLite)

### جدول Products
```sql
CREATE TABLE Products (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Type INTEGER NOT NULL DEFAULT 1,  -- 1=Physical, 2=Service
    TrackInventory INTEGER NOT NULL,  -- يتم حسابه تلقائياً من Type
    StockQuantity INTEGER,
    Price REAL NOT NULL,
    CategoryId INTEGER NOT NULL,
    -- ... باقي الحقول
);
```

**مثال بيانات:**
```
Id | Name        | Type | TrackInventory | StockQuantity
---|-------------|------|----------------|---------------
1  | قراقيش     | 1    | 1              | 91
2  | لحم قطع    | 1    | 1              | 80
25 | خدمة توصيل | 2    | 0              | NULL
```

### جدول OrderItems
```sql
CREATE TABLE OrderItems (
    Id INTEGER PRIMARY KEY,
    OrderId INTEGER NOT NULL,
    ProductId INTEGER,              -- ⚠️ nullable دلوقتي
    IsCustomItem INTEGER NOT NULL DEFAULT 0,
    CustomName TEXT,                -- للمنتجات المخصصة
    CustomUnitPrice REAL,           -- للمنتجات المخصصة
    CustomTaxRate REAL,             -- للمنتجات المخصصة
    ProductName TEXT NOT NULL,      -- snapshot
    Quantity INTEGER NOT NULL,
    Total REAL NOT NULL,
    -- ... باقي الحقول
);
```

**مثال بيانات:**
```
Id | ProductId | IsCustomItem | CustomName           | ProductName      | Quantity | Total
---|-----------|--------------|----------------------|------------------|----------|-------
1  | 7         | 0            | NULL                 | لحم راس          | 2        | 513.0
2  | NULL      | 1            | "تغليف هدية خاصة"   | تغليف هدية خاصة | 1        | 10.0
```

---

## 🏗️ 2. الـ Entity Layer (Domain)

### Product Entity
```csharp
// backend/KasserPro.Domain/Entities/Product.cs
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    // ✨ جديد: نوع المنتج
    public ProductType Type { get; set; } = ProductType.Physical;
    
    // ⚙️ يتم حسابه تلقائياً من Type
    public bool TrackInventory { get; set; } = true;
    
    public int? StockQuantity { get; set; }
    public decimal Price { get; set; }
    // ... باقي الحقول
}
```

### ProductType Enum
```csharp
// backend/KasserPro.Domain/Enums/ProductType.cs
public enum ProductType
{
    Physical = 1,  // منتج مادي - يتتبع المخزون
    Service = 2    // خدمة - بدون مخزون
}
```

### OrderItem Entity
```csharp
// backend/KasserPro.Domain/Entities/OrderItem.cs
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    
    // ✨ nullable دلوقتي - للمنتجات المخصصة
    public int? ProductId { get; set; }
    
    // ✨ حقول المنتجات المخصصة
    public bool IsCustomItem { get; set; } = false;
    public string? CustomName { get; set; }
    public decimal? CustomUnitPrice { get; set; }
    public decimal? CustomTaxRate { get; set; }
    
    // Snapshot من المنتج
    public string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Total { get; set; }
    // ... باقي الحقول
}
```

---

## 🔄 3. الـ Service Layer (Business Logic)

### ProductService - إنشاء منتج

```csharp
// backend/KasserPro.Application/Services/Implementations/ProductService.cs

public async Task<ApiResponse<ProductDto>> CreateAsync(CreateProductRequest request)
{
    var product = new Product
    {
        Name = request.Name,
        Type = request.Type,  // Physical أو Service
        
        // ⚙️ TrackInventory يتحسب تلقائياً
        TrackInventory = request.Type == ProductType.Physical,
        
        StockQuantity = request.Type == ProductType.Physical 
            ? request.StockQuantity 
            : null,  // الخدمات ما عندهاش مخزون
        
        Price = request.Price,
        // ... باقي الحقول
    };
    
    await _unitOfWork.Products.AddAsync(product);
    await _unitOfWork.SaveChangesAsync();
    
    return ApiResponse<ProductDto>.Ok(MapToDto(product));
}
```

**الفلو:**
```
1. Frontend يبعت: { name: "قهوة", type: 1, price: 25, stockQuantity: 100 }
2. ProductService يستقبل الـ request
3. ينشئ Product entity جديد
4. يحسب TrackInventory = (Type == Physical) → true
5. يحفظ في الداتابيز
6. يرجع ProductDto للـ Frontend
```

---

### OrderService - إضافة منتج عادي للطلب

```csharp
// backend/KasserPro.Application/Services/Implementations/OrderService.cs

public async Task<ApiResponse<OrderDto>> AddItemAsync(int orderId, AddOrderItemRequest request)
{
    // 1. جلب الطلب
    var order = await _unitOfWork.Orders.Query()
        .Include(o => o.Items)
        .FirstOrDefaultAsync(o => o.Id == orderId);
    
    // 2. جلب المنتج
    var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId);
    
    // 3. ✅ التحقق من المخزون (فقط للمنتجات المادية)
    if (product.TrackInventory)  // ← هنا الفحص
    {
        var currentStock = await _inventoryService.GetAvailableQuantityAsync(
            product.Id, _currentUser.BranchId);
        
        if (currentStock < request.Quantity)
            return ApiResponse<OrderDto>.Fail("المخزون غير كافٍ");
    }
    
    // 4. إنشاء OrderItem
    var orderItem = new OrderItem
    {
        ProductId = product.Id,  // ← موجود
        IsCustomItem = false,    // ← منتج عادي
        ProductName = product.Name,  // snapshot
        UnitPrice = product.Price,
        Quantity = request.Quantity,
        // ... حساب الضريبة والإجمالي
    };
    
    order.Items.Add(orderItem);
    await _unitOfWork.SaveChangesAsync();
    
    return ApiResponse<OrderDto>.Ok(MapToDto(order));
}
```

**الفلو:**
```
Frontend → AddItemAsync
    ↓
جلب Product من DB
    ↓
if (product.TrackInventory == true)  ← منتج مادي
    ↓
    فحص المخزون ✅
    ↓
    if (مخزون كافي)
        ↓
        إضافة للطلب ✅
    else
        ↓
        رفض ❌
else  ← خدمة
    ↓
    إضافة للطلب مباشرة ✅ (بدون فحص مخزون)
```

---

### OrderService - إضافة منتج مخصص للطلب

```csharp
// backend/KasserPro.Application/Services/Implementations/OrderService.cs

public async Task<ApiResponse<OrderDto>> AddCustomItemAsync(
    int orderId, 
    AddCustomItemRequest request)
{
    // 1. التحقق من البيانات
    if (request.Quantity <= 0)
        return ApiResponse<OrderDto>.Fail("الكمية يجب أن تكون أكبر من صفر");
    
    if (string.IsNullOrWhiteSpace(request.Name))
        return ApiResponse<OrderDto>.Fail("اسم المنتج مطلوب");
    
    if (request.UnitPrice < 0)
        return ApiResponse<OrderDto>.Fail("السعر يجب أن يكون أكبر من أو يساوي صفر");
    
    // 2. جلب الطلب
    var order = await _unitOfWork.Orders.Query()
        .Include(o => o.Items)
        .FirstOrDefaultAsync(o => o.Id == orderId);
    
    // 3. حساب معدل الضريبة
    var tenant = await _unitOfWork.Tenants.GetByIdAsync(order.TenantId);
    var taxRate = request.TaxRate ?? tenant.TaxRate;
    
    // 4. إنشاء OrderItem مخصص
    var orderItem = new OrderItem
    {
        // ✨ منتج مخصص
        ProductId = null,              // ← NULL
        IsCustomItem = true,           // ← true
        CustomName = request.Name,
        CustomUnitPrice = request.UnitPrice,
        CustomTaxRate = taxRate,
        
        // Snapshot (من البيانات المخصصة)
        ProductName = request.Name,
        UnitPrice = request.UnitPrice,
        Quantity = request.Quantity,
        TaxRate = taxRate,
        // ... حساب الإجمالي
    };
    
    // 5. حساب الإجمالي
    CalculateItemTotals(orderItem);
    
    order.Items.Add(orderItem);
    await _unitOfWork.SaveChangesAsync();
    
    return ApiResponse<OrderDto>.Ok(MapToDto(order));
}
```

**الفلو:**
```
Frontend → AddCustomItemAsync
    ↓
التحقق من البيانات (name, price, quantity)
    ↓
إنشاء OrderItem:
    - ProductId = NULL  ← ما فيش منتج في الكتالوج
    - IsCustomItem = true
    - CustomName = "تغليف هدية"
    - CustomUnitPrice = 10
    ↓
⏭️ تخطي فحص المخزون (لأنه custom)
    ↓
إضافة للطلب ✅
```

---

### OrderService - إتمام الطلب (CompleteAsync)

```csharp
public async Task<ApiResponse<OrderDto>> CompleteAsync(
    int orderId, 
    CompleteOrderRequest request)
{
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    
    try
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        // 1. التحقق من المدفوعات
        decimal totalPayment = request.Payments.Sum(p => p.Amount);
        if (totalPayment < order.Total)
        {
            // فحص الائتمان للعميل
        }
        
        // 2. ✅ فحص المخزون مرة أخرى (داخل Transaction)
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentUser.TenantId);
        if (!tenant.AllowNegativeStock)
        {
            // فقط للمنتجات التي لها ProductId وليست Custom
            foreach (var item in order.Items
                .Where(i => i.ProductId.HasValue && !i.IsCustomItem))
            {
                var product = await _unitOfWork.Products
                    .GetByIdAsync(item.ProductId.Value);
                
                // ✅ فحص TrackInventory
                if (product != null && product.TrackInventory)
                {
                    var branchStock = await _inventoryService
                        .GetAvailableQuantityAsync(
                            item.ProductId.Value, 
                            _currentUser.BranchId);
                    
                    if (branchStock < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<OrderDto>.Fail(
                            $"المخزون غير كافٍ: {item.ProductName}");
                    }
                }
            }
        }
        
        // 3. ✅ خصم المخزون (فقط للمنتجات المادية)
        var productIds = order.Items
            .Where(i => i.ProductId.HasValue && !i.IsCustomItem)
            .Select(i => i.ProductId!.Value)
            .Distinct()
            .ToList();
        
        var products = await _unitOfWork.Products.Query()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p);
        
        var stockItems = order.Items
            .Where(i => i.ProductId.HasValue 
                     && !i.IsCustomItem 
                     && products.ContainsKey(i.ProductId.Value) 
                     && products[i.ProductId.Value].TrackInventory)  // ← الفحص
            .Select(i => (i.ProductId!.Value, i.Quantity))
            .ToList();
        
        if (stockItems.Any())
        {
            await _inventoryService.BatchDecrementStockAsync(
                stockItems, 
                order.Id);
        }
        
        // 4. تحديث حالة الطلب
        order.Status = OrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        
        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        
        return ApiResponse<OrderDto>.Ok(MapToDto(order));
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return ApiResponse<OrderDto>.Fail($"خطأ: {ex.Message}");
    }
}
```

**الفلو:**
```
Frontend → CompleteAsync
    ↓
بداية Transaction 🔒
    ↓
فحص المدفوعات ✅
    ↓
فحص المخزون (داخل Transaction):
    ↓
    for each item in order.Items:
        ↓
        if (item.ProductId != null && !item.IsCustomItem)  ← منتج عادي
            ↓
            جلب Product من DB
            ↓
            if (product.TrackInventory == true)  ← منتج مادي
                ↓
                فحص المخزون ✅
                ↓
                if (مخزون كافي)
                    ↓
                    استمرار ✅
                else
                    ↓
                    Rollback ❌
            else  ← خدمة
                ↓
                تخطي ⏭️
        else  ← منتج مخصص
            ↓
            تخطي ⏭️
    ↓
خصم المخزون:
    ↓
    for each item where (ProductId != null && !IsCustomItem && TrackInventory):
        ↓
        خصم الكمية من BranchInventory ✅
    ↓
حفظ التغييرات ✅
    ↓
Commit Transaction ✅
```

---

## 🔍 4. InventoryService - إدارة المخزون

```csharp
// backend/KasserPro.Infrastructure/Services/InventoryService.cs

public async Task BatchDecrementStockAsync(
    List<(int ProductId, int Quantity)> items, 
    int orderId)
{
    foreach (var (productId, quantity) in items)
    {
        // 1. جلب المنتج
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        
        // ✅ الحماية: فحص TrackInventory
        if (product == null || !product.TrackInventory)
        {
            continue;  // تخطي الخدمات
        }
        
        // 2. جلب مخزون الفرع
        var branchInventory = await _unitOfWork.BranchInventories.Query()
            .FirstOrDefaultAsync(bi => 
                bi.ProductId == productId && 
                bi.BranchId == _currentUser.BranchId);
        
        if (branchInventory == null)
        {
            throw new Exception($"مخزون الفرع غير موجود للمنتج {productId}");
        }
        
        // 3. خصم الكمية
        branchInventory.Quantity -= quantity;
        branchInventory.LastUpdated = DateTime.UtcNow;
        
        // 4. تسجيل حركة المخزون
        var stockMovement = new StockMovement
        {
            ProductId = productId,
            BranchId = _currentUser.BranchId,
            Quantity = -quantity,  // سالب للخصم
            Type = StockMovementType.Sale,
            ReferenceType = "Order",
            ReferenceId = orderId,
            BalanceBefore = branchInventory.Quantity + quantity,
            BalanceAfter = branchInventory.Quantity,
        };
        
        await _unitOfWork.StockMovements.AddAsync(stockMovement);
    }
    
    await _unitOfWork.SaveChangesAsync();
}
```

**الفلو:**
```
CompleteAsync → BatchDecrementStockAsync
    ↓
for each (ProductId, Quantity):
    ↓
    جلب Product
    ↓
    if (product.TrackInventory == false)  ← خدمة
        ↓
        continue ⏭️ (تخطي)
    ↓
    جلب BranchInventory
    ↓
    خصم الكمية:
        Quantity = 100 - 2 = 98
    ↓
    تسجيل StockMovement:
        Type = Sale
        Quantity = -2
        BalanceBefore = 100
        BalanceAfter = 98
    ↓
حفظ ✅
```

---

## 🔄 5. RefundAsync - المرتجعات

```csharp
public async Task<ApiResponse<OrderDto>> RefundAsync(
    int orderId, 
    int userId, 
    string? reason, 
    List<RefundItemDto>? items = null)
{
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    
    try
    {
        var originalOrder = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        // إنشاء Return Order
        var returnOrder = new Order
        {
            OrderType = OrderType.Return,
            Status = OrderStatus.Completed,
            // ... باقي الحقول
        };
        
        // إضافة المنتجات المرتجعة
        foreach (var item in originalOrder.Items)
        {
            var returnItem = new OrderItem
            {
                ProductId = item.ProductId,  // ← قد يكون NULL للمنتجات المخصصة
                IsCustomItem = item.IsCustomItem,
                ProductName = item.ProductName,
                UnitPrice = -item.UnitPrice,  // سالب
                Quantity = item.Quantity,
                Total = -item.Total,  // سالب
            };
            returnOrder.Items.Add(returnItem);
            
            // ✅ إرجاع المخزون (فقط للمنتجات المادية)
            if (item.ProductId.HasValue && !item.IsCustomItem)
            {
                var product = await _unitOfWork.Products
                    .GetByIdAsync(item.ProductId.Value);
                
                if (product != null && product.TrackInventory)
                {
                    await _inventoryService.IncrementStockAsync(
                        item.ProductId.Value, 
                        item.Quantity, 
                        originalOrder.Id);
                }
            }
        }
        
        await _unitOfWork.Orders.AddAsync(returnOrder);
        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        
        return ApiResponse<OrderDto>.Ok(MapToDto(returnOrder));
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return ApiResponse<OrderDto>.Fail($"خطأ: {ex.Message}");
    }
}
```

**الفلو:**
```
Frontend → RefundAsync
    ↓
بداية Transaction 🔒
    ↓
إنشاء Return Order (بقيم سالبة)
    ↓
for each item in originalOrder.Items:
    ↓
    إنشاء returnItem (بقيم سالبة)
    ↓
    if (item.ProductId != null && !item.IsCustomItem)  ← منتج عادي
        ↓
        جلب Product
        ↓
        if (product.TrackInventory == true)  ← منتج مادي
            ↓
            إرجاع المخزون:
                IncrementStockAsync(+quantity) ✅
        else  ← خدمة
            ↓
            تخطي ⏭️
    else  ← منتج مخصص
        ↓
        تخطي ⏭️
    ↓
حفظ ✅
    ↓
Commit Transaction ✅
```

---

## 📊 6. ملخص الفلو الكامل

### سيناريو 1: بيع منتج مادي
```
Frontend: إضافة "قهوة تركي" (ProductId=1, Type=Physical)
    ↓
Backend: AddItemAsync
    ↓
جلب Product (Type=Physical, TrackInventory=true)
    ↓
✅ فحص المخزون (100 متاح)
    ↓
إنشاء OrderItem:
    - ProductId = 1
    - IsCustomItem = false
    - Quantity = 2
    ↓
Frontend: إتمام الدفع
    ↓
Backend: CompleteAsync
    ↓
✅ فحص المخزون مرة أخرى (داخل Transaction)
    ↓
✅ خصم المخزون: 100 - 2 = 98
    ↓
✅ تسجيل StockMovement
    ↓
✅ Commit
```

### سيناريو 2: بيع خدمة
```
Frontend: إضافة "خدمة توصيل" (ProductId=25, Type=Service)
    ↓
Backend: AddItemAsync
    ↓
جلب Product (Type=Service, TrackInventory=false)
    ↓
⏭️ تخطي فحص المخزون
    ↓
إنشاء OrderItem:
    - ProductId = 25
    - IsCustomItem = false
    - Quantity = 1
    ↓
Frontend: إتمام الدفع
    ↓
Backend: CompleteAsync
    ↓
⏭️ تخطي فحص المخزون (TrackInventory=false)
    ↓
⏭️ تخطي خصم المخزون
    ↓
✅ Commit
```

### سيناريو 3: بيع منتج مخصص
```
Frontend: إضافة "تغليف هدية" (custom)
    ↓
Backend: AddCustomItemAsync
    ↓
⏭️ لا يوجد Product للجلب
    ↓
إنشاء OrderItem:
    - ProductId = NULL
    - IsCustomItem = true
    - CustomName = "تغليف هدية"
    - CustomUnitPrice = 10
    ↓
Frontend: إتمام الدفع
    ↓
Backend: CompleteAsync
    ↓
⏭️ تخطي فحص المخزون (ProductId=NULL)
    ↓
⏭️ تخطي خصم المخزون (IsCustomItem=true)
    ↓
✅ Commit
```

---

## 🎯 النقاط المهمة

### 1. الحماية في كل مكان
```csharp
// في كل مكان نتعامل مع المخزون:
if (product.TrackInventory)  // ← الفحص الأساسي
{
    // عمليات المخزون
}
```

### 2. Custom Items آمنة
```csharp
// دائماً نفحص قبل الوصول للمخزون:
if (item.ProductId.HasValue && !item.IsCustomItem)
{
    // عمليات المخزون
}
```

### 3. Transaction للأمان
```csharp
// كل عمليات CompleteAsync و RefundAsync داخل Transaction
await using var transaction = await _unitOfWork.BeginTransactionAsync();
try {
    // العمليات
    await transaction.CommitAsync();
} catch {
    await transaction.RollbackAsync();
}
```

### 4. Snapshot للتاريخ
```csharp
// OrderItem يحفظ snapshot من المنتج
// حتى لو اتغير المنتج بعدين، الفاتورة تفضل زي ما هي
ProductName = product.Name,  // snapshot
UnitPrice = product.Price,   // snapshot
```

---

## ✅ الخلاصة

**الفكرة الأساسية:**
- `ProductType` يحدد نوع المنتج (Physical/Service)
- `TrackInventory` يتحسب تلقائياً من `Type`
- كل الكود يفحص `TrackInventory` قبل عمليات المخزون
- `IsCustomItem` يخلي المنتج يتخطى كل فحوصات المخزون
- `Transaction` يضمن consistency
- `Snapshot` يحفظ التاريخ

**الأمان:**
- ✅ فحص المخزون مرتين (قبل وأثناء CompleteAsync)
- ✅ Transaction يمنع race conditions
- ✅ Validation في كل خطوة
- ✅ Error handling شامل
