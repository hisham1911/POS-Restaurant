# 🚀 PRODUCTION DEPLOYMENT QUICK REFERENCE
## KasserPro POS — Rapid Deployment Guide

**Version:** 1.0.0  
**Status:** Production Ready  
**Deployment Time:** 2-3 hours

---

## ⚡ FASTEST PATH TO DEPLOYMENT

### Step 1: Build (30 minutes)

```powershell
# Run from project root
.\build-and-deploy.ps1 -Version "1.0.0"
```

**Output:** `KasserPro-v1.0.0-Production-YYYYMMDD.zip`

---

### Step 2: On Client Machine (2 hours)

```powershell
# 1. Install .NET 8 Runtime (5 min)
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0

# 2. Extract deployment package (2 min)
Expand-Archive -Path "KasserPro-v1.0.0-Production-YYYYMMDD.zip" -DestinationPath "C:\Temp\KasserPro"

# 3. Run installer (5 min)
cd C:\Temp\KasserPro
.\INSTALL.ps1

# 4. Install NSSM and create services (20 min)
# Download NSSM from: https://nssm.cc/download
# Follow instructions in DEPLOYMENT_GUIDE_COMPLETE.md Section 3.1

# 5. Configure IIS for frontend (20 min)
# Follow instructions in DEPLOYMENT_GUIDE_COMPLETE.md Section 4

# 6. Configure printer (10 min)
# Follow instructions in DEPLOYMENT_GUIDE_COMPLETE.md Section 5

# 7. Test system (30 min)
# Follow validation steps in DEPLOYMENT_GUIDE_COMPLETE.md Section 6

# 8. Train client (30 min)
# Follow training checklist in DEPLOYMENT_GUIDE_COMPLETE.md Section 9
```

---

## 📁 DOCUMENT GUIDE

### For Technical Implementation

| Document | Purpose | When to Use |
|----------|---------|-------------|
| 📘 **PRODUCTION_READINESS_AUDIT_REPORT.md** | Complete technical audit | Before deployment - understand all fixes |
| 📗 **DEPLOYMENT_GUIDE_COMPLETE.md** | Step-by-step deployment | During deployment - follow instructions |
| 📙 **PRE_DEPLOYMENT_CHECKLIST.md** | Validation checklist | Before & after deployment - verify everything |
| 📕 **EXECUTIVE_SUMMARY.md** | Quick overview for managers | Decision making - understand scope |

### For Client

| Document | Purpose | When to Use |
|----------|---------|-------------|
| 📄 **QUICK_START.txt** | Basic usage (Arabic) | Day 1 - learn basics |
| 📄 **EMERGENCY_PROCEDURES.txt** | Troubleshooting (Arabic) | When issues occur |
| 📄 **USER_MANUAL.pdf** | Complete guide | Training & reference |

---

## 🔧 CRITICAL CONFIGURATION

### Environment Variables (Set During Installation)

```powershell
# These are set automatically by INSTALL.ps1
[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[System.Environment]::SetEnvironmentVariable("Jwt__Key", "AUTO_GENERATED_64_CHAR_KEY", "Machine")
[System.Environment]::SetEnvironmentVariable("DeviceApiKey", "AUTO_GENERATED_GUID", "Machine")
```

### File Locations

| Component | Location | Size |
|-----------|----------|------|
| Backend API | `C:\KasserPro\Backend\` | ~40 MB |
| Frontend SPA | `C:\KasserPro\Frontend\` | ~3 MB |
| Bridge App | `C:\KasserPro\BridgeApp\` | ~25 MB |
| Database | `C:\KasserPro\Backend\kasserpro.db` | ~5 MB (start) |
| Backups | `C:\KasserPro\Backend\backups\` | Growing |
| Logs | `C:\KasserPro\Backend\logs\` | Growing |

---

## 🚨 EMERGENCY PROCEDURES

### System Not Starting

```powershell
# Check services
Get-Service KasserProAPI, KasserProFrontend, KasserProBridge | Format-Table

# Restart all services
Restart-Service KasserProAPI, KasserProFrontend, KasserProBridge

