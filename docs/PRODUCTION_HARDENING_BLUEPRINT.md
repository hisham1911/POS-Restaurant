# PRODUCTION HARDENING BLUEPRINT — KasserPro POS

**Version:** 1.0  
**Date:** 2026-02-12  
**Classification:** Engineering Internal — Pre-Commercial Release  
**Status:** Execution-Ready

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Verified Critical Risks](#2-verified-critical-risks)
3. [Engineering Principles Applied](#3-engineering-principles-applied)
4. [Detailed Remediation Plan](#4-detailed-remediation-plan)
5. [Architectural Improvements](#5-architectural-improvements)
6. [Execution Order](#6-execution-order)
7. [Risk Matrix — Before vs After](#7-risk-matrix--before-vs-after)
8. [Production Deployment Checklist](#8-production-deployment-checklist)
9. [Post-Deployment Validation Plan](#9-post-deployment-validation-plan)

---

## 1. System Overview

| Component | Technology | Path |
|-----------|-----------|------|
| API | ASP.NET Core 8.0 | `src/KasserPro.API/` |
| Application | .NET Class Library | `src/KasserPro.Application/` |
| Infrastructure | EF Core + SQLite | `src/KasserPro.Infrastructure/` |
| Domain | .NET Class Library | `src/KasserPro.Domain/` |
| Frontend | React 19 + Vite + RTK Query | `client/` |
| Desktop Bridge | WPF .NET 8 + SignalR | `src/KasserPro.BridgeApp/` |
| Tests | xUnit + Playwright | `src/KasserPro.Tests/`, `client/e2e/` |

**Architecture:** Layered (Domain → Application → Infrastructure → API)  
**Database:** SQLite (single file)  
**Auth:** JWT Bearer tokens  
**Real-time:** SignalR hub for device communication

---

## 2. Verified Critical Risks

Each item below was confirmed by reading source code. File paths and line references are from the Phase 1 verification pass.

| ID | Risk | Severity | Category |
|----|------|----------|----------|
| C-01 | JWT secret hardcoded in `appsettings.json` | CRITICAL | Security |
| C-02 | No password policy on `RegisterRequest` | CRITICAL | Security |
| C-03 | Demo credentials in production `LoginPage.tsx` | CRITICAL | Security |
| C-04 | `DeviceTestController` has no `[Authorize]` | CRITICAL | Security |
| C-05 | `DeviceHub` accepts any non-empty API key | CRITICAL | Security |
| C-06 | `CancelAsync` has no tenant filter | CRITICAL | Tenant Isolation |
| C-07 | `AddItemAsync`/`RemoveItemAsync` have no tenant filter | CRITICAL | Tenant Isolation |
| C-08 | Cash register balance race condition | CRITICAL | Financial Integrity |
| C-09 | Order-level tax recalculates over item-level tax | HIGH | Financial Integrity |
| C-10 | `RefundAsync` writes after transaction commit | HIGH | Transactional |
| H-01 | CORS `AllowAnyOrigin` | HIGH | Security |
| H-02 | No HTTPS enforcement | HIGH | Security |
| H-03 | No account lockout | HIGH | Security |
| H-04 | `X-Branch-Id` header unvalidated | HIGH | Tenant Isolation |
| H-05 | `adjust-stock` missing Admin role | HIGH | Authorization |
| H-06 | No global tenant query filter | HIGH | Tenant Isolation |
| H-07 | Audit trail missing financial entities | HIGH | Compliance |
| H-08 | Idempotency key optional on financial endpoints | HIGH | Financial Integrity |
| H-09 | Idempotency key scoped globally, not per-tenant | HIGH | Tenant Isolation |
| H-10 | Frontend retries financial mutations with new keys | HIGH | Financial Integrity |
| H-11 | Missing decimal precision on OrderItem, Payment, Customer | HIGH | Data Integrity |
| H-12 | Test project targets net9.0 vs net8.0 API | MEDIUM | Build |
| H-13 | `appsettings.example.json` key name mismatch | MEDIUM | Config |
| M-01 | PaymentModal two-step create+complete | MEDIUM | Atomicity |
| M-02 | `react-hot-toast` vs `sonner` dual libraries | MEDIUM | UX |
| M-03 | TypeScript `strict: false` | MEDIUM | Type Safety |
| M-04 | Cart selectors not memoized | MEDIUM | Performance |
| M-05 | `GenericRepository.Delete` hard-deletes | MEDIUM | Data Safety |
| M-06 | N+1 query in order creation | MEDIUM | Performance |
| M-07 | Stock check vs decrement on different tables | MEDIUM | Data Integrity |

---

## 3. Engineering Principles Applied

| Principle | Application |
|-----------|-------------|
| **Defense-in-depth** | Tenant isolation at query-filter level AND service level. Auth at controller AND hub level. |
| **Transactional integrity** | All financial state mutations wrapped in explicit DB transactions with serializable isolation where needed. |
| **Single Responsibility** | Each fix isolated to its architectural layer. No cross-layer hacks. |
| **Fail-closed** | Missing tenant context = reject request. Missing idempotency key = reject request. Missing auth = 401. |
| **Least privilege** | Role-based authorization on every write endpoint. Branch access validated against user assignment. |
| **Idempotency by design** | Stable keys generated once per operation, not per attempt. Server enforces uniqueness per tenant+user scope. |
| **Optimistic concurrency** | Concurrency tokens on financial entities to detect conflicting writes. |
| **Configuration externalization** | All secrets in environment variables or user-secrets. Never in committed files. |

---

## 4. Detailed Remediation Plan

### REM-01: JWT Secret Externalization

**Architectural goal:** Remove all secrets from source control. Enforce secret presence at startup.

**Design principle:** Configuration externalization, fail-closed.

**Files to modify:**
- `src/KasserPro.API/appsettings.json`
- `src/KasserPro.API/appsettings.example.json`
- `src/KasserPro.API/Program.cs`
- `.gitignore`

**Code pattern — Program.cs guard:**
```csharp
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "JWT Key must be configured via environment variable 'Jwt__Key' " +
        "or user-secrets. Minimum 32 characters required.");
```

**Code pattern — appsettings.json:**
```json
{
  "Jwt": {
    "Key": "",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
  }
}
```

**Code pattern — .gitignore addition:**
```
appsettings.json
appsettings.Development.json
!appsettings.example.json
```

**Code pattern — appsettings.example.json (fix key names):**
```json
{
  "Jwt": {
    "Key": "REPLACE_WITH_MINIMUM_32_CHARACTER_SECRET",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=kasserpro.db"
  }
}
```

**Failure scenarios prevented:**
- Token forgery via known secret
- Secret leak through Git history

**Regression risk:** All existing JWT tokens invalidated. Expected.

**Required tests:**
- `StartupValidation_MissingJwtKey_ThrowsException`
- `StartupValidation_ShortJwtKey_ThrowsException`
- `Login_WithNewKey_ReturnsValidToken`

**Implementation contract:**
| Item | Value |
|------|-------|
| Precondition | Backup current `appsettings.json` |
| Acceptance criteria | App refuses to start without JWT key ≥32 chars |
| Rollback plan | Restore `appsettings.json` from backup |

---

### REM-02: Password Policy Enforcement

**Architectural goal:** Reject weak passwords at DTO validation layer and service layer (defense-in-depth).

**Files to modify:**
- `src/KasserPro.Application/DTOs/Auth/RegisterRequest.cs`
- `src/KasserPro.Application/DTOs/Auth/LoginRequest.cs`
- `src/KasserPro.Application/Services/Implementations/AuthService.cs`

**Code pattern — RegisterRequest.cs:**
```csharp
public class RegisterRequest
{
    [Required(ErrorMessage = "الاسم مطلوب")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "بريد إلكتروني غير صالح")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [MinLength(8, ErrorMessage = "كلمة المرور يجب أن تكون 8 أحرف على الأقل")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "كلمة المرور يجب أن تحتوي على حرف كبير وحرف صغير ورقم")]
    public string Password { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    public string Role { get; set; } = "Cashier";
}
```

**Code pattern — LoginRequest.cs:**
```csharp
public class LoginRequest
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    public string Password { get; set; } = string.Empty;
}
```

**Code pattern — AuthService.cs defense-in-depth:**
```csharp
// Inside RegisterAsync, before hashing:
if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
    return ApiResponse<bool>.Fail("كلمة المرور يجب أن تكون 8 أحرف على الأقل");
```

**Failure scenarios prevented:** Registration with empty/trivial passwords.

**Regression risk:** None. Existing logins unaffected.

**Required tests:**
- `Register_EmptyPassword_Returns400`
- `Register_ShortPassword_Returns400`
- `Register_ValidPassword_Succeeds`

**Implementation contract:**
| Item | Value |
|------|-------|
| Precondition | None |
| Acceptance criteria | `POST /api/auth/register` with `"1"` as password → 400 |
| Rollback plan | Revert DTO changes |

---

### REM-03: Remove Demo Credentials from Production UI

**Architectural goal:** Prevent credential exposure in production builds.

**File to modify:** `client/src/pages/auth/LoginPage.tsx`

**Code pattern:**
```tsx
{/* Demo Credentials - Development Only */}
{import.meta.env.DEV && (
  <div className="mt-6 p-4 bg-gray-50 rounded-xl text-sm">
    <p className="font-medium text-gray-700 mb-2">بيانات تجريبية:</p>
    <p className="text-gray-600">
      <span className="font-medium">المدير:</span> admin@kasserpro.com / Admin@123
    </p>
    <p className="text-gray-600">
      <span className="font-medium">الكاشير:</span> ahmed@kasserpro.com / 123456
    </p>
  </div>
)}
```

**Failure scenario prevented:** Production users see default credentials.

**Regression risk:** None.

**Required tests:**
- Build production bundle, grep for `Admin@123` → zero matches.

**Implementation contract:**
| Item | Value |
|------|-------|
| Precondition | None |
| Acceptance criteria | `npm run build && grep -r "Admin@123" dist/` returns nothing |
| Rollback plan | Revert file |

---

### REM-04: Authorization on DeviceTestController

**Architectural goal:** No unauthenticated endpoint can trigger hardware operations.

**File to modify:** `src/KasserPro.API/Controllers/DeviceTestController.cs`

**Code pattern:**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]    // <-- ADD THIS
public class DeviceTestController : ControllerBase
```

**Failure scenario prevented:** Anonymous users sending unlimited print commands.

**Required tests:**
- `DeviceTest_UnauthenticatedRequest_Returns401`
- `DeviceTest_CashierRole_Returns403`
- `DeviceTest_AdminRole_Returns200`

---

### REM-05: Authorization on adjust-stock

**File to modify:** `src/KasserPro.API/Controllers/ProductsController.cs`

**Code pattern:**
```csharp
[HttpPost("{id}/adjust-stock")]
[Authorize(Roles = "Admin")]    // <-- ADD THIS
public async Task<IActionResult> AdjustStock(int id, [FromBody] AdjustStockRequest request)
```

**Required tests:**
- `AdjustStock_CashierRole_Returns403`
- `AdjustStock_AdminRole_Succeeds`

---

### REM-06: Tenant Isolation in OrderService Query Methods

**Architectural goal:** Every order query/mutation must be scoped by TenantId.

**Design principle:** Defense-in-depth — service-layer filter is a mandatory second line even after global query filters (REM-14) are in place.

**File to modify:** `src/KasserPro.Application/Services/Implementations/OrderService.cs`

**Code patterns — three methods to fix:**

**AddItemAsync (line ~302):**
```csharp
// BEFORE (vulnerable):
var order = await _unitOfWork.Orders.Query()
    .Include(o => o.Items)
    .FirstOrDefaultAsync(o => o.Id == orderId);

// AFTER (secured):
var order = await _unitOfWork.Orders.Query()
    .Include(o => o.Items)
    .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == _currentUser.TenantId);
```

**RemoveItemAsync (same pattern):**
```csharp
var order = await _unitOfWork.Orders.Query()
    .Include(o => o.Items)
    .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == _currentUser.TenantId);
```

**CancelAsync (line ~547):**
```csharp
// BEFORE (vulnerable):
var order = await _unitOfWork.Orders.GetByIdAsync(orderId);

// AFTER (secured):
var order = await _unitOfWork.Orders.Query()
    .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == _currentUser.TenantId);
```

**Failure scenarios prevented:**
- Tenant A cancels Tenant B's order
- Tenant A adds items to Tenant B's order
- Tenant A removes items from Tenant B's order

**Required tests:**
- `AddItem_CrossTenantOrder_ReturnsNotFound`
- `RemoveItem_CrossTenantOrder_ReturnsNotFound`
- `Cancel_CrossTenantOrder_ReturnsNotFound`

---

### REM-07: CORS Policy Restriction

**Architectural goal:** Restrict API access to known frontend origins only.

**Files to modify:**
- `src/KasserPro.API/appsettings.json` (add origins array)
- `src/KasserPro.API/Program.cs` (read from config)

**Code pattern — appsettings.json addition:**
```json
"AllowedOrigins": [
  "http://localhost:3000",
  "https://localhost:3000"
]
```

**Code pattern — Program.cs:**
```csharp
// BEFORE:
options.AddPolicy("AllowAll", policy =>
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader());

// AFTER:
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

options.AddPolicy("AllowAll", policy =>
    policy.WithOrigins(allowedOrigins)
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials());
```

**Failure scenario prevented:** Arbitrary websites making authenticated API calls.

**Required tests:**
- `CORS_UnknownOrigin_Rejected`
- `CORS_ConfiguredOrigin_Accepted`

---

### REM-08: HTTPS Enforcement

**File to modify:** `src/KasserPro.API/Program.cs`

**Code pattern — add after `UseStaticFiles()`:**
```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
```

---

### REM-09: Account Lockout

**Architectural goal:** Rate-limit brute-force login attempts at the data layer.

**Files to modify:**
- `src/KasserPro.Domain/Entities/User.cs`
- `src/KasserPro.Application/Services/Implementations/AuthService.cs`
- New migration

**Code pattern — User.cs additions:**
```csharp
public int FailedLoginAttempts { get; set; } = 0;
public DateTime? LockoutEndUtc { get; set; }
```

**Code pattern — AuthService.LoginAsync:**
```csharp
public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
{
    var users = await _unitOfWork.Users.FindAsync(u => u.Email == request.Email);
    var user = users.FirstOrDefault();

    if (user == null)
        return ApiResponse<LoginResponse>.Fail("بيانات الدخول غير صحيحة");

    // Check lockout
    if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc > DateTime.UtcNow)
    {
        var remaining = (user.LockoutEndUtc.Value - DateTime.UtcNow).Minutes;
        return ApiResponse<LoginResponse>.Fail(
            $"الحساب مقفل. حاول مرة أخرى بعد {remaining} دقيقة");
    }

    if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= 5)
        {
            user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
            user.FailedLoginAttempts = 0;
        }
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<LoginResponse>.Fail("بيانات الدخول غير صحيحة");
    }

    if (!user.IsActive)
        return ApiResponse<LoginResponse>.Fail("الحساب غير مفعل");

    // Reset on success
    user.FailedLoginAttempts = 0;
    user.LockoutEndUtc = null;
    await _unitOfWork.SaveChangesAsync();

    var token = GenerateToken(user);
    // ... rest unchanged
}
```

**Migration required:** Yes — `AddAccountLockoutFields`

**Required tests:**
- `Login_5FailedAttempts_LocksAccount`
- `Login_LockedAccount_RejectsValidCredentials`
- `Login_AfterLockoutExpiry_Succeeds`
- `Login_SuccessfulLogin_ResetsCounter`

---

### REM-10: `X-Branch-Id` Header Validation

**Architectural goal:** Authenticated users can only access branches they are assigned to.

**Design principle:** Least privilege.

**File to modify:** `src/KasserPro.Infrastructure/Services/CurrentUserService.cs`

**Code pattern:**
```csharp
public int BranchId
{
    get
    {
        // Get user's assigned branch from JWT claim
        var claimBranchId = 0;
        var claim = User?.FindFirst("branchId");
        if (claim != null)
            int.TryParse(claim.Value, out claimBranchId);

        // Check if header override is present
        var headerValue = _httpContextAccessor.HttpContext?
            .Request.Headers["X-Branch-Id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(headerValue) && int.TryParse(headerValue, out var headerId))
        {
            // Allow override ONLY if user's JWT branch matches OR user is Admin
            var role = Role;
            if (role == "Admin" || headerId == claimBranchId)
                return headerId;

            // Non-admin attempting cross-branch: fall back to JWT branch
            return claimBranchId > 0 ? claimBranchId : 1;
        }

        return claimBranchId > 0 ? claimBranchId : 1;
    }
}
```

**Failure scenario prevented:** Cashier at Branch 1 reading/writing Branch 2 data.

**Required tests:**
- `CurrentUser_AdminWithHeaderOverride_ReturnsHeaderBranch`
- `CurrentUser_CashierWithCrossBranchHeader_ReturnsCashierBranch`

---

### REM-11: DeviceHub Authentication

**Architectural goal:** Replace no-op API key check with validated device identity.

**Design principle:** Defense-in-depth — configurable API key with exact match.

**Files to modify:**
- `src/KasserPro.API/Hubs/DeviceHub.cs`
- `src/KasserPro.API/appsettings.json`

**Code pattern — appsettings.json:**
```json
"DeviceHub": {
  "ApiKey": ""
}
```

**Code pattern — Program.cs guard:**
```csharp
var deviceApiKey = builder.Configuration["DeviceHub:ApiKey"];
if (string.IsNullOrEmpty(deviceApiKey))
    throw new InvalidOperationException(
        "DeviceHub:ApiKey must be configured for device communication.");
```

**Code pattern — DeviceHub.OnConnectedAsync:**
```csharp
var apiKey = httpContext.Request.Headers["X-API-Key"].ToString();
var expectedKey = httpContext.RequestServices
    .GetRequiredService<IConfiguration>()["DeviceHub:ApiKey"];

if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
{
    _logger.LogWarning("Device connection rejected: Invalid API key");
    Context.Abort();
    return;
}
```

**Failure scenario prevented:** Unauthorized device connecting and receiving receipt data.

**Required tests:**
- `Hub_NoApiKey_ConnectionRejected`
- `Hub_WrongApiKey_ConnectionRejected`
- `Hub_CorrectApiKey_ConnectionAccepted`

---

### REM-12: Unify Toast Library

**Architectural goal:** Single notification system. No silent swallowing of errors.

**Files to modify:**
- `client/src/utils/errorHandler.ts` — change import
- `client/src/pages/products/ProductsPage.tsx` — change import
- `client/src/pages/branches/BranchesPage.tsx` — change import
- `client/src/components/customers/LoyaltyPointsModal.tsx` — change import
- `client/src/components/branches/BranchFormModal.tsx` — change import
- `client/package.json` — remove `react-hot-toast`

**Code pattern — every affected file:**
```typescript
// BEFORE:
import { toast } from "react-hot-toast";

// AFTER:
import { toast } from "sonner";
```

**Failure scenario prevented:** Error messages silently swallowed.

**Required tests:**
- Manual: trigger error in ProductsPage, verify toast visible.
- `grep -r "react-hot-toast" client/src/` → zero matches.

---

---

## 5. Architectural Improvements

### ARCH-01: Multi-Tenant Isolation at DbContext Level

**Current state:** `AppDbContext._currentTenantId` hardcoded to `1`. All query filters are `!e.IsDeleted` only. Tenant isolation relies entirely on service-layer `Where` clauses.

**Target state:** DbContext receives `ICurrentUserService`, sets `_currentTenantId` from JWT. Global query filters include `e.TenantId == _currentTenantId` on all tenant-scoped entities. Service-layer filters remain as defense-in-depth.

**Design:**

```
┌─────────────┐    ┌──────────────────┐    ┌──────────────┐
│ HTTP Request │───>│ CurrentUserService│───>│  AppDbContext │
│ (JWT + Hdr)  │    │ (reads claims)   │    │ (filters by  │
└─────────────┘    └──────────────────┘    │  TenantId)   │
                                            └──────────────┘
```

**Files to modify:**
- `src/KasserPro.Infrastructure/Data/AppDbContext.cs`
- `src/KasserPro.API/Program.cs` (DI registration change)

**Code pattern — AppDbContext.cs:**
```csharp
public class AppDbContext : DbContext
{
    private readonly int _currentTenantId;
    private readonly int? _currentBranchId;

    // Constructor for runtime (with user context)
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService currentUserService) : base(options)
    {
        _currentTenantId = currentUserService.IsAuthenticated
            ? currentUserService.TenantId
            : 0; // 0 = no filter (migrations, seeding)
        _currentBranchId = currentUserService.IsAuthenticated
            ? currentUserService.BranchId
            : (int?)null;
    }

    // Constructor for migrations/design-time (no user context)
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _currentTenantId = 0;
        _currentBranchId = null;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Combined soft-delete + tenant isolation filters
        // When _currentTenantId == 0 (unauthenticated context), tenant filter is bypassed
        modelBuilder.Entity<Order>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<Product>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<Category>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<Payment>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<Customer>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<Shift>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<Expense>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<CashRegisterTransaction>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<PurchaseInvoice>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<Supplier>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        modelBuilder.Entity<RefundLog>().HasQueryFilter(e =>
            !e.IsDeleted && (_currentTenantId == 0 || e.TenantId == _currentTenantId));
        // ... same pattern for all tenant-scoped entities

        // Non-tenant entities keep IsDeleted only
        modelBuilder.Entity<Tenant>().HasQueryFilter(e => !e.IsDeleted);
        // ... remaining config unchanged
    }
}
```

**Critical design decisions:**
1. `_currentTenantId == 0` bypass is required for migrations, seeders, and background services that run outside HTTP context.
2. Service-layer tenant filters REMAIN as defense-in-depth. The global filter is the primary gate.
3. `IgnoreQueryFilters()` is used ONLY for admin cross-tenant operations if ever needed.

**Required tests:**
- `Query_AuthenticatedTenantA_ReturnsOnlyTenantAData`
- `Query_AuthenticatedTenantB_ReturnsOnlyTenantBData`
- `Query_UnauthenticatedContext_ReturnsAllData` (for seeder/migration)
- `Insert_CrossTenantEntity_AuditInterceptorCorrectsTenantId`

---

### ARCH-02: Idempotency Enforcement for Financial Endpoints

**Current state:** Idempotency-Key header is optional. Cache key is global. Frontend generates new key per retry.

**Target state:**
1. Server rejects financial POST requests without `Idempotency-Key` header (400).
2. Cache key scoped by `{tenantId}:{userId}:{idempotencyKey}`.
3. Frontend generates key ONCE per user action, persists across retries.
4. Frontend disables retry for mutations entirely.

**Server-side — IdempotencyMiddleware.cs:**
```csharp
if (string.IsNullOrEmpty(idempotencyKey))
{
    context.Response.StatusCode = 400;
    context.Response.ContentType = "application/json";
    var response = ApiResponse<object>.Fail("IDEMPOTENCY_KEY_REQUIRED",
        "Idempotency-Key header is required for this endpoint");
    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    return;
}

