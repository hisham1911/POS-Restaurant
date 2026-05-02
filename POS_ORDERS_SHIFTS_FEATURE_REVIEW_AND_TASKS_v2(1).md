# مراجعة ميزة نقاط البيع والطلبات والورديات — النسخة القابلة للتنفيذ

## الهدف
توثيق مراجعة منطقية لميزة POS والورديات مع حسم القرارات وتحويل المشاكل إلى مهام دقيقة قابلة للتنفيذ المباشر.

---

## الملخص التنفيذي
البيكند **مهندس بشكل ممتاز** — Snapshot Pattern, Optimistic Concurrency, Transaction Safety, State Machine. الفجوة الرئيسية: **إغلاق الوردية لا يسجل Adjustment تلقائيًا في الخزينة عند وجود فرق**.

---

## القرارات الأساسية — محسومة

### القرار 1: الفرق عند إغلاق الوردية
**القرار: يُسجَّل تلقائيًا كـ `Adjustment` في الخزينة.**

**الأثر على التنفيذ:**
- `ShiftService.CloseAsync` يستدعي `CashRegisterService.RecordTransactionAsync` بنوع `Adjustment` إذا `Difference ≠ 0`.
- `IsReconciled = true` يُضبط تلقائيًا عند الإغلاق.
- لا يحتاج المستخدم خطوة يدوية منفصلة للتسوية.

### القرار 2: صفحة الطلبات
**القرار: الصفحة موجودة بالفعل — لا تحتاج task.**

---

## البيكند — ما يعمل بشكل ممتاز ✓
- Snapshot pattern (Product/Branch/User/Tax) — يمنع تغير البيانات التاريخية
- Optimistic concurrency (RowVersion) — يمنع race conditions
- Transaction safety — كل العمليات المالية داخل transactions
- State machine validation — لا انتقال غير صحيح
- Cash register integration — كل عملية نقدية مسجلة

---

## التاسكات المطلوبة

---

## المرحلة 1 — ربط إغلاق الوردية بالخزينة (أولوية عالية)

### Task 1.1 — تسجيل Adjustment تلقائيًا عند إغلاق الوردية
**الملف:** `KasserPro.Infrastructure/Services/ShiftService.cs` → `CloseAsync`

**المطلوب:**
بعد حساب `Difference = ClosingBalance - ExpectedBalance`، أضف:

```csharp
if (difference != 0)
{
    await _cashRegisterService.RecordTransactionAsync(new RecordTransactionRequest
    {
        ShiftId = shift.Id,
        BranchId = shift.BranchId,
        Type = CashTransactionType.Adjustment,
        Amount = difference, // موجب = زيادة، سالب = عجز
        Notes = difference > 0
            ? $"فائض عند إغلاق الوردية: {difference:F2}"
            : $"عجز عند إغلاق الوردية: {Math.Abs(difference):F2}",
        UserId = currentUserId
    });
}
```

**معيار الاكتمال:** بعد إغلاق وردية بفرق، يظهر `Adjustment` في `CashRegisterTransactions` بالمبلغ الصحيح والإشارة الصحيحة.

---

### Task 1.2 — ضبط `IsReconciled` تلقائيًا عند الإغلاق
**الملف:** `ShiftService.cs` → `CloseAsync`

**المطلوب:**
```csharp
shift.IsReconciled = true;
shift.ReconciledByUserId = currentUserId;
shift.ReconciledByUserName = currentUserName;
shift.ReconciledAt = DateTime.UtcNow;
```
يُضاف مباشرة قبل `SaveChangesAsync` النهائية في `CloseAsync`.

**معيار الاكتمال:** كل وردية مغلقة تصبح `IsReconciled = true` تلقائيًا.

---

### Task 1.3 — التحقق من أن `CashRegisterSummary` يشمل Adjustments
**الملف:** `CashRegisterService` → الـ method المسؤولة عن الـ Summary

**المطلوب:**
- التأكد أن `CashTransactionType.Adjustment` مُضمَّن في حساب الرصيد النهائي.
- لو غير مُضمَّن: إضافته في الـ query.

**معيار الاكتمال:** رصيد الخزينة بعد إغلاق وردية بفرق يعكس الـ Adjustment.

---

## المرحلة 2 — تحسين UX الورديات (أولوية متوسطة)

### Task 2.1 — عرض `ExpectedBalance` و `Difference` بوضوح عند الإغلاق
**الملف:** `frontend/src/pages/shifts/ShiftPage.tsx`

