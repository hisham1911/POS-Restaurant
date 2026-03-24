# 🎨 تحديثات الفرونت إند - ProductType & Custom Items

**التاريخ:** 1 مارس 2026  
**الحالة:** ✅ مكتمل - جاهز للاختبار

---

## 📝 ملخص التغييرات

### 1️⃣ تحديث الـ Types

#### `frontend/src/types/product.types.ts`
- ✅ إضافة `ProductType` enum (Physical = 1, Service = 2)
- ✅ تحديث `Product` interface لإضافة `type: ProductType`
- ✅ تحديث `CreateProductRequest` - استبدال `trackInventory` بـ `type`
- ✅ تحديث `UpdateProductRequest` - استبدال `trackInventory` بـ `type`
- ✅ تحديث `QuickCreateProductRequest` - استبدال `trackInventory` بـ `type`

#### `frontend/src/types/order.types.ts`
- ✅ تحديث `OrderItem` - جعل `productId` nullable
- ✅ إضافة `isCustomItem?: boolean` للـ OrderItem
- ✅ إضافة `AddCustomItemRequest` interface جديد

### 2️⃣ تحديث الـ API

#### `frontend/src/api/ordersApi.ts`
- ✅ إضافة `addCustomItem` mutation
- ✅ إضافة `useAddCustomItemMutation` hook
- ✅ استيراد `AddCustomItemRequest` type

### 3️⃣ تحديث المكونات

#### `frontend/src/components/pos/ProductQuickCreateModal.tsx`
- ✅ استبدال checkbox "تتبع المخزون" بـ dropdown "نوع المنتج"
- ✅ عرض حقل "الكمية الأولية" فقط للمنتجات المادية
- ✅ إضافة نص توضيحي لكل نوع منتج

#### `frontend/src/components/pos/CustomItemModal.tsx` (جديد)
- ✅ مكون جديد لإضافة منتجات مخصصة
- ✅ حقول: الاسم، السعر، الكمية، نسبة الضريبة، ملاحظات
- ✅ معاينة الإجمالي المتوقع
- ✅ تنبيه: "هذا المنتج لن يُحفظ في الكتالوج"

---

## 🎯 كيفية الاستخدام

### إنشاء منتج سريع (Quick Create)

```typescript
// الآن يستخدم ProductType بدلاً من trackInventory
const request: QuickCreateProductRequest = {
  name: "قهوة تركي",
  price: 25,
  categoryId: 1,
  type: ProductType.Physical, // أو ProductType.Service
  initialStock: 100, // فقط للمنتجات المادية
};
```

### إضافة منتج مخصص للطلب

```typescript
// منتج مخصص لا يُحفظ في الكتالوج
const customItem: AddCustomItemRequest = {
  name: "رسوم توصيل",
  unitPrice: 25,
  quantity: 1,
  taxRate: 14, // اختياري
  notes: "توصيل سريع",
};

await addCustomItem({ orderId: 123, item: customItem });
```

---

## 🔄 Migration Guide للمطورين

### قبل (Old Code)
```typescript
// ❌ الطريقة القديمة
const product = {
  name: "Product",
  trackInventory: true, // يتحكم فيه المستخدم
};
```

### بعد (New Code)
```typescript
// ✅ الطريقة الجديدة
const product = {
  name: "Product",
  type: ProductType.Physical, // يحدد النوع
  // trackInventory يتم تحديده تلقائياً
};
```

---

## 🧪 سيناريوهات الاختبار

### ✅ اختبار 1: إنشاء منتج مادي
1. افتح Quick Create Modal
2. اختر "منتج مادي" من dropdown النوع
3. تأكد من ظهور حقل "الكمية الأولية"
4. أدخل البيانات واحفظ
5. تحقق من إنشاء سجلات المخزون

### ✅ اختبار 2: إنشاء خدمة
1. افتح Quick Create Modal
2. اختر "خدمة" من dropdown النوع
3. تأكد من إخفاء حقل "الكمية الأولية"
4. أدخل البيانات واحفظ
5. تحقق من عدم إنشاء سجلات مخزون

### ✅ اختبار 3: منتج مخصص
1. افتح طلب جديد
2. اضغط زر "منتج مخصص" (يجب إضافته للـ POS)
3. أدخل: "رسوم توصيل" - 25 جنيه
4. احفظ وتحقق من إضافته للطلب
5. أكمل الطلب وتحقق من الحسابات
6. تحقق من عدم ظهوره في كتالوج المنتجات

