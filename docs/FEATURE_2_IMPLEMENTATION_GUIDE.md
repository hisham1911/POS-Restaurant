# دليل تنفيذ الميزة 2: تحسينات إدارة الورديات

## ✅ ما تم إنجازه

### Domain Layer
- ✅ إنشاء `ShiftHandover` Entity
- ✅ إضافة حقول جديدة لـ `Shift` Entity:
  - LastActivityAt
  - InactivityWarningAt
  - IsForceClose, ForceClosedByUserId, ForceClosedByUserName, ForceClosedAt, ForceCloseReason
  - HandedOverFromUserId, HandedOverFromUserName, HandedOverAt
  - Navigation: ForceClosedByUser, Handovers collection
- ✅ إضافة Error Codes الجديدة

## 📋 الخطوات المتبقية

### المرحلة 1: Infrastructure Layer

#### 1.1 إنشاء Configuration للـ ShiftHandover
```bash
ملف: src/KasserPro.Infrastructure/Data/Configurations/ShiftHandoverConfiguration.cs
```

```csharp
namespace KasserPro.Infrastructure.Data.Configurations;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ShiftHandoverConfiguration : IEntityTypeConfiguration<ShiftHandover>
{
    public void Configure(EntityTypeBuilder<ShiftHandover> builder)
    {
        builder.ToTable("ShiftHandovers");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.FromUserName)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(e => e.ToUserName)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(e => e.CashBalance)
            .HasPrecision(18, 2);
            
        builder.Property(e => e.TotalSales)
            .HasPrecision(18, 2);
            
        builder.Property(e => e.Issues)
            .HasMaxLength(1000);
            
        builder.Property(e => e.Notes)
            .HasMaxLength(1000);
        
        // Indexes
        builder.HasIndex(e => e.ShiftId);
        builder.HasIndex(e => new { e.FromUserId, e.HandoverTime });
        builder.HasIndex(e => new { e.ToUserId, e.HandoverTime });
        
        // Relationships
        builder.HasOne(e => e.Shift)
            .WithMany(s => s.Handovers)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(e => e.FromUser)
            .WithMany()
            .HasForeignKey(e => e.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.ToUser)
            .WithMany()
            .HasForeignKey(e => e.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### 1.2 تحديث ShiftConfiguration
```bash
ملف: src/KasserPro.Infrastructure/Data/Configurations/ShiftConfiguration.cs
```

أضف في نهاية Configure method:

```csharp
// New indexes for shift improvements
builder.HasIndex(e => e.LastActivityAt);
builder.HasIndex(e => new { e.TenantId, e.BranchId, e.IsClosed });
builder.HasIndex(e => new { e.UserId, e.IsClosed });

// ForceClosedByUser relationship
builder.HasOne(e => e.ForceClosedByUser)
    .WithMany()
    .HasForeignKey(e => e.ForceClosedByUserId)
    .OnDelete(DeleteBehavior.Restrict);
```

#### 1.3 إضافة ShiftHandovers إلى DbContext
```bash
ملف: src/KasserPro.Infrastructure/Data/AppDbContext.cs
```

أضف:
```csharp
public DbSet<ShiftHandover> ShiftHandovers => Set<ShiftHandover>();
```

#### 1.4 إنشاء Migration
```bash
cd src/KasserPro.Infrastructure
dotnet ef migrations add AddShiftImprovements --startup-project ../KasserPro.API
```

#### 1.5 تحديث UnitOfWork
```bash
ملف: src/KasserPro.Infrastructure/Repositories/UnitOfWork.cs
```

أضف:
```csharp
public IRepository<ShiftHandover> ShiftHandovers => GetRepository<ShiftHandover>();
```

### المرحلة 2: Application Layer - DTOs

#### 2.1 إنشاء DTOs الجديدة
```bash
ملف: src/KasserPro.Application/DTOs/Shifts/ForceCloseShiftRequest.cs
```

```csharp
namespace KasserPro.Application.DTOs.Shifts;

public class ForceCloseShiftRequest
{
    public string Reason { get; set; } = string.Empty;
    public decimal ClosingBalance { get; set; }
    public string? Notes { get; set; }
}
```

```bash
ملف: src/KasserPro.Application/DTOs/Shifts/HandoverShiftRequest.cs
```

```csharp
namespace KasserPro.Application.DTOs.Shifts;

