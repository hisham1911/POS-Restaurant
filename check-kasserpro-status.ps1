# Quick check for KasserPro deployment status

$VpsIp = "168.231.106.139"
$VpsUser = "root"
$Target = "$VpsUser@$VpsIp"

function Invoke-Remote {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command
    )

    ssh $Target "bash -lc '$Command'"
}

Write-Host "Checking KasserPro status on VPS" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "1) Service status" -ForegroundColor Yellow
Invoke-Remote "if systemctl list-unit-files | grep -q kasserpro; then echo SERVICE_EXISTS; systemctl status kasserpro --no-pager | head -15; else echo SERVICE_NOT_FOUND; fi"
Write-Host ""

Write-Host "2) Port 5243 status" -ForegroundColor Yellow
Invoke-Remote "if ss -tulpn | grep -q :5243; then echo PORT_5243_LISTENING; ss -tulpn | grep :5243; elif netstat -tulpn 2>/dev/null | grep -q :5243; then echo PORT_5243_LISTENING; netstat -tulpn | grep :5243; else echo PORT_5243_NOT_LISTENING; fi"
Write-Host ""

Write-Host "3) Installation directory" -ForegroundColor Yellow
Invoke-Remote "if [ -d /var/www/kasserpro ]; then echo DIR_EXISTS:/var/www/kasserpro; ls -lh /var/www/kasserpro/*.dll 2>/dev/null | head -5 || echo NO_DLL_FILES; else echo DIR_NOT_FOUND; fi"
Write-Host ""

Write-Host "4) Local health endpoint" -ForegroundColor Yellow
Invoke-Remote "curl -s -m 8 -o /tmp/kasser_health.out -w HTTP:%{http_code} http://localhost:5243/health; echo; cat /tmp/kasser_health.out 2>/dev/null || true"
Write-Host ""

Write-Host "5) Firewall status" -ForegroundColor Yellow
Invoke-Remote "if command -v ufw >/dev/null 2>&1; then if ufw status | grep -q 5243; then echo FIREWALL_5243_ALLOWED; else echo FIREWALL_5243_NOT_ALLOWED; echo RUN:ufw_allow_5243_tcp; fi; else echo UFW_NOT_AVAILABLE; fi"
Write-Host ""

Write-Host "6) Recent logs (last 20 lines)" -ForegroundColor Yellow
Invoke-Remote "if systemctl list-unit-files | grep -q kasserpro; then journalctl -u kasserpro -n 20 --no-pager 2>/dev/null || echo NO_LOGS; else echo SERVICE_NOT_FOUND; fi"
Write-Host ""

Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Check complete" -ForegroundColor Green
Write-Host ""
