# 📊 KasserPro — Report Explanation Prompts (v4 — Architecture-Grounded)

> الـ agent يبحث بنفسه ويقرر إيه اللي يقراه — لكن البرومبتس دلوقتي بتديله **خريطة دقيقة للخدمات الفعلية في الكود** عشان مايضيعش.
> مقسّمة على 12 prompt — Prompt 13 في الآخر بيجمع كل حاجة.

---

# 🗺️ خريطة الكود — اقرأها مرة واحدة قبل أي برومبت

التقارير في KasserPro موزّعة على **8 خدمات منفصلة** (مش خدمة واحدة):

| الخدمة | الموقع | يشمل |
|--------|--------|-------|
| `IReportService` | `backend/KasserPro.Application/Services/Interfaces/IReportService.cs` | Daily, Sales |
| `IFinancialReportService` | نفس المجلد | P&L, Expenses |
| `IProductReportService` | نفس المجلد | ProductMovement, ProfitableProducts, **SlowMoving**, COGS |
| `IInventoryReportService` | نفس المجلد | BranchInventory, UnifiedInventory, TransferHistory, LowStock |
| `ICustomerReportService` | نفس المجلد | TopCustomers, CustomerDebts, CustomerActivity |
| `ISupplierReportService` | نفس المجلد | SupplierPurchases, SupplierDebts, SupplierPerformance |
| `IEmployeeReportService` | نفس المجلد | CashierPerformance, DetailedShifts, SalesByEmployee |
| `ICashRegisterService.GetSummaryAsync` | نفس المجلد | CashRegisterSummary (**مش في ReportService**) |

التطبيقات الفعلية في `backend/KasserPro.Infrastructure/Services/{Name}ReportService.cs`.
الـ Controllers في `backend/KasserPro.API/Controllers/{Name}ReportsController.cs`.
صفحات الفرونت في `frontend/src/pages/reports/`.

---

# 📐 قواعد إلزامية على كل برومبت (من `AGENTS.md` و `kasserpro-bestpractices`)

في **كل** STEP 1 — EXPLORE، الـ agent لازم يتحقق ويذكر صراحةً في المستند الناتج:

1. **Tenant + Branch isolation**: كل query لازم تتفلتر بـ `TenantId` و(لو الفرع مهم) `BranchId`. لو التقرير tenant-wide — وضّح ده بإنذار صريح.
2. **مصدر المخزون**: **`BranchInventory.Quantity`** فقط. **`Product.StockQuantity` اتشال** (migration `20260329232433`). أي تقرير يستخدمها = الكود قديم.
3. **No AutoMapper / No FluentValidation**: التقارير تستخدم `.Select(...)` projections — مش mapping خارجي.
4. **التوقيت**: مصر — تأكد فلتر التاريخ بيستخدم helper للـ Egypt timezone مش UTC مباشر.
5. **العملة الواحدة**: single-currency (جنيه مصري). لا تفترض multi-currency.
6. **Soft Delete vs Cancellation**: فيه فرق بين `IsCancelled`، `Status == OrderStatus.Cancelled`، و`IsDeleted` — اتأكد إنت بتقرأ أيهم.
7. **Open Shifts**: الورديات المفتوحة (`Status == ShiftStatus.Open`) ممكن تظهر أو تتفلتر — لازم توثّق.
8. **Permission**: كل endpoint محمي بـ `[HasPermission(Permission.X)]` — اقرأ الـ permission واذكره في "نطاق التقرير".
9. **Snapshot للتكلفة**: `OrderItem.UnitCost` snapshot وقت البيع — التغيير اللاحق على `Product.AverageCost` لا يؤثر على التقارير القديمة.
10. **Export & Caching**: لو التقرير بيدعم export PDF/Excel — اذكره. لو بيتعمله caching — اذكره.

أي قسم "✅ حالة الحساب" لازم يذكر صراحةً النقاط دي.

---

# ════════════════════════════════
# PROMPT 1 — التقرير اليومي
# ════════════════════════════════

