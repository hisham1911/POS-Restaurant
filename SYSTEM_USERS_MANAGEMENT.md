# 👥 نظام إدارة المستخدمين - System Users Management

## 📋 نظرة عامة

تم إنشاء صفحة متكاملة لإدارة جميع مستخدمي النظام من قبل **مالك النظام (System Owner)** فقط.

---

## ✨ الميزات

### 1. عرض جميع المستخدمين
- عرض قائمة بجميع المستخدمين (14 مستخدم حالياً)
- تجميع المستخدمين حسب الشركة (Tenant)
- عرض معلومات كاملة لكل مستخدم:
  - الاسم والبريد الإلكتروني والهاتف
  - الدور (System Owner / Admin / Cashier)
  - الشركة والفرع
  - حالة التفعيل (نشط / غير نشط)

### 2. إحصائيات سريعة
- إجمالي عدد المستخدمين
- عدد المستخدمين النشطين
- عدد المستخدمين غير النشطين

### 3. البحث والتصفية
- البحث بالاسم أو البريد الإلكتروني أو اسم الشركة
- تصفية فورية أثناء الكتابة

### 4. تعديل بيانات المستخدم
- تعديل الاسم والبريد الإلكتروني والهاتف
- حفظ التغييرات مباشرة

### 5. إعادة تعيين كلمة المرور
- إعادة تعيين كلمة المرور لأي مستخدم
- تأكيد العملية قبل التنفيذ

### 6. تفعيل/تعطيل المستخدم
- تفعيل أو تعطيل أي مستخدم (ما عدا System Owner)
- تحديث الحالة فوراً

---

## 🚀 كيفية الوصول

### الخطوة 1: تسجيل الدخول
```
البريد: owner@kasserpro.com
كلمة المرور: Owner@123
```

### الخطوة 2: الانتقال إلى صفحة إدارة المستخدمين
بعد تسجيل الدخول، ستنتقل تلقائياً إلى `/owner/tenants`

من هناك:
- **الطريقة 1:** اضغط على "إدارة المستخدمين" من الـ Sidebar
- **الطريقة 2:** اذهب مباشرة إلى http://localhost:3000/owner/users

---

## 📊 البيانات الحالية

### المستخدمون الموجودون (14 مستخدم)

#### System Owner (1)
- System Owner (owner@kasserpro.com)

#### Admins (4)
- أحمد المدير (admin@kasserpro.com) - مجزر الأمانة
- محمد المدير (admin2@kasserpro.com) - مطعم الفرح
- علي المدير (admin3@kasserpro.com) - محل الملابس
- فاطمة المديرة (admin4@kasserpro.com) - محل الأحذية

#### Cashiers (9)
- محمد الكاشير (cashier1@kasserpro.com) - مجزر الأمانة
- علي الكاشير (cashier2@kasserpro.com) - مجزر الأمانة
- نور الكاشير (cashier3@kasserpro.com) - مطعم الفرح
- فاطمة الكاشير (cashier4@kasserpro.com) - مطعم الفرح
- عمر الكاشير (cashier5@kasserpro.com) - محل الملابس
- ليلى الكاشيرة (cashier6@kasserpro.com) - محل الملابس
- سارة الكاشيرة (cashier7@kasserpro.com) - محل الأحذية
- خالد الكاشير (cashier8@kasserpro.com) - محل الأحذية
- أحمد الكاشير (ahmed@kasserpro.com) - مجزر الأمانة

---

## 🔧 التفاصيل التقنية

### Backend Endpoints

#### GET /api/system/users
الحصول على جميع المستخدمين
```bash
curl -X GET http://localhost:5243/api/system/users \
  -H "Authorization: Bearer {token}"
```

**الاستجابة:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "System Owner",
      "email": "owner@kasserpro.com",
      "phone": null,
      "role": "SystemOwner",
      "tenantId": null,
      "tenantName": "System",
      "branchId": null,
      "branchName": null,
      "isActive": true,
      "createdAt": "2026-03-09T13:28:13.7461957",
      "updatedAt": null
    }
  ]
}
```

#### PUT /api/system/users/{userId}
تعديل بيانات المستخدم
```bash
curl -X PUT http://localhost:5243/api/system/users/2 \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "أحمد محمد",
    "email": "ahmed.new@kasserpro.com",
    "phone": "01012345678"
  }'
```

#### PATCH /api/system/users/{userId}/toggle-status
تفعيل/تعطيل المستخدم
```bash
curl -X PATCH http://localhost:5243/api/system/users/2/toggle-status \
  -H "Authorization: Bearer {token}"
```

#### POST /api/system/users/{userId}/reset-password
إعادة تعيين كلمة المرور
```bash
curl -X POST http://localhost:5243/api/system/users/2/reset-password \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "newPassword": "NewPassword@123"
  }'
