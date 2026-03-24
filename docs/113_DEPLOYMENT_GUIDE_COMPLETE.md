# 📦 PRODUCTION DEPLOYMENT GUIDE — KasserPro POS
## Complete Step-by-Step Local On-Premise Deployment

**Version:** 1.0  
**Date:** February 15, 2026  
**Target:** Non-Technical Client (Local Installation)  
**Estimated Time:** 2-3 hours

---

## 📋 PRE-DEPLOYMENT CHECKLIST

### Before You Start

✅ **Required:**
- [ ] Backend code has all critical fixes applied
- [ ] Frontend built with production configuration
- [ ] Desktop Bridge App compiled
- [ ] All environment variables documented
- [ ] Client machine meets minimum requirements
- [ ] Backup strategy explained to client

✅ **Required Software (bring installer USBs):**
- [ ] .NET 8 Runtime (ASP.NET Core) - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ ] .NET 8 Desktop Runtime (for Bridge App) - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ ] Modern web browser (Chrome/Edge already on Windows)

---

## 🎯 DEPLOYMENT ARCHITECTURE

### Final Folder Structure on Client Machine

```
C:\KasserPro\
├── Backend\
│   ├── KasserPro.API.exe
│   ├── appsettings.Production.json
│   ├── kasserpro.db
│   ├── backups\
│   ├── logs\
│   └── wwwroot\
├── Frontend\
│   ├── index.html
│   ├── assets\
│   └── (built static files)
├── BridgeApp\
│   ├── KasserPro.BridgeApp.exe
│   └── Config\
└── Documentation\
    ├── USER_MANUAL.pdf
    ├── QUICK_START.pdf
    └── SUPPORT_CONTACTS.txt
```

---

## 🔧 PHASE 1: BUILD & PREPARE (On Your Development Machine)

### Step 1.1: Build Backend (Production Mode)

```powershell
# Navigate to API project
cd src\KasserPro.API

# Clean previous builds
dotnet clean --configuration Release

# Publish for Windows x64
dotnet publish -c Release -r win-x64 --self-contained false -o publish\backend

# Verify output
ls publish\backend
# Should contain: KasserPro.API.exe, appsettings.json, appsettings.Production.json, etc.
```

**Important:** Use `--self-contained false` to require .NET Runtime (smaller size, easier updates).

---

### Step 1.2: Build Frontend (Production Mode)

```powershell
# Navigate to frontend
cd client

# Install dependencies (if not already)
npm install

# Build for production
npm run build

# Verify output
ls dist
# Should contain: index.html, assets folder, etc.
```

**Size check:** `dist` folder should be ~2-5 MB.

---

### Step 1.3: Build Desktop Bridge App

```powershell
# Navigate to Bridge App
cd src\KasserPro.BridgeApp

# Clean previous builds
dotnet clean --configuration Release

# Publish for Windows
dotnet publish -c Release -r win-x64 --self-contained false -o publish\bridgeapp

# Verify output
ls publish\bridgeapp
# Should contain: KasserPro.BridgeApp.exe, etc.
```

---

### Step 1.4: Package Everything

```powershell
# Create deployment package
New-Item -ItemType Directory -Path "C:\KasserProDeployment" -Force

# Copy Backend
Copy-Item -Path "src\KasserPro.API\publish\backend\*" -Destination "C:\KasserProDeployment\Backend\" -Recurse -Force

# Copy Frontend
Copy-Item -Path "client\dist\*" -Destination "C:\KasserProDeployment\Frontend\" -Recurse -Force

# Copy Bridge App
Copy-Item -Path "src\KasserPro.BridgeApp\publish\bridgeapp\*" -Destination "C:\KasserProDeployment\BridgeApp\" -Recurse -Force

# Copy Documentation
Copy-Item -Path "docs\*" -Destination "C:\KasserProDeployment\Documentation\" -Recurse -Force

# Compress into ZIP
Compress-Archive -Path "C:\KasserProDeployment\*" -DestinationPath "C:\KasserPro-v1.0-Deployment.zip"
```

---

## 🚀 PHASE 2: CLIENT MACHINE SETUP

### Step 2.1: Install .NET Runtime

**On Client Machine:**

```powershell
# Run .NET 8 Runtime installer
Start-Process -FilePath "dotnet-runtime-8.0.x-win-x64.exe" -Wait

# Run .NET 8 Desktop Runtime installer
Start-Process -FilePath "windowsdesktop-runtime-8.0.x-win-x64.exe" -Wait

# Verify installation
dotnet --list-runtimes
# Should show: Microsoft.NETCore.App 8.0.x, Microsoft.WindowsDesktop.App 8.0.x
```

