# KasserPro Full Build + Deploy Script
# Builds all 4 installer versions
$ErrorActionPreference = "Continue"
$logFile = "F:\POS\build-log.txt"
$ProjectRoot = "F:\POS"
$DeploymentRoot = "F:\POS\Deployment"
$ISCC = 'C:\Users\Hisham\AppData\Local\Programs\Inno Setup 6\ISCC.exe'

function Log($msg) {
    $ts = Get-Date -Format "HH:mm:ss"
    $line = "[$ts] $msg"
    Write-Host $line
    Add-Content $logFile $line
}

# Clear log
Set-Content $logFile "BUILD STARTED $(Get-Date)"

Log "========================================="
Log "   KasserPro Full Build - All 4 Versions"
Log "========================================="

# ---- Step 1: Frontend ----
Log "[1/8] Frontend already built - checking..."
if (Test-Path "$ProjectRoot\frontend\dist\index.html") {
    Log "  OK: Frontend dist exists"
} else {
    Log "  Building frontend..."
    Push-Location "$ProjectRoot\frontend"
    npm run build 2>&1 | Out-Null
    Pop-Location
    Log "  Frontend build done"
}

# ---- Step 2: .NET 8 x64 (Win10/11) ----
Log "[2/8] Publishing .NET 8 x64 (Win10/11)..."
$apiProj = "$ProjectRoot\backend\KasserPro.API\KasserPro.API.csproj"
$bridgeProj = "$ProjectRoot\backend\KasserPro.BridgeApp\KasserPro.BridgeApp.csproj"

# Clean output
if (Test-Path 'C:\temp\kasserpro-src') { Remove-Item 'C:\temp\kasserpro-src' -Recurse -Force }
New-Item 'C:\temp\kasserpro-src' -ItemType Directory -Force | Out-Null

dotnet publish $apiProj -c Release -r win-x64 --self-contained -o 'C:\temp\kasserpro-src' -p:PublishSingleFile=false -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ($_ -match 'error|warning') { Log "  $_" } }
if ($LASTEXITCODE -ne 0) { Log "  ERROR: API x64 publish failed!"; } else { Log "  API x64 OK" }

dotnet publish $bridgeProj -c Release -r win-x64 --self-contained -o 'C:\temp\kasserpro-src\bridge' -p:PublishSingleFile=true -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ($_ -match 'error|warning') { Log "  $_" } }
if ($LASTEXITCODE -ne 0) { Log "  ERROR: Bridge x64 publish failed!"; } else { Log "  Bridge x64 OK" }

Copy-Item "$DeploymentRoot\Icons\kasserpro.ico" 'C:\temp\kasserpro-src\' -Force
Copy-Item "$DeploymentRoot\Scripts\KasserPro.url" 'C:\temp\kasserpro-src\' -Force
Log "  .NET 8 x64 published"

# ---- Step 3: .NET 8 x86 (Win10/11) ----
Log "[3/8] Publishing .NET 8 x86 (Win10/11)..."
if (Test-Path 'C:\temp\kasserpro-src-x86') { Remove-Item 'C:\temp\kasserpro-src-x86' -Recurse -Force }
New-Item 'C:\temp\kasserpro-src-x86' -ItemType Directory -Force | Out-Null

dotnet publish $apiProj -c Release -r win-x86 --self-contained -o 'C:\temp\kasserpro-src-x86' -p:PublishSingleFile=false -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ($_ -match 'error|warning') { Log "  $_" } }
if ($LASTEXITCODE -ne 0) { Log "  ERROR: API x86 publish failed!"; } else { Log "  API x86 OK" }

dotnet publish $bridgeProj -c Release -r win-x86 --self-contained -o 'C:\temp\kasserpro-src-x86\bridge' -p:PublishSingleFile=true -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ($_ -match 'error|warning') { Log "  $_" } }
if ($LASTEXITCODE -ne 0) { Log "  ERROR: Bridge x86 publish failed!"; } else { Log "  Bridge x86 OK" }