```
MISSION: اشرح "التقرير اليومي" في KasserPro شرحاً كاملاً بلغة بشرية.

STEP 1 — EXPLORE FIRST:
الملفات بالضبط:
- backend/KasserPro.Application/Services/Interfaces/IReportService.cs → GetDailyReportAsync
- backend/KasserPro.Infrastructure/Services/ReportService.cs (اقرأه بالكامل + dependencies)
- backend/KasserPro.Application/DTOs/Reports/ReportDto.cs → DailyReportDto
- backend/KasserPro.API/Controllers/ReportsController.cs → endpoint + permission
- frontend/src/pages/reports/DailyReportPage.tsx

تحقق صراحةً:
□ TenantId filter في كل query
□ BranchId — per-branch ولا tenant-wide؟
□ Egypt timezone (مش UTC مباشر)
□ Product.StockQuantity — لو موجود = bug، الصح BranchInventory.Quantity
□ الورديات المفتوحة بتظهر؟
□ Permission من [HasPermission(...)]

افهم متغيرات DailyReportDto:
totalSales, actualNetSales, totalCollected, deferredAmount,
payment buckets (cash/card/instapay/transfer), shift summaries,
gross sales, total discount, total tax, total refunds.

STEP 2 — REFUND ANALYSIS (⚠️ قبل أي رقم):

أ) المرتجعات بتتطرح في أيهم؟ GrossSales / NetSales / TotalSales / أكتر؟

ب) لو في أكتر من مكان، اتأكد إنه sequential مش double deduction —
   لو الكود بيطرح من A ثم B بيُحسب من A الناتج، فده تسلسل مش طرح مرتين.

ج) مثال رقمي ثابت من البداية للنهاية:
   مبيعات خام = 10,000 | خصم = 500 | مرتجع = 570 | ضريبة = 14%
   احسب كل metric بالترتيب من نفس الأرقام.

د) لو المرتجعات النقدية > المبيعات النقدية — في Math.Max(0,...) ولا Guard؟

STEP 3 — THINK:
- ليه المبيعات > الكاش في الخزنة؟
- وردية مفتوحة → بتظهر؟
- مرتجع النهارده على أوردر قديم → بيظهر فين؟
- البيع الآجل في totalCollected ولا لأ؟ (لأ)
- الفرق بين totalSales و actualNetSales و totalCollected

STEP 4 — WRITE:

## 1. التقرير اليومي — Daily Sales Report

### 📍 نطاق التقرير
- المستوى: per-branch / tenant-wide
- فلتر التاريخ + Egypt timezone
- يشمل الملغي / المرتجعات / الورديات المفتوحة
- Permission

### 📊 الأرقام — من أين تأتي؟
⚠️ مثال رقمي ثابت لكل القسم:
مبيعات خام = 10,000 | خصم = 500 | مرتجع = 570 | ضريبة = 14%

لكل رقم اكتب: المعادلة + المثال + ✅ بيأثر / ❌ مش بيأثر

الأرقام (من DailyReportDto):
- إجمالي المبيعات / GrossSales
- الخصومات / TotalDiscount
- صافي المبيعات / NetSales
- الضريبة / TotalTax
- المرتجعات / TotalRefunds
- إجمالي المبيعات النهائي / TotalSales أو ActualTotalSales
- المحصّل فعلياً / TotalCollected
- الآجل / TotalDeferred
- ملخص كل وردية

⚠️ تحذير: لو شايف TotalSales = NetSales + Tax - Refunds وفي نفس الوقت NetSales طرح المرتجعات بالفعل — في غلط في فهمك. ارجع للكود.

### ❓ أسئلة شائعة (7+)
- ليه الكاش أقل من المبيعات؟
- البيع الآجل بيتحسب فين؟
- وردية مفتوحة → التقرير دقيق؟
- مرتجع على أوردر قديم بيأثر فين؟
- التقرير لكل الفروع ولا فرع واحد؟
- Permission

### 🚫 مش هيظهر (4+)
### ✅ حالة الحساب
[جملة + ذكر صريح: TenantId ✓، BranchId ✓، Egypt timezone ✓/✗، BranchInventory.Quantity (لو ينطبق)، Permission المطلوب]

RULES: لا كود، مثال رقمي ثابت، وضّح متى تُطرح المرتجعات.
```

---

# ════════════════════════════════
# PROMPT 2 — تقرير المبيعات
# ════════════════════════════════

```
MISSION: اشرح "تقرير المبيعات" بلغة بشرية كاملة.

STEP 1 — EXPLORE FIRST:
- IReportService.cs → GetSalesReportAsync(fromDate, toDate)
- ReportService.cs (Infrastructure)
- ReportDto.cs → SalesReportDto
- ReportsController.cs
- frontend/src/pages/reports/SalesReportPage.tsx

تحقق:
□ TenantId + BranchId scope
□ Egypt timezone
□ سعر التكلفة snapshot (OrderItem.UnitCost) ولا current
□ Daily breakdown — CompletedAt ولا CreatedAt؟
□ Permission

افهم: grossSales, totalRefunds, totalSales, totalCost, netCost, grossProfit, averageOrderValue, dailySales chart.

STEP 2 — THINK:
- الفرق عن P&L (مفيش مصروفات هنا)
- لو عدّلت تكلفة منتج النهارده → التقارير القديمة تتأثر؟
- مرتجع في شهر مختلف → فين بيظهر؟
- chart الأيام — يشمل أيام بـ 0؟

STEP 3 — WRITE:

## 2. تقرير المبيعات — Sales Report

### 📍 نطاق التقرير
### 📊 الأرقام
نفس المثال الرقمي + تكلفة بضاعة = 6,000

من SalesReportDto:
- إجمالي المبيعات / إجمالي المرتجعات / صافي المبيعات
- تكلفة البضاعة (snapshot)
- صافي التكلفة بعد المرتجعات
- إجمالي الربح (Gross Profit — قبل المصروفات)
- متوسط قيمة الأوردر
- chart يومي

### ❓ أسئلة شائعة (7+)
- الفرق عن P&L؟ (مش بيخصم المصروفات)
- ليه الربح هنا أعلى من P&L؟
- متوسط الأوردر بيشمل المرتجعات؟
- لو غيّرت التكلفة دلوقتي → التقرير القديم؟
- Permission

### 🚫 مش هيظهر
- المصروفات / تفصيل لكل منتج / تفصيل لكل كاشير
### ✅ حالة الحساب

RULES: لا كود، وضّح الفرق عن P&L والـ snapshot للتكلفة.
```

---

# ════════════════════════════════
# PROMPT 3 — تقرير الأرباح والخسائر (P&L)
# ════════════════════════════════

