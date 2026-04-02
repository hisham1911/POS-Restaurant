---
inclusion: always
---

# 📖 KasserPro — Developer Workflow Guide

> **لأي حد هيشتغل على المشروع ده — اقرأ الملف ده الأول قبل أي سطر كود**
> آخر تحديث: أبريل 2026

---

## ⚡ الملفات المرجعية (لازم تعرف مكانها)

| الملف | الغرض | مكانه |
|-------|--------|--------|
| `architecture.md` | القواعد المعمارية الكاملة | `.kiro/steering/architecture.md` |
| `kasserpro-api-contract.md` | عقد الـ API بين الباك والفرونت | `.kiro/steering/api-contract.md` |
| `SKILL.md` | Best practices + أمثلة كود جاهزة | `.kiro/skills/kasserpro-bestpractices/SKILL.md` |
| `kasserpro-verification-prompt.md` | اختبار شامل قبل كل release | `docs/kasserpro-verification-prompt.md` |

**القاعدة الذهبية:** الكود هو الحقيقة — لو في تعارض بين أي وثيقة والكود، الكود يكسب. بلّغ وحدّث الوثيقة.

---

## 🚀 قبل ما تبدأ أي Feature

### اسأل نفسك الأسئلة العشر دي

```
1. الـ Feature دي بتخص entity؟         → محتاج TenantId + BranchId؟
2. في عملية مالية (فلوس/مخزون)؟       → Transaction إلزامية
3. هتقرأ data؟                        → AsNoTracking + TenantId + BranchId filter
4. هتكتب data جديدة؟                  → ICurrentUserService للـ TenantId/BranchId
5. محتاج Permission جديدة؟            → ضيفها في Permission.cs الأول
6. هتتعامل مع المخزون؟               → BranchInventory.Quantity (مش Product.StockQuantity)
7. هتعمل Error response؟             → ApiResponse.Fail(ErrorCodes.X, ErrorMessages.Get(X))
8. هتعمل validation؟                  → يدوي في الـ Service (مش FluentValidation)
9. هتعمل DTO mapping؟                 → MapToDto() أو .Select() (مش AutoMapper)
10. الفرونت محتاج يعرف؟              → حدّث api-contract.md مع الـ PR
```

---

## 🏗️ إضافة Feature جديدة — الخطوات بالترتيب

### الباك-اند

**الخطوة 1: الـ Domain**
```csharp
// في KasserPro.Domain/Entities/
public class NewEntity : BaseEntity  // BaseEntity فيه Id, TenantId, BranchId, IsDeleted, CreatedAt
{
    public int TenantId { get; set; }   // إلزامي لأي entity (ما عدا Auth)
    public int BranchId { get; set; }   // إلزامي لو per-branch
    public string Name { get; set; } = string.Empty;
    // ... باقي الـ properties
}
```

**الخطوة 2: الـ Migration**
```bash
# ✅ إزاي تعمل migration صح
dotnet ef migrations add AddNewEntity \
  --project backend/KasserPro.Infrastructure \
  --startup-project backend/KasserPro.API

# ⚠️ قبل ما تعدّل column موجود — اتبع Add + Migrate + Drop
# ❌ ممنوع: migrationBuilder.AlterColumn()
# ✅ صح: AddColumn → Sql("UPDATE...") → DropColumn
```

**الخطوة 3: الـ DTOs**
```csharp
// في KasserPro.Application/DTOs/NewEntities/
public record NewEntityDto(int Id, string Name, ...);
public record CreateNewEntityDto(string Name, ...);
public record UpdateNewEntityDto(string Name, ...);
```

