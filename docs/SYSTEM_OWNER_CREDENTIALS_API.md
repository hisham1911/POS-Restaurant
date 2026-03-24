# 🔐 System Owner - Credentials API

## ⚠️ تحذير مهم
هذا الـ endpoint مخصص **للعرض التوضيحي فقط** ولا يجب استخدامه في الإنتاج!

---

## 📋 API Endpoint

### GET `/api/system/credentials`

**الوصف:** عرض جميع بيانات الدخول للمستخدمين (System Owner فقط)

**Authorization:** Bearer Token (System Owner Role)

**Response:**
```json
{
  "success": true,
  "data": {
    "totalUsers": 8,
    "tenants": [
      {
        "tenantId": null,
        "tenantName": "System",
        "users": [
          {
            "id": 1,
            "name": "System Owner",
            "email": "owner@kasserpro.com",
            "role": "SystemOwner",
            "password": "Owner@123",
            "tenantId": null,
            "tenantName": "System",
            "branchId": null,
            "branchName": null,
            "isActive": true
          }
        ]
      },
      {
        "tenantId": 1,
        "tenantName": "مجزر الأمانة",
        "users": [
          {
            "id": 2,
            "name": "أحمد المدير",
            "email": "admin@kasserpro.com",
            "role": "Admin",
            "password": "Admin@123",
            "tenantId": 1,
            "tenantName": "مجزر الأمانة",
            "branchId": 1,
            "branchName": "الفرع الرئيسي",
            "isActive": true
          },
          {
            "id": 3,
            "name": "محمد الكاشير",
            "email": "mohamed@kasserpro.com",
            "role": "Cashier",
            "password": "123456",
            "tenantId": 1,
            "tenantName": "مجزر الأمانة",
            "branchId": 1,
            "branchName": "الفرع الرئيسي",
            "isActive": true
          }
        ]
      },
      {
        "tenantId": 2,
        "tenantName": "محل الأمل للأدوات المنزلية",
        "users": [
          {
            "id": 5,
            "name": "سامي المدير",
            "email": "samy@homeappliances.com",
            "role": "Admin",
            "password": "Admin@123",
            "tenantId": 2,
            "tenantName": "محل الأمل للأدوات المنزلية",
            "branchId": 2,
            "branchName": "الفرع الرئيسي",
            "isActive": true
          }
        ]
      },
      {
        "tenantId": 3,
        "tenantName": "سوبر ماركت الخير",
        "users": [
          {
            "id": 6,
            "name": "كريم المدير",
            "email": "karim@supermarket.com",
            "role": "Admin",
            "password": "Admin@123",
            "tenantId": 3,
            "tenantName": "سوبر ماركت الخير",
            "branchId": 3,
            "branchName": "الفرع الرئيسي",
            "isActive": true
          }
        ]
      },
      {
        "tenantId": 4,
        "tenantName": "مطعم الأمير",
        "users": [
          {
            "id": 7,
            "name": "طارق المدير",
            "email": "tarek@restaurant.com",
            "role": "Admin",
            "password": "Admin@123",
            "tenantId": 4,
            "tenantName": "مطعم الأمير",
            "branchId": 4,
            "branchName": "الفرع الرئيسي",
            "isActive": true
          }
        ]
      }
    ]
  },
  "message": "⚠️ WARNING: This endpoint is for demo purposes only. Never expose passwords in production!"
}
```

---

## 🎯 كيفية الاستخدام

### 1. تسجيل الدخول كـ System Owner
```bash
POST /api/auth/login
{
  "email": "owner@kasserpro.com",
  "password": "Owner@123"
}
```

### 2. الحصول على جميع بيانات الدخول
```bash
GET /api/system/credentials
Authorization: Bearer {token}
```

---

## 📊 البيانات المتاحة

### System Owner
```
Email: owner@kasserpro.com
Password: Owner@123
Role: SystemOwner
```

### Tenant 1: مجزر الأمانة
```
Admin:
  Email: admin@kasserpro.com
  Password: Admin@123

Cashiers:
  Email: mohamed@kasserpro.com
  Password: 123456
  
  Email: ali@kasserpro.com
  Password: 123456
```

### Tenant 2: محل أدوات منزلية
```
Admin:
  Email: samy@homeappliances.com
  Password: Admin@123

Cashiers:
  Email: nour@homeappliances.com
  Password: 123456
  
  Email: hoda@homeappliances.com
  Password: 123456
```

