# KasserPro API Remediation â€” Task List

## Phase 1: ðŸ”´ Security Fixes (Immediate)

### Bug 1 â€” PaymentsController Cross-Tenant Leak
- [x] Create `IPaymentQueryService` interface in `Application/Services/Interfaces/`
- [x] Create `PaymentQueryService` implementation in `Application/Services/Implementations/`
  - [x] Accept `orderId` parameter
  - [x] Filter by `TenantId` from `ICurrentUserService`
  - [x] Return `ApiResponse<List<PaymentDto>>` (not anonymous object)
- [x] Create `PaymentDto` in `Application/DTOs/Payments/` (if not exists)
- [x] Register `IPaymentQueryService` in DI (`Program.cs`)
- [x] Refactor `PaymentsController.cs`:
  - [x] Remove direct `IUnitOfWork` dependency
  - [x] Inject `IPaymentQueryService` instead
  - [x] Add `[HasPermission(Permission.OrdersView)]`
  - [x] Return `ApiResponse<List<PaymentDto>>` instead of anonymous object

### Bug 2 â€” SystemController.GetSystemInfo Exposure
- [x] Change `[AllowAnonymous]` â†’ `[Authorize(Roles = "Admin,SystemOwner")]` (line ~70)
- [x] Create `SystemInfoDto` in `Application/DTOs/System/`
- [x] Replace ad-hoc `new { success, data = ... }` â†’ `ApiResponse<SystemInfoDto>.Ok(...)`
- [x] Replace `StatusCode(500, new { ... })` â†’ `ApiResponse<T>.Fail(ErrorCodes.INTERNAL_ERROR, ...)`

---

## Phase 2: ðŸŸ  Response Shape Fixes

### Fix 1 â€” CustomersController â†’ ApiResponse<T> (12 endpoints)
- [x] `GET /customers` (L49) â€” `Ok(new { Success, Data })` â†’ `Ok(ApiResponse<...>.Ok(result))`
- [x] `GET /customers/{id}` (L56-62) â€” NotFound ad-hoc â†’ `ApiResponse.Fail(CUSTOMER_NOT_FOUND)`
- [x] `GET /customers/search` (L69) â€” ad-hoc â†’ ApiResponse
- [x] `POST /customers` (L82-99) â€” BadRequest/Conflict ad-hoc â†’ ApiResponse + remove `ex.Message` exposure
- [x] `PUT /customers/{id}` (L107-138) â€” same pattern
- [x] `POST /customers/get-or-create` (L124-138) â€” Create proper `GetOrCreateCustomerResult` DTO with `Customer` + `WasCreated`
- [x] `POST /customers/{id}/loyalty/add` (L150-153) â€” English messages â†’ Arabic, ad-hoc â†’ ApiResponse
- [x] `POST /customers/{id}/loyalty/redeem` (L163) â€” same
- [x] `DELETE /customers/{id}` (L181) â€” English "Customer deleted" â†’ Arabic
- [x] `GET /customers/{id}/orders` (L208) â€” ad-hoc â†’ ApiResponse
- [x] `GET /customers/with-debt` (L219) â€” ad-hoc â†’ ApiResponse
- [x] `POST /customers/{id}/print-statement` (L300-305) â€” ad-hoc â†’ ApiResponse

### Fix 2 â€” OrdersController Ad-Hoc Responses (4 lines)
- [x] Line 224 â€” Refund validation: `BadRequest(new { ... })` â†’ `ApiResponse.Fail(VALIDATION_ERROR)`
- [x] Line 255 â€” Print validation: `BadRequest(new { ... })` â†’ `ApiResponse.Fail(ORDER_INVALID_STATE_TRANSITION)`
- [x] Line 337 â€” Print success: `Ok(new { ... })` â†’ `ApiResponse<bool>.Ok(true, msg)`
- [x] Line 342 â€” Print error: `StatusCode(500, new { ... })` â†’ `ApiResponse.Fail(INTERNAL_ERROR)`

