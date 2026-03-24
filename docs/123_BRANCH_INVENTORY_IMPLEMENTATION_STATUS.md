# حالة تنفيذ نظام المخزون متعدد الفروع (Branch-Specific Inventory)

## 📊 التقدم الإجمالي: 40%

---

## ✅ ما تم إنجازه

### 1. Domain Layer (100% مكتمل)
- ✅ **BranchInventory Entity** - كيان المخزون لكل فرع
- ✅ **BranchProductPrice Entity** - كيان الأسعار الخاصة بكل فرع
- ✅ **InventoryTransfer Entity** - كيان نقل المخزون بين الفروع
- ✅ **InventoryTransferStatus Enum** - حالات النقل (Pending, Approved, Completed, Cancelled)
- ✅ **Product Entity** - إضافة Navigation Properties للكيانات الجديدة
- ✅ **Branch Entity** - إضافة `IsWarehouse` flag و Navigation Properties

### 2. Infrastructure Layer - EF Core Configurations (100% مكتمل)
- ✅ **BranchInventoryConfiguration** - مع UNIQUE constraint على (BranchId, ProductId)
- ✅ **BranchProductPriceConfiguration** - مع indexes للأداء
- ✅ **InventoryTransferConfiguration** - مع relationships صحيحة
- ✅ **AppDbContext** - إضافة DbSets و Soft Delete Filters

### 3. Build Status
- ✅ **KasserPro.Domain** - بناء ناجح
- ✅ **KasserPro.Application** - بناء ناجح
- ✅ **KasserPro.Infrastructure** - بناء ناجح
- ⚠️ **KasserPro.API** - فشل البناء بسبب عملية Backend قيد التشغيل (Process 14912)

---

## ⏳ ما يجب إنجازه

### 4. Migration (0% - التالي)
```bash
# يجب إيقاف Backend أولاً ثم:
dotnet ef migrations add AddMultiBranchInventory --project src/KasserPro.Infrastructure --startup-project src/KasserPro.API
dotnet ef database update --project src/KasserPro.Infrastructure --startup-project src/KasserPro.API
```

**ملاحظة هامة**: الـ Migration يجب أن تتضمن:
- إنشاء الجداول الجديدة
- نقل البيانات الموجودة من `Product.StockQuantity` إلى `BranchInventory`
- **لا تحذف** `Product.StockQuantity` حالياً (للتوافق مع الكود الموجود)

### 5. Error Codes (0%)
إضافة أكواد الأخطاء الجديدة في `ErrorCodes.cs`:
```csharp
// Inventory Errors (7xxx)
public const string INVENTORY_NOT_FOUND = "INVENTORY_NOT_FOUND";
public const string INVENTORY_INVALID_QUANTITY = "INVENTORY_INVALID_QUANTITY";
public const string INVENTORY_INSUFFICIENT_STOCK = "INVENTORY_INSUFFICIENT_STOCK";
public const string INVENTORY_TRANSFER_SAME_BRANCH = "INVENTORY_TRANSFER_SAME_BRANCH";
public const string INVENTORY_TRANSFER_NOT_FOUND = "INVENTORY_TRANSFER_NOT_FOUND";
public const string INVENTORY_TRANSFER_ALREADY_APPROVED = "INVENTORY_TRANSFER_ALREADY_APPROVED";
public const string INVENTORY_TRANSFER_NOT_APPROVED = "INVENTORY_TRANSFER_NOT_APPROVED";
```

### 6. DTOs (0%)
إنشاء DTOs في `src/KasserPro.Application/DTOs/Inventory/`:
- `BranchInventoryDto`
- `InventoryTransferDto`
- `BranchProductPriceDto`
- `CreateTransferRequest`
- `AdjustInventoryRequest`
- `SetBranchPriceRequest`

### 7. Services (0%)
إنشاء Services في `src/KasserPro.Application/Services/`:
- `IInventoryService` + Implementation
- Methods:
  - `GetBranchInventoryAsync(branchId)`
  - `GetProductInventoryAcrossBranchesAsync(productId)`
  - `GetLowStockItemsAsync(branchId?)`
  - `AdjustInventoryAsync(request)`
  - `CreateTransferAsync(request)`
  - `ApproveTransferAsync(transferId)`
  - `ReceiveTransferAsync(transferId)`
  - `CancelTransferAsync(transferId, reason)`
  - `GetEffectivePriceAsync(productId, branchId)` - Branch price override logic

