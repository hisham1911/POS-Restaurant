# Build Win7 versions (.NET 6)
$ErrorActionPreference = "Continue"
$logFile = "F:\POS\build-log-win7.txt"
$ProjectRoot = "F:\POS"
$DeploymentRoot = "F:\POS\Deployment"
$ISCC = 'C:\Users\Hisham\AppData\Local\Programs\Inno Setup 6\ISCC.exe'
$net6src = "C:\temp\net6src"

function Log($msg) {
    $ts = Get-Date -Format "HH:mm:ss"
    $line = "[$ts] $msg"
    Write-Host $line
    Add-Content $logFile $line
}

Set-Content $logFile "WIN7 BUILD STARTED $(Get-Date)"

# Step 1: Create .NET 6 source copy
Log "[1/4] Creating .NET 6 compatible source..."
if (Test-Path $net6src) { Remove-Item $net6src -Recurse -Force }
New-Item "$net6src\backend" -ItemType Directory -Force | Out-Null

# Copy backend source
Copy-Item "$ProjectRoot\backend\*" "$net6src\backend\" -Recurse -Force
Log "  Source copied"

# Copy frontend dist to the net6 source wwwroot
$wwwroot = "$net6src\backend\KasserPro.API\wwwroot"
if (Test-Path "$ProjectRoot\backend\KasserPro.API\wwwroot") {
    Log "  wwwroot already included in copied source"
}

# Patch csproj files to target net6.0
$csprojFiles = Get-ChildItem "$net6src\backend" -Filter "*.csproj" -Recurse
foreach ($f in $csprojFiles) {
    $content = Get-Content $f.FullName -Raw
    $content = $content -replace '<TargetFramework>net8\.0</TargetFramework>', '<TargetFramework>net6.0</TargetFramework>'
    Set-Content $f.FullName $content -NoNewline
    Log "  Patched: $($f.Name) -> net6.0"
}

# Create global.json to force .NET 6 SDK
$globalJson = '{"sdk":{"version":"6.0.428","rollForward":"latestFeature"}}'
Set-Content "$net6src\global.json" $globalJson
Log "  global.json created"

# Step 2: Publish .NET 6 x64 (Win7)
Log "[2/4] Publishing .NET 6 x64 (Win7)..."
if (Test-Path 'C:\temp\kasserpro-src-win7-x64') { Remove-Item 'C:\temp\kasserpro-src-win7-x64' -Recurse -Force }
New-Item 'C:\temp\kasserpro-src-win7-x64' -ItemType Directory -Force | Out-Null

$apiProj = "$net6src\backend\KasserPro.API\KasserPro.API.csproj"
$bridgeProj = "$net6src\backend\KasserPro.BridgeApp\KasserPro.BridgeApp.csproj"

dotnet publish $apiProj -c Release -r win-x64 --self-contained -o 'C:\temp\kasserpro-src-win7-x64' -p:PublishSingleFile=false -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ("$_" -match 'error ') { Log "  $_" } }
if ($LASTEXITCODE -ne 0) { Log "  ERROR: Win7 API x64 failed! Exit code: $LASTEXITCODE" } else { Log "  Win7 API x64 OK" }

# BridgeApp targets net8.0-windows and cannot be built with the .NET 6 SDK.
# Copy the already-published net8 bridge from the corresponding main builds.
if (Test-Path 'C:\temp\kasserpro-src\bridge\KasserPro.BridgeApp.exe') {
    Copy-Item 'C:\temp\kasserpro-src\bridge\' 'C:\temp\kasserpro-src-win7-x64\bridge\' -Recurse -Force
    Log "  Win7 Bridge x64: copied from net8 x64 build"
} else {
    Log "  WARNING: net8 x64 bridge not found - run BUILD_DEPLOY_NOW.ps1 first"
}