### Fix 3 â€” ShiftsController Unauthorized Responses (5 occurrences)
- [x] Line 30 â€” `Unauthorized(new { success, message })` â†’ `Unauthorized(ApiResponse.Fail(UNAUTHORIZED, ...))`
- [x] Line 42 â€” same
- [x] Line 53 â€” same
- [x] Line 64 â€” same
- [x] Line 121 â€” same

### Fix 4 â€” TenantsController.UploadLogo
- [x] Lines 47, 52, 56 â€” `BadRequest(new { success, message })` â†’ `ApiResponse.Fail(VALIDATION_ERROR, ...)`
- [x] Line 93 â€” Create `LogoUploadResult` DTO, use `ApiResponse<LogoUploadResult>.Ok(...)`
- [x] Line 97 â€” Remove `ex.Message` from response, use `ApiResponse.Fail(INTERNAL_ERROR, ...)`

### Fix 5 â€” AdminController Raw Responses
- [x] Line 63 â€” Wrap backups: `Ok(ApiResponse<List<BackupInfo>>.Ok(backups))`
- [x] Line 102 â€” `NotFound(new { message })` â†’ `ApiResponse.Fail(NOT_FOUND, ...)`
- [x] Lines 120, 125 â€” `BadRequest(new { message })` â†’ `ApiResponse.Fail(VALIDATION_ERROR, ...)`

### Fix 6 â€” ReportsController & ExpenseCategoriesController
- [x] ReportsController lines 58, 197, 202 â€” ad-hoc â†’ ApiResponse
- [x] ExpenseCategoriesController lines 51, 56 â€” ad-hoc â†’ ApiResponse + English â†’ Arabic

---

## Phase 3: ðŸŸ  Permission Enforcement

### Step 1 â€” Add Missing Permission Enum Values
- [x] Add `OrdersCreate = 202` to `Permission.cs`
- [x] Add `ExpensesManage = 702` to `Permission.cs`
- [x] Add `CashRegisterManage = 1001` to `Permission.cs`

### Step 2 â€” Add `[HasPermission]` to OrdersController
- [x] `POST /orders` (Create) â†’ `[HasPermission(Permission.OrdersCreate)]`
- [x] `POST /orders/{id}/items` â†’ `[HasPermission(Permission.OrdersCreate)]`
- [x] `POST /orders/{id}/items/custom` â†’ `[HasPermission(Permission.OrdersCreate)]`
- [x] `DELETE /orders/{id}/items/{itemId}` â†’ `[HasPermission(Permission.OrdersCreate)]`
- [x] `POST /orders/{id}/complete` â†’ `[HasPermission(Permission.OrdersCreate)]`
- [x] `POST /orders/{id}/cancel` â†’ `[HasPermission(Permission.OrdersCreate)]`

### Step 3 â€” Add `[HasPermission]` to ShiftsController
- [x] `GET /shifts/current` â†’ `[HasPermission(Permission.OrdersView)]`
- [x] `POST /shifts/open` â†’ `[HasPermission(Permission.OrdersCreate)]`
- [x] `POST /shifts/close` â†’ `[HasPermission(Permission.OrdersCreate)]`
- [x] `GET /shifts/history` â†’ `[HasPermission(Permission.OrdersView)]`
- [x] `POST /shifts/{id}/handover` â†’ `[HasPermission(Permission.ShiftsManage)]`
- [x] `POST /shifts/{id}/update-activity` â†’ `[HasPermission(Permission.OrdersView)]`
- [x] `GET /shifts/warnings` â†’ `[HasPermission(Permission.OrdersView)]`

### Step 4 â€” Add `[HasPermission]` to ExpensesController
- [x] `PUT /expenses/{id}` â†’ `[HasPermission(Permission.ExpensesManage)]`
- [x] `DELETE /expenses/{id}` â†’ `[HasPermission(Permission.ExpensesManage)]`

### Step 5 â€” Add `[HasPermission]` to CashRegisterController
- [x] `POST /cash-register/deposit` â†’ `[HasPermission(Permission.CashRegisterManage)]`
- [x] `POST /cash-register/withdraw` â†’ `[HasPermission(Permission.CashRegisterManage)]`

