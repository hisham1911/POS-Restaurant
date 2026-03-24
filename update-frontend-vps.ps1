#!/usr/bin/env pwsh
# Quick Frontend Update to VPS

$VPS_IP = "168.231.106.139"
$VPS_USER = "root"
$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

Write-Host "Updating frontend on VPS" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build Frontend
Write-Host "Step 1: Building frontend" -ForegroundColor Yellow
$originalLocation = Get-Location
$frontendPath = Join-Path $scriptRoot "frontend"
Set-Location $frontendPath

if (Test-Path "dist") {
    Remove-Item -Recurse -Force "dist"
}

npm run build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    Set-Location $originalLocation
    exit 1
}

Write-Host "Build completed" -ForegroundColor Green
Write-Host ""

# Step 2: Create package
Write-Host "Step 2: Creating package" -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipFile = "frontend-$timestamp.zip"

Compress-Archive -Path dist/* -DestinationPath $zipFile -Force

Write-Host "Package created: $zipFile" -ForegroundColor Green
Write-Host ""

# Step 3: Upload
Write-Host "Step 3: Uploading to VPS" -ForegroundColor Yellow
scp $zipFile ${VPS_USER}@${VPS_IP}:/tmp/frontend-update.zip

if ($LASTEXITCODE -ne 0) {
    Write-Host "Upload failed" -ForegroundColor Red
    if (Test-Path $zipFile) {
        Remove-Item $zipFile -Force
    }
    Set-Location $originalLocation
    exit 1
}

Write-Host "Upload completed" -ForegroundColor Green
Write-Host ""

# Step 4: Deploy on VPS
Write-Host "Step 4: Deploying on VPS" -ForegroundColor Yellow
$remoteCmd = "if [ -d /var/www/kasserpro/wwwroot ]; then cp -r /var/www/kasserpro/wwwroot /var/www/kasserpro/wwwroot-backup-`$(date +%Y%m%d-%H%M%S); echo BACKUP_CREATED; fi; mkdir -p /var/www/kasserpro/wwwroot; unzip -o /tmp/frontend-update.zip -d /var/www/kasserpro/wwwroot/ >/dev/null; chown -R root:root /var/www/kasserpro/wwwroot; echo FRONTEND_UPDATED"
ssh ${VPS_USER}@${VPS_IP} "bash -lc '$remoteCmd'"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Remote deploy failed" -ForegroundColor Red
    Set-Location $originalLocation
    exit 1
}

Write-Host "Frontend updated successfully" -ForegroundColor Green
Write-Host ""

# Cleanup
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}
Set-Location $originalLocation

Write-Host "Test URL:" -ForegroundColor Yellow
Write-Host "  http://${VPS_IP}:5243" -ForegroundColor Gray
Write-Host ""
