# Script لنسخ النسخ الاحتياطية لمكان آمن
# استخدام: .\copy-backups-to-safe-location.ps1

$sourceFolder = "F:\POS\backend\KasserPro.API\backups"
$destinationBase = "D:\KasserPro_Backups_Safe"  # غير المسار حسب احتياجك

# إنشاء مجلد بتاريخ اليوم
$today = Get-Date -Format "yyyy-MM-dd"
$destinationFolder = Join-Path $destinationBase $today

# إنشاء المجلد إذا لم يكن موجوداً
if (-not (Test-Path $destinationFolder)) {
    New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null
    Write-Host "✓ تم إنشاء المجلد: $destinationFolder" -ForegroundColor Green
}

# نسخ جميع الملفات
Write-Host "`nجاري نسخ النسخ الاحتياطية..." -ForegroundColor Yellow

$files = Get-ChildItem -Path $sourceFolder -Filter "*.db"
$copiedCount = 0

foreach ($file in $files) {
    $destination = Join-Path $destinationFolder $file.Name
    
    # نسخ فقط إذا كان الملف غير موجود أو أحدث
    if (-not (Test-Path $destination) -or $file.LastWriteTime -gt (Get-Item $destination).LastWriteTime) {
        Copy-Item -Path $file.FullName -Destination $destination -Force
        Write-Host "  ✓ تم نسخ: $($file.Name)" -ForegroundColor Green
        $copiedCount++
    } else {
        Write-Host "  - موجود بالفعل: $($file.Name)" -ForegroundColor Gray
    }
}

Write-Host "`n✓ اكتمل النسخ: $copiedCount ملف جديد" -ForegroundColor Green
Write-Host "المسار: $destinationFolder" -ForegroundColor Cyan

# عرض حجم النسخ
$totalSize = (Get-ChildItem -Path $destinationFolder -Filter "*.db" | Measure-Object -Property Length -Sum).Sum
$sizeMB = [math]::Round($totalSize / 1MB, 2)
Write-Host "الحجم الإجمالي: $sizeMB MB" -ForegroundColor Cyan
