# KasserPro Multi-App VPS Deployment Runbook

Last updated: 2026-05-23

هذا الملف يشرح بالتفصيل نفس خطوات تجهيز ونشر تطبيق Restaurant على السيرفر، بحيث يمكن تكرارها على تطبيقات Cashier و Pharmacy بدون خلط الداتا أو الخدمات.

> مهم: لا تكتب أي كلمات مرور حقيقية أو مفاتيح SSH داخل هذا الملف أو داخل الكود. استخدم GitHub Secrets و systemd Environment فقط.

## 1. الخريطة العامة للسيرفر

السيرفر:

```text
168.231.106.139
Ubuntu 22.04
Reverse proxy: Nginx
Runtime: .NET 8
Database per app: SQLite file
```

كل تطبيق له:

- systemd service مستقل
- port مستقل
- folder مستقل
- SQLite database مستقلة
- logs و backups مستقلين
- domain/subdomain مستقل

| App | Service | Port | Folder | Domain |
| --- | --- | ---: | --- | --- |
| Cashier | `kasserpro` | `5243` | `/var/www/kasserpro` | `cashier.azinternational-eg.com` |
| Restaurant | `kasserpro-restaurant` | `5244` | `/var/www/kasserpro-restaurant` | `restaurant.azinternational-eg.com` |
| Pharmacy | `kasserpro-pharmacy` | `5245` | `/var/www/kasserpro-pharmacy` | `pharmacy.azinternational-eg.com` |

## 2. القاعدة الذهبية

لا تستخدم نفس القيم لتطبيقين مختلفين في هذه العناصر:

- `VPS_APP_DIR`
- `VPS_SERVICE_NAME`
- `VPS_HEALTH_PORT`
- `VPS_DOMAIN`
- SQLite database path
- `ASPNETCORE_URLS`
- Nginx upstream port

لو تطبيقين استخدموا نفس folder أو نفس database، الداتا هتتداخل أو تتضرب.

## 3. خطوات DNS

من لوحة Hostinger أو أي DNS provider، أضف A records:

```text
cashier    -> 168.231.106.139
restaurant -> 168.231.106.139
pharmacy   -> 168.231.106.139
```

اختبر من جهازك:

```bash
nslookup cashier.azinternational-eg.com
nslookup restaurant.azinternational-eg.com
nslookup pharmacy.azinternational-eg.com
```

لو DNS لم يرجع IP الصحيح، لا تكمل SSL قبل أن ينتشر الـ DNS.

## 4. تجهيز folder لكل تطبيق على السيرفر

ادخل على السيرفر:

```bash
ssh root@168.231.106.139
```

Cashier:

```bash
mkdir -p /var/www/kasserpro/logs /var/www/kasserpro/backups
```

Restaurant:

```bash
mkdir -p /var/www/kasserpro-restaurant/logs /var/www/kasserpro-restaurant/backups
```

Pharmacy:

```bash
mkdir -p /var/www/kasserpro-pharmacy/logs /var/www/kasserpro-pharmacy/backups
```

## 5. appsettings.json لكل تطبيق

كل folder لازم يحتوي `appsettings.json` خاص به.

مثال Cashier:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": ["*"],
  "Urls": "http://0.0.0.0:5243",
  "Jwt": {
    "Key": "PUT_A_RANDOM_32_PLUS_CHARACTER_SECRET_HERE",
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
  },
  "Seeding": {
    "AutoRunOnStartup": false
  }
}
```

Restaurant:

```json
"Urls": "http://0.0.0.0:5244",
"ConnectionStrings": {
  "DefaultConnection": "Data Source=/var/www/kasserpro-restaurant/kasserpro.db;Cache=Shared"
}
```

Pharmacy:

```json
"Urls": "http://0.0.0.0:5245",
"ConnectionStrings": {
  "DefaultConnection": "Data Source=/var/www/kasserpro-pharmacy/kasserpro.db;Cache=Shared"
}
```

ملاحظات مهمة:

- `Jwt:Key` لازم يكون secret قوي 32 حرف أو أكثر.
- يمكن وضع `Jwt__Key` في systemd بدل appsettings.
- لا ترفع `appsettings.json` الحقيقي إلى GitHub.
- `AllowedOrigins` يمكن تضييقها لاحقا بدل `*` بعد تثبيت دومينات الفرونت.

## 6. systemd service لكل تطبيق

مسارات ملفات الخدمات:

```text
/etc/systemd/system/kasserpro.service
/etc/systemd/system/kasserpro-restaurant.service
/etc/systemd/system/kasserpro-pharmacy.service
```

Template عام:

```ini
[Unit]
Description=KasserPro APP_NAME API
After=network.target

