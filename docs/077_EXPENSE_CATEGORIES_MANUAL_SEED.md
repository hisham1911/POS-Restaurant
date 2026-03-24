# حل مشكلة عدم ظهور التصنيفات - Seeding يدوي ✅

## المشكلة

التصنيفات ما زالت لا تظهر حتى بعد إضافة الـ Seeding في Program.cs.

## السبب المحتمل

قد يكون الـ Backend لم يقم بتشغيل الـ Seeding تلقائياً، أو حدث خطأ أثناء التشغيل.

## الحل

أضفت endpoint جديد لتشغيل الـ Seeding يدوياً.

## الخطوات

### 1. أعد بناء وتشغيل الـ Backend

```bash
cd src/KasserPro.API
dotnet build
dotnet run
```

### 2. استدعِ الـ Seeding Endpoint يدوياً

استخدم أحد الطرق التالية:

#### الطريقة الأولى: باستخدام Swagger

1. افتح المتصفح على: `http://localhost:5243/swagger`
2. ابحث عن endpoint: `POST /api/expense-categories/seed`
3. اضغط "Try it out"
4. اضغط "Execute"
5. يجب أن ترى رسالة نجاح

#### الطريقة الثانية: باستخدام PowerShell/CMD

```powershell
# احصل على الـ Token أولاً (استخدم بيانات Admin)
$loginBody = @{
    email = "admin@kasserpro.com"
    password = "Admin@123"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:5243/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
$token = $loginResponse.data.token

# استدعِ الـ Seeding endpoint
$headers = @{
    "Authorization" = "Bearer $token"
}

Invoke-RestMethod -Uri "http://localhost:5243/api/expense-categories/seed" -Method POST -Headers $headers
```

#### الطريقة الثالثة: باستخدام cURL

```bash
# احصل على الـ Token
TOKEN=$(curl -X POST http://localhost:5243/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@kasserpro.com","password":"Admin@123"}' \
  | jq -r '.data.token')

# استدعِ الـ Seeding endpoint
curl -X POST http://localhost:5243/api/expense-categories/seed \
  -H "Authorization: Bearer $TOKEN"
```

### 3. تحقق من النتيجة

1. افتح صفحة **إنشاء مصروف جديد** في الـ Frontend
2. يجب أن تظهر 10 تصنيفات في القائمة المنسدلة

## التصنيفات التي سيتم إضافتها

1. 💰 **رواتب** (Salaries) - #3B82F6
2. 🏢 **إيجار** (Rent) - #8B5CF6
3. ⚡ **كهرباء** (Electricity) - #F59E0B
4. 💧 **مياه** (Water) - #06B6D4
5. 🔧 **صيانة** (Maintenance) - #10B981
6. 📢 **تسويق** (Marketing) - #EC4899
7. 🚗 **مواصلات** (Transportation) - #6366F1
8. 📞 **اتصالات** (Communications) - #14B8A6
9. 📝 **مستلزمات مكتبية** (Office Supplies) - #F97316
10. 📦 **أخرى** (Other) - #64748B

## التحقق من قاعدة البيانات مباشرة

إذا أردت التحقق من قاعدة البيانات مباشرة:

```bash
cd src/KasserPro.API
sqlite3 kasserpro.db

# استعلام للتحقق من التصنيفات
SELECT Id, Name, NameEn, Icon, IsSystem, IsActive FROM ExpenseCategories;

# للخروج
.exit
```

## استكشاف الأخطاء

### إذا ظهرت رسالة خطأ "Unauthorized"

تأكد من:
- استخدام حساب Admin
- الـ Token صحيح وغير منتهي الصلاحية

### إذا لم تظهر التصنيفات في الـ Frontend

1. افتح Developer Tools (F12)
2. تحقق من Network tab
3. ابحث عن request إلى `/api/expense-categories`
4. تحقق من الـ Response - يجب أن يحتوي على array من التصنيفات

### إذا كان الـ Response فارغاً

قد تكون المشكلة في الـ TenantId. تحقق من:
- الـ JWT Token يحتوي على tenantId
- الـ X-Branch-Id header يتم إرساله بشكل صحيح

## الملفات المعدلة

- ✅ `src/KasserPro.API/Controllers/ExpenseCategoriesController.cs` - أضفت endpoint للـ Seeding اليدوي
- ✅ `src/KasserPro.API/Program.cs` - أضفت الـ Seeding التلقائي عند بدء التطبيق

## الـ Endpoint الجديد

```csharp
/// <summary>
/// Seed default expense categories (Admin only)
/// </summary>
[HttpPost("seed")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> SeedDefaultCategories()
{
    try
    {
        await _expenseCategoryService.SeedDefaultCategoriesAsync();
        return Ok(new { success = true, message = "Default categories seeded successfully" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error seeding default categories");
        return BadRequest(new { success = false, message = "Failed to seed categories" });
    }
}
```

---

**الحالة**: ✅ تم إضافة endpoint للـ Seeding اليدوي - جاهز للاختبار
