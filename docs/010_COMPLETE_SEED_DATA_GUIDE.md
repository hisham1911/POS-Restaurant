# 📊 دليل البيانات الكاملة - Complete Seed Data Guide

## 🎯 نظرة عامة

تم إنشاء **4 محلات كاملة** مع بيانات واقعية شاملة لكل محل.

---

## 🏪 المحلات المتاحة (4 Tenants)

### 1. 🥩 مجزر الأمانة (Butcher Shop)

**معلومات أساسية:**
```
Tenant ID: 1
Slug: al-amana-butcher
Admin: admin@kasserpro.com / Admin@123
```

**المستخدمين (4):**
- System Owner: owner@kasserpro.com / Owner@123
- Admin: admin@kasserpro.com / Admin@123
- Cashier 1: mohamed@kasserpro.com / 123456
- Cashier 2: ali@kasserpro.com / 123456

**البيانات:**
- ✅ Categories: 3 (لحوم بقري، مفرومة، أحشاء)
- ✅ Products: 24
- ✅ Customers: 15 (VIP: 3, Regular: 5, New: 2, Restaurants: 3, Shops: 2)
- ✅ Suppliers: 5
- ✅ Expense Categories: 6
- ✅ Shifts: 15 (14 closed + 1 open)
- ✅ Orders: ~100+
- ✅ Purchase Invoices: 5
- ✅ Expenses: 15 (over 60 days)
- ✅ Cash Register Transactions: 6

**خصائص الطلبات:**
- Order Types: 60% Takeaway, 20% Delivery, 20% DineIn
- Items per Order: 1-3
- Payment: 70% Cash, 30% Card

---

### 2. 📦 محل الأمل للأدوات المنزلية (Home Appliances)

**معلومات أساسية:**
```
Tenant ID: 2
Slug: home-appliances
Admin: samy@homeappliances.com / Admin@123
```

**المستخدمين (3):**
- Admin: samy@homeappliances.com / Admin@123
- Cashier 1: nour@homeappliances.com / 123456
- Cashier 2: hoda@homeappliances.com / 123456

**البيانات:**
- ✅ Categories: 3 (أدوات مطبخ، أجهزة كهربائية، أواني)
- ✅ Products: 5
  - طقم سكاكين: 150 ج.م
  - مقشرة خضار: 25 ج.م
  - خلاط كهربائي: 450 ج.م
  - محمصة خبز: 320 ج.م
  - طقم أطباق: 280 ج.م
- ✅ Customers: 9 (VIP: 2, Regular: 5, New: 2)
- ✅ Expense Categories: 6
- ✅ Shifts: 11 (10 closed + 1 open)
- ✅ Orders: ~60-80
- ✅ Expenses: 8
- ✅ Cash Register Transactions: 4

**خصائص الطلبات:**
- Order Types: 100% Takeaway
- Items per Order: 1-3
- Payment: 60% Card, 40% Cash

---

### 3. 🛒 سوبر ماركت الخير (Supermarket)

**معلومات أساسية:**
```
Tenant ID: 3
Slug: supermarket
Admin: karim@supermarket.com / Admin@123
```

**المستخدمين (4):**
- Admin: karim@supermarket.com / Admin@123
- Cashier 1: fatma@supermarket.com / 123456
- Cashier 2: zainab@supermarket.com / 123456
- Cashier 3: mariam@supermarket.com / 123456

**البيانات:**
- ✅ Categories: 3 (بقالة، مشروبات، منظفات)
- ✅ Products: 4
  - أرز: 45 ج.م
  - سكر: 35 ج.م
  - عصير: 15 ج.م
  - صابون: 20 ج.م
- ✅ Customers: 13 (VIP: 3, Regular: 5, New: 2, Wholesale: 3)
- ✅ Expense Categories: 6
- ✅ Shifts: 13 (12 closed + 1 open)
- ✅ Orders: ~150-200 (أكثر محل مبيعات)
- ✅ Expenses: 10
- ✅ Cash Register Transactions: 5