public class HandoverShiftRequest
{
    public int ToUserId { get; set; }
    public string? Issues { get; set; }
    public string? Notes { get; set; }
}
```

```bash
ملف: src/KasserPro.Application/DTOs/Shifts/AcknowledgeHandoverRequest.cs
```

```csharp
namespace KasserPro.Application.DTOs.Shifts;

public class AcknowledgeHandoverRequest
{
    public int HandoverId { get; set; }
}
```

```bash
ملف: src/KasserPro.Application/DTOs/Shifts/ShiftHandoverDto.cs
```

```csharp
namespace KasserPro.Application.DTOs.Shifts;

public class ShiftHandoverDto
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public string FromUserName { get; set; } = string.Empty;
    public string ToUserName { get; set; } = string.Empty;
    public DateTime HandoverTime { get; set; }
    public decimal CashBalance { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
    public string? Issues { get; set; }
    public string? Notes { get; set; }
    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
```

#### 2.2 تحديث ShiftDto
```bash
ملف: src/KasserPro.Application/DTOs/Shifts/ShiftDto.cs
```

أضف في نهاية الـ class:

```csharp
// New fields
public DateTime LastActivityAt { get; set; }
public DateTime? InactivityWarningAt { get; set; }
public bool IsForceClose { get; set; }
public string? ForceClosedByUserName { get; set; }
public DateTime? ForceClosedAt { get; set; }
public string? ForceCloseReason { get; set; }

public List<ShiftHandoverDto> Handovers { get; set; } = new();
```

### المرحلة 3: Application Layer - Service

#### 3.1 تحديث IShiftService Interface
```bash
ملف: src/KasserPro.Application/Services/Interfaces/IShiftService.cs
```

أضف:

```csharp
// New methods
Task<ApiResponse<List<ShiftDto>>> GetAllOpenShiftsAsync();
Task<ApiResponse<ShiftDto>> ForceCloseAsync(int shiftId, ForceCloseShiftRequest request);
Task<ApiResponse<ShiftHandoverDto>> HandoverShiftAsync(int shiftId, HandoverShiftRequest request);
Task<ApiResponse<bool>> AcknowledgeHandoverAsync(int shiftId, AcknowledgeHandoverRequest request);
Task<ApiResponse<bool>> UpdateActivityAsync(int shiftId);
Task<ApiResponse<List<ShiftDto>>> GetInactiveShiftsAsync(int hoursThreshold = 12);
```

#### 3.2 تنفيذ Methods في ShiftService
```bash
ملف: src/KasserPro.Application/Services/Implementations/ShiftService.cs
```

راجع التصميم في `market-ready-business-features/design.md` للتنفيذ الكامل.

الأهم:
- `GetAllOpenShiftsAsync()` - جلب كل الورديات المفتوحة (Admin only)
- `ForceCloseAsync()` - إغلاق قسري (Admin only)
- `HandoverShiftAsync()` - تسليم الوردية
- `AcknowledgeHandoverAsync()` - تأكيد استلام الوردية
- `UpdateActivityAsync()` - تحديث آخر نشاط (يُستدعى عند كل طلب)
- `GetInactiveShiftsAsync()` - جلب الورديات غير النشطة

### المرحلة 4: API Layer

#### 4.1 تحديث ShiftsController
```bash
ملف: src/KasserPro.API/Controllers/ShiftsController.cs
```

أضف Endpoints:

```csharp
[HttpGet("all-open")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetAllOpen()
{
    var result = await _shiftService.GetAllOpenShiftsAsync();
    return result.Success ? Ok(result) : BadRequest(result);
}

[HttpPost("{id}/force-close")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> ForceClose(int id, [FromBody] ForceCloseShiftRequest request)
{
    var result = await _shiftService.ForceCloseAsync(id, request);
    return result.Success ? Ok(result) : BadRequest(result);
}

[HttpPost("{id}/handover")]
public async Task<IActionResult> Handover(int id, [FromBody] HandoverShiftRequest request)
{
    var result = await _shiftService.HandoverShiftAsync(id, request);
    return result.Success ? Ok(result) : BadRequest(result);
}

[HttpPost("{id}/acknowledge-handover")]
public async Task<IActionResult> AcknowledgeHandover(int id, [FromBody] AcknowledgeHandoverRequest request)
{
    var result = await _shiftService.AcknowledgeHandoverAsync(id, request);
    return result.Success ? Ok(result) : BadRequest(result);
}
```

### المرحلة 5: Frontend - Types

#### 5.1 تحديث shift.types.ts
```bash
ملف: client/src/types/shift.types.ts
```

أضف:

```typescript
export interface Shift {
  // Existing fields...
  
  // New fields
  lastActivityAt: string;
  inactivityWarningAt?: string;
  isForceClose: boolean;
  forceClosedByUserName?: string;
  forceClosedAt?: string;
  forceCloseReason?: string;
  handovers: ShiftHandover[];
}

export interface ShiftHandover {
  id: number;
  shiftId: number;
  fromUserName: string;
  toUserName: string;
  handoverTime: string;
  cashBalance: number;
  orderCount: number;
  totalSales: number;
  issues?: string;
  notes?: string;
  acknowledged: boolean;
  acknowledgedAt?: string;
}

export interface ForceCloseShiftRequest {
  reason: string;
  closingBalance: number;
  notes?: string;
}

export interface HandoverShiftRequest {
  toUserId: number;
  issues?: string;
  notes?: string;
}
```

### المرحلة 6: Frontend - API

#### 6.1 تحديث shiftsApi.ts
```bash
ملف: client/src/api/shiftsApi.ts
```

أضف mutations:

```typescript
getAllOpenShifts: builder.query<ApiResponse<Shift[]>, void>({
  query: () => '/shifts/all-open',
  providesTags: ['Shifts'],
}),

forceCloseShift: builder.mutation<ApiResponse<Shift>, { id: number; request: ForceCloseShiftRequest }>({
  query: ({ id, request }) => ({
    url: `/shifts/${id}/force-close`,
    method: 'POST',
    body: request,
  }),
  invalidatesTags: ['Shifts'],
}),

handoverShift: builder.mutation<ApiResponse<ShiftHandover>, { id: number; request: HandoverShiftRequest }>({
  query: ({ id, request }) => ({
    url: `/shifts/${id}/handover`,
    method: 'POST',
    body: request,
  }),
  invalidatesTags: ['Shifts'],
}),

acknowledgeHandover: builder.mutation<ApiResponse<boolean>, { id: number; handoverId: number }>({
  query: ({ id, handoverId }) => ({
    url: `/shifts/${id}/acknowledge-handover`,
    method: 'POST',
    body: { handoverId },
  }),
  invalidatesTags: ['Shifts'],
}),
```

### المرحلة 7: Frontend - Components

#### 7.1 إنشاء ForceCloseShiftModal
```bash
ملف: client/src/components/shifts/ForceCloseShiftModal.tsx
```

Modal لإغلاق الوردية قسرياً (Admin only)

#### 7.2 إنشاء HandoverShiftModal
```bash
ملف: client/src/components/shifts/HandoverShiftModal.tsx
```

Modal لتسليم الوردية لمستخدم آخر

#### 7.3 إنشاء InactivityWarning
```bash
ملف: client/src/components/shifts/InactivityWarning.tsx
```

تحذير عند عدم النشاط لفترة طويلة

#### 7.4 تحديث ShiftPage
```bash
ملف: client/src/pages/shifts/ShiftPage.tsx
```

أضف:
- زر "تسليم الوردية"
- عرض Handovers في تفاصيل الوردية
- تحذير عدم النشاط

#### 7.5 إنشاء AllOpenShiftsPage (Admin)
```bash
ملف: client/src/pages/shifts/AllOpenShiftsPage.tsx
```

صفحة للـ Admin لعرض كل الورديات المفتوحة مع إمكانية الإغلاق القسري

## 🧪 الاختبار

### Backend Tests
```bash
cd src/KasserPro.Tests
dotnet test
```

### Frontend Tests
```bash
cd client
npm run test
```

### E2E Tests
```bash
cd client
npm run test:e2e
```

## 📝 ملاحظات مهمة

1. **Force Close**: فقط Admin يمكنه إغلاق الورديات قسرياً
2. **Handover**: يمكن للكاشير تسليم ورديته لكاشير آخر
3. **Activity Tracking**: يتم تحديث LastActivityAt عند كل طلب
4. **Inactivity Warning**: تحذير بعد 12 ساعة من عدم النشاط
5. **Transactions**: جميع العمليات تستخدم Transactions

## ✨ الميزات الجديدة

- ✅ إغلاق قسري للورديات (Admin)
- ✅ تسليم الوردية بين المستخدمين
- ✅ تتبع النشاط وتحذير عدم النشاط
- ✅ سجل كامل لتسليمات الورديات
- ✅ تأكيد استلام الوردية

---

**ملاحظة**: هذا الدليل يحتوي على الخطوات الأساسية. راجع `market-ready-business-features/design.md` للتفاصيل الكاملة والكود الكامل.