// Scope by tenant + user to prevent cross-tenant leakage
var userId = context.User?.FindFirst("userId")?.Value ?? "0";
var tenantId = context.User?.FindFirst("tenantId")?.Value ?? "0";
var cacheKey = $"idempotency:{tenantId}:{userId}:{idempotencyKey}";
```

**Client-side — baseApi.ts mutation-safe retry:**
```typescript
const baseQueryWithReauth = retry(
  async (args, api, extraOptions) => {
    const result = await baseQuery(args, api, extraOptions);

    if (result.error) {
      const error = result.error as FetchBaseQueryError;

      // NEVER retry mutations (POST/PUT/DELETE)
      const isMutation = typeof args === 'object' && 'method' in args &&
        ['POST', 'PUT', 'DELETE'].includes((args as any).method?.toUpperCase());

      if (isMutation) {
        retry.fail(error);
        return result;
      }

      // ... existing retry logic for queries only
    }
    return result;
  },
  { maxRetries: 3 }
);
```

**Client-side — ordersApi.ts stable idempotency key:**
```typescript
createOrder: builder.mutation<ApiResponse<Order>, CreateOrderRequest>({
  query: (order) => {
    // Key generated once per mutation call, stable across RTK retries
    const idempotencyKey = crypto.randomUUID();
    return {
      url: "/orders",
      method: "POST",
      body: order,
      headers: { "Idempotency-Key": idempotencyKey },
    };
  },
  invalidatesTags: [{ type: "Orders", id: "LIST" }, "Shifts"],
}),
```

**Required tests:**
- `FinancialEndpoint_NoIdempotencyKey_Returns400`
- `FinancialEndpoint_DuplicateKey_Returns200WithCachedResponse`
- `FinancialEndpoint_SameKeyDifferentTenant_ProcessesSeparately`
- Frontend: verify `crypto.randomUUID()` called once per user click

---

### ARCH-03: Cash Register Concurrency Handling

**Current state:** `GetCurrentBalanceForBranchAsync` reads last `BalanceAfter`, then `RecordTransactionAsync` writes new row. No lock between read and write.

**Target state:** Serializable transaction wrapping the read+write. For SQLite, this means `BEGIN IMMEDIATE` which acquires a write lock before reading.

**File to modify:** `src/KasserPro.Application/Services/Implementations/CashRegisterService.cs`

**Code pattern — RecordTransactionAsync:**
```csharp
public async Task RecordTransactionAsync(
    CashRegisterTransactionType type,
    decimal amount,
    string description,
    string? referenceType = null,
    int? referenceId = null,
    int? shiftId = null)
{
    // Serializable transaction ensures read+write atomicity
    // For SQLite: BEGIN IMMEDIATE acquires write lock before read
    await using var transaction = await _unitOfWork.BeginTransactionAsync();

    try
    {
        // Read current balance INSIDE transaction (locked)
        var currentBalance = await GetCurrentBalanceForBranchAsync(
            _currentUserService.BranchId);

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

        var transactionNumber = await GenerateTransactionNumberAsync();

        var balanceAfter = type switch
        {
            CashRegisterTransactionType.Sale => currentBalance + amount,
            CashRegisterTransactionType.Deposit => currentBalance + amount,
            CashRegisterTransactionType.Opening => amount,
            CashRegisterTransactionType.Refund => currentBalance - amount,
            CashRegisterTransactionType.Withdrawal => currentBalance - amount,
            CashRegisterTransactionType.Expense => currentBalance - amount,
            CashRegisterTransactionType.SupplierPayment => currentBalance - amount,
            CashRegisterTransactionType.Adjustment => currentBalance + amount,
            _ => currentBalance
        };

        var txn = new CashRegisterTransaction
        {
            TenantId = _currentUserService.TenantId,
            BranchId = _currentUserService.BranchId,
            TransactionNumber = transactionNumber,
            Type = type,
            Amount = amount,
            BalanceBefore = currentBalance,
            BalanceAfter = balanceAfter,
            TransactionDate = DateTime.UtcNow,
            Description = description,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ShiftId = shiftId,
            UserId = _currentUserService.UserId,
            UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
        };

        await _unitOfWork.CashRegisterTransactions.AddAsync(txn);
        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error recording cash register transaction");
        throw;
    }
}
```

**Important:** When `RecordTransactionAsync` is called from within `OrderService.CompleteAsync` (which has its own transaction), the outer transaction already holds a write lock in SQLite. The inner `BeginTransactionAsync()` would fail. To handle this, `RecordTransactionAsync` must detect whether it's already inside a transaction and skip creating a new one:

```csharp
// Check if we're already in a transaction (called from OrderService.CompleteAsync)
var existingTransaction = _unitOfWork.HasActiveTransaction;

