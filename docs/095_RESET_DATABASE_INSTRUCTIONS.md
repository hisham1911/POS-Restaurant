# 🔧 تعليمات إعادة تعيين قاعدة البيانات

**المشكلة**: قاعدة البيانات مقفلة من Backend

---

## ✅ الخطوات المطلوبة (يدوياً)

### 1. إيقاف Backend

إذا كان Backend يعمل في terminal:
```
اضغط Ctrl+C
```

إذا كان يعمل في الخلفية:
```powershell
# ابحث عن العملية
Get-Process | Where-Object {$_.ProcessName -like "*dotnet*"}

# أوقف العملية (استبدل PID برقم العملية)
Stop-Process -Id PID -Force
```

### 2. حذف قاعدة البيانات

```powershell
cd src\KasserPro.API
del kasserpro.db
del kasserpro.db-shm
del kasserpro.db-wal
```

### 3. تطبيق Migrations

```powershell
dotnet ef database update
```

**يجب أن ترى**:
```
Applying migration '20260209122732_EnhanceShiftManagement'.
Done.
```

### 4. تشغيل Backend

```powershell
dotnet run
```

**يجب أن ترى**:
```
info: KasserPro.API[0]
      Database initialized successfully
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5243
```

---

## 🎯 البديل: استخدام Script

إذا أوقفت Backend، يمكنك استخدام:

```powershell
.\reset-database.ps1
```

---

## ✅ التحقق من النجاح

1. افتح http://localhost:3000
2. سجل دخول: `admin@kasserpro.com` / `Admin@123`
3. اذهب إلى "الوردية"
4. افتح وردية جديدة
5. افتح Developer Tools (F12) → Network
6. ابحث عن `/shifts/current`
7. تحقق من وجود الحقول الجديدة:
   - `lastActivityAt`
   - `inactiveHours`
   - `isForceClosed`
   - `isHandedOver`
   - `durationHours`
   - `durationMinutes`

---

## 📋 Checklist

- [ ] أوقفت Backend
- [ ] حذفت قاعدة البيانات
- [ ] طبقت Migrations
- [ ] رأيت رسالة "Applying migration '20260209122732_EnhanceShiftManagement'"
- [ ] شغلت Backend
- [ ] رأيت رسالة "Database initialized successfully"
- [ ] فتحت Frontend
- [ ] سجلت دخول
- [ ] فتحت وردية
- [ ] تحققت من الحقول الجديدة

---

## 🐛 إذا واجهت مشاكل

### المشكلة: لا يمكن حذف قاعدة البيانات
**الحل**: Backend لا يزال يعمل. أوقفه أولاً.

### المشكلة: Migration لم تُطبق
**الحل**: 
```powershell
dotnet ef migrations list
# إذا لم تظهر EnhanceShiftManagement
dotnet ef migrations add EnhanceShiftManagement
dotnet ef database update
```

### المشكلة: Backend لا يبدأ
**الحل**:
```powershell
dotnet build
# تحقق من الأخطاء
```

---

## ⚠️ ملاحظة مهمة

**يجب إيقاف Backend قبل حذف قاعدة البيانات**

قاعدة البيانات SQLite تُقفل عند الاستخدام، لذلك لا يمكن حذفها أثناء عمل Backend.

---

**الوقت المتوقع**: 5 دقائق