**المطلوب:**
في modal أو خطوة إغلاق الوردية، إظهار:

| الحقل | الوصف |
|---|---|
| الرصيد الافتتاحي | `OpeningBalance` |
| إجمالي المبيعات النقدية | `TotalCash` |
| إجمالي المرتجعات | `TotalRefunds` |
| الرصيد المتوقع | `ExpectedBalance` (محسوب) |
| الرصيد الفعلي | حقل إدخال المستخدم `ClosingBalance` |
| الفرق | `Difference` — يظهر بلون أخضر إذا صفر، أحمر إذا سالب، برتقالي إذا موجب |

**معيار الاكتمال:** المستخدم يرى الفرق قبل تأكيد الإغلاق.

---

### Task 2.2 — إظهار `IsReconciled` في قائمة الورديات
**الملف:** `ShiftPage.tsx` → قائمة الورديات السابقة

**المطلوب:**
- إضافة عمود أو أيقونة "مسوّاة ✓" / "غير مسوّاة ✗" في قائمة الورديات.
- إذا `IsReconciled = true`: إظهار اسم المستخدم وتاريخ التسوية عند hover.

**معيار الاكتمال:** من قائمة الورديات يمكن معرفة أي وردية مسوّاة ومن سوّاها.

---

### Task 2.3 — إضافة `TotalRefunds` و `TotalExpenses` في `ShiftDto`
**الملفات:**
- `KasserPro.Application/DTOs/ShiftDto.cs`
- `ShiftService` → `GetByIdAsync` أو `CloseAsync`

**المطلوب:**
- إضافة `TotalRefunds` = مجموع المرتجعات النقدية في الوردية.
- إضافة `TotalExpenses` = مجموع المصروفات النقدية في الوردية (إن وُجدت).
- إظهارهما في Task 2.1 ضمن ملخص الإغلاق.

**معيار الاكتمال:** `ShiftDto` يحتوي على كل المؤثرات المالية ويمكن حساب `ExpectedBalance` منها كاملًا.

---

## المرحلة 3 — أمان وصلاحيات (أولوية متوسطة)

### Task 3.1 — إصلاح `OrdersController.Create`
**الملف:** `KasserPro.API/Controllers/OrdersController.cs` → line ~81

**المطلوب:**
استبدال:
```csharp
int.Parse(User.FindFirst("userId")?.Value ?? "0")
```
بنفس pattern `GetUserId()` الموجود في `ShiftsController`:
```csharp
var userId = GetUserId(); // أو الـ helper method المعتمدة في المشروع
if (userId == 0) return Unauthorized();
```

**معيار الاكتمال:** لا يوجد `int.Parse` مباشر على Claims في `OrdersController`.

---

### Task 3.2 — توحيد صلاحيات `ForceClose`
**الملف:** `KasserPro.API/Controllers/ShiftsController.cs` → `ForceClose` endpoint

**المطلوب:**
إزالة `[Authorize(Roles = "Admin")]` والاعتماد فقط على:
```csharp
[HasPermission(Permission.ShiftsManage)]
```

**معيار الاكتمال:** `ForceClose` لا يعتمد على Role مباشرة — يعتمد على Permission فقط.

---

## المرحلة 4 — تحسينات تشغيلية (أولوية منخفضة)

### Task 4.1 — ربط `ShiftWarning` بـ POS
**الملف:** `frontend/src/pages/pos/POSWorkspacePage.tsx` + `useShift.ts`

**المطلوب:**
- جلب `ShiftWarnings` من الـ API.
- إذا وُجد تحذير خمول: إظهار `ShiftWarningBanner` داخل `POSWorkspacePage`.
- الـ Banner موجود بالفعل (`ShiftWarningBanner.tsx`) — المطلوب فقط ربطه.

**معيار الاكتمال:** الكاشير يرى تحذير الخمول داخل شاشة الـ POS بدون الحاجة للذهاب لصفحة الوردية.

---

### Task 4.2 — مراجعة توقيت `stock decrement` في `CompleteAsync`
**الملف:** `OrderService.cs` → `CompleteAsync` lines ~666-713

**المطلوب:**
- التحقق أن `BatchDecrementStockAsync` يُستدعى **داخل** نفس الـ transaction scope قبل الـ `CommitAsync`.
- إذا كان خارج الـ transaction: نقله للداخل.

**معيار الاكتمال:** لو فشل `RecordCashRegisterTransaction`، يُعاد `BranchInventory` لحالته الأصلية تلقائيًا (Rollback).