if (!existingTransaction)
{
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    // ... full pattern above
}
else
{
    // Already in caller's transaction — just do the work
    // ... same logic without transaction management
}
```

This requires adding `bool HasActiveTransaction { get; }` to `IUnitOfWork`.

**Required tests:**
- `ConcurrentSales_SameBranch_CorrectFinalBalance`
- `RecordTransaction_InsideExistingTransaction_UsesOuterTransaction`
- `RecordTransaction_NoExistingTransaction_CreatesOwn`

---

### ARCH-04: Order Create+Complete Atomicity

**Current state:** Frontend calls `POST /api/orders` then `POST /api/orders/{id}/complete` as two separate HTTP requests. Network failure between them leaves a dangling draft.

**Target state:** Single endpoint `POST /api/orders/create-and-complete` that atomically creates the order and processes payment in one transaction.

**Files to modify:**
- `src/KasserPro.Application/Services/Interfaces/IOrderService.cs` — add new method
- `src/KasserPro.Application/Services/Implementations/OrderService.cs` — implement
- `src/KasserPro.API/Controllers/OrdersController.cs` — add endpoint
- `client/src/api/ordersApi.ts` — add new mutation
- `client/src/components/pos/PaymentModal.tsx` — call new endpoint

**New DTO:**
```csharp
public class CreateAndCompleteOrderRequest
{
    public CreateOrderRequest Order { get; set; } = null!;
    public CompleteOrderRequest Payment { get; set; } = null!;
}
```

**Service method:**
```csharp
public async Task<ApiResponse<OrderDto>> CreateAndCompleteAsync(
    CreateAndCompleteOrderRequest request, int userId)
{
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    try
    {
        // 1. Create order (reuse existing logic)
        var createResult = await CreateAsyncInternal(request.Order, userId);
        if (!createResult.Success)
            return createResult;

        // 2. Complete order (reuse existing logic)
        var completeResult = await CompleteAsyncInternal(
            createResult.Data!.Id, request.Payment);
        if (!completeResult.Success)
        {
            await transaction.RollbackAsync();
            return completeResult;
        }

        await transaction.CommitAsync();
        return completeResult;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return ApiResponse<OrderDto>.Fail("حدث خطأ أثناء إنشاء وإتمام الطلب");
    }
}
```

**Controller endpoint:**
```csharp
[HttpPost("create-and-complete")]
public async Task<IActionResult> CreateAndComplete(
    [FromBody] CreateAndCompleteOrderRequest request)
{
    var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
    var result = await _orderService.CreateAndCompleteAsync(request, userId);
    // ... print receipt logic on success
}
```

**Frontend — PaymentModal.tsx change:**
```typescript
// BEFORE (two calls):
const order = await createOrder(customerId);
const completedOrder = await completeOrder(order.id, { payments: [...] });

