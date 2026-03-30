# Full KasserPro Deployment to VPS (PowerShell 5.1 compatible)

param(
    [Parameter(Mandatory = $false)]
    [string]$VpsIp = "168.231.106.139",

    [Parameter(Mandatory = $false)]
    [string]$VpsUser = "root",

    [Parameter(Mandatory = $false)]
    [switch]$FixExistingBackend,

    [Parameter(Mandatory = $false)]
    [switch]$SkipKasserPro
)

$ErrorActionPreference = "Stop"

Write-Host "Starting full VPS deployment" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host "VPS: $VpsIp" -ForegroundColor Gray
Write-Host ""

if ($FixExistingBackend) {
    Write-Host "Step 1: Fixing existing AZ backend" -ForegroundColor Yellow

    if (-not (Test-Path "./fix-dotnet-backend.sh")) {
        throw "Missing file: fix-dotnet-backend.sh"
    }

    scp ./fix-dotnet-backend.sh ${VpsUser}@${VpsIp}:/tmp/
    ssh ${VpsUser}@${VpsIp} "bash /tmp/fix-dotnet-backend.sh"

    Write-Host "Validating AZ backend health" -ForegroundColor Gray
    ssh ${VpsUser}@${VpsIp} "bash -lc 'curl -s http://localhost:8080/health || echo AZ_backend_not_working'"
    Write-Host ""
}

if (-not $SkipKasserPro) {
    Write-Host "Step 2: Building KasserPro backend" -ForegroundColor Yellow

    $originalLocation = Get-Location
    $scriptRoot = $PSScriptRoot
    if (-not $scriptRoot) {
        $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    $apiPath = Join-Path (Split-Path $scriptRoot -Parent) "backend/KasserPro.API"

    if (-not (Test-Path $apiPath)) {
        throw "Missing path: $apiPath"
    }

    Set-Location $apiPath

    if (Test-Path "./publish") {
        Remove-Item -Recurse -Force "./publish"
    }

    dotnet publish -c Release -o ./publish --self-contained false
    if ($LASTEXITCODE -ne 0) {
        Set-Location $originalLocation
        throw "dotnet publish failed"
    }

    Write-Host "Step 3: Creating deployment package" -ForegroundColor Yellow

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $zipFile = "kasserpro-backend-$timestamp.zip"

    if (Test-Path $zipFile) {
        Remove-Item -Force $zipFile
    }

    Compress-Archive -Path ./publish/* -DestinationPath $zipFile -Force

    Write-Host "Step 4: Uploading artifacts to VPS" -ForegroundColor Yellow

    if (-not (Test-Path (Join-Path $scriptRoot "deploy-kasserpro-alongside.sh"))) {
        Set-Location $originalLocation
        throw "Missing file: deploy-kasserpro-alongside.sh"
    }

    scp ./$zipFile ${VpsUser}@${VpsIp}:/tmp/kasserpro-backend.zip
    Set-Location $scriptRoot
    scp ./deploy-kasserpro-alongside.sh ${VpsUser}@${VpsIp}:/tmp/

    Write-Host "Step 5: Deploying on VPS" -ForegroundColor Yellow
    ssh ${VpsUser}@${VpsIp} "bash /tmp/deploy-kasserpro-alongside.sh"

    Write-Host "Step 6: Verifying deployment" -ForegroundColor Yellow
    ssh ${VpsUser}@${VpsIp} "bash -lc 'curl -s http://localhost:5243/health || echo kasserpro_health_not_ready'"
    ssh ${VpsUser}@${VpsIp} "bash -lc 'if systemctl is-active kasserpro >/dev/null 2>&1; then echo kasserpro_service_running; else echo kasserpro_service_not_running; fi'"

    Set-Location $apiPath
    if (Test-Path $zipFile) {
        Remove-Item -Force $zipFile
    }
    Set-Location $originalLocation
}

Write-Host ""
Write-Host "============================" -ForegroundColor Cyan
Write-Host "Deployment complete" -ForegroundColor Green
Write-Host "============================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  - AZ International: https://azinternational-eg.com" -ForegroundColor Gray
Write-Host "  - KasserPro API: http://${VpsIp}:5243" -ForegroundColor Gray
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  ssh $VpsUser@$VpsIp 'journalctl -u kasserpro -f'" -ForegroundColor White
Write-Host "  ssh $VpsUser@$VpsIp 'systemctl restart kasserpro'" -ForegroundColor White
Write-Host "  ssh $VpsUser@$VpsIp 'systemctl status kasserpro'" -ForegroundColor White
