# 🖨️ شرح نظام الطباعة الكامل في KasserPro

## نظرة عامة - الصورة الكبيرة

```
┌─────────────┐      ┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│  Frontend   │─────▶│   Backend   │─────▶│ Bridge App  │─────▶│   Printer   │
│  (React)    │ HTTP │   (C# API)  │SignalR│  (Windows)  │ESCPOS│  (Thermal)  │
└─────────────┘      └─────────────┘      └─────────────┘      └─────────────┘
```

---

## 📊 الجزء 1: قاعدة البيانات (Database)

### جدول Tenants - إعدادات الطباعة

```sql
CREATE TABLE Tenants (
    -- إعدادات الفاتورة
    ReceiptPaperSize TEXT DEFAULT '80mm',
    ReceiptCustomWidth INTEGER,
    ReceiptHeaderFontSize INTEGER DEFAULT 12,
    ReceiptBodyFontSize INTEGER DEFAULT 9,
    ReceiptTotalFontSize INTEGER DEFAULT 11,
    ReceiptShowBranchName INTEGER DEFAULT 1,
    ReceiptShowCashier INTEGER DEFAULT 1,
    ReceiptShowThankYou INTEGER DEFAULT 1,
    ReceiptShowCustomerName INTEGER DEFAULT 1,
    ReceiptShowLogo INTEGER DEFAULT 1,
    ReceiptFooterMessage TEXT,
    ReceiptPhoneNumber TEXT,
    
    -- إعدادات توجيه الطباعة (جديد)
    PrintRoutingMode TEXT DEFAULT 'BranchWithFallback',
    AutoPrintOnSale INTEGER DEFAULT 1,
    AutoPrintOnDebtPayment INTEGER DEFAULT 1,
    AutoPrintDailyReports INTEGER DEFAULT 0
);
```

### الإعدادات المتاحة:

| الإعداد | القيم الممكنة | الوصف |
|---------|---------------|-------|
| PrintRoutingMode | BranchOnly, BranchWithFallback, AllDevices, Disabled | كيف توجه الطباعة |
| AutoPrintOnSale | 0 أو 1 | طباعة تلقائية عند البيع |
| AutoPrintOnDebtPayment | 0 أو 1 | طباعة تلقائية عند دفع دين |
| AutoPrintDailyReports | 0 أو 1 | طباعة تلقائية للتقارير |

---

## 🎯 الجزء 2: Backend (C# API)

### 2.1 Controllers - نقاط البداية

#### OrdersController - عند إتمام البيع

```csharp
[HttpPost("{id}/complete")]
public async Task<IActionResult> Complete(int id, [FromBody] CompleteOrderRequest request)
{
    // 1. إتمام البيع
    var result = await _orderService.CompleteAsync(id, request);
    
    // 2. التحقق من إعدادات الطباعة
    var tenant = await _tenantService.GetCurrentTenantAsync();
    if (!tenant.AutoPrintOnSale) return Ok(result); // لو الطباعة مش مفعلة
    
    // 3. تجهيز بيانات الفاتورة
    var printCommand = new {
        CommandId = Guid.NewGuid().ToString(),
        Receipt = new {
            ReceiptNumber = order.OrderNumber,
            Items = order.Items,
            TotalAmount = order.Total,
            // ... باقي البيانات
        },
        Settings = tenant.ReceiptSettings
    };
    
    // 4. إرسال للطابعة حسب PrintRoutingMode
    switch (tenant.PrintRoutingMode) {
        case "BranchOnly":
            await _hubContext.Clients.Group($"branch-{branchId}")
                .SendAsync("PrintReceipt", printCommand);
            break;
        case "AllDevices":
            await _hubContext.Clients.All
                .SendAsync("PrintReceipt", printCommand);
            break;
        // ... باقي الأوضاع
    }
}
```

#### CustomersController - عند دفع دين

```csharp
[HttpPost("{id}/pay-debt")]
public async Task<IActionResult> PayDebt(int id, [FromBody] PayDebtRequest request)
{
    // نفس المنطق لكن مع AutoPrintOnDebtPayment
    if (!tenant.AutoPrintOnDebtPayment) return Ok(result);
    
    // إرسال إيصال دفع الدين
    await _hubContext.Clients.Group(branchGroup)
        .SendAsync("PrintReceipt", debtPaymentCommand);
}
```

