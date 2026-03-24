#!/usr/bin/env pwsh
# Build KasserPro Bridge App - Simple Version

Write-Host "🔨 Building KasserPro Bridge App" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to Bridge App
$bridgePath = "backend\KasserPro.BridgeApp"

if (-not (Test-Path $bridgePath)) {
    Write-Host "❌ Error: Bridge App not found at $bridgePath" -ForegroundColor Red
    exit 1
}

Write-Host "📂 Found Bridge App at: $bridgePath" -ForegroundColor Green
Set-Location $bridgePath

# Clean previous builds
if (Test-Path "publish") {
    Write-Host "🧹 Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "publish"
}

# Build for Windows x64
Write-Host ""
Write-Host "🔨 Building for Windows x64 (self-contained)..." -ForegroundColor Yellow
Write-Host ""

dotnet publish -c Release -r win-x64 --self-contained true -o ./publish

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Build failed!" -ForegroundColor Red
    Set-Location ../..
    exit 1
}

Write-Host ""
Write-Host "✅ Build completed successfully!" -ForegroundColor Green
Write-Host ""

# Create ZIP package
Write-Host "📦 Creating installation package..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMdd-HHmm"
$zipFile = "KasserPro-Bridge-$timestamp.zip"

if (Test-Path $zipFile) {
    Remove-Item -Force $zipFile
}

Compress-Archive -Path ./publish/* -DestinationPath $zipFile

Write-Host "✅ Package created: $zipFile" -ForegroundColor Green
Write-Host ""

# Show package info
$zipInfo = Get-Item $zipFile
$sizeMB = [math]::Round($zipInfo.Length / 1MB, 2)
Write-Host "📊 Package Size: $sizeMB MB" -ForegroundColor Cyan
Write-Host "📍 Location: $(Get-Location)\$zipFile" -ForegroundColor Cyan
Write-Host ""

# Create README
$readmeContent = @"
# KasserPro Bridge App - دليل التثبيت

## خطوات التثبيت

1. فك ضغط هذا الملف في: C:\KasserPro\Bridge\
2. شغّل KasserPro.BridgeApp.exe
3. أدخل Backend URL: http://168.231.106.139:5243
4. التطبيق سيُنشئ Device ID و API Key تلقائياً

## المتطلبات

- Windows 7 أو أحدث
- .NET 8.0 Runtime (مُضمّن في البناء)
- طابعة حرارية (USB/Network)

## الإعدادات

الملف: %AppData%\KasserPro\settings.json

{
  "DeviceId": "سيتم إنشاؤه تلقائياً",
  "BackendUrl": "http://168.231.106.139:5243",
  "ApiKey": "سيتم إنشاؤه تلقائياً",
  "DefaultPrinterName": "اسم الطابعة"
}

## الدعم

- Logs: %AppData%\KasserPro\logs\bridge-app.log
- للمساعدة: راجع PRINTER_BRIDGE_QUICK_GUIDE.md
"@

$readmeContent | Out-File -FilePath "README.txt" -Encoding UTF8

Write-Host "=================================" -ForegroundColor Cyan
Write-Host "✅ Build Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📦 Installation Package:" -ForegroundColor Yellow
Write-Host "   File: $zipFile" -ForegroundColor White
Write-Host "   Size: $sizeMB MB" -ForegroundColor White
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. انسخ $zipFile لجهاز الكاشير" -ForegroundColor White
Write-Host "   2. فك الضغط في C:\KasserPro\Bridge\" -ForegroundColor White
Write-Host "   3. شغّل KasserPro.BridgeApp.exe" -ForegroundColor White
Write-Host "   4. أدخل Backend URL: http://168.231.106.139:5243" -ForegroundColor White
Write-Host ""

Set-Location ../..
