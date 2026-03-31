---
inclusion: always
---

# KasserPro Architecture Rules

> **Single Source of Truth for System Architecture, Development Rules, and Data Lifecycle**
> 
> This file defines the REAL architecture of KasserPro. All AI agents and developers MUST follow these rules.

---

## 📚 Reference Documents

- **Architecture Manifest:** `docs/061_KASSERPRO_ARCHITECTURE_MANIFEST.md`
- **Database Migrations Guide:** `.kiro/steering/database-migrations-guide.md`
- **API Documentation:** `docs/001_API_DOCUMENTATION.md`

---

## 🏗️ System Architecture Overview

### Backend Stack
- **.NET 9** with Clean Architecture
- **SQLite** database with EF Core
- **JWT Authentication** with SecurityStamp validation
- **SignalR** for real-time device communication
- **Serilog** for structured logging

### Frontend Stack
- **React 18** with TypeScript
- **Vite** build tool
- **Redux Toolkit** + RTK Query for state management
- **TailwindCSS** for styling
- **Playwright** for E2E testing

### Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│  API Layer (Controllers, Middleware, Filters, Hubs)         │
│  - JWT Authentication + SecurityStamp validation            │
│  - BranchAccessMiddleware (tenant isolation)                │
│  - MaintenanceModeMiddleware (critical operations)          │
│  - ExceptionMiddleware (global error handling)              │
│  - IdempotencyMiddleware (duplicate prevention)             │
├─────────────────────────────────────────────────────────────┤
│  Application Layer (Services, DTOs, Interfaces)             │
│  - Business logic implementation                            │
│  - Manual validation in service layer with ErrorCodes       │
│  - Manual DTO mapping via MapToDto() methods                │
├─────────────────────────────────────────────────────────────┤
│  Domain Layer (Entities, Enums, Value Objects)              │
│  - Pure business entities                                   │
│  - No dependencies on other layers                          │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure Layer (Repositories, Services, Data)        │
│  - EF Core DbContext + Migrations                           │
│  - Repository Pattern + Unit of Work                        │
│  - BackupService, RestoreService, DataValidationService     │
│  - Background Services (DailyBackup, ShiftWarning)          │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow (Request → Response)

```
HTTP Request
    ↓
[MaintenanceModeMiddleware] → Block if maintenance active
    ↓
[CorrelationIdMiddleware] → Add X-Correlation-ID header for tracing
    ↓
[ExceptionMiddleware] → Global error handling
    ↓
[IdempotencyMiddleware] → Prevent duplicate write operations
    ↓
[JWT Authentication] → Validate token + SecurityStamp + user active + tenant active
    ↓
[BranchAccessMiddleware] → Validate X-Branch-Id header ownership (middleware)
    ↓
[Authorization] → Check roles and permissions
    ↓
[Controller] → Parse request
    ↓
[BranchScopeAuthorizationFilter] → Validate branchId in route/query/body (action filter — NOT middleware)
    ↓
[Service Layer] → Execute business logic + manual validation
    ↓
[Repository/UnitOfWork] → Database operations
    ↓
[AuditSaveChangesInterceptor] → Log changes to AuditLogs
    ↓
[Response] → Return ApiResponse<T> to client
```

---

## 🔄 Database & Migration Architecture

### Critical Migration Rules

**WHY:** SQLite has limited ALTER TABLE support. Wrong migrations = data loss.

#### ❌ NEVER Do This
```csharp
// This triggers table recreation in SQLite - DANGEROUS!
migrationBuilder.AlterColumn<decimal>(
    name: "Price",
    table: "Products",
    type: "TEXT");
```

#### ✅ ALWAYS Use Add + Migrate + Drop Pattern
```csharp
// Step 1: Add new column
migrationBuilder.AddColumn<decimal>(
    name: "PriceNew",
    table: "Products",
    type: "TEXT",
    defaultValue: 0m);

// Step 2: Migrate data
migrationBuilder.Sql("UPDATE Products SET PriceNew = Price");

// Step 3: Drop old column (in separate migration after testing)
migrationBuilder.DropColumn("Price", "Products");
migrationBuilder.RenameColumn("PriceNew", "Products", newName: "Price");
```

### Migration Lifecycle

