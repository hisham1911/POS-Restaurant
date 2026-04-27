# 📊 التقرير اليومي — شرح كامل بلغة بشرية

> **آخر تحديث:** 27 أبريل 2026  
> **الهدف:** فهم كل رقم في التقرير اليومي — من أين يأتي، كيف يُحسب، ومتى يظهر

---

## 1. التقرير اليومي — Daily Sales Report

### 📍 نطاق التقرير

| البُعد | القيمة | التفاصيل |
|--------|--------|----------|
| **المستوى** | Per-Branch | كل فرع له تقريره المستقل |
| **الفلتر الزمني** | Egypt Timezone (UTC+2) | التاريخ المُدخل يُحوّل لـ UTC ثم يُجلب كل الورديات المُغلقة في هذا اليوم بتوقيت مصر |
| **مصدر البيانات** | الورديات المُغلقة فقط | الوردية التي تُفتح يوم 15 وتُغلق يوم 16 → تظهر في تقرير يوم 16 |
| **الطلبات المشمولة** | Completed, PartiallyRefunded, Refunded | الطلبات الملغاة (Cancelled) تُحسب في العدد فقط، لا تدخل في المبيعات |
| **المرتجعات** | OrderType = Return | تُطرح من كل الأرقام المالية |
| **Permission المطلوب** | `ReportsView` | من `[HasPermission(Permission.ReportsView)]` |

**✅ التحقق من الأمان:**
- ✅ `TenantId` filter موجود في كل query
- ✅ `BranchId` filter موجود في كل query
- ✅ Egypt timezone conversion عبر `DateTimeHelper.ToUtcDayRange()`
- ✅ `BranchInventory.Quantity` (لا يُستخدم في التقرير اليومي — التقرير يعتمد على Orders فقط)

---

## 2. الأرقام — من أين تأتي؟

### 🧮 مثال رقمي ثابت (نستخدمه في كل الشرح)

**السيناريو:**
- مبيعات خام (Subtotal) = **10,000 ج.م**
- خصومات (Discount) = **500 ج.م**
- مرتجعات (Refunds) = **570 ج.م** (منها Subtotal = 500 ج.م، Tax = 70 ج.م)
- معدل الضريبة (Tax Rate) = **14%**

---

### 📊 الأرقام بالتفصيل

#### 1️⃣ **GrossSales** — إجمالي المبيعات الخام

**المعادلة:**
```
GrossSales = مجموع Subtotal لكل الطلبات المكتملة (بدون Return)
           - مجموع Subtotal لطلبات Return
```

**المثال:**
```
مبيعات خام = 10,000 ج.م
مرتجعات خام = 500 ج.م
GrossSales = 10,000 - 500 = 9,500 ج.م
```

**✅ يتأثر بـ:** المبيعات، المرتجعات  
**❌ لا يتأثر بـ:** الخصومات، الضرائب

---

#### 2️⃣ **TotalDiscount** — إجمالي الخصومات

**المعادلة:**
```
TotalDiscount = (مجموع Item Discounts + مجموع Order Discounts للمبيعات)
              - (مجموع Item Discounts + مجموع Order Discounts للمرتجعات)
```

**المثال:**
```
خصومات المبيعات = 500 ج.م
خصومات المرتجعات = 0 ج.م (لأن المرتجع كان بدون خصم)
TotalDiscount = 500 - 0 = 500 ج.م
```

**✅ يتأثر بـ:** الخصومات على المبيعات والمرتجعات  
**❌ لا يتأثر بـ:** الضرائب

**⚠️ ملاحظة:** الخصومات تُطرح من المرتجعات لتجنب المبالغة في عرض الخصومات.

---

#### 3️⃣ **NetSales** — صافي المبيعات (بعد الخصم، قبل الضريبة)

**المعادلة:**
```
NetSales = GrossSales - TotalDiscount
```

