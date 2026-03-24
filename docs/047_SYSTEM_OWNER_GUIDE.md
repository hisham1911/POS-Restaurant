# 👑 دليل System Owner - KasserPro

## 📋 نظرة عامة

**System Owner** هو أعلى دور في النظام، مسؤول عن إدارة الشركات (Tenants) والإعدادات الحرجة.

---

## 🔐 بيانات الدخول الافتراضية

```
Email: owner@kasserpro.com
Password: Owner@123
```

⚠️ **مهم:** غيّر كلمة المرور فوراً بعد أول تسجيل دخول!

---

## 🎯 الصلاحيات

### ✅ صلاحيات حصرية لـ SystemOwner

| الصلاحية | الوصف | Endpoint |
|---------|-------|----------|
| **إدارة الشركات** | عرض جميع الشركات | `GET /api/system/tenants` |
| **تفعيل/تعطيل شركة** | التحكم في حالة الشركة | `PATCH /api/system/tenants/{id}/status` |
| **إنشاء شركة جديدة** | إضافة tenant جديد | `POST /api/system/tenants` |
| **Restore من Backup** | استعادة قاعدة البيانات | `POST /api/admin/restore` |

### ✅ صلاحيات مشتركة مع Admin

| الصلاحية | الوصف |
|---------|-------|
| **إنشاء Backup** | `POST /api/admin/backup` |
| **عرض Backups** | `GET /api/admin/backups` |
| **Deep Health Check** | `GET /api/health/deep` |
| **Migrate Inventory** | `POST /api/system/migrate-inventory` |

### ❌ ما لا يستطيع SystemOwner فعله

- **لا يصل لبيانات Tenants مباشرة** - يدير الشركات فقط، لا البيانات الداخلية
- **لا يستطيع حذف نفسه** - حماية من الأخطاء
- **لا يستطيع تسجيل الدخول كـ Tenant User** - separation of concerns

---

## 🏗️ Multi-Tenancy Architecture

### SystemOwner Scope

```csharp
// في قاعدة البيانات
TenantId = NULL  // ❗ NULL يعني global access
BranchId = NULL  // لا ينتمي لفرع محدد
```

### كيف يعمل؟

```
┌─────────────────────────────────────┐
│       System Owner (Global)         │
│     TenantId = NULL                 │
└──────────────┬──────────────────────┘
               │
       ┌───────┴───────┐
       │               │
   ┌───▼────┐     ┌───▼────┐
   │Tenant 1│     │Tenant 2│
   │  Admin │     │  Admin │
   └───┬────┘     └───┬────┘
       │              │
   ┌───▼────┐     ┌───▼────┐
   │Branch A│     │Branch X│
   │Cashiers│     │Cashiers│
   └────────┘     └────────┘
```

---

## 🔒 الأمان

### Role Escalation Prevention

```csharp
// ✅ SystemOwner يستطيع إنشاء أي role
if (currentUserRole == UserRole.SystemOwner) {
    // Allowed
}

// ❌ Admin لا يستطيع إنشاء SystemOwner
if (currentUserRole == UserRole.Admin && requestedRole == UserRole.SystemOwner) {
    return Fail("ليس لديك صلاحية إنشاء حساب مالك النظام");
}
```

### Security Logging

جميع محاولات privilege escalation تُسجل:

```
[WRN] Role escalation attempt: Admin {UserId} tried to create SystemOwner account
```

---

## 📱 الواجهة (Frontend)

### Routing

```typescript
// SystemOwner له مسارات خاصة
/owner/tenants  // إدارة الشركات

// لا يصل لمسارات Tenant
/pos            // ❌ Blocked
/orders         // ❌ Blocked
/products       // ❌ Blocked
```

### UI Indicators

```tsx
// Badge أحمر مميز
{user?.role === "SystemOwner" && (
  <span className="bg-red-100 text-red-700">
    مالك النظام
  </span>
)}
```

---

## 🛠️ كيفية إنشاء SystemOwner جديد

### الطريقة 1: Database Migration (الأكثر أماناً) ✅

```csharp
// في DbInitializer.cs
new User {
    Name = "Second Owner",
    Email = "owner2@kasserpro.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePassword123!"),
    Role = UserRole.SystemOwner,
    TenantId = null,
    BranchId = null,
    IsActive = true
}
```

### الطريقة 2: API Call (إذا كنت SystemOwner)

