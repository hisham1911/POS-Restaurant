#!/usr/bin/env pwsh
# Full KasserPro Deployment to VPS
# Fixes existing backend + Deploys KasserPro

param(
    [Parameter(Mandatory=$false)]
    [string]$VpsIp = "168.231.106.139",
    
    [Parameter(Mandatory=$false)]
    [string]$VpsUser = "root",
    
    [Parameter(Mandatory=$false)]
    [switch]$FixExistingBackend = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$DeployKasserPro = $true
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Full VPS Deployment Script" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "VPS: $VpsIp" -ForegroundColor Gray
Write-Host ""

# Step 1: Fix existing backend (optional)
if ($FixExistingBackend) {
    Write-Host "🔧 Step 1: Fixing existing AZ Backend..." -ForegroundColor Yellow
    
    Write-Host "Uploading fix script..." -ForegroundColor Gray
    scp fix-dotnet-backend.sh ${VpsUser}@${VpsIp}:/tmp/
    
    Write-Host "Running fix script..." -ForegroundColor Gray
    ssh ${VpsUser}@${VpsIp} "bash /tmp/fix-dotnet-backend.sh"
    
    Write-Host "✅ Backend fix completed" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "Testing AZ Backend..." -ForegroundColor Gray
    ssh ${VpsUser}@${VpsIp} "curl -s http://localhost:8080/api/health || echo 'Still not working'"
    Write-Host ""
}

# Step 2: Build KasserPro
if ($DeployKasserPro) {
    Write-Host "📦 Step 2: Building KasserPro..." -ForegroundColor Yellow
    
    $originalLocation = Get-Location
    Set-Location backend/KasserPro.API
    
    if (Test-Path "./publish") {
        Remove-Item -Recurse -Force "./publish"
    }
    
    Write-Host "Running dotnet publish..." -ForegroundColor Gray
    dotnet publish -c Release -o ./publish --self-contained false
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed!" -ForegroundColor Red
        Set-Location $originalLocation
        exit 1
    }
    
    Write-Host "✅ Build completed" -ForegroundColor Green
    Write-Host ""
    
    # Step 3: Create package
    Write-Host "📦 Step 3: Creating deployment package..." -ForegroundColor Yellow
    
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $zipFile = "kasserpro-backend-$timestamp.zip"
    
    if (Test-Path $zipFile) {
        Remove-Item -Force $zipFile
    }
    
    Compress-Archive -Path ./publish/* -DestinationPath $zipFile
    Write-Host "✅ Package created: $zipFile" -ForegroundColor Green
    Write-Host ""
    
    # Step 4: Upload files
    Write-Host "📤 Step 4: Uploading to VPS..." -ForegroundColor Yellow
    
    Write-Host "Uploading KasserPro package..." -ForegroundColor Gray
    scp $zipFile ${VpsUser}@${VpsIp}:/tmp/kasserpro-backend.zip
    
    Write-Host "Uploading deployment script..." -ForegroundColor Gray
    Set-Location $originalLocation
    scp deploy-kasserpro-alongside.sh ${VpsUser}@${VpsIp}:/tmp/
    
    Write-Host "✅ Upload completed" -ForegroundColor Green
    Write-Host ""
    
    # Step 5: Deploy on VPS
    Write-Host "🚀 Step 5: Deploying on VPS..." -ForegroundColor Yellow
    
    ssh ${VpsUser}@${VpsIp} "bash /tmp/deploy-kasserpro-alongside.sh"
    
    Write-Host ""
    Write-Host "✅ Deployment completed!" -ForegroundColor Green
    Write-Host ""
    
    # Step 6: Test
    Write-Host "🧪 Step 6: Testing deployment..." -ForegroundColor Yellow
    
    Write-Host "Testing KasserPro API..." -ForegroundColor Gray
    ssh ${VpsUser}@${VpsIp} "curl -s http://localhost:5243/api/health || echo 'API not responding yet'"
    
    Write-Host ""
    Write-Host "Checking service status..." -ForegroundColor Gray
    ssh ${VpsUser}@${VpsIp} "systemctl is-active kasserpro && echo '✅ Service is running' || echo '❌ Service is not running'"
    
    # Cleanup
    Set-Location backend/KasserPro.API
    Remove-Item $zipFile
    Set-Location $originalLocation
}

Write-Host ""
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "🎉 Deployment Complete!" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📊 Summary:" -ForegroundColor Yellow
Write-Host "  - AZ International: https://azinternational-eg.com" -ForegroundColor Gray
Write-Host "  - KasserPro API: http://$VpsIp:5243" -ForegroundColor Gray
Write-Host ""
Write-Host "🔧 Useful Commands:" -ForegroundColor Yellow
Write-Host "  View KasserPro logs:" -ForegroundColor Gray
Write-Host "    ssh $VpsUser@$VpsIp 'journalctl -u kasserpro -f'" -ForegroundColor White
Write-Host ""
Write-Host "  Restart KasserPro:" -ForegroundColor Gray
Write-Host "    ssh $VpsUser@$VpsIp 'systemctl restart kasserpro'" -ForegroundColor White
Write-Host ""
Write-Host "  Check status:" -ForegroundColor Gray
Write-Host "    ssh $VpsUser@$VpsIp 'systemctl status kasserpro'" -ForegroundColor White
Write-Host ""
Write-Host "⚠️  IMPORTANT:" -ForegroundColor Red
Write-Host "  1. Change JWT Key in /var/www/kasserpro/appsettings.json" -ForegroundColor Yellow
Write-Host "  2. Setup SSL if using domain" -ForegroundColor Yellow
Write-Host "  3. Configure firewall: ufw allow 5243/tcp" -ForegroundColor Yellow
Write-Host ""
