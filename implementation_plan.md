# KasserPro — API Contract Remediation Plan

## Evaluation of Claude Sonnet 4.6 Feedback

The feedback is **90% correct and well-prioritized**. Here's what I verified against actual code:

### ✅ Confirmed Correct
1. **PaymentsController cross-tenant leak** — REAL. Query at line 21-31 has no `TenantId` or `BranchId` filter
2. **SystemController.GetSystemInfo `[AllowAnonymous]`** — REAL. Line 70 exposes LAN IP without auth
3. **CustomersController ad-hoc responses** — REAL. All 12 endpoints use `new { Success, Data }` not `ApiResponse<T>`
4. **80+ `Fail()` without ErrorCode** — REAL. Services use single-arg `Fail(message)` which sets `ErrorCode = null`
5. **`[HasPermission]` missing on mutations** — REAL. Orders/Shifts/Expenses/CashRegister write endpoints lack it
6. **Contract document errors** (auth endpoints, SignalR events, permission names, ID types) — ALL CORRECT

### ❌ One Claim is WRONG — Bug 2 (PurchaseInvoiceService StockQuantity)

> [!IMPORTANT]
> **`PurchaseInvoiceService` does NOT reference `Product.StockQuantity` as an actual code operation.**
> 
> The grep match at line 497 is a **comment**, not code:
> ```csharp
> // Update BranchInventory (not Product.StockQuantity)
> ```
> The actual code at lines 383-410 and 498-508 **correctly uses `BranchInventory`** with proper `TenantId` + `BranchId` filters. This is NOT a data corruption bug. It's already fixed.

### ⚠️ Important Addition — Permission Enum Gaps

The feedback says to add `[HasPermission(Permission.OrdersCreate)]` etc., but warns to check the enum first. I checked — these **do not exist**:

| Needed | Exists? | Current Closest |
|--------|---------|-----------------|
| `OrdersCreate` | ❌ No | `OrdersView` (200), `OrdersRefund` (201) |
| `ExpensesManage` | ❌ No | `ExpensesView` (700), `ExpensesCreate` (701) |
| `CashRegisterManage` | ❌ No | `CashRegisterView` (1000) |
| `ShiftsView` | ❌ No | `ShiftsManage` (900) |

New enum values must be **created first** before adding `[HasPermission]` attributes.

---

## Proposed Changes

### Phase 1: 🔴 Security Fixes (Immediate)

---

#### Bug 1 — PaymentsController Cross-Tenant Leak

##### [MODIFY] [PaymentsController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/PaymentsController.cs)

1. Replace direct `IUnitOfWork` DB access with a new `IPaymentQueryService`
2. Add `[HasPermission(Permission.OrdersView)]` to the endpoint
3. Remove direct controller-level DB query

##### [NEW] IPaymentQueryService.cs + PaymentQueryService.cs

Create a service that:
- Accepts `orderId` parameter
- Filters by `TenantId` from `ICurrentUserService`
- Returns `ApiResponse<List<PaymentDto>>` (not anonymous object)

##### Register in DI (Program.cs)

---

#### Bug 2 — SystemController.GetSystemInfo Exposure

##### [MODIFY] [SystemController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/SystemController.cs)

Line 70: Change `[AllowAnonymous]` → `[Authorize(Roles = "Admin,SystemOwner")]`

Line 78-96: Replace ad-hoc `new { success, data = new { lanIp, ... } }` with:
```csharp
return Ok(ApiResponse<SystemInfoDto>.Ok(new SystemInfoDto { ... }));
```

Line 95: Replace `StatusCode(500, new { ... })` with proper `ApiResponse<T>.Fail(ErrorCodes.INTERNAL_ERROR, ...)`

##### [NEW] SystemInfoDto.cs in DTOs/System/

---

### Phase 2: 🟠 Contract Breakers — Response Shape Fixes

---

#### Fix 1 — CustomersController → ApiResponse<T>

##### [MODIFY] [CustomersController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/CustomersController.cs)

Refactor all 12 endpoints. Every response must use `ApiResponse<T>`:

