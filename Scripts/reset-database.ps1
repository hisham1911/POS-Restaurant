# Reset Database Script
# This script will delete the old database and create a new one with all migrations

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  KasserPro - Database Reset Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Navigate to API directory
Write-Host "[1/4] Navigating to API directory..." -ForegroundColor Yellow
Set-Location -Path "src\KasserPro.API"

# Step 2: Delete old database
Write-Host "[2/4] Deleting old database files..." -ForegroundColor Yellow
if (Test-Path "kasserpro.db") {
    Remove-Item "kasserpro.db" -Force
    Write-Host "  ✓ Deleted kasserpro.db" -ForegroundColor Green
}
if (Test-Path "kasserpro.db-shm") {
    Remove-Item "kasserpro.db-shm" -Force
    Write-Host "  ✓ Deleted kasserpro.db-shm" -ForegroundColor Green
}
if (Test-Path "kasserpro.db-wal") {
    Remove-Item "kasserpro.db-wal" -Force
    Write-Host "  ✓ Deleted kasserpro.db-wal" -ForegroundColor Green
}

# Step 3: Apply migrations
Write-Host "[3/4] Applying migrations..." -ForegroundColor Yellow
dotnet ef database update

if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Migrations applied successfully" -ForegroundColor Green
} else {
    Write-Host "  ✗ Failed to apply migrations" -ForegroundColor Red
    exit 1
}

# Step 4: Start backend
Write-Host "[4/4] Starting backend..." -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Database reset complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Backend will start now" -ForegroundColor White
Write-Host "  2. Open a new terminal and run: cd client && npm run dev" -ForegroundColor White
Write-Host "  3. Login with: admin@kasserpro.com / Admin@123" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop the backend" -ForegroundColor Gray
Write-Host ""

dotnet run
