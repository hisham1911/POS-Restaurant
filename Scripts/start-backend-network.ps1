# ============================================
# Start Backend for Network Access
# ============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Starting KasserPro Backend (Network Mode)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Stop existing processes
Write-Host "Stopping existing dotnet processes..." -ForegroundColor Yellow
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "✅ Stopped" -ForegroundColor Green
Write-Host ""

# Set environment variable for network binding
$env:ASPNETCORE_URLS = "http://0.0.0.0:5243"
Write-Host "Environment: ASPNETCORE_URLS = http://0.0.0.0:5243" -ForegroundColor Cyan
Write-Host ""

# Start backend
Write-Host "Starting backend..." -ForegroundColor Yellow
Write-Host "Location: backend\KasserPro.API" -ForegroundColor White
Write-Host ""
Write-Host "Look for: 'Now listening on: http://0.0.0.0:5243'" -ForegroundColor Cyan
Write-Host ""

Set-Location backend\KasserPro.API
dotnet run