**الخطوة 4: الـ Service**
```csharp
public class NewEntityService : INewEntityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;  // دايمًا inject ده
    private readonly AppDbContext _context;

    public async Task<ApiResponse<NewEntityDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var entity = await _context.NewEntities
            .AsNoTracking()                                          // ✅ للـ reads
            .FirstOrDefaultAsync(e => e.Id == id
                && e.TenantId == _currentUser.TenantId              // ✅ إلزامي
                && e.BranchId == _currentUser.BranchId, ct);        // ✅ إلزامي

        if (entity is null)
            return ApiResponse<NewEntityDto>.Fail(
                ErrorCodes.NEW_ENTITY_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.NEW_ENTITY_NOT_FOUND)); // ✅ ErrorCode دايمًا

        return ApiResponse<NewEntityDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateNewEntityDto dto, CancellationToken ct)
    {
        // Validation يدوي
        if (string.IsNullOrWhiteSpace(dto.Name))
            return ApiResponse<int>.Fail(
                ErrorCodes.NEW_ENTITY_NAME_REQUIRED,
                ErrorMessages.Get(ErrorCodes.NEW_ENTITY_NAME_REQUIRED));

        // لو في عملية مالية
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var entity = new NewEntity
            {
                Name = dto.Name,
                TenantId = _currentUser.TenantId,   // ✅ من ICurrentUserService دايمًا
                BranchId = _currentUser.BranchId,   // ✅ مش hardcoded
                CreatedBy = _currentUser.UserId
            };

            await _context.NewEntities.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ApiResponse<int>.Success(entity.Id, "تم الإنشاء بنجاح");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;   // ❌ ماتبلعش الـ exception
        }
    }

    private static NewEntityDto MapToDto(NewEntity e) => new(e.Id, e.Name, ...); // ✅ بدون AutoMapper
}
```

**الخطوة 5: الـ Controller**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NewEntitiesController : ControllerBase
{
    [HttpGet("{id}")]
    [HasPermission(Permission.NewEntityView)]    // ✅ دايمًا HasPermission مش [Authorize] بس
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(Permission.NewEntityManage)]
    public async Task<IActionResult> Create([FromBody] CreateNewEntityDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));
}
```

**الخطوة 6: الـ Error Codes**
```csharp
// في ErrorCodes.cs
public const string NEW_ENTITY_NOT_FOUND = "NEW_ENTITY_NOT_FOUND";
public const string NEW_ENTITY_NAME_REQUIRED = "NEW_ENTITY_NAME_REQUIRED";

// في ErrorMessages.cs
{ ErrorCodes.NEW_ENTITY_NOT_FOUND, "العنصر غير موجود" },
{ ErrorCodes.NEW_ENTITY_NAME_REQUIRED, "الاسم مطلوب" },
```

**الخطوة 7: الـ Permission**
```csharp
// في Permission.cs
NewEntityView = XX,
NewEntityManage = XX+1,
```

---

### الفرونت-اند

**الخطوة 1: الـ Types**
```typescript
// في frontend/src/types/newEntity.types.ts
export interface NewEntityDto {
  id: number;           // int من الباك-اند
  name: string;
  // ... باقي الـ fields — يطابق الـ DTO بالظبط
}

export interface CreateNewEntityDto {
  name: string;
}

// ❌ ممنوع
const entity: any = response;  // لا any
```

**الخطوة 2: الـ RTK Query**
```typescript
// في frontend/src/api/newEntityApi.ts
export const newEntityApi = createApi({
  reducerPath: 'newEntityApi',
  baseQuery: baseQueryWithAuth,  // استخدم الـ base query الموجود
  tagTypes: ['NewEntity'],
  endpoints: (builder) => ({
    getNewEntity: builder.query<ApiResponse<NewEntityDto>, number>({
      query: (id) => `/new-entities/${id}`,
      providesTags: ['NewEntity'],
    }),
    createNewEntity: builder.mutation<ApiResponse<number>, CreateNewEntityDto>({
      query: (dto) => ({
        url: '/new-entities',
        method: 'POST',
        body: dto,
        headers: { 'X-Idempotency-Key': crypto.randomUUID() },  // ✅ للـ writes
      }),
      invalidatesTags: ['NewEntity'],
    }),
  }),
});
```

**الخطوة 3: الـ Error Handling**
```typescript
// في الـ component
const [createEntity] = useCreateNewEntityMutation();

const handleCreate = async () => {
  try {
    const id = await createEntity(dto).unwrap();  // ✅ unwrap دايمًا
    toast.success('تم الإنشاء');
  } catch (err) {
    const error = err as { data: ApiResponse<null> };
    // ✅ errorCode للـ logic مش message
    switch (error.data?.errorCode) {
      case 'NEW_ENTITY_NAME_REQUIRED':
        setFieldError('name', 'الاسم مطلوب');
        break;
      default:
        toast.error(error.data?.message ?? 'حدث خطأ');
    }
  }
};
```

---

## ✏️ تعديل Feature موجودة

### قبل أي تعديل

```bash
# 1. تأكد من الـ build والـ tests شغالين
dotnet test backend/KasserPro.Tests/ --filter "YourFeatureTests"
npx tsc --noEmit   # من frontend/

