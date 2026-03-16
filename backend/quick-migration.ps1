# Quick Migration Creation Script
Write-Host "Creating fresh migration..." -ForegroundColor Cyan

# Ensure migrations folder exists
if (!(Test-Path "backend/KasserPro.Infrastructure/Migrations")) {
    New-Item -ItemType Directory -Path "backend/KasserPro.Infrastructure/Migrations" -Force | Out-Null
}

# Create migration
$result = dotnet ef migrations add InitialCreate `
    --context AppDbContext `
    --project backend/KasserPro.Infrastructure/KasserPro.Infrastructure.csproj `
    --startup-project backend/KasserPro.API/KasserPro.API.csproj `
    --no-build `
    2>&1

Write-Host $result

# Check if migration was created
$migrationFiles = Get-ChildItem -Path "backend/KasserPro.Infrastructure/Migrations" -Filter "*.cs" -ErrorAction SilentlyContinue
if ($migrationFiles) {
    Write-Host "`nMigration files created:" -ForegroundColor Green
    $migrationFiles | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor White }
} else {
    Write-Host "`nNo migration files found!" -ForegroundColor Red
}