# If still not working, reboot
Restart-Computer
```

### Check Logs

```powershell
# View latest errors
Get-Content C:\KasserPro\Backend\logs\kasserpro-*.log | Select-Object -Last 50
```

### Database Backup

```powershell
# Manual backup
Copy-Item "C:\KasserPro\Backend\kasserpro.db" `
    -Destination "C:\KasserPro\Backend\backups\manual-$(Get-Date -Format 'yyyyMMdd-HHmmss').db"
```

---

## 📊 SYSTEM HEALTH CHECK

### Quick Health Check

```powershell
# API Health
Invoke-RestMethod -Uri "http://localhost:5243/api/health"

# Expected output:
# status      : healthy
# timestamp   : 2026-02-15T...
# database    : connected
# environment : Production
```

### Full Health Check

```powershell
# Check everything
$health = Invoke-RestMethod -Uri "http://localhost:5243/api/health"
$services = Get-Service KasserProAPI, KasserProFrontend, KasserProBridge
$diskSpace = (Get-Volume C).SizeRemaining / 1GB

Write-Host "System Status:" -ForegroundColor Cyan
Write-Host "  API: $($health.status)" -ForegroundColor Green
Write-Host "  Database: $($health.database.status)" -ForegroundColor Green
Write-Host "  Services: $($services | Where-Object {$_.Status -eq 'Running'} | Measure-Object).Count/3" -ForegroundColor Green
Write-Host "  Disk Space: $([math]::Round($diskSpace, 2)) GB" -ForegroundColor Green
```

---

## 🔑 DEFAULT CREDENTIALS

### First Login

```
URL: http://localhost:3000
Email: admin@kasserpro.com
Password: Admin@123

⚠️ IMPORTANT: Change password after first login!
```

---

## 🎯 COMMON ISSUES & SOLUTIONS

| Issue | Quick Fix |
|-------|-----------|
| API not responding | `Restart-Service KasserProAPI` |
| Frontend not loading | Clear browser cache, restart `KasserProFrontend` service |
| Printer not working | Check printer is on, restart `KasserProBridge` service |
| Database locked | Close any DB browsers, restart API service |
| Low disk space | Run `.\weekly_maintenance.ps1` to clean old logs/backups |

---

## 📞 SUPPORT CONTACTS

| Type | Contact | Hours |
|------|---------|-------|
| Emergency | [YOUR_PHONE] | 24/7 (Week 1) |
| Technical Support | [YOUR_EMAIL] | Business hours |
| WhatsApp | [YOUR_WHATSAPP] | Business hours |

---

## 📈 MONITORING SCHEDULE

| Frequency | Task | Action |
|-----------|------|--------|
| Every 15 min | Health check | Automated (scheduled task) |
| Daily | Check logs | Review for errors |
| Weekly | Clean old files | Run maintenance script |
| Monthly | Database size | Monitor growth |
| Quarterly | Performance review | Check response times |
| Yearly | Full audit | Security & performance |

---

## 🎓 TRAINING CHECKLIST

### Session 1: Basics (1 hour)
- [ ] Login/logout
- [ ] Open shift
- [ ] Create order
- [ ] Process payment
- [ ] Print receipt
- [ ] Close shift

### Session 2: Products (30 min)
- [ ] Add product
- [ ] Edit product
- [ ] Manage categories
- [ ] View inventory

### Session 3: Reports (30 min)
- [ ] Daily report
- [ ] Shift summary
- [ ] Top products
- [ ] Export data

### Session 4: Maintenance (30 min)
- [ ] Create backup
- [ ] View logs
- [ ] Restart services
- [ ] Contact support

---

## ✅ POST-DEPLOYMENT CHECKLIST

### Immediate (Day 1)
- [ ] All services running
- [ ] Health check passing
- [ ] Can login and create order
- [ ] Receipt prints (if printer available)
- [ ] Client knows emergency contact
- [ ] Desktop shortcuts created

### Short-term (Week 1)
- [ ] No critical issues
- [ ] Daily use confirmed
- [ ] Backups running
- [ ] Client comfortable with basics
- [ ] Follow-up call completed