// AFTER (single call):
const result = await createAndCompleteOrder({
  order: { items, customerId, ... },
  payment: { payments: [{ method, amount }] }
});
```

**Required tests:**
- `CreateAndComplete_NetworkFailure_NoDanglingOrder`
- `CreateAndComplete_PaymentValidationFails_NoOrderCreated`
- `CreateAndComplete_Success_OrderAndPaymentPersisted`

---

### ARCH-05: Stock Validation Consistency

**Current state:** `CreateAsync` checks `product.StockQuantity` (Product table). `BatchDecrementStockAsync` decrements `inventory.Quantity` (BranchInventory table). These are two independent values.

**Target state:** Both validation and decrement operate on `BranchInventory.Quantity`. Negative stock guard added to decrement.

**File to modify:** `src/KasserPro.Application/Services/Implementations/OrderService.cs`

**Code pattern — CreateAsync stock validation:**
```csharp
// BEFORE:
if (product.TrackInventory)
{
    var currentStock = product.StockQuantity ?? 0;
    // ...
}

// AFTER:
if (product.TrackInventory)
{
    var currentStock = await _inventoryService.GetAvailableQuantityAsync(
        product.Id, _currentUser.BranchId);
    if (currentStock < item.Quantity && !tenant.AllowNegativeStock)
    {
        return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
            $"المخزون غير كافٍ: {product.Name}. " +
            $"المتاح: {currentStock}، المطلوب: {item.Quantity}");
    }
}
```

**File to modify:** `src/KasserPro.Infrastructure/Services/InventoryService.cs`

**Code pattern — BatchDecrementStockAsync negative guard:**
```csharp
if (inventory != null)
{
    var balanceBefore = inventory.Quantity;

    // Guard against negative stock
    if (inventory.Quantity < quantity)
    {
        _logger.LogWarning(
            "Stock would go negative for Product {ProductId} Branch {BranchId}: " +
            "current={Current}, decrement={Decrement}",
            productId, branchId, inventory.Quantity, quantity);
        // Proceed based on tenant configuration (AllowNegativeStock)
        // If not allowed, this should have been caught at validation
    }

    inventory.Quantity -= quantity;
    inventory.LastUpdatedAt = DateTime.UtcNow;
    // ... stock movement recording unchanged
}
```

**Required tests:**
- `CreateOrder_InsufficientBranchStock_ReturnsError`
- `BatchDecrement_NegativeResult_LogsWarning`
- `CreateOrder_StockAvailableInProductButNotBranch_ReturnsError`

---

### ARCH-06: Audit Trail Expansion

**File to modify:** `src/KasserPro.Infrastructure/Data/AuditSaveChangesInterceptor.cs`

**Code pattern:**
```csharp
private static readonly HashSet<string> AuditedEntities = new()
{
    nameof(Order),
    nameof(Product),
    nameof(Category),
    nameof(User),
    nameof(Branch),
    nameof(Shift),
    nameof(Payment),
    // Financial entities — previously missing
    nameof(Customer),
    nameof(Expense),
    nameof(CashRegisterTransaction),
    nameof(PurchaseInvoice),
    nameof(PurchaseInvoicePayment),
    nameof(Supplier),
    nameof(StockMovement),
    nameof(RefundLog),
    nameof(ExpenseCategory)
};

