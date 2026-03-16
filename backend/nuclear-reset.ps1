# Nuclear Reset Script for KasserPro Database
# This script performs a complete clean slate reset

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "KasserPro Nuclear Reset" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop any running KasserPro.API processes
Write-Host "Step 1: Stopping any running API processes..." -ForegroundColor Yellow
$processes = Get-Process -Name "KasserPro.API" -ErrorAction SilentlyContinue
if ($processes) {
    $processes | Stop-Process -Force
    Write-Host "   OK - Stopped $($processes.Count) process(es)" -ForegroundColor Green
    Start-Sleep -Seconds 2
} else {
    Write-Host "   OK - No running processes found" -ForegroundColor Green
}

# Step 2: Delete all database files
Write-Host ""
Write-Host "Step 2: Deleting all database files..." -ForegroundColor Yellow
$dbFiles = @(
    "backend/KasserPro.API/kasserpro.db",
    "backend/KasserPro.API/kasserpro.db-shm",
    "backend/KasserPro.API/kasserpro.db-wal"
)

foreach ($file in $dbFiles) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "   OK - Deleted $file" -ForegroundColor Green
    }
}

# Delete bin database files
if (Test-Path "backend/KasserPro.API/bin") {
    Get-ChildItem -Path "backend/KasserPro.API/bin" -Filter "*.db*" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
    Write-Host "   OK - Cleaned bin folder" -ForegroundColor Green
}

# Step 3: Verify migrations are deleted
Write-Host ""
Write-Host "Step 3: Verifying migrations folder is clean..." -ForegroundColor Yellow
$migrationCount = (Get-ChildItem -Path "backend/KasserPro.Infrastructure/Migrations" -File -ErrorAction SilentlyContinue | Measure-Object).Count
if ($migrationCount -eq 0) {
    Write-Host "   OK - Migrations folder is empty" -ForegroundColor Green
} else {
    Write-Host "   WARNING - Found $migrationCount migration files" -ForegroundColor Yellow
    Write-Host "   Deleting all migrations..." -ForegroundColor Yellow
    Remove-Item -Path "backend/KasserPro.Infrastructure/Migrations/*" -Force -Recurse
    Write-Host "   OK - Migrations deleted" -ForegroundColor Green
}

# Step 4: Clean build artifacts
Write-Host ""
Write-Host "Step 4: Cleaning build artifacts..." -ForegroundColor Yellow
dotnet clean backend/KasserPro.API/KasserPro.API.csproj --verbosity quiet
Write-Host "   OK - Build artifacts cleaned" -ForegroundColor Green

# Step 5: Create fresh migration
Write-Host ""
Write-Host "Step 5: Creating fresh unified migration..." -ForegroundColor Yellow
$migrationResult = dotnet ef migrations add InitialUnifiedSchema `
    --context AppDbContext `
    --project backend/KasserPro.Infrastructure/KasserPro.Infrastructure.csproj `
    --startup-project backend/KasserPro.API/KasserPro.API.csproj `
    2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "   OK - Migration created successfully" -ForegroundColor Green
} else {
    Write-Host "   FAIL - Migration creation failed" -ForegroundColor Red
    Write-Host $migrationResult
    exit 1
}

# Step 6: Apply migration to create database
Write-Host ""
Write-Host "Step 6: Applying migration to create fresh database..." -ForegroundColor Yellow
$updateResult = dotnet ef database update `
    --context AppDbContext `
    --project backend/KasserPro.Infrastructure/KasserPro.Infrastructure.csproj `
    --startup-project backend/KasserPro.API/KasserPro.API.csproj `
    2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "   OK - Database created successfully" -ForegroundColor Green
} else {
    Write-Host "   FAIL - Database update failed" -ForegroundColor Red
    Write-Host $updateResult
    exit 1
}

# Step 7: Verify database file exists
Write-Host ""
Write-Host "Step 7: Verifying database file..." -ForegroundColor Yellow
if (Test-Path "backend/KasserPro.API/kasserpro.db") {
    $dbSize = (Get-Item "backend/KasserPro.API/kasserpro.db").Length / 1KB
    Write-Host "   OK - Database file exists ($([math]::Round($dbSize, 2)) KB)" -ForegroundColor Green
} else {
    Write-Host "   FAIL - Database file not found" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Nuclear Reset Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "   - All old migrations deleted" -ForegroundColor White
Write-Host "   - Fresh unified migration created" -ForegroundColor White
Write-Host "   - Clean database with hardened schema" -ForegroundColor White
Write-Host "   - RowVersion configured for SQLite" -ForegroundColor White
Write-Host "   - Query splitting enabled" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Start the API: dotnet run --project backend/KasserPro.API" -ForegroundColor Gray
Write-Host "   2. Test order creation" -ForegroundColor Gray
Write-Host "   3. Verify no NULL exceptions" -ForegroundColor Gray
Write-Host ""
