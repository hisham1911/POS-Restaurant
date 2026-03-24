# توثيق النشر والتحقق - Deployment & Verification Checklist

**Document Type:** Deployment Checklist  
**Target Audience:** System Administrators, DevOps  
**Last Updated:** February 25, 2026

---

## 📋 Pre-Deployment Checklist

### Environment Requirements

```
☐ Windows Server 2019+ (or Windows 10/11)
☐ .NET 8 Runtime (not SDK required in production)
☐ Port 5243 available
☐ Minimum 2GB RAM
☐ Minimum 500MB free disk space
☐ Network connectivity (WiFi or LAN)
```

### Code Preparation

```
☐ Backend rebuilt with Release configuration
☐ Frontend built and tested
☐ No uncommitted changes in git
☐ All tests passing
☐ Performance baseline established
☐ Security review completed
```

---

## 🔐 Security Pre-Deployment Review

### Code Review

```csharp
// ✅ Verify SystemController has [AllowAnonymous]
[HttpGet("info")]
[AllowAnonymous]  // ← MUST BE PRESENT
public ActionResult<SystemInfoDto> GetSystemInfo() { ... }

// ✅ Verify CORS allows "*" for LAN
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", cors =>
    {
        cors.AllowAnyOrigin();  // ← CORRECT FOR LAN
        cors.AllowAnyMethod();
        cors.AllowAnyHeader();
    });
});

// ✅ Verify other endpoints still require [Authorize]
[HttpGet("orders")]
[Authorize]  // ← MUST BE PRESENT
public ActionResult<List<OrderDto>> GetOrders() { ... }

// ✅ Verify database file protected
// kasserpro.db should have appropriate filesystem permissions
```

### Network Configuration Review

```
☐ Firewall rule allows port 5243 inbound
☐ Windows Firewall not blocking .dotnet.exe
☐ No VPN/Proxy intercepting requests
☐ Network supports IPv4 (no IPv6-only networks)
☐ No DNS issues (can resolve hostnames)
```

### Database Review

```
☐ SQLite database file exists (kasserpro.db)
☐ Database migrations applied
☐ Test data loaded (if applicable)
☐ Backup created
☐ WAL mode enabled for concurrency
```

---

## 📦 Deployment Process

### Step 1: Backup (Pre-Deployment)

```powershell
$backupDate = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = "D:\Backups\KasserPro_$backupDate"

# Create backup directory
New-Item -ItemType Directory -Path $backupPath -Force

# Backup database
Copy-Item -Path "D:\مسح\POS\backend\KasserPro.API\kasserpro.db" `
          -Destination $backupPath\

# Backup current version
Copy-Item -Path "D:\مسح\POS\backend\KasserPro.API\bin" `
          -Destination $backupPath\ -Recurse

Write-Host "Backup created at: $backupPath"
```

### Step 2: Stop Current Application (if running)

```powershell
# Stop all dotnet processes
Get-Process dotnet | Stop-Process -Force

# Wait for graceful shutdown
Start-Sleep -Seconds 5

# Verify stopped
Get-Process dotnet -ErrorAction SilentlyContinue
```

### Step 3: Deploy New Build

```powershell
# Copy built backend to deployment location
$source = "D:\مسح\POS\backend\KasserPro.API\bin\Release\net8.0"
$dest = "D:\Deployment\KasserPro"

# Remove old files
Remove-Item -Path $dest -Recurse -Force -ErrorAction SilentlyContinue

# Copy new files
Copy-Item -Path $source -Destination $dest -Recurse

Write-Host "Deployment complete at: $dest"
```

### Step 4: Copy Frontend Static Files

```powershell
# Copy built frontend to wwwroot
$frontendBuild = "D:\مسح\POS\frontend\dist"
$wwwroot = "D:\Deployment\KasserPro\wwwroot"

# Clear old files
Remove-Item -Path $wwwroot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null

# Copy new files
Copy-Item -Path "$frontendBuild\*" -Destination $wwwroot -Recurse

Write-Host "Frontend deployed to wwwroot"
```

### Step 5: Update Configuration