| Current Pattern | Replacement |
|----------------|-------------|
| `Ok(new { Success = true, Data = result })` | `Ok(ApiResponse<CustomerDto>.Ok(result))` |
| `NotFound(new { Success = false, Message = "..." })` | `NotFound(ApiResponse<object>.Fail(ErrorCodes.CUSTOMER_NOT_FOUND, ErrorMessages.Get(...)))` |
| `BadRequest(new { Success = false, Message = "..." })` | `BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(...)))` |
| `Conflict(new { ... Message = ex.Message })` | `Conflict(ApiResponse<object>.Fail(ErrorCodes.CONFLICT, ErrorMessages.Get(...)))` |

Also fix English messages → Arabic.

Also: `GetOrCreate` returns `WasCreated` field — this needs a proper DTO (e.g., `GetOrCreateCustomerResult { Customer, WasCreated }`).

---

#### Fix 2 — OrdersController Ad-Hoc Responses

##### [MODIFY] [OrdersController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/OrdersController.cs)

| Line | Current | Fix |
|------|---------|-----|
| 224 | `BadRequest(new { Success = false, Message = "سبب الاسترجاع..." })` | `BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, ...))` |
| 255 | `BadRequest(new { Success = false, Message = "يمكن طباعة..." })` | `BadRequest(ApiResponse<object>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, ...))` |
| 337 | `Ok(new { Success = true, Message = "تم إرسال أمر..." })` | `Ok(ApiResponse<bool>.Ok(true, "تم إرسال أمر الطباعة بنجاح"))` |
| 342 | `StatusCode(500, new { ... })` | `StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ...))` |

---

#### Fix 3 — ShiftsController Unauthorized Responses

##### [MODIFY] [ShiftsController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/ShiftsController.cs)

Replace all 5 occurrences (lines 30, 42, 53, 64, 121):
```csharp
// FROM (lowercase, no ErrorCode):
return Unauthorized(new { success = false, message = "معرف المستخدم غير صالح في التوكن" });

// TO:
return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));
```

---

#### Fix 4 — TenantsController.UploadLogo

##### [MODIFY] [TenantsController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/TenantsController.cs)

| Line | Fix |
|------|-----|
| 47, 52, 56 | Replace `BadRequest(new { success = false, message })` with `BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, msg))` |
| 93 | Replace ad-hoc Ok with `Ok(ApiResponse<LogoUploadResult>.Ok(new LogoUploadResult { LogoUrl = logoUrl }, ...))` |
| 97 | Replace `StatusCode(500, new { ..., message = $"...{ex.Message}" })` with `StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(...)))` — **remove ex.Message exposure** |

##### [NEW] LogoUploadResult DTO

---

#### Fix 5 — AdminController Raw Responses

##### [MODIFY] [AdminController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/AdminController.cs)

| Line | Fix |
|------|-----|
| 63 | Wrap `backups` list: `Ok(ApiResponse<List<BackupInfo>>.Ok(backups))` |
| 102 | Replace `NotFound(new { message })` with `NotFound(ApiResponse<object>.Fail(ErrorCodes.NOT_FOUND, ...))` |
| 120, 125 | Replace `BadRequest(new { message })` with `BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, ...))` |

---

#### Fix 6 — ReportsController & ExpenseCategoriesController Ad-Hoc

##### [MODIFY] [ReportsController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/ReportsController.cs)

Lines 58, 197, 202 — same pattern as above.

##### [MODIFY] [ExpenseCategoriesController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/ExpenseCategoriesController.cs)

Lines 51, 56 — replace ad-hoc + fix English messages.

---

### Phase 3: 🟠 Permission Enforcement

---

#### Step 1 — Add Missing Permission Enum Values

##### [MODIFY] [Permission.cs](file:///f:/POS/backend/KasserPro.Domain/Enums/Permission.cs)

Add new values:
```csharp
// Orders
OrdersCreate       = 202,   // ← NEW

// Expenses  
ExpensesManage     = 702,   // ← NEW (update/delete/approve/reject/pay)

// Cash Register
CashRegisterManage = 1001,  // ← NEW (deposit/withdraw/transfer)
```

> [!WARNING]
> Adding new Permission enum values means existing cashier users will NOT have these permissions by default. This is **correct and safe** — it restricts access rather than opening it. Admins will need to grant these permissions explicitly.

---

#### Step 2 — Add `[HasPermission]` to Unprotected Endpoints

##### [MODIFY] [OrdersController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/OrdersController.cs)