```
Application Startup
    ↓
Check for pending migrations
    ↓
[YES] → Create pre-migration backup (MANDATORY)
    ↓
[Backup fails?] → STOP APPLICATION (safety first)
    ↓
Apply migrations via MigrateAsync()
    ↓
Seed initial data (if first run)
    ↓
Application starts normally
```

### SQLite-Specific Constraints

| Limitation | Impact | Solution |
|------------|--------|----------|
| No ALTER COLUMN | Cannot change column types directly | Use Add + Migrate + Drop |
| Type Affinity | `decimal` stored as TEXT | EF Core handles conversion |
| Limited FK support | Cannot add FK to existing data | Validate data first with SQL |
| No RENAME COLUMN | Cannot rename directly | Use Add + Copy + Drop |

### Migration Risk Assessment

| Change Type | Risk | Safe Pattern |
|-------------|------|--------------|
| Add table | ✅ Safe | `CreateTable` |
| Add column | ✅ Safe | `AddColumn` with default value |
| Drop table | ⚠️ Medium | Ensure no FK references |
| Drop column | ⚠️ Medium | Migrate data first |
| Change column type | 🔴 High | Add + Migrate + Drop (3 steps) |
| Rename column | 🔴 High | Add + Copy + Drop |
| Add FK | ⚠️ Medium | Validate existing data first |

---

## 🛡️ Data Safety & Backup Strategy

### Automatic Backup Types

| Backup Type | Trigger | Filename Pattern | Retention |
|-------------|---------|------------------|-----------|
| Pre-Migration | Pending migrations detected | `kasserpro-backup-YYYYMMDD-HHmmss-pre-migration.db` | Indefinite |
| Pre-Restore | Before restore operation | `kasserpro-backup-YYYYMMDD-HHmmss-pre-restore.db` | 14 days |
| Daily Scheduled | 2 AM UTC | `kasserpro-backup-YYYYMMDD-HHmmss-daily-scheduled.db` | 14 days |
| Manual | User-triggered | `kasserpro-backup-YYYYMMDD-HHmmss.db` | 14 days |

### Backup Integrity Validation

All backups undergo `PRAGMA integrity_check` immediately after creation. Corrupt backups are automatically deleted.

### Restore Flow

```
User initiates restore
    ↓
Validate backup file integrity
    ↓
Enable Maintenance Mode (blocks all API requests)
    ↓
Create pre-restore backup (safety net)
    ↓
Clear SQLite connection pools
    ↓
Delete WAL/SHM files
    ↓
Copy backup file over current database
    ↓
Apply pending migrations to restored database
    ↓
Run DataValidationService
    ↓
Disable Maintenance Mode
    ↓
Application resumes normal operation
```

---

## ⚙️ Critical Infrastructure Services

### LicenseService ⚠️ SECURITY CRITICAL
- Runs **BEFORE** application startup
- Binds application to machine MAC address
- Prevents copying database to another machine
- Throws exception if MAC mismatch — app won't start
- Location: `backend/KasserPro.API/LicenseService.cs`
- **Never remove or bypass this service**

### SqliteConfigurationService
- Configures SQLite PRAGMA settings on every startup
- Sets: `journal_mode=WAL`, `busy_timeout=5000`, `synchronous=NORMAL`, `cache_size=-64000`, `temp_store=MEMORY`
- Critical for concurrency and performance — runs before any DB operation
- Location: `backend/KasserPro.Infrastructure/Data/SqliteConfigurationService.cs`

### BranchScopeAuthorizationFilter
- **NOT middleware** — it is an `IAsyncActionFilter` registered globally
- Runs AFTER middleware pipeline, during controller action execution
- Validates branchId in route/query/body parameters using reflection
- Prevents non-admin users from accessing other branches
- Execution order: `BranchAccessMiddleware` (header check) → ... → `[Controller]` → `BranchScopeAuthorizationFilter` (parameter check)
- Location: `backend/KasserPro.API/Middleware/BranchScopeAuthorizationFilter.cs`

### AuditSaveChangesInterceptor
- Registered as **Singleton** in DI
- Logs all entity changes (Created, Updated, Deleted) automatically
- Excludes sensitive fields: `PinCode`, `PasswordHash`, `SecurityStamp`
- Captures: user who made the change, timestamp, old and new values
- Location: `backend/KasserPro.Infrastructure/Data/AuditSaveChangesInterceptor.cs`

