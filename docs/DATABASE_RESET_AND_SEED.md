# إعادة تعيين قاعدة البيانات وإنشاء Seed Data جديدة

**التاريخ**: 9 فبراير 2026

---

## 🔴 المشكلة

الميزات الجديدة لا تعمل بشكل صحيح. يجب إعادة تعيين قاعدة البيانات وإنشاء seed data جديدة.

---

## ✅ الحل

### الخطوة 1: حذف قاعدة البيانات الحالية

```bash
# في مجلد src/KasserPro.API
del kasserpro.db
del kasserpro.db-shm
del kasserpro.db-wal
```

### الخطوة 2: تطبيق جميع Migrations

```bash
cd src/KasserPro.API
dotnet ef database update
```

### الخطوة 3: تشغيل Backend لإنشاء Seed Data

```bash
cd src/KasserPro.API
dotnet run
```

---

## 📊 Seed Data الجديدة

سيتم إنشاء البيانات التالية تلقائياً:

### 1. Tenant & Branch
- **Tenant**: KasserPro Demo
- **Branch**: الفرع الرئيسي

### 2. Users
| الاسم | Email | Password | Role |
|------|-------|----------|------|
| Admin | admin@kasserpro.com | Admin@123 | Admin |
| أحمد محمد | ahmed@kasserpro.com | 123456 | Cashier |
| فاطمة علي | fatima@kasserpro.com | 123456 | Cashier |

### 3. Categories
- مشروبات ساخنة
- مشروبات باردة
- مأكولات
- حلويات

### 4. Products
- قهوة (15 ج.م)
- شاي (10 ج.م)
- عصير برتقال (20 ج.م)
- ساندويتش (25 ج.م)
- كيك (30 ج.م)

### 5. Customers
- عميل نقدي (Cash Customer)
- محمد أحمد
- سارة علي

### 6. Suppliers
- مورد المشروبات
- مورد المأكولات

### 7. Expense Categories
- رواتب
- إيجار
- كهرباء
- صيانة
- مشتريات

---

## 🔧 التحقق من النجاح

بعد تطبيق الخطوات:

1. ✅ تسجيل الدخول بـ admin@kasserpro.com / Admin@123
2. ✅ فتح وردية جديدة
3. ✅ التحقق من وجود الحقول الجديدة:
   - LastActivityAt
   - InactiveHours
   - IsForceClosed
   - IsHandedOver
   - DurationHours
   - DurationMinutes

4. ✅ اختبار الميزات:
   - تسليم الوردية
   - إغلاق بالقوة (Admin)
   - قائمة الورديات المفتوحة
   - استعادة بعد التعطل

---

## ⚠️ ملاحظات مهمة

1. **سيتم حذف جميع البيانات الحالية**
2. **تأكد من عمل backup إذا كنت تحتاج البيانات القديمة**
3. **Migration الجديدة تحتوي على جميع الحقول المطلوبة**
4. **Seed Data منطقية ومتطابقة مع التحديثات**

---

## 🚀 الخطوات التفصيلية

### 1. إيقاف Backend (إذا كان يعمل)
```bash
# اضغط Ctrl+C في terminal الخاص بالـ Backend
```

### 2. حذف قاعدة البيانات
```bash
cd src/KasserPro.API
del kasserpro.db
del kasserpro.db-shm
del kasserpro.db-wal
```

### 3. تطبيق Migrations
```bash
dotnet ef database update
```

**المتوقع**:
```
Build started...
Build succeeded.
Applying migration '20260106200546_InitialCreate'.
Applying migration '20260107005426_AddTenantBranchAudit'.
Applying migration '20260107110419_FixEncoding'.
Applying migration '20260107110814_AddOrderSnapshots'.
Applying migration '20260107112709_AddDynamicTaxFields'.
Applying migration '20260107180035_AddUserNameToAuditLog'.
Applying migration '20260107202837_AddShiftRowVersion'.
Applying migration '20260107210624_AddTenantTaxSettings'.
Applying migration '20260107221702_ConvertOrderTypeToEnum'.
Applying migration '20260107221916_ConvertOrderTypeData'.
Applying migration '20260108120344_SellableV1_Customers_Inventory_Refunds'.
Applying migration '20260108121706_SellableV1_Customer_IsActive'.
Applying migration '20260108122449_SellableV1_Tenant_AllowNegativeStock'.
Applying migration '20260108203101_EnableTrackInventoryForAllProducts'.
Applying migration '20260126204530_AddSuppliers'.
Applying migration '20260128151428_AddPurchaseInvoiceFeature'.
Applying migration '20260129144848_AddExpensesAndCashRegister'.
Applying migration '20260209111456_AddReceiptSettings'.
Applying migration '20260209113437_AddReceiptCustomWidth'.
Applying migration '20260209114810_AddCustomerCreditTracking'.
Applying migration '20260209115641_AddReceiptShowCustomerNameAndLogo'.
Applying migration '20260209120140_AddReceiptCustomerLogo'.
Applying migration '20260209122732_EnhanceShiftManagement'. ← هذه المهمة!
Done.
```

### 4. تشغيل Backend
```bash
dotnet run
```

**المتوقع**:
```
info: KasserPro.API[0]
      Starting database initialization...
info: KasserPro.API[0]
      Database initialized successfully
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5243
```

### 5. التحقق من Frontend
```bash
# في terminal جديد
cd client
npm run dev
```

### 6. اختبار الميزات
1. افتح http://localhost:3000
2. سجل دخول بـ admin@kasserpro.com / Admin@123
3. اذهب إلى صفحة الوردية
4. افتح وردية جديدة
5. جرب الميزات الجديدة

---

## 🐛 إذا واجهت مشاكل

### المشكلة: Migration لم تُطبق
```bash
# تحقق من Migrations المطبقة
dotnet ef migrations list

# إذا لم تظهر EnhanceShiftManagement، أعد إنشاءها
dotnet ef migrations remove
dotnet ef migrations add EnhanceShiftManagement
dotnet ef database update
```

### المشكلة: Backend لا يبدأ
```bash
# تحقق من الأخطاء
dotnet build
dotnet run --verbosity detailed
```

### المشكلة: Frontend يعرض أخطاء
```bash
# امسح cache
cd client
rm -rf node_modules/.vite
npm run dev
```

---

## ✅ Checklist النهائي

- [ ] حذف قاعدة البيانات القديمة
- [ ] تطبيق جميع Migrations
- [ ] تشغيل Backend بنجاح
- [ ] Seed Data تم إنشاؤها
- [ ] تسجيل الدخول يعمل
- [ ] فتح وردية يعمل
- [ ] الحقول الجديدة موجودة
- [ ] تسليم الوردية يعمل
- [ ] إغلاق بالقوة يعمل (Admin)
- [ ] قائمة الورديات المفتوحة تعمل
- [ ] استعادة بعد التعطل تعمل

---

**بعد تطبيق هذه الخطوات، يجب أن تعمل جميع الميزات بشكل صحيح!** ✅
