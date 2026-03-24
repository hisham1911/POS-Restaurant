# ============================================
# Restart Backend Script
# ============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Restarting KasserPro Backend" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop all dotnet processes
Write-Host "Step 1: Stopping all dotnet processes..." -ForegroundColor Yellow
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "✅ All dotnet processes stopped" -ForegroundColor Green
Write-Host ""

# Step 2: Verify port is free
Write-Host "Step 2: Checking port 5243..." -ForegroundColor Yellow
$portCheck = netstat -ano | findstr ":5243"
if ($portCheck) {
    Write-Host "⚠️  Port 5243 still in use:" -ForegroundColor Yellow
    Write-Host $portCheck -ForegroundColor White
    Write-Host "Waiting for port to be released..." -ForegroundColor Yellow
    Start-Sleep -Seconds 3
} else {
    Write-Host "✅ Port 5243 is free" -ForegroundColor Green
}
Write-Host ""

# Step 3: Start backend
Write-Host "Step 3: Starting backend..." -ForegroundColor Yellow
Write-Host "Location: backend\KasserPro.API" -ForegroundColor White
Write-Host "Command: dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Backend will start in a new window..." -ForegroundColor Cyan
Write-Host "Look for: 'Now listening on: http://0.0.0.0:5243'" -ForegroundColor Cyan
Write-Host ""

# Start backend in new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\backend\KasserPro.API'; dotnet run"

Write-Host "✅ Backend starting..." -ForegroundColor Green
Write-Host ""
Write-Host "Wait 10 seconds for backend to start, then test:" -ForegroundColor Yellow
Write-Host "  1. On this device: http://localhost:5243" -ForegroundColor Cyan
Write-Host "  2. On other device: http://192.168.1.5:5243" -ForegroundColor Cyan
Write-Host ""
