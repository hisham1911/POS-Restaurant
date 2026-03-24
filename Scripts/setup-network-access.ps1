# ============================================
# KasserPro Network Access Setup Script
# ============================================
# This script configures Windows Firewall to allow network access to KasserPro API
# Run as Administrator

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "KasserPro Network Access Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Step 1: Checking current network configuration..." -ForegroundColor Yellow
Write-Host ""

# Get IP addresses
$ipAddresses = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.*" }

Write-Host "Your IP Addresses:" -ForegroundColor Green
foreach ($ip in $ipAddresses) {
    Write-Host "  - $($ip.IPAddress) (Interface: $($ip.InterfaceAlias))" -ForegroundColor White
}
Write-Host ""

# Check if port 5243 is in use
Write-Host "Step 2: Checking if port 5243 is available..." -ForegroundColor Yellow
$portInUse = netstat -ano | findstr ":5243"
if ($portInUse) {
    Write-Host "Port 5243 is currently in use:" -ForegroundColor Green
    Write-Host $portInUse -ForegroundColor White
} else {
    Write-Host "Port 5243 is available (not currently in use)" -ForegroundColor Yellow
}
Write-Host ""

# Remove old firewall rules
Write-Host "Step 3: Removing old firewall rules..." -ForegroundColor Yellow
Remove-NetFirewallRule -DisplayName "KasserPro API" -ErrorAction SilentlyContinue
Remove-NetFirewallRule -DisplayName "KasserPro API (TCP 5243)" -ErrorAction SilentlyContinue
Write-Host "Old rules removed (if any existed)" -ForegroundColor Green
Write-Host ""

# Add new firewall rule
Write-Host "Step 4: Adding new firewall rule..." -ForegroundColor Yellow
try {
    New-NetFirewallRule `
        -DisplayName "KasserPro API (TCP 5243)" `
        -Description "Allow inbound connections to KasserPro API on port 5243 for network access" `
        -Direction Inbound `
        -Protocol TCP `
        -LocalPort 5243 `
        -Action Allow `
        -Enabled True `
        -Profile Any `
        -ErrorAction Stop | Out-Null
    
    Write-Host "Firewall rule created successfully!" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to create firewall rule: $_" -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

# Verify firewall rule
Write-Host "Step 5: Verifying firewall rule..." -ForegroundColor Yellow
$rule = Get-NetFirewallRule -DisplayName "KasserPro API (TCP 5243)" -ErrorAction SilentlyContinue
if ($rule) {
    Write-Host "Firewall rule verified:" -ForegroundColor Green
    Write-Host "  Name: $($rule.DisplayName)" -ForegroundColor White
    Write-Host "  Enabled: $($rule.Enabled)" -ForegroundColor White
    Write-Host "  Direction: $($rule.Direction)" -ForegroundColor White
    Write-Host "  Action: $($rule.Action)" -ForegroundColor White
} else {
    Write-Host "WARNING: Could not verify firewall rule" -ForegroundColor Yellow
}
Write-Host ""

# Display connection instructions
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Start the backend server:" -ForegroundColor White
Write-Host "   cd backend\KasserPro.API" -ForegroundColor Cyan
Write-Host "   dotnet run" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. On this device, open:" -ForegroundColor White
Write-Host "   http://localhost:5243" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. On other devices on the same network, open:" -ForegroundColor White
foreach ($ip in $ipAddresses) {
    Write-Host "   http://$($ip.IPAddress):5243" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Troubleshooting:" -ForegroundColor Yellow
Write-Host "- Make sure all devices are on the same WiFi network" -ForegroundColor White
Write-Host "- Check that Windows Firewall is enabled" -ForegroundColor White
Write-Host "- Try disabling antivirus temporarily if connection fails" -ForegroundColor White
Write-Host "- Check router settings for AP Isolation (should be disabled)" -ForegroundColor White
Write-Host ""
Read-Host "Press Enter to exit"
