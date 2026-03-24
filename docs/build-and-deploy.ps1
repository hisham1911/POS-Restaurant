# ═══════════════════════════════════════════════════════════════════════
# KasserPro Production Build & Deploy Script
# ═══════════════════════════════════════════════════════════════════════
# Version: 1.0
# Date: February 15, 2026
# Purpose: Automated build and packaging for production deployment
# ═══════════════════════════════════════════════════════════════════════

param(
    [string]$Version = "1.0.0",
    [string]$OutputPath = "C:\KasserProRelease",
    [switch]$SkipTests = $false,
    [switch]$CleanBuild = $true
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Colors for output
function Write-Step { param($msg) Write-Host "`n▶ $msg" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "✓ $msg" -ForegroundColor Green }
function Write-Error { param($msg) Write-Host "✗ $msg" -ForegroundColor Red }
function Write-Warning { param($msg) Write-Host "⚠ $msg" -ForegroundColor Yellow }

# ═══════════════════════════════════════════════════════════════════════
# STAGE 0: Pre-Flight Checks
# ═══════════════════════════════════════════════════════════════════════

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       KasserPro Production Build & Deploy v$Version        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

Write-Step "Pre-flight checks..."

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK found: $dotnetVersion"
}
catch {
    Write-Error ".NET SDK not found. Please install .NET 8 SDK."
    exit 1
}

# Check Node.js
try {
    $nodeVersion = node --version
    Write-Success "Node.js found: $nodeVersion"
}
catch {
    Write-Error "Node.js not found. Please install Node.js."
    exit 1
}

# Check npm
try {
    $npmVersion = npm --version
    Write-Success "npm found: $npmVersion"
}
catch {
    Write-Error "npm not found. Please install npm."
    exit 1
}

# Verify we're in the correct directory
if (-not (Test-Path "src\KasserPro.API\KasserPro.API.csproj")) {
    Write-Error "Please run this script from the root of the KasserPro repository."
    exit 1
}

Write-Success "All pre-flight checks passed"

# ═══════════════════════════════════════════════════════════════════════
# STAGE 1: Clean Previous Builds
# ═══════════════════════════════════════════════════════════════════════

if ($CleanBuild) {
    Write-Step "Cleaning previous builds..."
    
    # Clean backend
    if (Test-Path "src\KasserPro.API\bin") {
        Remove-Item "src\KasserPro.API\bin" -Recurse -Force
        Write-Success "Cleaned backend bin"
    }
    if (Test-Path "src\KasserPro.API\obj") {
        Remove-Item "src\KasserPro.API\obj" -Recurse -Force
        Write-Success "Cleaned backend obj"
    }
    
    # Clean frontend
    if (Test-Path "client\dist") {
        Remove-Item "client\dist" -Recurse -Force
        Write-Success "Cleaned frontend dist"
    }
    if (Test-Path "client\node_modules\.vite") {
        Remove-Item "client\node_modules\.vite" -Recurse -Force
        Write-Success "Cleaned Vite cache"
    }
    
    # Clean Bridge App
    if (Test-Path "src\KasserPro.BridgeApp\bin") {
        Remove-Item "src\KasserPro.BridgeApp\bin" -Recurse -Force
        Write-Success "Cleaned Bridge App bin"
    }
    if (Test-Path "src\KasserPro.BridgeApp\obj") {
        Remove-Item "src\KasserPro.BridgeApp\obj" -Recurse -Force
        Write-Success "Cleaned Bridge App obj"
    }
    
    # Clean output directory
    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Recurse -Force
        Write-Success "Cleaned output directory"
    }
}

# ═══════════════════════════════════════════════════════════════════════
# STAGE 2: Build Backend (ASP.NET Core API)
# ═══════════════════════════════════════════════════════════════════════

Write-Step "Building Backend API..."

Push-Location "src\KasserPro.API"