### 8. Controllers (0%)
إنشاء `InventoryController` في `src/KasserPro.API/Controllers/`:
- GET `/api/inventory/branch/{branchId}`
- GET `/api/inventory/product/{productId}`
- POST `/api/inventory/adjust`
- POST `/api/inventory/transfer`
- POST `/api/inventory/transfer/{id}/approve`
- POST `/api/inventory/transfer/{id}/receive`
- POST `/api/inventory/transfer/{id}/cancel`
- GET `/api/inventory/low-stock`
- GET `/api/branch-prices/{branchId}`
- POST `/api/branch-prices`
- DELETE `/api/branch-prices/{id}`

### 9. Frontend Types (0%)
إنشاء `client/src/types/branchInventory.types.ts`:
```typescript
export type InventoryTransferStatus = 'Pending' | 'Approved' | 'Completed' | 'Cancelled';

export interface BranchInventory {
  branchId: number;
  branchName: string;
  productId: number;
  productName: string;
  quantity: number;
  reorderPoint?: number;
  lowStockThreshold?: number;
  isLowStock: boolean;
  lastStockUpdate: string;
}

export interface InventoryTransfer {
  id: number;
  transferNumber: string;
  fromBranchId: number;
  fromBranchName: string;
  toBranchId: number;
  toBranchName: string;
  productId: number;
  productName: string;
  quantity: number;
  status: InventoryTransferStatus;
  reason: string;
  // ... more fields
}
```

### 10. Frontend API (0%)
إنشاء `client/src/api/branchInventoryApi.ts` مع RTK Query endpoints

### 11. Frontend UI (0%)
- صفحة إدارة المخزون (branch-aware)
- صفحة نقل المخزون (Admin only)
- مكون Branch Selector
- مكون Low Stock Alerts
- تحديث POS لاستخدام أسعار الفرع

### 12. Data Migration (0%)
تحديث `DbInitializer.cs` لإنشاء:
- BranchInventory records لكل منتج في كل فرع
- نقل `Product.StockQuantity` الحالية إلى BranchInventory

### 13. Update Existing Services (0%)
تحديث الخدمات الموجودة لاستخدام BranchInventory:
- `OrderService` - خصم من BranchInventory بدلاً من Product.StockQuantity
- `PurchaseInvoiceService` - إضافة إلى BranchInventory
- `ProductService` - استخدام GetEffectivePriceAsync

---

## 🚨 المشكلة الحالية

**Backend يعمل حالياً (Process ID: 14912)** ويمنع البناء والـ Migration.

### الحل:
1. أوقف Backend من Terminal أو Task Manager
2. أعد تشغيل الأوامر:
```bash
dotnet build src/KasserPro.API
dotnet ef migrations add AddMultiBranchInventory --project src/KasserPro.Infrastructure --startup-project src/KasserPro.API
```

---

## 📋 الخطوات التالية (بالترتيب)

1. ⚠️ **إيقاف Backend** (يدوياً)
2. إنشاء Migration
3. إضافة Error Codes
4. إنشاء DTOs
5. إنشاء Services
6. إنشاء Controllers
7. تطبيق Migration وتحديث قاعدة البيانات
8. Frontend Types
9. Frontend API
10. Frontend UI
11. Testing

---

## 🎯 الهدف النهائي

نظام مخزون متعدد الفروع كامل يدعم:
- ✅ مخزون منفصل لكل فرع
- ✅ أسعار مختلفة لكل فرع (optional override)
- ✅ نقل المخزون بين الفروع (Admin only)
- ✅ تنبيهات المخزون المنخفض لكل فرع
- ✅ تقارير شاملة للمخزون
- ✅ Transactional operations لضمان سلامة البيانات

---

**تاريخ آخر تحديث**: 9 فبراير 2026
**الحالة**: في انتظار إيقاف Backend لإكمال Migration