```
MISSION: اشرح P&L — أهم تقرير مالي. خصص له وقت كافي.

STEP 1 — EXPLORE FIRST:
- IFinancialReportService.cs → GetProfitLossReportAsync
- backend/KasserPro.Infrastructure/Services/FinancialReportService.cs
- FinancialReportDto.cs → ProfitLossReportDto
- backend/KasserPro.API/Controllers/FinancialReportsController.cs
- frontend/src/pages/reports/ProfitLossReportPage.tsx

تحقق: TenantId + BranchId، Egypt timezone، snapshot للتكلفة، Permission.

افهم بالترتيب الفعلي:
  grossSales ← totalDiscount ← netSales
  returnNetSubtotal ← returnDiscounts (⚠️ subtotal مش total)
  actualNetSales (⚠️ مش = netSales - returnTotal — الفرق ضريبة)
  totalRevenue ← returnTotal ← actualTotalRevenue
  totalCost ← returnedCost ← netCost
  grossProfit ← grossProfitMargin
  totalExpenses (وفئاتها)
  netProfit ← netProfitMargin

⚠️ pre-tax vs tax-inclusive في المرتجعات:
- Subtotal = قبل الضريبة | Total = بعد الضريبة
- التقرير بيطرح subtotal من netSales (pre-tax)
- ويطرح total من actualTotalRevenue (tax-inclusive)

STEP 2 — THINK:
- الضريبة بتأثر على الربح الصافي ولا بس على الإيرادات؟
- المصروفات بتأثر على grossProfit ولا netProfit بس؟ (netProfit فقط)
- يوم كله مرتجعات → netProfit سالب؟
- الفرق الجوهري عن Sales Report

STEP 3 — WRITE:

## 3. تقرير الأرباح والخسائر — P&L

### 📍 نطاق التقرير
### 📊 المعادلة الكاملة خطوة بخطوة

مثال موسّع:
مبيعات خام = 10,000 | خصم = 500 | مرتجع subtotal = 500 | مرتجع total = 570 | ضريبة = 14% | تكلفة = 6,000 | تكلفة المرتجع = 350 | مصروفات = 800

**خطوة 1 — GrossSales**
**خطوة 2 — TotalDiscount**
**خطوة 3 — NetSales (قبل المرتجعات)**
**خطوة 4 — طرح المرتجعات (subtotal مش total، اشرح ليه)**
**خطوة 5 — NetCost = TotalCost - ReturnedCost**
**خطوة 6 — GrossProfit = ActualNetSales - NetCost**
**خطوة 7 — TotalExpenses بفئاتها**
**خطوة 8 — NetProfit = GrossProfit - TotalExpenses**
**خطوة 9 — GrossProfitMargin% و NetProfitMargin%**

### ❓ أسئلة شائعة (8+)
- ليه الربح هنا مختلف عن Sales Report؟
- المصروفات الكارت كمان بتخصم؟
- subtotal vs total للمرتجع — الفرق ليه مهم؟
- منتج بـ null cost — يدخل إزاي؟
- مقارنة شهرين بمصروفات مختلفة؟
- Permission

### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: لا كود، مثال يستخدم subtotal و total، خطوة بخطوة.
```

---

# ════════════════════════════════
# PROMPT 4 — المصروفات + COGS
# ════════════════════════════════

```
MISSION: اشرح تقريرين بلغة بشرية كاملة.

STEP 1 — EXPLORE FIRST:

للمصروفات:
- IFinancialReportService.cs → GetExpensesReportAsync
- FinancialReportService.cs
- FinancialReportDto.cs → ExpensesReportDto
- frontend/src/pages/reports/ExpensesReportPage.tsx
- + ExpenseService لفهم الكاش بيأثر على الخزنة

للـ COGS:
- IProductReportService.cs → GetCogsReportAsync
- backend/KasserPro.Infrastructure/Services/ProductReportService.cs (method GetCogsReportAsync)
- ProductReportDto.cs → CogsReportDto
- frontend/src/pages/reports/CogsReportPage.tsx

تحقق للـ COGS:
□ Opening + Purchases - Closing = COGS
□ closingInventoryValue fallback: AverageCost ?? Cost ?? 0 (مش Price!)
□ productsWithNoCostCount
□ Opening — تقديري ولا snapshot؟
□ المخزون السالب
□ مصدر الكميات: BranchInventory.Quantity

STEP 2 — THINK:
المصروفات:
- الكاش بيأثر على الخزنة، الكارت لأ
- مصروف بأثر رجعي في وردية مغلقة؟
- إجمالي header = backend (كل النتائج) ولا الصفحة الحالية؟

COGS:
- COGS = "بعت بضاعة كانت قيمتها كام لما اشتريتها؟"
- منتج null cost → 0 ولا exclusion؟
- الفرق عن "تكلفة البضاعة" في Sales Report و P&L
- التحويلات بين الفروع → بتأثر إزاي؟

STEP 3 — WRITE:

## 4. تقرير المصروفات — Expenses Report
### 📍 نطاق التقرير
### 📊 الأرقام
- إجمالي المصروفات (backend = كل النتائج، مش صفحة)
- توزيع الفئات
- توزيع طرق الدفع
- علاقة بالخزنة (كاش فقط)
### ❓ أسئلة شائعة (6+)
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 5. تقرير COGS
### 📍 نطاق التقرير (per-branch — يستخدم BranchInventory)
### 📊 الأرقام
مثال: افتتاحي = 50,000 | مشتريات = 30,000 | ختامي = 25,000 | بدون تكلفة = 3

- المخزون الافتتاحي (snapshot ولا تقديري؟)
- المشتريات
- المخزون الختامي (تقييم: AverageCost ?? Cost ?? 0)
- COGS = Opening + Purchases - Closing
- إجمالي الربح = Sales - COGS
- ⚠️ تحذير المنتجات بدون تكلفة

### ❓ أسئلة شائعة (6+)
- COGS بالكلام البسيط؟
- ليه مختلف عن "تكلفة البضاعة" في Sales Report؟
- منتج null cost → قيمته؟
- المخزون السالب؟
- التحويلات بين الفروع تأثيرها؟
- Permission

### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: لا كود، Closing بـ التكلفة لا البيع.
```

---

# ════════════════════════════════
# PROMPT 5 — تقارير المخزون (3 تقارير)
# ════════════════════════════════

