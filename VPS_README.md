# 📚 KasserPro VPS Deployment - دليل شامل

## 📁 الملفات المتوفرة

| الملف | الوصف |
|------|-------|
| `VPS_DEPLOYMENT_GUIDE.md` | الدليل الكامل والمفصل للنشر |
| `QUICK_START_VPS.md` | دليل البدء السريع (15 دقيقة) |
| `FRONTEND_VPS_CONFIG.md` | إعداد Frontend للاتصال بـ VPS |
| `deploy-to-vps.ps1` | Script تلقائي لرفع Backend |
| `vps-setup.sh` | Script إعداد VPS الأولي |
| `vps-monitor.sh` | Script للمراقبة والصيانة |

---

## 🚀 البدء السريع

### للمبتدئين
```bash
# 1. على VPS
ssh root@your-vps-ip
wget https://raw.githubusercontent.com/your-repo/vps-setup.sh
bash vps-setup.sh

# 2. على جهازك
.\deploy-to-vps.ps1 -VpsIp "your-vps-ip"

# 3. افتح المتصفح
http://your-vps-ip
```

### للمحترفين
راجع `VPS_DEPLOYMENT_GUIDE.md` للتحكم الكامل في كل خطوة.

---

## 📋 المتطلبات

### VPS Specifications
- **OS:** Ubuntu 22.04 LTS (موصى به)
- **RAM:** 2GB minimum, 4GB recommended
- **Storage:** 20GB minimum
- **CPU:** 1 core minimum, 2 cores recommended

### Software Requirements
- .NET 8.0 Runtime
- Nginx
- SQLite (مدمج)
- Certbot (للـ SSL)

---

## 🎯 سيناريوهات الاستخدام

### السيناريو 1: Backend فقط على VPS
```
[VPS] Backend API (Port 5243)
      ↓
[Nginx] Reverse Proxy (Port 80/443)
      ↓
[Client] أي جهاز على الشبكة
```

**الاستخدام:**
- Frontend يعمل محلياً أو على Vercel/Netlify
- Backend على VPS
- راجع: `FRONTEND_VPS_CONFIG.md`

### السيناريو 2: Full Stack على VPS
```
[VPS] 
  ├─ Backend API (Port 5243)
  └─ Frontend Static Files
      ↓
[Nginx] يخدم Frontend + Proxy للـ Backend
      ↓
[Client] يصل عبر Domain واحد
```

**الاستخدام:**
- كل شيء على VPS
- Domain واحد
- راجع: `VPS_DEPLOYMENT_GUIDE.md` - الخطوة 5

### السيناريو 3: Multi-Domain Setup
```
[VPS]
  ├─ api.yourdomain.com → Backend
  └─ yourdomain.com → Frontend (Vercel/Netlify)
```

**الاستخدام:**
- Frontend على Vercel/Netlify
- Backend على subdomain منفصل
- راجع: `FRONTEND_VPS_CONFIG.md` - الطريقة 3

---

## 🔧 الأوامر الأساسية

### إدارة Service
```bash
# تشغيل
systemctl start kasserpro

# إيقاف
systemctl stop kasserpro

# إعادة تشغيل
systemctl restart kasserpro

# الحالة
systemctl status kasserpro

# تفعيل البدء التلقائي
systemctl enable kasserpro
```

### عرض Logs
```bash
# Service logs (live)
journalctl -u kasserpro -f

# آخر 100 سطر
journalctl -u kasserpro -n 100

# Application logs
tail -f /var/www/kasserpro/logs/kasserpro-*.log

# Nginx logs
tail -f /var/log/nginx/access.log
tail -f /var/log/nginx/error.log
```

### Backup & Restore
```bash
# Backup يدوي
bash vps-monitor.sh backup

# أو مباشرة
cp /var/www/kasserpro/kasserpro.db \
   /var/www/kasserpro/backups/backup-$(date +%Y%m%d).db

# Restore
systemctl stop kasserpro
cp /var/www/kasserpro/backups/backup-20260322.db \
   /var/www/kasserpro/kasserpro.db
systemctl start kasserpro
```

