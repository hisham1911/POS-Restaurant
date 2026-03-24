# Verify Inventory Seeding Script
# This script helps verify that the inventory data was seeded correctly

Write-Host "Verifying Inventory Seeding..." -ForegroundColor Cyan
Write-Host ""

# Check if Backend is running
Write-Host "1. Checking Backend..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5243/health" -Method Get -TimeoutSec 5 -ErrorAction Stop
    Write-Host "   Backend is running on port 5243" -ForegroundColor Green
}
catch {
    Write-Host "   Backend is NOT running!" -ForegroundColor Red
    Write-Host "   Run: cd src/KasserPro.API; dotnet run" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Check if Frontend is running
Write-Host "2. Checking Frontend..." -ForegroundColor Yellow
$frontendRunning = $false
try {
    $response = Invoke-WebRequest -Uri "http://localhost:3001" -Method Get -TimeoutSec 5 -ErrorAction Stop
    Write-Host "   Frontend is running on port 3001" -ForegroundColor Green
    $frontendRunning = $true
}
catch {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:3000" -Method Get -TimeoutSec 5 -ErrorAction Stop
        Write-Host "   Frontend is running on port 3000" -ForegroundColor Green
        $frontendRunning = $true
    }
    catch {
        Write-Host "   Frontend is NOT running!" -ForegroundColor Red
        Write-Host "   Run: cd client; npm run dev" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""

# Check if database file exists
Write-Host "3. Checking Database..." -ForegroundColor Yellow
if (Test-Path "src/KasserPro.API/kasserpro.db") {
    Write-Host "   Database file exists" -ForegroundColor Green
    
    # Get file size
    $dbFile = Get-Item "src/KasserPro.API/kasserpro.db"
    $sizeKB = [math]::Round($dbFile.Length / 1KB, 2)
    Write-Host "   Database size: $sizeKB KB" -ForegroundColor Cyan
    
    # Check last modified time
    $lastModified = $dbFile.LastWriteTime
    $timeDiff = (Get-Date) - $lastModified
    
    if ($timeDiff.TotalMinutes -lt 10) {
        Write-Host "   Database was recently updated ($([math]::Round($timeDiff.TotalMinutes, 1)) minutes ago)" -ForegroundColor Green
    }
    else {
        Write-Host "   Database was last modified $([math]::Round($timeDiff.TotalHours, 1)) hours ago" -ForegroundColor Yellow
        Write-Host "   You may need to restart the backend to trigger seeding" -ForegroundColor Yellow
    }
}
else {
    Write-Host "   Database file NOT found!" -ForegroundColor Red
    Write-Host "   The database should be created automatically when Backend starts" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "All checks passed!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Open your browser: http://localhost:3001" -ForegroundColor White
Write-Host "2. Login with:" -ForegroundColor White
Write-Host "   Email: admin@kasserpro.com" -ForegroundColor Yellow
Write-Host "   Password: Admin@123" -ForegroundColor Yellow
Write-Host ""
Write-Host "3. Navigate to Inventory from the sidebar" -ForegroundColor White
Write-Host ""
Write-Host "4. You should see:" -ForegroundColor White
Write-Host "   - List of all products with quantities" -ForegroundColor Gray
Write-Host "   - Low stock alerts (if any)" -ForegroundColor Gray
Write-Host "   - Ability to update prices per branch" -ForegroundColor Gray
Write-Host "   - Ability to transfer inventory between branches" -ForegroundColor Gray
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "If inventory page is empty:" -ForegroundColor Yellow
Write-Host "   1. Open browser console (F12)" -ForegroundColor White
Write-Host "   2. Run: localStorage.clear()" -ForegroundColor Yellow
Write-Host "   3. Refresh and login again" -ForegroundColor White
Write-Host ""
Write-Host "Need help? Check SEED_DATA_UPDATE_SUMMARY.md" -ForegroundColor Cyan
Write-Host ""