Copy-Item "$DeploymentRoot\Icons\kasserpro.ico" 'C:\temp\kasserpro-src-x86\' -Force
Copy-Item "$DeploymentRoot\Scripts\KasserPro.url" 'C:\temp\kasserpro-src-x86\' -Force
Log "  .NET 8 x86 published"

# ---- Step 4 & 5: .NET 6 for Win7 ----
# Check if .NET 6 SDK is available
$net6sdk = dotnet --list-sdks 2>&1 | Select-String "6\.0\."
if ($net6sdk) {
    Log "[4/8] Preparing .NET 6 source for Win7..."

    # Always recreate net6 source copy to pick up latest changes
    $net6src = "C:\temp\net6src"
    Log "  Creating .NET 6 compatible source copy..."
    if (Test-Path $net6src) { Remove-Item $net6src -Recurse -Force }
    if ($true) {

        # Copy backend source
        New-Item "$net6src\backend" -ItemType Directory -Force | Out-Null
        Copy-Item "$ProjectRoot\backend\*" "$net6src\backend\" -Recurse -Force

        # Remove obj/bin folders from copy (stale auto-generated files)
        Get-ChildItem "$net6src\backend" -Directory -Recurse | Where-Object { $_.Name -in 'obj','bin' } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

        # Patch csproj files to target net6.0
        $csprojFiles = Get-ChildItem "$net6src\backend" -Filter "*.csproj" -Recurse
        foreach ($f in $csprojFiles) {
            $content = Get-Content $f.FullName -Raw
            $content = $content -replace '<TargetFramework>net8\.0</TargetFramework>', '<TargetFramework>net6.0</TargetFramework>'
            $content = $content -replace '<TargetFramework>net8\.0-windows</TargetFramework>', '<TargetFramework>net6.0-windows</TargetFramework>'
            # Downgrade 8.0.x package versions for net6 compat
            $content = $content -replace 'Version="8\.0\.\d+"', 'Version="6.0.36"'
            # Remove Microsoft.AspNetCore.OpenApi (doesn't exist for net6, introduced in net7)
            $content = $content -replace '(?m)^\s*<PackageReference\s+Include="Microsoft\.AspNetCore\.OpenApi"[^/]*/>\s*\r?\n', ''
            # Downgrade FluentValidation.DependencyInjectionExtensions (12.x = net8 only, 11.5.1 = last net6-compatible)
            $content = $content -replace '(Include="FluentValidation\.DependencyInjectionExtensions"\s+Version=")[^"]+"', '${1}11.5.1"'
            Set-Content $f.FullName $content -NoNewline
            Log "  Patched: $($f.Name) -> net6.0"
        }

        # Fix BridgeApp package versions to avoid NU1605 downgrade errors
        $bridgeCsproj = "$net6src\backend\KasserPro.BridgeApp\KasserPro.BridgeApp.csproj"
        if (Test-Path $bridgeCsproj) {
            $bc = Get-Content $bridgeCsproj -Raw
            $bc = $bc -replace '"Microsoft.Extensions.DependencyInjection"\s+Version="[^"]+"', '"Microsoft.Extensions.DependencyInjection" Version="6.0.2"'
            $bc = $bc -replace '"Microsoft.Extensions.Logging"\s+Version="[^"]+"', '"Microsoft.Extensions.Logging" Version="6.0.1"'
            Set-Content $bridgeCsproj $bc -NoNewline
            Log "  BridgeApp packages fixed for net6"
        }

        # Patch ALL .cs files: remove RateLimiter references (requires .NET 7+)
        $csFiles = Get-ChildItem "$net6src\backend" -Filter "*.cs" -Recurse
        foreach ($csFile in $csFiles) {
            $code = Get-Content $csFile.FullName -Raw
            $changed = $false
            if ($code -match 'RateLimiting|RateLimiter') {
                # Remove RateLimiting using statements
                $code = $code -replace 'using System\.Threading\.RateLimiting;\r?\n', ''
                $code = $code -replace 'using Microsoft\.AspNetCore\.RateLimiting;\r?\n', ''
                # Remove [EnableRateLimiting("...")] attributes
                $code = $code -replace '\s*\[EnableRateLimiting\("[^"]*"\)\]\r?\n', "`n"
                # Remove the entire AddRateLimiter block (has nested braces - match via RejectionStatusCode anchor)
                $code = $code -replace '(?s)builder\.Services\.AddRateLimiter\(.*?RejectionStatusCode[^\n]*\n\}\);\r?\n', ''
                # Remove app.UseRateLimiter();
                $code = $code -replace 'app\.UseRateLimiter\(\);', '// app.UseRateLimiter(); // .NET 7+ only'
                Set-Content $csFile.FullName $code -NoNewline
                Log "  $($csFile.Name): RateLimiting removed"
                $changed = $true
            }
        }
        if (-not $changed) { Log "  No RateLimiting references found" }

        # Create a global.json to force .NET 6 SDK
        $globalJson = @{
            sdk = @{
                version = "6.0.428"
                rollForward = "latestMajor"
            }
        } | ConvertTo-Json
        Set-Content "$net6src\global.json" $globalJson
    }

    # Publish .NET 6 x64 (Win7)
    Log "[5/8] Publishing .NET 6 x64 (Win7)..."
    if (Test-Path 'C:\temp\kasserpro-src-win7-x64') { Remove-Item 'C:\temp\kasserpro-src-win7-x64' -Recurse -Force }
    New-Item 'C:\temp\kasserpro-src-win7-x64' -ItemType Directory -Force | Out-Null

    dotnet publish "$net6src\backend\KasserPro.API\KasserPro.API.csproj" -c Release -r win-x64 --self-contained -o 'C:\temp\kasserpro-src-win7-x64' -p:PublishSingleFile=false -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ($_ -match 'error|warning') { Log "  $_" } }
    if ($LASTEXITCODE -ne 0) { Log "  ERROR: Win7 API x64 failed!"; } else { Log "  Win7 API x64 OK" }

    # BridgeApp: copy pre-built net8 bridge (net6 BridgeApp has package compat issues)
    if (Test-Path 'C:\temp\kasserpro-src\bridge\KasserPro.BridgeApp.exe') {
        Copy-Item 'C:\temp\kasserpro-src\bridge' 'C:\temp\kasserpro-src-win7-x64\bridge' -Recurse -Force
        Log "  Win7 Bridge x64: copied from net8 x64 build"
    } else {
        Log "  WARNING: net8 x64 bridge not found - Bridge will be missing from Win7 x64 installer"
    }

    Copy-Item "$DeploymentRoot\Icons\kasserpro.ico" 'C:\temp\kasserpro-src-win7-x64\' -Force
    Copy-Item "$DeploymentRoot\Scripts\KasserPro.url" 'C:\temp\kasserpro-src-win7-x64\' -Force
    Log "  .NET 6 x64 published"

    # Publish .NET 6 x86 (Win7)
    Log "[6/8] Publishing .NET 6 x86 (Win7)..."
    if (Test-Path 'C:\temp\kasserpro-src-win7-x86') { Remove-Item 'C:\temp\kasserpro-src-win7-x86' -Recurse -Force }
    New-Item 'C:\temp\kasserpro-src-win7-x86' -ItemType Directory -Force | Out-Null

    dotnet publish "$net6src\backend\KasserPro.API\KasserPro.API.csproj" -c Release -r win-x86 --self-contained -o 'C:\temp\kasserpro-src-win7-x86' -p:PublishSingleFile=false -p:PublishReadyToRun=false 2>&1 | ForEach-Object { if ($_ -match 'error|warning') { Log "  $_" } }
    if ($LASTEXITCODE -ne 0) { Log "  ERROR: Win7 API x86 failed!"; } else { Log "  Win7 API x86 OK" }

    # BridgeApp: copy pre-built net8 bridge
    if (Test-Path 'C:\temp\kasserpro-src-x86\bridge\KasserPro.BridgeApp.exe') {
        Copy-Item 'C:\temp\kasserpro-src-x86\bridge' 'C:\temp\kasserpro-src-win7-x86\bridge' -Recurse -Force
        Log "  Win7 Bridge x86: copied from net8 x86 build"
    } else {
        Log "  WARNING: net8 x86 bridge not found - Bridge will be missing from Win7 x86 installer"
    }

    Copy-Item "$DeploymentRoot\Icons\kasserpro.ico" 'C:\temp\kasserpro-src-win7-x86\' -Force
    Copy-Item "$DeploymentRoot\Scripts\KasserPro.url" 'C:\temp\kasserpro-src-win7-x86\' -Force
    Log "  .NET 6 x86 published"
} else {
    Log "[4/8] SKIP: .NET 6 SDK not found - Win7 builds skipped"
    Log "[5/8] SKIP: (see above)"
    Log "[6/8] SKIP: (see above)"
}

