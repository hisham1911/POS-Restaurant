#!/usr/bin/env pwsh
# Quick VPS Check Script
# سيشغل كل الأوامر المطلوبة ويجمع النتائج

$VPS_IP = "168.231.106.139"
$VPS_USER = "root"

Write-Host "🔍 Checking VPS Status..." -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Function to run SSH command
function Invoke-SSHCommand {
    param([string]$Command)
    ssh ${VPS_USER}@${VPS_IP} $Command
}

Write-Host "📋 1. System Info..." -ForegroundColor Yellow
Invoke-SSHCommand "uname -a && cat /etc/os-release | grep -E '^(NAME|VERSION)='"
Write-Host ""

Write-Host "💾 2. Resources..." -ForegroundColor Yellow
Invoke-SSHCommand "free -h && df -h | grep -E '^/dev/'"
Write-Host ""

Write-Host "🔧 3. Running Services..." -ForegroundColor Yellow
Invoke-SSHCommand "systemctl list-units --type=service --state=running | grep -E '(nginx|apache|httpd|dotnet|node|kasserpro)' || echo 'No web services found'"
Write-Host ""

Write-Host "🔌 4. Open Ports..." -ForegroundColor Yellow
Invoke-SSHCommand "ss -tulpn | grep LISTEN | grep -E ':(80|443|3000|5243|8080)' || netstat -tulpn | grep LISTEN | grep -E ':(80|443|3000|5243|8080)' || echo 'No common ports listening'"
Write-Host ""

Write-Host "🌐 5. Nginx Status..." -ForegroundColor Yellow
Invoke-SSHCommand "nginx -v 2>&1 && echo '' && ls -la /etc/nginx/sites-enabled/ 2>/dev/null || echo 'Nginx not installed or no sites'"
Write-Host ""

Write-Host "📁 6. Web Directories..." -ForegroundColor Yellow
Invoke-SSHCommand "ls -la /var/www/ 2>/dev/null || echo 'No /var/www directory'"
Write-Host ""

Write-Host "🔧 7. .NET Status..." -ForegroundColor Yellow
Invoke-SSHCommand "dotnet --version 2>/dev/null || echo '.NET not installed'"
Write-Host ""

Write-Host "⚙️  8. Running Processes..." -ForegroundColor Yellow
Invoke-SSHCommand "ps aux | grep -E '(nginx|apache|httpd|dotnet|node)' | grep -v grep | head -10 || echo 'No web processes'"
Write-Host ""

Write-Host "🔥 9. Firewall..." -ForegroundColor Yellow
Invoke-SSHCommand "ufw status 2>/dev/null || firewall-cmd --list-all 2>/dev/null || echo 'No firewall detected'"
Write-Host ""

Write-Host "✅ Check Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Summary will help determine next steps..." -ForegroundColor Cyan
