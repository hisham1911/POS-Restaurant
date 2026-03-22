# Update KasserPro Deployment
# Run this script as Administrator

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "KasserPro - Update Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[ERROR] This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click and select 'Run as Administrator'" -ForegroundColor Yellow
    pause
    exit 1
}

# Step 1: Stop the service
Write-Host "[1/5] Stopping KasserProService..." -ForegroundColor Yellow
try {
    Stop-Service -Name "KasserProService" -Force -ErrorAction Stop
    Start-Sleep -Seconds 3
    Write-Host "   ✓ Service stopped" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed to stop service: $_" -ForegroundColor Red
    pause
    exit 1
}

# Step 2: Backup old files (optional)
Write-Host "[2/5] Creating backup..." -ForegroundColor Yellow
$backupPath = "C:\KasserPro_Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
try {
    Copy-Item -Path "C:\KasserPro" -Destination $backupPath -Recurse -Force
    Write-Host "   ✓ Backup created at: $backupPath" -ForegroundColor Green
} catch {
    Write-Host "   ⚠ Backup failed (continuing anyway): $_" -ForegroundColor Yellow
}

# Step 3: Delete old database
Write-Host "[3/5] Deleting old database..." -ForegroundColor Yellow
try {
    Remove-Item "C:\KasserPro\kasserpro.db*" -Force -ErrorAction SilentlyContinue
    Write-Host "   ✓ Database deleted" -ForegroundColor Green
} catch {
    Write-Host "   ⚠ Database deletion failed: $_" -ForegroundColor Yellow
}

# Step 4: Copy new files
Write-Host "[4/5] Copying new files..." -ForegroundColor Yellow
try {
    Copy-Item -Path "C:\KasserPro_New\*" -Destination "C:\KasserPro\" -Recurse -Force
    Write-Host "   ✓ Files copied successfully" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed to copy files: $_" -ForegroundColor Red
    Write-Host "   Attempting to restore from backup..." -ForegroundColor Yellow
    Copy-Item -Path "$backupPath\*" -Destination "C:\KasserPro\" -Recurse -Force
    Start-Service -Name "KasserProService"
    pause
    exit 1
}

# Step 5: Start the service
Write-Host "[5/5] Starting KasserProService..." -ForegroundColor Yellow
try {
    Start-Service -Name "KasserProService" -ErrorAction Stop
    Start-Sleep -Seconds 5
    
    $service = Get-Service -Name "KasserProService"
    if ($service.Status -eq "Running") {
        Write-Host "   ✓ Service started successfully" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Service failed to start (Status: $($service.Status))" -ForegroundColor Red
    }
} catch {
    Write-Host "   ✗ Failed to start service: $_" -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "✓ Deployment updated successfully!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Access the application at: http://localhost:5243" -ForegroundColor Cyan
Write-Host ""
Write-Host "Login credentials:" -ForegroundColor Yellow
Write-Host "  Admin: admin@kasserpro.com / Admin@123" -ForegroundColor White
Write-Host ""
pause