---

## ترتيب التنفيذ للأيجنت

```
1. Task 1.1 — Adjustment في الخزينة عند الإغلاق  ← الأهم
2. Task 1.2 — IsReconciled = true تلقائيًا
3. Task 1.3 — التحقق من CashRegisterSummary
4. Task 2.3 — إضافة TotalRefunds/TotalExpenses في ShiftDto
5. Task 2.1 — عرض ملخص مالي واضح عند الإغلاق
6. Task 2.2 — إظهار IsReconciled في قائمة الورديات
7. Task 3.1 — إصلاح int.Parse في OrdersController
8. Task 3.2 — توحيد صلاحيات ForceClose
9. Task 4.2 — مراجعة توقيت stock decrement
10. Task 4.1 — ربط ShiftWarning بـ POS
```

---

## المرحلة 5 — صفحة إدارة الورديات المفصلة (أولوية عالية)

### Task 5.1 — جدول الورديات في صفحة الإدارة
**الملف:** `frontend/src/pages/shifts/ShiftPage.tsx` أو صفحة مستقلة `ShiftsManagementPage.tsx`

**المطلوب — جدول يعرض لكل وردية:**

| العمود | المصدر |
|---|---|
| اسم الكاشير | `Shift.UserName` |
| الفرع | `Shift.BranchName` |
| وقت الفتح | `Shift.OpenedAt` |
| وقت الإغلاق | `Shift.ClosedAt` (أو "مفتوحة" إن لم تُغلق) |
| الرصيد الافتتاحي | `Shift.OpeningBalance` |
| الرصيد الختامي | `Shift.ClosingBalance` |
| الفرق | `Shift.Difference` — ملوّن (أخضر/أحمر/رمادي) |
| الحالة | مفتوحة / مغلقة / مُغلقة بالقوة / مسوّاة |
| زر | "عرض التفاصيل" |

فلاتر:
- حسب التاريخ (من/إلى)
- حسب الكاشير
- حسب الفرع
- حسب الحالة

**معيار الاكتمال:** المدير يرى كل الورديات في جدول واحد مع إمكانية الفلترة.

---

### Task 5.2 — Backend: إضافة بيانات مالية مفصلة في `ShiftDto`
**الملف:** `KasserPro.Application/DTOs/ShiftDto.cs` + `ShiftService`

**المطلوب — إضافة هذه الحقول في `ShiftDto`:**

```csharp
// المبيعات حسب طريقة الدفع
public decimal TotalCashSales { get; set; }
public decimal TotalCardSales { get; set; }
public decimal TotalFawrySales { get; set; }
public decimal TotalBankTransferSales { get; set; }
public decimal TotalVodafoneCashSales { get; set; }

// المرتجعات
public decimal TotalRefunds { get; set; }
public int RefundsCount { get; set; }

// الطلبات
public int TotalOrdersCount { get; set; }
public int CompletedOrdersCount { get; set; }
public int CancelledOrdersCount { get; set; }
public int RefundedOrdersCount { get; set; }

// الآجل
public decimal TotalCreditSales { get; set; }
public int CreditOrdersCount { get; set; }

// الصافي النقدي
public decimal NetCash { get; set; } // OpeningBalance + TotalCashSales - TotalRefunds(Cash)
```

**طريقة الحساب:**
- جلب كل `Payments` المرتبطة بطلبات الوردية وتجميعها حسب `PaymentMethod`.
- جلب كل `Orders` في نطاق زمن الوردية للفرع والمستخدم.

**معيار الاكتمال:** `GET /api/shifts/{id}` يُرجع كل هذه الحقول بشكل صحيح.

---

### Task 5.3 — Modal/Drawer تفاصيل الوردية الكاملة
**الملف:** `frontend/src/components/shifts/ShiftDetailsDrawer.tsx` (جديد)

**المطلوب — الـ Drawer يحتوي على 3 أقسام:**

#### القسم 1: معلومات الوردية
```
الكاشير: [اسم المستخدم]          الفرع: [اسم الفرع]
فُتحت:  [التاريخ والوقت]         أُغلقت: [التاريخ والوقت]
الحالة: [badge ملوّن]            مسوّاة: [✓ أو ✗ + اسم من سوّى]
```