# ---- Step 7: Build Installers ----
Log "[7/8] Building Installers..."
$ISSPath = "$DeploymentRoot\ISS"

# Clean old installers
Remove-Item "$DeploymentRoot\Installers\*.exe" -Force -ErrorAction SilentlyContinue

# x64 Win10/11
if (Test-Path 'C:\temp\kasserpro-src\KasserPro.API.exe') {
    Log "  Building KasserPro-Setup.exe (Win10/11 x64)..."
    & $ISCC "$ISSPath\KasserPro-Setup.iss" 2>&1 | Select-Object -Last 3 | ForEach-Object { Log "  $_" }
} else {
    Log "  SKIP: x64 source not found"
}

# x86 Win10/11
if (Test-Path 'C:\temp\kasserpro-src-x86\KasserPro.API.exe') {
    Log "  Building KasserPro-Setup-x86.exe (Win10/11 x86)..."
    & $ISCC "$ISSPath\KasserPro-Setup-x86.iss" 2>&1 | Select-Object -Last 3 | ForEach-Object { Log "  $_" }
} else {
    Log "  SKIP: x86 source not found"
}

# Win7 x64
if (Test-Path 'C:\temp\kasserpro-src-win7-x64\KasserPro.API.exe') {
    Log "  Building KasserPro-Setup-Win7-x64.exe..."
    & $ISCC "$ISSPath\KasserPro-Setup-Win7-x64.iss" 2>&1 | Select-Object -Last 3 | ForEach-Object { Log "  $_" }
} else {
    Log "  SKIP: Win7 x64 source not found"
}

# Win7 x86
if (Test-Path 'C:\temp\kasserpro-src-win7-x86\KasserPro.API.exe') {
    Log "  Building KasserPro-Setup-Win7-x86.exe..."
    & $ISCC "$ISSPath\KasserPro-Setup-Win7-x86.iss" 2>&1 | Select-Object -Last 3 | ForEach-Object { Log "  $_" }
} else {
    Log "  SKIP: Win7 x86 source not found"
}

# ---- Step 8: Summary ----
Log "[8/8] ========= BUILD SUMMARY ========="
$installers = Get-ChildItem "$DeploymentRoot\Installers\*.exe" -ErrorAction SilentlyContinue
if ($installers) {
    foreach ($f in $installers) {
        $sizeMB = [math]::Round($f.Length / 1MB, 1)
        Log "  $($f.Name) - $sizeMB MB"
    }
    Log "  Total: $($installers.Count) installers built"
} else {
    Log "  WARNING: No installers found!"
}

Log "========================================="
Log "BUILD COMPLETED $(Get-Date)"
Add-Content $logFile "BUILD_FINISHED"