### BackupService
- Creates hot backups using SQLite Backup API
- Validates backup integrity with PRAGMA checks
- Supports manual and automatic backups
- Location: `backend/KasserPro.Infrastructure/Services/BackupService.cs`

### RestoreService
- Handles database restoration with safety checks
- Enables maintenance mode during restore
- Creates pre-restore backup automatically
- Applies pending migrations after restore
- Location: `backend/KasserPro.Infrastructure/Services/RestoreService.cs`

### DataValidationService
- Validates data integrity after restore
- Checks numeric fields (Price, StockQuantity, Total)
- Logs issues but doesn't block restore
- Location: `backend/KasserPro.Infrastructure/Services/DataValidationService.cs`

### MaintenanceModeMiddleware
- Blocks all API requests during critical operations
- Returns 503 Service Unavailable
- Used during restore operations
- Location: `backend/KasserPro.API/Middleware/MaintenanceModeMiddleware.cs`

### Background Services

| Service | Purpose | Schedule | Status |
|---------|---------|----------|--------|
| DailyBackupBackgroundService | Automated daily backups | 2 AM UTC | ✅ Active |
| ShiftWarningBackgroundService | Warns about long-running shifts via SignalR | Every 30 minutes | ✅ Active |
| AutoCloseShiftBackgroundService | Auto-closes long-running shifts | — | ❌ Disabled in Program.cs |

> **Note:** `AutoCloseShiftBackgroundService` exists in code but is commented out. Shifts are managed manually.

---

## 🔄 Development Workflow

### Before Writing Any Code

1. **Identify Entities** - Does it need `TenantId` + `BranchId`?
2. **Identify Logic** - Is it financial? Add transactions
3. **Define DTOs** - Create/Update Request/Response
4. **Update Types** - Frontend types MUST match backend DTOs
5. **Write Tests** - TDD or alongside code
6. **Document API** - Update `docs/001_API_DOCUMENTATION.md`

### Feature Development Checklist

```
Backend:
- [ ] Entity + Migration (use Add + Migrate + Drop for changes)
- [ ] Repository + Service
- [ ] Controller + manual validation in service layer (ErrorCodes + ErrorMessages.Get())
- [ ] Integration Test
- [ ] Update API documentation

Frontend:
- [ ] Types in types/*.ts (MUST match backend DTOs — folder is frontend/ not client/)
- [ ] RTK Query API endpoint
- [ ] Components + Pages
- [ ] E2E Test (if UI-facing)
```

### Git Workflow

```bash
# Feature Branch
git checkout -b feature/feature-name

# Commit Message Format
feat: add new feature
fix: fix bug
docs: update documentation
test: add tests
refactor: refactor code
chore: update dependencies
```

---

## 🏛️ Backend Architecture Rules

### Layer Responsibilities

**API Layer (Controllers)**
- Parse HTTP requests
- Validate input (FluentValidation)
- Call service layer
- Return DTOs (never entities)
- Handle HTTP status codes

**Application Layer (Services)**
- Business logic implementation
- Transaction management (UnitOfWork)
- DTO mapping (AutoMapper)
- Permission checks
- Audit logging

**Domain Layer (Entities)**
- Pure business entities
- No dependencies on other layers
- Value objects and enums

**Infrastructure Layer**
- EF Core DbContext
- Repository implementations
- External service integrations
- Background services

### Service Pattern

```csharp
public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    
    public async Task<ApiResponse<OrderDto>> CreateOrderAsync(CreateOrderRequest request)
    {
        // 1. Get current user context
        var tenantId = _currentUserService.TenantId;
        var branchId = _currentUserService.BranchId;
        
        // 2. Validate business rules
        if (request.Items.Count == 0)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_EMPTY);
        
        // 3. Start transaction for financial operations
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            // 4. Execute business logic
            var order = new Order
            {
                TenantId = tenantId,
                BranchId = branchId,
                // ... other properties
            };
            
            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // 5. Return DTO
            return ApiResponse<OrderDto>.Success(MapToDto(order));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Repository + Unit of Work Pattern

```csharp
// Generic Repository
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
}