### Monitoring
```bash
# استخدم monitoring script
bash vps-monitor.sh status
bash vps-monitor.sh health
bash vps-monitor.sh logs
```

---

## 🔒 الأمان

### Firewall
```bash
# السماح فقط بالمنافذ الضرورية
ufw allow 22/tcp   # SSH
ufw allow 80/tcp   # HTTP
ufw allow 443/tcp  # HTTPS
ufw enable
```

### SSL Certificate
```bash
# تثبيت Certbot
apt install certbot python3-certbot-nginx

# الحصول على Certificate
certbot --nginx -d yourdomain.com

# التجديد التلقائي (مُفعّل تلقائياً)
certbot renew --dry-run
```

### JWT Security
```bash
# توليد مفتاح آمن
openssl rand -base64 64

# تعديل appsettings.json
nano /var/www/kasserpro/appsettings.json
# ضع المفتاح في Jwt:Key
```

### Database Permissions
```bash
# التأكد من الصلاحيات الصحيحة
chown kasserpro:kasserpro /var/www/kasserpro/kasserpro.db
chmod 600 /var/www/kasserpro/kasserpro.db
```

---

## 🔄 التحديثات

### طريقة 1: باستخدام Script
```powershell
# على جهازك
.\deploy-to-vps.ps1 -VpsIp "your-vps-ip"
```

### طريقة 2: يدوياً
```bash
# 1. رفع الملف الجديد
scp kasserpro-backend.zip root@your-vps:/tmp/

# 2. على VPS
bash vps-monitor.sh update
```

### طريقة 3: Git Pull (للمطورين)
```bash
# على VPS
cd /path/to/source
git pull
cd backend/KasserPro.API
dotnet publish -c Release -o /var/www/kasserpro
systemctl restart kasserpro
```

---

## 📊 Performance Optimization

### Database Optimization
```bash
# تحسين SQLite
sqlite3 /var/www/kasserpro/kasserpro.db "VACUUM;"
sqlite3 /var/www/kasserpro/kasserpro.db "ANALYZE;"
```

### Nginx Caching
```nginx
# في /etc/nginx/sites-available/kasserpro
location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
    expires 1y;
    add_header Cache-Control "public, immutable";
}
```

### Log Rotation
```bash
# إنشاء logrotate config
cat > /etc/logrotate.d/kasserpro << 'EOF'
/var/www/kasserpro/logs/*.log {
    daily
    rotate 30
    compress
    delaycompress
    notifempty
    missingok
    create 0644 kasserpro kasserpro
}
EOF
```

---

## 🆘 استكشاف الأخطاء الشائعة

### المشكلة: Service لا يبدأ
```bash
# عرض الأخطاء
journalctl -u kasserpro -n 50

# الأسباب الشائعة:
# 1. Port مستخدم
netstat -tulpn | grep 5243

# 2. صلاحيات خاطئة
ls -la /var/www/kasserpro/

# 3. .NET غير مثبت
dotnet --version
```

### المشكلة: 502 Bad Gateway
```bash
# تحقق من Backend
curl http://localhost:5243/api/health

# تحقق من Nginx config
nginx -t

# عرض Nginx errors
tail -f /var/log/nginx/error.log
```

### المشكلة: Database locked
```bash
# إيجاد العمليات المفتوحة
lsof /var/www/kasserpro/kasserpro.db

# إعادة تشغيل
systemctl restart kasserpro
```

### المشكلة: Out of Memory
```bash
# عرض استخدام الذاكرة
free -h

# إضافة Swap
fallocate -l 2G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
```

