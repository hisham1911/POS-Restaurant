# 📊 نظام التقارير الشامل - KasserPro

## 📋 جدول المحتويات
1. [نظرة عامة](#نظرة-عامة)
2. [التقارير المتوفرة](#التقارير-المتوفرة)
3. [التقارير المنفذة](#التقارير-المنفذة)
4. [التقارير قيد التطوير](#التقارير-قيد-التطوير)
5. [البنية التقنية](#البنية-التقنية)
6. [API Endpoints](#api-endpoints)

---

## 🎯 نظرة عامة

نظام التقارير في KasserPro يوفر رؤية شاملة لأداء الأعمال من خلال تقارير متنوعة تغطي:
- المبيعات والإيرادات
- المخزون والمنتجات
- المصروفات والأرباح
- العملاء والموردين
- أداء الموظفين

---

## ✅ التقارير المتوفرة

### 1. تقارير المبيعات (Sales Reports)

#### 1.1 التقرير اليومي (Daily Report) ✅
**الحالة:** منفذ بالكامل (Backend + Frontend)

**المسار:** `/reports`

**البيانات المتوفرة:**
- معلومات الورديات المغلقة في اليوم
- إحصائيات الطلبات (مكتملة، ملغاة، معلقة)
- إجمالي المبيعات والضرائب والخصومات
- المرتجعات
- تفصيل طرق الدفع (نقدي، بطاقة، فوري)
- أعلى 10 منتجات مبيعًا
- المبيعات بالساعة

**API Endpoint:**
```
GET /api/reports/daily?date=2026-03-07
```

**DTO:**
```csharp
DailyReportDto {
  Date, BranchId, BranchName,
  TotalShifts, Shifts[],
  TotalOrders, CompletedOrders, CancelledOrders,
  GrossSales, TotalDiscount, NetSales, TotalTax, TotalSales,
  TotalRefunds, TotalCash, TotalCard, TotalFawry,
  TopProducts[], HourlySales[]
}
```

#### 1.2 تقرير المبيعات (Sales Report) ⚠️
**الحالة:** Backend جاهز، Frontend ناقص

**المسار:** `/reports/sales`

**البيانات المتوفرة:**
- إجمالي المبيعات لفترة زمنية
- تكلفة البضاعة المباعة
- إجمالي الربح
- عدد الطلبات
- متوسط قيمة الطلب
- المبيعات اليومية

**API Endpoint:**
```
GET /api/reports/sales?fromDate=2026-03-01&toDate=2026-03-07
```

---

### 2. تقارير المخزون (Inventory Reports)

#### 2.1 تقرير مخزون الفرع ✅
**الحالة:** Backend جاهز، Frontend ناقص

**المسار:** `/reports/inventory/branch`

**البيانات المتوفرة:**
- إجمالي المنتجات في الفرع
- الكمية الإجمالية
- عدد المنتجات منخفضة المخزون
- قيمة المخزون الإجمالية
- تفاصيل كل منتج (الكمية، مستوى إعادة الطلب، التكلفة)

**API Endpoint:**
```
GET /api/inventory-reports/branch/{branchId}?categoryId=1&lowStockOnly=true
```

#### 2.2 تقرير المخزون الموحد ✅
**الحالة:** Backend جاهز، Frontend ناقص

**المسار:** `/reports/inventory/unified`

**البيانات المتوفرة:**
- عرض موحد للمخزون عبر جميع الفروع
- إجمالي الكمية لكل منتج
- عدد الفروع التي تحتوي على المنتج
- الفروع ذات المخزون المنخفض

**API Endpoint:**
```
GET /api/inventory-reports/unified?categoryId=1&lowStockOnly=true
```

#### 2.3 تقرير تاريخ التحويلات ✅
**الحالة:** Backend جاهز، Frontend ناقص

**البيانات المتوفرة:**
- إجمالي التحويلات (مكتملة، معلقة، ملغاة)
- الكمية المحولة
- تفاصيل كل تحويل
- إحصائيات التحويلات لكل فرع

**API Endpoint:**
```
GET /api/inventory-reports/transfer-history?fromDate=2026-03-01&toDate=2026-03-07&branchId=1
```

#### 2.4 تقرير المخزون المنخفض ✅
**الحالة:** Backend جاهز، Frontend ناقص

**البيانات المتوفرة:**
- عدد المنتجات منخفضة المخزون
- الفروع المتأثرة
- المنتجات الحرجة (كمية = 0)
- قيمة إعادة التخزين المقدرة

**API Endpoint:**
```
GET /api/inventory-reports/low-stock-summary?branchId=1
```

---

### 3. التقارير المالية (Financial Reports)

#### 3.1 تقرير الأرباح والخسائر ✅
**الحالة:** منفذ بالكامل (Backend + Frontend)

**المسار:** `/reports/profit-loss`

**البيانات المتوفرة:**
- **الإيرادات:**
  - إجمالي المبيعات
  - الخصومات
  - صافي المبيعات
  - الضرائب
  - إجمالي الإيرادات
  
- **تكلفة البضاعة المباعة (COGS):**
  - التكلفة الإجمالية
  - إجمالي الربح
  - هامش الربح الإجمالي
  
- **المصروفات التشغيلية:**
  - إجمالي المصروفات
  - المصروفات حسب الفئة
  
- **صافي الربح:**
  - صافي الربح/الخسارة
  - هامش صافي الربح

**API Endpoint:**
```
GET /api/financial-reports/profit-loss?fromDate=2026-03-01&toDate=2026-03-07
```

**DTO:**
```csharp
ProfitLossReportDto {
  FromDate, ToDate, BranchId, BranchName,
  GrossSales, TotalDiscount, NetSales, TotalTax, TotalRevenue,
  TotalCost, GrossProfit, GrossProfitMargin,
  TotalExpenses, ExpensesByCategory[],
  NetProfit, NetProfitMargin,
  TotalOrders, AverageOrderValue, RefundsAmount
}
```

#### 3.2 تقرير المصروفات ✅
**الحالة:** منفذ بالكامل (Backend + Frontend)

**المسار:** `/reports/expenses`

**البيانات المتوفرة:**
- إجمالي المصروفات
- عدد المصروفات
- متوسط قيمة المصروف
- المصروفات حسب الفئة
- المصروفات حسب طريقة الدفع (نقدي، بطاقة، أخرى)
- المصروفات اليومية
- أكبر 10 مصروفات

**API Endpoint:**
```
GET /api/financial-reports/expenses?fromDate=2026-03-01&toDate=2026-03-07
```

**DTO:**
```csharp
ExpensesReportDto {
  FromDate, ToDate, BranchId, BranchName,
  TotalExpenses, TotalExpenseCount, AverageExpenseAmount,
  ExpensesByCategory[], CashExpenses, CardExpenses, OtherExpenses,
  DailyExpenses[], TopExpenses[]
}
```

---

### 4. تقارير العملاء (Customer Reports)

#### 4.1 تقرير أفضل العملاء ✅
**الحالة:** Backend جاهز، Frontend ناقص

**المسار:** `/reports/customers/top`

**البيانات المتوفرة:**
- إجمالي العملاء
- العملاء النشطين
- العملاء الجدد
- إجمالي الإيرادات من العملاء
- متوسط قيمة العميل
- قائمة أفضل 20 عميل (قابلة للتخصيص)

**API Endpoint:**
```
GET /api/customer-reports/top-customers?fromDate=2026-03-01&toDate=2026-03-07&topCount=20
```

**DTO:**
```csharp
TopCustomersReportDto {
  FromDate, ToDate, BranchId, BranchName,
  TotalCustomers, ActiveCustomers, NewCustomers,
  TotalRevenue, AverageCustomerValue,
  TopCustomers[]
}
```

#### 4.2 تقرير ديون العملاء ✅
**الحالة:** Backend جاهز، Frontend ناقص

**المسار:** `/reports/customers/debts`

**البيانات المتوفرة:**
- إجمالي العملاء المدينين
- إجمالي المبلغ المستحق
- المبالغ المتأخرة
- عدد العملاء المتأخرين
- تحليل الأعمار (0-30، 31-60، 61-90، +90 يوم)
- تفاصيل ديون كل عميل

**API Endpoint:**
```
GET /api/customer-reports/debts
```

#### 4.3 تقرير نشاط العملاء ✅
**الحالة:** Backend جاهز، Frontend ناقص

**المسار:** `/reports/customers/activity`

**البيانات المتوفرة:**
- العملاء الجدد
- العملاء العائدون
- العملاء غير النشطين
- إيرادات العملاء الجدد vs العائدين
- معدل الاحتفاظ بالعملاء
- معدل فقدان العملاء
- تقسيم العملاء

**API Endpoint:**
```
GET /api/customer-reports/activity?fromDate=2026-03-01&toDate=2026-03-07
```

---

### 5. تقارير المنتجات (Product Reports)

#### 5.1 تقرير حركة المنتجات ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- الكمية المباعة
- إجمالي الإيرادات
- التكلفة الإجمالية
- إجمالي الربح
- هامش الربح
- المخزون الافتتاحي والختامي
- الكميات المشتراة والمحولة
- معدل الدوران
- أيام البيع

#### 5.2 تقرير المنتجات الأكثر ربحية ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- أعلى 10 منتجات ربحية
- أقل 10 منتجات ربحية
- الإيرادات والتكلفة والربح لكل منتج
- هامش الربح
- متوسط سعر البيع والتكلفة

#### 5.3 تقرير المنتجات الراكدة ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- المنتجات بطيئة الحركة
- المخزون الحالي
- الكمية المباعة
- متوسط المبيعات اليومية
- أيام المخزون المتبقية
- آخر تاريخ بيع
- قيمة المخزون المعرض للخطر

#### 5.4 تقرير تكلفة البضاعة المباعة (COGS) ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- قيمة المخزون الافتتاحي
- إجمالي المشتريات
- قيمة المخزون الختامي
- تكلفة البضاعة المباعة
- إجمالي الإيرادات
- إجمالي الربح
- هامش الربح الإجمالي
- التفصيل حسب الفئة

---

### 6. تقارير الموظفين (Employee Reports)

#### 6.1 تقرير أداء الكاشير ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- عدد الورديات
- إجمالي المبيعات
- متوسط قيمة الطلب
- الطلبات في الساعة
- معدل الإلغاء
- معدل المرتجعات
- تقييم الأداء

#### 6.2 تقرير الورديات التفصيلي ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- تفاصيل كل وردية
- الرصيد الافتتاحي والختامي
- الفرق المتوقع
- المبيعات حسب طريقة الدفع
- الورديات المغلقة قسريًا

#### 6.3 تقرير المبيعات حسب الموظف ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- إجمالي المبيعات لكل موظف
- عدد الطلبات
- متوسط قيمة الطلب
- نسبة المبيعات
- المبيعات اليومية

---

### 7. تقارير الموردين (Supplier Reports)

#### 7.1 تقرير مشتريات الموردين ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- إجمالي المشتريات من كل مورد
- عدد الفواتير
- المبالغ المدفوعة والمستحقة
- آخر تاريخ شراء
- عدد المنتجات الموردة

#### 7.2 تقرير ديون الموردين ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- إجمالي المبالغ المستحقة للموردين
- الفواتير غير المدفوعة
- أقدم فاتورة غير مدفوعة
- آخر تاريخ دفع

#### 7.3 تقرير أداء الموردين ⏳
**الحالة:** DTOs جاهزة، Services ناقصة

**البيانات المخططة:**
- عدد الفواتير
- قيمة المشتريات
- متوسط قيمة الفاتورة
- عدد المنتجات الفريدة
- معدل الدفع في الوقت المحدد
- تقييم الموثوقية

---

## 🏗️ البنية التقنية

### Backend Structure

```
KasserPro.Application/
├── DTOs/Reports/
│   ├── ReportDto.cs                    ✅ (Daily, Sales)
│   ├── InventoryReportDto.cs           ✅ (4 reports)
│   ├── FinancialReportDto.cs           ✅ (P&L, Expenses)
│   ├── CustomerReportDto.cs            ✅ (3 reports)
│   ├── EmployeeReportDto.cs            ✅ (3 reports)
│   ├── ProductReportDto.cs             ✅ (4 reports)
│   └── SupplierReportDto.cs            ✅ (3 reports)
│
├── Services/Interfaces/
│   ├── IReportService.cs               ✅
│   ├── IInventoryReportService.cs      ✅
│   ├── IFinancialReportService.cs      ✅
│   ├── ICustomerReportService.cs       ✅
│   ├── IEmployeeReportService.cs       ✅
│   ├── IProductReportService.cs        ✅
│   └── ISupplierReportService.cs       ✅
│
KasserPro.Infrastructure/Services/
├── ReportService.cs                    ✅
├── InventoryReportService.cs           ✅
├── FinancialReportService.cs           ✅
├── CustomerReportService.cs            ✅
├── EmployeeReportService.cs            ⏳
├── ProductReportService.cs             ⏳
└── SupplierReportService.cs            ⏳

KasserPro.API/Controllers/
├── ReportsController.cs                ✅
├── InventoryReportsController.cs       ✅
├── FinancialReportsController.cs       ✅
├── CustomerReportsController.cs        ✅
├── EmployeeReportsController.cs        ⏳
├── ProductReportsController.cs         ⏳
└── SupplierReportsController.cs        ⏳
```

### Frontend Structure

```
frontend/src/
├── types/
│   ├── report.types.ts                 ✅
│   ├── financial-report.types.ts       ✅
│   ├── customer-report.types.ts        ✅
│   ├── employee-report.types.ts        ⏳
│   ├── product-report.types.ts         ⏳
│   └── supplier-report.types.ts        ⏳
│
├── api/
│   ├── reportsApi.ts                   ✅
│   ├── financialReportsApi.ts          ✅
│   ├── customerReportsApi.ts           ✅
│   ├── employeeReportsApi.ts           ⏳
│   ├── productReportsApi.ts            ⏳
│   └── supplierReportsApi.ts           ⏳
│
└── pages/reports/
    ├── DailyReportPage.tsx             ✅
    ├── SalesReportPage.tsx             ⏳
    ├── ProfitLossReportPage.tsx        ✅
    ├── ExpensesReportPage.tsx          ✅
    ├── TopCustomersReportPage.tsx      ⏳
    ├── CustomerDebtsReportPage.tsx     ⏳
    ├── CustomerActivityReportPage.tsx  ⏳
    └── ... (more pages)                ⏳
```

---

## 📊 ملخص الحالة

### ✅ منفذ بالكامل (Backend + Frontend)
1. التقرير اليومي (Daily Report)
2. تقرير الأرباح والخسائر (Profit & Loss)
3. تقرير المصروفات (Expenses Report)

### ⚠️ Backend جاهز، Frontend ناقص
1. تقرير المبيعات (Sales Report)
2. تقرير مخزون الفرع (Branch Inventory)
3. تقرير المخزون الموحد (Unified Inventory)
4. تقرير تاريخ التحويلات (Transfer History)
5. تقرير المخزون المنخفض (Low Stock Summary)
6. تقرير أفضل العملاء (Top Customers)
7. تقرير ديون العملاء (Customer Debts)
8. تقرير نشاط العملاء (Customer Activity)

### ⏳ DTOs جاهزة، Services ناقصة
1. تقارير المنتجات (4 تقارير)
2. تقارير الموظفين (3 تقارير)
3. تقارير الموردين (3 تقارير)

---

## 🎯 الأولويات المقترحة

### المرحلة 1 (أسبوع واحد)
- ✅ تقرير الأرباح والخسائر
- ✅ تقرير المصروفات
- ⏳ صفحة تقرير المبيعات (Frontend)
- ⏳ صفحات تقارير المخزون (4 صفحات Frontend)

### المرحلة 2 (أسبوع واحد)
- ⏳ صفحات تقارير العملاء (3 صفحات Frontend)
- ⏳ تنفيذ تقارير المنتجات (Backend + Frontend)

### المرحلة 3 (أسبوع واحد)
- ⏳ تنفيذ تقارير الموظفين (Backend + Frontend)
- ⏳ تنفيذ تقارير الموردين (Backend + Frontend)

---

## 📝 ملاحظات مهمة

1. **Multi-Tenancy:** جميع التقارير تحترم `TenantId` و `BranchId` من `ICurrentUserService`
2. **Permissions:** جميع التقارير محمية بـ `ReportsView` permission
3. **Date Ranges:** معظم التقارير تدعم فترات زمنية مخصصة
4. **Export:** تقارير المخزون تدعم التصدير إلى CSV
5. **Performance:** التقارير تستخدم EF Core Include للتحميل الفعال

---

## 🔗 روابط مفيدة

- **Architecture:** `docs/KASSERPRO_ARCHITECTURE_MANIFEST.md`
- **API Docs:** `docs/api/API_DOCUMENTATION.md`
- **Backend:** `backend/KasserPro.API/Controllers/*ReportsController.cs`
- **Frontend:** `frontend/src/pages/reports/`

---

**آخر تحديث:** 7 مارس 2026
**الإصدار:** 1.0.0
