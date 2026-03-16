# 📋 TODO: نظام التقارير - KasserPro

## 🎯 المرحلة 1: إكمال التقارير الجاهزة (أولوية عالية)
**الوقت المتوقع:** 3-5 أيام

### Frontend Pages (8 صفحات)

#### تقارير المبيعات
- [ ] `SalesReportPage.tsx` - صفحة تقرير المبيعات
  - عرض المبيعات اليومية
  - رسم بياني للمبيعات
  - مقارنة الفترات

#### تقارير المخزون (4 صفحات)
- [ ] `BranchInventoryReportPage.tsx` - تقرير مخزون الفرع
  - جدول المنتجات مع الكميات
  - فلتر حسب الفئة
  - تمييز المنتجات منخفضة المخزون
  - زر Export to CSV

- [ ] `UnifiedInventoryReportPage.tsx` - تقرير المخزون الموحد
  - عرض موحد لجميع الفروع
  - إجمالي الكميات
  - الفروع ذات المخزون المنخفض
  - زر Export to CSV

- [ ] `TransferHistoryReportPage.tsx` - تقرير تاريخ التحويلات
  - جدول التحويلات
  - فلتر حسب التاريخ والفرع
  - إحصائيات التحويلات
  - حالة التحويلات (مكتملة، معلقة، ملغاة)

- [ ] `LowStockSummaryReportPage.tsx` - تقرير المخزون المنخفض
  - المنتجات الحرجة (كمية = 0)
  - المنتجات منخفضة المخزون
  - قيمة إعادة التخزين المقدرة
  - تفاصيل كل فرع

#### تقارير العملاء (3 صفحات)
- [ ] `TopCustomersReportPage.tsx` - تقرير أفضل العملاء
  - جدول أفضل 20 عميل
  - إجمالي الإنفاق
  - عدد الطلبات
  - متوسط قيمة الطلب
  - آخر تاريخ طلب

- [ ] `CustomerDebtsReportPage.tsx` - تقرير ديون العملاء
  - جدول العملاء المدينين
  - إجمالي المبالغ المستحقة
  - تحليل الأعمار (Aging Analysis)
  - العملاء المتأخرين
  - تمييز العملاء الذين تجاوزوا حد الائتمان

- [ ] `CustomerActivityReportPage.tsx` - تقرير نشاط العملاء
  - العملاء الجدد vs العائدون
  - معدل الاحتفاظ بالعملاء
  - معدل فقدان العملاء
  - تقسيم العملاء
  - رسوم بيانية

### API Integration
- [ ] إضافة `inventoryReportsApi.ts` في Frontend
- [ ] تحديث `baseApi.ts` لإضافة tag "InventoryReports"

### Routes & Navigation
- [ ] تحديث `App.tsx` - التأكد من جميع الـ routes موجودة
- [ ] تحديث `MainLayout.tsx` - التأكد من القائمة الفرعية كاملة

---

## 🚀 المرحلة 2: تقارير المنتجات (أولوية متوسطة)
**الوقت المتوقع:** 5-7 أيام

### Backend Services
- [ ] `ProductReportService.cs` - تنفيذ الـ Service
  - [ ] `GetProductMovementReportAsync()`
  - [ ] `GetProfitableProductsReportAsync()`
  - [ ] `GetSlowMovingProductsReportAsync()`
  - [ ] `GetCogsReportAsync()`

### Backend Controllers
- [ ] `ProductReportsController.cs` - إنشاء Controller
  - [ ] `GET /api/product-reports/movement`
  - [ ] `GET /api/product-reports/profitable`
  - [ ] `GET /api/product-reports/slow-moving`
  - [ ] `GET /api/product-reports/cogs`

### Frontend Types & API
- [ ] `product-report.types.ts` - نقل الـ types من DTOs
- [ ] `productReportsApi.ts` - RTK Query endpoints

### Frontend Pages
- [ ] `ProductMovementReportPage.tsx`
- [ ] `ProfitableProductsReportPage.tsx`
- [ ] `SlowMovingProductsReportPage.tsx`
- [ ] `CogsReportPage.tsx`

### Registration
- [ ] تسجيل `IProductReportService` في `Program.cs`
- [ ] إضافة Routes في `App.tsx`
- [ ] تحديث القائمة في `MainLayout.tsx`

---

## 👥 المرحلة 3: تقارير الموظفين (أولوية متوسطة)
**الوقت المتوقع:** 4-6 أيام

### Backend Services
- [ ] `EmployeeReportService.cs` - تنفيذ الـ Service
  - [ ] `GetCashierPerformanceReportAsync()`
  - [ ] `GetDetailedShiftsReportAsync()`
  - [ ] `GetSalesByEmployeeReportAsync()`

### Backend Controllers
- [ ] `EmployeeReportsController.cs` - إنشاء Controller
  - [ ] `GET /api/employee-reports/cashier-performance`
  - [ ] `GET /api/employee-reports/detailed-shifts`
  - [ ] `GET /api/employee-reports/sales-by-employee`

### Frontend Types & API
- [ ] `employee-report.types.ts` - نقل الـ types من DTOs
- [ ] `employeeReportsApi.ts` - RTK Query endpoints

### Frontend Pages
- [ ] `CashierPerformanceReportPage.tsx`
- [ ] `DetailedShiftsReportPage.tsx`
- [ ] `SalesByEmployeeReportPage.tsx`

### Registration
- [ ] تسجيل `IEmployeeReportService` في `Program.cs`
- [ ] إضافة Routes في `App.tsx`
- [ ] تحديث القائمة في `MainLayout.tsx`

---

