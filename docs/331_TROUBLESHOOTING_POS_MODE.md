# 🔧 Troubleshooting: POS Mode Not Switching

## المشكلة
الإعدادات لا تغير وضع POS رغم أن الكود صحيح.

## ✅ الحلول المحتملة

### 1. إعادة تشغيل Frontend Server

الكود الجديد يحتاج إعادة تشغيل الـ dev server:

```bash
# في terminal الـ frontend
# اضغط Ctrl+C لإيقاف السيرفر
# ثم شغله مرة أخرى
npm run dev
```

### 2. مسح Cache المتصفح

```
1. افتح Developer Tools (F12)
2. اضغط بزر الماوس الأيمن على زر Refresh
3. اختر "Empty Cache and Hard Reload"
```

أو:

```
Chrome/Edge: Ctrl + Shift + Delete
Firefox: Ctrl + Shift + Delete
```

### 3. مسح localStorage يدوياً

افتح Console في المتصفح واكتب:

```javascript
localStorage.clear();
location.reload();
```

### 4. التحقق من الملفات

تأكد أن هذه الملفات موجودة وصحيحة:

#### ✅ `frontend/src/hooks/usePOSMode.ts`
```typescript
import { useState, useEffect } from "react";

export type POSMode = "cashier" | "standard";

const POS_MODE_KEY = "pos_mode";

export const usePOSMode = () => {
  const [mode, setModeState] = useState<POSMode>(() => {
    const saved = localStorage.getItem(POS_MODE_KEY);
    return (saved as POSMode) || "cashier";
  });

  const setMode = (newMode: POSMode) => {
    setModeState(newMode);
    localStorage.setItem(POS_MODE_KEY, newMode);
  };

  useEffect(() => {
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === POS_MODE_KEY && e.newValue) {
        setModeState(e.newValue as POSMode);
      }
    };

    window.addEventListener("storage", handleStorageChange);
    return () => window.removeEventListener("storage", handleStorageChange);
  }, []);

  return { mode, setMode };
};
```

#### ✅ `frontend/src/pages/pos/POSPage.tsx`
يجب أن يحتوي على:
```typescript
import { usePOSMode } from "@/hooks/usePOSMode";
import { Navigate } from "react-router-dom";

export const POSPage = () => {
  const { mode } = usePOSMode();

  // Redirect to workspace if mode is standard
  if (mode === "standard") {
    return <Navigate to="/pos-workspace" replace />;
  }
  
  // ... rest of code
}
```

#### ✅ `frontend/src/pages/pos/POSWorkspacePage.tsx`
يجب أن يحتوي على:
```typescript
import { usePOSMode } from "@/hooks/usePOSMode";
import { Navigate } from "react-router-dom";

export const POSWorkspacePage = () => {
  const { mode } = usePOSMode();

  // Redirect to cashier mode if mode is cashier
  if (mode === "cashier") {
    return <Navigate to="/pos" replace />;
  }
  
  // ... rest of code
}
```

### 5. اختبار يدوي في Console

افتح Console واكتب:

```javascript
// تعيين الوضع
localStorage.setItem('pos_mode', 'standard');

// قراءة الوضع
console.log(localStorage.getItem('pos_mode'));

// إعادة تحميل الصفحة
location.reload();
```

### 6. التحقق من الأخطاء

افتح Console (F12) وابحث عن أي أخطاء حمراء.

## 🧪 خطوات الاختبار

### Test 1: Manual localStorage
```javascript
// في Console
localStorage.setItem('pos_mode', 'standard');
location.href = '/pos';
// يجب أن يوجه لـ /pos-workspace
```

### Test 2: Settings UI
```
1. اذهب لـ /settings
2. اضغط على "الوضع الأساسي"
3. يجب أن تظهر toast notification
4. افتح Console واكتب: localStorage.getItem('pos_mode')
5. يجب أن يرجع "standard"
6. اذهب لـ /pos
7. يجب أن يوجه تلقائياً لـ /pos-workspace
```

### Test 3: Direct Navigation
```
1. اذهب لـ /pos-workspace مباشرة
2. إذا الوضع "cashier" → يجب أن يوجه لـ /pos
3. إذا الوضع "standard" → يجب أن يبقى في /pos-workspace
```

## 🔍 Debug Mode

أضف هذا الكود مؤقتاً في POSPage للتأكد:

```typescript
export const POSPage = () => {
  const { mode } = usePOSMode();
  
  // Debug
  console.log('🔍 POSPage - Current mode:', mode);
  console.log('🔍 localStorage:', localStorage.getItem('pos_mode'));

  if (mode === "standard") {
    console.log('🔄 Redirecting to workspace...');
    return <Navigate to="/pos-workspace" replace />;
  }
  
  console.log('✅ Staying in cashier mode');
  // ... rest
}
```

## 📝 Checklist

- [ ] أعدت تشغيل frontend server
- [ ] مسحت cache المتصفح
- [ ] مسحت localStorage
- [ ] تحققت من وجود الملفات
- [ ] لا توجد أخطاء في Console
- [ ] localStorage.getItem('pos_mode') يرجع القيمة الصحيحة
- [ ] الـ redirect يعمل عند تغيير localStorage يدوياً

## 🆘 إذا لم يعمل

### الحل النهائي: إعادة بناء كاملة

```bash
# في terminal الـ frontend
# أوقف السيرفر (Ctrl+C)

# امسح node_modules و build cache
rm -rf node_modules
rm -rf .next
rm -rf dist

# أعد تثبيت الـ dependencies
npm install

# شغل السيرفر
npm run dev
```

### تحقق من TypeScript Compilation

```bash
# في terminal الـ frontend
npx tsc --noEmit
```

إذا ظهرت أخطاء، أصلحها أولاً.

## 💡 ملاحظات مهمة

1. **Hot Reload**: بعض التغييرات تحتاج إعادة تشغيل كاملة
2. **localStorage**: يجب أن يكون متاحاً في المتصفح
3. **React Router**: تأكد أن الـ routes محدثة
4. **Import Paths**: تأكد أن `@/hooks/usePOSMode` يعمل

## 🎯 النتيجة المتوقعة

بعد تطبيق الحلول:

1. ✅ الضغط على "الوضع الأساسي" في الإعدادات
2. ✅ Toast notification تظهر
3. ✅ localStorage يتحدث
4. ✅ زيارة `/pos` توجه تلقائياً لـ `/pos-workspace`
5. ✅ الضغط على "وضع الكاشير" يرجع لـ `/pos`

---

**إذا استمرت المشكلة، شاركني:**
1. محتوى Console (F12)
2. قيمة `localStorage.getItem('pos_mode')`
3. هل السيرفر يعمل بدون أخطاء؟
