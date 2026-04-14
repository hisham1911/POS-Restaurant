# دليل إعادة نشر KasserPro على VPS بدون Backup للداتابيز

هذا الدليل مخصص لحالة:

- لديك تحديثات Backend وFrontend
- تريد رفع نسخة جديدة على VPS
- تريد حذف قاعدة البيانات القديمة نهائيا بدون Backup

> تحذير: هذا السيناريو يحذف البيانات القديمة بشكل نهائي.

---

## 1) بناء Frontend ثم دمجه داخل Backend

من جذر المشروع:

```powershell
# Build frontend
Push-Location frontend
npm ci
npm run build
Pop-Location

# Copy frontend dist to API wwwroot
Remove-Item -Recurse -Force backend/KasserPro.API/wwwroot/* -ErrorAction SilentlyContinue
Copy-Item -Recurse -Force frontend/dist/* backend/KasserPro.API/wwwroot/
```

---

## 2) بناء حزمة النشر (Backend + Frontend)

```powershell
$ts = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = "artifacts/backend-publish/deploy-$ts"
$zip = "artifacts/kasserpro-deploy-$ts.zip"

New-Item -ItemType Directory -Force -Path $out | Out-Null
dotnet publish backend/KasserPro.API/KasserPro.API.csproj -c Release -o $out --self-contained false

if ($LASTEXITCODE -ne 0) {
  throw "Publish failed"
}

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$out/*" -DestinationPath $zip -Force
Write-Host "ZIP: $zip"
```

---

## 3) رفع الحزمة إلى VPS

```powershell
scp artifacts/kasserpro-deploy-YYYYMMDD-HHMMSS.zip root@YOUR_VPS_IP:/root/
```

---

## 4) نشر النسخة على VPS مع حذف الداتابيز بدون Backup

```bash
ssh root@YOUR_VPS_IP

set -e
TS=$(date +%Y%m%d-%H%M%S)
APP=/var/www/kasserpro
PKG=/root/kasserpro-deploy-YYYYMMDD-HHMMSS.zip
STAGE=/root/kasserpro-stage-$TS

systemctl stop kasserpro

# Delete DB permanently (no backup)
rm -f "$APP/kasserpro.db" "$APP/kasserpro.db-wal" "$APP/kasserpro.db-shm"

mkdir -p "$STAGE"
unzip -oq "$PKG" -d "$STAGE"

if command -v rsync >/dev/null 2>&1; then
  rsync -a --delete \
    --exclude kasserpro.db \
    --exclude appsettings.json \
    --exclude appsettings.Development.json \
    --exclude license.key \
    --exclude backups \
    --exclude logs \
    "$STAGE/" "$APP/"
else
  cp -a "$STAGE/." "$APP/"
fi

SERVICE_USER=$(systemctl show -p User --value kasserpro)
SERVICE_GROUP=$(systemctl show -p Group --value kasserpro)
[ -z "$SERVICE_USER" ] && SERVICE_USER=root
[ -z "$SERVICE_GROUP" ] && SERVICE_GROUP=root
chown -R "$SERVICE_USER:$SERVICE_GROUP" "$APP"
chmod -R u+rwX,go+rX "$APP"

systemctl start kasserpro
sleep 5
systemctl is-active kasserpro
```

---

## 5) التحقق النهائي

```bash
systemctl status kasserpro --no-pager -l | head -n 25
curl -sS -m 10 http://localhost:5243/api/health
ss -ltnp | grep 5243 || true
journalctl -u kasserpro -n 50 --no-pager
```

النتيجة المتوقعة:

- الخدمة `active`
- endpoint الصحة يرجع `healthy`
- المنفذ `5243` في حالة `LISTEN`

---

## 6) ملاحظات تشغيل

- أول تشغيل بعد حذف الداتابيز سيقوم بإنشاء قاعدة جديدة وتطبيق migrations.
- لا تلمس `appsettings.json` و `license.key` أثناء النشر إلا إذا كان ذلك مقصودا.
- إذا ظهر تحذير unzip عن backslashes، تجاهله طالما خروج الأمر ليس خطأ.