```powershell
# Verify appsettings.Production.json
$configPath = "D:\Deployment\KasserPro\appsettings.Production.json"

# Check critical settings
$config = Get-Content $configPath | ConvertFrom-Json

if ($config.Urls -ne "http://0.0.0.0:5243") {
    Write-Error "Incorrect Kestrel binding!"
    exit 1
}

if ($config.AllowedOrigins -notcontains "*") {
    Write-Error "CORS not configured for network access!"
    exit 1
}

Write-Host "Configuration verified ✓"
```

### Step 6: Start Application

```powershell
# Start the application
$appPath = "D:\Deployment\KasserPro"
$logPath = "$appPath\deployment.log"

# Start as background job
$job = Start-Job -ScriptBlock {
    Set-Location "D:\Deployment\KasserPro"
    & "dotnet" "KasserPro.API.dll"
} -Name KasserPro

# Wait for startup
Start-Sleep -Seconds 5

# Check if running
$process = Get-Process dotnet -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "✓ Application started successfully"
    Write-Host "  Process ID: $($process.Id)"
} else {
    Write-Error "✗ Application failed to start"
    exit 1
}
```

---

## ✅ Post-Deployment Verification

### Step 1: Health Check Endpoint

```powershell
# Test health endpoint
$maxRetries = 30
$retryCount = 0

while ($retryCount -lt $maxRetries) {
    try {
        $response = curl.exe -s http://localhost:5243/api/system/health
        if ($response -like "*true*") {
            Write-Host "✓ Health check passed"
            break
        }
    } catch {
        $retryCount++
        Write-Host "Waiting for API to respond... ($retryCount/$maxRetries)"
        Start-Sleep -Seconds 1
    }
}

if ($retryCount -eq $maxRetries) {
    Write-Error "✗ API failed to start"
    exit 1
}
```

### Step 2: System Info Endpoint

```powershell
# Verify system info endpoint
$response = curl.exe -s -H "Accept: application/json" `
    http://localhost:5243/api/system/info | convertFrom-Json

# Validate response
if (!$response.lanIp) {
    Write-Error "✗ System info missing LAN IP"
    exit 1
}

if (!$response.url) {
    Write-Error "✗ System info missing URL"
    exit 1
}

Write-Host "✓ System Info Response:"
Write-Host "  IP: $($response.lanIp)"
Write-Host "  Port: $($response.port)"
Write-Host "  URL: $($response.url)"
```

### Step 3: Frontend Loading

```powershell
# Verify frontend loads
$response = curl.exe -s -w "%{http_code}" -o nul `
    http://localhost:5243/

if ($response -eq "200") {
    Write-Host "✓ Frontend loads successfully (HTTP 200)"
} else {
    Write-Error "✗ Frontend returned HTTP $response"
    exit 1
}
```

### Step 4: CORS Configuration

```powershell
# Verify CORS headers
$headers = curl.exe -s -i http://localhost:5243/api/system/info |
    select-string "Access-Control"

if ($headers) {
    Write-Host "✓ CORS headers present:"
    $headers | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Error "✗ No CORS headers found"
}
```

### Step 5: Network Accessibility

```powershell
# Get current IP
$ip = (Test-Connection -ComputerName (hostname) -Count 1).IPV4Address

# Verify from another device or local test
$testUrl = "http://$ip:5243/api/system/info"
Write-Host "Testing from: $testUrl"

$response = curl.exe -s $testUrl | convertFrom-Json

if ($response.lanIp -eq $ip.ToString()) {
    Write-Host "✓ Network accessibility verified"
    Write-Host "  Devices can access: http://$ip:5243"
} else {
    Write-Host "⚠ IP mismatch (might be OK if multiple adapters)"
}
```

---

## 📊 Verification Report Template

Create a deployment report:

```
═══════════════════════════════════════════════════════════════
  DEPLOYMENT VERIFICATION REPORT
═══════════════════════════════════════════════════════════════

Date: [DATE]
Deployed To: [SERVER_NAME]
Deployed By: [ADMIN_NAME]
Build Version: [VERSION]

───────────────────────────────────────────────────────────────
1. PRE-DEPLOYMENT
───────────────────────────────────────────────────────────────
☐ Backup created:        YES/NO    [Path]
☐ Code reviewed:         YES/NO    [Notes]
☐ Tests passed:          YES/NO    [# passed]
☐ Security review:       YES/NO    [Issues: None/List]

───────────────────────────────────────────────────────────────
2. DEPLOYMENT
───────────────────────────────────────────────────────────────
☐ Old version stopped:   YES/NO    [Time]
☐ New files copied:      YES/NO    [# files]
☐ Configuration updated: YES/NO    [Changes]
☐ Application started:   YES/NO    [Time]

───────────────────────────────────────────────────────────────
3. POST-DEPLOYMENT TESTS
───────────────────────────────────────────────────────────────
☐ Health endpoint:       PASS/FAIL  [Response time: XXms]
☐ System info endpoint:  PASS/FAIL  [IP: XXX.XXX.XXX.XXX]
☐ Frontend loads:        PASS/FAIL  [Time: XXms]
☐ CORS headers:          PASS/FAIL  [Headers: ]
☐ Network access:        PASS/FAIL  [IP: XXX.XXX.XXX.XXX]

───────────────────────────────────────────────────────────────
4. FUNCTIONAL TESTS
───────────────────────────────────────────────────────────────
☐ Login works:           PASS/FAIL  [Notes]
☐ Settings accessible:   PASS/FAIL  [Notes]
☐ Network card visible:  PASS/FAIL  [IP displayed: YES/NO]
☐ Status indicator:      PASS/FAIL  [Color: Green/Red]
☐ Copy button works:     PASS/FAIL  [URL copied correctly]
☐ Multi-device access:   PASS/FAIL  [Tested from: ?]

───────────────────────────────────────────────────────────────
5. ROLLBACK PLAN
───────────────────────────────────────────────────────────────
If issues occur, rollback to: [BACKUP_PATH]
Steps:
1. Stop current application
2. Restore from backup
3. Restart application
4. Verify previous version works
Estimated rollback time: [MINUTES]

───────────────────────────────────────────────────────────────
OVERALL STATUS: ✓ PASSED / ✗ FAILED

If failed, issues:
[List any issues found and resolution]

───────────────────────────────────────────────────────────────
Signed by: ________________    Date: ___________
═══════════════════════════════════════════════════════════════
```

---

## 🔄 Rollback Procedures

### If Deployment Fails

```powershell
# Step 1: Stop current application
Get-Process dotnet | Stop-Process -Force
Start-Sleep -Seconds 3

# Step 2: Restore from backup
$backupPath = "D:\Backups\KasserPro_20260225_100000"
$deployPath = "D:\Deployment\KasserPro"

# Remove failed deployment
Remove-Item -Path $deployPath -Recurse -Force

# Restore from backup
Copy-Item -Path $backupPath -Destination $deployPath -Recurse

Write-Host "Restored from backup: $backupPath"

# Step 3: Start restored version
Set-Location $deployPath
& "dotnet" "KasserPro.API.dll"

Write-Host "Application rolled back and restarted"
```

### If Issues Found After Deployment

```powershell
# Option 1: Quick restart (if minor issue)
Get-Process dotnet | Stop-Process -Force
Start-Sleep -Seconds 2
Set-Location "D:\Deployment\KasserPro"
& "dotnet" "KasserPro.API.dll"

# Option 2: Full rollback (if critical issue)
# Follow "If Deployment Fails" steps above
```

---

## 📈 Performance Baseline (for future comparisons)

Establish baseline metrics after successful deployment:

| Metric                               | Value     | Target   |
| ------------------------------------ | --------- | -------- |
| GET /api/system/info response time   | \_\_\_ ms | < 15ms   |
| GET /api/system/health response time | \_\_\_ ms | < 10ms   |
| Frontend page load time              | \_\_\_ ms | < 2000ms |
| Memory usage                         | \_\_\_ MB | < 200MB  |
| CPU usage (idle)                     | \_\_\_ %  | < 5%     |
| Concurrent connections               | 5+        | Yes/No   |

---

## 👥 Support Contact Information

| Issue Type              | Contact  | Response Time |
| ----------------------- | -------- | ------------- |
| Critical (app down)     | [PHONE]  | 15 min        |
| Major (features broken) | [EMAIL]  | 1 hour        |
| Minor (cosmetic issue)  | [TICKET] | 24 hours      |
| Question                | [FORUM]  | 48 hours      |

---

**Document Owner:** Development Team  
**Last Reviewed:** February 25, 2026  
**Next Review:** May 25, 2026