| Endpoint | Add |
|----------|-----|
| `POST /api/orders` (Create) | `[HasPermission(Permission.OrdersCreate)]` |
| `POST /api/orders/{id}/items` | `[HasPermission(Permission.OrdersCreate)]` |
| `POST /api/orders/{id}/items/custom` | `[HasPermission(Permission.OrdersCreate)]` |
| `DELETE /api/orders/{id}/items/{itemId}` | `[HasPermission(Permission.OrdersCreate)]` |
| `POST /api/orders/{id}/complete` | `[HasPermission(Permission.OrdersCreate)]` |
| `POST /api/orders/{id}/cancel` | `[HasPermission(Permission.OrdersCreate)]` |

##### [MODIFY] [ShiftsController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/ShiftsController.cs)

| Endpoint | Add |
|----------|-----|
| `GET /api/shifts/current` | `[HasPermission(Permission.OrdersView)]` |
| `POST /api/shifts/open` | `[HasPermission(Permission.OrdersCreate)]` |
| `POST /api/shifts/close` | `[HasPermission(Permission.OrdersCreate)]` |
| `GET /api/shifts/history` | `[HasPermission(Permission.OrdersView)]` |
| `POST /api/shifts/{id}/handover` | `[HasPermission(Permission.ShiftsManage)]` |
| `POST /api/shifts/{id}/update-activity` | `[HasPermission(Permission.OrdersView)]` |
| `GET /api/shifts/warnings` | `[HasPermission(Permission.OrdersView)]` |

##### [MODIFY] [ExpensesController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/ExpensesController.cs)

| Endpoint | Add |
|----------|-----|
| `PUT /api/expenses/{id}` | `[HasPermission(Permission.ExpensesManage)]` |
| `DELETE /api/expenses/{id}` | `[HasPermission(Permission.ExpensesManage)]` |

##### [MODIFY] [CashRegisterController.cs](file:///f:/POS/backend/KasserPro.API/Controllers/CashRegisterController.cs)

| Endpoint | Add |
|----------|-----|
| `POST /api/cash-register/deposit` | `[HasPermission(Permission.CashRegisterManage)]` |
| `POST /api/cash-register/withdraw` | `[HasPermission(Permission.CashRegisterManage)]` |

---

### Phase 4: 🟠 Service-Layer ErrorCode Fixes (Batched)

---

#### Batch 1 — UserManagementService (~22 occurrences)

##### [MODIFY] [UserManagementService.cs](file:///f:/POS/backend/KasserPro.Application/Services/Implementations/UserManagementService.cs)

For each `Fail("message")` call:
1. Map to appropriate `ErrorCodes.*` constant (most already exist)
2. Replace `Fail("message")` → `Fail(ErrorCodes.X, ErrorMessages.Get(ErrorCodes.X))`

Key mappings:
| Current Message | ErrorCode |
|----------------|-----------|
| `"المستخدم غير موجود"` | `ErrorCodes.USER_NOT_FOUND` |
| `"غير مصرح بالوصول"` | `ErrorCodes.FORBIDDEN` |
| `"البريد الإلكتروني مستخدم"` | `ErrorCodes.CONFLICT` (+ add `USER_EMAIL_DUPLICATE`?) |
| `"لا يمكنك حذف حسابك الخاص"` | `ErrorCodes.VALIDATION_ERROR` |

---

#### Batch 2 — TenantService (~8 occurrences)

##### [MODIFY] [TenantService.cs](file:///f:/POS/backend/KasserPro.Application/Services/Implementations/TenantService.cs)

Key mappings:
| Current Message | ErrorCode |
|----------------|-----------|
| `"الشركة غير موجودة"` | `ErrorCodes.TENANT_NOT_FOUND` |
| `"نسبة الضريبة يجب أن تكون بين 0 و 100"` | `ErrorCodes.VALIDATION_ERROR` |
| `"اسم الشركة مستخدم بالفعل"` | `ErrorCodes.CONFLICT` |

---

#### Batch 3 — SupplierService (~5 occurrences)

##### [MODIFY] [SupplierService.cs](file:///f:/POS/backend/KasserPro.Application/Services/Implementations/SupplierService.cs)