private static readonly HashSet<string> TenantScopedEntities = new()
{
    nameof(Order),
    nameof(Product),
    nameof(Category),
    nameof(User),
    nameof(Branch),
    nameof(Shift),
    nameof(Payment),
    // Previously missing
    nameof(Customer),
    nameof(Expense),
    nameof(CashRegisterTransaction),
    nameof(PurchaseInvoice),
    nameof(PurchaseInvoiceItem),
    nameof(PurchaseInvoicePayment),
    nameof(Supplier),
    nameof(StockMovement),
    nameof(RefundLog),
    nameof(ExpenseCategory),
    nameof(BranchInventory),
    nameof(InventoryTransfer)
};
```

**Required tests:**
- `SaveExpense_CreatesAuditLog`
- `SaveCashRegisterTransaction_CreatesAuditLog`
- `SavePurchaseInvoice_CreatesAuditLog`

---

### ARCH-07: Tax Calculation Fix

**Current state:** `CalculateOrderTotals` recalculates tax from `order.TaxRate` (tenant default), ignoring per-item tax rates.

**Target state:** `order.TaxAmount` = sum of all `item.TaxAmount` values. Order-level discount is distributed proportionally across items.

**File to modify:** `src/KasserPro.Application/Services/Implementations/OrderService.cs`

**Code pattern — CalculateOrderTotals:**
```csharp
private static void CalculateOrderTotals(Order order)
{
    // Subtotal = sum of item subtotals
    order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);

    // Order-level discount
    if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
        order.DiscountAmount = Math.Round(
            order.Subtotal * (order.DiscountValue.Value / 100m), 2);
    else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
        order.DiscountAmount = Math.Round(order.DiscountValue.Value, 2);
    else
        order.DiscountAmount = 0;

    if (order.DiscountAmount > order.Subtotal)
        order.DiscountAmount = order.Subtotal;

    // Tax = sum of per-item tax amounts (respects per-product tax rates)
    // If order-level discount exists, distribute proportionally
    if (order.DiscountAmount > 0 && order.Subtotal > 0)
    {
        var discountRatio = order.DiscountAmount / order.Subtotal;
        order.TaxAmount = Math.Round(order.Items.Sum(i =>
        {
            var itemDiscountedSubtotal = i.Subtotal * (1 - discountRatio);
            return itemDiscountedSubtotal * (i.TaxRate / 100m);
        }), 2);
    }
    else
    {
        order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);
    }

    var afterDiscount = order.Subtotal - order.DiscountAmount;
    order.ServiceChargeAmount = Math.Round(
        afterDiscount * (order.ServiceChargePercent / 100m), 2);
    order.Total = Math.Round(
        afterDiscount + order.TaxAmount + order.ServiceChargeAmount, 2);
    order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
}
```

**Required tests:**
- `Order_DifferentItemTaxRates_OrderTaxEqualsItemTaxSum`
- `Order_WithOrderLevelDiscount_TaxProportionallyReduced`
- `Order_UniformTaxRate_MatchesPreviousBehavior`

---

### ARCH-08: RefundAsync Transaction Boundary Fix

**File to modify:** `src/KasserPro.Application/Services/Implementations/OrderService.cs`

**Code pattern — move notes update BEFORE commit:**
```csharp
// ... existing refund logic ...

