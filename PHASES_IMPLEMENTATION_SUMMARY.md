# KasserPro — ملخص تنفيذ الخطة (5 Phases)

## تاريخ التحديث
2026-04-28

---

## ✅ Phase 1: توسيع الصلاحيات
**الحالة: مكتمل**

### ما تم:
- إضافة 13 permission جديد في `Permission.cs`:
  - `PosCreditSale`, `PosEditPrice`, `PosDeleteItem`, `PosCancelOrder`
  - `InventoryManage`, `InventoryTransfer`
  - `PurchaseInvoicesView`, `PurchaseInvoicesManage`
  - `UsersView`, `UsersManage`
  - `SettingsManage`
  - `DeliveryView`, `DeliveryManage`
- تحديث `PermissionService.GetAllAvailablePermissions()` بالأوصاف
- تفعيل `[HasPermission]` على الـ controllers
- Frontend: `usePermission` hook + route guards + UI buttons

### الملفات المعدلة:
- `backend/KasserPro.Domain/Enums/Permission.cs`
- `backend/KasserPro.Application/Services/PermissionService.cs`
- `backend/KasserPro.API/Middleware/HasPermissionAttribute.cs`
- `frontend/src/hooks/usePermission.ts`

---

## ⏳ Phase 2: نظام الوحدات والتجزئة
**الحالة: لم يبدأ**

### المتطلبات:
- إنشاء `UnitOfMeasure` + `ProductUnit` entities
- تعديل `Product` — إضافة `BaseUnitId`
- تغيير `Quantity` من `int` → `decimal` في `BranchInventory`, `StockMovement`, `OrderItem`, `PurchaseInvoiceItem`
- تعديل `OrderService` + `InventoryService` + `PurchaseInvoiceService` + POS UI

### الملاحظة:
هذا Phase الأكبر والأكثر تعقيداً (breaking change في كميات المخزون).

---

## ✅ Phase 3: الباتش وتاريخ الصلاحية
**الحالة: مكتمل**

### ما تم:
- إنشاء `ProductBatch` entity + `BatchStatus` enum
- تعديل `Tenant` (+ `ExpiryAlertDays`, `AllowExpiredSales`)
- تعديل `StockMovement` (+ `BatchId`)
- تعديل `OrderItem` (+ `BatchId`, `BatchNumber`, `ExpiryDate`)
- تعديل `PurchaseInvoiceItem` (+ `BatchNumber`, `ExpiryDate`, `ProductionDate`)
- تعديل `InventoryService` — FEFO logic عند الخصم
- Frontend: `BatchExpiryAlertBanner`, عرض الباتش في صفحة المخزون

### الملفات الجديدة:
- `backend/KasserPro.Domain/Entities/ProductBatch.cs`
- `frontend/src/components/inventory/BatchExpiryAlertBanner.tsx`

---

## ✅ Phase 4: نظام التوصيل والمناديب
**الحالة: مكتمل**

### ما تم:
- إنشاء `DeliveryPerson` entity + `DeliveryStatus` enum
- إضافة `DeliveryFee` في `Order`
- تعديل `OrderService` — حساب DeliveryFee في Total
- `DeliveryService` + `IDeliveryService`
- `DeliveryController`
- Frontend: `DeliveryPersonsPage` + تعديل POS لإضافة بيانات التوصيل

### الملفات الجديدة:
- `backend/KasserPro.Domain/Entities/DeliveryPerson.cs`
- `backend/KasserPro.Application/Services/DeliveryService.cs`
- `backend/KasserPro.API/Controllers/DeliveryController.cs`
- `frontend/src/pages/delivery/DeliveryPersonsPage.tsx`
- `frontend/src/types/delivery.types.ts`
- `frontend/src/api/deliveryApi.ts`

---

## ✅ Phase 5: تحسين حركة المخزون (الجرد)
**الحالة: مكتمل**

### ما تم:
- تعديل `StockMovementType` (+ `StockTaking = 7`, `Expired = 8`)
- إنشاء `StockTakingStatus` enum (`InProgress`, `Completed`, `Cancelled`)
- إنشاء `StockTaking` entity (TenantId, BranchId, StockTakingNumber, Status, StartedAt, CompletedAt, CreatedByUserId, CompletedByUserId, Notes)
- إنشاء `StockTakingItem` entity (StockTakingId, ProductId, SystemQuantity, ActualQuantity, Difference, Reason, BatchId)
- تسجيل في `AppDbContext` + `IUnitOfWork` + `UnitOfWork`
- Migration: `AddStockTaking`
- `StockTakingService` + `IStockTakingService`
- `StockTakingController`
- Frontend types + API slice + `StockTakingPage`
- Route `/stock-taking` + Navigation item "الجرد"