Key mappings:
| Current Message | ErrorCode |
|----------------|-----------|
| `"المورد غير موجود"` | `ErrorCodes.SUPPLIER_NOT_FOUND` |
| `"اسم المورد مطلوب"` | `ErrorCodes.VALIDATION_ERROR` |

---

#### Batch 4 — PurchaseInvoiceService (~20+ occurrences)

##### [MODIFY] [PurchaseInvoiceService.cs](file:///f:/POS/backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs)

Key mappings:
| Current Message | ErrorCode |
|----------------|-----------|
| `"الفاتورة غير موجودة"` | `ErrorCodes.PURCHASE_INVOICE_NOT_FOUND` |
| `"لا يمكن تعديل الفاتورة بعد التأكيد"` | `ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE` |
| `"الكمية يجب أن تكون أكبر من صفر"` | `ErrorCodes.PURCHASE_INVOICE_INVALID_QUANTITY` |
| `"المورد غير موجود"` | `ErrorCodes.SUPPLIER_NOT_FOUND` |
| `"المبلغ أكبر من المبلغ المستحق"` | `ErrorCodes.PAYMENT_EXCEEDS_DUE` |

> [!NOTE]
> All these ErrorCodes **already exist** in ErrorCodes.cs. The fix is purely changing the `Fail()` call signature, not adding new constants.

---

### Phase 5: 🟡 Contract Document Corrections

---

##### [MODIFY] [kasserpro-api-contract.md](file:///f:/POS/backend/kasserpro-api-contract.md)

#### 1. Auth Module Fix
- **Remove**: `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/change-password`
- **Add**: `POST /api/auth/register` (Admin role), `GET /api/auth/me` (Authorize)

#### 2. Permission Names Fix
- Replace all `ProductsCreate` → `ProductsManage`
- Replace all `ProductsEdit` → `ProductsManage`
- Replace all `ProductsDelete` → `ProductsManage`
- Add note: `OrdersCreate` (202) — NEW enum value added in Phase 3

#### 3. SignalR Events Fix
- **Remove**: `ShiftWarning`, `LowStockAlert`, `MaintenanceStarted`, `MaintenanceEnded`, `NotifyDevice` (none exist in code)
- **Add**: `PrintReceipt` (Server → Client) with print command shape
- **Add**: `PrintCompleted` (Client → Server) with completion event shape

#### 4. Enum Updates
- OrderStatus: Add `Pending`, `PartiallyRefunded`
- OrderType: Add `Return`
- PaymentMethod: Add `BankTransfer`

#### 5. ID Type Fix
- Product ID type: `Guid` → `int` (code uses int everywhere)

#### 6. System/Admin Route Fix
- Backup/restore endpoints: `/api/system/backup` → `/api/admin/backup` etc.
- Or document that backup is in AdminController, not SystemController

#### 7. Health Check Response Fix
- Status value: `'ok'` → `'healthy'`
- Add actual extra fields: `version`, `database`, `uptime`

---

## Decisions (Confirmed)

> [!NOTE]
> **Q1: Permission Enum — New Values → Decision: (A) Add normally**
> Add `OrdersCreate = 202`, `ExpensesManage = 702`, `CashRegisterManage = 1001` to the enum.
> No auto-migration needed — the app is NOT in production yet. Admin will assign permissions manually.

> [!NOTE]
> **Q2: Reports Routes → Decision: (B) Update contract to match code**
> Keep code routes as-is (`/api/inventory-reports/*`, `/api/financial-reports/*`, etc.).
> Update the contract document to reflect the actual routes.

> [!NOTE]
> **Q3: System vs Admin Controller → Decision: (B) Update contract to match code**
> Keep backup/restore under `/api/admin/*` as implemented in `AdminController`.
> Update the contract document to use `/api/admin/backup`, `/api/admin/restore`, `/api/admin/backups`.

---

## Verification Plan

### Automated Tests
- After Phase 1: Run existing tests + manually test PaymentsController with two different tenant JWTs to confirm cross-tenant leak is fixed
- After Phase 2: Compile check — ensure no anonymous objects remain in modified controllers
- After Phase 3: Test that Cashier users without new permissions get 403 on mutation endpoints
- After Phase 4: Grep for `\.Fail\("[^"]+"\)` single-arg pattern — count should be 0

### Manual Verification
- After Phase 5: Frontend team reviews updated contract document for accuracy
