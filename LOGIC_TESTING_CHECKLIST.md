# 📋 KasserPro Logic Testing Checklist

## قبل التسليم - اختبارات الأخطاء المنطقية

### ✅ 1. المخزون (Inventory)

#### 1.1 إضافة منتج
- [ ] إضافة منتج من صفحة المنتجات → يضيف لكل الفروع
- [ ] إضافة منتج من POS → يضيف للفرع الحالي فقط
- [ ] التحقق من `BranchInventory.Quantity` مش `Product.StockQuantity`

#### 1.2 فواتير الشراء
- [ ] إنشاء فاتورة شراء (Draft)
- [ ] تأكيد الفاتورة (Confirm) → يضيف للمخزون
- [ ] التحقق: `BranchInventory.Quantity` زاد بالكمية الصحيحة
- [ ] التحقق: `Product.AverageCost` اتحدث بـ weighted average

#### 1.3 البيع من POS
- [ ] بيع منتج → يخصم من `BranchInventory` للفرع الحالي
- [ ] محاولة بيع أكثر من المخزون → رفض (إذا AllowNegativeStock = false)
- [ ] التحقق: المخزون في الفروع الأخرى لم يتأثر

#### 1.4 المرتجعات
- [ ] إنشاء طلب مرتجع (OrderType = Return)
- [ ] التحقق: المخزون زاد بالكمية المرتجعة
- [ ] التحقق: الكميات سالبة في OrderItems

#### 1.5 التحويلات بين الفروع
- [ ] تحويل 30 وحدة من فرع A لفرع B
- [ ] التحقق: فرع A نقص 30
- [ ] التحقق: فرع B زاد 30
- [ ] التحقق: إجمالي المخزون ثابت

---

### ✅ 2. المالية (Financial)

#### 2.1 حساب الضريبة (Tax Exclusive)
- [ ] منتج سعره 100 جنيه
- [ ] الضريبة 14% = 14 جنيه
- [ ] الإجمالي = 114 جنيه ✅

#### 2.2 الخصومات
- [ ] خصم على مستوى المنتج (Item Discount)
- [ ] خصم على مستوى الطلب (Order Discount)
- [ ] التحقق: الضريبة تحسب بعد الخصم
- [ ] مثال: 100 - 10% خصم = 90 + 14% ضريبة = 102.6 ✅

#### 2.3 الدفع
- [ ] دفع كامل → AmountDue = 0
- [ ] دفع جزئي (مع عميل) → AmountDue = Total - AmountPaid
- [ ] دفع جزئي (بدون عميل) → رفض
- [ ] دفع زائد → ChangeAmount = AmountPaid - Total

#### 2.4 حد الائتمان
- [ ] عميل حد ائتمانه 1000 جنيه
- [ ] مديونية حالية 800 جنيه
- [ ] طلب جديد 300 جنيه → رفض ❌
- [ ] طلب جديد 150 جنيه → قبول ✅

---

### ✅ 3. الورديات (Shifts)

#### 3.1 فتح وإغلاق الوردية
- [ ] لا يمكن فتح وردية جديدة قبل إغلاق السابقة
- [ ] رصيد افتتاحي = 1000 جنيه
- [ ] مبيعات نقدية = 500 جنيه
- [ ] الرصيد المتوقع = 1500 جنيه

#### 3.2 تقرير الوردية
- [ ] إجمالي المبيعات = مجموع الطلبات المكتملة
- [ ] إجمالي النقدية = مجموع المدفوعات النقدية
- [ ] إجمالي البطاقات = مجموع مدفوعات البطاقات

---

### ✅ 4. التقارير (Reports)

#### 4.1 تقرير حركة المنتجات
```
الرصيد الحالي = الرصيد الافتتاحي + المشتريات - المبيعات + التحويلات
```
- [ ] الرصيد الافتتاحي = 0 (بعد التعديل)
- [ ] المشتريات = من فواتير الشراء
- [ ] المبيعات = من الطلبات المكتملة
- [ ] الرصيد الحالي = BranchInventory.Quantity

#### 4.2 تقرير الأرباح والخسائر
```
الربح الإجمالي = المبيعات - تكلفة البضاعة المباعة - المرتجعات
الربح الصافي = الربح الإجمالي - المصروفات
```
- [ ] المبيعات = إجمالي الطلبات المكتملة
- [ ] المرتجعات = إجمالي طلبات المرتجعات (سالبة)
- [ ] المصروفات = المصروفات المدفوعة (Status = Paid)