try {
    # Restore packages
    Write-Host "  Restoring NuGet packages..." -ForegroundColor Gray
    dotnet restore | Out-Null
    
    # Build
    Write-Host "  Compiling..." -ForegroundColor Gray
    dotnet build -c Release --no-restore | Out-Null
    
    # Publish
    Write-Host "  Publishing for Windows x64..." -ForegroundColor Gray
    dotnet publish -c Release -r win-x64 --self-contained false -o "publish\backend" --no-build
    
    Write-Success "Backend built successfully"
}
catch {
    Write-Error "Backend build failed: $_"
    Pop-Location
    exit 1
}
finally {
    Pop-Location
}

# Verify output
if (-not (Test-Path "src\KasserPro.API\publish\backend\KasserPro.API.exe")) {
    Write-Error "Backend executable not found after build"
    exit 1
}

$backendSize = (Get-ChildItem "src\KasserPro.API\publish\backend" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "  Backend size: $([math]::Round($backendSize, 2)) MB" -ForegroundColor Gray

# ═══════════════════════════════════════════════════════════════════════
# STAGE 3: Build Frontend (React SPA)
# ═══════════════════════════════════════════════════════════════════════

Write-Step "Building Frontend SPA..."

Push-Location "client"

try {
    # Install dependencies
    Write-Host "  Installing npm packages..." -ForegroundColor Gray
    npm install --silent
    
    # Build for production
    Write-Host "  Building for production..." -ForegroundColor Gray
    $env:NODE_ENV = "production"
    npm run build
    
    Write-Success "Frontend built successfully"
}
catch {
    Write-Error "Frontend build failed: $_"
    Pop-Location
    exit 1
}
finally {
    Pop-Location
}

# Verify output
if (-not (Test-Path "client\dist\index.html")) {
    Write-Error "Frontend index.html not found after build"
    exit 1
}

$frontendSize = (Get-ChildItem "client\dist" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "  Frontend size: $([math]::Round($frontendSize, 2)) MB" -ForegroundColor Gray

# ═══════════════════════════════════════════════════════════════════════
# STAGE 4: Build Desktop Bridge App (WPF)
# ═══════════════════════════════════════════════════════════════════════

Write-Step "Building Desktop Bridge App..."

Push-Location "src\KasserPro.BridgeApp"

try {
    # Restore packages
    Write-Host "  Restoring NuGet packages..." -ForegroundColor Gray
    dotnet restore | Out-Null
    
    # Build
    Write-Host "  Compiling..." -ForegroundColor Gray
    dotnet build -c Release --no-restore | Out-Null
    
    # Publish
    Write-Host "  Publishing for Windows..." -ForegroundColor Gray
    dotnet publish -c Release -r win-x64 --self-contained false -o "publish\bridgeapp" --no-build
    
    Write-Success "Bridge App built successfully"
}
catch {
    Write-Error "Bridge App build failed: $_"
    Pop-Location
    exit 1
}
finally {
    Pop-Location
}

# Verify output
if (-not (Test-Path "src\KasserPro.BridgeApp\publish\bridgeapp\KasserPro.BridgeApp.exe")) {
    Write-Error "Bridge App executable not found after build"
    exit 1
}

$bridgeAppSize = (Get-ChildItem "src\KasserPro.BridgeApp\publish\bridgeapp" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "  Bridge App size: $([math]::Round($bridgeAppSize, 2)) MB" -ForegroundColor Gray

# ═══════════════════════════════════════════════════════════════════════
# STAGE 5: Package for Deployment
# ═══════════════════════════════════════════════════════════════════════

Write-Step "Packaging for deployment..."

# Create output directory structure
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputPath\Backend" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputPath\Frontend" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputPath\BridgeApp" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputPath\Documentation" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputPath\Scripts" -Force | Out-Null

# Copy Backend
Write-Host "  Copying Backend files..." -ForegroundColor Gray
Copy-Item -Path "src\KasserPro.API\publish\backend\*" -Destination "$OutputPath\Backend\" -Recurse -Force

# Copy Frontend
Write-Host "  Copying Frontend files..." -ForegroundColor Gray
Copy-Item -Path "client\dist\*" -Destination "$OutputPath\Frontend\" -Recurse -Force

# Copy Bridge App
Write-Host "  Copying Bridge App files..." -ForegroundColor Gray
Copy-Item -Path "src\KasserPro.BridgeApp\publish\bridgeapp\*" -Destination "$OutputPath\BridgeApp\" -Recurse -Force

# Copy Documentation
Write-Host "  Copying Documentation..." -ForegroundColor Gray
if (Test-Path "docs") {
    Copy-Item -Path "docs\*" -Destination "$OutputPath\Documentation\" -Recurse -Force
}

# Copy deployment guides
$deploymentDocs = @(
    "PRODUCTION_READINESS_AUDIT_REPORT.md",
    "DEPLOYMENT_GUIDE_COMPLETE.md",
    "README.md"
)

foreach ($doc in $deploymentDocs) {
    if (Test-Path $doc) {
        Copy-Item -Path $doc -Destination "$OutputPath\Documentation\" -Force
    }
}

Write-Success "Files packaged successfully"

# ═══════════════════════════════════════════════════════════════════════
# STAGE 6: Create Setup Scripts
# ═══════════════════════════════════════════════════════════════════════

Write-Step "Creating setup scripts..."

# Create install script
$installScript = @"
# KasserPro Installation Script
# Run as Administrator

`$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         KasserPro Installation Wizard v$Version            ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝``n" -ForegroundColor Cyan

# Check if running as Administrator
`$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
`$isAdmin = `$currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not `$isAdmin) {
    Write-Host "⚠ This script must be run as Administrator" -ForegroundColor Yellow
    Write-Host "  Please right-click and select 'Run as Administrator'" -ForegroundColor Gray
    pause
    exit 1
}

# Installation steps
Write-Host "▶ Step 1: Checking .NET Runtime..." -ForegroundColor Cyan
try {
    `$runtimes = dotnet --list-runtimes
    if (`$runtimes -match "Microsoft.NETCore.App 8.0") {
        Write-Host "✓ .NET 8 Runtime found" -ForegroundColor Green
    }
    else {
        Write-Host "✗ .NET 8 Runtime not found" -ForegroundColor Red
        Write-Host "  Please install .NET 8 Runtime from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        pause
        exit 1
    }
}
catch {
    Write-Host "✗ .NET not found" -ForegroundColor Red
    Write-Host "  Please install .NET 8 Runtime from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "``n▶ Step 2: Creating application directory..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path "C:\KasserPro" -Force | Out-Null
New-Item -ItemType Directory -Path "C:\KasserPro\Backend" -Force | Out-Null
New-Item -ItemType Directory -Path "C:\KasserPro\Frontend" -Force | Out-Null
New-Item -ItemType Directory -Path "C:\KasserPro\BridgeApp" -Force | Out-Null
New-Item -ItemType Directory -Path "C:\KasserPro\Documentation" -Force | Out-Null
Write-Host "✓ Directories created" -ForegroundColor Green

Write-Host "``n▶ Step 3: Copying application files..." -ForegroundColor Cyan
Copy-Item -Path ".\Backend\*" -Destination "C:\KasserPro\Backend\" -Recurse -Force
Copy-Item -Path ".\Frontend\*" -Destination "C:\KasserPro\Frontend\" -Recurse -Force
Copy-Item -Path ".\BridgeApp\*" -Destination "C:\KasserPro\BridgeApp\" -Recurse -Force
Copy-Item -Path ".\Documentation\*" -Destination "C:\KasserPro\Documentation\" -Recurse -Force
Write-Host "✓ Files copied" -ForegroundColor Green

Write-Host "``n▶ Step 4: Generating security keys..." -ForegroundColor Cyan
`$jwtKey = [Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])
`$deviceApiKey = [System.Guid]::NewGuid().ToString()

[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", [System.EnvironmentVariableTarget]::Machine)
[System.Environment]::SetEnvironmentVariable("Jwt__Key", `$jwtKey, [System.EnvironmentVariableTarget]::Machine)
[System.Environment]::SetEnvironmentVariable("DeviceApiKey", `$deviceApiKey, [System.EnvironmentVariableTarget]::Machine)

Write-Host "✓ Security keys generated and set" -ForegroundColor Green
Write-Host "  JWT Key: `$jwtKey" -ForegroundColor Gray
Write-Host "  Device API Key: `$deviceApiKey" -ForegroundColor Gray

Write-Host "``n▶ Step 5: Creating Windows services..." -ForegroundColor Cyan
Write-Host "  Please install NSSM to create services" -ForegroundColor Yellow
Write-Host "  Download from: https://nssm.cc/download" -ForegroundColor Gray

Write-Host "``n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              Installation Completed Successfully!              ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════╝``n" -ForegroundColor Green

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "1. Install NSSM and create services (see DEPLOYMENT_GUIDE_COMPLETE.md)" -ForegroundColor Gray
Write-Host "2. Configure IIS for frontend (optional)" -ForegroundColor Gray
Write-Host "3. Configure thermal printer" -ForegroundColor Gray
Write-Host "4. Open browser to: http://localhost:3000" -ForegroundColor Gray
Write-Host "5. Login with: admin@kasserpro.com / Admin@123" -ForegroundColor Gray

pause
"@

$installScript | Out-File -FilePath "$OutputPath\INSTALL.ps1" -Encoding UTF8
Write-Success "Install script created"

# Create uninstall script
$uninstallScript = @"
# KasserPro Uninstallation Script
# Run as Administrator

`$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
Write-Host "║         KasserPro Uninstallation Wizard v$Version          ║" -ForegroundColor Red
Write-Host "╚════════════════════════════════════════════════════════════════╝``n" -ForegroundColor Red

Write-Host "⚠ WARNING: This will remove all KasserPro files and services!" -ForegroundColor Yellow
Write-Host "  Database backups will be preserved in C:\KasserPro\Backend\backups" -ForegroundColor Gray
`$confirm = Read-Host "``nType 'UNINSTALL' to confirm"

if (`$confirm -ne "UNINSTALL") {
    Write-Host "Uninstallation cancelled" -ForegroundColor Green
    pause
    exit 0
}

Write-Host "``n▶ Stopping services..." -ForegroundColor Cyan
try {
    Stop-Service KasserProAPI -ErrorAction SilentlyContinue
    Stop-Service KasserProFrontend -ErrorAction SilentlyContinue
    Stop-Service KasserProBridge -ErrorAction SilentlyContinue
    Write-Host "✓ Services stopped" -ForegroundColor Green
}
catch {
    Write-Host "⚠ Some services could not be stopped" -ForegroundColor Yellow
}

Write-Host "``n▶ Removing services..." -ForegroundColor Cyan
# Note: Requires NSSM to remove services
Write-Host "  Please remove services manually using:" -ForegroundColor Yellow
Write-Host "  nssm remove KasserProAPI confirm" -ForegroundColor Gray
Write-Host "  nssm remove KasserProFrontend confirm" -ForegroundColor Gray
Write-Host "  nssm remove KasserProBridge confirm" -ForegroundColor Gray

Write-Host "``n▶ Preserving backups..." -ForegroundColor Cyan
if (Test-Path "C:\KasserPro\Backend\backups") {
    Copy-Item -Path "C:\KasserPro\Backend\backups" -Destination "C:\KasserPro_Backups" -Recurse -Force
    Write-Host "✓ Backups preserved to C:\KasserPro_Backups" -ForegroundColor Green
}

Write-Host "``n▶ Removing application files..." -ForegroundColor Cyan
Remove-Item -Path "C:\KasserPro" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "✓ Files removed" -ForegroundColor Green

Write-Host "``n▶ Removing environment variables..." -ForegroundColor Cyan
[System.Environment]::SetEnvironmentVariable("Jwt__Key", `$null, [System.EnvironmentVariableTarget]::Machine)
[System.Environment]::SetEnvironmentVariable("DeviceApiKey", `$null, [System.EnvironmentVariableTarget]::Machine)
Write-Host "✓ Environment variables removed" -ForegroundColor Green

Write-Host "``n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║            Uninstallation Completed Successfully!              ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════╝``n" -ForegroundColor Green

Write-Host "Your backups are preserved in: C:\KasserPro_Backups" -ForegroundColor Cyan
pause
"@

$uninstallScript | Out-File -FilePath "$OutputPath\UNINSTALL.ps1" -Encoding UTF8
Write-Success "Uninstall script created"

# ═══════════════════════════════════════════════════════════════════════
# STAGE 7: Create Deployment Package (ZIP)
# ═══════════════════════════════════════════════════════════════════════

Write-Step "Creating deployment package..."

$zipFileName = "KasserPro-v$Version-Production-$(Get-Date -Format 'yyyyMMdd').zip"
$zipPath = Join-Path $PWD $zipFileName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "  Compressing files..." -ForegroundColor Gray
Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath -CompressionLevel Optimal

$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Success "Deployment package created: $zipFileName ($([math]::Round($zipSize, 2)) MB)"

# ═══════════════════════════════════════════════════════════════════════
# STAGE 8: Generate Checksums
# ═══════════════════════════════════════════════════════════════════════

Write-Step "Generating checksums..."

$checksumFile = Join-Path $PWD "KasserPro-v$Version-CHECKSUMS.txt"
$checksums = @()

$checksums += "KasserPro v$Version Build Checksums"
$checksums += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$checksums += "=" * 70
$checksums += ""

# ZIP checksum
$zipHash = Get-FileHash -Path $zipPath -Algorithm SHA256
$checksums += "Deployment Package:"
$checksums += "  File: $zipFileName"
$checksums += "  SHA256: $($zipHash.Hash)"
$checksums += "  Size: $([math]::Round($zipSize, 2)) MB"
$checksums += ""

# Backend checksum
$backendExe = "$OutputPath\Backend\KasserPro.API.exe"
$backendHash = Get-FileHash -Path $backendExe -Algorithm SHA256
$checksums += "Backend API:"
$checksums += "  File: KasserPro.API.exe"
$checksums += "  SHA256: $($backendHash.Hash)"
$checksums += ""

# Bridge App checksum
$bridgeExe = "$OutputPath\BridgeApp\KasserPro.BridgeApp.exe"
$bridgeHash = Get-FileHash -Path $bridgeExe -Algorithm SHA256
$checksums += "Bridge App:"
$checksums += "  File: KasserPro.BridgeApp.exe"
$checksums += "  SHA256: $($bridgeHash.Hash)"
$checksums += ""

$checksums | Out-File -FilePath $checksumFile -Encoding UTF8
Write-Success "Checksums generated: KasserPro-v$Version-CHECKSUMS.txt"

# ═══════════════════════════════════════════════════════════════════════
# STAGE 9: Final Summary
# ═══════════════════════════════════════════════════════════════════════

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              Build Completed Successfully! ✓                   ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Green

Write-Host "Build Summary:" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Gray
Write-Host "  Backend Size: $([math]::Round($backendSize, 2)) MB" -ForegroundColor Gray
Write-Host "  Frontend Size: $([math]::Round($frontendSize, 2)) MB" -ForegroundColor Gray
Write-Host "  Bridge App Size: $([math]::Round($bridgeAppSize, 2)) MB" -ForegroundColor Gray
Write-Host "  Total Package Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Gray
Write-Host "  Output Directory: $OutputPath" -ForegroundColor Gray
Write-Host "  Deployment Package: $zipFileName" -ForegroundColor Gray

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "1. Copy $zipFileName to deployment machine" -ForegroundColor Gray
Write-Host "2. Extract and run INSTALL.ps1 as Administrator" -ForegroundColor Gray
Write-Host "3. Follow DEPLOYMENT_GUIDE_COMPLETE.md for detailed instructions" -ForegroundColor Gray
Write-Host "4. Review PRODUCTION_READINESS_AUDIT_REPORT.md for important notes" -ForegroundColor Gray

Write-Host "`n⚡ Ready for Production Deployment! ⚡`n" -ForegroundColor Green
