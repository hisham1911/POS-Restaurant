#!/usr/bin/env pwsh

Write-Host "🔍 KasserPro Build Performance Diagnosis" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# Navigate to backend directory
Set-Location $PSScriptRoot

Write-Host "`n📊 Phase 1: Cleaning and Baseline Measurement" -ForegroundColor Yellow

# Clean everything
Write-Host "🧹 Cleaning solution..." -ForegroundColor Green
dotnet clean --verbosity quiet
dotnet nuget locals all --clear

Write-Host "`n⏱️ Measuring overall build time with performance summary..." -ForegroundColor Green
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build -clp:PerformanceSummary --verbosity normal
$stopwatch.Stop()
Write-Host "Total Build Time: $($stopwatch.Elapsed.TotalMinutes.ToString('F2')) minutes" -ForegroundColor Magenta

Write-Host "`n📊 Phase 2: Individual Project Analysis" -ForegroundColor Yellow

# Test individual projects
$projects = @(
    "KasserPro.Domain/KasserPro.Domain.csproj",
    "KasserPro.Application/KasserPro.Application.csproj", 
    "KasserPro.Infrastructure/KasserPro.Infrastructure.csproj",
    "KasserPro.API/KasserPro.API.csproj"
)

foreach ($project in $projects) {
    if (Test-Path $project) {
        Write-Host "Building $project..." -ForegroundColor Green
        $projectStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        dotnet build $project --no-restore --verbosity quiet
        $projectStopwatch.Stop()
        Write-Host "  ⏱️ Time: $($projectStopwatch.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor White
    }
}

Write-Host "`n📊 Phase 3: Reference Analysis" -ForegroundColor Yellow
Write-Host "Project References:" -ForegroundColor Green
dotnet list KasserPro.API/KasserPro.API.csproj reference

Write-Host "`n📊 Phase 4: Single-Threaded Test" -ForegroundColor Yellow
Write-Host "Testing single-threaded build..." -ForegroundColor Green
$singleThreadStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build /m:1 --verbosity quiet --no-restore
$singleThreadStopwatch.Stop()
Write-Host "Single-threaded Build Time: $($singleThreadStopwatch.Elapsed.TotalMinutes.ToString('F2')) minutes" -ForegroundColor Magenta

Write-Host "`n📊 Phase 5: System Resource Check" -ForegroundColor Yellow

# Check available memory
$memory = Get-WmiObject -Class Win32_OperatingSystem
$totalMemory = [math]::Round($memory.TotalVisibleMemorySize / 1MB, 2)
$freeMemory = [math]::Round($memory.FreePhysicalMemory / 1MB, 2)
$usedMemory = $totalMemory - $freeMemory
$memoryUsagePercent = [math]::Round(($usedMemory / $totalMemory) * 100, 1)

Write-Host "💾 Memory Usage: $usedMemory GB / $totalMemory GB ($memoryUsagePercent%)" -ForegroundColor White

# Check disk space
$disk = Get-WmiObject -Class Win32_LogicalDisk -Filter "DeviceID='C:'"
$totalDisk = [math]::Round($disk.Size / 1GB, 2)
$freeDisk = [math]::Round($disk.FreeSpace / 1GB, 2)
$usedDisk = $totalDisk - $freeDisk
$diskUsagePercent = [math]::Round(($usedDisk / $totalDisk) * 100, 1)

Write-Host "💽 Disk Usage: $usedDisk GB / $totalDisk GB ($diskUsagePercent%)" -ForegroundColor White

Write-Host "`n📊 Phase 6: Package Analysis" -ForegroundColor Yellow
Write-Host "NuGet Global Packages Location:" -ForegroundColor Green
dotnet nuget locals global-packages --list

Write-Host "`n🎯 Recommendations:" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan

if ($stopwatch.Elapsed.TotalMinutes -gt 5) {
    Write-Host "❌ Build time is excessive (>5 minutes)" -ForegroundColor Red
    
    if ($memoryUsagePercent -gt 80) {
        Write-Host "⚠️  High memory usage detected - consider closing other applications" -ForegroundColor Yellow
    }
    
    if ($diskUsagePercent -gt 90) {
        Write-Host "⚠️  Low disk space - consider cleaning up disk space" -ForegroundColor Yellow
    }
    
    Write-Host "🔧 Try these immediate fixes:" -ForegroundColor Green
    Write-Host "   1. Run: dotnet restore --use-lock-file" -ForegroundColor White
    Write-Host "   2. Add antivirus exclusions for bin/, obj/, and NuGet cache" -ForegroundColor White
    Write-Host "   3. Check if EF Core model is too large in KasserproContext.cs" -ForegroundColor White
    Write-Host "   4. Consider splitting large DbContext into partial classes" -ForegroundColor White
} else {
    Write-Host "✅ Build time is acceptable" -ForegroundColor Green
}

Write-Host "`n📝 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Review the performance summary above for slow tasks" -ForegroundColor White
Write-Host "2. If Infrastructure/API projects are slowest, focus on EF Core optimization" -ForegroundColor White  
Write-Host "3. If all projects are slow, focus on package resolution optimization" -ForegroundColor White
Write-Host "4. Generate binary log with: dotnet msbuild KasserPro.API/KasserPro.API.csproj /bl:analysis.binlog" -ForegroundColor White

Write-Host "`n✅ Diagnosis Complete!" -ForegroundColor Green