### Tenant 3: سوبر ماركت
```
Admin:
  Email: karim@supermarket.com
  Password: Admin@123

Cashiers:
  Email: fatma@supermarket.com
  Password: 123456
  
  Email: zainab@supermarket.com
  Password: 123456
  
  Email: mariam@supermarket.com
  Password: 123456
```

### Tenant 4: مطعم
```
Admin:
  Email: tarek@restaurant.com
  Password: Admin@123

Cashiers:
  Email: omar@restaurant.com
  Password: 123456
  
  Email: youssef@restaurant.com
  Password: 123456
```

---

## 🎨 عرض البيانات في الـ Frontend

### مثال Component (React)
```typescript
import { useGetCredentialsQuery } from '@/store/api/systemApi';

export function CredentialsPanel() {
  const { data, isLoading } = useGetCredentialsQuery();

  if (isLoading) return <div>Loading...</div>;

  return (
    <div className="credentials-panel">
      <div className="warning">
        ⚠️ هذه البيانات للعرض التوضيحي فقط
      </div>
      
      {data?.data.tenants.map(tenant => (
        <div key={tenant.tenantId} className="tenant-section">
          <h3>{tenant.tenantName}</h3>
          
          <table>
            <thead>
              <tr>
                <th>الاسم</th>
                <th>البريد الإلكتروني</th>
                <th>كلمة المرور</th>
                <th>الدور</th>
              </tr>
            </thead>
            <tbody>
              {tenant.users.map(user => (
                <tr key={user.id}>
                  <td>{user.name}</td>
                  <td>{user.email}</td>
                  <td>
                    <code className="password">{user.password}</code>
                    <button onClick={() => copyToClipboard(user.password)}>
                      📋 نسخ
                    </button>
                  </td>
                  <td>
                    <span className={`role-badge ${user.role}`}>
                      {user.role}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}
```

---

## 🔒 الأمان

### ⚠️ تحذيرات مهمة:

1. **للعرض التوضيحي فقط**
   - لا تستخدم هذا الـ endpoint في الإنتاج
   - كلمات المرور يجب أن تكون مشفرة دائماً

2. **محمي بـ SystemOwner Role**
   - فقط System Owner يمكنه الوصول
   - يتطلب JWT Token صالح

3. **للتطوير والعرض**
   - مفيد للعروض التوضيحية
   - يسهل تجربة الحسابات المختلفة

### ✅ في الإنتاج:
```csharp
// احذف هذا الـ endpoint أو عطّله
#if DEBUG
[HttpGet("credentials")]
[Authorize(Roles = "SystemOwner")]
public async Task<IActionResult> GetAllCredentials()
{
    // ... code
}
#endif
```

---

## 💡 حالات الاستخدام

### 1. العرض على العملاء
```
System Owner يفتح لوحة البيانات
↓
يعرض جميع الحسابات المتاحة
↓
العميل يختار الحساب المناسب
↓
System Owner ينسخ البيانات ويسجل الدخول
```

### 2. التبديل السريع بين الحسابات
```
System Owner → نسخ بيانات Admin
↓
تسجيل الخروج
↓
تسجيل الدخول بحساب Admin
↓
عرض المميزات
```

### 3. العرض على فئات مختلفة
```
عرض على صاحب محل أدوات:
  → نسخ: samy@homeappliances.com / Admin@123
  
عرض على صاحب سوبر ماركت:
  → نسخ: karim@supermarket.com / Admin@123
  
عرض على صاحب مطعم:
  → نسخ: tarek@restaurant.com / Admin@123
```

---

## 📝 ملاحظات

1. **كلمات المرور الافتراضية:**
   - System Owner: `Owner@123`
   - Admins: `Admin@123`
   - Cashiers: `123456`

2. **التنظيم:**
   - البيانات مرتبة حسب Tenant
   - ثم حسب Role (Admin أولاً)

3. **المعلومات المعروضة:**
   - الاسم
   - البريد الإلكتروني
   - كلمة المرور (plain text)
   - الدور
   - اسم المحل
   - اسم الفرع

---

## ✅ الخلاصة

**الآن System Owner يمكنه:**
- ✅ عرض جميع بيانات الدخول
- ✅ نسخ كلمات المرور بسهولة
- ✅ التبديل السريع بين الحسابات
- ✅ عرض التطبيق على أي فئة

**مثالي للعروض التوضيحية والتطوير!**