---

### Step 2.2: Extract Deployment Package

```powershell
# Create application directory
New-Item -ItemType Directory -Path "C:\KasserPro" -Force

# Extract ZIP
Expand-Archive -Path "C:\KasserPro-v1.0-Deployment.zip" -DestinationPath "C:\KasserPro\" -Force

# Verify structure
tree C:\KasserPro /F
```

---

### Step 2.3: Configure Environment Variables

```powershell
# Generate secure JWT key
$jwtKey = [Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])

# Set system environment variables (requires admin)
[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", [System.EnvironmentVariableTarget]::Machine)
[System.Environment]::SetEnvironmentVariable("Jwt__Key", $jwtKey, [System.EnvironmentVariableTarget]::Machine)

# Generate Device API Key for Bridge App
$deviceApiKey = [System.Guid]::NewGuid().ToString()
[System.Environment]::SetEnvironmentVariable("DeviceApiKey", $deviceApiKey, [System.EnvironmentVariableTarget]::Machine)

# Display keys for documentation (SAVE THESE!)
Write-Host "JWT Key: $jwtKey" -ForegroundColor Green
Write-Host "Device API Key: $deviceApiKey" -ForegroundColor Green
```

⚠️ **IMPORTANT:** Save these keys in a secure location! You'll need them for:
- System recovery
- Additional installations
- Technical support

---

### Step 2.4: Configure appsettings.Production.json

**Edit:** `C:\KasserPro\Backend\appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Jwt": {
    "Key": "",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=kasserpro.db;Cache=Shared;Busy Timeout=5000;Journal Mode=WAL;Synchronous=NORMAL;Foreign Keys=True"
  },
  "ShiftAutoClose": {
    "Enabled": true,
    "HoursThreshold": 12
  },
  "AllowedOrigins": [
    "http://localhost:3000"
  ],
  "DeviceApiKey": "",
  "Backup": {
    "Enabled": true,
    "DailyBackupTime": "02:00",
    "RetentionDays": 14,
    "BackupDirectory": "backups"
  }
}
```

**Note:** JWT Key and Device API Key are read from environment variables (more secure).

---

## 🔐 PHASE 3: BACKEND SETUP

### Step 3.1: Create Windows Service (Recommended)

**Why Windows Service?**
- Starts automatically on boot
- Runs in background
- Restarts on failure
- No console window

**Create service using NSSM (Non-Sucking Service Manager):**

```powershell
# Download NSSM
Invoke-WebRequest -Uri "https://nssm.cc/release/nssm-2.24.zip" -OutFile "nssm.zip"
Expand-Archive -Path "nssm.zip" -DestinationPath "C:\nssm"

# Install Backend as service
C:\nssm\nssm-2.24\win64\nssm.exe install KasserProAPI "C:\KasserPro\Backend\KasserPro.API.exe"

# Configure service
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProAPI AppDirectory C:\KasserPro\Backend
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProAPI Description "KasserPro POS Backend API"
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProAPI Start SERVICE_AUTO_START
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProAPI AppEnvironmentExtra ASPNETCORE_ENVIRONMENT=Production

# Set restart policy (restart on failure)
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProAPI AppExit Default Restart
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProAPI AppStdout C:\KasserPro\Backend\logs\service-stdout.log
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProAPI AppStderr C:\KasserPro\Backend\logs\service-stderr.log

# Start service
Start-Service KasserProAPI

# Verify service is running
Get-Service KasserProAPI
# Status should be: Running
```

---

### Step 3.2: Verify Backend is Running

```powershell
# Test API health
Invoke-RestMethod -Uri "http://localhost:5243/api/health" -Method Get

# Expected output:
# status      : healthy
# timestamp   : 2026-02-15T10:30:00.000Z
# version     : 1.0.0
# database    : connected
# environment : Production
```

**If health check fails:**
1. Check service status: `Get-Service KasserProAPI`
2. Check logs: `Get-Content C:\KasserPro\Backend\logs\kasserpro-*.log | Select-Object -Last 50`
3. Check ports: `netstat -ano | findstr :5243`

---

## 🌐 PHASE 4: FRONTEND SETUP

### Option A: Serve with IIS (Recommended for Windows)

