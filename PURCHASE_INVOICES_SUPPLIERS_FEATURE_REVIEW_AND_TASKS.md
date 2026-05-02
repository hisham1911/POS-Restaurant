# مراجعة ميزة فواتير الشراء والموردين (Purchase Invoices & Suppliers)

## الملخص التنفيذي
ميزة فواتير الشراء والموردين **متقدمة ومتكاملة** — تغطي: إدارة الموردين، دورة حياة فاتورة شراء كاملة (Draft → Confirm → Pay → Cancel)، تحديث المخزون تلقائيًا عند التأكيد مع Batch/Expiry tracking، دفعات مورد متعددة مع CashRegister integration، 3 تقارير موردين. الفجوات الرئيسية: (1) `Supplier.TotalPaid/TotalPurchases/LastPurchaseDate` **لا تُحدَّث أبدًا** رغم وجودها في الـ Entity، (2) `SupplierProduct` (many-to-many) **لا يُحدَّث** عند تأكيد الفاتورة، (3) `GetAllAsync` لا يُفلتر بـ `BranchId`، (4) حذف الدفعة لا يعكس عملية CashRegister.

---

## ما الموجود حاليًا

### Backend Entities

| Entity | الوصف |
|--------|-------|
| `Supplier` | TenantId, **BranchId** (branch-scoped), Name/NameEn, Phone, Email, Address, TaxNumber, ContactPerson, Notes, IsActive. Stats: **TotalDue**, **TotalPaid**, **TotalPurchases**, **LastPurchaseDate** (الثلاثة الأخيرة لا تُحدَّث ⚠️) |
| `SupplierProduct` | SupplierId, ProductId (M:M), IsPreferred, LastPurchasePrice, LastPurchaseDate, TotalQuantityPurchased, TotalAmountSpent. **لا يُحدَّث من PurchaseInvoiceService** ⚠️ |
| `PurchaseInvoice` | TenantId, BranchId, InvoiceNumber (unique per tenant, format: `PI-YYYY-NNNN`), SupplierId + supplier snapshot, InvoiceDate, Status, Subtotal/TaxRate/TaxAmount/Total, AmountPaid/AmountDue, Notes, CreatedBy/ConfirmedBy/CancelledBy audit fields, InventoryAdjustedOnCancellation |
| `PurchaseInvoiceItem` | PurchaseInvoiceId, ProductId + product snapshot (Name/NameEn/Sku/Barcode), Quantity, PurchasePrice, SellingPrice, Total, Notes. **Batch tracking**: BatchId, BatchNumber, ExpiryDate, ProductionDate |
| `PurchaseInvoicePayment` | PurchaseInvoiceId, Amount, PaymentDate, Method (PaymentMethod enum), ReferenceNumber, Notes, CreatedByUserId/Name |

### Backend Enums

```
PurchaseInvoiceStatus: Draft(0), Confirmed(1), Paid(2), PartiallyPaid(3), Cancelled(4), Returned(5), PartiallyReturned(6)
```

### Backend Services

