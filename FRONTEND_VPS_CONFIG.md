# 🌐 إعداد Frontend للاتصال بـ VPS Backend

## الطريقة 1: تعديل API Base URL

### في ملف Frontend Configuration
إذا كان لديك ملف `.env` أو `config.ts` في Frontend:

```typescript
// client/src/config.ts أو .env
export const API_BASE_URL = 'https://yourdomain.com';
// أو
export const API_BASE_URL = 'http://your-vps-ip';
```

### في RTK Query Setup
```typescript
// client/src/store/api.ts
import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const api = createApi({
  baseQuery: fetchBaseQuery({
    baseUrl: process.env.REACT_APP_API_URL || 'https://yourdomain.com',
    prepareHeaders: (headers, { getState }) => {
      const token = (getState() as RootState).auth.token;
      if (token) {
        headers.set('authorization', `Bearer ${token}`);
      }
      return headers;
    },
  }),
  endpoints: () => ({}),
});
```

---

## الطريقة 2: رفع Frontend على نفس VPS

### الخيار A: Nginx يخدم Frontend + Backend

#### 1. بناء Frontend
```bash
# على جهازك المحلي
cd client
npm run build
```

#### 2. رفع Build للـ VPS
```bash
# ضغط ملفات Build
tar -czf frontend-build.tar.gz -C build .

# رفع للـ VPS
scp frontend-build.tar.gz root@your-vps-ip:/tmp/
```

#### 3. على VPS - إعداد Frontend
```bash
# إنشاء مجلد Frontend
mkdir -p /var/www/kasserpro-frontend

# فك الضغط
tar -xzf /tmp/frontend-build.tar.gz -C /var/www/kasserpro-frontend/

# تعيين الصلاحيات
chown -R www-data:www-data /var/www/kasserpro-frontend
```

#### 4. تعديل Nginx Configuration
```nginx
# /etc/nginx/sites-available/kasserpro
server {
    listen 80;
    server_name yourdomain.com www.yourdomain.com;

    # Frontend - Static Files
    location / {
        root /var/www/kasserpro-frontend;
        try_files $uri $uri/ /index.html;
        
        # Cache static assets
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }

    # Backend API
    location /api/ {
        proxy_pass http://localhost:5243/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR WebSocket
    location /hubs/ {
        proxy_pass http://localhost:5243/hubs/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_read_timeout 86400;
    }
}
```

```bash
# إعادة تحميل Nginx
nginx -t
systemctl reload nginx
```

---

## الطريقة 3: Frontend على Vercel/Netlify + Backend على VPS

### 1. إعداد Environment Variables على Vercel/Netlify
```
REACT_APP_API_URL=https://api.yourdomain.com
```

### 2. إعداد CORS على Backend
في `appsettings.json`:
```json
{
  "AllowedOrigins": [
    "https://your-frontend.vercel.app",
    "https://yourdomain.com"
  ]
}
```

### 3. إعداد Subdomain للـ API
```nginx
# /etc/nginx/sites-available/kasserpro-api
server {
    listen 80;
    server_name api.yourdomain.com;

    location / {
        proxy_pass http://localhost:5243;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 🔒 إعداد SSL للـ Frontend + Backend

### إذا كان Frontend و Backend على نفس Domain
```bash
certbot --nginx -d yourdomain.com -d www.yourdomain.com
```

### إذا كان API على Subdomain منفصل
```bash
certbot --nginx -d yourdomain.com -d www.yourdomain.com -d api.yourdomain.com
```

---

## 🧪 اختبار الاتصال

### من Frontend Console
```javascript
// افتح Developer Console في المتصفح
fetch('https://yourdomain.com/api/health')
  .then(r => r.json())
  .then(console.log)
  .catch(console.error);
```

### من Terminal
```bash
curl https://yourdomain.com/api/health
```

---

## 📝 Script لرفع Frontend

```powershell
# deploy-frontend-to-vps.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$VpsIp,
    
    [Parameter(Mandatory=$false)]
    [string]$VpsUser = "root"
)

Write-Host "🎨 Building Frontend..." -ForegroundColor Yellow
cd client
npm run build

Write-Host "📦 Creating package..." -ForegroundColor Yellow
tar -czf frontend-build.tar.gz -C build .

Write-Host "📤 Uploading to VPS..." -ForegroundColor Yellow
scp frontend-build.tar.gz ${VpsUser}@${VpsIp}:/tmp/

Write-Host "🔄 Deploying on VPS..." -ForegroundColor Yellow
ssh ${VpsUser}@${VpsIp} @"
    mkdir -p /var/www/kasserpro-frontend
    tar -xzf /tmp/frontend-build.tar.gz -C /var/www/kasserpro-frontend/
    chown -R www-data:www-data /var/www/kasserpro-frontend
    rm /tmp/frontend-build.tar.gz
"@

Remove-Item frontend-build.tar.gz
cd ..

Write-Host "✅ Frontend deployed successfully!" -ForegroundColor Green
```

---

## ✅ Checklist

- [ ] Frontend مبني بنجاح (`npm run build`)
- [ ] API Base URL محدث في Frontend
- [ ] CORS مُعد بشكل صحيح في Backend
- [ ] Nginx يخدم Frontend (إذا كان على نفس VPS)
- [ ] SSL مُفعّل للـ Frontend و Backend
- [ ] اختبار Login من Frontend
- [ ] اختبار WebSocket/SignalR (إذا مستخدم)
