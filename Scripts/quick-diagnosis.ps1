#!/usr/bin/env powershell

Write-Host "🔍 KasserPro Build Performance - Quick Diagnosis" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# Test 1: Simple Console App Build Time
Write-Host "`n🧪 Test 1: Creating simple console app..." -ForegroundColor Yellow
$tempDir = Join-Path $env:TEMP "BuildTest"
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null
Set-Location $tempDir

Write-Host "Creating test project..." -ForegroundColor Green
dotnet new console -n SimpleTest --force | Out-Null

Write-Host "Building test project..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build SimpleTest/SimpleTest.csproj --verbosity quiet
$stopwatch.Stop()
$simpleTestTime = $stopwatch.Elapsed.TotalSeconds

Write-Host "Simple console app build time: $($simpleTestTime.ToString('F1'))s" -ForegroundColor White

# Test 2: System Resources
Write-Host "`n💾 Test 2: System Resources Check" -ForegroundColor Yellow

# Memory check
$memory = Get-WmiObject -Class Win32_OperatingSystem
$totalMemoryGB = [math]::Round($memory.TotalVisibleMemorySize / 1MB, 2)
$freeMemoryGB = [math]::Round($memory.FreePhysicalMemory / 1MB, 2)
$usedMemoryGB = $totalMemoryGB - $freeMemoryGB
$memoryUsagePercent = [math]::Round(($usedMemoryGB / $totalMemoryGB) * 100, 1)

Write-Host "Memory: $usedMemoryGB GB / $totalMemoryGB GB ($memoryUsagePercent%)" -ForegroundColor White

# Disk check
$disk = Get-WmiObject -Class Win32_LogicalDisk -Filter "DeviceID='C:'"
$totalDiskGB = [math]::Round($disk.Size / 1GB, 2)
$freeDiskGB = [math]::Round($disk.FreeSpace / 1GB, 2)
$diskUsagePercent = [math]::Round((($totalDiskGB - $freeDiskGB) / $totalDiskGB) * 100, 1)

Write-Host "Disk C: $freeDiskGB GB free / $totalDiskGB GB total ($diskUsagePercent% used)" -ForegroundColor White

# Disk type check
try {
    $physicalDisks = Get-PhysicalDisk | Where-Object { $_.DeviceID -eq 0 }
    if ($physicalDisks) {
        $diskType = $physicalDisks[0].MediaType
        Write-Host "Primary disk type: $diskType" -ForegroundColor White
    }
} catch {
    Write-Host "Could not determine disk type" -ForegroundColor Yellow
}

# Test 3: .NET SDK Version
Write-Host "`n🔧 Test 3: .NET SDK Information" -ForegroundColor Yellow
$dotnetVersion = dotnet --version
Write-Host ".NET SDK Version: $dotnetVersion" -ForegroundColor White

# Test 4: NuGet Cache Size
Write-Host "`n📦 Test 4: NuGet Cache Information" -ForegroundColor Yellow
$nugetGlobalPath = dotnet nuget locals global-packages --list
if ($nugetGlobalPath -match "global-packages: (.+)") {
    $cachePath = $matches[1]
    if (Test-Path $cachePath) {
        $cacheSize = (Get-ChildItem $cachePath -Recurse | Measure-Object -Property Length -Sum).Sum
        $cacheSizeGB = [math]::Round($cacheSize / 1GB, 2)
        Write-Host "NuGet cache size: $cacheSizeGB GB" -ForegroundColor White
        Write-Host "NuGet cache path: $cachePath" -ForegroundColor White
    }
}

# Test 5: Windows Defender Status
Write-Host "`n🛡️ Test 5: Windows Defender Status" -ForegroundColor Yellow
try {
    $defenderStatus = Get-MpComputerStatus -ErrorAction SilentlyContinue
    if ($defenderStatus) {
        Write-Host "Real-time protection: $($defenderStatus.RealTimeProtectionEnabled)" -ForegroundColor White
        Write-Host "Antimalware enabled: $($defenderStatus.AntivirusEnabled)" -ForegroundColor White
    }
} catch {
    Write-Host "Could not check Windows Defender status" -ForegroundColor Yellow
}

# Cleanup
Set-Location $PSScriptRoot
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

# Analysis and Recommendations
Write-Host "`n📊 Analysis & Recommendations" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

if ($simpleTestTime -gt 10) {
    Write-Host "❌ CRITICAL: Simple console app took $($simpleTestTime.ToString('F1'))s" -ForegroundColor Red
    Write-Host "   This indicates a system-wide performance issue" -ForegroundColor Red
    Write-Host "   🔧 Immediate actions needed:" -ForegroundColor Yellow
    Write-Host "      1. Add antivirus exclusions for .NET folders" -ForegroundColor White
    Write-Host "      2. Check if running on HDD instead of SSD" -ForegroundColor White
    Write-Host "      3. Close memory-intensive applications" -ForegroundColor White
} elseif ($simpleTestTime -gt 5) {
    Write-Host "⚠️  WARNING: Simple console app took $($simpleTestTime.ToString('F1'))s" -ForegroundColor Yellow
    Write-Host "   This is slower than expected but manageable" -ForegroundColor Yellow
    Write-Host "   🔧 Recommended actions:" -ForegroundColor Yellow
    Write-Host "      1. Add antivirus exclusions" -ForegroundColor White
    Write-Host "      2. Clear NuGet cache if very large" -ForegroundColor White
} else {
    Write-Host "✅ GOOD: Simple console app took $($simpleTestTime.ToString('F1'))s" -ForegroundColor Green
    Write-Host "   System performance is acceptable" -ForegroundColor Green
    Write-Host "   🔧 Focus on project-specific optimizations" -ForegroundColor Yellow
}

if ($memoryUsagePercent -gt 85) {
    Write-Host "⚠️  HIGH MEMORY USAGE: $memoryUsagePercent%" -ForegroundColor Yellow
    Write-Host "   Close unnecessary applications" -ForegroundColor White
}

if ($diskUsagePercent -gt 90) {
    Write-Host "⚠️  LOW DISK SPACE: $diskUsagePercent% used" -ForegroundColor Yellow
    Write-Host "   Free up disk space" -ForegroundColor White
}

Write-Host "`n🎯 Next Steps:" -ForegroundColor Cyan
if ($simpleTestTime -gt 10) {
    Write-Host "1. Add these folders to antivirus exclusions:" -ForegroundColor White
    Write-Host "   - F:\POS\backend\" -ForegroundColor Gray
    Write-Host "   - C:\Users\$env:USERNAME\.nuget\packages\" -ForegroundColor Gray
    Write-Host "   - C:\Program Files\dotnet\" -ForegroundColor Gray
    Write-Host "2. Restart computer after adding exclusions" -ForegroundColor White
    Write-Host "3. Re-run this diagnosis script" -ForegroundColor White
} else {
    Write-Host "1. Apply project-specific optimizations" -ForegroundColor White
    Write-Host "2. Consider splitting large projects" -ForegroundColor White
    Write-Host "3. Use incremental builds" -ForegroundColor White
}

Write-Host "`n✅ Diagnosis Complete!" -ForegroundColor Green