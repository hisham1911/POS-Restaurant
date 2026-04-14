# KasserPro Deployment Build Script
# Version: 2.1
# Last Updated: 2026-04-08

param(
    [switch]$SkipFrontend,
    [switch]$SkipBackend,
    [switch]$SkipInstallers
)

$ErrorActionPreference = "Stop"
# Use script location to determine paths
$ScriptDir = $PSScriptRoot
$DeploymentRoot = Split-Path $ScriptDir -Parent
$ProjectRoot = Split-Path $DeploymentRoot -Parent
$ISSPath = Join-Path $DeploymentRoot 'ISS'
$ISCC = 'C:\Users\Hisham\AppData\Local\Programs\Inno Setup 6\ISCC.exe'
$Net6ApiProject = 'C:\temp\net6src\backend\KasserPro.API\KasserPro.API.csproj'
$Net6BridgeProject = 'C:\temp\net6src\backend\KasserPro.BridgeApp\KasserPro.BridgeApp.csproj'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipInstallers -and -not (Test-Path $ISCC)) {
    throw "Inno Setup compiler not found at: $ISCC"
}

if (-not $SkipBackend) {
    if (-not (Test-Path $Net6ApiProject)) {
        throw "Win7 API project not found: $Net6ApiProject"
    }

    if (-not (Test-Path $Net6BridgeProject)) {
        throw "Win7 Bridge project not found: $Net6BridgeProject"
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   KasserPro Build All Versions v2.1   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 0: Clean old installers
if (-not $SkipInstallers) {
    Write-Host "[0/7] Cleaning old installers..." -ForegroundColor Yellow
    $installersPath = Join-Path $DeploymentRoot 'Installers'
    Remove-Item (Join-Path $installersPath '*.exe') -Force -ErrorAction SilentlyContinue
}

# Step 1: Build Frontend
if (-not $SkipFrontend) {
    Write-Host "[1/7] Building Frontend (React)" -ForegroundColor Green
    $frontendDir = Join-Path $ProjectRoot 'frontend'
    Push-Location $frontendDir
    try {
        Invoke-CheckedCommand "Frontend build" {
            npm run build
        }
    }
    finally {
        Pop-Location
    }
    Write-Host "Frontend build complete" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[1/7] Skipping Frontend build" -ForegroundColor Gray
}

# Step 2: Publish .NET 8 x64 (Win10/11)
if (-not $SkipBackend) {
    Write-Host "[2/7] Publishing .NET 8 x64 Backend for Win10-11" -ForegroundColor Green
    $apiProject = Join-Path $ProjectRoot 'backend\KasserPro.API\KasserPro.API.csproj'
    Invoke-CheckedCommand ".NET 8 x64 API publish" {
        dotnet publish $apiProject `
            -c Release -r win-x64 --self-contained -o C:\temp\kasserpro-src `
            -p:PublishSingleFile=false -p:PublishReadyToRun=false
    }

    $bridgeProject = Join-Path $ProjectRoot 'backend\KasserPro.BridgeApp\KasserPro.BridgeApp.csproj'
    Invoke-CheckedCommand ".NET 8 x64 Bridge publish" {
        dotnet publish $bridgeProject `
            -c Release -r win-x64 --self-contained -o C:\temp\kasserpro-src `
            -p:PublishSingleFile=true -p:PublishReadyToRun=false
    }

    $iconPath = Join-Path $DeploymentRoot 'Icons\kasserpro.ico'
    Copy-Item $iconPath 'C:\temp\kasserpro-src\' -Force
    Write-Host ".NET 8 x64 published" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[2/7] Skipping .NET 8 x64 build" -ForegroundColor Gray
}

# Step 3: Publish .NET 8 x86 (Win10/11)
if (-not $SkipBackend) {
    Write-Host "[3/7] Publishing .NET 8 x86 Backend for Win10-11" -ForegroundColor Green
    $apiProject = Join-Path $ProjectRoot 'backend\KasserPro.API\KasserPro.API.csproj'
    Invoke-CheckedCommand ".NET 8 x86 API publish" {
        dotnet publish $apiProject `
            -c Release -r win-x86 --self-contained -o C:\temp\kasserpro-src-x86 `
            -p:PublishSingleFile=false -p:PublishReadyToRun=false
    }

    $bridgeProject = Join-Path $ProjectRoot 'backend\KasserPro.BridgeApp\KasserPro.BridgeApp.csproj'
    Invoke-CheckedCommand ".NET 8 x86 Bridge publish" {
        dotnet publish $bridgeProject `
            -c Release -r win-x86 --self-contained -o C:\temp\kasserpro-src-x86 `
            -p:PublishSingleFile=true -p:PublishReadyToRun=false
    }

    $iconPath = Join-Path $DeploymentRoot 'Icons\kasserpro.ico'
    Copy-Item $iconPath 'C:\temp\kasserpro-src-x86\' -Force
    Write-Host ".NET 8 x86 published" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[3/7] Skipping .NET 8 x86 build" -ForegroundColor Gray
}

# Step 4: Publish .NET 6 x64 (Win7)
if (-not $SkipBackend) {
    Write-Host "[4/7] Publishing .NET 6 x64 Backend for Win7" -ForegroundColor Green
    Invoke-CheckedCommand ".NET 6 x64 API publish" {
        dotnet publish $Net6ApiProject `
            -c Release -r win-x64 --self-contained -o C:\temp\kasserpro-src-win7-x64 `
            -p:PublishSingleFile=false -p:PublishReadyToRun=false
    }

    Invoke-CheckedCommand ".NET 6 x64 Bridge publish" {
        dotnet publish $Net6BridgeProject `
            -c Release -r win-x64 --self-contained -o C:\temp\kasserpro-src-win7-x64 `
            -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:LangVersion=11.0
    }

    $iconPath = Join-Path $DeploymentRoot 'Icons\kasserpro.ico'
    Copy-Item $iconPath 'C:\temp\kasserpro-src-win7-x64\' -Force
    Write-Host ".NET 6 x64 published" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[4/7] Skipping .NET 6 x64 build" -ForegroundColor Gray
}

# Step 5: Publish .NET 6 x86 (Win7)
if (-not $SkipBackend) {
    Write-Host "[5/7] Publishing .NET 6 x86 Backend for Win7" -ForegroundColor Green
    Invoke-CheckedCommand ".NET 6 x86 API publish" {
        dotnet publish $Net6ApiProject `
            -c Release -r win-x86 --self-contained -o C:\temp\kasserpro-src-win7-x86 `
            -p:PublishSingleFile=false -p:PublishReadyToRun=false
    }

    Invoke-CheckedCommand ".NET 6 x86 Bridge publish" {
        dotnet publish $Net6BridgeProject `
            -c Release -r win-x86 --self-contained -o C:\temp\kasserpro-src-win7-x86 `
            -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:LangVersion=11.0
    }

    $iconPath = Join-Path $DeploymentRoot 'Icons\kasserpro.ico'
    Copy-Item $iconPath 'C:\temp\kasserpro-src-win7-x86\' -Force
    Write-Host ".NET 6 x86 published" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[5/7] Skipping .NET 6 x86 build" -ForegroundColor Gray
}

# Step 6: Build All Installers
if (-not $SkipInstallers) {
    Write-Host "[6/7] Building Installers" -ForegroundColor Green

    Write-Host "  Building KasserPro-Setup.exe Win10-11 x64" -ForegroundColor Cyan
    $issFile = Join-Path $ISSPath 'KasserPro-Setup.iss'
    Invoke-CheckedCommand "Installer build: KasserPro-Setup.exe" {
        & $ISCC $issFile
    }

    Write-Host "  Building KasserPro-Setup-x86.exe Win10-11 x86" -ForegroundColor Cyan
    $issFile = Join-Path $ISSPath 'KasserPro-Setup-x86.iss'
    Invoke-CheckedCommand "Installer build: KasserPro-Setup-x86.exe" {
        & $ISCC $issFile
    }

    Write-Host "  Building KasserPro-Setup-Win7-x64.exe Win7 x64" -ForegroundColor Cyan
    $issFile = Join-Path $ISSPath 'KasserPro-Setup-Win7-x64.iss'
    Invoke-CheckedCommand "Installer build: KasserPro-Setup-Win7-x64.exe" {
        & $ISCC $issFile
    }

    Write-Host "  Building KasserPro-Setup-Win7-x86.exe Win7 x86" -ForegroundColor Cyan
    $issFile = Join-Path $ISSPath 'KasserPro-Setup-Win7-x86.iss'
    Invoke-CheckedCommand "Installer build: KasserPro-Setup-Win7-x86.exe" {
        & $ISCC $issFile
    }

    Write-Host "All installers built successfully" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[6/7] Skipping installer builds" -ForegroundColor Gray
}

# Step 7: Summary
Write-Host "[7/7] Build Summary" -ForegroundColor Green
Write-Host "==================" -ForegroundColor Green
$installerDir = Join-Path $DeploymentRoot 'Installers'
Get-ChildItem (Join-Path $installerDir '*.exe') | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name) - $sizeMB MB" -ForegroundColor White
}
Write-Host ""
Write-Host "All builds completed successfully!" -ForegroundColor Green
Write-Host "Output: $installerDir" -ForegroundColor Cyan