| Service | الوصف |
|---------|-------|
| `SupplierService` | **CRUD**: Create/Update/Delete (soft-delete مع guard: لا يُحذف لو عنده فواتير غير ملغاة أو TotalDue > 0). Update يمنع deactivation لو عنده TotalDue. **Query**: GetAll (tenant-wide, no branch filter), GetById. لا pagination |
| `PurchaseInvoiceService` | **CRUD**: GetAll (paginated + filters), GetById, Create (Draft), Update (Draft only), Delete (Draft only). **State transitions**: ConfirmAsync (→ Confirmed, updates inventory + batches + average cost + Supplier.TotalDue), CancelAsync (→ Cancelled, optional inventory reversal + batch adjustment). **Payments**: AddPaymentAsync (transaction-safe, auto-status recalculation, CashRegister for cash), DeletePaymentAsync (reverses amounts but **doesn't reverse CashRegister** ⚠️). PrepareAsync (preview for tax calculation) |
| `SupplierReportService` | **Purchases**: مشتريات per-supplier في فترة. **Debts**: ديون الموردين مع oldest unpaid invoice. **Performance**: أداء الموردين مع reliability score |

### Backend Controllers

| Controller | Endpoints | Authorization |
|------------|-----------|---------------|
| `SuppliersController` | GET all/by-id | `HasPermission(SuppliersView)` |
| | POST create, PUT update, DELETE | `Authorize(Roles = "Admin")` + `HasPermission(SuppliersManage)` |
| `PurchaseInvoicesController` | GET all/by-id | `HasPermission(PurchaseInvoicesView)` |
| | POST create/prepare, PUT update, DELETE, POST confirm/cancel | `HasPermission(PurchaseInvoicesManage)` |
| | POST `{invoiceId}/payments`, DELETE `{invoiceId}/payments/{paymentId}` | `HasPermission(PurchaseInvoicesManage)` |
| `SupplierReportsController` | GET purchases, debts, performance | `HasPermission(ReportsView)` |

### Backend DTOs

| DTO | الوصف |
|-----|-------|
| `SupplierDto` | Id, Name/NameEn, Phone, Email, Address, TaxNumber, ContactPerson, Notes, IsActive, CreatedAt. **لا يعرض TotalDue/TotalPaid/TotalPurchases** ⚠️ |
| `CreateSupplierRequest` | Name (required), optional fields |
| `UpdateSupplierRequest` | Same + IsActive |
| `PurchaseInvoiceDto` | Full invoice + Items[] + Payments[] |
| `PurchaseInvoicePreviewDto` | Subtotal, TaxRate, TaxAmount, Total |
| `PurchaseInvoiceItemDto` | Product snapshot + pricing + batch/expiry |
| `PurchaseInvoicePaymentDto` | Amount, Date, Method (string), Reference, CreatedBy |
| `CreatePurchaseInvoiceRequest` | SupplierId, InvoiceDate, Items[], Notes |
| `UpdatePurchaseInvoiceRequest` | SupplierId, InvoiceDate, Items[] (Id? = null for new), Notes |
| `AddPaymentRequest` | Amount, PaymentDate, Method, Reference, Notes. **يستخدم DataAnnotations** ⚠️ (inconsistent مع المشروع) |
| `CancelInvoiceRequest` | Reason, AdjustInventory |
| `SupplierProductDto` | Supplier-Product link مع stats |
| `LinkSupplierProductRequest` | ProductId, IsPreferred, Notes |
| Report DTOs | SupplierPurchasesReportDto, SupplierDebtsReportDto, SupplierPerformanceReportDto |

### Frontend

| المكوّن | الوصف |
|---------|-------|
| **`SuppliersPage.tsx`** | قائمة الموردين مع بحث محلي (اسم/هاتف/بريد). Summary cards: إجمالي الموردين + النشطين. جدول بسيط (الاسم/الهاتف/البريد/جهة الاتصال/الحالة). **لا يعرض TotalDue أو معلومات مالية** |
| **`SupplierFormModal.tsx`** | مودال إنشاء/تعديل مورد. Fields: Name/NameEn/Phone/Email/Address/TaxNumber/ContactPerson/Notes + IsActive (edit only) |
| **`PurchaseInvoicesPage.tsx`** | قائمة فواتير الشراء مع ترقيم وفلاتر (مورد/حالة/تاريخ). Summary cards: إجمالي الفواتير + المبلغ الإجمالي + الفواتير المدفوعة. جدول (رقم/مورد/تاريخ/حالة/إجمالي/مدفوع/متبقي/إجراءات) |
| **`PurchaseInvoiceFormPage.tsx`** | صفحة إنشاء/تعديل فاتورة شراء. اختيار مورد + تاريخ + إضافة بنود (منتج/كمية/سعر شراء/سعر بيع/باتش/انتهاء). حساب إجمالي تلقائي عبر Backend PrepareAsync. يدعم Quick Add Product |
| **`PurchaseInvoiceDetailsPage.tsx`** | صفحة تفاصيل فاتورة كاملة: معلومات المورد + الفاتورة، جدول المنتجات مع subtotal/tax/total، جدول الدفعات، أزرار (تأكيد/إلغاء/تعديل/إضافة دفعة) |
| **`AddPaymentModal.tsx`** | مودال إضافة دفعة: المبلغ (pre-filled بالمتبقي)، التاريخ، طريقة الدفع (Cash/Card/Fawry)، reference، ملاحظات |
| **`CancelInvoiceModal.tsx`** | مودال إلغاء فاتورة: سبب الإلغاء + checkbox لتعديل المخزون (للفواتير المؤكدة) |
| **`QuickAddProductModal.tsx`** | مودال إنشاء منتج سريع من داخل فاتورة الشراء (اسم/SKU/باركود/فئة/نوع/سعر) |
| **`suppliersApi.ts`** | RTK Query: 5 endpoints (getAll, getById, create, update, delete) |
| **`purchaseInvoiceApi.ts`** | RTK Query: 10 endpoints كامل مع cache invalidation ذكي (يشمل Products + ProductBatch عند التأكيد) |
| **`supplierReportsApi.ts`** | RTK Query: 3 endpoints (purchases, debts, performance) |
| **`supplier.types.ts`** | TypeScript types للمورد (بدون financial fields) |
| **`purchaseInvoice.types.ts`** | TypeScript types كاملة للفاتورة + items + payments + filters |
| **`supplier-report.types.ts`** | TypeScript types للتقارير الثلاثة |
| **Report Pages** | `SupplierPurchasesReportPage.tsx`, `SupplierDebtsReportPage.tsx`, `SupplierPerformanceReportPage.tsx` |

---

## تحليل الـ Workflows

### 1. Workflow إنشاء مورد
```
[SuppliersPage] → Create
    ├── Validate: Name required
    └── Create → Save → Return SupplierDto
    (لا تحقق من تكرار الاسم أو الهاتف)
```

### 2. Workflow فاتورة شراء كاملة
```
[PurchaseInvoiceFormPage] → Create (Draft)
    ├── Validate: Items not empty, quantities > 0, prices >= 0
    ├── Validate: Supplier exists + active
    ├── Validate: Products exist + not Service type
    ├── Snapshot: Supplier name/phone/address + Product name/sku
    ├── Calculate: Subtotal + Tax (from Tenant.TaxRate) + Total
    ├── Generate: InvoiceNumber (PI-YYYY-NNNN, unique per tenant)
    └── Save as Draft (AmountDue = Total)

[PurchaseInvoiceDetailsPage] → Confirm
    ├── Guard: Status must be Draft
    ├── Transaction starts
    ├── Update: Status → Confirmed, ConfirmedBy audit
    ├── Update: Supplier.TotalDue += invoice.AmountDue
    ├── For each item:
    │     ├── Upsert BranchInventory (create or += Quantity)
    │     ├── Create StockMovement (Receiving, with BalanceBefore/After)
    │     ├── If batch/expiry → Create ProductBatch (Active status)
    │     ├── Update Product.LastPurchasePrice + LastPurchaseDate
    │     └── Update Product.AverageCost (weighted average)
    └── Commit
```

### 3. Workflow دفعة مورد
```
[PurchaseInvoiceDetailsPage → AddPaymentModal] → AddPayment
    ├── Guard: Status = Confirmed or PartiallyPaid
    ├── Validate: Amount > 0 && <= AmountDue
    ├── Validate: Reference for non-cash payments
    ├── Transaction starts
    ├── Create PurchaseInvoicePayment
    ├── Update Invoice: AmountPaid += Amount, AmountDue -= Amount
    ├── Recalculate Status: Paid if AmountDue <= 0, PartiallyPaid if > 0
    ├── Update Supplier.TotalDue -= Amount
    ├── If Cash → CashRegister.RecordTransaction(SupplierPayment)
    └── Commit
```

### 4. Workflow إلغاء فاتورة
```
[PurchaseInvoiceDetailsPage → CancelInvoiceModal] → Cancel
    ├── Guard: Status = Confirmed or PartiallyPaid (not Paid, not Cancelled)
    ├── Transaction starts
    ├── Update: Status → Cancelled, CancelledBy audit + reason
    ├── If wasConfirmed → Supplier.TotalDue -= invoice.AmountDue
    ├── If AdjustInventory && wasConfirmed:
    │     ├── For each item:
    │     │     ├── BranchInventory.Quantity -= item.Quantity (clamp to 0)
    │     │     ├── Find + adjust ProductBatch (→ Depleted if 0)
    │     │     └── Create StockMovement (Adjustment, negative)
    └── Commit
```

### 5. Workflow حذف دفعة
```
[Controller] → DeletePayment
    ├── Guard: Status not Cancelled/Returned/PartiallyReturned/Draft
    ├── Transaction starts
    ├── Reverse: Invoice.AmountPaid -= payment.Amount
    ├── Recalculate: Invoice.AmountDue + Status
    ├── Update: Supplier.TotalDue += payment.Amount
    ├── Delete payment record
    └── Commit
    ⚠️ لا يعكس عملية CashRegister إذا كان الدفع نقدي
```

---

## المشاكل المفصلة

### Backend

| # | المشكلة | الشدة | الملف |
|---|---------|-------|-------|
| B-1 | `Supplier.TotalPaid` **لا يُحدَّث أبدًا** — dead field في Entity | **P1** | `PurchaseInvoiceService.cs` |
| B-2 | `Supplier.TotalPurchases` **لا يُحدَّث أبدًا** — dead field في Entity | **P1** | `PurchaseInvoiceService.cs` |
| B-3 | `Supplier.LastPurchaseDate` **لا يُحدَّث أبدًا** — dead field في Entity | **P1** | `PurchaseInvoiceService.cs` |
| B-4 | `SupplierProduct` (many-to-many) **لا يُحدَّث عند تأكيد الفاتورة** — `LastPurchasePrice`, `TotalQuantityPurchased`, `TotalAmountSpent` dead | **P2** | `PurchaseInvoiceService.cs:ConfirmAsync` |
| B-5 | `DeletePaymentAsync` **لا يعكس عملية CashRegister** عند حذف دفعة نقدية — يفسد رصيد الكاش | **P1** | `PurchaseInvoiceService.cs:841-910` |
| B-6 | `GetAllAsync` يُفلتر بـ `TenantId` فقط **بدون BranchId** — يعرض فواتير كل الفروع | **P2** | `PurchaseInvoiceService.cs:34` |
| B-7 | `AddPaymentRequest` يستخدم `DataAnnotations` attributes — inconsistent مع المشروع (manual service-layer validation) | P3 | `AddPaymentRequest.cs` |
| B-8 | `ConfirmAsync` لا يتحقق أن المنتج ليس soft-deleted (`!p.IsDeleted` missing) عند تحديث المخزون | P2 | `PurchaseInvoiceService.cs:483` |
| B-9 | `SupplierService.GetAllAsync` لا يُفلتر بـ `BranchId` — يعرض موردين كل الفروع رغم أن Supplier branch-scoped | P2 | `SupplierService.cs:27` |
| B-10 | `SupplierDebtsReportService` فيه N+1 query (loop over each supplier → 2 queries per supplier) | P2 | `SupplierReportService.cs:124-156` |
| B-11 | `ConfirmAsync` يستخدم `PurchaseInvoicesManage` permission — لا يوجد permission مخصصة للتأكيد (Admin-only operation) | P2 | `PurchaseInvoicesController.cs:106` |
| B-12 | لا يوجد `PurchaseReturn` workflow — `Returned(5)` و `PartiallyReturned(6)` statuses موجودة في الـ enum لكن **لا implementation** | P3 | Architecture |
| B-13 | `Supplier` scoped to `BranchId` — لا يمكن مشاركة مورد بين فروع نفس الـ Tenant | P3 | Architecture |

### Frontend / UX

| # | المشكلة | الشدة | الملف |
|---|---------|-------|-------|
| F-1 | `SupplierDto` و `supplier.types.ts` لا يعرضان **أي معلومات مالية** (TotalDue/TotalPaid/TotalPurchases) | **P1** | `SupplierDto.cs`, `supplier.types.ts` |
| F-2 | `SuppliersPage` لا يعرض ملخص مالي (إجمالي المستحق للموردين) — فقط عدد + نشطين | **P1** | `SuppliersPage.tsx:94-107` |
| F-3 | `PaymentMethod` type في Frontend = `'Cash' \| 'Card' \| 'Fawry'` — **لا يتضمن BankTransfer** (موجود في Backend) | P2 | `purchaseInvoice.types.ts:12` |
| F-4 | `AddPaymentModal` يعرض 3 طرق دفع فقط (Cash/Card/Fawry) — **بدون BankTransfer** | P2 | `AddPaymentModal.tsx:121-124` |
| F-5 | `PurchaseInvoiceDetailsPage` لا يعرض **batch/expiry** في جدول المنتجات | P2 | `PurchaseInvoiceDetailsPage.tsx:200-224` |
| F-6 | `PurchaseInvoicesPage` statusLabels لا تتضمن `Returned` و `PartiallyReturned` | P3 | `PurchaseInvoicesPage.tsx:69-75` |
| F-7 | `PurchaseInvoiceDetailsPage` لا يوجد زر **حذف دفعة** رغم أن Backend يدعمه | P2 | `PurchaseInvoiceDetailsPage.tsx:280-324` |
| F-8 | `SupplierFormModal` لا يتحقق من تكرار الاسم أو الهاتف (Backend أيضًا لا يتحقق) | P3 | `SupplierFormModal.tsx` |
| F-9 | `PurchaseInvoiceFormPage` يستدعي `PrepareAsync` عند كل تغيير (مع `useEffect`) — ممكن يسبب excessive API calls | P3 | `PurchaseInvoiceFormPage.tsx:96-126` |

### Information Architecture

| # | المشكلة | الشدة |
|---|---------|-------|
| A-1 | `Supplier` scoped to `BranchId` لكن `GetAllAsync` و `SuppliersPage` لا يُفلتران بالفرع — يعرض موردين فروع أخرى | **P1** |
| A-2 | `PurchaseInvoice.GetAllAsync` لا يُفلتر بـ `BranchId` — المستخدم يرى فواتير فروع أخرى | **P2** |
| A-3 | `SupplierProduct` entity + DTO + configuration موجودة لكن لا يوجد endpoints لإدارتها — dead code | P3 |
| A-4 | لا يوجد Supplier Details page — لا يمكن رؤية فواتير المورد أو سجل الدفعات أو المنتجات المرتبطة | P2 |
| A-5 | No purchase return workflow — Returned/PartiallyReturned statuses unused | P3 |

---

## خطة التاسكات المقترحة

### المرحلة 1: سلامة البيانات المالية (P1)
- [ ] **B-1/B-2/B-3** تحديث `Supplier.TotalPaid` و `TotalPurchases` و `LastPurchaseDate` في `ConfirmAsync` و `AddPaymentAsync` و `CancelAsync`.
- [ ] **B-5** عكس عملية CashRegister عند حذف دفعة نقدية في `DeletePaymentAsync`.
- [ ] **F-1/F-2** إضافة `TotalDue`, `TotalPaid`, `TotalPurchases`, `LastPurchaseDate` لـ `SupplierDto` و `supplier.types.ts` وعرضهم في `SuppliersPage` (summary cards + جدول).
- [ ] **A-1** إضافة `BranchId` filter في `SupplierService.GetAllAsync` (الموردين branch-scoped).

### المرحلة 2: عزل الفروع (P2)
- [ ] **B-6/A-2** إضافة `BranchId` filter في `PurchaseInvoiceService.GetAllAsync`.
- [ ] **B-9** إضافة `BranchId` filter في `SupplierService.GetAllAsync` (أو جعل Suppliers tenant-wide).
- [ ] **B-11** إضافة permission مخصصة `PurchaseInvoicesConfirm` أو `Authorize(Roles = "Admin")` على Confirm endpoint.

### المرحلة 3: تحسين UX (P2)
- [ ] **F-3/F-4** إضافة `BankTransfer` كطريقة دفع في Frontend (`PaymentMethod` type + AddPaymentModal).
- [ ] **F-5** عرض batch/expiry info في `PurchaseInvoiceDetailsPage`.
- [ ] **F-7** إضافة زر حذف دفعة في `PurchaseInvoiceDetailsPage`.
- [ ] **B-4** تحديث `SupplierProduct` في `ConfirmAsync` (LastPurchasePrice, TotalQuantityPurchased, TotalAmountSpent).
- [ ] **A-4** إنشاء Supplier Details page (فواتير المورد + سجل الدفعات + إحصائيات).

### المرحلة 4: تحسينات إضافية (P3)
- [ ] **B-7** إزالة `DataAnnotations` من `AddPaymentRequest` وتوحيد الـ validation.
- [ ] **B-8** إضافة `!p.IsDeleted` check في `ConfirmAsync` عند تحديث المنتج.
- [ ] **B-10** إصلاح N+1 في `SupplierDebtsReportService` بتحويل loop إلى batch query.
- [ ] **F-6** إضافة `Returned`/`PartiallyReturned` labels في statusLabels maps.
- [ ] **F-8** إضافة unique validation للمورد (اسم أو هاتف per branch/tenant).
- [ ] **F-9** إضافة debounce لاستدعاء `PrepareAsync` في `PurchaseInvoiceFormPage`.
- [ ] **A-3** تقييم: إما تفعيل `SupplierProduct` management endpoints أو إزالة dead code.
- [ ] **A-5** (مستقبلي) تنفيذ Purchase Return workflow.

---

## ملخص التكامل مع الميزات الأخرى

| الميزة | نقطة التكامل | الحالة |
|--------|-------------|--------|
| **Inventory/BranchInventory** | `ConfirmAsync` → Upsert BranchInventory + StockMovement (Receiving) | ✅ يعمل |
| **ProductBatch** | `ConfirmAsync` → Create ProductBatch (Active) مع batch/expiry data | ✅ يعمل |
| **Product.AverageCost** | `ConfirmAsync` → Weighted average cost update | ✅ يعمل |
| **Product.LastPurchasePrice** | `ConfirmAsync` → Update per item | ✅ يعمل |
| **CashRegister** | `AddPaymentAsync` → RecordTransaction(SupplierPayment) for cash | ✅ يعمل (لكن Delete لا يعكس ⚠️) |
| **Supplier.TotalDue** | ConfirmAsync (+), CancelAsync (-), AddPayment (-), DeletePayment (+) | ✅ يعمل |
| **Supplier.TotalPaid** | — | ❌ لا يُحدَّث |
| **Supplier.TotalPurchases** | — | ❌ لا يُحدَّث |
| **SupplierProduct** | — | ❌ لا يُحدَّث |
| **Cancel → Inventory** | Optional inventory reversal مع StockMovement (Adjustment) + batch status | ✅ يعمل |
| **Reports** | 3 تقارير (مشتريات/ديون/أداء) | ✅ يعمل (N+1 في Debts) |
| **Permissions** | HasPermission(PurchaseInvoicesView/Manage, SuppliersView/Manage, ReportsView) | ✅ يعمل |

---

## الخلاصة

ميزة فواتير الشراء **متقدمة ومتكاملة** في الجوانب الأساسية:
- ✅ **Full lifecycle**: Draft → Confirm → Pay → Cancel مع state guards
- ✅ **Inventory integration**: تحديث المخزون + batch creation + stock movements + weighted average cost
- ✅ **Payment management**: دفعات متعددة مع CashRegister integration + status auto-calculation
- ✅ **Cancellation**: إلغاء مع optional inventory reversal + batch adjustment
- ✅ **Tax handling**: Backend-driven tax calculation مع Prepare endpoint
- ✅ **Audit trail**: CreatedBy/ConfirmedBy/CancelledBy مع timestamps + CancellationReason
- ✅ **Reports**: 3 تقارير شاملة مع performance scoring

**الفجوات الرئيسية**:
- ❌ **3 dead fields في Supplier**: `TotalPaid`, `TotalPurchases`, `LastPurchaseDate` — لا تُحدَّث من أي service
- ❌ **Delete payment لا يعكس CashRegister** — يفسد رصيد الكاش
- ❌ **No branch isolation**: GetAllAsync لفواتير الشراء والموردين لا يُفلتر بـ BranchId
- ❌ **No financial summary في SuppliersPage** — المستخدم لا يعرف كم يدين لكل مورد
- ❌ **BankTransfer missing** من Frontend payment methods