```
MISSION: اشرح 3 تقارير مخزون بلغة بشرية كاملة.

STEP 1 — EXPLORE FIRST:
- IInventoryReportService.cs → GetBranchInventoryReportAsync, GetUnifiedInventoryReportAsync, GetLowStockSummaryReportAsync
- backend/KasserPro.Infrastructure/Services/InventoryReportService.cs
- InventoryReportDto.cs
- backend/KasserPro.API/Controllers/InventoryReportsController.cs
- frontend/src/pages/reports/{BranchInventory,UnifiedInventory,LowStockSummary}ReportPage.tsx

تحقق صراحةً:
□ مصدر الكمية = BranchInventory.Quantity (Product.StockQuantity اتشال — لو موجود = bug)
□ TenantId scope
□ TotalValue = Quantity × (AverageCost ?? 0) — مش Price
□ Unified = aggregate عبر فروع نفس Tenant
□ LowStock threshold (Product.LowStockThreshold ولا BranchInventory.MinQuantity؟)
□ Shortage = Threshold - Quantity (لو الكمية أقل)
□ EstimatedRestockCost = Shortage × AverageCost
□ Permission

افهم تغيرات الرصيد:
- بيع → -=  | مرتجع → +=  | شراء → +=
- تحويل out → -=  | تحويل in → +=  | تعديل → +/-

STEP 2 — THINK:
- قيمة المخزون = تكلفة (الصح)، مش بيع — ليه مهم محاسبياً
- منتج بـ AverageCost = null → قيمته 0 (يخفي قيمة حقيقية)
- المخزون السالب: ممكن (race condition)؟
- Branch vs Unified: متى أستخدم كل واحد؟

STEP 3 — WRITE:

## 6. Branch Inventory Report
### 📍 نطاق التقرير (per-branch، real-time)
### 📊 الأرقام
- BranchInventory.Quantity
- TotalValue = Qty × AverageCost ?? 0
- المنتجات المنخفضة
- إجمالي المنتجات / القيمة
### ❓ أسئلة شائعة (6+)
- القيمة دي تكلفة ولا بيع؟ (تكلفة)
- منتج جديد بدون شراء — قيمته؟
- المخزون السالب؟
- يشمل IsActive=false؟
- العلاقة بـ COGS
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 7. Unified Inventory Report
### 📍 نطاق التقرير (tenant-wide — كل الفروع)
### 📊 الأرقام
- SUM الكميات عبر الفروع
- AverageCost (موحد ولا per-branch؟)
- totals header
- breakdown لكل فرع
### ❓ أسئلة شائعة (6+)
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 8. Low Stock Summary
### 📍 نطاق التقرير (per-branch optional)
### 📊 الأرقام
- معيار "منخفض" (Threshold)
- Shortage = Threshold - Qty
- EstimatedRestockCost = Shortage × AverageCost
- إجمالي المنتجات / القيمة المقدرة
### ❓ أسئلة شائعة (6+)
- مين بيحدد threshold؟ المنتج ولا الفرع؟
- منتج صفر = منخفض ولا منعدم؟
- EstimatedRestockCost تقديرية ولا فعلية؟
- يشمل المعطلة؟
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: ركّز على "القيمة تكلفة مش بيع" و BranchInventory.Quantity.
```

---

# ════════════════════════════════
# PROMPT 6 — حركة المنتجات + الأكثر ربحية + بطيئة الحركة + التحويلات (4 تقارير)
# ════════════════════════════════

```
MISSION: اشرح 4 تقارير منتجات/تحويلات.

STEP 1 — EXPLORE FIRST:

ProductReports:
- IProductReportService.cs → GetProductMovementReportAsync, GetProfitableProductsReportAsync, GetSlowMovingProductsReportAsync
- backend/KasserPro.Infrastructure/Services/ProductReportService.cs
- ProductReportDto.cs

Transfers:
- IInventoryReportService.cs → GetTransferHistoryReportAsync
- InventoryReportService.cs

Frontend:
- frontend/src/pages/reports/{ProductMovement,ProfitableProducts,SlowMovingProducts,TransferHistory}ReportPage.tsx

في ProductMovement:
- qtySold = soldQty - returnedQty
- revenue / cost net بعد المرتجعات
- OpeningStock = currentStock + qtySold + transfersOut - purchases - transfersIn
  ⚠️ تقديري — مش snapshot
- TurnoverRate, DaysToSellOut

في ProfitableProducts:
- profit = revenue - cost
- profitMargin = profit / revenue * 100
- ترتيب على profit DESC

في SlowMoving:
- daysSinceLastSale من Order.CompletedAt
- Dead Stock (≥90)، Very Slow (30-89)، Slow (<30)
- TotalValueAtRisk = SUM(StockValue)
- مصدر الكمية: BranchInventory.Quantity

في TransferHistory:
- Status == Completed فقط
- NetChange = received - sent
- IsTransferOut

تحقق: TenantId + BranchId، Egypt timezone، Permission.

STEP 2 — THINK:
- Opening Stock تقديري ليه؟ (مفيش snapshot يومي)
- profit عالي vs margin عالي
- DaysToSellOut على متوسط الفترة
- Slow vs Dead — العتبات ثابتة في الكود (30/90)
- التحويلات المعلقة مفيش ليها رصيد لأن المخزون ماتحركش

STEP 3 — WRITE:

## 9. Product Movement Report
### 📍 نطاق التقرير
### 📊 الأرقام
⚠️ Opening Stock تقديري — وضّحه في أول جملة

مثال: منتج X — currentStock=20, soldQty=50, returnedQty=5, transfersOut=10, purchases=30, transfersIn=5
→ qtySold الصافي = 45
→ OpeningStock التقديري = 20 + 45 + 10 - 30 - 5 = 40

- الكمية الصافية المباعة
- الإيرادات الصافية
- التكلفة الصافية
- المخزون الافتتاحي (تقديري)
- TurnoverRate, DaysToSellOut
### ❓ أسئلة شائعة (7+)
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 10. Profitable Products Report
### 📍 نطاق التقرير
### 📊 الأرقام
- إيرادات صافية / تكلفة snapshot / ربح / margin %
- ترتيب profit DESC
### ❓ أسئلة شائعة (6+)
- profit عالي vs margin عالي
- المرتجعات تأثيرها
- منتج بدون cost
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 11. Slow-Moving Products Report
### 📍 نطاق التقرير
- per-branch، daysThreshold قابل للتعديل (افتراضي 30)
- Permission
### 📊 الأرقام
- daysSinceLastSale
- التصنيف (Dead/Very Slow/Slow)
- StockValue = Qty × AverageCost ?? 0
- TotalValueAtRisk / TotalQuantityAtRisk
### ❓ أسئلة شائعة (6+)
- منتج جديد بدون مبيعات — يظهر؟
- العتبات ثابتة في الكود؟
- استبعاد فئة معينة؟
- العلاقة بـ ProductMovement
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 12. Transfer History Report
### 📍 نطاق التقرير (Status==Completed فقط)
### 📊 الأرقام
- إجمالي وارد/صادر (Completed)
- NetChange = received - sent
- تفصيل لكل تحويل
### ❓ أسئلة شائعة (5+)
- المعلقة بتظهر؟ (لأ)
- الملغية؟
- يظهر تحويلات فروع تانية؟
- العلاقة بـ Branch Inventory
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: لا كود، وضّح Opening Stock التقديري وفلتر Status==Completed.
```