**المثال:**
```
NetSales = 9,500 - 500 = 9,000 ج.م
```

**✅ يتأثر بـ:** المبيعات، الخصومات، المرتجعات  
**❌ لا يتأثر بـ:** الضرائب

---

#### 4️⃣ **TotalTax** — إجمالي الضرائب

**المعادلة:**
```
TotalTax = مجموع TaxAmount للمبيعات
         - مجموع TaxAmount للمرتجعات
```

**المثال:**
```
ضريبة المبيعات = (10,000 - 500) × 14% = 1,330 ج.م
ضريبة المرتجعات = 70 ج.م
TotalTax = 1,330 - 70 = 1,260 ج.م
```

**✅ يتأثر بـ:** المبيعات، المرتجعات  
**❌ لا يتأثر بـ:** الخصومات (الضريبة تُحسب على NetSales)

---

#### 5️⃣ **TotalRefunds** — إجمالي المرتجعات

**المعادلة:**
```
TotalRefunds = Math.Abs(مجموع Total لطلبات Return)
```

**المثال:**
```
TotalRefunds = 570 ج.م (موجب للعرض)
```

**✅ يتأثر بـ:** طلبات Return فقط  
**❌ لا يتأثر بـ:** أي شيء آخر

**⚠️ ملاحظة:** المرتجعات تُخزن بقيم سالبة في قاعدة البيانات، لكن تُعرض موجبة في التقرير.

---

#### 6️⃣ **TotalSales / ActualTotalSales** — إجمالي المبيعات النهائي

**المعادلة:**
```
TotalSales = NetSales + TotalTax
           = (GrossSales - TotalDiscount) + TotalTax
```

**المثال:**
```
TotalSales = 9,000 + 1,260 = 10,260 ج.م
```

**✅ يتأثر بـ:** المبيعات، الخصومات، الضرائب، المرتجعات  
**❌ لا يتأثر بـ:** طريقة الدفع

**⚠️ ملاحظة:** `ActualTotalSales` هو alias لـ `TotalSales` — نفس القيمة.

---

#### 7️⃣ **TotalCollected** — المحصّل فعلياً

**المعادلة:**
```
TotalCollected = TotalCash + TotalCard + TotalFawry + TotalOther
```

**حيث:**
```
TotalCash = مجموع Payments (Cash) للمبيعات
          - Math.Abs(مجموع Payments (Cash) للمرتجعات)
          
TotalCard = مجموع Payments (Card) للمبيعات
          - Math.Abs(مجموع Payments (Card) للمرتجعات)
          
... وهكذا لكل طريقة دفع
```

**المثال:**
```
افترض:
- نقدي مبيعات = 7,000 ج.م
- نقدي مرتجعات = 570 ج.م
- بطاقة مبيعات = 3,260 ج.م
- بطاقة مرتجعات = 0 ج.م

TotalCash = 7,000 - 570 = 6,430 ج.م
TotalCard = 3,260 - 0 = 3,260 ج.م
TotalCollected = 6,430 + 3,260 = 9,690 ج.م
```

**✅ يتأثر بـ:** طرق الدفع الفعلية، المرتجعات  
**❌ لا يتأثر بـ:** البيع الآجل (لم يُدفع بعد)

**⚠️ حماية:** `Math.Max(0, ...)` تضمن عدم ظهور أرقام سالبة لو المرتجعات النقدية > المبيعات النقدية.

---

#### 8️⃣ **TotalDeferred** — الآجل / فرق التحصيل

**المعادلة:**
```
TotalDeferred = TotalSales - TotalCollected
```

**المثال:**
```
TotalDeferred = 10,260 - 9,690 = 570 ج.م
```

**✅ يتأثر بـ:** الفرق بين المبيعات والتحصيل  
**❌ لا يتأثر بـ:** أي شيء مباشر

**⚠️ ملاحظة:** لو الرقم موجب = في مبيعات آجلة. لو سالب = في دفع زيادة (نادر).