// Unit of Work (Transaction Management)
public interface IUnitOfWork : IDisposable
{
    IGenericRepository<Product> Products { get; }
    IGenericRepository<Order> Orders { get; }
    // ... other repositories
    
    Task<int> SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
}
```

---

## 🎨 Frontend Architecture Rules

### Project Structure

```
client/src/
├── api/              # RTK Query API definitions
│   ├── authApi.ts
│   ├── productsApi.ts
│   └── ordersApi.ts
├── components/       # Reusable components
│   ├── ui/          # Base UI components
│   └── features/    # Feature-specific components
├── hooks/           # Custom React hooks
├── pages/           # Page components (routes)
├── store/           # Redux store + slices
├── types/           # TypeScript types (MUST match backend DTOs)
└── utils/           # Utility functions
```

### Type Safety Rules

```typescript
// ✅ CORRECT - Types match backend DTOs
interface CreateOrderRequest {
  orderType: 'DineIn' | 'Takeaway' | 'Delivery';
  items: OrderItemDto[];
  paymentMethod: 'Cash' | 'Card' | 'Fawry';
}

// ❌ WRONG - Using 'any'
const data: any = response;

// ❌ WRONG - Magic strings
order.orderType = "dine_in";
```

### RTK Query Pattern

```typescript
// Define API
export const ordersApi = createApi({
  reducerPath: 'ordersApi',
  baseQuery: fetchBaseQuery({
    baseUrl: '/api',
    prepareHeaders: (headers) => {
      const token = localStorage.getItem('token');
      if (token) headers.set('Authorization', `Bearer ${token}`);
      return headers;
    },
  }),
  endpoints: (builder) => ({
    createOrder: builder.mutation<OrderDto, CreateOrderRequest>({
      query: (request) => ({
        url: '/orders',
        method: 'POST',
        body: request,
      }),
    }),
  }),
});

// Use in component
const [createOrder, { isLoading }] = useCreateOrderMutation();
```

---

## 💰 Business Logic Rules

### Financial Calculations (Tax Exclusive)

**WHY:** Prices in database are NET (without tax). Tax is ADDED at calculation time.

```typescript
// ✅ CORRECT
const netTotal = unitPrice * quantity;
const taxAmount = netTotal * (taxRate / 100);
const totalAmount = netTotal + taxAmount;

// ❌ WRONG - Tax Inclusive
const taxAmount = total / (1 + taxRate / 100);
```

### Rounding & Precision

```csharp
// All financial values MUST be rounded to 2 decimal places
decimal total = Math.Round(subtotal + tax, 2);
```

### Dynamic Tax Configuration

```csharp
// ❌ WRONG - Hardcoded
const decimal TAX_RATE = 0.14m;

