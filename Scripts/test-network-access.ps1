# ============================================
# KasserPro Network Access Test Script
# ============================================
# This script tests if network access is working correctly

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "KasserPro Network Access Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check IP addresses
Write-Host "Test 1: Network Configuration" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Gray
$ipAddresses = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.*" }

if ($ipAddresses.Count -eq 0) {
    Write-Host "❌ FAIL: No network IP addresses found" -ForegroundColor Red
    Write-Host "   Make sure you're connected to WiFi or LAN" -ForegroundColor Yellow
} else {
    Write-Host "✅ PASS: Found $($ipAddresses.Count) network interface(s)" -ForegroundColor Green
    foreach ($ip in $ipAddresses) {
        Write-Host "   - $($ip.IPAddress) ($($ip.InterfaceAlias))" -ForegroundColor White
    }
}
Write-Host ""

# Test 2: Check if port 5243 is listening
Write-Host "Test 2: Port 5243 Status" -ForegroundColor Yellow
Write-Host "-------------------------" -ForegroundColor Gray
$portStatus = netstat -ano | findstr ":5243.*LISTENING"

if (-not $portStatus) {
    Write-Host "❌ FAIL: Port 5243 is not listening" -ForegroundColor Red
    Write-Host "   Backend is not running. Start it with:" -ForegroundColor Yellow
    Write-Host "   cd backend\KasserPro.API" -ForegroundColor Cyan
    Write-Host "   dotnet run" -ForegroundColor Cyan
} else {
    Write-Host "✅ PASS: Port 5243 is listening" -ForegroundColor Green
    
    # Check if listening on 0.0.0.0 or 127.0.0.1
    if ($portStatus -match "0\.0\.0\.0:5243") {
        Write-Host "   ✅ Listening on 0.0.0.0 (all networks) - CORRECT!" -ForegroundColor Green
    } elseif ($portStatus -match "127\.0\.0\.1:5243") {
        Write-Host "   ❌ Listening on 127.0.0.1 (localhost only) - WRONG!" -ForegroundColor Red
        Write-Host "   Fix: Update launchSettings.json to use 0.0.0.0:5243" -ForegroundColor Yellow
    }
    
    Write-Host "   Details:" -ForegroundColor Gray
    Write-Host "   $portStatus" -ForegroundColor White
}
Write-Host ""

# Test 3: Check firewall rule
Write-Host "Test 3: Firewall Configuration" -ForegroundColor Yellow
Write-Host "-------------------------------" -ForegroundColor Gray
$firewallRule = Get-NetFirewallRule -DisplayName "KasserPro API (TCP 5243)" -ErrorAction SilentlyContinue

if (-not $firewallRule) {
    Write-Host "❌ FAIL: Firewall rule not found" -ForegroundColor Red
    Write-Host "   Run setup script as Administrator:" -ForegroundColor Yellow
    Write-Host "   .\setup-network-access.ps1" -ForegroundColor Cyan
} else {
    if ($firewallRule.Enabled -eq $true -and $firewallRule.Action -eq "Allow") {
        Write-Host "✅ PASS: Firewall rule is configured correctly" -ForegroundColor Green
        Write-Host "   Name: $($firewallRule.DisplayName)" -ForegroundColor White
        Write-Host "   Enabled: $($firewallRule.Enabled)" -ForegroundColor White
        Write-Host "   Action: $($firewallRule.Action)" -ForegroundColor White
    } else {
        Write-Host "❌ FAIL: Firewall rule exists but not configured correctly" -ForegroundColor Red
        Write-Host "   Enabled: $($firewallRule.Enabled) (should be True)" -ForegroundColor Yellow
        Write-Host "   Action: $($firewallRule.Action) (should be Allow)" -ForegroundColor Yellow
    }
}
Write-Host ""

# Test 4: Test localhost connection
Write-Host "Test 4: Localhost API Connection" -ForegroundColor Yellow
Write-Host "---------------------------------" -ForegroundColor Gray