## 🏢 المرحلة 4: تقارير الموردين (أولوية منخفضة)
**الوقت المتوقع:** 4-6 أيام

### Backend Services
- [ ] `SupplierReportService.cs` - تنفيذ الـ Service
  - [ ] `GetSupplierPurchasesReportAsync()`
  - [ ] `GetSupplierDebtsReportAsync()`
  - [ ] `GetSupplierPerformanceReportAsync()`

### Backend Controllers
- [ ] `SupplierReportsController.cs` - إنشاء Controller
  - [ ] `GET /api/supplier-reports/purchases`
  - [ ] `GET /api/supplier-reports/debts`
  - [ ] `GET /api/supplier-reports/performance`

### Frontend Types & API
- [ ] `supplier-report.types.ts` - نقل الـ types من DTOs
- [ ] `supplierReportsApi.ts` - RTK Query endpoints

### Frontend Pages
- [ ] `SupplierPurchasesReportPage.tsx`
- [ ] `SupplierDebtsReportPage.tsx`
- [ ] `SupplierPerformanceReportPage.tsx`

### Registration
- [ ] تسجيل `ISupplierReportService` في `Program.cs`
- [ ] إضافة Routes في `App.tsx`
- [ ] تحديث القائمة في `MainLayout.tsx`

---

## 🎨 المرحلة 5: تحسينات UI/UX
**الوقت المتوقع:** 3-5 أيام

### Export Features
- [ ] إضافة Export to Excel لجميع التقارير
- [ ] إضافة Export to PDF
- [ ] تحسين Export to CSV (إضافة encoding UTF-8 مع BOM)

### Print Features
- [ ] إضافة Print Layout لكل تقرير
- [ ] تحسين التنسيق للطباعة
- [ ] إضافة Print Preview

### Charts & Visualizations
- [ ] إضافة مكتبة Charts (Chart.js أو Recharts)
- [ ] إضافة رسوم بيانية للمبيعات
- [ ] إضافة Pie Charts للتوزيعات
- [ ] إضافة Line Charts للاتجاهات

### Filters & Search
- [ ] إضافة فلاتر متقدمة
- [ ] إضافة بحث في التقارير
- [ ] إضافة Sort للجداول
- [ ] إضافة Pagination للتقارير الكبيرة

---

## 📊 المرحلة 6: Dashboard & Analytics
**الوقت المتوقع:** 5-7 أيام

### Dashboard Widgets
- [ ] Widget: إجمالي المبيعات اليوم
- [ ] Widget: صافي الربح الشهري
- [ ] Widget: المنتجات منخفضة المخزون
- [ ] Widget: أفضل 5 منتجات
- [ ] Widget: أفضل 5 عملاء
- [ ] Widget: المصروفات الشهرية

### Analytics
- [ ] Comparison Reports (مقارنة الفترات)
- [ ] Branch Comparison (مقارنة الفروع)
- [ ] Trend Analysis (تحليل الاتجاهات)
- [ ] Forecasting (التنبؤ بالمبيعات)

---

## 🔄 المرحلة 7: Automation & Scheduling
**الوقت المتوقع:** 7-10 أيام

### Scheduled Reports
- [ ] Backend: إضافة Hangfire أو Quartz.NET
- [ ] إنشاء جدول للتقارير الدورية
- [ ] إعدادات التقارير المجدولة

### Email Reports
- [ ] إعداد Email Service
- [ ] Templates للتقارير
- [ ] إرسال التقارير تلقائيًا
- [ ] إعدادات المستلمين

### Notifications
- [ ] إشعارات المخزون المنخفض
- [ ] إشعارات الديون المتأخرة
- [ ] إشعارات الأداء

---

## 🧪 Testing & Documentation

### Testing
- [ ] Unit Tests للـ Services
- [ ] Integration Tests للـ Controllers
- [ ] E2E Tests للصفحات الرئيسية
- [ ] Performance Tests للتقارير الكبيرة

### Documentation
- [x] `REPORTS_SYSTEM_DOCUMENTATION.md` - توثيق شامل
- [x] `REPORTS_STATUS_SUMMARY.md` - ملخص الحالة
- [ ] API Documentation - تحديث Swagger
- [ ] User Guide - دليل المستخدم
- [ ] Video Tutorials - فيديوهات تعليمية

---

## 📝 ملاحظات

### Best Practices
- استخدام `ICurrentUserService` لـ TenantId و BranchId
- التحقق من Permissions في كل Controller
- استخدام DTOs منفصلة لكل تقرير
- معالجة الأخطاء بشكل صحيح
- Logging للعمليات المهمة

### Performance
- استخدام Pagination للتقارير الكبيرة
- Caching للتقارير المتكررة
- Async/Await في جميع العمليات
- Optimize Database Queries

### Security
- التحقق من Permissions
- Validate Input Parameters
- Prevent SQL Injection
- Rate Limiting للـ API

---

## 🎯 الأولويات الحالية

### هذا الأسبوع
1. ✅ تقرير الأرباح والخسائر (مكتمل)
2. ✅ تقرير المصروفات (مكتمل)
3. ⏳ صفحة تقرير المبيعات
4. ⏳ صفحات تقارير المخزون (4 صفحات)

### الأسبوع القادم
1. صفحات تقارير العملاء (3 صفحات)
2. بدء تنفيذ تقارير المنتجات

### هذا الشهر
1. إكمال تقارير المنتجات
2. إكمال تقارير الموظفين
3. إكمال تقارير الموردين
4. تحسينات UI/UX الأساسية

---

**آخر تحديث:** 7 مارس 2026  
**الحالة:** قيد التنفيذ النشط 🚀
