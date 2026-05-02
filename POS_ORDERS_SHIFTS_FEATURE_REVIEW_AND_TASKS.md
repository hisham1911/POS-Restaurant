# مراجعة ميزة نقاط البيع والطلبات والورديات (POS, Orders & Shifts)

## الملخص التنفيذي
هذه المراجعة تغطي workflow كامل نقاط البيع: فتح وردية → إنشاء طلب → إضافة عناصر → إتمام الطلب (مع دفع/آجل) → إغلاق الوردية → تسوية الخزينة. البنية Backend قوية جدًا (Optimistic Concurrency, Snapshot Pattern, Transaction Safety, State Machine). الـ Frontend متكامل. الفجوات الرئيسية: (1) إغلاق الوردية لا يسجل تسوية تلقائية في CashRegister، (2) بعض State Transitions غير محمية بشكل كامل، (3) الـ Frontend لا يعرض Reconcile بشكل منفصل عن Close.

---

## ما الموجود حاليًا

### Backend Entities

| Entity | الوصف |
|--------|-------|
| `Order` | OrderNumber, Status (Draft→Pending→Completed→Refunded), OrderType (DineIn/Delivery/etc.), Branch/User/Customer Snapshots, Discount/Tax/ServiceCharge Snapshots, AmountPaid/Due/Change, RefundInfo, RowVersion (Optimistic Locking) |
| `OrderItem` | Product Snapshot (Name/Sku/Barcode), UnitPrice, UnitCost, Quantity, Discount, Tax Snapshot, IsCustomItem flag |
| `Payment` | Method (Cash/Card/Fawry/BankTransfer), Amount, Reference, OrderId |
| `Shift` | Opening/Closing/Expected/Difference, IsClosed, RowVersion, IsReconciled, LastActivityAt, ForceClose info, Handover info |
| `RefundLog` | OrderId, RefundedByUser, RefundAmount, RefundDate |

### Backend Services

| Service | الوصف |
|---------|-------|
| `OrderService` | CreateAsync (مع stock validation), AddItem/AddCustomItem/RemoveItem (Draft only), CompleteAsync (transaction-safe, cash register integration, stock decrement, credit sale validation), CancelAsync, RefundAsync (cash register integration, stock restore, customer debt adjustment), GetById/GetAll/GetTodayOrders/GetByCustomerId |
| `ShiftService` | OpenAsync (مع cash register opening transaction), CloseAsync (optimistic concurrency, pending orders guard, financial calculations), ForceCloseAsync, HandoverAsync, UpdateActivityAsync, GetCurrent/GetUserShifts/GetActiveShifts/GetShiftWarnings |
| `CashRegisterService` | RecordTransactionAsync يُستدعى من OrderService (Sale/Refund) و ShiftService (Opening) |

### Backend Controllers

| Controller | Endpoints |
|------------|-----------|
| `OrdersController` | GET/POST/PUT + Complete, Cancel, Refund + AddItem/AddCustomItem/RemoveItem + SignalR (DeviceHub) |
| `ShiftsController` | GET current/history/active/warnings + POST open/close/force-close/handover/update-activity |

### Frontend

| المكوّن | الوصف |
|---------|-------|
| `POSWorkspacePage.tsx` | واجهة POS الرئيسية — تابس: Products/CurrentOrder/Customer/OrderHistory. يتكامل مع useShift لعرض حالة الوردية |
| `POSPage.tsx` | صفحة POS (wrapper) |
| `ShiftPage.tsx` | عرض الوردية الحالية + فتح/إغلاق + سجل الورديات + تحذيرات الخمول |
| `useShift.ts` | Hook مركزي: currentShift, hasActiveShift, openShift, closeShift |
| `shiftsApi.ts` | RTK Query كامل: 8 endpoints |
| `shift.types.ts` | Types كاملة: Shift, ShiftOrder, Open/Close/ForceClose/Handover requests, ShiftWarning |
| `ActiveShiftsList.tsx` | قائمة الورديات النشطة (للإدارة) |
| `ForceCloseShiftModal.tsx` | مودال إغلاق بالقوة |
| `HandoverShiftModal.tsx` | مودال تسليم الوردية |
| `InactivityAlertModal.tsx` | تحذير خمول الوردية |
| `ShiftRecoveryModal.tsx` | استعادة الوردية المفقودة |
| `ShiftWarningBanner.tsx` | بنر تحذير الوردية |
| `useInactivityMonitor.ts` | Hook مراقبة الخمول |
| `usePOSMode.ts` | Hook وضع POS |

### غير موجود
- ❌ UI منفصل لـ Reconcile (التسوية جزء من Close فقط)
- ❌ صفحة تفاصيل طلب منفصلة (OrderDetailsPage) — التفاصيل داخل الـ POS tab فقط
- ❌ إدارة Refund منفصلة في قائمة الطلبات (Refund موجود في OrderService لكن لا يوجد UI عام له)
- ❌ تقرير يومي موحد (Daily Report موجود لكنه منفصل عن Shift)