---

### 🔄 تسلسل الحساب (من البداية للنهاية)

```
1. GrossSales (مبيعات خام)           = 10,000 - 500 (مرتجع) = 9,500 ج.م
2. TotalDiscount (خصومات)            = 500 ج.م
3. NetSales (صافي قبل ضريبة)         = 9,500 - 500 = 9,000 ج.م
4. TotalTax (ضريبة)                  = 1,260 ج.م
5. TotalSales (إجمالي نهائي)         = 9,000 + 1,260 = 10,260 ج.م
6. TotalRefunds (مرتجعات)            = 570 ج.م (للعرض فقط)
7. TotalCollected (محصّل)            = 9,690 ج.م
8. TotalDeferred (آجل)               = 10,260 - 9,690 = 570 ج.م
```

**⚠️ تحذير:** المرتجعات تُطرح **مرة واحدة فقط** في الخطوة 1 (GrossSales). باقي الأرقام تُحسب من GrossSales الناتج.

---

## 3. ملخص الورديات — Shift Summaries

كل وردية في التقرير تعرض:

| الحقل | المصدر | الملاحظات |
|-------|--------|-----------|
| `UserName` | `Shift.User.Name` | اسم الكاشير |
| `OpenedAt` | `Shift.OpenedAt` | وقت فتح الوردية (UTC → يُعرض بتوقيت مصر في الفرونت) |
| `ClosedAt` | `Shift.ClosedAt` | وقت إغلاق الوردية |
| `TotalOrders` | `Shift.TotalOrders` | عدد الطلبات في الوردية |
| `TotalSales` | مجموع Orders.Total - مجموع Return Orders.Total | صافي المبيعات بعد المرتجعات |
| `TotalCash` | مجموع Payments (Cash) - مرتجعات Cash | النقدي المحصّل |
| `TotalCard` | مجموع Payments (Card) - مرتجعات Card | البطاقات المحصّلة |
| `TotalFawry` | مجموع Payments (Fawry) - مرتجعات Fawry | فوري المحصّل |
| `TotalOther` | مجموع Payments (BankTransfer) - مرتجعات BankTransfer | التحويلات المحصّلة |
| `TotalCollected` | TotalCash + TotalCard + TotalFawry + TotalOther | إجمالي المحصّل |
| `DeferredAmount` | TotalSales - TotalCollected | الآجل في الوردية |
| `IsForceClosed` | `Shift.IsForceClosed` | هل أُغلقت قسرياً؟ |
| `ForceCloseReason` | `Shift.ForceCloseReason` | سبب الإغلاق القسري |

**⚠️ ملاحظة:** الورديات تُحسب بنفس منطق التقرير الكلي — المرتجعات تُطرح من كل طريقة دفع.

---

## 4. أعلى المنتجات مبيعاً — Top Products

**المنطق:**
```
1. جمع كل OrderItems من الطلبات المكتملة (بدون Return)
2. جمع كل OrderItems من طلبات Return
3. حساب صافي الكمية = كمية المبيعات - كمية المرتجعات (per product)
4. حساب صافي المبيعات = مبيعات المنتج - مرتجعات المنتج
5. عرض أعلى 10 منتجات بصافي كمية > 0
```

**⚠️ تجاهل المنتجات المخصصة:** المنتجات التي `ProductId = null` (custom items) لا تظهر في القائمة.

**مثال:**
```
منتج A:
- مبيعات: 50 قطعة × 100 ج.م = 5,000 ج.م
- مرتجعات: 5 قطع × 100 ج.م = 500 ج.م
- صافي: 45 قطعة، 4,500 ج.م ← يظهر في التقرير

منتج B:
- مبيعات: 10 قطع × 50 ج.م = 500 ج.م
- مرتجعات: 10 قطع × 50 ج.م = 500 ج.م
- صافي: 0 قطعة، 0 ج.م ← لا يظهر في التقرير
```