#### ReportsController - طباعة التقرير اليومي

```csharp
[HttpPost("daily/print")]
public async Task<IActionResult> PrintDailyReport([FromQuery] DateTime? date)
{
    // تحويل بيانات التقرير لصيغة Receipt
    var receiptItems = new List<object>();
    receiptItems.Add(new { Name = "الطلبات", Quantity = -1 }); // Header
    receiptItems.Add(new { Name = "إجمالي الطلبات", Quantity = report.TotalOrders });
    // ... باقي البيانات
    
    await _hubContext.Clients.Group(branchGroup)
        .SendAsync("PrintReceipt", reportCommand);
}
```

---

### 2.2 SignalR Hub - الجسر بين Backend و Bridge App

```csharp
public class DeviceHub : Hub
{
    // عند اتصال جهاز جديد
    public override async Task OnConnectedAsync()
    {
        var deviceId = Headers["X-Device-Id"];
        var branchId = Headers["X-Branch-Id"];
        
        // إضافة الجهاز لـ Group حسب الفرع
        var groupName = $"branch-{branchId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Device {DeviceId} joined {GroupName}", 
            deviceId, groupName);
    }
    
    // عند استلام تأكيد الطباعة من Bridge App
    public async Task PrintCompleted(PrintCompletedEventDto eventDto)
    {
        _logger.LogInformation("Print {CommandId}: {Success}", 
            eventDto.CommandId, eventDto.Success);
    }
}
```

---

### 2.3 DTOs - هيكل البيانات

```csharp
// الأمر المرسل للطابعة
public class PrintCommandDto
{
    public string CommandId { get; set; }      // معرف فريد
    public ReceiptDto Receipt { get; set; }    // بيانات الفاتورة
    public ReceiptPrintSettings Settings { get; set; } // إعدادات الطباعة
}

// بيانات الفاتورة
public class ReceiptDto
{
    public string ReceiptNumber { get; set; }
    public string BranchName { get; set; }
    public DateTime Date { get; set; }
    public List<ReceiptItemDto> Items { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; }
    public string CashierName { get; set; }
    // ... باقي الحقول
}
```

---

## 🖥️ الجزء 3: Bridge App (Windows Desktop)

### 3.1 البنية الأساسية

```
KasserPro.BridgeApp/
├── App.xaml.cs              # نقطة البداية
├── Services/
│   ├── SignalRClientService.cs   # الاتصال بالـ Backend
│   ├── PrinterService.cs         # الطباعة الفعلية
│   └── SettingsManager.cs        # إدارة الإعدادات
├── Models/
│   └── PrintCommandDto.cs        # نفس الـ DTOs من Backend
└── ViewModels/
    └── SystemTrayManager.cs      # واجهة System Tray
```

---

### 3.2 App.xaml.cs - نقطة البداية

```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    // 1. تسجيل الخدمات
    services.AddSingleton<ISignalRClientService, SignalRClientService>();
    services.AddSingleton<IPrinterService, PrinterService>();
    
    // 2. الاتصال بالـ Backend
    var signalRClient = serviceProvider.GetRequiredService<ISignalRClientService>();
    
    // 3. ربط معالج الطباعة
    signalRClient.OnPrintCommandReceived += async (sender, args) =>
    {
        Log.Information("Processing print command {CommandId}", 
            args.Command.CommandId);
        
        // طباعة الفاتورة
        var success = await printerService.PrintReceiptAsync(args.Command);
        
        // إرسال تأكيد للـ Backend
        await signalRClient.SendPrintCompletedAsync(
            args.Command.CommandId, success, null);
    };
    
    // 4. محاولة الاتصال
    await signalRClient.ConnectAsync();
}
```

---

### 3.3 SignalRClientService - الاتصال بالـ Backend