---

## تحليل الـ Workflows

### 1. Workflow POS الكامل
```
[User opens POS]
    ↓
Check Current Shift
    ├── No shift → OpenShiftModal (OpeningBalance) → OpenAsync → CashRegister(Opening)
    └── Has shift → Continue
    ↓
Create Order (Draft) → Add Items (Product/Custom) → Calculate Totals
    ↓
Complete Order
    ├── Validate: Open Shift required
    ├── Validate: State Transition (Draft→Completed)
    ├── Validate: Payment amount (partial = credit sale permission + customer)
    ├── Validate: Credit limit (if partial)
    ├── Validate: Overpayment limit (2x total)
    ├── Validate: Reference for non-cash payments
    ├── Transaction starts
    ├── Add Payment entities
    ├── Update Order status → Completed
    ├── Decrement stock (BranchInventory)
    ├── Record CashRegisterTransaction(Sale) if cash
    ├── Commit transaction
    └── SignalR notify
    ↓
[End of day]
Close Shift
    ├── Validate: No pending orders
    ├── Validate: RowVersion (optimistic concurrency)
    ├── Calculate: ExpectedBalance = Opening + TotalCashSales - Refunds - Expenses
    ├── User inputs: ClosingBalance
    ├── Calculate: Difference = Closing - Expected
    ├── Save shift
    └── (No automatic Reconcile/Adjustment in CashRegister)
```

### 2. Workflow Refund
```
Order(Completed) → RefundAsync
    ├── Validate: State Transition (Completed→Refunded)
    ├── Validate: Refund items (if partial refund)
    ├── Transaction starts
    ├── Create RefundLog
    ├── Restore stock (BranchInventory)
    ├── Record CashRegisterTransaction(Refund) if cash was paid
    ├── Adjust customer debt (if credit sale)
    ├── Update Order status → Refunded
    └── Commit transaction
```

### 3. Workflow Shift Handover
```
Shift(Open) → HandoverAsync
    ├── Validate: Not closed, not already handed over
    ├── Validate: Target user exists, no open shift for target
    ├── Record: HandedOverFrom/To info + HandoverBalance
    ├── Transfer Shift.UserId to target user
    └── Commit
```

### 4. Workflow Force Close
```
Admin → ForceCloseAsync
    ├── Validate: Shift exists, not closed, not already force-closed
    ├── Validate: Reason required
    ├── Calculate financials (completed orders only)
    ├── Set: ClosingBalance = ActualBalance or Expected
    ├── Mark: IsForceClosed=true, ForceClosedByUser, ForceCloseReason
    └── Commit
```

---

## المشاكل المفصلة

### Backend

| # | المشكلة | الشدة | الملف |
|---|---------|-------|-------|
| B-1 | `Shift.CloseAsync` لا يسجل `CashRegisterTransaction` نوع `Adjustment` عند وجود فرق (Difference ≠ 0) | **P1** | `ShiftService.cs:155` |
| B-2 | `Shift.CloseAsync` لا يضبط `IsReconciled=true` أو يسجل Reconcile في CashRegister | **P1** | `ShiftService.cs` |
| B-3 | `OrderService.CreateAsync` يتحقق من Stock لكن `AddItemAsync` يتحقق مرة تانية — الـ validation مكرر لكن مقبول (soft then hard check) | P3 | `OrderService.cs` |
| B-4 | `OrderService.CompleteAsync` يحسب stock decrement بعد الـ SaveChangesAsync الأولى (lines 666) لكن قبل الـ final SaveChangesAsync — يمكن أن يحدث race condition في SQLite | P2 | `OrderService.cs:666-713` |
| B-5 | `OrderService.RefundAsync` (lines 1212+) يتحقق من `cashBalanceResponse.Success` لكن يستخدم `originalOrder.BranchId` بدل `_currentUser.BranchId` — صحيح لكن يجب التحقق من tenant isolation | P2 | `OrderService.cs:1197` |
| B-6 | `OrdersController` يستخدم `int.Parse` بدون validation في `Create` (line 81: `int.Parse(User.FindFirst("userId")?.Value ?? "0")`) | P2 | `OrdersController.cs:81` |
| B-7 | `ShiftsController.ForceClose` يستخدم `[Authorize(Roles = "Admin")]` + `[HasPermission(Permission.ShiftsManage)]` — mix of role and permission | P2 | `ShiftsController.cs:76` |

### Frontend / UX