// Update original order notes INSIDE transaction
originalOrder.Notes = string.IsNullOrWhiteSpace(originalOrder.Notes)
    ? $"تم إنشاء مرتجع: #{returnOrder.OrderNumber}"
    : originalOrder.Notes + $" | تم إنشاء مرتجع: #{returnOrder.OrderNumber}";

await _unitOfWork.SaveChangesAsync();

// Commit AFTER all modifications
await transaction.CommitAsync();

// Remove the post-commit SaveChangesAsync call entirely
```

---

### ARCH-09: Missing Decimal Precision Configurations

**New files to create:**
- `src/KasserPro.Infrastructure/Data/Configurations/OrderItemConfiguration.cs`
- `src/KasserPro.Infrastructure/Data/Configurations/PaymentConfiguration.cs`
- `src/KasserPro.Infrastructure/Data/Configurations/CustomerConfiguration.cs`
- `src/KasserPro.Infrastructure/Data/Configurations/RefundLogConfiguration.cs`

**Code pattern — OrderItemConfiguration.cs:**
```csharp
public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.UnitCost).HasPrecision(18, 2);
        builder.Property(i => i.OriginalPrice).HasPrecision(18, 2);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
        builder.Property(i => i.DiscountValue).HasPrecision(18, 2);
        builder.Property(i => i.TaxRate).HasPrecision(5, 2);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 2);
        builder.Property(i => i.Subtotal).HasPrecision(18, 2);
        builder.Property(i => i.Total).HasPrecision(18, 2);
    }
}
```

**Code pattern — PaymentConfiguration.cs:**
```csharp
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
    }
}
```

**Code pattern — CustomerConfiguration.cs:**
```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.TotalSpent).HasPrecision(18, 2);
        builder.Property(c => c.TotalDue).HasPrecision(18, 2);
        builder.Property(c => c.CreditLimit).HasPrecision(18, 2);
    }
}
```

**Code pattern — RefundLogConfiguration.cs:**
```csharp
public class RefundLogConfiguration : IEntityTypeConfiguration<RefundLog>
{
    public void Configure(EntityTypeBuilder<RefundLog> builder)
    {
        builder.Property(r => r.RefundAmount).HasPrecision(18, 2);
    }
}
```

**Migration required:** `AddMissingDecimalPrecision`

---

### ARCH-10: GenericRepository Soft Delete

**File to modify:** `src/KasserPro.Infrastructure/Repositories/GenericRepository.cs`

**Code pattern:**
```csharp
public void Delete(T entity)
{
    if (entity is BaseEntity baseEntity)
    {
        baseEntity.IsDeleted = true;
        baseEntity.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
    }
    else
    {
        _dbSet.Remove(entity);
    }
}
```

---

### ARCH-11: N+1 Query Fix in Order Creation

**File to modify:** `src/KasserPro.Application/Services/Implementations/OrderService.cs`

**Code pattern — batch load products in CreateAsync:**
```csharp
// BEFORE (N+1):
foreach (var item in request.Items)
{
    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
    // ...
}

