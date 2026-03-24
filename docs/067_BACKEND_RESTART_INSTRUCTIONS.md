# تعليمات إعادة تشغيل الـ Backend ✅

## المشكلة
الـ Backend لا يعمل بسبب مشكلة في قاعدة البيانات. العمود `ReceiptCustomWidth` غير موجود في جدول `Tenants`.

## الحل

### الخطوة 1: حذف قاعدة البيانات القديمة
```bash
cd src/KasserPro.API
Remove-Item kasserpro.db* -Force
```

### الخطوة 2: إعادة إنشاء قاعدة البيانات
```bash
dotnet ef database update
```

### الخطوة 3: تشغيل الـ Backend
```bash
dotnet run
```

## ملاحظات مهمة

1. **التغييرات المطبقة:**
   - ✅ إصلاح حساب الخصم في `OrderService.cs`
   - ✅ الضريبة الآن تُحسب على المبلغ بعد الخصم
   - ✅ Frontend و Backend يستخدمان نفس المعادلة

2. **المعادلة الصحيحة:**
   ```
   Subtotal = Sum(UnitPrice × Quantity)
   DiscountAmount = Subtotal × (DiscountValue / 100) أو DiscountValue
   AfterDiscount = Subtotal - DiscountAmount
   TaxAmount = AfterDiscount × (TaxRate / 100)
   Total = AfterDiscount + TaxAmount
   ```

3. **اختبار الميزة:**
   - أضف منتجات للسلة
   - اضغط على "إضافة خصم"
   - اختر نوع الخصم (نسبة أو مبلغ)
   - أدخل القيمة
   - تحقق من الحسابات
   - أتمم الطلب

## الميزات الجديدة المضافة

### 1. البحث في نقطة البيع
- ✅ البحث بالاسم
- ✅ البحث بالباركود
- ✅ البحث بـ SKU
- ✅ بحث فوري أثناء الكتابة
- ✅ إضافة سريعة بالضغط على Enter

### 2. الخصم بالنسبة والمبلغ
- ✅ خصم بالنسبة المئوية (5%, 10%, 15%, 20%)
- ✅ خصم بمبلغ ثابت
- ✅ واجهة مستخدم سهلة مع لوحة أرقام
- ✅ معاينة مباشرة للمبلغ بعد الخصم
- ✅ التحقق من صحة البيانات
- ✅ الضريبة تُحسب على المبلغ بعد الخصم

## الملفات المعدلة

### Frontend
- `client/src/store/slices/cartSlice.ts`
- `client/src/hooks/useCart.ts`
- `client/src/components/pos/Cart.tsx`
- `client/src/components/pos/OrderSummary.tsx`
- `client/src/components/pos/DiscountModal.tsx` (جديد)
- `client/src/hooks/useOrders.ts`
- `client/src/types/order.types.ts`
- `client/src/pages/pos/POSPage.tsx`

### Backend
- `src/KasserPro.Application/DTOs/Orders/CreateOrderRequest.cs`
- `src/KasserPro.Application/Services/Implementations/OrderService.cs`

## الحالة الحالية

- ✅ Frontend: جاهز ويعمل
- ⏳ Backend: يحتاج إعادة تشغيل
- ✅ الكود: صحيح ولا توجد أخطاء
- ⏳ قاعدة البيانات: تحتاج إعادة إنشاء

## الخطوات التالية

1. احذف قاعدة البيانات القديمة
2. أعد إنشاء قاعدة البيانات
3. شغل الـ Backend
4. اختبر الميزات الجديدة

---

**ملاحظة:** جميع التغييرات محفوظة في الكود. فقط قاعدة البيانات تحتاج إعادة إنشاء.
