# Phase 1 – Backend Readiness (MVP)

> **المرجع الرسمي:** [API_DOCUMENTATION.md](../api/API_DOCUMENTATION.md)  
> **التقنيات:** .NET 9 · EF Core · SQLite · JWT · Clean Architecture  
> **حالة المشروع:** ✅ مكتمل وعملي

---

## الحالة الحالية

| الميزة                                | الحالة |
| ------------------------------------- | ------ |
| Auth (JWT + Roles)                    | ✅     |
| Products / Categories                 | ✅     |
| Orders / Payments (Cash, Card, Fawry) | ✅     |
| Shifts & VAT 14%                      | ✅     |
| Tenants (multi-tenant)                | ✅     |
| Branches                              | ✅     |
| Audit Logs                            | ✅     |
| ICurrentUserService                   | ✅     |
| Price & Tax Snapshots                 | ✅     |
| Daily Reports                         | ✅     |
| Integration Tests                     | ✅     |

---

## هيكل المشروع

```
src/
├── KasserPro.Domain/        # Entities, Enums, Common
├── KasserPro.Application/   # DTOs, Services, Interfaces
├── KasserPro.Infrastructure/# DbContext, Repositories, Migrations
├── KasserPro.API/           # Controllers, Middleware
└── KasserPro.Tests/         # Integration Tests
```

---

## الكيانات الأساسية (Entities)

| Entity    | الوصف                      |
| --------- | -------------------------- |
| Tenant    | الشركات (multi-tenant)     |
| Branch    | الفروع                     |
| User      | المستخدمون (Admin/Cashier) |
| Category  | التصنيفات                  |
| Product   | المنتجات                   |
| Order     | الطلبات                    |
| OrderItem | عناصر الطلب                |
| Payment   | المدفوعات                  |
| Shift     | الورديات                   |
| AuditLog  | سجل التدقيق                |

---

## ملخص الـ APIs

| Method              | Endpoint                    | الوصف           |
| ------------------- | --------------------------- | --------------- |
| POST                | `/api/auth/login`           | تسجيل الدخول    |
| GET                 | `/api/auth/me`              | المستخدم الحالي |
| GET/PUT             | `/api/tenants/current`      | بيانات الشركة   |
| GET/POST/PUT/DELETE | `/api/branches`             | CRUD الفروع     |
| GET/POST/PUT/DELETE | `/api/categories`           | CRUD التصنيفات  |
| GET/POST/PUT/DELETE | `/api/products`             | CRUD المنتجات   |
| GET/POST            | `/api/orders`               | الطلبات         |
| POST                | `/api/orders/{id}/complete` | إكمال الطلب     |
| GET/POST            | `/api/shifts`               | الورديات        |
| GET                 | `/api/reports/daily`        | التقرير اليومي  |
| GET                 | `/api/audit-logs`           | سجل التدقيق     |

> التفاصيل الكاملة في [API_DOCUMENTATION.md](../api/API_DOCUMENTATION.md)

---

## ✅ Checklist للإنهاء

- [x] إنشاء `Tenant.cs`
- [x] إنشاء `Branch.cs`
- [x] إنشاء `AuditLog.cs`
- [x] تحديث `User.cs` (إضافة TenantId + BranchId)
- [x] تحديث `Order.cs` (إضافة TenantId + BranchId + Snapshots)
- [x] تحديث `Shift.cs` (إضافة TenantId + BranchId)
- [x] تحديث `Product.cs` (إضافة TenantId + TaxRate + TaxInclusive)
- [x] تحديث `Category.cs` (إضافة TenantId)
- [x] تحديث `Payment.cs` (إضافة TenantId + BranchId)
- [x] تحديث `AppDbContext.cs` (DbSets + Relationships)
- [x] إنشاء DTOs (TenantDto, BranchDto, AuditLogDto)
- [x] إنشاء Services (ITenantService, IBranchService, IAuditLogService)
- [x] إنشاء Controllers (TenantsController, BranchesController, AuditLogsController)
- [x] تحديث UnitOfWork
- [x] Migration (AddTenantBranchAudit)
- [x] تحديث Seed Data
- [x] تطبيق Migration على قاعدة البيانات
- [x] اختبار الـ APIs
- [x] إنشاء ICurrentUserService
- [x] إنشاء AuditSaveChangesInterceptor
- [x] إنشاء Integration Tests
- [x] تطبيق Price & Tax Snapshots
- [x] ربط الطلبات بالورديات (ShiftId)

---

## 🏆 ملخص التحسينات المعمارية

### 1. أمان البيانات (ICurrentUserService)

```csharp
// استخراج TenantId, BranchId, UserId من JWT Claims
public class CurrentUserService : ICurrentUserService
{
    public int TenantId => // من JWT claim "tenantId"
    public int BranchId => // من X-Branch-Id header أو JWT claim
    public int UserId => // من JWT claim "userId"
}
```

**الفوائد:**
- لا يوجد Hardcoded TenantId/BranchId
- دعم تبديل الفروع عبر Header `X-Branch-Id`
- عزل البيانات بين المستأجرين

### 2. حماية البيانات (Price & Tax Snapshots)

```csharp
// Order.cs - Snapshots للفرع
public string? BranchName { get; set; }
public string? BranchAddress { get; set; }
public string? BranchPhone { get; set; }

// OrderItem.cs - Snapshots للمنتج
public string ProductName { get; set; }
public decimal UnitPrice { get; set; }
public decimal TaxRate { get; set; }
public bool TaxInclusive { get; set; }
```

