#!/usr/bin/env powershell

Write-Host "🚀 Ultra-Fast Build Script" -ForegroundColor Cyan

# Set environment variables for maximum performance
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:MSBUILD_EXE_PATH = ""

Write-Host "Building Domain..." -ForegroundColor Yellow
$stopwatch1 = [System.Diagnostics.Stopwatch]::StartNew()
dotnet msbuild KasserPro.Domain/KasserPro.Domain.csproj `
    /p:Configuration=Debug `
    /p:Platform="Any CPU" `
    /p:BuildInParallel=true `
    /p:UseSharedCompilation=true `
    /p:RunAnalyzersDuringBuild=false `
    /p:RunCodeAnalysis=false `
    /p:GenerateAssemblyInfo=false `
    /p:GenerateDocumentationFile=false `
    /p:TreatWarningsAsErrors=false `
    /p:WarningLevel=0 `
    /verbosity:minimal `
    /nologo
$stopwatch1.Stop()
Write-Host "Domain: $($stopwatch1.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor Green

Write-Host "Building Application..." -ForegroundColor Yellow
$stopwatch2 = [System.Diagnostics.Stopwatch]::StartNew()
dotnet msbuild KasserPro.Application/KasserPro.Application.csproj `
    /p:Configuration=Debug `
    /p:Platform="Any CPU" `
    /p:BuildInParallel=true `
    /p:UseSharedCompilation=true `
    /p:RunAnalyzersDuringBuild=false `
    /p:RunCodeAnalysis=false `
    /p:GenerateAssemblyInfo=false `
    /p:GenerateDocumentationFile=false `
    /p:TreatWarningsAsErrors=false `
    /p:WarningLevel=0 `
    /p:UseSourceLink=false `
    /p:PublishRepositoryUrl=false `
    /verbosity:minimal `
    /nologo
$stopwatch2.Stop()
Write-Host "Application: $($stopwatch2.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor Green

$totalTime = $stopwatch1.Elapsed.TotalSeconds + $stopwatch2.Elapsed.TotalSeconds
Write-Host "Total: $($totalTime.ToString('F1'))s" -ForegroundColor Magenta