**خصائص الطلبات:**
- Order Types: 100% Takeaway
- Items per Order: 2-4 (أكثر من المحلات الأخرى)
- Payment: 50% Cash, 50% Card

---

### 4. 🍽️ مطعم الأمير (Restaurant)

**معلومات أساسية:**
```
Tenant ID: 4
Slug: restaurant
Admin: tarek@restaurant.com / Admin@123
```

**المستخدمين (3):**
- Admin: tarek@restaurant.com / Admin@123
- Cashier 1: omar@restaurant.com / 123456
- Cashier 2: youssef@restaurant.com / 123456

**البيانات:**
- ✅ Categories: 3 (مشويات، مقبلات، مشروبات)
- ✅ Products: 4
  - كباب: 80 ج.م
  - كفتة: 70 ج.م
  - سلطة: 25 ج.م
  - عصير طازج: 30 ج.م
- ✅ Customers: 12 (VIP: 3, Regular: 5, New: 2, Companies: 2)
- ✅ Expense Categories: 6
- ✅ Shifts: 15 (14 closed + 1 open)
- ✅ Orders: ~120-150
- ✅ Expenses: 10
- ✅ Cash Register Transactions: 5

**خصائص الطلبات:**
- Order Types: 50% DineIn, 30% Takeaway, 20% Delivery
- Items per Order: 2-4
- Payment: 60% Card, 40% Cash
- Order Duration: 15-45 minutes (أطول من المحلات الأخرى)

---

## 📊 مقارنة شاملة

| المحل | Users | Products | Customers | Shifts | Orders | Expenses | Transactions |
|------|-------|----------|-----------|--------|--------|----------|--------------|
| مجزر الأمانة | 4 | 24 | 15 | 15 | ~100+ | 15 | 6 |
| أدوات منزلية | 3 | 5 | 9 | 11 | ~70 | 8 | 4 |
| سوبر ماركت | 4 | 4 | 13 | 13 | ~180 | 10 | 5 |
| مطعم | 3 | 4 | 12 | 15 | ~135 | 10 | 5 |

---

## 💰 المصروفات الشهرية

### مجزر الأمانة
```
رواتب: 12,000 ج.م × 2 = 24,000 ج.م
إيجار: 8,000 ج.م × 2 = 16,000 ج.م
كهرباء: 1,850 + 1,620 = 3,470 ج.م
صيانة: 450 + 680 + 320 = 1,450 ج.م
مواصلات: 280 + 310 + 265 = 855 ج.م
أخرى: 520 + 380 + 450 = 1,350 ج.م
───────────────────────────────
إجمالي: ~47,125 ج.م
```

### محل أدوات منزلية
```
رواتب: 9,000 ج.م × 2 = 18,000 ج.م
إيجار: 6,500 ج.م × 2 = 13,000 ج.م
كهرباء: 1,200 ج.م
صيانة: 380 ج.م
مواصلات: 220 ج.م
أخرى: 450 ج.م
───────────────────────────────
إجمالي: ~33,250 ج.م
```

### سوبر ماركت
```
رواتب: 15,000 ج.م × 2 = 30,000 ج.م
إيجار: 10,000 ج.م × 2 = 20,000 ج.م
كهرباء: 2,200 + 1,950 = 4,150 ج.م
صيانة: 580 + 420 = 1,000 ج.م
مواصلات: 350 ج.م
أخرى: 680 ج.م
───────────────────────────────
إجمالي: ~56,180 ج.م
```

### مطعم
```
رواتب: 18,000 ج.م × 2 = 36,000 ج.م
إيجار: 12,000 ج.م × 2 = 24,000 ج.م
كهرباء: 2,800 + 2,500 = 5,300 ج.م
صيانة: 850 + 620 = 1,470 ج.م
مواصلات: 420 ج.م
أخرى: 780 ج.م
───────────────────────────────
إجمالي: ~67,970 ج.م
```