// ✅ CORRECT - From Tenant
var tenant = await _tenantService.GetCurrentAsync();
var taxRate = tenant.TaxRate;
var isTaxEnabled = tenant.IsTaxEnabled;
```

### Transaction Integrity

```csharp
// ANY financial operation MUST be in a transaction
// ✅ CORRECT — always 'await using var' for proper disposal
await using var transaction = await _unitOfWork.BeginTransactionAsync();
try
{
    // Create Order
    // Process Payment
    // Update Inventory
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

// ❌ WRONG — missing 'await using var' causes connection leaks
await _unitOfWork.BeginTransactionAsync(); // Not assigned = not disposed!
```

> **Warning:** `UnitOfWork.BeginTransactionAsync()` returns the existing transaction if one is already active. The caller that STARTED the transaction should be the only one to call `CommitAsync()`.

### DTO Mapping Pattern

No AutoMapper is used. Use one of these patterns consistently:

```csharp
// ✅ Pattern 1 (preferred for projections — fastest, no entity load)
.Select(o => new OrderDto { Id = o.Id, Status = o.Status, ... })

// ✅ Pattern 2 (for single entity after fetch)
private static OrderDto MapToDto(Order order) => new() { Id = order.Id, ... };

// ❌ Don't mix patterns randomly in the same service
```

---

## 🔒 Multi-Tenancy & Security Rules

### Multi-Tenancy by Design

```csharp
// Every entity (except Auth) MUST have:
public int TenantId { get; set; }
public int BranchId { get; set; }

// ❌ NEVER hardcode IDs
var order = new Order { TenantId = 1, BranchId = 1 };

// ✅ ALWAYS use ICurrentUserService
var order = new Order
{
    TenantId = _currentUserService.TenantId,
    BranchId = _currentUserService.BranchId
};
```

### Optimistic Concurrency

```csharp
// Shift entity uses RowVersion for concurrency control
[Timestamp]
public byte[] RowVersion { get; set; }
```

### Audit Trails

```csharp
// AuditSaveChangesInterceptor automatically logs:
// - Entity changes (Created, Updated, Deleted)
// - User who made the change
// - Timestamp
// - Old and new values

// Sensitive fields are excluded:
// - PinCode
// - PasswordHash
// - SecurityStamp
```

---

## ✅ Validation Rules

### Input Validation

| Rule | Error Code | Enforcement |
|------|------------|-------------|
| Product.Price >= 0 | `PRODUCT_INVALID_PRICE` | Service layer |
| OrderItem.Quantity > 0 | `ORDER_INVALID_QUANTITY` | Service layer |
| Order.Items.length > 0 | `ORDER_EMPTY` | Service layer |
| Order.Status == Draft | `ORDER_NOT_EDITABLE` | Service layer |
| Product.IsActive == true | `PRODUCT_INACTIVE` | Service layer |
| Shift must be open | `NO_OPEN_SHIFT` | Service layer |
| Overpayment | Max 2x Total | Service layer |

### Actual Validation Pattern (No FluentValidation — manual checks in service)

```csharp
// ✅ CORRECT — manual validation in service layer
public async Task<ApiResponse<Guid>> CreateAsync(CreateProductDto dto, CancellationToken ct)
{
    if (dto.Price < 0)
        return ApiResponse<Guid>.Fail(ErrorCodes.PRODUCT_INVALID_PRICE,
            ErrorMessages.Get(ErrorCodes.PRODUCT_INVALID_PRICE));

    if (string.IsNullOrWhiteSpace(dto.Name))
        return ApiResponse<Guid>.Fail(ErrorCodes.PRODUCT_NAME_REQUIRED,
            ErrorMessages.Get(ErrorCodes.PRODUCT_NAME_REQUIRED));

    // ... business logic
}
```

### Error Response — Correct Pattern

```csharp
// ✅ CORRECT — Pattern 1 (preferred): ErrorCode + ErrorMessages.Get()
return ApiResponse<T>.Fail(ErrorCodes.ORDER_NOT_FOUND,
    ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

// ⚠️ ACCEPTABLE — Pattern 2: ErrorCode + hardcoded message
return ApiResponse<T>.Fail("BRANCH_ACCESS_DENIED", "ليس لديك صلاحية الوصول لهذا الفرع");

// ❌ WRONG — Pattern 3: message only (frontend can't handle by code)
return ApiResponse<T>.Fail("الشركة غير موجودة");
```

---

## 🧪 Testing Rules

### Testing Pyramid

```
        ┌─────────┐
        │  E2E    │  ← Playwright (complete-flow.spec.ts)
       ┌┴─────────┴┐
       │Integration│  ← ShiftLifecycleIntegrationTests
      ┌┴───────────┴┐
      │    Unit     │  ← Tax calculations, Business logic
     └──────────────┘
```

### E2E Test Scenarios

| Scene | Description | File |
|-------|-------------|------|
| Scene 1 | Admin Setup - Change tax rate | `frontend/e2e/` |
| Scene 2 | Cashier Workday - Open shift, create order, payment | `frontend/e2e/` |
| Scene 3 | Security Guard - Negative tests | `frontend/e2e/` |
| Scene 4 | Report Verification - Financial reports | `frontend/e2e/` |

> **Note:** Frontend folder is `frontend/` not `client/`. `complete-flow.spec.ts` is not yet created.

### Golden Rule

**❌ DO NOT deploy if ANY E2E test fails**

---

## 🚨 Non-Negotiable Constraints

### Database Rules
- ❌ NEVER modify DB schema without migration
- ❌ NEVER use `AlterColumn` in SQLite migrations
- ❌ NEVER skip pre-migration backup
- ✅ ALWAYS use Add + Migrate + Drop pattern for column changes
- ✅ ALWAYS test restore of old backups on new versions

### Security Rules
- ❌ NEVER bypass ICurrentUserService for tenant/branch IDs
- ❌ NEVER remove AuditSaveChangesInterceptor
- ❌ NEVER store passwords in plain text
- ✅ ALWAYS validate SecurityStamp in JWT
- ✅ ALWAYS use transactions for financial operations

### Code Quality Rules
- ❌ NEVER use `any` type in TypeScript
- ❌ NEVER use magic strings for enums
- ❌ NEVER have silent try/catch blocks
- ❌ NEVER allow negative prices or quantities
- ✅ ALWAYS log exceptions
- ✅ ALWAYS use proper enums
- ✅ ALWAYS match frontend types to backend DTOs

### Testing Rules
- ❌ NEVER skip E2E tests before deployment
- ❌ NEVER commit failing tests
- ✅ ALWAYS run integration tests for financial logic
- ✅ ALWAYS test restore operations after migrations

---

## 🔧 Configuration

### Ports

| Service | Port |
|---------|------|
| Backend API | 5243 |
| Frontend Dev | 3000 |

### Test Credentials

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@kasserpro.com | Admin@123 |
| Cashier | ahmed@kasserpro.com | 123456 |

### Tax Configuration
- **Default Rate:** 14% (Egypt VAT)
- **Model:** Tax Exclusive (Additive)
- **Timezone:** Africa/Cairo

---

## 📝 Pre-Deployment Checklist

```
Database:
- [ ] All migrations tested on production data copy
- [ ] Old backup restore tested on new version
- [ ] Pre-migration backup created automatically

Backend:
- [ ] All integration tests passing
- [ ] JWT secret configured (min 32 chars)
- [ ] Logging configured (Serilog)
- [ ] CORS configured for production origins

Frontend:
- [ ] Types match backend DTOs
- [ ] All E2E tests passing
- [ ] Production build tested
- [ ] API base URL configured

Security:
- [ ] SecurityStamp validation enabled
- [ ] Maintenance mode tested
- [ ] Audit logging verified
- [ ] Branch access middleware active
```

---

## 📦 BranchInventory Architecture (Updated March 2026)

> **Breaking Change:** `Product.StockQuantity` was removed in migration `20260329232433`.

Stock is now tracked **per branch** via `BranchInventory`:

```csharp
// ❌ OLD — no longer exists
product.StockQuantity -= quantity;

// ✅ NEW — per-branch inventory
var inventory = await _context.BranchInventory
    .FirstOrDefaultAsync(i => i.ProductId == productId
                            && i.BranchId == _currentUser.BranchId
                            && i.TenantId == _currentUser.TenantId, ct);
if (inventory is null || inventory.Quantity < quantity)
    return ApiResponse<T>.Fail(ErrorCodes.INSUFFICIENT_STOCK, ...);

inventory.Quantity -= quantity;
```

---

## 🤖 AI Agent Rules

### Before Generating Any Code

1. **مفيش AutoMapper** — استخدم `.Select()` أو `private static MapToDto()`
2. **مفيش FluentValidation** — validation يدوي في الـ service مع `ErrorCodes`
3. **الـ frontend في `frontend/`** مش `client/`
4. **Transaction دايمًا** `await using var transaction = await _unitOfWork.BeginTransactionAsync()`
5. **BranchScopeAuthorizationFilter** هو Action Filter مش Middleware
6. **Stock** في `BranchInventory` مش في `Product.StockQuantity`

### Error Response — الأولوية

```csharp
// ✅ دايمًا Pattern 1
ApiResponse<T>.Fail(ErrorCodes.X, ErrorMessages.Get(ErrorCodes.X))
```

### لو في تعارض بين الكود والـ Docs

- **الكود هو الحقيقة** — الـ docs ممكن تكون قديمة
- اسأل المطور قبل ما تفترض

---

> **BUILD. MAINTAIN. IMPROVE.**
> 
> Respect the structure. Protect the money. Secure the data.

---

**Document Owner:** Principal Software Architect  
**Last Updated:** March 30, 2026  
**Review Cycle:** Monthly
