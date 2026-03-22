# ⚡ دليل البدء السريع - رفع KasserPro على VPS

## 🎯 الخطوات الأساسية (15 دقيقة)

### 1️⃣ على VPS - التجهيز الأولي
```bash
# الاتصال بـ VPS
ssh root@your-vps-ip

# تحميل وتشغيل script الإعداد
wget https://raw.githubusercontent.com/your-repo/kasserpro/main/vps-setup.sh
chmod +x vps-setup.sh
./vps-setup.sh
```

### 2️⃣ على جهازك - رفع التطبيق
```powershell
# في مجلد المشروع
.\deploy-to-vps.ps1 -VpsIp "your-vps-ip" -VpsUser "root"
```

### 3️⃣ اختبار التطبيق
```bash
# افتح المتصفح
http://your-vps-ip
```

---

## 🔧 الإعداد اليدوي (إذا فشل Script)

### على VPS
```bash
# 1. تثبيت .NET
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
apt update && apt install -y aspnetcore-runtime-8.0

# 2. تثبيت Nginx
apt install -y nginx

# 3. إنشاء مستخدم
useradd -m -s /bin/bash kasserpro
mkdir -p /var/www/kasserpro
chown -R kasserpro:kasserpro /var/www/kasserpro
```

### على جهازك
```powershell
# 1. بناء التطبيق
cd backend/KasserPro.API
dotnet publish -c Release -o ./publish

# 2. ضغط الملفات
Compress-Archive -Path ./publish/* -DestinationPath kasserpro.zip

# 3. رفع للـ VPS
scp kasserpro.zip root@your-vps-ip:/tmp/
```

### على VPS - النشر
```bash
# 1. فك الضغط
unzip /tmp/kasserpro.zip -d /var/www/kasserpro/
chown -R kasserpro:kasserpro /var/www/kasserpro
chmod +x /var/www/kasserpro/KasserPro.API

# 2. إنشاء Service
cat > /etc/systemd/system/kasserpro.service << 'EOF'
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
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF

# 3. تشغيل Service
systemctl daemon-reload
systemctl enable kasserpro
systemctl start kasserpro
systemctl status kasserpro

# 4. إعداد Nginx
cat > /etc/nginx/sites-available/kasserpro << 'EOF'
server {
    listen 80;
    server_name _;

    location / {
        proxy_pass http://localhost:5243;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
EOF

ln -s /etc/nginx/sites-available/kasserpro /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx

# 5. إعداد Firewall
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
```

---

## 🔒 إضافة SSL (اختياري)

```bash
# تثبيت Certbot
apt install -y certbot python3-certbot-nginx

# الحصول على Certificate
certbot --nginx -d yourdomain.com -d www.yourdomain.com

# التجديد التلقائي مُفعّل تلقائياً
```

---

## 📊 الأوامر المفيدة

```bash
# عرض Logs
journalctl -u kasserpro -f

# إعادة تشغيل
systemctl restart kasserpro

# حالة Service
systemctl status kasserpro

# Backup Database
cp /var/www/kasserpro/kasserpro.db /var/www/kasserpro/backups/backup-$(date +%Y%m%d).db
```

---

## 🆘 استكشاف الأخطاء السريع

### المشكلة: Service لا يبدأ
```bash
journalctl -u kasserpro -n 50
```

### المشكلة: 502 Bad Gateway
```bash
# تحقق من أن Backend يعمل
curl http://localhost:5243/api/health

# تحقق من Nginx
tail -f /var/log/nginx/error.log
```

### المشكلة: Database locked
```bash
systemctl restart kasserpro
```

---

## 📞 الدعم

للمزيد من التفاصيل، راجع:
- `VPS_DEPLOYMENT_GUIDE.md` - الدليل الكامل
- `FRONTEND_VPS_CONFIG.md` - إعداد Frontend
- Logs: `journalctl -u kasserpro -f`

---

## ✅ Checklist النهائي

- [ ] VPS جاهز ومُحدّث
- [ ] .NET 8.0 مثبت
- [ ] Nginx مثبت
- [ ] التطبيق مرفوع ويعمل
- [ ] Firewall مُعد
- [ ] API يستجيب على `http://your-vps-ip`
- [ ] (اختياري) SSL مُفعّل
- [ ] (اختياري) Frontend مرفوع

---

## 🎉 تم!

التطبيق الآن يعمل على:
- **API:** `http://your-vps-ip` أو `https://yourdomain.com`
- **Admin:** admin@kasserpro.com / Admin@123
- **Cashier:** ahmed@kasserpro.com / 123456
