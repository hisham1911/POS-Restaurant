# 🖨️ دليل ربط تطبيق الطابعة بـ KasserPro

## 📋 نظرة عامة

تطبيق **KasserPro Bridge** هو تطبيق Windows يعمل كجسر بين:
- **KasserPro Web** (المتصفح)
- **الطابعة الحرارية** (USB/Network)

يستخدم **SignalR** للاتصال الفوري بين المتصفح والتطبيق.

---

## 🏗️ البنية

```
┌─────────────────┐
│  KasserPro Web  │ (المتصفح)
│  Port 5243      │
└────────┬────────┘
         │ SignalR WebSocket
         │ /hubs/devices
         ↓
┌─────────────────┐
│  Backend API    │
│  Port 5243      │
└────────┬────────┘
         │ SignalR
         ↓
┌─────────────────┐
│  Bridge App     │ (Windows)
│  يستمع للأوامر  │
└────────┬────────┘
         │ ESCPOS
         ↓
┌─────────────────┐
│  الطابعة        │ (USB/Network)
└─────────────────┘
```

---

## 🔧 الإعداد

### الخطوة 1: بناء تطبيق Bridge

```powershell
# على جهازك المحلي
cd backend/KasserPro.BridgeApp

# بناء التطبيق
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish

# إنشاء Installer (اختياري)
# أو ضغط المجلد
Compress-Archive -Path ./publish/* -DestinationPath KasserPro-Bridge-Setup.zip
```

### الخطوة 2: تثبيت على جهاز الكاشير

1. **انسخ** `KasserPro-Bridge-Setup.zip` لجهاز الكاشير
2. **فك الضغط** في مجلد مثل `C:\KasserPro\Bridge`
3. **شغّل** `KasserPro.BridgeApp.exe`

### الخطوة 3: إعداد التطبيق

عند أول تشغيل، أدخل:

```
API URL: http://168.231.106.139:5243
أو
API URL: https://kasserpro.azinternational-eg.com (إذا عملت SSL)
```

---

## ⚙️ Configuration

### ملف الإعداد: `appsettings.json`

```json
{
  "ApiUrl": "http://168.231.106.139:5243",
  "SignalRHub": "/hubs/devices",
  "DeviceId": "CASHIER-01",
  "Printer": {
    "Type": "USB",
    "Name": "POS-80",
    "Port": "USB001",
    "Encoding": "CP864"
  }
}
```

### أنواع الطابعات المدعومة

| النوع | الإعداد |
|------|---------|
| USB | `Type: "USB"`, `Port: "USB001"` |
| Network | `Type: "Network"`, `IP: "192.168.1.100"`, `Port: 9100` |
| Serial | `Type: "Serial"`, `Port: "COM1"` |

---

## 🔌 الاتصال

### 1. التطبيق يتصل بالـ Backend

```csharp
// SignalR Connection
var connection = new HubConnectionBuilder()
    .WithUrl("http://168.231.106.139:5243/hubs/devices")
    .Build();

await connection.StartAsync();
```

### 2. Backend يرسل أوامر الطباعة

```csharp
// من Backend
await _hubContext.Clients.Group(deviceId).SendAsync("PrintReceipt", receiptData);
```

### 3. Bridge يستقبل ويطبع

```csharp
// في Bridge App
connection.On<ReceiptData>("PrintReceipt", async (data) => {
    await _printerService.PrintAsync(data);
});
```

---

## 🖨️ أوامر الطباعة

### من Frontend (React)

```typescript
// في POS Page بعد إتمام الطلب
const handleCompleteSale = async () => {
  // إتمام البيع
  const order = await completeOrder(orderData);
  
  // طباعة الفاتورة (تلقائي)
  // Backend يرسل للـ Bridge تلقائياً
};
```

### من Backend (C#)

```csharp
// في OrdersController
[HttpPost("complete")]
public async Task<IActionResult> CompleteOrder([FromBody] CompleteOrderDto dto)
{
    var order = await _orderService.CompleteOrderAsync(dto);
    
    // إرسال أمر الطباعة للـ Bridge
    await _deviceCommandService.SendPrintCommandAsync(
        deviceId: dto.DeviceId,
        order: order
    );
    
    return Ok(order);
}
```