```csharp
public async Task<bool> ConnectAsync()
{
    var settings = await _settingsManager.GetSettingsAsync();
    
    // إنشاء الاتصال
    _hubConnection = new HubConnectionBuilder()
        .WithUrl($"{settings.BackendUrl}/hubs/devices", options =>
        {
            options.Headers.Add("X-API-Key", settings.ApiKey);
            options.Headers.Add("X-Device-Id", settings.DeviceId);
            options.Headers.Add("X-Branch-Id", settings.BranchId);
        })
        .WithAutomaticReconnect(new InfiniteRetryPolicy())
        .Build();
    
    // تسجيل معالج الرسائل
    _hubConnection.On<PrintCommandDto>("PrintReceipt", (command) =>
    {
        Log.Information("Received print command: {CommandId}", command.CommandId);
        OnPrintCommandReceived?.Invoke(this, new PrintCommandEventArgs(command));
    });
    
    await _hubConnection.StartAsync();
    return true;
}
```

---

### 3.4 PrinterService - الطباعة الفعلية

```csharp
public async Task<bool> PrintReceiptAsync(PrintCommandDto command)
{
    try
    {
        var settings = await _settingsManager.GetSettingsAsync();
        var printerName = settings.DefaultPrinterName;
        
        // توليد بيانات ESCPOS (لغة الطابعات الحرارية)
        byte[] escposData = GenerateReceiptEscPos(command.Receipt);
        
        // إرسال للطابعة
        await SendToPrinterAsync(printerName, escposData);
        
        return true;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Print failed");
        return false;
    }
}

private byte[] GenerateReceiptEscPos(ReceiptDto receipt)
{
    var e = new EPSON();
    
    // Header
    e.SetAlignment(Alignment.Center);
    e.SetStyles(PrintStyle.Bold | PrintStyle.FontB);
    e.Append(receipt.BranchName);
    e.Append("\n\n");
    
    // Items
    e.SetAlignment(Alignment.Left);
    foreach (var item in receipt.Items)
    {
        e.Append($"{item.Name} x{item.Quantity}");
        e.SetAlignment(Alignment.Right);
        e.Append($"{item.TotalPrice:F2}\n");
    }
    
    // Total
    e.Append("─────────────────\n");
    e.SetStyles(PrintStyle.Bold);
    e.Append($"الإجمالي: {receipt.TotalAmount:F2}\n");
    
    // Cut paper
    e.FullPaperCut();
    
    return e.ToByteArray();
}
```

---

### 3.5 SettingsManager - إدارة الإعدادات

```csharp
// الإعدادات محفوظة في:
// %AppData%\KasserPro\settings.json

public class AppSettings
{
    public string DeviceId { get; set; }           // معرف الجهاز
    public string BackendUrl { get; set; }         // عنوان Backend
    public string ApiKey { get; set; }             // مفتاح API
    public string BranchId { get; set; }           // رقم الفرع
    public string DefaultPrinterName { get; set; } // اسم الطابعة
}
```

---

## 🌐 الجزء 4: Frontend (React) - حالياً غير موجود!

### الوضع الحالي:
❌ **لا يوجد كود طباعة في Frontend**

Frontend فقط بيعمل:
1. إتمام البيع عن طريق API: `POST /api/orders/{id}/complete`
2. Backend بياخد الطلب ويطبع تلقائياً

### المطلوب إضافته (اختياري):
```typescript
// في POS Page
const handleCompleteSale = async () => {
  // إتمام البيع
  const result = await completeOrder(orderId, paymentData);
  
  // الطباعة تحصل تلقائياً من Backend
  // لكن ممكن نضيف زرار "طباعة يدوية"
};

// زرار طباعة يدوية
const handleManualPrint = async (orderId: number) => {
  await fetch(`/api/orders/${orderId}/print`, { method: 'POST' });
};
```

---

## 🔄 الجزء 5: تدفق البيانات الكامل

### سيناريو: كاشير بيبيع فاتورة

