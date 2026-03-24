# 🎯 Demo Instructions - System Owner Tenant Creation

## ✅ التطبيق شغال الآن!

### 🌐 URLs
- **Backend API**: http://localhost:5243
- **Frontend**: http://localhost:3000

---

## 🔐 Test Credentials

### SystemOwner (لإنشاء Tenants جديدة)
```
Email: owner@kasserpro.com
Password: Owner@123
```

### Admin (للإدارة العادية)
```
Email: admin@kasserpro.com
Password: Admin@123
```

### Cashier (للكاشير)
```
Email: ahmed@kasserpro.com
Password: 123456
```

---

## 📋 خطوات التجربة

### 1️⃣ تسجيل الدخول كـ SystemOwner

1. افتح المتصفح على: http://localhost:3000
2. سجل دخول بـ:
   - Email: `owner@kasserpro.com`
   - Password: `Owner@123`

### 2️⃣ الوصول لصفحة إنشاء Tenant

بعد تسجيل الدخول، اذهب مباشرة إلى:
```
http://localhost:3000/owner/tenants
```

أو يمكنك إضافة رابط في الـ navigation menu لاحقاً.

### 3️⃣ إنشاء Tenant جديد

املأ الفورم:
- **اسم الشركة**: مثال: "مطعم الأمل"
- **اسم الفرع الرئيسي**: مثال: "الفرع الرئيسي"
- **البريد الإلكتروني للمدير**: مثال: "admin@amal.com"
- **كلمة المرور**: مثال: "SecurePass123"

اضغط "إنشاء الشركة"

### 4️⃣ التحقق من النجاح

✅ ستظهر رسالة نجاح: "تم إنشاء الشركة بنجاح"

✅ الفورم سيتم مسحه تلقائياً

✅ يمكنك إنشاء tenant آخر

### 5️⃣ تسجيل الدخول بالـ Tenant الجديد

1. اضغط Logout
2. سجل دخول بالبيانات الجديدة:
   - Email: `admin@amal.com`
   - Password: `SecurePass123`
3. ستجد نفسك في tenant منفصل تماماً!

---

## 🧪 اختبار الـ API مباشرة (Postman/curl)

### Get JWT Token (Login as SystemOwner)

```bash
POST http://localhost:5243/api/auth/login
Content-Type: application/json

{
  "email": "owner@kasserpro.com",
  "password": "Owner@123"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "id": 5,
      "name": "System Owner",
      "email": "owner@kasserpro.com",
      "role": "SystemOwner"
    }
  }
}
```

### Create New Tenant

```bash
POST http://localhost:5243/api/system/tenants
Authorization: Bearer <your_token_here>
Content-Type: application/json

{
  "tenantName": "مطعم الأمل",
  "adminEmail": "admin@amal.com",
  "adminPassword": "SecurePass123",
  "branchName": "الفرع الرئيسي"
}
```

**Expected Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "tenantId": 2,
    "tenantName": "مطعم الأمل",
    "tenantSlug": "مطعم-الأمل",
    "branchId": 3,
    "branchName": "الفرع الرئيسي",
    "adminUserId": 6,
    "adminEmail": "admin@amal.com"
  },
  "message": "تم إنشاء الشركة بنجاح"
}
```

---

## 🔒 Security Features Demonstrated

### ✅ Role-Based Authorization
- جرب الدخول على `/owner/tenants` بـ Admin أو Cashier
- سيتم redirect تلقائياً إلى `/pos`

### ✅ Email Uniqueness
- جرب إنشاء tenant بنفس الـ email مرتين
- سيظهر خطأ: "البريد الإلكتروني مستخدم بالفعل"

### ✅ Form Validation
- جرب ترك حقل فاضي
- جرب كتابة email غير صحيح
- جرب password أقل من 6 أحرف
- HTML5 validation سيمنعك

### ✅ Transaction Safety
- إذا حصل خطأ في أي خطوة، كل العملية ترجع rollback
- لن يتم إنشاء tenant ناقص

---

## 📊 What Happens Behind the Scenes

عند إنشاء tenant جديد:

1. ✅ **Validation**: Email uniqueness check
2. ✅ **Slug Generation**: "مطعم الأمل" → "مطعم-الأمل"
3. ✅ **Transaction Start**: Begin atomic operation
4. ✅ **Create Tenant**: With default settings (Tax 14%, EGP, etc.)
5. ✅ **Create Branch**: Code "MAIN", linked to tenant
6. ✅ **Create Admin User**: Password hashed with BCrypt, Role=Admin
7. ✅ **Commit**: All or nothing
8. ✅ **Response**: Return complete tenant info

---

## 🎨 UI Features

### Form
- Clean, accessible design with Card layout
- Real-time HTML5 validation
- Loading state during submission
- Clear error/success messages
- Auto-reset on success

### Security
- Password field (hidden input)
- Email validation
- Required field indicators
- Min/max length enforcement

---

## 🐛 Troubleshooting

### Backend not starting?
```bash
# Set JWT Key first
$env:Jwt__Key = [Convert]::ToBase64String((1..40 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])

# Then run
dotnet run --project src/KasserPro.API
```

### Frontend not starting?
```bash
cd client
npm install
npm run dev
```

### Can't access /owner/tenants?
- تأكد إنك مسجل دخول كـ SystemOwner
- Email: owner@kasserpro.com
- Password: Owner@123

### API returns 401 Unauthorized?
- تأكد من الـ JWT token في الـ Authorization header
- تأكد إن الـ token لـ SystemOwner مش Admin

---

## 📝 Next Steps

بعد التجربة، يمكنك:

1. ✅ إضافة رابط في الـ navigation menu للـ SystemOwner
2. ✅ إنشاء صفحة لعرض قائمة الـ Tenants
3. ✅ إضافة إمكانية تعطيل/تفعيل Tenant
4. ✅ إضافة statistics dashboard للـ SystemOwner
5. ✅ إضافة email notification للـ admin الجديد

---

## ✨ Features Implemented

✅ SystemOwner role
✅ Secure tenant creation endpoint
✅ Transaction-wrapped atomic operations
✅ Password hashing with BCrypt
✅ Email uniqueness validation
✅ Slug generation with collision handling
✅ Frontend form with validation
✅ Role-based route protection
✅ RTK Query API integration
✅ Error handling and display
✅ Success feedback
✅ Auto-reset form

---

**🎉 Enjoy testing the new feature!**