**Install IIS:**
```powershell
# Enable IIS feature
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer
Enable-WindowsOptionalFeature -Online -FeatureName IIS-CommonHttpFeatures
Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpErrors
Enable-WindowsOptionalFeature -Online -FeatureName IIS-StaticContent
Enable-WindowsOptionalFeature -Online -FeatureName IIS-DefaultDocument
```

**Configure IIS:**
```powershell
# Import IIS module
Import-Module WebAdministration

# Create application pool
New-WebAppPool -Name "KasserProFrontend"
Set-ItemProperty IIS:\AppPools\KasserProFrontend -Name managedRuntimeVersion -Value ""

# Create website
New-Website -Name "KasserProFrontend" `
    -Port 3000 `
    -PhysicalPath "C:\KasserPro\Frontend" `
    -ApplicationPool "KasserProFrontend"

# Start website
Start-Website -Name "KasserProFrontend"
```

**Create web.config for IIS:**
Create `C:\KasserPro\Frontend\web.config`:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="React Routes" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/" />
        </rule>
      </rules>
    </rewrite>
    <staticContent>
      <mimeMap fileExtension=".json" mimeType="application/json" />
    </staticContent>
  </system.webServer>
</configuration>
```

---

### Option B: Serve with http-server (Simpler, but less robust)

```powershell
# Install Node.js (if not already)
# Then install http-server globally
npm install -g http-server

# Create start script
@"
@echo off
cd C:\KasserPro\Frontend
http-server -p 3000 -c-1
"@ | Out-File -FilePath "C:\KasserPro\Frontend\start-frontend.bat" -Encoding ASCII

# Create Windows service for frontend (with NSSM)
C:\nssm\nssm-2.24\win64\nssm.exe install KasserProFrontend "C:\KasserPro\Frontend\start-frontend.bat"
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProFrontend Description "KasserPro POS Frontend"
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProFrontend Start SERVICE_AUTO_START

# Start service
Start-Service KasserProFrontend
```

---

### Step 4.1: Verify Frontend

```powershell
# Test in browser
Start-Process "http://localhost:3000"
```

**Expected:** Login page should appear.

---

## 🖨️ PHASE 5: DESKTOP BRIDGE APP SETUP

### Step 5.1: Configure Bridge App

**Create settings file:**
Create `C:\KasserPro\BridgeApp\Config\settings.json`:
```json
{
  "BackendUrl": "http://localhost:5243",
  "ApiKey": "YOUR_DEVICE_API_KEY_HERE",
  "DeviceId": "DEVICE-001",
  "PrinterType": "Thermal",
  "PrinterName": "",
  "PaperWidth": 80,
  "AutoConnect": true
}
```

**Replace `YOUR_DEVICE_API_KEY_HERE` with the Device API Key from environment variable.**

---

### Step 5.2: Create Startup Shortcut

```powershell
# Create shortcut in Startup folder
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\KasserPro Bridge.lnk")
$Shortcut.TargetPath = "C:\KasserPro\BridgeApp\KasserPro.BridgeApp.exe"
$Shortcut.WorkingDirectory = "C:\KasserPro\BridgeApp"
$Shortcut.WindowStyle = 7  # Minimized
$Shortcut.Save()
```

**Alternative: Create Windows Service (recommended for production):**
```powershell
C:\nssm\nssm-2.24\win64\nssm.exe install KasserProBridge "C:\KasserPro\BridgeApp\KasserPro.BridgeApp.exe"
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProBridge Description "KasserPro Printer Bridge"
C:\nssm\nssm-2.24\win64\nssm.exe set KasserProBridge Start SERVICE_AUTO_START
Start-Service KasserProBridge
```

---

### Step 5.3: Test Printer Integration

```powershell
# Test from backend
Invoke-RestMethod -Uri "http://localhost:5243/api/DeviceTest/test-print" -Method Post
```

**Expected:** Test receipt should print on thermal printer.

---

## ✅ PHASE 6: POST-DEPLOYMENT VALIDATION

### Step 6.1: System Health Check

```powershell
# Check all services
Get-Service KasserProAPI, KasserProFrontend, KasserProBridge | Format-Table -AutoSize

# All should show: Running

# Check API health
$health = Invoke-RestMethod -Uri "http://localhost:5243/api/health"
Write-Host "Backend Status: $($health.status)" -ForegroundColor Green

# Check database
Test-Path "C:\KasserPro\Backend\kasserpro.db"
# Should return: True

# Check backup directory
Test-Path "C:\KasserPro\Backend\backups"
# Should return: True
```

---

### Step 6.2: Functional Testing

