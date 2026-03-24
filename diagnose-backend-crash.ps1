#!/usr/bin/env pwsh
# Diagnose Backend Crash on VPS

$VPS_IP = "168.231.106.139"
$VPS_USER = "root"

Write-Host "Diagnosing Backend Crash" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# 1. Check service status
Write-Host "1. Service Status:" -ForegroundColor Yellow
ssh ${VPS_USER}@${VPS_IP} "systemctl status kasserpro --no-pager -l"
Write-Host ""

# 2. Check recent logs
Write-Host "2. Recent Logs (last 50 lines):" -ForegroundColor Yellow
ssh ${VPS_USER}@${VPS_IP} "journalctl -u kasserpro -n 50 --no-pager"
Write-Host ""

# 3. Check if port is listening
Write-Host "3. Port 5243 Status:" -ForegroundColor Yellow
ssh ${VPS_USER}@${VPS_IP} "ss -tulpn | grep :5243"
Write-Host ""

# 4. Check disk space
Write-Host "4. Disk Space:" -ForegroundColor Yellow
ssh ${VPS_USER}@${VPS_IP} "df -h /var/www/kasserpro"
Write-Host ""

# 5. Check memory
Write-Host "5. Memory Usage:" -ForegroundColor Yellow
ssh ${VPS_USER}@${VPS_IP} "free -h"
Write-Host ""

# 6. Check database
Write-Host "6. Database Status:" -ForegroundColor Yellow
ssh ${VPS_USER}@${VPS_IP} "ls -lh /var/www/kasserpro/kasserpro.db"
Write-Host ""

# 7. Try to start manually
Write-Host "7. Attempting manual start..." -ForegroundColor Yellow
ssh ${VPS_USER}@${VPS_IP} "systemctl restart kasserpro"
Start-Sleep -Seconds 3
ssh ${VPS_USER}@${VPS_IP} "systemctl status kasserpro --no-pager"
Write-Host ""

Write-Host "========================" -ForegroundColor Cyan
Write-Host "Diagnosis Complete" -ForegroundColor Green
Write-Host ""
