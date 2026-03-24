# 🏛️ KasserPro Architecture Manifest
**Version:** 1.0  
**Last Updated:** January 8, 2026  
**Status:** Production-Ready ✅

> **هذا الملف هو المرجع الأساسي للمشروع. يجب الالتزام بكل القواعد المذكورة.**

---

## 🏗️ THE ARCHITECTURE (القواعد الثابتة)

### Backend (.NET 9, Clean Architecture)

```
┌─────────────────────────────────────────────────────────────┐
│                      API Layer                               │
│              Controllers, Middleware, Filters                │
├─────────────────────────────────────────────────────────────┤
│                  Application Layer                           │
│           Services, DTOs, Interfaces, Validators             │
├─────────────────────────────────────────────────────────────┤
│                    Domain Layer                              │
│              Entities, Enums, Value Objects                  │
├─────────────────────────────────────────────────────────────┤
│                Infrastructure Layer                          │
│        EF Core, Repositories, External Services              │
└─────────────────────────────────────────────────────────────┘
```

**القواعد:**
- **Layering:** Domain → Application → Infrastructure → API
- **Dependency Injection:** التزام صارم بـ Dependency Inversion Principle
- **Patterns:** Repository Pattern, Unit of Work (Transactions)
- **Database:** SQLite + EF Core with Interceptors (Audit Logs)

### Frontend (React 18, TypeScript, Vite)

```
client/
├── src/
│   ├── api/          # RTK Query APIs
│   ├── components/   # Reusable Components
│   ├── hooks/        # Custom Hooks
│   ├── pages/        # Page Components
│   ├── store/        # Redux Store & Slices
│   ├── types/        # TypeScript Types
│   └── utils/        # Utilities
└── e2e/              # Playwright E2E Tests
    └── pages/        # Page Objects
```

**القواعد:**
- **State Management:** Redux Toolkit (Global) + RTK Query (Server State)
- **Architecture:** Component-based, Page Objects pattern for tests
- **Styling:** TailwindCSS
- **Testing:** Playwright E2E

---

## 💰 THE FINANCIAL CONTRACT (منطق المال)

### Rule #1: Tax Logic - Tax Exclusive (Additive) ✅

السعر في قاعدة البيانات هو السعر **الصافي** (بدون ضريبة).

```csharp
// ✅ الطريقة الصحيحة
NetTotal = UnitPrice * Quantity
TaxAmount = NetTotal * (TaxRate / 100)
TotalAmount = NetTotal + TaxAmount

// ❌ ممنوع - Tax Inclusive
TaxAmount = Total / (1 + TaxRate/100)  // NEVER DO THIS
```

### Rule #2: Rounding & Precision

```csharp
// كل القيم المالية يجب تقريبها لخانتين عشريتين
decimal total = Math.Round(subtotal + tax, 2);
```

### Rule #3: Transaction Integrity (Atomicity)

```csharp
// أي عملية مالية يجب أن تكون في Transaction
await using var transaction = await _unitOfWork.BeginTransactionAsync();
try
{
    // Create Order
    // Process Payment
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Rule #4: Dynamic Configuration

```csharp
// ❌ ممنوع - Hardcoded
const decimal TAX_RATE = 0.14m;

// ✅ صحيح - من Tenant
var tenant = await _tenantService.GetCurrentAsync();
var taxRate = tenant.TaxRate;
var isTaxEnabled = tenant.IsTaxEnabled;
```

---

## 🔒 THE SECURITY PACT (ميثاق الأمان)

### Rule #5: Multi-Tenancy by Design

```csharp
// كل Entity (ما عدا Auth) يجب أن يحتوي على:
public int TenantId { get; set; }
public int BranchId { get; set; }

// استخدم ICurrentUserService - لا تكتب IDs يدوياً
var tenantId = _currentUserService.TenantId;
var branchId = _currentUserService.BranchId;
```

### Rule #6: Optimistic Concurrency

```csharp
// Shift Entity يجب أن يستخدم RowVersion
[Timestamp]
public byte[] RowVersion { get; set; }
```

### Rule #7: Validation Gates (Fail Fast)

| Validation | Rule |
|------------|------|
| Product Price | `>= 0` |
| Order Quantity | `> 0` |
| Order Status | Cannot modify if `Status != Draft` |
| Product Active | Cannot sell `IsActive == false` |
| Empty Orders | Cannot create with 0 items |
| Overpayment | Max 2x Total |

### Rule #8: Audit Trails

```csharp
// AuditSaveChangesInterceptor يسجل كل التغييرات
// البيانات الحساسة مستثناة: PinCode, PasswordHash
```

---

## 💻 THE CODE QUALITY STANDARDS

### Rule #9: Type Safety (Zero "Any")

```typescript
// ❌ ممنوع
const data: any = response;

// ✅ صحيح
const data: OrderDto = response;
```

**Enums بدلاً من Magic Strings:**

```csharp
// ❌ ممنوع
order.OrderType = "dine_in";

// ✅ صحيح
order.OrderType = OrderType.DineIn;
```

### Rule #10: Error Handling

```csharp
// Backend - استخدم ErrorCodes
return ApiResponse<T>.Fail(ErrorCodes.SHIFT_NOT_FOUND);

// Frontend - Global Error Handler
// 401 → Logout
// 400 → Show Toast
// 500 → Show Error Page
```

---

## 🧪 THE TESTING PYRAMID

```
        ┌─────────┐
        │  E2E    │  ← Playwright (complete-flow.spec.ts)
       ┌┴─────────┴┐
       │Integration│  ← ShiftLifecycleIntegrationTests
      ┌┴───────────┴┐
      │    Unit     │  ← Tax calculations, Business logic
     └──────────────┘
```

**Golden Rule:** ❌ لا تنشر إذا فشل أي E2E test

### E2E Test Scenarios (complete-flow.spec.ts)

| Scene | Description |
|-------|-------------|
| Scene 1 | Admin Setup - تغيير نسبة الضريبة |
| Scene 2 | Cashier Workday - فتح وردية، طلب، دفع |
| Scene 3 | Security Guard - اختبارات سلبية |
| Scene 4 | Report Verification - التقارير |

---

## 🛠️ DEVELOPMENT WORKFLOW

### قبل كتابة أي كود جديد:

1. **Identify Entities:** هل هي mapped؟ هل تحتوي على `TenantId`؟
2. **Identify Logic:** هل هي مالية؟ أضف Transactions
3. **Identify DTOs:** Create/Update Request/Response
4. **Write Tests:** TDD أو مع الكود
5. **Frontend Types:** حدّث `types/*.ts` فوراً
6. **E2E Scenario:** أضف test إذا أثر على UI

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
```

---

## ❌ FORBIDDEN ACTIONS (الأفعال الممنوعة)

| Action | Reason |
|--------|--------|
| ❌ تعديل DB بدون Migration | `dotnet ef migrations add` |
| ❌ حذف AuditSaveChangesInterceptor | Audit Trail مطلوب |
| ❌ تجاوز ICurrentUserService | Multi-tenancy |
| ❌ أسعار أو كميات سالبة | Financial integrity |
| ❌ try/catch صامت | Exceptions يجب تسجيلها |
| ❌ استخدام `any` في TypeScript | Type safety |
| ❌ Magic strings للـ Enums | Use proper Enums |

---

## 📋 CONFIGURATION

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

## 🎯 OBJECTIVE

> **BUILD. MAINTAIN. IMPROVE.**
> 
> احترم الهيكل. احمِ المال. أمّن البيانات.

---

**Document Owner:** Principal Software Architect  
**Review Cycle:** Monthly