# 2. اقرأ الكود الموجود الأول — متفترضش
```

### لو هتغير DTO (Breaking Change)

```
الخطوات الإلزامية:
1. غيّر الـ DTO في الباك-اند
2. حدّث الـ type المقابل في frontend/src/types/
3. حدّث أي RTK Query endpoints بتستخدم الـ type ده
4. حدّث kasserpro-api-contract.md
5. اعمل dotnet build + npx tsc --noEmit
6. لو في migration محتاجها → اتبع Add + Migrate + Drop
```

### لو هتغير Error Code

```
الخطوات الإلزامية:
1. غيّر في ErrorCodes.cs
2. غيّر في ErrorMessages.cs
3. دوّر على كل مكان بيستخدم الـ error code ده:
   rg -rn "OLD_ERROR_CODE" frontend/src/
4. حدّث الـ switch/case في الفرونت
5. حدّث جدول الـ Error Codes في kasserpro-api-contract.md
```

### لو هتغير Permission

```
الخطوات الإلزامية:
1. غيّر في Permission.cs
2. غيّر في كل الـ [HasPermission(...)] attributes
3. حدّث أي permission checks في الفرونت (useAuth / permission guards)
4. حدّث قائمة الـ permissions في api-contract.md
```

---

## 🗄️ تغيير Database Schema

### القاعدة الأهم: ❌ ممنوع AlterColumn في SQLite

```csharp
// ❌ ده بيعمل table recreation وممكن يضيع data
migrationBuilder.AlterColumn<decimal>("Price", "Products", "TEXT");

// ✅ الطريقة الآمنة الوحيدة — 3 خطوات
// Step 1: Add new column
migrationBuilder.AddColumn<decimal>("PriceNew", "Products", "TEXT", defaultValue: 0m);

// Step 2: Copy data
migrationBuilder.Sql("UPDATE Products SET PriceNew = Price");

// Step 3: في migration منفصلة بعد testing
migrationBuilder.DropColumn("Price", "Products");
migrationBuilder.RenameColumn("PriceNew", "Products", "Price");
```

### قبل أي Migration

```bash
# 1. خذ backup من الـ DB
curl -X POST http://localhost:5243/api/admin/backup \
  -H "Authorization: Bearer $TOKEN"

# 2. اعمل الـ migration
dotnet ef migrations add YourMigrationName ...

# 3. راجع الـ migration file الناتج يدوياً
# تأكد مفيش AlterColumn

# 4. اعمل migration على نسخة test
dotnet ef database update
```

---

## 🧪 اختبر كودك قبل الـ Commit

### Checklist إلزامية

```bash
# 1. Build
dotnet build backend/KasserPro.API/ -c Release
# EXPECTED: 0 errors

# 2. Tests
dotnet test backend/KasserPro.Tests/
# EXPECTED: all pass

# 3. TypeScript
cd frontend && npx tsc --noEmit
# EXPECTED: 0 errors

# 4. Contract violations check
rg -n "return Ok\(new \{" backend/KasserPro.API/Controllers/
rg -n 'ApiResponse<.*>\.Fail\("[^"]+"\)' backend/KasserPro.Application/Services/
rg -n "\.success\s*===" frontend/src/ | grep -v "toast"
# EXPECTED: 0 matches في الثلاثة
```

### اكتب Integration Test لكل Feature

```csharp
// لازم يغطي الثلاث سيناريوهات دول على الأقل:

// 1. Happy path
[Fact] public async Task Create_ValidData_ReturnsSuccess() { }

// 2. Security — Tenant isolation
[Fact] public async Task GetById_WrongTenant_ReturnsNotFound() { }