**الفوائد:**
- الفواتير القديمة تحتفظ بالأسعار الأصلية
- تغيير سعر المنتج لا يؤثر على الطلبات السابقة
- دعم الضريبة المضمنة (Egypt VAT 14%)

### 3. التكامل الكامل للورديات (Shift Logic)

```csharp
// OrderService.CreateAsync - التحقق من الوردية
var currentShift = await _unitOfWork.Shifts.Query()
    .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsClosed);

if (currentShift == null)
    return ApiResponse.Fail("يجب فتح وردية قبل إنشاء طلب");

order.ShiftId = currentShift.Id;
```

**الفوائد:**
- لا يمكن إنشاء طلب بدون وردية مفتوحة
- الطلبات مرتبطة بالوردية تلقائياً
- حساب إجماليات الوردية (TotalCash, TotalCard, TotalOrders)

### 4. سجل التدقيق المتقدم (AuditSaveChangesInterceptor)

```csharp
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    // يسجل تلقائياً: Create, Update, Delete
    // يحفظ: UserId, UserName, IpAddress, OldValues, NewValues
    // يدعم: EntityId الصحيح للكيانات الجديدة (بعد SaveChanges)
}
```

**الفوائد:**
- تسجيل تلقائي لجميع التغييرات
- تتبع المستخدم وعنوان IP
- حفظ القيم القديمة والجديدة بصيغة JSON

---

## أوامر مهمة

```powershell
# تشغيل المشروع
cd src/KasserPro.API
dotnet run

# إنشاء Migration
dotnet ef migrations add <Name> -p ../KasserPro.Infrastructure -s .

# تطبيق Migration
dotnet ef database update -p ../KasserPro.Infrastructure -s .

# تشغيل الاختبارات
dotnet test src/KasserPro.Tests
```

---

## ملاحظات تنفيذية

- حافظ على Clean Architecture (Domain ← Application ← Infrastructure ← API).
- استخدم DTOs للـ API responses (لا تُرجع Entities مباشرة).
- أنواع الدفع: `Cash = 0`, `Card = 1`, `Fawry = 2`.
- نسبة الضريبة: `14%` (VAT مصر) - مضمنة في السعر.
- نمط الاستجابة الموحد: `ApiResponse<T>` مع `success`, `message`, `data`.
- جميع الكيانات مرتبطة بـ TenantId للدعم متعدد الشركات.
- الطلبات والورديات والمدفوعات مرتبطة بـ BranchId.
- التوقيت: يُخزن بـ UTC ويُعرض بتوقيت القاهرة.

---

## 🔧 سجل الإصلاحات (Hotfixes)

### المشاكل التي وُجدت وتم إصلاحها

| المشكلة                                 | الملف                              | الإصلاح                                                |
| --------------------------------------- | ---------------------------------- | ------------------------------------------------------ |
| Missing `TenantId/BranchId` في Orders   | `OrderService.cs`                  | استخدام `ICurrentUserService` بدلاً من Hardcoded      |
| Missing `TenantId/BranchId` في Payments | `OrderService.cs`                  | استخدام `ICurrentUserService`                         |
| Missing `TenantId/BranchId` في Shifts   | `ShiftService.cs`                  | استخدام `ICurrentUserService` + Validation            |
| عدم تطابق `CompleteOrderRequest`        | `CreateOrderRequest.cs`            | استخدام `Payments[]` array                            |
| DTOs ناقصة الحقول                       | `OrderDto.cs`                      | إضافة Snapshots + ShiftId                             |
| Missing Payments في Query               | `OrderService.cs`                  | إضافة `.Include(o => o.Payments)`                     |
| Shift Orders لا تظهر                    | `ShiftService.cs`                  | إضافة `.Include(s => s.Orders).ThenInclude(Payments)` |
| Audit Log بدون UserId                   | `AuditSaveChangesInterceptor.cs`   | استخدام `IHttpContextAccessor` لاستخراج User          |
| EntityId = 0 للكيانات الجديدة           | `AuditSaveChangesInterceptor.cs`   | نقل التسجيل إلى `SavedChangesAsync`                   |
| Date Filter لا يعمل                     | `AuditLogService.cs`               | إصلاح `.Date` للمقارنة الصحيحة                        |

---

## 🎯 الدروس المستفادة (Lessons Learned)

### 1. **Multi-Tenancy من البداية**
```
✅ الحل: استخدام ICurrentUserService في كل Service
✅ الحل: Global Query Filters للـ TenantId (مستقبلاً)
```

### 2. **Snapshots للبيانات المالية**
```
✅ الحل: حفظ الأسعار والضرائب في OrderItem
✅ الحل: حفظ بيانات الفرع في Order
```

### 3. **Integration Tests**
```
✅ الحل: إنشاء CustomWebApplicationFactory
✅ الحل: اختبار سيناريو تبديل الفروع (X-Branch-Id)
```

### 4. **Audit Trail**
```
✅ الحل: EF Core Interceptor للتسجيل التلقائي
✅ الحل: حفظ IP Address من HttpContext
```

---

## 📋 TODO للـ Phase 2

- [ ] Global Query Filters للـ TenantId
- [ ] Soft Delete Filter
- [ ] Rate Limiting
- [ ] API Versioning
- [ ] Background Jobs (Hangfire)
- [ ] Email Notifications
- [ ] Export Reports (PDF/Excel)