[Service]
WorkingDirectory=APP_DIR
ExecStart=/usr/bin/dotnet APP_DIR/KasserPro.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=SERVICE_NAME
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:PORT
Environment=KASSERPRO_DB_PASSWORD=PUT_DB_PASSWORD_HERE
Environment=KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD=PUT_OWNER_SEED_PASSWORD_HERE

[Install]
WantedBy=multi-user.target
```

Cashier values:

```text
APP_NAME=Cashier
SERVICE_NAME=kasserpro
APP_DIR=/var/www/kasserpro
PORT=5243
```

Restaurant values:

```text
APP_NAME=Restaurant
SERVICE_NAME=kasserpro-restaurant
APP_DIR=/var/www/kasserpro-restaurant
PORT=5244
```

Pharmacy values:

```text
APP_NAME=Pharmacy
SERVICE_NAME=kasserpro-pharmacy
APP_DIR=/var/www/kasserpro-pharmacy
PORT=5245
```

بعد تعديل أي service:

```bash
systemctl daemon-reload
systemctl enable SERVICE_NAME
systemctl restart SERVICE_NAME
systemctl status SERVICE_NAME
```

أمثلة:

```bash
systemctl daemon-reload
systemctl enable kasserpro-pharmacy
systemctl restart kasserpro-pharmacy
systemctl status kasserpro-pharmacy
```

Logs:

```bash
journalctl -u kasserpro -f
journalctl -u kasserpro-restaurant -f
journalctl -u kasserpro-pharmacy -f
```

## 7. Nginx config

كل domain يوجه إلى port التطبيق الخاص به.

يمكن وضعهم في ملف واحد:

```text
/etc/nginx/sites-available/kasserpro-multi
/etc/nginx/sites-enabled/kasserpro-multi
```

Cashier:

```nginx
server {
    listen 80;
    server_name cashier.azinternational-eg.com;

    location / {
        proxy_pass http://127.0.0.1:5243;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Restaurant:

```nginx
server {
    listen 80;
    server_name restaurant.azinternational-eg.com;

    location / {
        proxy_pass http://127.0.0.1:5244;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Pharmacy:

```nginx
server {
    listen 80;
    server_name pharmacy.azinternational-eg.com;

    location / {
        proxy_pass http://127.0.0.1:5245;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

اختبر وأعد تحميل Nginx:

```bash
nginx -t
systemctl reload nginx
```

## 8. SSL باستخدام Certbot

بعد DNS:

```bash
certbot --nginx --redirect -d cashier.azinternational-eg.com
certbot --nginx --redirect -d restaurant.azinternational-eg.com
certbot --nginx --redirect -d pharmacy.azinternational-eg.com
```

أو مرة واحدة:

```bash
certbot --nginx --redirect \
  -d cashier.azinternational-eg.com \
  -d restaurant.azinternational-eg.com \
  -d pharmacy.azinternational-eg.com
```

ثم:

```bash
nginx -t
systemctl reload nginx
```

## 9. GitHub Actions auto-deploy

استخدم نفس workflow الموجود في:

```text
.github/workflows/deploy-vps.yml
```

ماذا يفعل الـ workflow:

1. يسحب الكود من GitHub.
2. يشغل Node 20.
3. يبني الفرونت:

```bash
cd frontend
npm ci
npm run build
```

4. ينسخ `frontend/dist` إلى:

```text
backend/KasserPro.API/wwwroot
```

5. يعمل publish للباك:

```bash
dotnet publish backend/KasserPro.API/KasserPro.API.csproj \
  -c Release \
  -o artifacts/backend-publish/ci \
  --self-contained false \
  /p:UseAppHost=false
```

6. يضغط publish output في zip.
7. يرفع zip إلى `/tmp` على السيرفر.
8. يعمل backup قبل النشر:

```text
kasserpro.db
appsettings.json
license.key
wwwroot
```

9. يوقف service.
10. يعمل rsync إلى app folder مع استثناء الملفات المهمة:

```text
kasserpro.db
appsettings.json
appsettings.Development.json
license.key
backups
logs
```

11. يشغل service.
12. يعمل health check.
13. يشغل Certbot للدومين.

## 10. GitHub Secrets المطلوبة لكل repo

كل repo خاص بتطبيق يجب أن يحتوي Secrets خاصة به.

Secrets المشتركة:

```text
VPS_HOST=168.231.106.139
VPS_USER=root
VPS_SSH_PORT=22
CERTBOT_EMAIL=admin@azinternational-eg.com
VPS_SSH_KEY=<PRIVATE_SSH_KEY>
```

Cashier:

```text
VPS_APP_DIR=/var/www/kasserpro
VPS_SERVICE_NAME=kasserpro
VPS_HEALTH_PORT=5243
VPS_DOMAIN=cashier.azinternational-eg.com
```

Restaurant:

```text
VPS_APP_DIR=/var/www/kasserpro-restaurant
VPS_SERVICE_NAME=kasserpro-restaurant
VPS_HEALTH_PORT=5244
VPS_DOMAIN=restaurant.azinternational-eg.com
```

Pharmacy:

```text
VPS_APP_DIR=/var/www/kasserpro-pharmacy
VPS_SERVICE_NAME=kasserpro-pharmacy
VPS_HEALTH_PORT=5245
VPS_DOMAIN=pharmacy.azinternational-eg.com
```

إضافة secret باستخدام GitHub CLI:

```bash
gh secret set VPS_HOST --body "168.231.106.139"
gh secret set VPS_USER --body "root"
gh secret set VPS_SSH_PORT --body "22"
gh secret set VPS_APP_DIR --body "/var/www/kasserpro-pharmacy"
gh secret set VPS_SERVICE_NAME --body "kasserpro-pharmacy"
gh secret set VPS_HEALTH_PORT --body "5245"
gh secret set VPS_DOMAIN --body "pharmacy.azinternational-eg.com"
gh secret set CERTBOT_EMAIL --body "admin@azinternational-eg.com"
gh secret set VPS_SSH_KEY < ~/.ssh/YOUR_PRIVATE_KEY
```

## 11. SSH key للـ GitHub Actions

لو لا يوجد key جاهز:

على جهازك:

```bash
ssh-keygen -t ed25519 -C "github-actions-kasserpro" -f ~/.ssh/kasserpro_github_actions
```

على السيرفر أضف public key:

```bash
cat ~/.ssh/kasserpro_github_actions.pub
```

انسخ الناتج إلى:

```bash
nano /root/.ssh/authorized_keys
```

ثم:

```bash
chmod 700 /root/.ssh
chmod 600 /root/.ssh/authorized_keys
```

ضع private key في GitHub Secret باسم:

```text
VPS_SSH_KEY
```

لا تضع private key في repo.

## 12. Vercel frontend configuration

في Vercel لكل frontend project، اضبط:

Cashier:

```text
VITE_API_URL=https://cashier.azinternational-eg.com/api
```

Restaurant:

```text
VITE_API_URL=https://restaurant.azinternational-eg.com/api
```

Pharmacy:

```text
VITE_API_URL=https://pharmacy.azinternational-eg.com/api
```

لو الفرونت والباك على نفس الدومين، يمكن عدم ضبط `VITE_API_URL` لأن الكود سيستخدم:

```text
window.location.origin + /api
```

لكن في Vercel غالبا الفرونت على دومين مختلف، لذلك اضبط `VITE_API_URL`.

## 13. Health checks

داخل السيرفر:

```bash
curl http://127.0.0.1:5243/api/health
curl http://127.0.0.1:5244/api/health
curl http://127.0.0.1:5245/api/health
```

من الخارج:

```bash
curl https://cashier.azinternational-eg.com/api/health
curl https://restaurant.azinternational-eg.com/api/health
curl https://pharmacy.azinternational-eg.com/api/health
```

المتوقع:

```json
{
  "status": "healthy",
  "database": {
    "status": "connected"
  }
}
```

## 14. System Owner

### 14.1 ما هو System Owner؟

هو المستخدم الأعلى على مستوى النظام كله. يمكنه إدارة tenants/users من مسارات System Owner.

الصفحة المهمة في الفرونت:

```text
/owner/users
```

مثال:

```text
https://restaurant.azinternational-eg.com/owner/users
```

### 14.2 بيانات System Owner الافتراضية عند قاعدة بيانات جديدة

لو قاعدة البيانات غير موجودة والتطبيق بدأ من الصفر، الكود ينشئ System Owner تلقائيا.

الافتراضي في الكود:

```text
Email: owner@kasserpro.com
Password: Owner@123
```

هذا يحدث فقط لو لا يوجد أي user بدور `SystemOwner`.

### 14.3 متغير seed password

يمكن تغيير باسورد أول System Owner يتم إنشاؤه عن طريق systemd environment:

```text
KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD
```

مثال داخل service file:

```ini
Environment=KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD=PUT_STRONG_OWNER_PASSWORD_HERE
```

ثم:

```bash
systemctl daemon-reload
systemctl restart SERVICE_NAME
```

مهم جدا:

- هذا المتغير يؤثر فقط عند إنشاء System Owner لأول مرة في DB جديدة.
- لو DB موجودة وبها SystemOwner بالفعل، لن يغير الباسورد.
- لا تضع الباسورد الحقيقي في GitHub أو documentation.
- احفظه في Password Manager.

### 14.4 ماذا يحدث لو الداتابيز اتمسحت؟

لو حذفت:

```text
kasserpro.db
kasserpro.db-wal
kasserpro.db-shm
```

ثم شغلت التطبيق:

- لو `KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD` موجود، سيتم إنشاء owner بهذا الباسورد.
- لو المتغير غير موجود، سيتم إنشاء owner بـ `Owner@123`.

لذلك في production الأفضل ضبط `KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD` لكل service.

### 14.5 تغيير باسورد System Owner من الواجهة

1. سجل دخول كـ System Owner.
2. افتح:

```text
/owner/users
```

3. ابحث عن:

```text
owner@kasserpro.com
```

4. استخدم زر reset/change password.
5. سجل خروج وادخل بالباسورد الجديد.

الباسورد الجديد يتخزن داخل قاعدة البيانات الحالية فقط.

### 14.6 تغيير الباسورد من API

تحتاج token من System Owner.

احصل على token من login:

```bash
curl -sS -X POST https://APP_DOMAIN/api/auth/login \
  -H "Content-Type: application/json" \
  --data '{"email":"owner@kasserpro.com","password":"CURRENT_PASSWORD"}'
```

ثم اعرف users:

```bash
curl -sS https://APP_DOMAIN/api/system/users \
  -H "Authorization: Bearer ACCESS_TOKEN"
```

ثم reset password:

```bash
curl -sS -X POST https://APP_DOMAIN/api/system/users/USER_ID/reset-password \
  -H "Authorization: Bearer ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"newPassword":"NEW_STRONG_PASSWORD"}'
```

### 14.7 لو System Owner اتقفل أو الباسورد ضاع

لا تضف endpoint عام بدون حماية لتغيير الباسورد.

الطريقة الآمنة:

1. اعمل backup من DB.
2. أوقف service.
3. شغل maintenance script مؤقت من GitHub Actions أو SSH.
4. script يغير `PasswordHash` و `SecurityStamp` للمستخدم ذي `Role = 2`.
5. شغل service.
6. اختبر login.
7. احذف workflow/secret المؤقت.

الخطوات التي تم تنفيذها سابقا للـ Restaurant كانت:

- إنشاء GitHub secret مؤقت للباسورد الجديد.
- إنشاء workflow مؤقت يعمل reset للـ System Owner.
- workflow عمل backup للـ DB قبل التعديل.
- أوقف `kasserpro-restaurant`.
- عدل PasswordHash و SecurityStamp.
- شغل service مرة أخرى.
- تم اختبار login.
- تم حذف workflow المؤقت.
- تم حذف GitHub secret المؤقت.

لا تترك workflow reset password موجود في repo بعد انتهاء المهمة.

## 15. Reset DB لتطبيق واحد فقط

تحذير: هذا يحذف داتا التطبيق المحدد فقط. خذ backup أولا.

Restaurant example:

```bash
systemctl stop kasserpro-restaurant
cp -a /var/www/kasserpro-restaurant/kasserpro.db \
      /var/www/kasserpro-restaurant/backups/kasserpro.db.manual-backup-$(date +%Y%m%d-%H%M%S).bak
rm -f /var/www/kasserpro-restaurant/kasserpro.db \
      /var/www/kasserpro-restaurant/kasserpro.db-wal \
      /var/www/kasserpro-restaurant/kasserpro.db-shm
systemctl start kasserpro-restaurant
```

Pharmacy:

```bash
systemctl stop kasserpro-pharmacy
cp -a /var/www/kasserpro-pharmacy/kasserpro.db \
      /var/www/kasserpro-pharmacy/backups/kasserpro.db.manual-backup-$(date +%Y%m%d-%H%M%S).bak
rm -f /var/www/kasserpro-pharmacy/kasserpro.db \
      /var/www/kasserpro-pharmacy/kasserpro.db-wal \
      /var/www/kasserpro-pharmacy/kasserpro.db-shm
systemctl start kasserpro-pharmacy
```

Cashier:

```bash
systemctl stop kasserpro
cp -a /var/www/kasserpro/kasserpro.db \
      /var/www/kasserpro/backups/kasserpro.db.manual-backup-$(date +%Y%m%d-%H%M%S).bak
rm -f /var/www/kasserpro/kasserpro.db \
      /var/www/kasserpro/kasserpro.db-wal \
      /var/www/kasserpro/kasserpro.db-shm
systemctl start kasserpro
```

بعد reset DB، راجع قسم System Owner لأن login قد يرجع إلى seed password.

## 16. Manual deploy بدون GitHub Actions

لو احتجت deploy يدوي:

على جهازك:

```bash
cd backend/KasserPro.API
dotnet publish -c Release -o ./publish --self-contained false /p:UseAppHost=false
cd publish
zip -qr ../kasserpro-backend.zip .
```

ارفع للسيرفر:

```bash
scp backend/KasserPro.API/kasserpro-backend.zip root@168.231.106.139:/tmp/
```

على السيرفر، مثال Pharmacy:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
APP_DIR="/var/www/kasserpro-pharmacy"
STAGE="/tmp/kasserpro-stage-$TS"
BACKUP_DIR="$APP_DIR/backups/deploy-$TS"

mkdir -p "$STAGE" "$BACKUP_DIR"
cp -a "$APP_DIR/kasserpro.db" "$BACKUP_DIR/kasserpro.db.bak" 2>/dev/null || true
cp -a "$APP_DIR/appsettings.json" "$BACKUP_DIR/appsettings.json.bak" 2>/dev/null || true
cp -a "$APP_DIR/license.key" "$BACKUP_DIR/license.key.bak" 2>/dev/null || true

systemctl stop kasserpro-pharmacy
unzip -oq /tmp/kasserpro-backend.zip -d "$STAGE"

rsync -a --delete \
  --exclude kasserpro.db \
  --exclude appsettings.json \
  --exclude appsettings.Development.json \
  --exclude license.key \
  --exclude backups \
  --exclude logs \
  "$STAGE/" "$APP_DIR/"

systemctl start kasserpro-pharmacy
systemctl status kasserpro-pharmacy
curl http://127.0.0.1:5245/api/health
```

## 17. Checklist قبل أي deploy

- [ ] الكود اتعمله commit.
- [ ] لا يوجد installer أو ملفات build ضخمة داخل Git.
- [ ] `appsettings.json` الحقيقي غير مرفوع.
- [ ] GitHub Secrets صحيحة للتطبيق.
- [ ] `VPS_APP_DIR` صحيح.
- [ ] `VPS_SERVICE_NAME` صحيح.
- [ ] `VPS_HEALTH_PORT` صحيح.
- [ ] `VPS_DOMAIN` صحيح.
- [ ] DNS يشير إلى السيرفر.
- [ ] Nginx config يوجه إلى نفس port.
- [ ] service يعمل محليا على `127.0.0.1:PORT`.
- [ ] health endpoint يرجع healthy.
- [ ] Certbot شغال والـ HTTPS يعمل.

## 18. Troubleshooting سريع

### 502 Bad Gateway

غالبا service متوقف أو Nginx يوجه إلى port غلط.

```bash
systemctl status SERVICE_NAME
journalctl -u SERVICE_NAME -n 100 --no-pager
curl http://127.0.0.1:PORT/api/health
nginx -t
```

### Login يرجع 400 INVALID_CREDENTIALS

الأسباب المحتملة:

- email/password غلط.
- DB موجودة وبها SystemOwner بباسورد مختلف.
- أنت تتوقع `Owner@123` لكن الباسورد اتغير داخل DB.

راجع قسم System Owner.

### Certbot فشل

افحص:

```bash
nslookup DOMAIN
nginx -t
systemctl status nginx
```

ثم أعد:

```bash
certbot --nginx --redirect -d DOMAIN
```

### GitHub Actions يفشل عند SSH

راجع:

- `VPS_HOST`
- `VPS_USER`
- `VPS_SSH_PORT`
- `VPS_SSH_KEY`
- أن public key موجود في `/root/.ssh/authorized_keys`

### App يعمل على السيرفر لكن الفرونت لا يتصل

راجع Vercel:

```text
VITE_API_URL=https://APP_DOMAIN/api
```

بعد تغيير env في Vercel لازم تعمل redeploy للفرونت.

## 19. ما تم عمله للـ Restaurant

تم تطبيق نفس النموذج على:

```text
Service: kasserpro-restaurant
Folder: /var/www/kasserpro-restaurant
Port: 5244
Domain: restaurant.azinternational-eg.com
Health: https://restaurant.azinternational-eg.com/api/health
```

تم:

- تجهيز GitHub Secrets.
- تشغيل workflow auto-deploy.
- بناء frontend و backend.
- نشر الملفات إلى السيرفر.
- الحفاظ على `kasserpro.db` و `appsettings.json` أثناء النشر.
- تشغيل service.
- إصدار SSL بالدومين.
- التأكد من health endpoint.
- حل مشكلة System Owner بتغيير الباسورد داخل DB الحالية بعد backup.

## 20. Summary لكل تطبيق

Cashier:

```text
Service: kasserpro
Port: 5243
Folder: /var/www/kasserpro
Domain: cashier.azinternational-eg.com
Frontend env: VITE_API_URL=https://cashier.azinternational-eg.com/api
```

Restaurant:

```text
Service: kasserpro-restaurant
Port: 5244
Folder: /var/www/kasserpro-restaurant
Domain: restaurant.azinternational-eg.com
Frontend env: VITE_API_URL=https://restaurant.azinternational-eg.com/api
```

Pharmacy:

```text
Service: kasserpro-pharmacy
Port: 5245
Folder: /var/www/kasserpro-pharmacy
Domain: pharmacy.azinternational-eg.com
Frontend env: VITE_API_URL=https://pharmacy.azinternational-eg.com/api
```
