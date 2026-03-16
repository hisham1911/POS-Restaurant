# إصلاح مشكلة الخطوط على Windows 7

## المشكلة
- الخطوط والأيقونات لا تعمل بشكل صحيح على Windows 7 أوفلاين
- تأخير ملحوظ (ثانية) في تحميل الخطوط عند بدء التطبيق
- FOIT (Flash of Invisible Text) - النص يختفي ثم يظهر

## الحل المطبق ✅

### 1. استخدام @fontsource/cairo
- الخطوط محملة محلياً من npm package
- تُنسخ تلقائياً إلى `/assets/` عند البناء
- دعم كامل لـ woff و woff2

### 2. منع FOIT في HTML
```html
<style>
  html {
    font-family: 'Tahoma', 'Arial', sans-serif;
  }
  body {
    font-family: 'Cairo', 'Tahoma', 'Arial', sans-serif;
  }
</style>
```
- يعرض Tahoma فوراً (خط Windows 7 الافتراضي)
- يتحول لـ Cairo بمجرد التحميل

### 3. تحسين CSS
```css
@font-face {
  font-family: 'Cairo';
  font-display: block !important;
}
```
- `font-display: block` للتحميل الفوري
- لا يوجد تأخير أو وميض

### 4. Vite Configuration
```typescript
optimizeDeps: {
  include: ['@fontsource/cairo'],
}
```

## الملفات المعدلة

1. **frontend/index.html**
   - إضافة inline CSS لمنع FOIT
   - Tahoma كـ fallback فوري

2. **frontend/src/main.tsx**
   - استيراد @fontsource/cairo (400, 500, 600, 700)

3. **frontend/src/index.css**
   - تعريف CSS variables
   - Override font-display

4. **frontend/vite.config.ts**
   - optimizeDeps للخطوط
   - assetsInlineLimit: 0

## النتيجة المتوقعة

✅ تحميل فوري للخطوط (< 100ms)
✅ لا يوجد FOIT أو تأخير
✅ دعم كامل لـ Windows 7
✅ fallback تلقائي لـ Tahoma
✅ الأيقونات (lucide-react SVG) تعمل بشكل صحيح

## الاختبار

```bash
# 1. بناء التطبيق
cd frontend
npm run build

# 2. نسخ إلى backend (تلقائي)
Copy-Item -Path "dist\*" -Destination "..\backend\KasserPro.API\wwwroot\" -Recurse -Force

# 3. تشغيل Backend
cd ..\backend\KasserPro.API
dotnet run

# 4. افتح المتصفح على Windows 7
# http://localhost:5243
```

## التحقق من النجاح

افتح التطبيق على Windows 7 وتحقق من:
- ✅ الخطوط العربية تظهر فوراً
- ✅ لا يوجد تأخير أو وميض
- ✅ الأيقونات تعمل بشكل صحيح
- ✅ النصوص واضحة ومقروءة

## الملفات الموجودة في wwwroot/assets

```
cairo-arabic-400-normal.woff2  (13.29 KB)
cairo-arabic-400-normal.woff   (16.81 KB)
cairo-arabic-500-normal.woff2  (13.89 KB)
cairo-arabic-500-normal.woff   (17.34 KB)
cairo-arabic-600-normal.woff2  (13.90 KB)
cairo-arabic-600-normal.woff   (17.34 KB)
cairo-arabic-700-normal.woff2  (13.95 KB)
cairo-arabic-700-normal.woff   (17.26 KB)
+ Latin variants
```

## ملاحظات تقنية

- Windows 7 يدعم woff و woff2
- @fontsource يوفر كلا الصيغتين تلقائياً
- المتصفح يختار الصيغة المناسبة
- Tahoma موجود في كل نسخ Windows
- font-display: block أفضل من swap للأوفلاين