**Test 1: Login**
1. Open browser: `http://localhost:3000`
2. Login with default credentials:
   - Email: `admin@kasserpro.com`
   - Password: `Admin@123`
3. Should redirect to dashboard

**Test 2: Create Product**
1. Navigate to Products
2. Click "Add Product"
3. Fill details and save
4. Verify product appears in list

**Test 3: Create Order**
1. Navigate to POS
2. Open shift
3. Add products to cart
4. Complete order
5. Verify order appears in Orders list

**Test 4: Print Receipt**
1. Complete an order
2. Click "Print Receipt"
3. Verify receipt prints on thermal printer

**Test 5: Backup**
1. Navigate to System → Backup
2. Click "Create Backup"
3. Verify backup file created in `C:\KasserPro\Backend\backups`

---

### Step 6.3: Performance Check

```powershell
# Check database size
$dbSize = (Get-Item "C:\KasserPro\Backend\kasserpro.db").Length / 1MB
Write-Host "Database Size: $([math]::Round($dbSize, 2)) MB"

# Check log file count
$logCount = (Get-ChildItem "C:\KasserPro\Backend\logs").Count
Write-Host "Log Files: $logCount"

# Check backup count
$backupCount = (Get-ChildItem "C:\KasserPro\Backend\backups").Count
Write-Host "Backup Files: $backupCount"

# Check memory usage
Get-Process dotnet | Select-Object Name, @{Name="Memory(MB)";Expression={[math]::Round($_.WorkingSet64/1MB, 2)}}
```

---

## 🔧 PHASE 7: CLIENT TRAINING & HANDOVER

### Step 7.1: Create Desktop Shortcuts

```powershell
# Create "Open KasserPro" shortcut on desktop
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\KasserPro.lnk")
$Shortcut.TargetPath = "http://localhost:3000"
$Shortcut.IconLocation = "C:\KasserPro\Frontend\favicon.ico"
$Shortcut.Save()
```

---

### Step 7.2: Client Documentation Package

**Create:** `C:\KasserPro\Documentation\QUICK_START.txt`

```text
═══════════════════════════════════════════════════════
      KasserPro POS - دليل البدء السريع
═══════════════════════════════════════════════════════

🚀 لفتح البرنامج:
   - اضغط على أيقونة "KasserPro" على سطح المكتب
   - أو افتح المتصفح واذهب إلى: http://localhost:3000

🔐 تسجيل الدخول:
   - البريد الإلكتروني: admin@kasserpro.com
   - كلمة المرور: Admin@123
   
   ⚠️ هام: قم بتغيير كلمة المرور بعد أول تسجيل دخول!

📊 الميزات الأساسية:
   ✅ نقطة البيع (POS) - شاشة البيع السريع
   ✅ إدارة المنتجات والفئات
   ✅ إدارة المخزون
   ✅ تقارير المبيعات اليومية
   ✅ إدارة الورديات
   ✅ إدارة العملاء
   ✅ فواتير الشراء
   ✅ المصروفات وسند الصرف

🖨️ الطباعة:
   - تأكد من تشغيل الطابعة الحرارية
   - سيتم طباعة الفاتورة تلقائياً بعد كل عملية بيع

💾 النسخ الاحتياطي:
   - يتم النسخ الاحتياطي تلقائياً كل يوم الساعة 2:00 صباحاً
   - يمكنك عمل نسخة احتياطية يدوية من: الإعدادات → النظام → نسخ احتياطي
   - النسخ الاحتياطية محفوظة في: C:\KasserPro\Backend\backups

📞 الدعم الفني:
   - الهاتف: [YOUR_PHONE]
   - البريد الإلكتروني: [YOUR_EMAIL]
   - ساعات العمل: [YOUR_HOURS]

⚠️ في حالة حدوث مشكلة:
   1. أعد تشغيل الجهاز
   2. إذا استمرت المشكلة، اتصل بالدعم الفني
   3. لا تحاول حذف أو تعديل أي ملفات
```

---

### Step 7.3: Emergency Procedures Document

**Create:** `C:\KasserPro\Documentation\EMERGENCY_PROCEDURES.txt`