### المشكلة: Disk Full
```bash
# عرض استخدام القرص
df -h

# حذف Backups القديمة
cd /var/www/kasserpro/backups
ls -t | tail -n +11 | xargs rm

# حذف Logs القديمة
find /var/www/kasserpro/logs -name "*.log" -mtime +30 -delete
```

---

## 📈 Monitoring & Alerts

### Setup Email Alerts
```bash
# تثبيت mailutils
apt install -y mailutils

# إنشاء monitoring script
cat > /root/check-kasserpro.sh << 'EOF'
#!/bin/bash
if ! systemctl is-active --quiet kasserpro; then
    echo "KasserPro service is down!" | mail -s "ALERT: KasserPro Down" admin@yourdomain.com
fi
EOF

chmod +x /root/check-kasserpro.sh

# إضافة لـ cron (كل 5 دقائق)
(crontab -l; echo "*/5 * * * * /root/check-kasserpro.sh") | crontab -
```

### Setup Uptime Monitoring
استخدم خدمات مثل:
- UptimeRobot (مجاني)
- Pingdom
- StatusCake

---

## 🎓 Best Practices

### 1. Backups
- ✅ Backup يومي تلقائي (مُعد في vps-setup.sh)
- ✅ احتفظ بـ 30 backup على الأقل
- ✅ Backup قبل كل تحديث
- ✅ اختبر Restore بشكل دوري

### 2. Security
- ✅ استخدم SSL دائماً
- ✅ غيّر JWT Key بشكل دوري
- ✅ حدّث النظام بانتظام
- ✅ استخدم Firewall
- ✅ راقب Logs للأنشطة المشبوهة

### 3. Performance
- ✅ راقب استخدام الموارد
- ✅ نظّف Logs القديمة
- ✅ حسّن Database دورياً
- ✅ استخدم CDN للـ Static Files

### 4. Maintenance
- ✅ راجع Logs أسبوعياً
- ✅ اختبر Backups شهرياً
- ✅ حدّث التطبيق بانتظام
- ✅ راقب Disk Space

---

## 📞 الدعم والمساعدة

### الملفات المرجعية
1. `VPS_DEPLOYMENT_GUIDE.md` - الدليل الكامل
2. `QUICK_START_VPS.md` - البدء السريع
3. `FRONTEND_VPS_CONFIG.md` - إعداد Frontend

### الأوامر المفيدة
```bash
# Status شامل
bash vps-monitor.sh status

# Health check
bash vps-monitor.sh health

# عرض Logs
bash vps-monitor.sh logs

# Backup
bash vps-monitor.sh backup
```

### Logs Locations
- Service: `journalctl -u kasserpro`
- Application: `/var/www/kasserpro/logs/`
- Nginx: `/var/log/nginx/`

---

## ✅ Deployment Checklist

### قبل النشر
- [ ] VPS جاهز ومُحدّث
- [ ] Domain مُعد (اختياري)
- [ ] SSL Certificate جاهز (اختياري)
- [ ] Backup من البيانات الحالية

### أثناء النشر
- [ ] .NET 8.0 مثبت
- [ ] Nginx مُعد
- [ ] Service يعمل
- [ ] Firewall مُعد
- [ ] Logs يمكن الوصول إليها

### بعد النشر
- [ ] API يستجيب بشكل صحيح
- [ ] Frontend يتصل بـ Backend
- [ ] Login يعمل
- [ ] Database Backup مُجدول
- [ ] Monitoring مُفعّل
- [ ] SSL يعمل (إذا مُعد)

---

## 🎉 النتيجة النهائية

بعد اتباع هذا الدليل، سيكون لديك:

✅ Backend API يعمل على VPS  
✅ Nginx Reverse Proxy مُعد  
✅ SSL Certificate (اختياري)  
✅ Automatic Backups  
✅ Systemd Service للبدء التلقائي  
✅ Monitoring & Logging  
✅ Easy Update Process  

**التطبيق جاهز للإنتاج! 🚀**
