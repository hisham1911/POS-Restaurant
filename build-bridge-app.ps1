#!/usr/bin/env pwsh
# Build KasserPro Bridge App for Windows

Write-Host "Building KasserPro Bridge App" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""

# Navigate to Bridge App
Set-Location backend/KasserPro.BridgeApp

# Clean previous builds
if (Test-Path "publish") {
    Remove-Item -Recurse -Force "publish"
}

# Build for Windows x64
Write-Host "Building for Windows x64..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Set-Location ../..
    exit 1
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""

# Create ZIP package
Write-Host "Creating installation package..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMdd"
$zipFile = "KasserPro-Bridge-Setup-$timestamp.zip"

if (Test-Path $zipFile) {
    Remove-Item -Force $zipFile
}

Compress-Archive -Path ./publish/* -DestinationPath $zipFile

Write-Host "Package created: $zipFile" -ForegroundColor Green
Write-Host ""

# Show package info
$zipInfo = Get-Item $zipFile
Write-Host "Package Size: $([math]::Round($zipInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
Write-Host ""

# Create README
$readmeContent = @"
# KasserPro Bridge App - Installation Guide

## Installation Steps

1. Extract this ZIP file to: C:\KasserPro\Bridge\
2. Run KasserPro.BridgeApp.exe
3. Enter API URL: http://168.231.106.139:5243
4. Enter Device ID: CASHIER-01 (or unique ID)
5. Configure printer settings

## Requirements

- Windows 7 or later
- .NET 8.0 Runtime (included in self-contained build)
- USB/Network Thermal Printer

## Configuration

Edit appsettings.json:
{
  "ApiUrl": "http://168.231.106.139:5243",
  "DeviceId": "CASHIER-01",
  "Printer": {
    "Type": "USB",
    "Port": "USB001"
  }
}

## Support

For help, check logs at: C:\KasserPro\Bridge\logs\
"@

$readmeContent | Out-File -FilePath "README.txt" -Encoding UTF8

Write-Host "=============================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Installation Package:" -ForegroundColor Yellow
Write-Host "  File: $zipFile" -ForegroundColor White
Write-Host "  Location: $(Get-Location)\$zipFile" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Copy $zipFile to cashier PC" -ForegroundColor White
Write-Host "  2. Extract to C:\KasserPro\Bridge\" -ForegroundColor White
Write-Host "  3. Run KasserPro.BridgeApp.exe" -ForegroundColor White
Write-Host "  4. Configure API URL and Device ID" -ForegroundColor White
Write-Host ""

Set-Location ../..