---

## Phase 4: ðŸŸ  Service-Layer ErrorCode Fixes

### Batch 1 â€” UserManagementService (~22 occurrences)
- [x] Map each `Fail("message")` to appropriate `ErrorCodes.*`
- [x] Replace all single-arg `Fail()` â†’ two-arg `Fail(ErrorCodes.X, ErrorMessages.Get(ErrorCodes.X))`
- [x] Add any missing ErrorCodes/ErrorMessages if needed (e.g. `USER_EMAIL_DUPLICATE`)

### Batch 2 â€” TenantService (~8 occurrences)
- [x] Map and replace all `Fail("message")` calls
- [x] Key: `"Ø§Ù„Ø´Ø±ÙƒØ© ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯Ø©"` â†’ `TENANT_NOT_FOUND`, validation â†’ `VALIDATION_ERROR`

### Batch 3 â€” SupplierService (~5 occurrences)
- [x] Map and replace all `Fail("message")` calls
- [x] Key: `"Ø§Ù„Ù…ÙˆØ±Ø¯ ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯"` â†’ `SUPPLIER_NOT_FOUND`

### Batch 4 â€” PurchaseInvoiceService (~20+ occurrences)
- [x] Map and replace all `Fail("message")` calls
- [x] Key: `PURCHASE_INVOICE_NOT_FOUND`, `PURCHASE_INVOICE_NOT_EDITABLE`, `PURCHASE_INVOICE_INVALID_QUANTITY`, etc.
- [x] Note: Most ErrorCodes already exist in `ErrorCodes.cs` â€” just wire them up

---

## Phase 5: ðŸŸ¡ Contract Document Corrections

### Fix 1 â€” Auth Module
- [x] Remove: `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/change-password`
- [x] Add: `POST /api/auth/register` (Admin role), `GET /api/auth/me` (Authorize)

### Fix 2 â€” Permission Names
- [x] Replace all `ProductsCreate` â†’ `ProductsManage`
- [x] Replace all `ProductsEdit` â†’ `ProductsManage`
- [x] Replace all `ProductsDelete` â†’ `ProductsManage`
- [x] Add `OrdersCreate = 202` to documented permissions
- [x] Add `ExpensesManage = 702` to documented permissions
- [x] Add `CashRegisterManage = 1001` to documented permissions

### Fix 3 â€” SignalR Events
- [x] Remove: `ShiftWarning`, `LowStockAlert`, `MaintenanceStarted`, `MaintenanceEnded`, `NotifyDevice`
- [x] Add: `PrintReceipt` (Server â†’ Client) with actual `PrintCommandDto` shape
- [x] Add: `PrintCompleted` (Client â†’ Server) with `PrintCompletedEventDto` shape

### Fix 4 â€” Enum Updates
- [x] Add `Pending` and `PartiallyRefunded` to documented `OrderStatus`
- [x] Add `Return` to documented `OrderType`
- [x] Add `BankTransfer` to documented `PaymentMethod`

### Fix 5 â€” Product ID Type
- [x] Change Product ID type from `Guid` â†’ `int`

### Fix 6 â€” Report Routes
- [x] Update contract from `/api/reports/inventory` â†’ `/api/inventory-reports/*`
- [x] Update contract from `/api/reports/financial` â†’ `/api/financial-reports/*`
- [x] Document all 6 specialized report controllers and their actual routes

### Fix 7 â€” System/Admin Routes
- [x] Update contract from `/api/system/backup` â†’ `/api/admin/backup`
- [x] Update contract from `/api/system/restore/{filename}` â†’ `/api/admin/restore`
- [x] Update contract from `/api/system/backups` â†’ `/api/admin/backups`

---

## Verification

- [x] Compile check â€” `dotnet build` passes
- [x] Grep audit â€” `return Ok(new {` and `return BadRequest(new {` returns 0 in modified controllers
- [x] Grep audit â€” `\.Fail\("[^"]+"\)` single-arg pattern count in modified services = 0
- [x] Manual test â€” PaymentsController with different tenant JWT returns empty/403