---

## 🎯 سيناريوهات العرض التفصيلية

### سيناريو 1: عرض على صاحب محل أدوات منزلية

**الخطوات:**
1. تسجيل الدخول: `samy@homeappliances.com / Admin@123`
2. عرض Dashboard:
   - إجمالي المبيعات: ~15,000 ج.م
   - عدد الطلبات: ~70
   - متوسط الطلب: ~215 ج.م
3. عرض المنتجات (5 منتجات)
4. إنشاء طلب جديد:
   - خلاط كهربائي × 1 = 450 ج.م
   - طقم سكاكين × 1 = 150 ج.م
   - Subtotal: 600 ج.م
   - Tax (14%): 84 ج.م
   - Total: 684 ج.م
5. قبول الدفع (بطاقة)
6. طباعة الفاتورة
7. عرض التقارير:
   - تقرير المبيعات اليومية
   - تقرير المخزون
   - تقرير المصروفات

**النقاط المميزة:**
- ✅ منتجات متنوعة (أدوات + أجهزة)
- ✅ أسعار واقعية
- ✅ مخزون كافي
- ✅ تقارير دقيقة

---

### سيناريو 2: عرض على صاحب سوبر ماركت

**الخطوات:**
1. تسجيل الدخول: `karim@supermarket.com / Admin@123`
2. عرض Dashboard:
   - إجمالي المبيعات: ~35,000 ج.م (أعلى مبيعات)
   - عدد الطلبات: ~180
   - متوسط الطلب: ~195 ج.م
3. عرض المنتجات (4 منتجات بقالة)
4. إنشاء طلب سريع:
   - أرز × 2 = 90 ج.م
   - سكر × 1 = 35 ج.م
   - عصير × 3 = 45 ج.م
   - صابون × 2 = 40 ج.م
   - Subtotal: 210 ج.م
   - Tax (14%): 29.4 ج.م
   - Total: 239.4 ج.م
5. قبول الدفع (نقدي)
6. عرض تقرير الورديات (3 كاشيرات)
7. عرض تقرير المبيعات الشهرية

**النقاط المميزة:**
- ✅ سرعة في البيع (طلبات كثيرة)
- ✅ 3 كاشيرات (تعدد المستخدمين)
- ✅ مخزون كبير
- ✅ عملاء جملة

---

### سيناريو 3: عرض على صاحب مطعم

**الخطوات:**
1. تسجيل الدخول: `tarek@restaurant.com / Admin@123`
2. عرض Dashboard:
   - إجمالي المبيعات: ~28,000 ج.م
   - عدد الطلبات: ~135
   - متوسط الطلب: ~207 ج.م
3. عرض قائمة الطعام (4 أصناف)
4. إنشاء طلب DineIn:
   - كباب × 2 = 160 ج.م
   - سلطة × 1 = 25 ج.م
   - عصير طازج × 2 = 60 ج.م
   - Subtotal: 245 ج.م
   - Tax (14%): 34.3 ج.م
   - Total: 279.3 ج.م
   - Order Type: DineIn
5. قبول الدفع (بطاقة)
6. عرض أنواع الطلبات:
   - DineIn: 50%
   - Takeaway: 30%
   - Delivery: 20%
7. عرض تقرير الورديات (10 AM - 11 PM)

**النقاط المميزة:**
- ✅ أنواع طلبات متعددة (DineIn, Takeaway, Delivery)
- ✅ ساعات عمل طويلة
- ✅ عملاء شركات (طلبات جماعية)
- ✅ متوسط طلب أعلى

---

### سيناريو 4: عرض على مستثمر (System Owner)