#### 4.3 تقرير المخزون
```
قيمة المخزون = الكمية × متوسط التكلفة
```
- [ ] الكمية = BranchInventory.Quantity
- [ ] متوسط التكلفة = Product.AverageCost
- [ ] قيمة المخزون > 0 (ليست صفر)

---

### ✅ 5. العملاء (Customers)

#### 5.1 نقاط الولاء
- [ ] طلب بقيمة 100 جنيه → 100 نقطة
- [ ] التحقق: Customer.LoyaltyPoints زاد

#### 5.2 إحصائيات العميل
- [ ] بعد كل طلب: TotalOrders++
- [ ] بعد كل طلب: TotalSpent += OrderTotal
- [ ] بعد دفع جزئي: TotalDue += AmountDue

---

### ✅ 6. الموردين (Suppliers)

#### 6.1 إحصائيات المورد
- [ ] بعد فاتورة شراء: TotalPurchases += InvoiceTotal
- [ ] بعد دفع: TotalPaid += PaymentAmount
- [ ] المتبقي: TotalDue = TotalPurchases - TotalPaid

---

### ✅ 7. Multi-Tenancy

#### 7.1 عزل البيانات
- [ ] مستخدم من Tenant A لا يرى بيانات Tenant B
- [ ] كل عملية تحفظ TenantId و BranchId صحيح
- [ ] التقارير تعرض بيانات الـ Tenant الحالي فقط

---

## 🔧 أدوات الفحص

### 1. Unit Tests
```bash
cd backend/KasserPro.Tests
dotnet test
```

### 2. Integration Tests
```bash
cd backend/KasserPro.Tests
dotnet test --filter "Category=Integration"
```

### 3. E2E Tests
```bash
cd client
npm run test:e2e
```

### 4. Manual Testing
- استخدم Postman/Thunder Client لاختبار الـ APIs
- استخدم الـ Frontend لاختبار الـ User Flows

---

## 📊 Metrics to Track

### قبل التسليم، تأكد من:

1. **Inventory Accuracy**
   - [ ] المخزون في التقارير = المخزون في BranchInventory
   - [ ] لا يوجد مخزون سالب (إلا إذا AllowNegativeStock = true)

2. **Financial Accuracy**
   - [ ] إجمالي المبيعات = مجموع الطلبات المكتملة
   - [ ] إجمالي المصروفات = مجموع المصروفات المدفوعة
   - [ ] الربح = المبيعات - التكلفة - المصروفات

3. **Data Consistency**
   - [ ] Customer.TotalSpent = مجموع طلباته
   - [ ] Supplier.TotalPurchases = مجموع فواتير الشراء منه
   - [ ] Shift.TotalCash = مجموع المدفوعات النقدية في الوردية

---

## 🚨 Common Logic Errors to Watch For

### ❌ أخطاء شائعة:

1. **Double Counting**
   - إضافة للمخزون في Product.StockQuantity و BranchInventory معاً

2. **Missing Updates**
   - تحديث المخزون في مكان وعدم تحديثه في مكان آخر

3. **Wrong Calculation Order**
   - حساب الضريبة قبل الخصم (خطأ)
   - الصحيح: الخصم أولاً ثم الضريبة

4. **Multi-Branch Issues**
   - خصم من مخزون كل الفروع بدل الفرع الحالي
   - إضافة لكل الفروع بدل الفرع المحدد

5. **Status Checks**
   - عدم التحقق من Status قبل العمليات
   - مثال: تعديل طلب Completed (يجب أن يكون Draft فقط)

---

## ✅ Final Checklist

قبل التسليم:

- [ ] كل الـ Unit Tests تعدي
- [ ] كل الـ Integration Tests تعدي
- [ ] كل الـ E2E Tests تعدي
- [ ] Manual Testing للـ Critical Flows
- [ ] مراجعة التقارير (الأرقام منطقية)
- [ ] اختبار Multi-Tenant (عزل البيانات)
- [ ] اختبار Multi-Branch (المخزون مستقل)
- [ ] اختبار الأداء (Load Testing)
- [ ] مراجعة الـ Logs (لا توجد أخطاء)

---

## 📚 Resources

- [POS Testing Best Practices](https://sandra-parker.medium.com/pos-testing-in-retail-how-to-test-point-of-sale-systems-2fc5786be16e)
- [Inventory Management Challenges](https://koronapos.com/blog/inventory-management-challenges/)
- [Multi-Location POS Checks](https://supy.io/en-au/blog/10-pos-checks-every-multi-location-operator-should-run-before-scaling)