---

# ════════════════════════════════
# PROMPT 7 — تقارير العملاء (3 تقارير)
# ════════════════════════════════

```
MISSION: اشرح 3 تقارير عملاء بلغة بشرية كاملة.

STEP 1 — EXPLORE FIRST:
- ICustomerReportService.cs → GetTopCustomersReportAsync, GetCustomerDebtsReportAsync, GetCustomerActivityReportAsync
- backend/KasserPro.Infrastructure/Services/CustomerReportService.cs
- CustomerReportDto.cs
- backend/KasserPro.API/Controllers/CustomerReportsController.cs
- frontend/src/pages/reports/{TopCustomers,CustomerDebts,CustomerActivity}ReportPage.tsx

⚠️ تحقق حساس:
□ Customer.TotalDue — tenant-wide ولا per-branch؟ (المتوقع: tenant-wide)
□ CustomerBranchBalance — موجود؟ بيستخدم؟
□ التقرير عملاء الفرع الحالي ولا كل tenant؟
□ IsOverLimit logic
□ Aging brackets (0-30, 31-60, 61-90, 90+)
□ Permission

في Top Customers: أساس الترتيب، OutstandingBalance من فين.
في Activity: customerRevenue = salesTotal - returnTotal، تعريف "جديد" / "عائد"، retention/churn.

STEP 2 — THINK:
⚠️ النقطة الأخطر: لو دين العميل tenant-wide:
- اشترى آجل في فرع A → رصيده يظهر في فرع B
- يأثر على قرار الائتمان (cashier B هيرفض البيع)
- صاحب المشروع لازم يفهم ده

أسئلة:
- Balance > Limit في فرع تاني — يقدر يشتري؟
- المرتجعات تقلل الرصيد فوراً؟
- "العائد" Tenant ولا Branch؟

STEP 3 — WRITE:

## 13. Top Customers Report
### 📍 نطاق التقرير
### 📊 الأرقام
- أساس الترتيب
- إجمالي المشتريات (بعد مرتجعات؟)
- رصيد الدين
⚠️ نبّه: رصيد الدين tenant-wide
### ❓ أسئلة شائعة (6+)
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 14. Customer Debts Report
### 📍 نطاق التقرير
⚠️ tenant-wide ولا per-branch؟ (تحقق من الكود)
### 📊 الأرقام
- TotalDue (متى يتحدث: بيع آجل / سداد / إلغاء سداد)
- CreditLimit والتجاوز
- Aging brackets
- إجمالي المديونية
### ❓ أسئلة شائعة (7 — حساس)
- ليه عميل يظهر بدين وهو دفع؟
- التجاوز يمنع البيع تلقائياً؟
- الأقدمية على تاريخ الفاتورة ولا آخر دفعة؟
- رصيد سالب (overpaid)؟
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 15. Customer Activity Report
### 📍 نطاق التقرير
### 📊 الأرقام
- إيرادات بعد مرتجعات
- جدد vs عائدين (تعريف دقيق)
- Retention / Churn
- متوسط قيمة كل شريحة
### ❓ أسئلة شائعة (6+)
- "جديد" مقارنة بإيه؟
- العائد لازم اشترى مرتين؟
- Tenant ولا Branch؟
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: وضّح موضوع tenant-wide debts بأسلوب غير تقني، تنبيه في Top + Debts.
```

---

# ════════════════════════════════
# PROMPT 8 — تقارير الموردين (3 تقارير)
# ════════════════════════════════

```
MISSION: اشرح 3 تقارير موردين.

STEP 1 — EXPLORE FIRST:
- ISupplierReportService.cs
- backend/KasserPro.Infrastructure/Services/SupplierReportService.cs
- SupplierReportDto.cs
- backend/KasserPro.API/Controllers/SupplierReportsController.cs
- frontend/src/pages/reports/{SupplierPurchases,SupplierDebts,SupplierPerformance}ReportPage.tsx
- + PurchaseInvoiceService لفهم متى يتحدث Supplier.TotalDue

تحقق: TenantId scope، Permission، Egypt timezone، مرتجعات الشراء تأثيرها.

في Debts: TotalDue يتحدث في:
- confirm purchase (آجل) → +
- payment → -
- deletePayment → +
- مرتجع شراء → -

في Purchases: Outstanding = max(0, totalPurchases - totalPaid). يشمل الملغية؟

في Performance:
- avgInvoiceValue
- onTimePaymentRate (قبل DueDate)
- avgPaymentDelay (أيام)

STEP 2 — THINK:
- onTimePaymentRate يحتاج DueDate — المستخدم بيدخلها؟
- مرتجعات الشراء على Performance ولا Purchases بس؟
- الفرق بين Debts (حالي) و Purchases (تاريخي)

STEP 3 — WRITE:

## 16. Supplier Purchases
### 📍 نطاق
### 📊 الأرقام (إجمالي مشتريات / مدفوع / Outstanding / عدد فواتير)
### ❓ أسئلة شائعة (6+)
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 17. Supplier Debts
### 📍 نطاق
### 📊 الأرقام (TotalDue + متى يتحدث / UnpaidInvoicesCount / TotalOverdueAmount بـ DueDate)
### ❓ أسئلة شائعة (6+)
- سداد دفعة الآن — بيتحدث فوراً؟
- حذف دفعة بالخطأ — الرصيد بيرجع؟
- متأخرة معتمدة على DueDate — لو فاضية؟
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 18. Supplier Performance
### 📍 نطاق
### 📊 الأرقام (avgInvoiceValue / onTimePaymentRate / avgPaymentDelay / المنتجات الموردة)
### ❓ أسئلة شائعة (6+)
- onTimePaymentRate يشمل اللي لسه ماستحقتش؟
- لو مفيش DueDate — تدخل؟
- مرتجعات الشراء على Score؟
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: وضّح بدقة شروط onTimePayment.
```