#### القسم 2: الملخص المالي
```
┌─────────────────────────────────────────┐
│ الرصيد الافتتاحي          XXX.XX جنيه  │
│ ─────────────────────────────────────── │
│ مبيعات نقدي               XXX.XX جنيه  │
│ مبيعات فيزا               XXX.XX جنيه  │
│ مبيعات فودافون كاش        XXX.XX جنيه  │
│ مبيعات فوري               XXX.XX جنيه  │
│ مبيعات تحويل بنكي         XXX.XX جنيه  │
│ مبيعات آجل                XXX.XX جنيه  │
│ ─────────────────────────────────────── │
│ إجمالي المبيعات           XXX.XX جنيه  │
│ المرتجعات (X طلب)        (XXX.XX جنيه) │
│ ─────────────────────────────────────── │
│ الرصيد المتوقع            XXX.XX جنيه  │
│ الرصيد الفعلي             XXX.XX جنيه  │
│ الفرق                    [+/-XXX.XX]   │
└─────────────────────────────────────────┘
```

#### القسم 3: جدول الطلبات
جدول بكل طلبات الوردية:

| رقم الطلب | الوقت | العميل | المبلغ | طريقة الدفع | الحالة |
|---|---|---|---|---|---|
| #0001 | 10:32 | نقدي | 150.00 | نقدي | مكتمل |
| #0002 | 11:15 | محمد علي | 320.00 | آجل | مكتمل |
| #0003 | 12:00 | — | 75.00 | فيزا | مُرتجع |

- الضغط على أي طلب يفتح تفاصيله.
- فلترة الجدول حسب الحالة (مكتمل/مرتجع/ملغي).

**معيار الاكتمال:** المدير يفتح تفاصيل أي وردية ويرى كل الأرقام والطلبات في مكان واحد.

---

### Task 5.4 — Backend: Endpoint لطلبات الوردية
**الملف:** `ShiftsController` + `ShiftService`

**المطلوب:**
```
GET /api/shifts/{id}/orders
```
يُرجع كل الطلبات في نطاق زمن الوردية للفرع والمستخدم:
- `OrderId`, `OrderNumber`, `CreatedAt`, `CustomerName`, `TotalAmount`, `PaymentMethod`, `Status`
- مرتبة بالتاريخ تصاعديًا

**معيار الاكتمال:** الـ Endpoint يُرجع فقط طلبات الوردية المحددة بـ `TenantId + BranchId`.

---

### Task 5.5 — إضافة صلاحية عرض تفاصيل الورديات
**المطلوب:**
- عرض جدول الورديات: `ShiftsView` أو `ReportsView`
- عرض تفاصيل وردية: `ShiftsView`
- عرض طلبات الوردية: `ShiftsView` + `OrdersView`
- التأكد من تطبيق هذه الصلاحيات في الـ Controller والـ Frontend.

**معيار الاكتمال:** مستخدم بدون `ShiftsView` لا يرى الصفحة ولا الـ Endpoints.

---

## ترتيب التنفيذ للأيجنت — المُحدَّث

```
1. Task 1.1 — Adjustment في الخزينة عند الإغلاق  ← الأهم
2. Task 1.2 — IsReconciled = true تلقائيًا
3. Task 1.3 — التحقق من CashRegisterSummary
4. Task 5.2 — إضافة بيانات مالية مفصلة في ShiftDto  ← أساس صفحة التفاصيل
5. Task 5.4 — Endpoint طلبات الوردية
6. Task 2.3 — إضافة TotalRefunds/TotalExpenses في ShiftDto
7. Task 5.1 — جدول الورديات في صفحة الإدارة
8. Task 5.3 — Drawer تفاصيل الوردية الكاملة
9. Task 5.5 — صلاحيات عرض الورديات
10. Task 2.1 — عرض ملخص مالي واضح عند الإغلاق
11. Task 2.2 — إظهار IsReconciled في قائمة الورديات
12. Task 3.1 — إصلاح int.Parse في OrdersController
13. Task 3.2 — توحيد صلاحيات ForceClose
14. Task 4.2 — مراجعة توقيت stock decrement
15. Task 4.1 — ربط ShiftWarning بـ POS
```

---

## قيود معروفة — لا تحتاج action الآن
- **B-4 (race condition):** SQLite يعمل بـ WAL mode — الخطر منخفض جدًا. راقب فقط.
- **A-2 (Payment snapshot):** `Payment` لا يحتوي `BranchName` — مقبول للـ audit الحالي.

---

## الحالة الحالية
**البيكند ممتاز. المشكلة الوحيدة الحقيقية هي Task 1.1 — ابدأ بها.**
