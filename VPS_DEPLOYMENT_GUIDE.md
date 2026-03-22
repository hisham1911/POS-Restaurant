# 🚀 دليل رفع KasserPro Backend على VPS

## 📋 المتطلبات الأساسية

### على VPS
- Ubuntu 22.04 LTS أو أحدث (موصى به)
- RAM: 2GB على الأقل
- Storage: 20GB على الأقل
- .NET 8.0 Runtime
- Nginx (للـ Reverse Proxy)
- SQLite (مدمج مع .NET)

---

## 🔧 الخطوة 1: تجهيز VPS

### 1.1 الاتصال بـ VPS
```bash
ssh root@your-vps-ip
```

### 1.2 تحديث النظام
```bash
apt update && apt upgrade -y
```

### 1.3 تثبيت .NET 8.0 Runtime
```bash
# إضافة Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# تثبيت .NET Runtime
apt update
apt install -y aspnetcore-runtime-8.0

# التحقق من التثبيت
dotnet --version
```

### 1.4 تثبيت Nginx
```bash
apt install -y nginx
systemctl enable nginx
systemctl start nginx
```

### 1.5 إنشاء مستخدم للتطبيق (أمان)
```bash
useradd -m -s /bin/bash kasserpro
```

---

## 📦 الخطوة 2: بناء ونشر التطبيق

### 2.1 على جهازك المحلي - بناء التطبيق
```powershell
# الانتقال لمجلد Backend
cd backend/KasserPro.API

# بناء التطبيق للنشر
dotnet publish -c Release -o ./publish --self-contained false

# ضغط الملفات
Compress-Archive -Path ./publish/* -DestinationPath kasserpro-backend.zip
```

### 2.2 رفع الملفات للـ VPS
```bash
# من جهازك المحلي
scp kasserpro-backend.zip root@your-vps-ip:/tmp/
```

### 2.3 على VPS - فك الضغط والإعداد
```bash
# إنشاء مجلد التطبيق
mkdir -p /var/www/kasserpro
cd /var/www/kasserpro

# فك الضغط
unzip /tmp/kasserpro-backend.zip -d /var/www/kasserpro/

# تعيين الصلاحيات
chown -R kasserpro:kasserpro /var/www/kasserpro
chmod +x /var/www/kasserpro/KasserPro.API

# إنشاء مجلدات البيانات
mkdir -p /var/www/kasserpro/logs
mkdir -p /var/www/kasserpro/backups
chown -R kasserpro:kasserpro /var/www/kasserpro
```

---

## ⚙️ الخطوة 3: إعداد Configuration

### 3.1 تعديل appsettings.json
```bash
nano /var/www/kasserpro/appsettings.json
```

```json
{
  "Urls": "http://0.0.0.0:5243",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": ["https://yourdomain.com", "http://your-vps-ip"],
  "Jwt": {
    "Key": "YOUR_SECURE_JWT_KEY_HERE_AT_LEAST_64_CHARACTERS_LONG",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/www/kasserpro/kasserpro.db;Cache=Shared"
  },
  "ShiftAutoClose": {
    "Enabled": true,
    "HoursThreshold": 12
  }
}
```

### 3.2 توليد JWT Key آمن
```bash
# توليد مفتاح عشوائي قوي
openssl rand -base64 64
# انسخ الناتج واستخدمه في Jwt:Key
```

---

## 🔄 الخطوة 4: إنشاء Systemd Service

### 4.1 إنشاء Service File
```bash
nano /etc/systemd/system/kasserpro.service
```

```ini
[Unit]
Description=KasserPro Backend API
After=network.target

[Service]
Type=notify
User=kasserpro
Group=kasserpro
WorkingDirectory=/var/www/kasserpro
ExecStart=/usr/bin/dotnet /var/www/kasserpro/KasserPro.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=kasserpro
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/www/kasserpro

[Install]
WantedBy=multi-user.target
```

### 4.2 تفعيل وتشغيل Service
```bash
# إعادة تحميل systemd
systemctl daemon-reload

# تفعيل Service للبدء التلقائي
systemctl enable kasserpro

# تشغيل Service
systemctl start kasserpro

# التحقق من الحالة
systemctl status kasserpro

# عرض Logs
journalctl -u kasserpro -f
```

---

## 🌐 الخطوة 5: إعداد Nginx Reverse Proxy

### 5.1 إنشاء Nginx Configuration
```bash
nano /etc/nginx/sites-available/kasserpro
```

```nginx
# HTTP - Redirect to HTTPS
server {
    listen 80;
    server_name yourdomain.com www.yourdomain.com;
    
    # Redirect all HTTP to HTTPS
    return 301 https://$server_name$request_uri;
}

# HTTPS
server {
    listen 443 ssl http2;
    server_name yourdomain.com www.yourdomain.com;

    # SSL Certificates (سيتم إعدادها في الخطوة 6)
    ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;
    
    # SSL Configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # Security Headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Max upload size
    client_max_body_size 10M;

    # Proxy to Backend
    location / {
        proxy_pass http://localhost:5243;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # SignalR WebSocket Support
    location /hubs/ {
        proxy_pass http://localhost:5243;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # WebSocket timeouts
        proxy_read_timeout 86400;
    }
}
```

