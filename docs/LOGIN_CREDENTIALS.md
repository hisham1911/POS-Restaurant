# 🔐 بيانات تسجيل الدخول - TajerPro

## ملاحظة مهمة

قاعدة البيانات الحالية تستخدم **ButcherDataSeeder** وليس DbInitializer.
البيانات التالية هي البيانات الفعلية الموجودة في النظام حالياً.

---

## 1️⃣ System Owner (مالك النظام) ✅

- **الاسم:** System Owner
- **البريد الإلكتروني:** `owner@kasserpro.com`
- **كلمة المرور:** `Owner@123`
- **الدور:** SystemOwner
- **الصلاحيات:** جميع الصلاحيات + إدارة الشركات (Tenants) + إدارة المستخدمين
- **الحالة:** نشط ✅

### 🎯 كيفية الوصول إلى صفحة إدارة المستخدمين:

1. اذهب إلى http://localhost:3000
2. سجل الدخول باستخدام:
   - البريد: `owner@kasserpro.com`
   - كلمة المرور: `Owner@123`
3. ستنتقل تلقائياً إلى `/owner/tenants`
4. من الـ Sidebar، اختر **"إدارة المستخدمين"** أو اذهب مباشرة إلى http://localhost:3000/owner/users
5. ستشاهد جميع المستخدمين (14 مستخدم) مع إمكانية:
   - تعديل بيانات المستخدم (الاسم، البريد، الهاتف)
   - إعادة تعيين كلمة المرور
   - تفعيل/تعطيل المستخدم

---

## 2️⃣ Admin (مدير النظام) ✅

- **الاسم:** أحمد المدير
- **البريد الإلكتروني:** `admin@kasserpro.com`
- **كلمة المرور:** `Admin@123`
- **الدور:** Admin
- **الصلاحيات:** جميع الصلاحيات تلقائياً
- **الحالة:** نشط ✅

---

## 3️⃣ Cashiers (الكاشيرز) ✅

### محمد الكاشير

- **الاسم:** محمد الكاشير
- **البريد الإلكتروني:** `mohamed@kasserpro.com`
- **كلمة المرور:** `123456`
- **الدور:** Cashier
- **الصلاحيات الحالية:** CashRegisterView فقط
- **الحالة:** نشط ✅

### علي الكاشير

- **الاسم:** علي الكاشير
- **البريد الإلكتروني:** `ali@kasserpro.com`
- **كلمة المرور:** `123456`
- **الدور:** Cashier
- **الصلاحيات الحالية:** CashRegisterView فقط
- **الحالة:** نشط ✅

---

## 📝 ملاحظات

1. **الكاشيرز الموجودون في architecture.md خاطئون:**
   - ❌ ahmed@kasserpro.com (غير موجود)
   - ❌ fatima@kasserpro.com (غير موجود)
   - ❌ mahmoud@kasserpro.com (غير موجود)

2. **الكاشيرز الصحيحون:**
   - ✅ mohamed@kasserpro.com
   - ✅ ali@kasserpro.com

3. **SystemOwner تم إضافته** إلى ButcherDataSeeder وسيتم إنشاؤه في قاعدة البيانات الجديدة

4. **الصلاحيات الافتراضية للكاشيرز الجدد:**
   - PosSell
   - OrdersView

5. **Admin والSystemOwner** يحصلون على جميع الصلاحيات تلقائياً ولا يمكن تعديل صلاحياتهم

---

## 🔗 روابط سريعة

- **Frontend:** http://localhost:3000
- **Backend API:** http://localhost:5243
- **Swagger:** http://localhost:5243/swagger

---

## 🧪 للاختبار

```bash
# تسجيل دخول Admin
curl -X POST http://localhost:5243/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@kasserpro.com","password":"Admin@123"}'

# تسجيل دخول Cashier
curl -X POST http://localhost:5243/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"mohamed@kasserpro.com","password":"123456"}'
```