### ✅ اختبار 4: طلب مختلط
1. أنشئ طلب جديد
2. أضف منتج مادي (قهوة)
3. أضف خدمة (تغليف)
4. أضف منتج مخصص (توصيل)
5. أكمل الطلب
6. تحقق من خصم المخزون للمنتج المادي فقط
7. استرجع الطلب
8. تحقق من إرجاع المخزون للمنتج المادي فقط

---

## 🎨 تحديثات UI المطلوبة

### 1. صفحة POS الرئيسية
```typescript
// إضافة زر "منتج مخصص" بجانب "منتج سريع"
<button onClick={() => setShowCustomItemModal(true)}>
  <Plus /> منتج مخصص
</button>
```

### 2. قائمة عناصر الطلب
```typescript
// عرض badge للمنتجات المخصصة
{item.isCustomItem && (
  <span className="badge badge-blue">مخصص</span>
)}
```

### 3. صفحة المنتجات
```typescript
// عرض نوع المنتج بدلاً من "يتتبع المخزون"
<span className={product.type === ProductType.Physical ? "badge-green" : "badge-blue"}>
  {product.type === ProductType.Physical ? "منتج مادي" : "خدمة"}
</span>
```

---

## 📦 الملفات المطلوب تحديثها

### ✅ تم التحديث
- [x] `frontend/src/types/product.types.ts`
- [x] `frontend/src/types/order.types.ts`
- [x] `frontend/src/api/ordersApi.ts`
- [x] `frontend/src/components/pos/ProductQuickCreateModal.tsx`
- [x] `frontend/src/components/pos/CustomItemModal.tsx` (جديد)

### ⏳ يحتاج تحديث
- [ ] `frontend/src/pages/pos/POSPage.tsx` - إضافة زر "منتج مخصص"
- [ ] `frontend/src/components/products/ProductForm.tsx` - استخدام ProductType
- [ ] `frontend/src/components/products/ProductList.tsx` - عرض نوع المنتج
- [ ] `frontend/src/components/orders/OrderItemsList.tsx` - عرض badge للمنتجات المخصصة
- [ ] `frontend/src/pages/products/ProductsPage.tsx` - تحديث الفلاتر

---

## 🚀 خطوات النشر

### 1. تثبيت التبعيات (إن وجدت)
```bash
cd frontend
npm install
```

### 2. التحقق من الأخطاء
```bash
npm run type-check
npm run lint
```

### 3. البناء
```bash
npm run build
```

### 4. الاختبار
```bash
npm run dev
# افتح http://localhost:3000
# اختبر السيناريوهات أعلاه
```

---

## 🐛 مشاكل محتملة وحلولها

### المشكلة 1: TypeScript Errors
```
Property 'type' does not exist on type 'Product'
```
**الحل:** تأكد من تحديث `product.types.ts` وإعادة تشغيل TypeScript server

### المشكلة 2: API 400 Bad Request
```
TrackInventory is required
```
**الحل:** تأكد من تشغيل الباك إند المحدث وتطبيق الـ migration

### المشكلة 3: Custom Item لا يظهر
```
useAddCustomItemMutation is not defined
```
**الحل:** تأكد من استيراد الـ hook من `ordersApi.ts`

---

## 📞 الدعم

**أسئلة؟** اتصل بفريق التطوير  
**مشاكل؟** أنشئ ticket مع label `frontend-refactoring`  
**اقتراحات؟** نرحب بها في #kasserpro-frontend

---

## ✨ الخطوات التالية

### قصيرة المدى
1. إضافة زر "منتج مخصص" في POS
2. تحديث صفحة المنتجات لعرض النوع
3. إضافة فلتر حسب نوع المنتج
4. اختبار E2E شامل

### متوسطة المدى
1. إضافة قوالب للمنتجات المخصصة الشائعة
2. تقارير للمنتجات المخصصة
3. إحصائيات استخدام المنتجات المخصصة

### طويلة المدى
1. تحويل منتج مخصص إلى منتج كتالوج
2. اقتراحات ذكية للمنتجات المخصصة
3. دعم أنواع منتجات إضافية (Digital, Subscription)
