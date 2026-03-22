#!/usr/bin/env pwsh
# KasserPro VPS Deployment Script
# Usage: .\deploy-to-vps.ps1 -VpsIp "your-vps-ip" -VpsUser "root"

param(
    [Parameter(Mandatory=$true)]
    [string]$VpsIp,
    
    [Parameter(Mandatory=$false)]
    [string]$VpsUser = "root",
    
    [Parameter(Mandatory=$false)]
    [string]$SshKeyPath = ""
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 KasserPro VPS Deployment Script" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build the application
Write-Host "📦 Step 1: Building application..." -ForegroundColor Yellow
Set-Location backend/KasserPro.API

if (Test-Path "./publish") {
    Remove-Item -Recurse -Force "./publish"
}

dotnet publish -c Release -o ./publish --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build completed successfully" -ForegroundColor Green
Write-Host ""

# Step 2: Create deployment package
Write-Host "📦 Step 2: Creating deployment package..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipFile = "kasserpro-backend-$timestamp.zip"

if (Test-Path $zipFile) {
    Remove-Item -Force $zipFile
}

Compress-Archive -Path ./publish/* -DestinationPath $zipFile

Write-Host "✅ Package created: $zipFile" -ForegroundColor Green
Write-Host ""

# Step 3: Upload to VPS
Write-Host "📤 Step 3: Uploading to VPS..." -ForegroundColor Yellow

$scpCommand = if ($SshKeyPath) {
    "scp -i `"$SshKeyPath`" $zipFile ${VpsUser}@${VpsIp}:/tmp/kasserpro-backend.zip"
} else {
    "scp $zipFile ${VpsUser}@${VpsIp}:/tmp/kasserpro-backend.zip"
}

Write-Host "Executing: $scpCommand" -ForegroundColor Gray
Invoke-Expression $scpCommand

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Upload failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Upload completed" -ForegroundColor Green
Write-Host ""

# Step 4: Deploy on VPS
Write-Host "🔄 Step 4: Deploying on VPS..." -ForegroundColor Yellow

$deployScript = @"
#!/bin/bash
set -e

echo '🔄 Starting deployment...'

# Stop service if running
if systemctl is-active --quiet kasserpro; then
    echo '⏸️  Stopping kasserpro service...'
    systemctl stop kasserpro
fi

# Backup current version
if [ -d /var/www/kasserpro ]; then
    echo '💾 Backing up current version...'
    cp -r /var/www/kasserpro /var/www/kasserpro-backup-\$(date +%Y%m%d-%H%M%S)
    
    # Backup database
    if [ -f /var/www/kasserpro/kasserpro.db ]; then
        cp /var/www/kasserpro/kasserpro.db /var/www/kasserpro/backups/kasserpro-pre-deploy-\$(date +%Y%m%d-%H%M%S).db
    fi
fi

# Create directory if not exists
mkdir -p /var/www/kasserpro
mkdir -p /var/www/kasserpro/backups

# Extract new version
echo '📦 Extracting new version...'
unzip -o /tmp/kasserpro-backend.zip -d /var/www/kasserpro/

# Set permissions
echo '🔐 Setting permissions...'
chown -R kasserpro:kasserpro /var/www/kasserpro
chmod +x /var/www/kasserpro/KasserPro.API

# Start service
echo '▶️  Starting kasserpro service...'
systemctl start kasserpro

# Wait for service to start
sleep 3

# Check status
if systemctl is-active --quiet kasserpro; then
    echo '✅ Deployment completed successfully!'
    systemctl status kasserpro --no-pager
else
    echo '❌ Service failed to start!'
    journalctl -u kasserpro -n 20 --no-pager
    exit 1
fi
"@

$deployScriptPath = "/tmp/deploy-kasserpro.sh"

# Upload deploy script
$deployScript | Out-File -FilePath "./deploy-script.sh" -Encoding UTF8 -NoNewline

$scpDeployCommand = if ($SshKeyPath) {
    "scp -i `"$SshKeyPath`" ./deploy-script.sh ${VpsUser}@${VpsIp}:$deployScriptPath"
} else {
    "scp ./deploy-script.sh ${VpsUser}@${VpsIp}:$deployScriptPath"
}

Invoke-Expression $scpDeployCommand
Remove-Item "./deploy-script.sh"

# Execute deploy script on VPS
$sshCommand = if ($SshKeyPath) {
    "ssh -i `"$SshKeyPath`" ${VpsUser}@${VpsIp} `"chmod +x $deployScriptPath && $deployScriptPath`""
} else {
    "ssh ${VpsUser}@${VpsIp} `"chmod +x $deployScriptPath && $deployScriptPath`""
}

Write-Host "Executing deployment on VPS..." -ForegroundColor Gray
Invoke-Expression $sshCommand

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Deployment failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 Your API should be available at: http://$VpsIp:5243" -ForegroundColor Cyan
Write-Host ""
Write-Host "📊 To view logs, run:" -ForegroundColor Yellow
Write-Host "   ssh ${VpsUser}@${VpsIp} 'journalctl -u kasserpro -f'" -ForegroundColor Gray
Write-Host ""

# Cleanup
Remove-Item $zipFile
Set-Location ../..
