# 🖨️ دليل سريع: ربط تطبيق الطابعة

## الفكرة ببساطة

```
المتصفح (POS) → Backend (VPS) → Bridge App (جهاز الكاشير) → الطابعة
```

التطبيق يشتغل على جهاز الكاشير ويستمع للأوامر من السيرفر عن طريق SignalR.

---

## 🚀 خطوات التشغيل السريعة

### 1️⃣ بناء التطبيق (على جهازك الحالي)

```powershell
# استخدم السكريبت الجاهز (أسهل طريقة)
.\build-bridge-simple.ps1

# أو يدوياً:
cd backend\KasserPro.BridgeApp
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath KasserPro-Bridge.zip
cd ..\..
```

### 2️⃣ نقل التطبيق لجهاز الكاشير

1. انسخ ملف `KasserPro-Bridge.zip` لجهاز الكاشير
2. فك الضغط في مجلد مثل: `C:\KasserPro\Bridge`

### 3️⃣ إعداد التطبيق (أول مرة)

شغّل `KasserPro.BridgeApp.exe` وهيطلب منك:

```
Backend URL: http://168.231.106.139:5243
API Key: [سيتم إنشاؤه تلقائياً]
Device ID: [سيتم إنشاؤه تلقائياً]
Branch ID: [اختياري - اتركه فاضي للفرع الافتراضي]
```

الإعدادات تُحفظ في:
```
%AppData%\KasserPro\settings.json
```

### 4️⃣ تسجيل الجهاز في Backend

بعد تشغيل Bridge App، لازم تسجل الجهاز في Backend:

```powershell
# من جهازك المحلي
$deviceId = "الـ Device ID اللي ظهر في Bridge App"
$apiKey = "الـ API Key اللي ظهر في Bridge App"
$branchId = "1" # أو ID الفرع المطلوب

curl -X POST http://168.231.106.139:5243/api/devices/register `
  -H "Content-Type: application/json" `
  -d "{\"deviceId\":\"$deviceId\",\"apiKey\":\"$apiKey\",\"branchId\":$branchId,\"deviceType\":\"Printer\"}"
```

---

## 📝 ملف الإعدادات

الملف موجود في: `%AppData%\KasserPro\settings.json`

```json
{
  "DeviceId": "abc123-xyz789",
  "BackendUrl": "http://168.231.106.139:5243",
  "ApiKey": "your-api-key-here",
  "BranchId": "1",
  "DefaultPrinterName": "POS-80"
}
```

---

## 🔌 كيف يشتغل؟

### من Frontend (عند إتمام البيع)

```typescript
// في POS Page
const handleCompleteSale = async () => {
  // إتمام البيع
  const order = await completeOrder(orderData);
  
  // Backend تلقائياً يرسل أمر الطباعة للـ Bridge App
  // لا تحتاج كود إضافي!
};
```

### Backend يرسل للـ Bridge

```csharp
// في OrdersController
await _deviceCommandService.SendPrintCommandAsync(
    deviceId: "abc123-xyz789",
    order: order
);
```

### Bridge App يستقبل ويطبع

```csharp
// في SignalRClientService
_hubConnection.On<PrintCommandDto>("PrintReceipt", (command) => {
    // طباعة الفاتورة
    await _printerService.PrintReceiptAsync(command);
});
```

---

## ✅ اختبار الاتصال

### 1. تحقق من حالة Bridge App

شوف نافذة التطبيق:
```
✅ Connected to Backend
✅ Device ID: abc123-xyz789
✅ Printer: POS-80 (Ready)
```

### 2. اختبار طباعة من Backend

```powershell
# اختبار بسيط
curl -X POST http://168.231.106.139:5243/api/test/print `
  -H "Content-Type: application/json" `
  -d "{\"deviceId\":\"abc123-xyz789\",\"message\":\"Test Print\"}"
```

### 3. شوف Logs

```
%AppData%\KasserPro\logs\bridge-app.log
```

---

## 🔧 حل المشاكل الشائعة

### المشكلة: Bridge App مش بيتصل

**الحل:**
1. تأكد من Backend شغال: `http://168.231.106.139:5243/health`
2. تأكد من Firewall مفتوح على Port 5243
3. شوف Logs في `%AppData%\KasserPro\logs\`

### المشكلة: الطابعة مش بتطبع

**الحل:**
1. تأكد من الطابعة متصلة (USB/Network)
2. تأكد من Driver مثبت
3. جرب طباعة تجريبية من Windows
4. تأكد من اسم الطابعة صحيح في `settings.json`

### المشكلة: SignalR Disconnected

**الحل:**
- التطبيق بيعيد الاتصال تلقائياً كل 10 ثواني
- شوف Logs للتفاصيل

---

## 🎯 نصائح مهمة

1. ✅ شغّل Bridge App مع بداية Windows (حطه في Startup)
2. ✅ احتفظ بنسخة من `settings.json`
3. ✅ اختبر الطباعة قبل بداية كل وردية
4. ✅ راقب Logs بانتظام
5. ✅ استخدم Device ID فريد لكل كاشير

---

## 📞 الملفات المهمة

| الملف | الموقع |
|------|--------|
| التطبيق | `C:\KasserPro\Bridge\KasserPro.BridgeApp.exe` |
| الإعدادات | `%AppData%\KasserPro\settings.json` |
| Logs | `%AppData%\KasserPro\logs\bridge-app.log` |

---

## 🔄 التحديثات

لتحديث Bridge App:
1. أوقف التطبيق
2. احذف المجلد القديم
3. فك ضغط النسخة الجديدة
4. شغّل التطبيق (الإعدادات محفوظة في AppData)

---

## ✅ Checklist

- [ ] بنيت التطبيق بنجاح
- [ ] نقلت الملفات لجهاز الكاشير
- [ ] شغّلت التطبيق وأدخلت Backend URL
- [ ] سجّلت الجهاز في Backend
- [ ] الطابعة متصلة ومُكتشفة
- [ ] اختبرت الطباعة بنجاح
- [ ] حطيت التطبيق في Startup

---

**جاهز للاستخدام! 🎉**