// 3. Permission
[Fact] public async Task Create_WithoutPermission_Returns403() { }
```

---

## 🚨 المشاكل اللي ممكن تحصل وإزاي تحلها

### "build failed — obj corrupted"
```bash
# امسح الـ obj folders وابني من جديد
Remove-Item -Recurse -Force backend/KasserPro.API/obj
Remove-Item -Recurse -Force backend/KasserPro.Application/obj
Remove-Item -Recurse -Force backend/KasserPro.Infrastructure/obj
dotnet build backend/KasserPro.API/
```

### "Database is locked"
```bash
# تأكد WAL mode شغال
sqlite3 kasserpro.db "PRAGMA journal_mode;"
# Expected: wal

# لو locked، اعمل checkpoint
sqlite3 kasserpro.db "PRAGMA wal_checkpoint(TRUNCATE);"
```

### "Frontend types mismatch with backend"
```bash
# دوّر على الـ type اللي مش متطابق
rg -rn "interfaceName" frontend/src/types/
# قارنه بالـ DTO في الباك-اند
cat backend/KasserPro.Application/DTOs/...
# حدّث الـ TypeScript type يطابق الـ C# DTO
```

### "errorCode is null in response"
```csharp
// في الـ service، تأكد من الـ pattern ده
// ❌ غلط
return ApiResponse<T>.Fail("رسالة فقط");

// ✅ صح
return ApiResponse<T>.Fail(
    ErrorCodes.ENTITY_SPECIFIC_ERROR,
    ErrorMessages.Get(ErrorCodes.ENTITY_SPECIFIC_ERROR));
```

### "401 بعد reset password"
```csharp
// بعد أي تغيير في الـ password، لازم:
user.UpdateSecurityStamp();  // ✅ هينهي كل الـ JWT tokens الحالية
await _unitOfWork.SaveChangesAsync(ct);
```

---

## 📋 قبل الـ Release — الـ Final Checklist

```
🔐 Security:
- [ ] dotnet test — all pass
- [ ] npx tsc --noEmit — 0 errors
- [ ] مفيش hardcoded credentials في الكود
- [ ] Swagger disabled في production (if !IsDevelopment)
- [ ] CORS محدد بـ IP الـ frontend الصح مش "*"
- [ ] JWT secret في environment variable مش في appsettings.json

🗄️ Database:
- [ ] كل الـ migrations اتاختبرت على نسخة من الـ production data
- [ ] Pre-migration backup شغال تلقائي
- [ ] WAL mode شغال: PRAGMA journal_mode = wal

📝 Code Quality:
- [ ] مفيش ad-hoc response objects
- [ ] مفيش single-arg Fail() بدون ErrorCode
- [ ] مفيش silent catch {}
- [ ] مفيش Product.StockQuantity references

📄 Documentation:
- [ ] kasserpro-api-contract.md اتحدث لو في endpoints جديدة
- [ ] Error codes جديدة اتضافت للجدول
- [ ] architecture.md اتحدث لو في architectural decision جديد

🧪 Testing:
- [ ] Integration tests للـ feature الجديدة
- [ ] Security test (cross-tenant isolation)
- [ ] Permission test
- [ ] Manual browser test للـ user flow الكامل
```

---

## 💬 لما تشتغل مع AI Agent (Kiro / Cursor / Copilot)

**ابدأ كل prompt بده:**
```
Read these files first before generating any code:
1. .kiro/steering/architecture.md
2. .kiro/steering/api-contract.md
3. .kiro/skills/kasserpro-bestpractices/SKILL.md

Then: [طلبك هنا]
```

**بعد أي كود يتولّد، اعمل verify:**
```bash
dotnet build && dotnet test && npx tsc --noEmit
```

**لو الكود فيه أي من الـ patterns دي → reject وابدأ تاني:**
- `AutoMapper` أو `_mapper.Map<>`
- `FluentValidation` أو `AbstractValidator`
- `new { success = true, data = ... }` بدل `ApiResponse<T>`
- `.Fail("نص فقط")` بدون ErrorCode
- `product.StockQuantity` (اتشال)
- `client/` بدل `frontend/`
- `TenantId = 1` hardcoded

---

> **المبدأ الوحيد:** اكتب كود زي ما لو حد هيقرأه بعدك بسنتين — واضح، آمن، ومتوقع.
>
> **Build. Maintain. Improve.**
