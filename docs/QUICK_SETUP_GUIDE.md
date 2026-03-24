# دليل سريع للإعداد - Quick Setup Guide
# Network Multi-Device Feature - Quick Start

**Duration:** 10 minutes  
**Difficulty:** Medium  
**Prerequisites:** .NET 8 SDK, Node.js 18+

---

## ✅ Pre-Flight Checklist

```
☐ Windows 10/11 with .NET 8 SDK installed
☐ Node.js 18+ and npm
☐ Backend project at: d:\مسح\POS\backend\KasserPro.API
☐ Frontend project at: d:\مسح\POS\frontend
☐ Port 5243 available (not in use)
☐ Primary device connected to network (WiFi or LAN)
```

---

## 🚀 5-Minute Setup

### Step 1: Build Backend (2 minutes)

```powershell
cd "d:\مسح\POS\backend\KasserPro.API"
dotnet build --configuration Release
```

**Expected Output:**
```
Build succeeded.
0 Warning(s)
0 Error(s)
```

### Step 2: Build Frontend (2 minutes)

```powershell
cd "d:\مسح\POS\frontend"
npm install
npm run build
```

**Expected Output:**
```
✓ 1234 modules transformed
dist/ ready in 5s
```

### Step 3: Start Backend (1 minute)

```powershell
cd "d:\مسح\POS\backend\KasserPro.API"
dotnet run --configuration Release
```

**Expected Output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5243
      Application started
```

---

## 🧪 Verification (2 minutes)

### Test 1: Primary Device

```
✓ Open: http://localhost:5243
✓ Login with admin@kasserpro.com / Admin@123
✓ Go to Settings page
✓ Verify Network Info card appears
✓ Note the IP (e.g., 192.168.1.100)
```

### Test 2: Secondary Device

```
✓ On another device on same WiFi:
✓ Open: http://192.168.1.100:5243
✓ Should see login page (no CORS error)
✓ Login with same credentials
✓ Go to Settings
✓ Should see SAME IP displayed
```

---

## 🎯 Success Criteria

| Item | Status |
|------|--------|
| Backend running on port 5243 | ✅ |
| Frontend accessible via browser | ✅ |
| Network Info card visible in Settings | ✅ |
| Secondary device can access via IP | ✅ |
| Copy button works | ✅ |
| Status indicator shows green (online) | ✅ |

---

## 🔧 Troubleshooting Quick Fixes

| Problem | Solution |
|---------|----------|
| Port 5243 already in use | Kill process: `taskkill /im dotnet.exe /f` |
| CORS error | Restart backend |
| Card not visible | Hard refresh (Ctrl+Shift+R) |
| Can't reach from secondary | Check both on same WiFi network |

---

**Ready to use! 🎉**
