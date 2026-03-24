# ============================================
# Diagnose Mobile Connection Issue
# ============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Mobile Connection Diagnostics" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Network Info
Write-Host "Step 1: Network Configuration" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Gray
$ips = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.*" }

if ($ips.Count -eq 0) {
    Write-Host "❌ No network IP found!" -ForegroundColor Red
    exit
}

Write-Host "Available IPs:" -ForegroundColor White
foreach ($ip in $ips) {
    Write-Host "  - $($ip.IPAddress) ($($ip.InterfaceAlias))" -ForegroundColor Cyan
}

$primaryIp = ($ips | Where-Object { $_.InterfaceAlias -like "*Wi-Fi*" } | Select-Object -First 1).IPAddress
if (-not $primaryIp) {
    $primaryIp = $ips[0].IPAddress
}

Write-Host "`nPrimary IP for mobile access: $primaryIp" -ForegroundColor Green
Write-Host ""

# Step 2: Backend Status
Write-Host "Step 2: Backend Status" -ForegroundColor Yellow
Write-Host "----------------------" -ForegroundColor Gray
$listening = netstat -ano | findstr ":5243" | findstr "LISTENING"

if ($listening) {
    Write-Host "✅ Backend is listening on port 5243" -ForegroundColor Green
    if ($listening -match "0\.0\.0\.0:5243") {
        Write-Host "✅ Listening on all interfaces (0.0.0.0)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  WARNING: Not listening on 0.0.0.0" -ForegroundColor Yellow
        Write-Host "   Backend may not be accessible from network" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ Backend is NOT listening on port 5243" -ForegroundColor Red
    Write-Host "   Run: .\start-backend-network.ps1" -ForegroundColor Yellow
    exit
}
Write-Host ""

# Step 3: Firewall Check
Write-Host "Step 3: Firewall Configuration" -ForegroundColor Yellow
Write-Host "-------------------------------" -ForegroundColor Gray
$rule = Get-NetFirewallRule -DisplayName "*KasserPro*" -ErrorAction SilentlyContinue | Where-Object { $_.Enabled -eq $true }

if ($rule) {
    Write-Host "✅ Firewall rule exists and is enabled" -ForegroundColor Green
} else {
    Write-Host "⚠️  WARNING: Firewall rule not found or disabled" -ForegroundColor Yellow
    Write-Host "   Run: .\setup-network-access.ps1" -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Test API from localhost
Write-Host "Step 4: Test API (localhost)" -ForegroundColor Yellow
Write-Host "-----------------------------" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri 'http://localhost:5243/api/system/health' -TimeoutSec 5
    Write-Host "✅ API responds on localhost" -ForegroundColor Green
} catch {
    Write-Host "❌ API does not respond on localhost" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Yellow
}
Write-Host ""

# Step 5: Test API from network IP
Write-Host "Step 5: Test API (network IP)" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "http://${primaryIp}:5243/api/system/health" -TimeoutSec 5
    Write-Host "✅ API responds on network IP" -ForegroundColor Green
} catch {
    Write-Host "❌ API does not respond on network IP" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Possible causes:" -ForegroundColor Yellow
    Write-Host "   - Firewall blocking connections" -ForegroundColor White
    Write-Host "   - Backend not listening on 0.0.0.0" -ForegroundColor White
    Write-Host "   - Antivirus blocking connections" -ForegroundColor White
}
Write-Host ""

# Step 6: Test Login from network IP
Write-Host "Step 6: Test Login (network IP)" -ForegroundColor Yellow
Write-Host "--------------------------------" -ForegroundColor Gray
$body = '{"email":"admin@kasserpro.com","password":"Admin@123"}'
try {
    $response = Invoke-RestMethod -Uri "http://${primaryIp}:5243/api/auth/login" -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 5
    Write-Host "✅ Login works on network IP" -ForegroundColor Green
    Write-Host "   Token: $($response.data.accessToken.Substring(0,20))..." -ForegroundColor White
} catch {
    Write-Host "❌ Login failed on network IP" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Yellow
    
    # Check if it's a 400 error (wrong credentials) or connection error
    if ($_.Exception.Message -like "*400*") {
        Write-Host ""
        Write-Host "   This is a 400 Bad Request error" -ForegroundColor Yellow
        Write-Host "   The API is reachable but login failed" -ForegroundColor Yellow
        Write-Host "   Check credentials or see logs" -ForegroundColor Yellow
    }
}
Write-Host ""

# Step 7: Check CORS configuration
Write-Host "Step 7: CORS Configuration" -ForegroundColor Yellow
Write-Host "--------------------------" -ForegroundColor Gray
$appsettings = Get-Content "backend\KasserPro.API\appsettings.json" -Raw | ConvertFrom-Json

if ($appsettings.AllowedOrigins -and ($appsettings.AllowedOrigins -contains "*")) {
    Write-Host "✅ CORS allows all origins (*)" -ForegroundColor Green
} else {
    Write-Host "⚠️  WARNING: CORS may not allow all origins" -ForegroundColor Yellow
    Write-Host "   Current: $($appsettings.AllowedOrigins -join ', ')" -ForegroundColor White
    Write-Host "   For mobile access, set to: [""*""]" -ForegroundColor Yellow
}
Write-Host ""

# Step 8: Recent errors in logs
Write-Host "Step 8: Recent Errors in Logs" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Gray
$log = Get-ChildItem backend\KasserPro.API\logs\*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$errors = Get-Content $log.FullName -Tail 100 | Select-String -Pattern "\[ERR\]|\[WRN\].*login|\[WRN\].*auth|400|401|403"

if ($errors.Count -gt 0) {
    Write-Host "⚠️  Found $($errors.Count) recent errors/warnings:" -ForegroundColor Yellow
    $errors | Select-Object -Last 5 | ForEach-Object {
        Write-Host "   $_" -ForegroundColor White
    }
} else {
    Write-Host "✅ No recent errors in logs" -ForegroundColor Green
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary & Instructions for Mobile" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "On your mobile device:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Make sure you're connected to the same WiFi" -ForegroundColor White
Write-Host "   (Not mobile data, not guest WiFi)" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Open browser and go to:" -ForegroundColor White
Write-Host "   http://${primaryIp}:5243" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. If page doesn't load:" -ForegroundColor White
Write-Host "   - Check WiFi connection" -ForegroundColor Gray
Write-Host "   - Try: http://${primaryIp}:5243/api/system/health" -ForegroundColor Gray
Write-Host "   - Restart router if needed" -ForegroundColor Gray
Write-Host ""
Write-Host "4. If page loads but login fails:" -ForegroundColor White
Write-Host "   - Check browser console (F12)" -ForegroundColor Gray
Write-Host "   - Look for CORS errors" -ForegroundColor Gray
Write-Host "   - Try clearing browser cache" -ForegroundColor Gray
Write-Host ""
Write-Host "5. Login credentials:" -ForegroundColor White
Write-Host "   Email: admin@kasserpro.com" -ForegroundColor Cyan
Write-Host "   Password: Admin@123" -ForegroundColor Cyan
Write-Host ""

Read-Host "Press Enter to exit"
