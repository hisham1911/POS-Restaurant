# إصلاح مشكلة الـ Overlay في المودالات 🎭

## المشكلة 🐛
عند فتح أي مودال (مثل تفاصيل الطلب)، كان فيه شريط في أعلى الشاشة (navbar/header) مش بيتغطى بالطبقة الضبابية (overlay)، وده كان بيخلي المستخدم يقدر يضغط على الأزرار اللي فوق والمودال مفتوح - وده خطأ كبير في تجربة المستخدم!

## السبب 🔍
المودالات كانت بتستخدم `z-50` أو `z-[60]`، وده نفس الـ z-index بتاع الـ mobile sidebar في MainLayout (`z-50`)، فكان فيه تعارض في الطبقات.

## الحل ✅
رفعنا الـ z-index بتاع **كل المودالات** في النظام من `z-50` إلى `z-[100]` عشان تكون فوق أي عنصر تاني في الصفحة.

## الملفات المعدلة 📝

### مودالات POS (نقطة البيع)
- ✅ `frontend/src/components/pos/ProductQuickCreateModal.tsx`
- ✅ `frontend/src/components/pos/CustomItemModal.tsx`
- ✅ `frontend/src/components/pos/StockAdjustmentModal.tsx`
- ✅ `frontend/src/components/pos/DiscountModal.tsx`
- ✅ `frontend/src/components/pos/CustomerQuickCreateModal.tsx`
- ✅ `frontend/src/components/pos/PaymentModal.tsx`

### مودالات الورديات
- ✅ `frontend/src/components/shifts/ShiftRecoveryModal.tsx`
- ✅ `frontend/src/components/shifts/InactivityAlertModal.tsx`
- ✅ `frontend/src/components/shifts/HandoverShiftModal.tsx`
- ✅ `frontend/src/components/shifts/ForceCloseShiftModal.tsx`

### مودالات الطلبات
- ✅ `frontend/src/components/orders/OrderDetailsModal.tsx`
- ✅ `frontend/src/components/orders/RefundModal.tsx`

### مودالات العملاء
- ✅ `frontend/src/components/customers/CustomerFormModal.tsx`
- ✅ `frontend/src/components/customers/CustomerDetailsModal.tsx`
- ✅ `frontend/src/components/customers/LoyaltyPointsModal.tsx`
- ✅ `frontend/src/pages/customers/CustomersPage.tsx` (delete confirmation)

### مودالات أخرى
- ✅ `frontend/src/components/branches/BranchFormModal.tsx`
- ✅ `frontend/src/pages/users/components/UserFormModal.tsx`
- ✅ `frontend/src/pages/backup/BackupPage.tsx` (3 modals)
- ✅ `frontend/src/components/common/Modal.tsx` (base modal)
- ✅ `frontend/src/components/common/Loading.tsx` (loading overlay)

## النتيجة 🎯
- الآن **كل المودالات** بتغطي الشاشة كلها بالطبقة الضبابية
- المستخدم **مش هيقدر** يضغط على أي حاجة تانية والمودال مفتوح
- تجربة مستخدم أفضل وأكثر احترافية

## الهيكل الجديد للطبقات (Z-Index Hierarchy)
```
z-[100] → المودالات (أعلى طبقة)
z-50    → Mobile Sidebar
z-40    → Mobile Cart
z-10    → Dropdowns & Tooltips
z-0     → Normal Content
```

## اختبار الإصلاح 🧪
1. افتح أي صفحة في النظام
2. افتح أي مودال (مثل تفاصيل طلب، إضافة منتج، إلخ)
3. تأكد إن الشاشة كلها بقت ضبابية
4. حاول تضغط على أي زرار في الـ navbar → مش هينفع!
5. اضغط خارج المودال أو على زرار الإغلاق → المودال هيقفل

## ملاحظات مهمة 📌
- الـ mobile sidebar لسه عنده `z-50` وده صح، لأن المودالات دلوقتي `z-[100]`
- لو هتضيف مودال جديد، استخدم `z-[100]` مش `z-50`
- الـ Loading Overlay كمان عنده `z-[100]` عشان يغطي كل حاجة