```text
═══════════════════════════════════════════════════════
      KasserPro POS - إجراءات الطوارئ
═══════════════════════════════════════════════════════

🚨 إذا توقف النظام عن العمل:

الخطوة 1: أعد تشغيل الخدمات
   اضغط Windows + R
   اكتب: services.msc
   اضغط Enter
   ابحث عن: KasserProAPI
   اضغط يمين → إعادة التشغيل

الخطوة 2: أعد تشغيل الجهاز
   إذا لم تنجح الخطوة 1، أعد تشغيل الكمبيوتر

الخطوة 3: اتصل بالدعم الفني
   إذا استمرت المشكلة بعد إعادة التشغيل

═══════════════════════════════════════════════════════

📞 أرقام الطوارئ:
   - الدعم الفني: [YOUR_PHONE]
   - الدعم الفوري (WhatsApp): [YOUR_WHATSAPP]

═══════════════════════════════════════════════════════

⚠️ لا تحاول:
   ❌ حذف أي ملفات من مجلد C:\KasserPro
   ❌ تعديل إعدادات النظام
   ❌ فتح أي ملفات .db أو .log
   ❌ إيقاف الخدمات من Task Manager

═══════════════════════════════════════════════════════
```

---

## 📊 PHASE 8: MONITORING & MAINTENANCE

### Step 8.1: Setup Automatic Monitoring

**Create monitoring script:**
`C:\KasserPro\Scripts\monitor.ps1`

```powershell
# KasserPro Health Monitor
$logFile = "C:\KasserPro\Logs\monitor.log"

function Log {
    param($message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $message" | Out-File -FilePath $logFile -Append
}

function Check-Service {
    param($serviceName)
    
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    
    if ($null -eq $service) {
        Log "ERROR: Service $serviceName not found"
        return $false
    }
    
    if ($service.Status -ne 'Running') {
        Log "WARNING: Service $serviceName is not running. Attempting restart..."
        try {
            Start-Service -Name $serviceName
            Log "SUCCESS: Service $serviceName restarted"
            return $true
        }
        catch {
            Log "ERROR: Failed to restart service $serviceName - $($_.Exception.Message)"
            # TODO: Send alert email/SMS
            return $false
        }
    }
    
    return $true
}

function Check-Health {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5243/api/health" -TimeoutSec 10
        if ($response.status -eq "healthy") {
            Log "INFO: Health check passed"
            return $true
        }
        else {
            Log "WARNING: Health check returned: $($response.status)"
            return $false
        }
    }
    catch {
        Log "ERROR: Health check failed - $($_.Exception.Message)"
        return $false
    }
}

# Main monitoring loop
Log "=== Monitoring started ==="

Check-Service "KasserProAPI"
Check-Service "KasserProFrontend"
Check-Service "KasserProBridge"
Check-Health

Log "=== Monitoring completed ==="
```

**Schedule monitoring task:**
```powershell
# Create scheduled task (runs every 15 minutes)
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File C:\KasserPro\Scripts\monitor.ps1"
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 15) -RepetitionDuration ([TimeSpan]::MaxValue)
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

Register-ScheduledTask -TaskName "KasserPro Health Monitor" -Action $action -Trigger $trigger -Principal $principal -Settings $settings
```

---

### Step 8.2: Weekly Maintenance Script

**Create:** `C:\KasserPro\Scripts\weekly_maintenance.ps1`

```powershell
# KasserPro Weekly Maintenance

$logFile = "C:\KasserPro\Logs\maintenance.log"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

"$timestamp - Starting weekly maintenance..." | Out-File -FilePath $logFile -Append

# 1. Clean old logs (keep last 30 days)
Get-ChildItem "C:\KasserPro\Backend\logs" -Filter "*.log" | 
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | 
    ForEach-Object {
        Remove-Item $_.FullName
        "$timestamp - Deleted old log: $($_.Name)" | Out-File -FilePath $logFile -Append
    }

# 2. Clean old backups (keep last 90 days)
Get-ChildItem "C:\KasserPro\Backend\backups" -Filter "*.db" | 
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-90) } | 
    ForEach-Object {
        Remove-Item $_.FullName
        "$timestamp - Deleted old backup: $($_.Name)" | Out-File -FilePath $logFile -Append
    }

# 3. Check disk space
$drive = Get-Volume -DriveLetter C
$freeSpaceGB = $drive.SizeRemaining / 1GB
if ($freeSpaceGB -lt 5) {
    "$timestamp - WARNING: Low disk space - $([math]::Round($freeSpaceGB, 2)) GB remaining" | Out-File -FilePath $logFile -Append
    # TODO: Send alert
}

# 4. Database integrity check
$dbPath = "C:\KasserPro\Backend\kasserpro.db"
$dbSizeMB = (Get-Item $dbPath).Length / 1MB
"$timestamp - Database size: $([math]::Round($dbSizeMB, 2)) MB" | Out-File -FilePath $logFile -Append

"$timestamp - Weekly maintenance completed" | Out-File -FilePath $logFile -Append
```

