# Reset database and re-seed
Write-Host "Resetting database and re-seeding..."
Write-Host ""

$dbPath = "backend/KasserPro.API/kasserpro.db"
$dbShmPath = "backend/KasserPro.API/kasserpro.db-shm"
$dbWalPath = "backend/KasserPro.API/kasserpro.db-wal"

# Stop backend if running
Write-Host "1. Stopping backend (if running)..."
Get-Process -Name "KasserPro.API" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Delete database files
Write-Host "2. Deleting old database files..."
if (Test-Path $dbPath) { Remove-Item $dbPath -Force; Write-Host "   Deleted kasserpro.db" }
if (Test-Path $dbShmPath) { Remove-Item $dbShmPath -Force; Write-Host "   Deleted kasserpro.db-shm" }
if (Test-Path $dbWalPath) { Remove-Item $dbWalPath -Force; Write-Host "   Deleted kasserpro.db-wal" }

Write-Host ""
Write-Host "3. Database reset complete!"
Write-Host ""
Write-Host "Now run the backend to create fresh database:"
Write-Host "   cd backend/KasserPro.API"
Write-Host "   dotnet run"
Write-Host ""
Write-Host "Test credentials after seeding:"
Write-Host "   Admin: admin@kasserpro.com / Admin@123"
Write-Host "   Cashier: mohamed@kasserpro.com / 123456"