---

## 5. المبيعات بالساعة — Hourly Sales

**المنطق:**
```
1. جمع الطلبات المكتملة (بدون Return)
2. تجميع حسب ساعة الإكمال (CompletedAt.Hour)
3. حساب عدد الطلبات ومجموع Total لكل ساعة
```

**⚠️ ملاحظة:** المرتجعات **لا تُطرح** من المبيعات بالساعة — هذا عرض تاريخي للنشاط فقط.

---

## 6. ❓ أسئلة شائعة

### 1️⃣ **ليه الكاش في التقرير أقل من المبيعات؟**

**الإجابة:** لأن في طرق دفع أخرى (بطاقة، فوري، تحويل) أو في مبيعات آجلة.

**مثال:**
```
TotalSales = 10,260 ج.م
TotalCash = 6,430 ج.م
TotalCard = 3,260 ج.م
TotalDeferred = 570 ج.م

6,430 + 3,260 + 570 = 10,260 ✅
```

---

### 2️⃣ **البيع الآجل بيتحسب فين؟**

**الإجابة:** في `TotalDeferred` فقط. **لا يظهر** في `TotalCollected`.

**مثال:**
```
طلب بـ 1,000 ج.م، دُفع 500 ج.م نقدي، والباقي آجل:
- TotalSales = 1,000 ج.م
- TotalCash = 500 ج.م
- TotalCollected = 500 ج.م
- TotalDeferred = 500 ج.م
```

---

### 3️⃣ **وردية مفتوحة → التقرير دقيق؟**

**الإجابة:** **لا**. التقرير اليومي يعرض **الورديات المُغلقة فقط**.

**مثال:**
```
وردية فُتحت الساعة 8 مساءً (15 يناير) ولم تُغلق بعد:
- تقرير 15 يناير: لا يشمل هذه الوردية
- تقرير 16 يناير: سيشملها بعد الإغلاق
```

**⚠️ تنبيه:** الفرونت يعرض banner أزرق يشرح هذا السلوك.

---

### 4️⃣ **مرتجع على أوردر قديم بيأثر فين؟**

**الإجابة:** في تقرير **يوم المرتجع** (يوم إغلاق الوردية التي فيها المرتجع).

**مثال:**
```
- طلب بـ 1,000 ج.م في 10 يناير
- مرتجع بـ 200 ج.م في 15 يناير

تقرير 10 يناير: TotalSales = 1,000 ج.م
تقرير 15 يناير: TotalRefunds = 200 ج.م، TotalSales يقل بـ 200 ج.م
```

---

### 5️⃣ **التقرير لكل الفروع ولا فرع واحد؟**

**الإجابة:** **فرع واحد** فقط (الفرع الحالي للمستخدم).

**الكود:**
```csharp
var branchId = _currentUser.BranchId;
var shifts = await _unitOfWork.Shifts.Query()
    .Where(s => s.TenantId == tenantId
             && s.BranchId == branchId  // ← هنا
             && s.IsClosed
             && s.ClosedAt >= utcFrom
             && s.ClosedAt < utcTo)
    .ToListAsync();
```

---

### 6️⃣ **Permission المطلوب؟**

**الإجابة:** `Permission.ReportsView` (من `[HasPermission(Permission.ReportsView)]`).

**الكود:**
```csharp
[HttpGet("daily")]
[HasPermission(Permission.ReportsView)]
public async Task<IActionResult> GetDailyReport([FromQuery] DateTime? date)
```

---

### 7️⃣ **لو المرتجعات النقدية > المبيعات النقدية؟**

**الإجابة:** في **حماية** `Math.Max(0, ...)` تضمن عدم ظهور أرقام سالبة.

**الكود:**
```csharp
var totalCash = Math.Round(Math.Max(0, 
    allPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount) 
    - refundedCash), 2);
```