**Schedule weekly task:**
```powershell
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File C:\KasserPro\Scripts\weekly_maintenance.ps1"
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 3am
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName "KasserPro Weekly Maintenance" -Action $action -Trigger $trigger -Principal $principal
```

---

## 🎓 PHASE 9: CLIENT TRAINING SESSION

### Training Checklist (2-3 hours)

**✅ Session 1: Basic Operations (1 hour)**
- [ ] Login and logout
- [ ] Opening a shift
- [ ] Adding products to cart
- [ ] Processing payment (cash/card)
- [ ] Printing receipt
- [ ] Closing shift
- [ ] Viewing shift report

**✅ Session 2: Product Management (30 min)**
- [ ] Adding new products
- [ ] Editing product details
- [ ] Setting prices
- [ ] Managing categories
- [ ] Viewing inventory levels

**✅ Session 3: Reports (30 min)**
- [ ] Daily sales report
- [ ] Shift summary
- [ ] Top products
- [ ] Customer reports
- [ ] Exporting reports

**✅ Session 4: Maintenance (30 min)**
- [ ] Creating manual backup
- [ ] Viewing logs
- [ ] Emergency restart procedures
- [ ] When to call support

---

## 📝 POST-DEPLOYMENT CHECKLIST

### Before Leaving Client Site

- [ ] All services running and verified
- [ ] Frontend accessible from browser
- [ ] Printer configured and tested
- [ ] Desktop shortcuts created
- [ ] Default admin password changed
- [ ] First backup created manually
- [ ] Monitoring tasks scheduled
- [ ] Emergency procedures documented
- [ ] Client trained on basic operations
- [ ] Support contact information provided
- [ ] Remote access configured (if agreed)
- [ ] Invoice/payment completed

---

## 🆘 TROUBLESHOOTING GUIDE

### Issue: Backend service won't start

**Check:**
1. .NET Runtime installed: `dotnet --list-runtimes`
2. Port 5243 available: `netstat -ano | findstr :5243`
3. Database file exists: `Test-Path C:\KasserPro\Backend\kasserpro.db`
4. Environment variables set: `[Environment]::GetEnvironmentVariable("Jwt__Key", "Machine")`
5. Logs for errors: `Get-Content C:\KasserPro\Backend\logs\*.log | Select-Object -Last 100`

**Solution:**
```powershell
# Restart service
Restart-Service KasserProAPI

# If fails, check event logs
Get-EventLog -LogName Application -Source KasserProAPI -Newest 20
```

---

### Issue: Frontend shows API connection error

**Check:**
1. Backend service running: `Get-Service KasserProAPI`
2. API health: `Invoke-RestMethod http://localhost:5243/api/health`
3. Firewall not blocking: `Test-NetConnection -ComputerName localhost -Port 5243`

**Solution:**
```powershell
# Test API directly
Invoke-RestMethod -Uri "http://localhost:5243/api/health" -Method Get
```

---

### Issue: Printer not working

**Check:**
1. Bridge App service running: `Get-Service KasserProBridge`
2. Printer connected and on
3. Bridge App configuration: `Get-Content C:\KasserPro\BridgeApp\Config\settings.json`

**Solution:**
```powershell
# Restart bridge service
Restart-Service KasserProBridge

# Test print
Invoke-RestMethod -Uri "http://localhost:5243/api/DeviceTest/test-print" -Method Post
```

---

## 📞 SUPPORT & MAINTENANCE

### Monthly Maintenance (Remote)

**Tasks:**
- Review system logs for errors
- Verify backups are running
- Check disk space
- Review performance metrics
- Apply any updates (if needed)

### Yearly Maintenance (On-Site or Remote)

**Tasks:**
- Full system health check
- Update .NET runtime (if needed)
- Database optimization
- Review and archive old data
- Renew support contract

---

## 🎉 DEPLOYMENT COMPLETE!

### Success Criteria

✅ All services running automatically
✅ Frontend accessible and responsive
✅ Printer integration working
✅ Daily backups scheduled
✅ Monitoring tasks active
✅ Client trained and confident
✅ Documentation provided
✅ Support channels established

---

**Deployment Status:** ✅ **PRODUCTION READY**

**Next Steps:**
1. Monitor system for first 48 hours
2. Schedule follow-up call after 1 week
3. Provide remote support as needed
4. Plan for next training session (advanced features)

---

**Deployment Completed By:** ___________________  
**Date:** ___________________  
**Client Signature:** ___________________  