// AFTER (single query):
var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
var products = await _unitOfWork.Products.Query()
    .Where(p => productIds.Contains(p.Id))
    .ToDictionaryAsync(p => p.Id);

foreach (var item in request.Items)
{
    if (!products.TryGetValue(item.ProductId, out var product))
        return ApiResponse<OrderDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND,
            $"المنتج غير موجود: {item.ProductId}");

    if (!product.IsActive)
        return ApiResponse<OrderDto>.Fail(ErrorCodes.PRODUCT_INACTIVE,
            $"المنتج غير متاح: {product.Name}");
    // ... rest unchanged
}
```

---

### ARCH-12: Test Project Framework Alignment

**File to modify:** `src/KasserPro.Tests/KasserPro.Tests.csproj`

```xml
<!-- BEFORE -->
<TargetFramework>net9.0</TargetFramework>
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />

<!-- AFTER -->
<TargetFramework>net8.0</TargetFramework>
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
```

---

## 6. Execution Order

Strict dependency-aware ordering. Each step must be completed and validated before the next.

### Wave 0: Configuration & Build (no code logic changes)

| Order | Task | Fix Ref | Est. | Migration |
|-------|------|---------|------|-----------|
| 0.1 | JWT secret externalization | REM-01 | 1h | No |
| 0.2 | Fix `appsettings.example.json` key names | REM-01 | 0.5h | No |
| 0.3 | Add `.gitignore` entries | REM-01 | 0.5h | No |
| 0.4 | Test project TFM alignment | ARCH-12 | 0.5h | No |

**Validation gate:** Solution builds. App starts with env-var JWT key. Tests compile.

### Wave 1: Authorization & Access Control

| Order | Task | Fix Ref | Est. | Migration |
|-------|------|---------|------|-----------|
| 1.1 | `[Authorize]` on `DeviceTestController` | REM-04 | 0.5h | No |
| 1.2 | `[Authorize(Roles="Admin")]` on `adjust-stock` | REM-05 | 0.5h | No |
| 1.3 | Password policy on DTOs + AuthService | REM-02 | 1h | No |
| 1.4 | Remove demo credentials from LoginPage | REM-03 | 0.5h | No |
| 1.5 | CORS policy restriction | REM-07 | 1h | No |
| 1.6 | HTTPS enforcement | REM-08 | 0.5h | No |

**Validation gate:** All endpoints require auth. CORS blocks unknown origins. Password `"1"` rejected.

### Wave 2: Tenant Isolation

| Order | Task | Fix Ref | Est. | Migration |
|-------|------|---------|------|-----------|
| 2.1 | Fix tenant filter in `CancelAsync` | REM-06 | 0.5h | No |
| 2.2 | Fix tenant filter in `AddItemAsync` | REM-06 | 0.5h | No |
| 2.3 | Fix tenant filter in `RemoveItemAsync` | REM-06 | 0.5h | No |
| 2.4 | Validate `X-Branch-Id` header | REM-10 | 1.5h | No |
| 2.5 | Global tenant query filters in `AppDbContext` | ARCH-01 | 3h | No |

**Validation gate:** Cross-tenant queries return empty. Cross-branch header rejected for non-admin.

### Wave 3: Financial Integrity

| Order | Task | Fix Ref | Est. | Migration |
|-------|------|---------|------|-----------|
| 3.1 | Cash register serializable transactions | ARCH-03 | 2h | No |
| 3.2 | Idempotency key enforcement + scoping | ARCH-02 server | 1.5h | No |
| 3.3 | Frontend: disable mutation retry | ARCH-02 client | 1h | No |
| 3.4 | Frontend: stable idempotency keys | ARCH-02 client | 1h | No |
| 3.5 | Tax calculation fix | ARCH-07 | 1.5h | No |
| 3.6 | RefundAsync transaction boundary fix | ARCH-08 | 0.5h | No |
| 3.7 | Stock validation consistency | ARCH-05 | 1.5h | No |

**Validation gate:** 10 concurrent sales produce correct balance. Duplicate POST returns cached response. Tax totals match item sums.

### Wave 4: Schema & Data Integrity

| Order | Task | Fix Ref | Est. | Migration |
|-------|------|---------|------|-----------|
| 4.1 | Account lockout fields + logic | REM-09 | 3h | Yes |
| 4.2 | Decimal precision configurations | ARCH-09 | 2h | Yes |
| 4.3 | GenericRepository soft delete | ARCH-10 | 1h | No |
| 4.4 | N+1 query fix | ARCH-11 | 1h | No |

**Validation gate:** Migration applies cleanly. 5 failed logins lock account. Financial fields have (18,2) precision.

### Wave 5: Device Security & Audit

| Order | Task | Fix Ref | Est. | Migration |
|-------|------|---------|------|-----------|
| 5.1 | DeviceHub API key validation | REM-11 | 2h | No |
| 5.2 | Audit trail expansion | ARCH-06 | 1h | No |
| 5.3 | Unify toast library | REM-12 | 1h | No |

**Validation gate:** Hub rejects wrong key. All financial entity changes produce audit logs. All error toasts visible.

### Wave 6: Atomicity & Performance

| Order | Task | Fix Ref | Est. | Migration |
|-------|------|---------|------|-----------|
| 6.1 | Atomic create-and-complete endpoint | ARCH-04 | 3h | No |
| 6.2 | Frontend: use atomic endpoint | ARCH-04 | 1h | No |

**Validation gate:** Single network call creates and completes order. Network failure leaves no dangling draft.

### Total Estimated Effort

| Wave | Hours | Migration |
|------|-------|-----------|
| Wave 0 | 2.5h | No |
| Wave 1 | 4h | No |
| Wave 2 | 6h | No |
| Wave 3 | 9h | No |
| Wave 4 | 7h | Yes (2 migrations) |
| Wave 5 | 4h | No |
| Wave 6 | 4h | No |
| **Total** | **36.5h** | **2 migrations** |

---

## 7. Risk Matrix — Before vs After

| Risk | Before | After Wave 2 | After Wave 6 |
|------|--------|-------------|-------------|
| Token forgery | CRITICAL | Eliminated | Eliminated |
| Cross-tenant data leak | CRITICAL | Eliminated | Eliminated |
| Cash register corruption | CRITICAL | CRITICAL | Eliminated |
| Double-charge via retry | HIGH | HIGH | Eliminated |
| Brute-force login | HIGH | HIGH | Eliminated |
| Unauthenticated device control | CRITICAL | Eliminated | Eliminated |
| Negative stock | MEDIUM | MEDIUM | Eliminated |
| Dangling draft orders | MEDIUM | MEDIUM | Eliminated |
| Tax calculation error | HIGH | HIGH | Eliminated |
| Silent error swallowing | MEDIUM | MEDIUM | Eliminated |
| Audit trail gaps | HIGH | HIGH | Eliminated |

---

## 8. Production Deployment Checklist

### Security Hardening

- [ ] JWT secret moved to environment variable, `appsettings.json` contains empty placeholder
- [ ] `appsettings.json` added to `.gitignore`
- [ ] JWT key rotated (minimum 32 chars, cryptographically random)
- [ ] All controllers have appropriate `[Authorize]` attributes
- [ ] `DeviceTestController` requires Admin role
- [ ] `adjust-stock` requires Admin role
- [ ] CORS restricted to known frontend origins
- [ ] HTTPS enforcement enabled for non-development
- [ ] Password policy enforced (min 8 chars, mixed case + digit)
- [ ] Account lockout after 5 failed attempts (15 min)
- [ ] Demo credentials removed from production UI
- [ ] DeviceHub validates API key against configured value
- [ ] `X-Branch-Id` header validated against user's assigned branch

### Financial Integrity Hardening

- [ ] Cash register transactions use serializable isolation
- [ ] Idempotency-Key header required for all financial POST endpoints
- [ ] Idempotency cache scoped by `{tenantId}:{userId}:{key}`
- [ ] Frontend does not retry financial mutations
- [ ] Frontend generates stable idempotency keys (once per action)
- [ ] Order tax = sum of per-item tax amounts
- [ ] `RefundAsync` notes update inside transaction boundary
- [ ] Decimal precision (18,2) configured for all money fields
- [ ] Stock validation reads from `BranchInventory` (same table as decrement)
- [ ] Negative stock guard in `BatchDecrementStockAsync`

### Multi-Tenant Safety

- [ ] Global tenant query filters on all tenant-scoped entities in `AppDbContext`
- [ ] `_currentTenantId` populated from `ICurrentUserService`, not hardcoded
- [ ] `CancelAsync` filters by TenantId
- [ ] `AddItemAsync` filters by TenantId
- [ ] `RemoveItemAsync` filters by TenantId
- [ ] Service-layer tenant filters maintained as defense-in-depth
- [ ] Audit interceptor scopes TenantId enforcement to all tenant entities

### Concurrency Safety

- [ ] Cash register balance read+write in single serializable transaction
- [ ] `RecordTransactionAsync` detects existing transaction context
- [ ] Shift entity retains `RowVersion` concurrency token
- [ ] `IUnitOfWork.HasActiveTransaction` property available for nested transaction detection

### Infrastructure Readiness

- [ ] Test project targets `net8.0` (matches API)
- [ ] `appsettings.example.json` key names match `appsettings.json`
- [ ] Single toast library (`sonner`) across entire frontend
- [ ] N+1 query in order creation replaced with batch load
- [ ] `GenericRepository.Delete` performs soft delete for `BaseEntity` descendants

### Deployment Safety

- [ ] Database backed up before migration
- [ ] Migrations tested on copy of production database
- [ ] `AddAccountLockoutFields` migration applied
- [ ] `AddMissingDecimalPrecision` migration applied
- [ ] Frontend production build verified (no demo credentials)
- [ ] SignalR hub endpoint tested with new API key
- [ ] Desktop bridge app updated with correct API key

### Monitoring & Logging

- [ ] Audit trail covers all financial entities
- [ ] Failed login attempts logged with IP address
- [ ] Cross-tenant access attempts logged as warnings
- [ ] Cash register transaction recording logged
- [ ] Hub connection rejections logged

### Backup & Recovery

- [ ] SQLite database file backup scheduled (pre-deployment)
- [ ] Connection string points to known, backed-up location
- [ ] Rollback plan documented for each migration
- [ ] JWT key rotation plan documented

---

## 9. Post-Deployment Validation Plan

### Smoke Tests (run immediately after deployment)

| # | Test | Expected Result | Method |
|---|------|-----------------|--------|
| 1 | App starts without hardcoded JWT key | App starts with env-var key | Manual |
| 2 | `POST /api/auth/login` with correct credentials | 200 + JWT token | curl |
| 3 | `POST /api/auth/login` with wrong password 5x | Account locked | curl |
| 4 | `GET /api/devicetest/status` without token | 401 | curl |
| 5 | `POST /api/orders` without `Idempotency-Key` | 400 | curl |
| 6 | `POST /api/orders` with `Idempotency-Key` | 201 | curl |
| 7 | Duplicate `POST /api/orders` same key | 200 cached | curl |
| 8 | Cross-origin request from `http://evil.com` | CORS rejected | curl |
| 9 | Register with password `"1"` | 400 validation error | curl |
| 10 | Login page in production build | No demo credentials | Browser |

