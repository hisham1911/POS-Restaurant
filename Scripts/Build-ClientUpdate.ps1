# KasserPro - Build Update Package for Client
# This script creates an update ZIP that the client can extract over their installation
# It EXCLUDES: database files, appsettings.json (user config), logs

param(
    [string]$OutputPath = "d:\مسح\POS\KasserPro-Update.zip"
)

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  KasserPro - Building Client Update Package" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

$sourceDir = "d:\مسح\POS\output\kasserpro-allinone"
$tempDir = "d:\مسح\POS\output\_update_temp"

# Clean temp
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item $tempDir -ItemType Directory -Force | Out-Null

Write-Host "  [1/4] Publishing backend..." -ForegroundColor Yellow
dotnet publish "d:\مسح\POS\backend\KasserPro.API\KasserPro.API.csproj" -c Release -r win-x64 --self-contained true -o $sourceDir 2>&1 | Out-Null
Write-Host "        Done" -ForegroundColor Green

Write-Host "  [2/4] Publishing printer bridge..." -ForegroundColor Yellow
dotnet publish "d:\مسح\POS\backend\KasserPro.BridgeApp\KasserPro.BridgeApp.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o "d:\مسح\POS\output\desktop-app" 2>&1 | Out-Null
Copy-Item "d:\مسح\POS\output\desktop-app\KasserPro.BridgeApp.exe" "$sourceDir\KasserPro.BridgeApp.exe" -Force
Write-Host "        Done" -ForegroundColor Green

Write-Host "  [3/4] Copying update files (excluding database & user config)..." -ForegroundColor Yellow

# Copy everything EXCEPT database, user settings, logs
Get-ChildItem $sourceDir -File | Where-Object {
    $_.Name -notmatch "^kasserpro\.db" -and
    $_.Name -ne "appsettings.json" -and
    $_.Name -notmatch "\.log$" -and
    $_.Name -notmatch "\.backup$"
} | Copy-Item -Destination $tempDir -Force

# Copy wwwroot
if (Test-Path "$sourceDir\wwwroot") {
    Copy-Item "$sourceDir\wwwroot" "$tempDir\wwwroot" -Recurse -Force
}

Write-Host "        Done" -ForegroundColor Green

Write-Host "  [4/4] Creating ZIP..." -ForegroundColor Yellow

# Remove old ZIP
if (Test-Path $OutputPath) { Remove-Item $OutputPath -Force }

# Create ZIP
Compress-Archive -Path "$tempDir\*" -DestinationPath $OutputPath -Force

# Cleanup
Remove-Item $tempDir -Recurse -Force

$zip = Get-Item $OutputPath
Write-Host "        Done" -ForegroundColor Green

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  Update package ready!" -ForegroundColor Green
Write-Host ""
Write-Host "  File: $($zip.Name)" -ForegroundColor White
Write-Host "  Size: $([Math]::Round($zip.Length/1MB, 1)) MB" -ForegroundColor White
Write-Host ""
Write-Host "  Instructions for client:" -ForegroundColor Yellow
Write-Host "    1. Close KasserPro (close the server window)" -ForegroundColor White
Write-Host "    2. Run UPDATE.bat (backs up database)" -ForegroundColor White
Write-Host "    3. Extract this ZIP into the app folder" -ForegroundColor White
Write-Host "    4. Click 'Replace All' when asked" -ForegroundColor White
Write-Host "    5. Run START.bat" -ForegroundColor White
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
