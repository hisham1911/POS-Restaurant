# مشكلة تسجيل الدخول من الشبكة - Network Login Issue

**التاريخ:** 25 فبراير 2026  
**الحالة:** 🔍 قيد التشخيص

---

## 📋 ملخص المشكلة

1. ✅ الصفحة تفتح على `http://192.168.1.5:5243`
2. ❌ تسجيل الدخول يفشل
3. ❌ Backend لا يستمع على المنفذ 5243

---

## 🔍 التشخيص

### الخطوة 1: فحص التغييرات
```
✅ appsettings.json - أضفنا "AllowedOrigins": ["*"]
✅ launchSettings.json - غيرنا إلى "http://0.0.0.0:5243"
✅ Firewall Rule - تمت الإضافة
```

### الخطوة 2: فحص Backend
```powershell
PS> Get-Process dotnet
# النتيجة: 2 processes تعمل

PS> netstat -ano | findstr ":5243"
# النتيجة: لا يوجد شيء يستمع!
```

### الخطوة 3: فحص Logs
```
2026-02-25 23:31:02 [INF] Daily backup scheduler started
# لا يوجد "Now listening on" في الـ logs!
```

---

## 🎯 السبب المحتمل

Backend يبدأ لكن لا يصل إلى `app.Run()`. الأسباب المحتملة:

1. **Exception أثناء Startup** - لكن لا يوجد في الـ logs
2. **Process يتوقف مباشرة** - لكن الـ processes لا تزال تعمل
3. **launchSettings.json لا يُستخدم** - عند تشغيل `dotnet run` بدون profile

---

## 💡 الحل

المشكلة: عند تشغيل `dotnet run` بدون تحديد profile، يستخدم الإعدادات الافتراضية وليس `launchSettings.json`!

### الحل الصحيح:

**الطريقة 1: استخدام Environment Variable**
```powershell
$env:ASPNETCORE_URLS = "http://0.0.0.0:5243"
dotnet run
```

**الطريقة 2: استخدام --urls**
```powershell
dotnet run --urls "http://0.0.0.0:5243"
```

**الطريقة 3: استخدام --launch-profile**
```powershell
dotnet run --launch-profile http
```

---

## ✅ الإصلاح النهائي

سنستخدم الطريقة 1 (Environment Variable) لأنها الأبسط:

```powershell
# أوقف Backend الحالي
Get-Process dotnet | Stop-Process -Force

# شغل Backend مع الإعدادات الصحيحة
$env:ASPNETCORE_URLS = "http://0.0.0.0:5243"
cd backend\KasserPro.API
dotnet run
```

---

## 🧪 التحقق

بعد التشغيل، يجب أن ترى:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5243
```

ثم اختبر:
```powershell
# Test 1: Localhost
curl http://localhost:5243/api/system/health

# Test 2: Network IP
curl http://192.168.1.5:5243/api/system/health

# Test 3: Login
.\test-login-network.ps1
```

---

## 📝 ملاحظات

- `launchSettings.json` يُستخدم فقط في Visual Studio أو عند تحديد `--launch-profile`
- عند تشغيل `dotnet run` مباشرة، يجب تحديد URLs عبر:
  - Environment Variable: `ASPNETCORE_URLS`
  - Command line: `--urls`
  - appsettings.json: `"Kestrel": { "Endpoints": {...} }`

---

**الحالة:** جاهز للتطبيق