---

# ════════════════════════════════
# PROMPT 9 — تقارير الموظفين (3 تقارير)
# ════════════════════════════════

```
MISSION: اشرح 3 تقارير موظفين.

STEP 1 — EXPLORE FIRST:
- IEmployeeReportService.cs → GetCashierPerformanceReportAsync, GetDetailedShiftsReportAsync, GetSalesByEmployeeReportAsync
- backend/KasserPro.Infrastructure/Services/EmployeeReportService.cs
- EmployeeReportDto.cs
- backend/KasserPro.API/Controllers/EmployeeReportsController.cs
- frontend/src/pages/reports/{CashierPerformance,ShiftDetails,SalesByEmployee}ReportPage.tsx

تحقق: TenantId + BranchId، Egypt timezone، Permission، موظف في فرعين بيظهر إزاي.

في CashierPerformance:
- totalRevenue بعد المرتجعات
- performanceScore — اقرأ المنطق (binary thresholds؟ weighted؟ المتغيرات؟)
- cancellationRate
- payment mix %

في DetailedShifts:
- TotalSales من Orders (مش payment buckets)
- ExpectedBalance = Opening + Sales(cash) + Deposits - Withdrawals - Refunds(cash) - Expenses(cash) - Transfers(out)
- Variance = ClosingBalance - ExpectedBalance

في SalesByEmployee:
- employeeRevenue بعد المرتجعات
- revenuePercentage = employeeRevenue / totalRevenue * 100
- averageOrderValue

STEP 2 — THINK:
- Performance Score — bias لمن وقت تشغيل أطول؟
- مرتجعات كتير تأثير على الدرجة؟
- موظف في فرعين — منفصل ولا مجمّع؟
- Variance موجب = sale مش متسجل / tip
- Variance سالب = سرقة / غلطة عد

STEP 3 — WRITE:

## 19. Cashier Performance Report
### 📍 نطاق
### 📊 الأرقام
- إيرادات بعد مرتجعات / عدد أوردرات / متوسط قيمة / payment mix
- Performance Score (المعادلة بأمثلة محسوبة)
- cancellationRate
### ❓ أسئلة شائعة (7+)
- Score يعتمد على حجم الورديات؟
- كاشير مرضيش بيعمل مرتجعات → درجة أعلى؟
- في فرعين — مرة ولا مرتين؟
- العلاقة بـ SalesByEmployee
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 20. Shift Details Report
### 📍 نطاق
### 📊 الأرقام
- إجمالي المبيعات (من Orders)
- توزيع طرق الدفع
- ExpectedBalance (المعادلة الكاملة)
- Variance (موجب/سالب)
- متوسط إيراد الشيفت
⚠️ ملاحظة: ده تقرير، مش CashRegisterSummary (الفرق في Prompt 10)
### ❓ أسئلة شائعة (6+)
- Variance موجب يعني؟
- يشوف الشيفتات المفتوحة؟ (لأ عادةً)
- ForceClose يظهر؟
- العلاقة بـ DailyReport
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 21. Sales By Employee Report
### 📍 نطاق
### 📊 الأرقام (إيرادات / نسبة % / متوسط أوردر / عدد أوردرات)
### ❓ أسئلة شائعة (5+)
- الفرق عن CashierPerformance
- موظف في فرعين
- مرتجعات باسم موظف تاني — تنزل من رصيد مين؟
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: اشرح Performance Score بمثال محسوب خطوة بخطوة.
```

---

# ════════════════════════════════
# PROMPT 10 — ملخص الخزنة + ملخص الوردية (خارج Report Services!)
# ════════════════════════════════