### Long-term (Month 1)
- [ ] System stable
- [ ] No data loss
- [ ] Performance good
- [ ] Client satisfied
- [ ] Support plan active

---

## 🎉 SUCCESS CRITERIA

**Deployment is successful when:**

✅ System runs 24/7 without intervention  
✅ Daily backups complete automatically  
✅ Client can operate independently  
✅ Response times under 500ms  
✅ Zero data loss incidents  
✅ Support tickets under 5/week (after month 1)

---

## 📦 PACKAGE CONTENTS

```
KasserPro-v1.0.0-Production-YYYYMMDD.zip (50-100 MB)
│
├── Backend/ (40 MB)
│   ├── KasserPro.API.exe
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   └── (dependencies)
│
├── Frontend/ (3 MB)
│   ├── index.html
│   └── assets/
│
├── BridgeApp/ (25 MB)
│   └── KasserPro.BridgeApp.exe
│
├── Documentation/
│   ├── PRODUCTION_READINESS_AUDIT_REPORT.md
│   ├── DEPLOYMENT_GUIDE_COMPLETE.md
│   ├── PRE_DEPLOYMENT_CHECKLIST.md
│   ├── EXECUTIVE_SUMMARY.md
│   └── README.md
│
├── INSTALL.ps1 (automated installer)
└── UNINSTALL.ps1 (clean removal)
```

---

## 🔄 UPDATE PROCEDURE (Future Updates)

```powershell
# 1. Create backup
Copy-Item C:\KasserPro\Backend\kasserpro.db `
    -Destination C:\KasserPro\Backend\backups\pre-update-$(Get-Date -Format 'yyyyMMdd').db

# 2. Stop services
Stop-Service KasserProAPI, KasserProFrontend, KasserProBridge

# 3. Extract new version to temp location
Expand-Archive -Path "KasserPro-v1.1.0.zip" -DestinationPath "C:\Temp\KasserPro-1.1.0"

# 4. Copy new files (preserve database!)
Copy-Item C:\Temp\KasserPro-1.1.0\Backend\* C:\KasserPro\Backend\ -Force -Exclude kasserpro.db

# 5. Run migrations (if any)
cd C:\KasserPro\Backend
.\KasserPro.API.exe --migrate

# 6. Start services
Start-Service KasserProAPI, KasserProFrontend, KasserProBridge

# 7. Verify health
Invoke-RestMethod http://localhost:5243/api/health
```

---

## 🛠️ BUILD COMMANDS REFERENCE

### Full Build

```powershell
.\build-and-deploy.ps1 -Version "1.0.0"
```

### Backend Only

```powershell
cd src\KasserPro.API
dotnet publish -c Release -r win-x64 -o publish
```

### Frontend Only

```powershell
cd client
npm run build
```

### Bridge App Only

```powershell
cd src\KasserPro.BridgeApp
dotnet publish -c Release -r win-x64 -o publish
```

---

## 📱 REMOTE SUPPORT COMMANDS

### Check System Status (Remote)

```powershell
# Run on client machine via remote session
$status = @{
    Health = Invoke-RestMethod http://localhost:5243/api/health
    Services = Get-Service KasserPro* | Select-Object Name, Status
    DiskSpace = (Get-Volume C).SizeRemaining / 1GB
    DBSize = (Get-Item C:\KasserPro\Backend\kasserpro.db).Length / 1MB
    LastBackup = (Get-ChildItem C:\KasserPro\Backend\backups | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
}
$status | ConvertTo-Json
```

### Collect Logs for Analysis

```powershell
# Package logs for remote review
$date = Get-Date -Format 'yyyyMMdd'
Compress-Archive `
    -Path C:\KasserPro\Backend\logs\* `
    -DestinationPath "C:\KasserPro_Logs_$date.zip"

# Send C:\KasserPro_Logs_$date.zip to support
```

---

**Document Version:** 1.0  
**Last Updated:** February 15, 2026  
**Status:** ✅ Production Ready

---

**Quick Start:** Read this document → Run `build-and-deploy.ps1` → Follow `DEPLOYMENT_GUIDE_COMPLETE.md` → Success! 🎉