```bash
# Login as SystemOwner
TOKEN=$(curl -X POST http://localhost:5243/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"owner@kasserpro.com","password":"Owner@123"}' \
  | jq -r '.data.accessToken')

# Create new SystemOwner
curl -X POST http://localhost:5243/api/users \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Second Owner",
    "email": "owner2@kasserpro.com",
    "password": "SecurePassword123!",
    "role": "SystemOwner"
  }'
```

### الطريقة 3: Direct Database (Emergency Only) ⚠️

```sql
-- استخدم فقط في حالات الطوارئ
INSERT INTO Users (Name, Email, PasswordHash, Role, TenantId, BranchId, IsActive, CreatedAt, IsDeleted)
VALUES (
    'Emergency Owner',
    'emergency@kasserpro.com',
    '$2a$11$...',  -- BCrypt hash
    2,              -- SystemOwner
    NULL,
    NULL,
    1,
    datetime('now'),
    0
);
```

---

## 📊 إدارة الشركات (Tenants)

### إنشاء شركة جديدة

```bash
curl -X POST http://localhost:5243/api/system/tenants \
  -H "Authorization: Bearer $SYSTEMOWNER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "مطعم النور",
    "code": "NOUR",
    "adminName": "أحمد محمد",
    "adminEmail": "admin@nour.com",
    "adminPassword": "Admin@123",
    "defaultBranchName": "الفرع الرئيسي"
  }'
```

### تعطيل شركة

```bash
curl -X PATCH http://localhost:5243/api/system/tenants/5/status \
  -H "Authorization: Bearer $SYSTEMOWNER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"isActive": false}'
```

---

## 🧪 الاختبارات

### Test 1: SystemOwner يستطيع إنشاء SystemOwner

```bash
# Expected: ✅ Success
curl -X POST http://localhost:5243/api/users \
  -H "Authorization: Bearer $SYSTEMOWNER_TOKEN" \
  -d '{"role": "SystemOwner", ...}'
```

### Test 2: Admin لا يستطيع إنشاء SystemOwner

```bash
# Expected: ❌ "ليس لديك صلاحية إنشاء حساب مالك النظام"
curl -X POST http://localhost:5243/api/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"role": "SystemOwner", ...}'
```

### Test 3: SystemOwner يرى جميع Tenants

```bash
# Expected: ✅ List of all tenants
curl -X GET http://localhost:5243/api/system/tenants \
  -H "Authorization: Bearer $SYSTEMOWNER_TOKEN"
```

---

## ⚠️ Best Practices

### ✅ افعل

1. **غيّر كلمة المرور الافتراضية فوراً**
2. **استخدم 2FA إذا كان متاحاً** (future feature)
3. **راجع Audit Logs بانتظام**
4. **أنشئ backup قبل أي عملية حرجة**
5. **استخدم strong passwords** (12+ characters)

### ❌ لا تفعل

1. **لا تشارك بيانات SystemOwner مع أحد**
2. **لا تستخدم SystemOwner للعمليات اليومية** - استخدم Admin
3. **لا تحذف SystemOwner الوحيد** - احتفظ بنسخة احتياطية
4. **لا تعطل جميع Tenants** - قد تفقد الوصول
5. **لا تستخدم كلمات مرور ضعيفة**

---

## 🔄 Workflow مقترح

### إعداد شركة جديدة

```
1. SystemOwner: إنشاء Tenant جديد
   ↓
2. النظام: إنشاء Admin للـ Tenant تلقائياً
   ↓
3. Admin: تسجيل الدخول وإنشاء Branches
   ↓
4. Admin: إنشاء Cashiers وتعيين Permissions
   ↓
5. Cashiers: البدء في العمل
```

### تعطيل شركة

```
1. SystemOwner: مراجعة سبب التعطيل
   ↓
2. SystemOwner: إنشاء backup للـ Tenant
   ↓
3. SystemOwner: تعطيل الشركة
   ↓
4. النظام: منع جميع users من تسجيل الدخول
```

---

## 📞 الدعم

إذا واجهت مشاكل:

1. **راجع Logs:** `backend/KasserPro.API/logs/`
2. **تحقق من Database:** `backend/KasserPro.API/kasserpro.db`
3. **استعد من Backup:** `POST /api/admin/restore`

---

## 📚 مراجع إضافية

- [Architecture Manifest](./KASSERPRO_ARCHITECTURE_MANIFEST.md)
- [API Documentation](./api/API_DOCUMENTATION.md)
- [Security Guide](./SECURITY_GUIDE.md) (إذا كان موجوداً)

---

**آخر تحديث:** 2026-03-01  
**الإصدار:** 1.0  
**الحالة:** ✅ Production Ready