**الخطوات:**
1. تسجيل الدخول: `owner@kasserpro.com / Owner@123`
2. عرض جميع المحلات (4 محلات)
3. استدعاء API: `GET /api/system/credentials`
4. عرض جميع بيانات الدخول:
   ```json
   {
     "totalUsers": 14,
     "tenants": [
       {
         "tenantName": "System",
         "users": [{"email": "owner@kasserpro.com", "password": "Owner@123"}]
       },
       {
         "tenantName": "مجزر الأمانة",
         "users": [
           {"email": "admin@kasserpro.com", "password": "Admin@123"},
           {"email": "mohamed@kasserpro.com", "password": "123456"},
           {"email": "ali@kasserpro.com", "password": "123456"}
         ]
       },
       // ... باقي المحلات
     ]
   }
   ```
5. التبديل بين الحسابات:
   - نسخ بيانات أي حساب
   - تسجيل الخروج
   - تسجيل الدخول بالحساب الجديد
6. عرض إحصائيات شاملة:
   - إجمالي المبيعات: ~78,000 ج.م
   - إجمالي الطلبات: ~485
   - إجمالي العملاء: 49
   - إجمالي المنتجات: 37

**النقاط المميزة:**
- ✅ إدارة متعددة المحلات
- ✅ عرض جميع البيانات
- ✅ تبديل سريع بين الحسابات
- ✅ تقارير شاملة

---

## 🔄 إعادة تحميل البيانات

```bash
# 1. حذف قاعدة البيانات
rm backend/KasserPro.API/kasserpro.db

# 2. إعادة التشغيل
cd backend/KasserPro.API
dotnet run

# سيتم تلقائياً:
# ✅ إنشاء قاعدة البيانات
# ✅ تطبيق Migrations
# ✅ تحميل ButcherDataSeeder (Tenant 1)
# ✅ تحميل MultiTenantSeeder (Tenants 2-4 - Basic)
# ✅ تحميل HomeAppliancesSeeder (Complete Data)
# ✅ تحميل SupermarketSeeder (Complete Data)
# ✅ تحميل RestaurantSeeder (Complete Data)
```

---

## 📝 ملاحظات مهمة

### 1. Tax Exclusive (14%)
جميع الحسابات تستخدم Tax Exclusive:
```
NetTotal = UnitPrice × Quantity
TaxAmount = NetTotal × 0.14
Total = NetTotal + TaxAmount
```

### 2. Multi-Tenancy
- كل محل له بيانات مستقلة تماماً
- System Owner يمكنه الوصول لجميع المحلات
- لا يوجد تداخل بين البيانات

### 3. Realistic Data
- جميع الأسعار واقعية
- جميع الأسماء عربية حقيقية
- جميع التواريخ منطقية (آخر 60 يوم)
- جميع الكميات معقولة

### 4. Performance
- إجمالي البيانات: ~500+ سجل
- وقت التحميل: ~5-10 ثواني
- حجم قاعدة البيانات: ~2-3 MB

---

## ✅ الخلاصة

**الآن لديك 4 محلات كاملة:**

1. ✅ **مجزر الأمانة** - بيانات كاملة (24 منتج، 15 عميل، 100+ طلب)
2. ✅ **محل أدوات منزلية** - بيانات كاملة (5 منتجات، 9 عملاء، 70 طلب)
3. ✅ **سوبر ماركت** - بيانات كاملة (4 منتجات، 13 عميل، 180 طلب)
4. ✅ **مطعم** - بيانات كاملة (4 منتجات، 12 عميل، 135 طلب)

**كل محل يحتوي على:**
- ✅ Users (Admin + Cashiers)
- ✅ Products (مناسبة لنوع المحل)
- ✅ Customers (VIP + Regular + New + Wholesale/Companies)
- ✅ Shifts (10-15 وردية)
- ✅ Orders (60-200 طلب)
- ✅ Expenses (8-15 مصروف)
- ✅ Cash Register Transactions (4-6 حركة)

**System Owner يمكنه:**
- ✅ عرض جميع المحلات
- ✅ عرض جميع بيانات الدخول
- ✅ التبديل السريع بين الحسابات
- ✅ إنشاء محلات جديدة

**جاهز للعرض على أي نوع محل! 🚀**