---

## 🧪 اختبار الاتصال

### 1. من Bridge App

```
✅ Connected to API
✅ Device registered: CASHIER-01
✅ Printer detected: POS-80
```

### 2. من Frontend

افتح Console في المتصفح:
```javascript
// تحقق من اتصال SignalR
console.log('SignalR State:', connection.state);
// يجب أن يكون: "Connected"
```

### 3. اختبار طباعة

```powershell
# من Backend API
curl -X POST http://168.231.106.139:5243/api/device-test/print \
  -H "Content-Type: application/json" \
  -d '{"deviceId":"CASHIER-01","message":"Test Print"}'
```

---

## 🔧 استكشاف الأخطاء

### المشكلة: Bridge لا يتصل

**الحل:**
1. تأكد من URL صحيح
2. تأكد من Firewall مفتوح على Port 5243
3. شوف Logs في `C:\KasserPro\Bridge\logs\`

### المشكلة: الطابعة لا تطبع

**الحل:**
1. تأكد من الطابعة متصلة (USB/Network)
2. تأكد من Driver مثبت
3. جرب طباعة تجريبية من Windows

### المشكلة: SignalR Disconnected

**الحل:**
```csharp
// في Bridge App - إعادة الاتصال التلقائي
connection.Closed += async (error) =>
{
    await Task.Delay(5000);
    await connection.StartAsync();
};
```

---

## 📱 تشغيل تلقائي مع Windows

### إنشاء Shortcut في Startup

1. اضغط `Win + R`
2. اكتب: `shell:startup`
3. انسخ Shortcut لـ `KasserPro.BridgeApp.exe`

أو استخدم Task Scheduler:

```powershell
# PowerShell Script
$action = New-ScheduledTaskAction -Execute "C:\KasserPro\Bridge\KasserPro.BridgeApp.exe"
$trigger = New-ScheduledTaskTrigger -AtLogon
Register-ScheduledTask -TaskName "KasserPro Bridge" -Action $action -Trigger $trigger
```

---

## 🔒 الأمان

### 1. استخدم HTTPS (موصى به)

```json
{
  "ApiUrl": "https://kasserpro.azinternational-eg.com"
}
```

### 2. Device Authentication

```csharp
// في Bridge App
connection.On<string>("Authenticate", async (token) => {
    // حفظ Token للطلبات المستقبلية
    _authToken = token;
});
```

---

## 📊 Monitoring

### Logs Location

```
C:\KasserPro\Bridge\logs\
  ├─ bridge-20260323.log
  ├─ printer-20260323.log
  └─ signalr-20260323.log
```

### عرض الحالة

```
Bridge App Window:
┌─────────────────────────────┐
│ KasserPro Bridge v1.0       │
├─────────────────────────────┤
│ Status: ✅ Connected        │
│ API: 168.231.106.139:5243   │
│ Device: CASHIER-01          │
│ Printer: POS-80 (Ready)     │
│ Last Print: 2 mins ago      │
└─────────────────────────────┘
```

---

## 🎯 Best Practices

1. ✅ شغّل Bridge App على جهاز الكاشير فقط
2. ✅ استخدم Device ID فريد لكل كاشير
3. ✅ اختبر الطباعة قبل بداية الوردية
4. ✅ احتفظ بنسخة احتياطية من الإعدادات
5. ✅ راقب Logs بانتظام

---

## 📞 الدعم

للمزيد من المساعدة:
- Logs: `C:\KasserPro\Bridge\logs\`
- Backend Logs: `ssh root@168.231.106.139 "journalctl -u kasserpro -f"`
- SignalR Hub: `http://168.231.106.139:5243/hubs/devices`

---

## ✅ Checklist

- [ ] Bridge App مبني ومثبت
- [ ] API URL مُعد صحيح
- [ ] Device ID مُسجل
- [ ] الطابعة متصلة ومُكتشفة
- [ ] SignalR Connection نشط
- [ ] اختبار طباعة ناجح
- [ ] تشغيل تلقائي مُفعّل