### Financial Integrity Tests

| # | Test | Expected Result | Method |
|---|------|-----------------|--------|
| 11 | Complete order with cash payment | Cash register balance updates correctly | API + DB check |
| 12 | Complete 2 orders simultaneously via 2 terminals | Balance = sum of both amounts | Concurrent curl |
| 13 | Refund order | Stock restored, cash register debited | API + DB check |
| 14 | Order with 14% product + 0% product | `order.TaxAmount` = `item1.TaxAmount + item2.TaxAmount` | DB check |
| 15 | Create-and-complete endpoint | Single HTTP call, order completed | Browser network tab |

### Tenant Isolation Tests (if multi-tenant)

| # | Test | Expected Result | Method |
|---|------|-----------------|--------|
| 16 | Tenant A token, query Tenant B order by ID | 404 Not Found | curl |
| 17 | Tenant A token, cancel Tenant B order | 404 Not Found | curl |
| 18 | Cashier token with `X-Branch-Id` pointing to other branch | Falls back to own branch | curl |
| 19 | Admin token with `X-Branch-Id` pointing to other branch | Accesses target branch | curl |

### Regression Tests

| # | Test | Expected Result |
|---|------|-----------------|
| 20 | Full POS flow: open shift → create order → payment → receipt | Success |
| 21 | Expense creation and approval flow | Audit log created |
| 22 | Purchase invoice creation and payment | Audit log created |
| 23 | Customer credit sale and payment | Customer stats updated |
| 24 | Shift open/close with activity summary | Correct totals |

---

*End of document. This blueprint is execution-ready. Each fix is independently implementable and testable. Proceed in wave order.*