```
⚠️ تنبيه: التقريرين دول مش في Report Services — دول في CashRegisterService و ShiftService.

MISSION: اشرح "ملخص الخزنة" و"ملخص الوردية" بلغة بشرية كاملة.

STEP 1 — EXPLORE FIRST:

ملخص الخزنة (Cash Register Summary):
- backend/KasserPro.Application/Services/Interfaces/ICashRegisterService.cs → GetSummaryAsync(branchId, fromDate, toDate)
- backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs
- backend/KasserPro.Application/DTOs/CashRegister/CashRegisterSummaryDto.cs
- backend/KasserPro.API/Controllers/CashRegisterController.cs

افهم كل CashRegisterTransactionType:
Sale, Refund, Expense, Deposit, Withdrawal, TransferOut, TransferIn, SupplierPayment, Adjustment
+ IsTransferOut flag
+ ExpectedBalance vs ClosingBalance vs Difference

ملخص الوردية (Shift Summary):
- backend/KasserPro.Application/Services/Interfaces/IShiftService.cs → CloseAsync (ينتج summary)
- backend/KasserPro.Application/Services/Implementations/ShiftService.cs
- frontend/src/pages/ShiftPage.tsx (لو موجودة) أو ShiftSummary component

افهم:
- TotalSales من Orders (Order.Total)
- TotalCollected من Payments
- DeferredAmount = TotalSales - TotalCollected (الآجل)
- ExpectedBalance من CashRegisterService
- Difference = ClosingBalance - ExpectedBalance

تحقق: TenantId + BranchId، Egypt timezone، Permission.

STEP 2 — THINK:
- الفرق بين Cash Register Summary و Shift Summary — متى تستخدم كل واحد؟
- البيع الآجل بيأثر على الخزنة؟ (لأ — الكاش بس)
- Difference موجب → فلوس زيادة. سالب → ناقصة.
- الخزنة مشتركة بين الورديات ولا كل وردية ليها خزنة منفصلة؟

STEP 3 — WRITE:

## 22. Cash Register Summary
### 📍 نطاق التقرير (per-branch، نطاق تاريخ، Permission)
### 📊 الأرقام — كل نوع حركة وأثره:
- المبيعات الكاش: +
- المرتجعات الكاش: -
- المصروفات الكاش: -
- الإيداعات: +
- السحوبات: -
- TransferOut: -
- TransferIn: +
- SupplierPayment: -
- Adjustment: +/-
- ExpectedBalance vs ClosingBalance vs Difference
### ❓ أسئلة شائعة (7+)
- البيع الآجل بيأثر؟ (لأ)
- البيع كارت/instapay/transfer بيأثر؟ (لأ على الخزنة، آه على الإيرادات)
- Difference موجب — يعني إيه؟
- ExpectedBalance بيتحسب من فين؟
- العلاقة بـ Shift Summary
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 23. Shift Summary
### 📍 نطاق التقرير (وردية واحدة)
### 📊 الأرقام
- TotalSales (من Orders)
- TotalCollected (من Payments)
- DeferredAmount = الفرق
- ExpectedBalance (من CashRegisterService)
- Difference (Variance)
- توزيع طرق الدفع
### ❓ أسئلة شائعة (8+ — تقرير يومي مهم)
- المبيعات vs المحصّل vs الآجل — العلاقة؟
- ليه ExpectedBalance ≠ TotalCollected؟ (المصروفات والإيداعات)
- ForceClose يأثر إزاي؟
- Handover بين الكاشيرين؟
- العلاقة بـ Daily Report
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: وضّح بشكل خاص العلاقة بين المبيعات والمحصّل والآجل والخزنة.
```

---

# ════════════════════════════════
# PROMPT 11 — ملخصات الصفحات (مش تقارير)
# ════════════════════════════════

```
⚠️ تنبيه: ده مش تقارير — دي UI summaries على صفحات CRUD.
الناتج هيدخل في فصل منفصل في الدليل اسمه "ملخصات الصفحات".

MISSION: اشرح ملخصات صفحات OrdersPage + headers على CustomersPage / ExpensesPage / PurchaseInvoicesPage.

STEP 1 — EXPLORE FIRST:

OrdersPage:
- frontend/src/pages/OrdersPage.tsx
- افهم:
  * completedOrders / returnedOrders كيف تُحسب
  * displayedNetSales = total - refundAmount
  * displayedReturns
  * هل المرتجعات مزدوجة في العرض؟ (مهم)

Page header totals:
- frontend/src/pages/CustomersPage.tsx
- frontend/src/pages/ExpensesPage.tsx
- frontend/src/pages/PurchaseInvoicesPage.tsx
- لكل صفحة:
  * الإجمالي = backend (كل النتائج) ولا locally (الصفحة الحالية)؟
  * بيتفلتر بالـ filters المُطبقة؟

STEP 2 — THINK:
- صاحب المتجر ممكن يجمع Net Sales + Returns بالخطأ — إيه الجواب الصح؟
- إجماليات header — صاحب المتجر يفترض إنها كل النتائج، لو لأ ده mistake خطير

STEP 3 — WRITE:

## 24. ملخص صفحة الأوردرات — Orders Page Summary
### 📍 نطاق هذه الصفحة (مش تقرير، ملخص سريع للصفحة + الـ filters)
### 📊 الأرقام المعروضة
- عدد الأوردرات المكتملة
- عدد المرتجعات
- صافي المبيعات (كيف يُحسب وليه يختلف عن إجمالي المبيعات)
- بطاقة المرتجعات: مشمولة في Net Sales أم مستقلة؟
### ❓ أسئلة شائعة (5+)
### 🚫 مش هيظهر
### ✅ حالة الحساب

---

## 25. إجماليات الصفحات — Page Header Totals
(CustomersPage + ExpensesPage + PurchaseInvoicesPage)
### الوضع الحالي لكل صفحة
- backend (كل النتائج) ولا locally (الصفحة الحالية)؟
- اكتشف من الكود وأبلّغ بالحالة الفعلية
### ❓ أسئلة شائعة (4)

RULES: لا كود، أرقام أمثلة. وضّح صراحةً إن ده مش تقرير.
```

---

# ════════════════════════════════
# PROMPT 12 — ReportsDashboardPage (الصفحة الرئيسية)
# ════════════════════════════════

```
⚠️ ده برومبت قصير — لأن الصفحة index/launchpad وليست تقرير.

MISSION: اشرح صفحة لوحة التقارير الرئيسية.

STEP 1 — EXPLORE FIRST:
- frontend/src/pages/reports/ReportsDashboardPage.tsx
- frontend/src/pages/reports/InventoryReportsPage.tsx (لو في sub-launchpad للمخزون)

تحقق:
□ هل تعرض أرقام مجمعة (KPIs) ولا روابط فقط؟
□ لو KPIs — مصدر كل رقم (أي endpoint)؟
□ Permission filtering — التقارير اللي مش مسموح ليها بتختفي؟
□ Caching strategy

STEP 2 — WRITE:

## 26. لوحة التقارير الرئيسية — Reports Dashboard
### 📍 نطاق الصفحة
- launchpad للتقارير الـ 23
- KPIs محتملة (لو موجودة)
- Permission-aware navigation
### 📊 KPIs المعروضة (لو موجودة)
- لكل KPI: المصدر (endpoint) + التحديث (real-time/cache)
### ❓ أسئلة شائعة (4+)
- التقارير اللي مش مسموح ليها بتختفي؟
- KPIs بتستجيب لتغيير الفرع (X-Branch-Id)؟
- بتعمل refresh تلقائي؟
- Permission
### 🚫 مش هيظهر
### ✅ حالة الحساب

RULES: قصير ومباشر — ده مش تقرير، ده launchpad.
```