| # | المشكلة | الشدة | الملف |
|---|---------|-------|-------|
| F-1 | `ShiftPage.tsx` لا يعرض Difference أو ExpectedBalance بشكل واضح عند الإغلاق | **P1** | `ShiftPage.tsx` |
| F-2 | لا يوجد UI لـ `Reconcile` منفصل — التسوية مخفية داخل إجراء `Close Shift` | **P1** | — |
| F-3 | `POSWorkspacePage.tsx` لا يعرض تحذيرًا واضحًا عند `NO_OPEN_SHIFT` — يعتمد على redirect لـ ShiftPage | P2 | `POSWorkspacePage.tsx` |
| F-4 | `useShift.ts` لا يتعامل مع `ShiftWarning` — التحذيرات موجودة في API لكن لا يتم عرضها في POS | P2 | `useShift.ts` |
| F-5 | `ShiftPage.tsx` لا يعرض `IsReconciled` status أو `ReconciledByUserName` | P2 | `ShiftPage.tsx` |
| F-6 | لا يوجد صفحة `OrdersPage.tsx` منفصلة لإدارة الطلبات (قائمة الطلبات) — كل الطلبات تُدار داخل POS tab فقط | P2 | — |
| F-7 | `InactivityAlertModal.tsx` موجود لكن لا يتم تفعيله تلقائيًا في `POSWorkspacePage` | P3 | `useInactivityMonitor.ts` |

### Information Architecture

| # | المشكلة | الشدة |
|---|---------|-------|
| A-1 | Shift.Close و CashRegister.Reconcile غير مرتبطين بشكل صريح — المفترض أن Close يستدعي Reconcile تلقائيًا | **P1** |
| A-2 | `Order` snapshot pattern ممتاز لكن `Payment` لا يحتوي على `BranchName` snapshot (ممكن للaudit) | P3 |
| A-3 | `ShiftDto` يحتوي على `TotalCash/TotalCard` لكن لا يحتوي على `TotalRefunds` أو `TotalExpenses` — المفترض أن يعرض كل المؤثرات | P2 |

---

## خطة التاسكات المقترحة

### المرحلة 1: ربط Shift.Close بتسوية CashRegister (P1)
- [ ] **B-1/B-2** تعديل `ShiftService.CloseAsync` ليسجل `CashRegisterTransaction` نوع `Adjustment` إذا كان `Difference ≠ 0`.
- [ ] **B-2** تعديل `ShiftService.CloseAsync` ليضبط `IsReconciled = true` ويسجل `ReconciledByUserId/Name/At`.
- [ ] **Verify** التأكد أن `CashRegisterSummary` يتضمن Adjustments من Shift.Close.

### المرحلة 2: تحسين UX للورديات (P1-P2)
- [ ] **F-1** إضافة عرض `ExpectedBalance` و `Difference` بشكل واضح في `ShiftPage.tsx` عند الإغلاق.
- [ ] **F-2** إنشاء مودال/خطوة منفصلة لـ Reconcile قبل Close (أو دمجهما في خطوة واحدة واضحة).
- [ ] **F-5** إضافة عرض `IsReconciled` status في قائمة الورديات.

### المرحلة 3: إدارة الطلبات (P2)
- [ ] **F-6** إنشاء صفحة `OrdersPage.tsx` منفصلة لعرض قائمة الطلبات (جميع الحالات) مع فلاتر (تاريخ/حالة/نوع/عميل).
- [ ] إضافة إجراء `Refund` في قائمة الطلبات (للطلبات المكتملة فقط).
- [ ] إضافة إجراء `Cancel` في قائمة الطلبات (للطلبات Draft/Pending فقط).

### المرحلة 4: أمان وصلاحيات (P2)
- [ ] **B-6** تعديل `OrdersController.Create` ليستخدم نفس pattern `GetUserId()` الموجود في `ShiftsController`.
- [ ] **B-7** إزالة `[Authorize(Roles = "Admin")]` من `ShiftsController.ForceClose` والاعتماد على `[HasPermission(Permission.ShiftsManage)]` فقط.

### المرحلة 5: تحسينات تشغيلية (P3)
- [ ] **F-4** ربط `ShiftWarning` بـ `POSWorkspacePage` لعرض بنر تحذيري عند الخمول الطويل.
- [ ] **A-3** إضافة `TotalRefunds` و `TotalExpenses` في `ShiftDto` وعرضهم في الـ UI.
- [ ] **B-4** مراجعة توقيت stock decrement في `CompleteAsync` — التأكد من أنه داخل transaction scope.

---

## الخلاصة

Workflow POS والورديات **مهندس بشكل ممتاز** Backend:
- ✅ Snapshot pattern (Product/Branch/User/Tax) — يمنع تغير البيانات التاريخية
- ✅ Optimistic concurrency (RowVersion) — يمنع race conditions
- ✅ Transaction safety — كل العمليات المالية داخل transactions
- ✅ State machine validation — لا يمكن الانتقال غير الصحيح
- ✅ Cash register integration — كل عملية نقدية مسجلة

الفجوة الرئيسية الوحيدة: **ربط إغلاق الوردية بتسوية CashRegister تلقائيًا**. حاليًا الـ user يُغلق الوردية لكن الفرق (Difference) لا يُسجل كـ Adjustment في الخزينة.
