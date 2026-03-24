#!/usr/bin/env pwsh
# Data Reset Script for KasserPro
# This script resets all business data and seeds realistic test data

Write-Host "🔄 KasserPro Data Reset Script - مجزر الأمانة" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Confirm action
Write-Host "⚠️  تحذير: سيتم حذف جميع البيانات التجارية!" -ForegroundColor Yellow
Write-Host "   - جميع الطلبات والمدفوعات والورديات" -ForegroundColor Yellow
Write-Host "   - جميع المنتجات والفئات والعملاء" -ForegroundColor Yellow
Write-Host "   - جميع الموردين وفواتير الشراء" -ForegroundColor Yellow
Write-Host "   - جميع المصروفات وحركات الخزينة" -ForegroundColor Yellow
Write-Host ""
Write-Host "   سيتم إنشاء بيانات جديدة لمجزر الأمانة" -ForegroundColor Green
Write-Host ""

$confirmation = Read-Host "اكتب 'RESET' للمتابعة"

if ($confirmation -ne "RESET") {
    Write-Host "❌ تم إلغاء العملية." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🗑️  جاري حذف قاعدة البيانات..." -ForegroundColor Yellow

# Delete the database file
$dbPath = "src/KasserPro.API/kasserpro.db"
if (Test-Path $dbPath) {
    Remove-Item $dbPath -Force
    Write-Host "✅ تم حذف قاعدة البيانات" -ForegroundColor Green
} else {
    Write-Host "ℹ️  ملف قاعدة البيانات غير موجود (سيتم إنشاؤه)" -ForegroundColor Cyan
}

# Delete WAL and SHM files if they exist
$walPath = "src/KasserPro.API/kasserpro.db-wal"
$shmPath = "src/KasserPro.API/kasserpro.db-shm"

if (Test-Path $walPath) {
    Remove-Item $walPath -Force
}

if (Test-Path $shmPath) {
    Remove-Item $shmPath -Force
}

Write-Host ""
Write-Host "🔄 جاري تشغيل الباك إند (سيتم إنشاء البيانات تلقائياً)..." -ForegroundColor Cyan
Write-Host ""

# Navigate to API directory and run
Set-Location src/KasserPro.API

# Run the application (it will seed data on startup)
dotnet run

Write-Host ""
Write-Host "✅ تم إعادة تعيين البيانات بنجاح!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 بيانات الدخول:" -ForegroundColor Cyan
Write-Host "   مدير:    admin@kasserpro.com / Admin@123" -ForegroundColor White
Write-Host "   كاشير:   mohamed@kasserpro.com / 123456" -ForegroundColor White
Write-Host "   كاشير:   ali@kasserpro.com / 123456" -ForegroundColor White
Write-Host ""
Write-Host "🥩 المتجر: مجزر الأمانة" -ForegroundColor Green
Write-Host "📦 المنتجات: 24 منتج (لحوم بقري، مفرومة، أحشاء)" -ForegroundColor Green
Write-Host ""