**مثال:**
```
مبيعات نقدية = 500 ج.م
مرتجعات نقدية = 700 ج.م
TotalCash = Math.Max(0, 500 - 700) = 0 ج.م (مش -200)
```

---

### 8️⃣ **الضريبة بتتحسب إزاي؟**

**الإجابة:** **Tax Exclusive** (الضريبة تُضاف على السعر).

**الكود:**
```csharp
var totalTax = Math.Round(completedOrders.Sum(o => o.TaxAmount), 2);
```

**مثال:**
```
NetSales = 9,000 ج.م
TaxRate = 14%
TotalTax = 9,000 × 0.14 = 1,260 ج.م
TotalSales = 9,000 + 1,260 = 10,260 ج.م
```

---

## 7. 🚫 مش هيظهر في التقرير

| الحالة | السبب |
|--------|-------|
| **وردية مفتوحة** | التقرير يعرض الورديات المُغلقة فقط |
| **طلبات Draft** | لازم تكون Completed/PartiallyRefunded/Refunded |
| **طلبات Pending** | لازم تكون مكتملة |
| **طلبات Cancelled** | تُحسب في العدد فقط، لا تدخل في المبيعات |
| **منتجات مخصصة (ProductId = null)** | لا تظهر في Top Products |
| **مبيعات فروع أخرى** | كل فرع له تقريره المستقل |
| **مبيعات أيام أخرى** | التقرير يعرض يوم واحد فقط |

---

## 8. ✅ حالة الحساب — Verification Checklist

**✅ TenantId:** موجود في كل query  
**✅ BranchId:** موجود في كل query  
**✅ Egypt Timezone:** `DateTimeHelper.ToUtcDayRange()` يحوّل التاريخ لـ UTC بتوقيت مصر  
**✅ BranchInventory.Quantity:** لا يُستخدم في التقرير اليومي (التقرير يعتمد على Orders فقط)  
**✅ Permission:** `Permission.ReportsView` مطلوب  
**✅ Refund Handling:** المرتجعات تُطرح مرة واحدة فقط من GrossSales  
**✅ Math.Max(0, ...):** حماية من الأرقام السالبة في طرق الدفع  

---

## 9. 🔍 الكود المرجعي

### Backend
- **Service:** `backend/KasserPro.Application/Services/Implementations/ReportService.cs`
- **Controller:** `backend/KasserPro.API/Controllers/ReportsController.cs`
- **DTO:** `backend/KasserPro.Application/DTOs/Reports/ReportDto.cs`
- **Timezone Helper:** `backend/KasserPro.Application/Common/DateTimeHelper.cs`

### Frontend
- **Page:** `frontend/src/pages/reports/DailyReportPage.tsx`
- **API:** `frontend/src/api/reportsApi.ts`
- **Types:** `frontend/src/types/report.types.ts`

---

## 10. 📝 ملخص تنفيذي

**التقرير اليومي** يعرض:
1. **الورديات المُغلقة** في يوم معين (بتوقيت مصر)
2. **المبيعات الصافية** بعد طرح المرتجعات والخصومات
3. **التحصيل الفعلي** حسب طريقة الدفع (نقدي، بطاقة، فوري، تحويل)
4. **الآجل** (الفرق بين المبيعات والتحصيل)
5. **أعلى المنتجات** مبيعاً (بعد طرح المرتجعات)
6. **المبيعات بالساعة** (عرض تاريخي للنشاط)

**القاعدة الذهبية:** المرتجعات تُطرح **مرة واحدة** من GrossSales، وباقي الأرقام تُحسب من الناتج.

---

> **BUILD. MAINTAIN. IMPROVE.**  
> **الكود هو الحقيقة — الوثيقة تشرح الكود.**

**Document Owner:** Principal Software Architect  
**Last Updated:** April 27, 2026  
**Review Trigger:** عند أي تغيير في ReportService أو DailyReportDto