```

### Frontend Components

#### صفحة إدارة المستخدمين
- **المسار:** `frontend/src/pages/system/SystemUsersPage.tsx`
- **الحجم:** ~400 سطر
- **المكونات المستخدمة:**
  - Card (عرض الإحصائيات)
  - Input (البحث)
  - Modal (تعديل البيانات وإعادة تعيين كلمة المرور)
  - Button (الإجراءات)
  - Loading (حالة التحميل)

#### RTK Query API
- **المسار:** `frontend/src/api/systemUsersApi.ts`
- **الـ Endpoints:**
  - `useGetAllSystemUsersQuery()` - جلب جميع المستخدمين
  - `useUpdateSystemUserMutation()` - تعديل المستخدم
  - `useToggleSystemUserStatusMutation()` - تفعيل/تعطيل
  - `useResetSystemUserPasswordMutation()` - إعادة تعيين كلمة المرور

### Backend Controller
- **المسار:** `backend/KasserPro.API/Controllers/SystemController.cs`
- **الـ Methods:**
  - `GetAllUsers()` - جلب جميع المستخدمين
  - `UpdateUser()` - تعديل المستخدم
  - `ToggleUserStatus()` - تفعيل/تعطيل
  - `ResetUserPassword()` - إعادة تعيين كلمة المرور

---

## 🔐 الأمان والصلاحيات

### التحكم في الوصول
- ✅ **System Owner فقط** يمكنه الوصول إلى صفحة إدارة المستخدمين
- ✅ جميع الطلبات تتطلب توكن صحيح
- ✅ لا يمكن تعطيل System Owner

### التحقق من الصلاحيات
```csharp
[Authorize(Roles = "SystemOwner")]
public async Task<IActionResult> GetAllUsers()
```

---

## 📱 واجهة المستخدم

### الجدول الرئيسي
يعرض جميع المستخدمين مع الأعمدة التالية:
- الاسم
- البريد الإلكتروني
- الهاتف
- الدور (مع ألوان مختلفة)
- الفرع
- الحالة (نشط/غير نشط)
- الإجراءات (تعديل، إعادة تعيين كلمة المرور، تفعيل/تعطيل)

### الإحصائيات
- عرض ثلاث بطاقات في الأعلى:
  - إجمالي المستخدمين (أزرق)
  - المستخدمين النشطين (أخضر)
  - المستخدمين غير النشطين (أحمر)

### البحث
- حقل بحث في الأعلى
- تصفية فورية أثناء الكتابة
- البحث في الاسم والبريد واسم الشركة

---

## 🧪 الاختبار

### اختبار يدوي
1. سجل الدخول كـ System Owner
2. انتقل إلى صفحة إدارة المستخدمين
3. تحقق من ظهور جميع المستخدمين (14)
4. جرب البحث عن مستخدم
5. جرب تعديل بيانات مستخدم
6. جرب إعادة تعيين كلمة المرور
7. جرب تفعيل/تعطيل مستخدم

### اختبار API
```bash
# تسجيل الدخول
TOKEN=$(curl -X POST http://localhost:5243/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"owner@kasserpro.com","password":"Owner@123"}' \
  | jq -r '.data.accessToken')

# جلب جميع المستخدمين
curl -X GET http://localhost:5243/api/system/users \
  -H "Authorization: Bearer $TOKEN"
```

---

## 📝 الملاحظات

### ✅ ما تم إنجازه
- ✅ Backend endpoints متكاملة
- ✅ Frontend page مع واجهة جميلة
- ✅ RTK Query API configuration
- ✅ البحث والتصفية
- ✅ تعديل البيانات
- ✅ إعادة تعيين كلمة المرور
- ✅ تفعيل/تعطيل المستخدمين
- ✅ الإحصائيات السريعة
- ✅ التحكم في الوصول (System Owner فقط)

### 🔄 التحسينات المستقبلية
- [ ] إضافة تصدير المستخدمين إلى Excel
- [ ] إضافة فلاتر متقدمة (حسب الدور، الحالة، إلخ)
- [ ] إضافة تاريخ آخر تسجيل دخول
- [ ] إضافة سجل تدقيق (Audit Log) للتغييرات
- [ ] إضافة إرسال بريد إلكتروني عند إعادة تعيين كلمة المرور
- [ ] إضافة تصريح دفعي (Bulk Actions)

---

## 🔗 الروابط المهمة

- **صفحة إدارة المستخدمين:** http://localhost:3000/owner/users
- **Backend API:** http://localhost:5243/api/system/users
- **Swagger:** http://localhost:5243/swagger
- **بيانات تسجيل الدخول:** `LOGIN_CREDENTIALS.md`

---

## 📞 الدعم

إذا واجهت أي مشاكل:
1. تأكد من تسجيل الدخول كـ System Owner
2. تحقق من أن Backend يعمل على المنفذ 5243
3. تحقق من أن Frontend يعمل على المنفذ 3000
4. افتح أدوات المطور (F12) وتحقق من الأخطاء
5. تحقق من ملفات السجل في `backend/KasserPro.API/logs/`
