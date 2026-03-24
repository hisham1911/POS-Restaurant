#!/usr/bin/env pwsh
# Manual KasserPro Deployment - Step by Step

$VPS_IP = "168.231.106.139"
$VPS_USER = "root"
$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

Write-Host "Manual KasserPro Deployment" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build
Write-Host "Step 1: Building KasserPro" -ForegroundColor Yellow
Set-Location (Join-Path $scriptRoot "backend/KasserPro.API")

if (Test-Path "./publish") {
    Remove-Item -Recurse -Force "./publish"
}

dotnet publish -c Release -o ./publish --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "Build completed" -ForegroundColor Green
Write-Host ""

# Step 2: Create package
Write-Host "Step 2: Creating package" -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipFile = "kasserpro-$timestamp.zip"

Compress-Archive -Path ./publish/* -DestinationPath $zipFile -Force

Write-Host "Package created: $zipFile" -ForegroundColor Green
Write-Host ""

# Step 3: Upload
Write-Host "Step 3: Uploading to VPS" -ForegroundColor Yellow
scp $zipFile ${VPS_USER}@${VPS_IP}:/tmp/kasserpro-backend.zip

if ($LASTEXITCODE -ne 0) {
    Write-Host "Upload failed" -ForegroundColor Red
    exit 1
}

Write-Host "Upload completed" -ForegroundColor Green
Write-Host ""

# Step 4: Upload deployment script
Write-Host "Step 4: Uploading deployment script" -ForegroundColor Yellow
Set-Location $scriptRoot
scp deploy-kasserpro-alongside.sh ${VPS_USER}@${VPS_IP}:/tmp/

Write-Host "Script uploaded" -ForegroundColor Green
Write-Host ""

# Step 5: Execute deployment
Write-Host "Step 5: Executing deployment on VPS" -ForegroundColor Yellow
Write-Host "This will:" -ForegroundColor Gray
Write-Host "  - Extract files to /var/www/kasserpro" -ForegroundColor Gray
Write-Host "  - Create systemd service" -ForegroundColor Gray
Write-Host "  - Configure Nginx" -ForegroundColor Gray
Write-Host "  - Start service on port 5243" -ForegroundColor Gray
Write-Host ""

ssh ${VPS_USER}@${VPS_IP} "bash /tmp/deploy-kasserpro-alongside.sh"

Write-Host ""
Write-Host "Deployment script executed" -ForegroundColor Green
Write-Host ""

# Step 6: Verify
Write-Host "Step 6: Verifying deployment" -ForegroundColor Yellow

Write-Host "Checking service status..." -ForegroundColor Gray
ssh ${VPS_USER}@${VPS_IP} "systemctl status kasserpro --no-pager | head -10"

Write-Host ""
Write-Host "Checking port 5243..." -ForegroundColor Gray
ssh ${VPS_USER}@${VPS_IP} "ss -tulpn | grep :5243 || echo 'Port not listening'"

Write-Host ""
Write-Host "Testing health endpoint..." -ForegroundColor Gray
ssh ${VPS_USER}@${VPS_IP} "curl -s http://localhost:5243/api/health || echo 'Not responding'"

Write-Host ""
Write-Host "===============================" -ForegroundColor Cyan
Write-Host "Deployment Complete" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Open firewall: ssh root@$VPS_IP 'ufw allow 5243/tcp'" -ForegroundColor Gray
Write-Host "2. Test API: curl http://$VPS_IP:5243/api/health" -ForegroundColor Gray
Write-Host "3. View logs: ssh root@$VPS_IP 'journalctl -u kasserpro -f'" -ForegroundColor Gray
Write-Host ""

# Cleanup
Set-Location (Join-Path $scriptRoot "backend/KasserPro.API")
Remove-Item $zipFile -Force
Set-Location $scriptRoot

Write-Host "Cleanup completed" -ForegroundColor Green
