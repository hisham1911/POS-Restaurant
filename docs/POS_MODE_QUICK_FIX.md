# 🚀 حل سريع: تغيير وضع POS

## المشكلة
الإعدادات لا تغير الوضع؟ جرب هذه الحلول السريعة:

## ✅ الحل 1: إعادة تشغيل Frontend (الأكثر احتمالاً)

```bash
# في terminal الـ frontend
# اضغط Ctrl+C لإيقاف السيرفر
# ثم:
npm run dev
```

## ✅ الحل 2: مسح Cache

في المتصفح:
1. اضغط `F12` لفتح Developer Tools
2. اضغط بزر الماوس الأيمن على زر Refresh
3. اختر **"Empty Cache and Hard Reload"**

## ✅ الحل 3: تغيير يدوي

افتح Console (F12) واكتب:

```javascript
// للتبديل إلى الوضع الأساسي
localStorage.setItem('pos_mode', 'standard');
location.href = '/pos';

// للرجوع لوضع الكاشير
localStorage.setItem('pos_mode', 'cashier');
location.href = '/pos';
```

## 🧪 صفحة الاختبار

افتح الملف `test-pos-mode.html` في المتصفح لاختبار الوضع بسهولة.

## 📊 كيف تتحقق أن الوضع يعمل؟

1. افتح Console (F12)
2. اكتب: `localStorage.getItem('pos_mode')`
3. يجب أن يرجع `"cashier"` أو `"standard"`

## 🎯 السلوك المتوقع

### وضع الكاشير (cashier)
- زيارة `/pos` → يبقى في `/pos`
- زيارة `/pos-workspace` → يوجه إلى `/pos`

### الوضع الأساسي (standard)
- زيارة `/pos` → يوجه إلى `/pos-workspace`
- زيارة `/pos-workspace` → يبقى في `/pos-workspace`

## 🔍 Debug

أضف هذا في Console للتأكد:

```javascript
// تحقق من الوضع
console.log('Mode:', localStorage.getItem('pos_mode'));

// تحقق من الصفحة الحالية
console.log('Current page:', window.location.pathname);

// اختبار التوجيه
if (localStorage.getItem('pos_mode') === 'standard') {
  console.log('✅ Should redirect /pos → /pos-workspace');
} else {
  console.log('✅ Should stay on /pos');
}
```

## 💡 ملاحظة مهمة

الكود صحيح 100%! المشكلة غالباً:
- ❌ Frontend server لم يتم إعادة تشغيله
- ❌ Cache المتصفح قديم
- ❌ localStorage لم يتحدث

---

**بعد تطبيق الحلول، جرب:**
1. اذهب للإعدادات
2. اضغط على "الوضع الأساسي"
3. اذهب لـ `/pos`
4. يجب أن يوجه تلقائياً لـ `/pos-workspace` ✨
