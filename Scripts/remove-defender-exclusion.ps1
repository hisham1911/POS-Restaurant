# ============================================
# Windows Defender Exclusion Removal Script
# KasserPro Project
# ============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Windows Defender Exclusion Removal" -ForegroundColor Cyan
Write-Host "  KasserPro Project" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    Write-Host ""
    Write-Host "Solution:" -ForegroundColor Yellow
    Write-Host "1. Open PowerShell as Administrator" -ForegroundColor Yellow
    Write-Host "2. Navigate to project folder" -ForegroundColor Yellow
    Write-Host "3. Run: .\remove-defender-exclusion.ps1" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

$projectPath = Get-Location | Select-Object -ExpandProperty Path

Write-Host "Project Path: $projectPath" -ForegroundColor Green
Write-Host ""

# Remove main folder exclusion
Write-Host "Removing main folder exclusion..." -ForegroundColor Yellow
try {
    Remove-MpPreference -ExclusionPath $projectPath
    Write-Host "SUCCESS: Removed exclusion for: $projectPath" -ForegroundColor Green
} catch {
    Write-Host "WARNING: $_" -ForegroundColor Yellow
}

# Remove subfolder exclusions
$subFolders = @(
    "backend",
    "backend\KasserPro.API\bin",
    "backend\KasserPro.API\obj",
    "client\node_modules",
    "client\.next"
)

Write-Host ""
Write-Host "Removing subfolder exclusions..." -ForegroundColor Yellow

foreach ($folder in $subFolders) {
    $fullPath = Join-Path $projectPath $folder
    try {
        Remove-MpPreference -ExclusionPath $fullPath
        Write-Host "SUCCESS: Removed exclusion for: $folder" -ForegroundColor Green
    } catch {
        Write-Host "SKIP: $folder" -ForegroundColor Gray
    }
}

# Remove process exclusions
Write-Host ""
Write-Host "Removing process exclusions..." -ForegroundColor Yellow

$processes = @(
    "dotnet.exe",
    "node.exe",
    "npm.exe",
    "yarn.exe"
)

foreach ($process in $processes) {
    try {
        Remove-MpPreference -ExclusionProcess $process
        Write-Host "SUCCESS: Removed exclusion for: $process" -ForegroundColor Green
    } catch {
        Write-Host "SKIP: $process" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUCCESS: All exclusions removed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