---

# ════════════════════════════════
# PROMPT 13 — الدمج النهائي
# ════════════════════════════════

```
لديك الآن ردود من 12 prompt تغطي كل تقارير KasserPro + ملخصات الصفحات + الـ Dashboard.

YOUR TASK:
اجمع كل الردود في ملف واحد اسمه: kasserpro-reports-guide.md

الهيكل المطلوب:

─────────────────────────────────────
# دليل التقارير — KasserPro
─────────────────────────────────────

## 📋 فهرس التقارير
[رابط لكل قسم بالترقيم 1→26]

─────────────────────────────────────
## ⚠️ تنبيهات مهمة — اقرأها أولاً
─────────────────────────────────────

(5-7 تنبيهات جوهرية، أمثلة):
1. **رصيد دين العميل**: tenant-wide — مش الفرع الحالي بس
2. **تكلفة المنتج في التقارير**: snapshot وقت البيع، مش السعر الحالي
3. **المخزون الافتتاحي في حركة المنتجات**: تقديري مش فعلي
4. **مصدر المخزون**: BranchInventory.Quantity (Product.StockQuantity اتشال)
5. **التحويلات في تقرير التحويلات**: المكتملة فقط (Status==Completed)
6. **CashRegisterSummary مش في ReportService**: في CashRegisterService
7. **العملة الواحدة**: single-currency (جنيه مصري)

─────────────────────────────────────
[محتوى كل تقرير من الردود السابقة]
─────────────────────────────────────

## 📊 جدول مقارنة التقارير

| # | التقرير | للفرع بس؟ | فلتر تاريخ؟ | يشمل المرتجعات؟ | يشمل الملغي؟ | المصدر الرئيسي | Permission |
|---|---------|:---------:|:-----------:|:---------------:|:-----------:|----------------|-----------|
[اكمل الجدول لكل التقارير من 1 إلى 26]

─────────────────────────────────────
## ❓ الأسئلة الأكثر شيوعاً عبر كل التقارير
─────────────────────────────────────
[10 أسئلة بإجاباتها الموحدة]

─────────────────────────────────────
## 🔄 العلاقة بين التقارير
─────────────────────────────────────
أمثلة:
- Daily ↔ Sales ↔ P&L: التتابع من اليومي للفترة للربحية
- Sales ↔ COGS: مصدر "تكلفة البضاعة"
- DailyReport ↔ ShiftSummary ↔ CashRegisterSummary: المستويات الثلاث للخزنة
- BranchInventory ↔ UnifiedInventory ↔ LowStock: المستويات الثلاث للمخزون
- ProductMovement ↔ ProfitableProducts ↔ SlowMoving: تحليل المنتجات بثلاث زوايا
- CustomerDebts ↔ TopCustomers: الديون tenant-wide تظهر في الاثنين

─────────────────────────────────────
## 🏗️ الخريطة المعمارية للتقارير
─────────────────────────────────────
شجرة الخدمات:
IReportService → Daily, Sales
IFinancialReportService → P&L, Expenses
IProductReportService → Movement, Profitable, SlowMoving, COGS
IInventoryReportService → BranchInv, UnifiedInv, TransferHistory, LowStock
ICustomerReportService → TopCust, CustDebts, CustActivity
ISupplierReportService → SuppPurchases, SuppDebts, SuppPerformance
IEmployeeReportService → CashierPerf, ShiftDetails, SalesByEmp
ICashRegisterService.GetSummaryAsync → CashRegSummary
IShiftService.CloseAsync → ShiftSummary

OUTPUT: ملف markdown واحد منظم.
```

---

# 📋 ملاحظات تشغيلية للـ agent

1. **الترتيب الموصى به**: شغّل البرومبتس بالترتيب 1→12، ثم 13 للدمج. ميتعداش 2 برومبت في session واحد لتقليل الـ context drift.
2. **التحقق من المسارات**: لو ملف مش موجود في المسار المحدد فوق، الـ codebase اتغيّر — ابحث بالـ method name (مثلاً `GetDailyReportAsync`) عشان تلاقي المكان الجديد.
3. **التحقق من الـ Permissions**: لكل endpoint، اقرأ `[HasPermission(Permission.X)]` فوقه واذكر الاسم في "نطاق التقرير".
4. **المثال الرقمي**: ثابت داخل الـ prompt الواحد، لكن كل prompt يستخدم مثاله. اللي أوصي به:
   - Daily/Sales/P&L: 10,000 / 500 / 570 / 14% / 6,000
   - COGS: 50,000 / 30,000 / 25,000 / 3 منتجات بدون تكلفة
   - Inventory: 100 وحدة × 50 ج.م تكلفة = 5,000 ج.م قيمة
5. **Egypt timezone**: لو لاقيت `DateTime.UtcNow` بدون conversion في فلتر التاريخ — bug محتمل، نبّه عليه.
6. **اختلاف الأرقام بين تقارير**: لو شفت TotalSales في Daily ≠ Sales لنفس الفترة — هاتلاقي السبب في:
   - الورديات المفتوحة
   - فلتر التاريخ (CompletedAt vs CreatedAt)
   - timezone offset
   - معاملة الملغي