Copy-Item "$DeploymentRoot\Icons\kasserpro.ico" 'C:\temp\kasserpro-src-win7-x64\' -Force
Copy-Item "$DeploymentRoot\Scripts\KasserPro.url" 'C:\temp\kasserpro-src-win7-x64\' -Force
Log "  .NET 6 x64 published"

# Step 3: Publish .NET 6 x86 (Win7)
Log "[3/4] Publishing .NET 6 x86 (Win7)..."
if (Test-Path 'C:\temp\kasserpro-src-win7-x86') { Remove-Item 'C:\temp\kasserpro-src-win7-x86' -Recurse -Force }
New-Item 'C:\temp\kasserpro-src-win7-x86' -ItemType Directory -Force | Out-Null

dotnet publish $apiProj -c Release -r win-x86 --self-contained -o 'C:\temp\kasserpro-src-win7-x86' -p:PublishSingleFile=false -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ("$_" -match 'error ') { Log "  $_" } }
if ($LASTEXITCODE -ne 0) { Log "  ERROR: Win7 API x86 failed! Exit code: $LASTEXITCODE" } else { Log "  Win7 API x86 OK" }

# BridgeApp targets net8.0-windows - copy from the net8 x86 main build.
if (Test-Path 'C:\temp\kasserpro-src-x86\bridge\KasserPro.BridgeApp.exe') {
    Copy-Item 'C:\temp\kasserpro-src-x86\bridge\' 'C:\temp\kasserpro-src-win7-x86\bridge\' -Recurse -Force
    Log "  Win7 Bridge x86: copied from net8 x86 build"
} else {
    Log "  WARNING: net8 x86 bridge not found - run BUILD_DEPLOY_NOW.ps1 first"
}

Copy-Item "$DeploymentRoot\Icons\kasserpro.ico" 'C:\temp\kasserpro-src-win7-x86\' -Force
Copy-Item "$DeploymentRoot\Scripts\KasserPro.url" 'C:\temp\kasserpro-src-win7-x86\' -Force
Log "  .NET 6 x86 published"

# Step 4: Build Win7 Installers
Log "[4/4] Building Win7 Installers..."
$ISSPath = "$DeploymentRoot\ISS"

# Win7 x64
if (Test-Path 'C:\temp\kasserpro-src-win7-x64\KasserPro.API.exe') {
    Log "  Building KasserPro-Setup-Win7-x64.exe..."
    & $ISCC "$ISSPath\KasserPro-Setup-Win7-x64.iss" 2>&1 | Select-Object -Last 3 | ForEach-Object { Log "  $_" }
} else {
    Log "  SKIP: Win7 x64 source not found (KasserPro.API.exe missing)"
    Get-ChildItem 'C:\temp\kasserpro-src-win7-x64\*.exe' -ErrorAction SilentlyContinue | ForEach-Object { Log "  Found: $($_.Name)" }
}

# Win7 x86
if (Test-Path 'C:\temp\kasserpro-src-win7-x86\KasserPro.API.exe') {
    Log "  Building KasserPro-Setup-Win7-x86.exe..."
    & $ISCC "$ISSPath\KasserPro-Setup-Win7-x86.iss" 2>&1 | Select-Object -Last 3 | ForEach-Object { Log "  $_" }
} else {
    Log "  SKIP: Win7 x86 source not found (KasserPro.API.exe missing)"
    Get-ChildItem 'C:\temp\kasserpro-src-win7-x86\*.exe' -ErrorAction SilentlyContinue | ForEach-Object { Log "  Found: $($_.Name)" }
}

# Summary
Log "========= WIN7 BUILD SUMMARY ========="
Get-ChildItem "$DeploymentRoot\Installers\*.exe" -ErrorAction SilentlyContinue | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 1)
    Log "  $($_.Name) - $sizeMB MB"
}
$total = (Get-ChildItem "$DeploymentRoot\Installers\*.exe" -ErrorAction SilentlyContinue).Count
Log "  Total: $total installers"
Log "======================================="
Add-Content $logFile "WIN7_BUILD_FINISHED"
