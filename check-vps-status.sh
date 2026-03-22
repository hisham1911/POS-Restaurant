#!/bin/bash
# Script لفحص حالة VPS والمواقع الموجودة عليه
# Usage: ssh root@168.231.106.139 'bash -s' < check-vps-status.sh

echo "======================================"
echo "🔍 KasserPro VPS Status Check"
echo "======================================"
echo ""

# 1. معلومات النظام
echo "📋 System Information:"
echo "----------------------"
uname -a
echo ""
cat /etc/os-release | grep -E "^(NAME|VERSION)="
echo ""

# 2. الموارد المتاحة
echo "💾 System Resources:"
echo "--------------------"
echo "CPU Cores: $(nproc)"
echo "RAM:"
free -h | grep -E "^Mem:"
echo ""
echo "Disk Usage:"
df -h | grep -E "^/dev/"
echo ""

# 3. الخدمات الشغالة
echo "🔧 Running Services:"
echo "--------------------"
systemctl list-units --type=service --state=running | grep -E "(nginx|apache|httpd|dotnet|node|kasserpro)" || echo "No web services found"
echo ""

# 4. المواقع على Nginx
echo "🌐 Nginx Configuration:"
echo "-----------------------"
if command -v nginx &> /dev/null; then
    echo "✅ Nginx installed: $(nginx -v 2>&1)"
    echo ""
    echo "Active Sites:"
    ls -la /etc/nginx/sites-enabled/ 2>/dev/null || echo "No sites-enabled directory"
    echo ""
    echo "Available Sites:"
    ls -la /etc/nginx/sites-available/ 2>/dev/null || echo "No sites-available directory"
else
    echo "❌ Nginx not installed"
fi
echo ""

# 5. المواقع على Apache
echo "🌐 Apache Configuration:"
echo "------------------------"
if command -v apache2 &> /dev/null || command -v httpd &> /dev/null; then
    echo "✅ Apache installed"
    ls -la /etc/apache2/sites-enabled/ 2>/dev/null || ls -la /etc/httpd/conf.d/ 2>/dev/null || echo "No Apache sites found"
else
    echo "❌ Apache not installed"
fi
echo ""

# 6. المنافذ المفتوحة
echo "🔌 Open Ports:"
echo "--------------"
if command -v netstat &> /dev/null; then
    netstat -tulpn | grep LISTEN | grep -E ":(80|443|3000|5243|8080)" || echo "No common web ports listening"
elif command -v ss &> /dev/null; then
    ss -tulpn | grep LISTEN | grep -E ":(80|443|3000|5243|8080)" || echo "No common web ports listening"
else
    echo "⚠️  netstat/ss not available"
fi
echo ""

# 7. مجلدات المواقع
echo "📁 Web Directories:"
echo "-------------------"
echo "/var/www:"
ls -la /var/www/ 2>/dev/null || echo "Directory not found"
echo ""
echo "/usr/share/nginx:"
ls -la /usr/share/nginx/html/ 2>/dev/null || echo "Directory not found"
echo ""

# 8. .NET Runtime
echo "🔧 .NET Runtime:"
echo "----------------"
if command -v dotnet &> /dev/null; then
    echo "✅ .NET installed: $(dotnet --version)"
else
    echo "❌ .NET not installed"
fi
echo ""

# 9. Node.js
echo "🔧 Node.js:"
echo "-----------"
if command -v node &> /dev/null; then
    echo "✅ Node.js installed: $(node --version)"
    echo "   npm: $(npm --version 2>/dev/null || echo 'not installed')"
else
    echo "❌ Node.js not installed"
fi
echo ""

# 10. العمليات الشغالة
echo "⚙️  Running Processes (Web Related):"
echo "-------------------------------------"
ps aux | grep -E "(nginx|apache|httpd|dotnet|node|pm2)" | grep -v grep || echo "No web processes found"
echo ""

# 11. Firewall Status
echo "🔥 Firewall Status:"
echo "-------------------"
if command -v ufw &> /dev/null; then
    ufw status
elif command -v firewall-cmd &> /dev/null; then
    firewall-cmd --list-all
else
    echo "No firewall detected"
fi
echo ""

# 12. SSL Certificates
echo "🔒 SSL Certificates:"
echo "--------------------"
if [ -d /etc/letsencrypt/live ]; then
    echo "Let's Encrypt certificates found:"
    ls -la /etc/letsencrypt/live/
else
    echo "No Let's Encrypt certificates found"
fi
echo ""

# 13. Recent Logs
echo "📊 Recent System Logs (Errors):"
echo "--------------------------------"
journalctl -p err -n 10 --no-pager 2>/dev/null || echo "Cannot access journalctl"
echo ""

echo "======================================"
echo "✅ Check Complete"
echo "======================================"