```
1. Frontend (React)
   ↓ POST /api/orders/123/complete
   
2. Backend - OrdersController.Complete()
   ├─ حفظ البيع في Database
   ├─ قراءة Tenant Settings
   ├─ التحقق: AutoPrintOnSale = true?
   └─ تجهيز PrintCommandDto
   
3. Backend - DeviceHub
   ├─ تحديد الـ Group حسب PrintRoutingMode
   ├─ BranchOnly → Group: "branch-1"
   ├─ AllDevices → All connected devices
   └─ إرسال SignalR: "PrintReceipt"
   
4. Bridge App - SignalRClientService
   ├─ استقبال "PrintReceipt" message
   ├─ Trigger: OnPrintCommandReceived event
   └─ استدعاء PrinterService
   
5. Bridge App - PrinterService
   ├─ تحويل Receipt → ESCPOS bytes
   ├─ إرسال للطابعة عن طريق Windows API
   └─ إرسال تأكيد: PrintCompleted
   
6. Backend - DeviceHub.PrintCompleted()
   └─ تسجيل في Logs: "Print completed successfully"
   
7. Printer (Hardware)
   └─ طباعة الفاتورة 🖨️
```

---

## ⚙️ الجزء 6: إعدادات التحكم

### في Tenant Settings:

```json
{
  "PrintRoutingMode": "BranchOnly",
  "AutoPrintOnSale": true,
  "AutoPrintOnDebtPayment": true,
  "AutoPrintDailyReports": false
}
```

### أوضاع التوجيه (PrintRoutingMode):

| الوضع | السلوك | متى تستخدمه |
|-------|---------|-------------|
| **BranchOnly** | الطباعة تروح للفرع فقط | فروع متعددة، كل فرع له طابعة |
| **BranchWithFallback** | الفرع + الأجهزة الافتراضية | الوضع الآمن (افتراضي) |
| **AllDevices** | كل الأجهزة تطبع | فرع واحد أو طابعة مركزية |
| **Disabled** | لا طباعة تلقائية | طباعة يدوية فقط |

---

## 🔍 الجزء 7: استكشاف الأخطاء

### المشكلة: الطباعة مش شغالة

#### 1. تحقق من Backend
```bash
ssh root@168.231.106.139
journalctl -u kasserpro -f | grep Print
```

#### 2. تحقق من Bridge App
```
%AppData%\KasserPro\logs\bridge-app.log
```

#### 3. تحقق من الاتصال
```http
GET http://168.231.106.139:5243/api/device-test/status
```

#### 4. اختبار طباعة
```http
POST http://168.231.106.139:5243/api/device-test/print
{
  "branchId": "1",
  "message": "Test Print"
}
```

---

## 📝 الجزء 8: الملخص

### ما يحصل بالظبط:

1. **Database**: بتخزن إعدادات الطباعة في جدول Tenants
2. **Backend**: بيقرأ الإعدادات ويقرر يطبع ولا لأ
3. **SignalR**: بينقل الأمر من Backend للـ Bridge App
4. **Bridge App**: بيستقبل الأمر ويطبع على الطابعة
5. **Printer**: بتطبع الفاتورة

### الملفات المهمة:

```
Backend:
├── Entities/Tenant.cs                    # إعدادات الطباعة
├── Controllers/OrdersController.cs       # طباعة البيع
├── Controllers/CustomersController.cs    # طباعة دفع الدين
├── Controllers/ReportsController.cs      # طباعة التقارير
├── Hubs/DeviceHub.cs                     # SignalR Hub
└── DTOs/Orders/PrintCommandDto.cs        # هيكل البيانات

Bridge App:
├── App.xaml.cs                           # نقطة البداية
├── Services/SignalRClientService.cs      # الاتصال
├── Services/PrinterService.cs            # الطباعة
└── Models/PrintCommandDto.cs             # هيكل البيانات

Frontend:
└── (لا يوجد كود طباعة حالياً)
```

---

## ✅ الخلاصة

النظام شغال بالكامل ومتكامل! 

- ✅ Database: إعدادات كاملة
- ✅ Backend: منطق الطباعة موجود
- ✅ SignalR: الاتصال شغال
- ✅ Bridge App: جاهز للاستخدام
- ⚠️ Frontend: مافيش كود طباعة (Backend بيطبع تلقائياً)

**الطباعة تحصل تلقائياً بدون أي تدخل من Frontend!**
