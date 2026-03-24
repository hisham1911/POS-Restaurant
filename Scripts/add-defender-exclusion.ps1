# ============================================
# Windows Defender Exclusion Script
# KasserPro Project
# ============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Windows Defender Exclusion Setup" -ForegroundColor Cyan
Write-Host "  KasserPro Project" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    Write-Host ""
    Write-Host "Solution:" -ForegroundColor Yellow
    Write-Host "1. Open PowerShell as Administrator (Run as Administrator)" -ForegroundColor Yellow
    Write-Host "2. Navigate to project folder" -ForegroundColor Yellow
    Write-Host "3. Run: .\add-defender-exclusion.ps1" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

# Get current project path
$projectPath = Get-Location | Select-Object -ExpandProperty Path

Write-Host "Project Path: $projectPath" -ForegroundColor Green
Write-Host ""

# Add main folder exclusion
Write-Host "Adding main folder exclusion..." -ForegroundColor Yellow
try {
    Add-MpPreference -ExclusionPath $projectPath
    Write-Host "SUCCESS: Added exclusion for: $projectPath" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to add exclusion: $_" -ForegroundColor Red
}

# Add subfolder exclusions
$subFolders = @(
    "backend",
    "backend\KasserPro.API\bin",
    "backend\KasserPro.API\obj",
    "client\node_modules",
    "client\.next"
)

Write-Host ""
Write-Host "Adding subfolder exclusions..." -ForegroundColor Yellow

foreach ($folder in $subFolders) {
    $fullPath = Join-Path $projectPath $folder
    if (Test-Path $fullPath) {
        try {
            Add-MpPreference -ExclusionPath $fullPath
            Write-Host "SUCCESS: Added exclusion for: $folder" -ForegroundColor Green
        } catch {
            Write-Host "WARNING: $folder - $_" -ForegroundColor Yellow
        }
    } else {
        Write-Host "SKIP (not found): $folder" -ForegroundColor Gray
    }
}

# Add process exclusions
Write-Host ""
Write-Host "Adding process exclusions..." -ForegroundColor Yellow

$processes = @(
    "dotnet.exe",
    "node.exe",
    "npm.exe",
    "yarn.exe"
)

foreach ($process in $processes) {
    try {
        Add-MpPreference -ExclusionProcess $process
        Write-Host "SUCCESS: Added exclusion for process: $process" -ForegroundColor Green
    } catch {
        Write-Host "WARNING: $process - $_" -ForegroundColor Yellow
    }
}

# Display current exclusions
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Current Exclusions" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$preferences = Get-MpPreference

Write-Host ""
Write-Host "Excluded Folders:" -ForegroundColor Green
$preferences.ExclusionPath | Where-Object { $_ -like "*$projectPath*" } | ForEach-Object {
    Write-Host "  - $_" -ForegroundColor White
}

Write-Host ""
Write-Host "Excluded Processes:" -ForegroundColor Green
$preferences.ExclusionProcess | Where-Object { $processes -contains $_ } | ForEach-Object {
    Write-Host "  - $_" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUCCESS: Completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Notes:" -ForegroundColor Yellow
Write-Host "  - Build and development should now be faster" -ForegroundColor White
Write-Host "  - To remove exclusions, run: .\remove-defender-exclusion.ps1" -ForegroundColor White
Write-Host ""