if (-not $portStatus) {
    Write-Host "⏭️  SKIP: Backend not running" -ForegroundColor Yellow
} else {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5243/api/system/health" -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ PASS: API responds on localhost" -ForegroundColor Green
            $content = $response.Content | ConvertFrom-Json
            Write-Host "   Status: $($content.status)" -ForegroundColor White
            Write-Host "   Timestamp: $($content.timestamp)" -ForegroundColor White
        } else {
            Write-Host "❌ FAIL: API returned status code $($response.StatusCode)" -ForegroundColor Red
        }
    } catch {
        Write-Host "❌ FAIL: Cannot connect to API on localhost" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
Write-Host ""

# Test 5: Test network IP connection
Write-Host "Test 5: Network IP API Connection" -ForegroundColor Yellow
Write-Host "----------------------------------" -ForegroundColor Gray

if (-not $portStatus) {
    Write-Host "⏭️  SKIP: Backend not running" -ForegroundColor Yellow
} elseif ($ipAddresses.Count -eq 0) {
    Write-Host "⏭️  SKIP: No network IP addresses" -ForegroundColor Yellow
} else {
    $testPassed = $false
    foreach ($ip in $ipAddresses) {
        try {
            $response = Invoke-WebRequest -Uri "http://$($ip.IPAddress):5243/api/system/health" -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                Write-Host "✅ PASS: API responds on $($ip.IPAddress)" -ForegroundColor Green
                $testPassed = $true
            }
        } catch {
            Write-Host "❌ FAIL: Cannot connect via $($ip.IPAddress)" -ForegroundColor Red
            Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    if (-not $testPassed) {
        Write-Host ""
        Write-Host "   Possible causes:" -ForegroundColor Yellow
        Write-Host "   - Backend listening on 127.0.0.1 instead of 0.0.0.0" -ForegroundColor White
        Write-Host "   - Firewall blocking connections" -ForegroundColor White
        Write-Host "   - Antivirus blocking connections" -ForegroundColor White
    }
}
Write-Host ""

# Test 6: Check CORS configuration
Write-Host "Test 6: CORS Configuration" -ForegroundColor Yellow
Write-Host "--------------------------" -ForegroundColor Gray

$appsettingsPath = "backend\KasserPro.API\appsettings.json"
if (Test-Path $appsettingsPath) {
    $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    
    if ($appsettings.AllowedOrigins) {
        if ($appsettings.AllowedOrigins -contains "*") {
            Write-Host "✅ PASS: CORS allows all origins (*)" -ForegroundColor Green
        } else {
            Write-Host "⚠️  WARNING: CORS has specific origins configured" -ForegroundColor Yellow
            Write-Host "   Allowed origins:" -ForegroundColor White
            foreach ($origin in $appsettings.AllowedOrigins) {
                Write-Host "   - $origin" -ForegroundColor White
            }
            Write-Host "   For network access, consider using '*'" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ FAIL: AllowedOrigins not found in appsettings.json" -ForegroundColor Red
        Write-Host "   Add: `"AllowedOrigins`": [`"*`"]" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ FAIL: appsettings.json not found at $appsettingsPath" -ForegroundColor Red
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$allTestsPassed = $true

if ($ipAddresses.Count -eq 0) { $allTestsPassed = $false }
if (-not $portStatus) { $allTestsPassed = $false }
if ($portStatus -and $portStatus -notmatch "0\.0\.0\.0:5243") { $allTestsPassed = $false }
if (-not $firewallRule) { $allTestsPassed = $false }

if ($allTestsPassed) {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your KasserPro is ready for network access!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Connect from other devices using:" -ForegroundColor White
    foreach ($ip in $ipAddresses) {
        Write-Host "  http://$($ip.IPAddress):5243" -ForegroundColor Cyan
    }
} else {
    Write-Host "❌ Some tests failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Review the failed tests above" -ForegroundColor White
    Write-Host "2. Follow the suggested fixes" -ForegroundColor White
    Write-Host "3. Run this test script again" -ForegroundColor White
    Write-Host ""
    Write-Host "For detailed help, see:" -ForegroundColor Yellow
    Write-Host "  NETWORK_ACCESS_FIX.md" -ForegroundColor Cyan
}

Write-Host ""
Read-Host "Press Enter to exit"