### الملفات الجديدة:
- `backend/KasserPro.Domain/Enums/StockMovementType.cs` (مُحدَّث)
- `backend/KasserPro.Domain/Enums/StockTakingStatus.cs` (جديد)
- `backend/KasserPro.Domain/Entities/StockTaking.cs`
- `backend/KasserPro.Domain/Entities/StockTakingItem.cs`
- `backend/KasserPro.Application/DTOs/Inventory/StockTakingDto.cs`
- `backend/KasserPro.Application/Services/Interfaces/IStockTakingService.cs`
- `backend/KasserPro.Infrastructure/Services/StockTakingService.cs`
- `backend/KasserPro.API/Controllers/StockTakingController.cs`
- `frontend/src/types/stockTaking.types.ts`
- `frontend/src/api/stockTakingApi.ts`
- `frontend/src/pages/inventory/StockTakingPage.tsx`

### الملفات المعدلة:
- `backend/KasserPro.Infrastructure/Data/AppDbContext.cs`
- `backend/KasserPro.Application/Common/Interfaces/IUnitOfWork.cs`
- `backend/KasserPro.Infrastructure/Repositories/UnitOfWork.cs`
- `backend/KasserPro.API/Program.cs`
- `frontend/src/App.tsx`
- `frontend/src/components/layout/navigation.ts`
- `frontend/src/api/baseApi.ts`

---

## 📋 ملخص التقدم

| Phase | الحالة |
|-------|--------|
| Phase 1: الصلاحيات | ✅ مكتمل |
| Phase 2: الوحدات | ⏳ لم يبدأ |
| Phase 3: الباتش والصلاحية | ✅ مكتمل |
| Phase 4: التوصيل | ✅ مكتمل |
| Phase 5: الجرد | ✅ مكتمل |

**المتبقي: Phase 2 (نظام الوحدات والتجزئة)** — يتضمن تغيير `Quantity` من `int` إلى `decimal` في المخزون والحركات والطلبات والفواتير.

---

## 🔧 Fixes Applied (Post-Review)

### Fix 1: StockMovement ↔ Batch Link (Critical)
**File:** `PurchaseInvoiceService.cs` (ConfirmAsync)
**Problem:** `ProductBatch` created but `StockMovement.Batch` never linked.
**Fix:** Added `stockMovement.Batch = batch;` after `AddAsync(batch)`.
**Migration:** Included in `FixStockTakingAndPurchaseInvoiceBatch`

### Fix 2: StockTakingItem Unique Index (Critical)
**File:** `AppDbContext.cs`
**Problem:** Unique index on `(StockTakingId, ProductId)` prevented counting same product across multiple batches.
**Fix:** Changed to `(StockTakingId, ProductId, BatchId)`.
**Migration:** Included in `FixStockTakingAndPurchaseInvoiceBatch`

### Fix 3: PurchaseInvoiceItem → Batch FK
**File:** `PurchaseInvoiceItem.cs`, `PurchaseInvoiceItemConfiguration.cs`
**Problem:** No direct FK from invoice line to ProductBatch.
**Fix:** Added `BatchId` + navigation property + FK configuration.
**Migration:** Included in `FixStockTakingAndPurchaseInvoiceBatch`

### Fix 4: BatchNumber Unique (Non-critical)
**File:** `AppDbContext.cs`
**Problem:** `BatchNumber` not unique — could duplicate across products/branches.
**Fix:** Added unique index on `(TenantId, BranchId, ProductId, BatchNumber)`.
**Migration:** `FixBatchUniqueAndDeliveryNullable`

### Fix 5: DeliveryStatus for Non-Delivery Orders (Non-critical)
**File:** `Order.cs`, `OrderService.cs`, `OrderDto.cs`
**Problem:** Every order got `DeliveryStatus = PendingAssignment` even DineIn/Pickup.
**Fix:** Made `DeliveryStatus` nullable; only set when `OrderType == Delivery`.
**Migration:** `FixBatchUniqueAndDeliveryNullable`

### Fix 6: CancelAsync Transaction (Non-critical)
**File:** `OrderService.cs`
**Problem:** `CancelAsync` updated customer credit without a transaction.
**Fix:** Wrapped in `BeginTransactionAsync` + `CommitAsync` / `RollbackAsync`.