### 5.2 تفعيل Configuration
```bash
# إنشاء symbolic link
ln -s /etc/nginx/sites-available/kasserpro /etc/nginx/sites-enabled/

# اختبار Configuration
nginx -t

# إعادة تحميل Nginx
systemctl reload nginx
```

---

## 🔒 الخطوة 6: إعداد SSL Certificate (Let's Encrypt)

### 6.1 تثبيت Certbot
```bash
apt install -y certbot python3-certbot-nginx
```

### 6.2 الحصول على SSL Certificate
```bash
# إيقاف Nginx مؤقتاً
systemctl stop nginx

# الحصول على Certificate
certbot certonly --standalone -d yourdomain.com -d www.yourdomain.com

# تشغيل Nginx
systemctl start nginx
```

### 6.3 تجديد تلقائي للـ Certificate
```bash
# اختبار التجديد
certbot renew --dry-run

# Certbot يضيف cron job تلقائياً للتجديد
```

---

## 🔥 الخطوة 7: إعداد Firewall

```bash
# تفعيل UFW
ufw enable

# السماح بـ SSH
ufw allow 22/tcp

# السماح بـ HTTP/HTTPS
ufw allow 80/tcp
ufw allow 443/tcp

# التحقق من القواعد
ufw status
```

---

## 📊 الخطوة 8: Monitoring & Maintenance

### 8.1 عرض Logs
```bash
# Application logs
journalctl -u kasserpro -f

# Nginx logs
tail -f /var/log/nginx/access.log
tail -f /var/log/nginx/error.log

# Application file logs
tail -f /var/www/kasserpro/logs/kasserpro-*.log
```

### 8.2 إعادة تشغيل Service
```bash
systemctl restart kasserpro
```

### 8.3 Backup Database
```bash
# Backup يدوي
cp /var/www/kasserpro/kasserpro.db /var/www/kasserpro/backups/kasserpro-$(date +%Y%m%d-%H%M%S).db

# إنشاء Cron Job للـ Backup اليومي
crontab -e
```

أضف السطر التالي:
```cron
0 2 * * * cp /var/www/kasserpro/kasserpro.db /var/www/kasserpro/backups/kasserpro-$(date +\%Y\%m\%d-\%H\%M\%S).db
```

---

## 🔄 الخطوة 9: تحديث التطبيق

### 9.1 Script للتحديث
```bash
nano /root/update-kasserpro.sh
```

```bash
#!/bin/bash
set -e

echo "🔄 Starting KasserPro update..."

# Stop service
systemctl stop kasserpro

# Backup current version
cp -r /var/www/kasserpro /var/www/kasserpro-backup-$(date +%Y%m%d-%H%M%S)

# Backup database
cp /var/www/kasserpro/kasserpro.db /var/www/kasserpro/backups/kasserpro-pre-update-$(date +%Y%m%d-%H%M%S).db

# Extract new version
unzip -o /tmp/kasserpro-backend.zip -d /var/www/kasserpro/

# Set permissions
chown -R kasserpro:kasserpro /var/www/kasserpro
chmod +x /var/www/kasserpro/KasserPro.API

# Start service
systemctl start kasserpro

echo "✅ Update completed!"
systemctl status kasserpro
```

```bash
chmod +x /root/update-kasserpro.sh
```

---

## ✅ الخطوة 10: اختبار التطبيق

### 10.1 اختبار API
```bash
# Health check
curl http://localhost:5243/api/health

# من خارج VPS
curl https://yourdomain.com/api/health
```

### 10.2 اختبار Login
```bash
curl -X POST https://yourdomain.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@kasserpro.com",
    "password": "Admin@123"
  }'
```

---

## 🎯 Checklist النهائي

- [ ] .NET 8.0 Runtime مثبت
- [ ] Nginx مثبت ومُعد
- [ ] SSL Certificate مُفعّل
- [ ] Firewall مُعد بشكل صحيح
- [ ] Systemd Service يعمل
- [ ] JWT Key آمن ومُعد
- [ ] Database backup مُجدول
- [ ] Logs يمكن الوصول إليها
- [ ] API يستجيب بشكل صحيح
- [ ] Frontend يمكنه الاتصال بـ Backend

---

## 🆘 استكشاف الأخطاء

### المشكلة: Service لا يبدأ
```bash
# عرض الأخطاء التفصيلية
journalctl -u kasserpro -n 50 --no-pager

# التحقق من الصلاحيات
ls -la /var/www/kasserpro/
```

### المشكلة: Database locked
```bash
# التحقق من العمليات المفتوحة
lsof /var/www/kasserpro/kasserpro.db

# إعادة تشغيل Service
systemctl restart kasserpro
```

### المشكلة: Nginx 502 Bad Gateway
```bash
# التحقق من أن Backend يعمل
systemctl status kasserpro
curl http://localhost:5243/api/health

# التحقق من Nginx logs
tail -f /var/log/nginx/error.log
```

---

## 📞 الدعم

للمزيد من المساعدة، راجع:
- Application logs: `/var/www/kasserpro/logs/`
- System logs: `journalctl -u kasserpro`
- Nginx logs: `/var/log/nginx/`
