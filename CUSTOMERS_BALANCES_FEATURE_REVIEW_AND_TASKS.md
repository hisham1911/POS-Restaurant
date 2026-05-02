# مراجعة ميزة العملاء والأرصدة (Customers & Balances)

## الملخص التنفيذي
ميزة العملاء والأرصدة **ناضجة جدًا** — تغطي: إنشاء/تعديل/حذف عميل، بيع آجل (Credit Sale) مع تتبع مديونية per-branch، تسديد دين مع تكامل كامل مع CashRegister والورديات، نقاط ولاء (إضافة/استبدال)، حد ائتمان، تقارير عملاء (أفضل عملاء/ديون/نشاط). الفجوات الرئيسية: (1) `UpdateCustomerRequest` لا يستخدم `RowVersion` في الـ Backend رغم إرساله من Frontend، (2) `AddLoyaltyPoints` يستخدم `Authorize(Roles)` بدل `HasPermission`، (3) بعض UX improvements في عرض BranchBalance.

---

## ما الموجود حاليًا

### Backend Entities

| Entity | الوصف |
|--------|-------|
| `Customer` | Phone (unique per tenant), Name, Email, Address, Notes, IsActive, LoyaltyPoints, TotalOrders, TotalSpent, LastOrderAt, **TotalDue** (denormalized tenant-wide), **CreditLimit** (0=unlimited), **RowVersion** (optimistic concurrency) |
| `CustomerBranchBalance` | CustomerId, BranchId, TenantId, **AmountDue** (رصيد المديونية per-branch) |
| `DebtPayment` | TenantId, BranchId, CustomerId, Amount, PaymentMethod, ReferenceNumber, Notes, RecordedByUserId/Name (snapshot), ShiftId, **BalanceBefore/After** (audit trail) |

### Backend Services

| Service | الوصف |
|---------|-------|
| `CustomerService` | **CRUD**: Create (مع restore soft-deleted), Update (partial fields), Delete (soft-delete مع guard: لا يُحذف لو عنده دين أو طلبات مفتوحة). **Credit**: UpdateCreditBalance, ReduceCreditBalance, ValidateCreditLimit (per-branch). **Loyalty**: AddPoints, RedeemPoints. **Debt**: PayDebtAsync (transaction-safe, shift required, cash register integration, balance audit). **Stats**: UpdateOrderStats, DeductRefundStats (both participate in parent transaction). **Query**: GetAll (paginated + totals), GetById, GetByPhone, GetOrCreateByPhone, GetCustomersWithDebt, GetDebtPaymentHistory |
| `CustomerReportService` | **TopCustomers**: أفضل عملاء بالمبيعات (مع خصم المرتجعات) + عدد جدد. **Debts**: ديون العملاء per-branch مع Aging Analysis (0-30, 31-60, 61-90, 90+ يوم). **Activity**: تحليل عملاء جدد vs عائدون مع Retention/Churn rate |

### Backend Controllers

| Controller | Endpoints | Authorization |
|------------|-----------|---------------|
| `CustomersController` | GET all/by-id/by-phone/with-debt, POST create/get-or-create, PUT update, DELETE | `HasPermission(CustomersView/CustomersManage/PosSell)` |
| | POST `{id}/pay-debt`, GET `{id}/debt-history` | `HasPermission(CustomersManage)` |
| | POST `{id}/loyalty/add` | **`Authorize(Roles = "Admin,Manager")`** ⚠️ |
| | POST `{id}/loyalty/redeem` | **No permission check** ⚠️ |
| | DELETE `{id}` | `Authorize(Roles = "Admin")` + `HasPermission(CustomersManage)` |
| | POST `debt-payments/{paymentId}/print` | `HasPermission(CustomersView)` |
| `CustomerReportsController` | GET top-customers, debts, activity | `HasPermission(ReportsView)` |

### Backend DTOs

| DTO | الوصف |
|-----|-------|
| `CustomerDto` | Full customer info + TotalDue (tenant-wide) + CreditLimit |
| `CustomerSummaryDto` | Minimal for order attachment: Id, Phone, Name, LoyaltyPoints, TotalDue, CreditLimit |
| `CreateCustomerRequest` | Phone (required), Name, Email, Address, Notes |
| `UpdateCustomerRequest` | Name, Email, Address, Notes, IsActive, CreditLimit — **لا يحتوي RowVersion** ⚠️ |
| `GetOrCreateCustomerResult` | Customer + WasCreated flag |
| `PayDebtRequest` | Amount, PaymentMethod, ReferenceNumber, Notes |
| `PayDebtResponse` | PaymentId, AmountPaid, BalanceBefore/After, RemainingDebt, Message |
| `DebtPaymentDto` | Full payment audit: Id, CustomerId, Amount, Method, Reference, RecordedBy, ShiftId, BalanceBefore/After, CreatedAt |
| `TopCustomersReportDto` | Date range, totals + list of TopCustomerDto (with OutstandingBalance per branch) |
| `CustomerDebtsReportDto` | Totals + CustomerDebtDetailDto list + AgingAnalysis brackets |
| `CustomerActivityReportDto` | New/Returning/Inactive segments + Retention/Churn rates |

### Frontend

| المكوّن | الوصف |
|---------|-------|
| **`CustomersPage.tsx`** | قائمة العملاء مع بحث، ترقيم، summary cards (عدد/مبيعات/مستحق)، إجراءات (عرض/تعديل/حذف) |
| **`CustomerDetailsModal.tsx`** | مودال تفاصيل كامل: 3 تابات (سجل الطلبات/سجل الدفعات/تفاصيل). يعرض stats bar، credit info مع progress bar، loyalty points مع +/−، زر تسديد دين، طباعة إيصال دفع |
| **`CustomerFormModal.tsx`** | مودال إنشاء/تعديل عميل. عند التعديل يعرض CreditLimit ويرسل RowVersion |
| **`DebtPaymentModal.tsx`** | مودال تسديد دين متقدم: يتحقق من وردية مفتوحة، يعرض الدين الحالي، أزرار سريعة (الكل/النصف)، اختيار طريقة دفع، reference لغير النقدي، preview للمتبقي |
| **`LoyaltyPointsModal.tsx`** | مودال إضافة/استبدال نقاط ولاء |
| **`CustomerSearch.tsx`** | بحث عميل بالهاتف في POS مع debounce، نتيجة بحث/not found، زر إنشاء سريع |
| **`CustomerQuickCreateModal.tsx`** | إنشاء عميل سريع من POS (phone + name + optional fields) |
| **`customersApi.ts`** | RTK Query كامل: 13 endpoint مع cache tags (LIST, DEBT-LIST, per-id, per-debt) |
| **`customerReportsApi.ts`** | RTK Query: 3 endpoints (top-customers, debts, activity) |
| **`customer.types.ts`** | TypeScript types كاملة مع RowVersion support |
| **`customer-report.types.ts`** | TypeScript types للتقارير الثلاثة |
| **Report Pages** | `CustomerActivityReportPage.tsx`, `CustomerDebtsReportPage.tsx`, `TopCustomersReportPage.tsx` |

---

## تحليل الـ Workflows

### 1. Workflow إنشاء عميل
```
[CustomersPage or POS] → Create/GetOrCreate
    ├── Validate: Phone required + unique per tenant
    ├── If soft-deleted with same phone → Restore + overwrite info
    ├── If exists and active → Error (conflict)
    └── Create new → Save → Return CustomerDto
```

### 2. Workflow بيع آجل (Credit Sale)
```
[POS] → CompleteOrder with partial payment
    ├── Validate: Customer attached (required for credit)
    ├── Validate: HasPermission(CreditSale)
    ├── Validate: CreditLimit per-branch (ValidateCreditLimitAsync)
    │     └── BranchBalance.AmountDue + additionalAmount <= CreditLimit
    ├── Transaction starts
    ├── Add Payments (cash portion)
    ├── Update Order: AmountPaid, AmountDue, Status=Completed
    ├── UpdateCreditBalanceAsync(customerId, unpaidAmount)
    │     ├── Customer.TotalDue += unpaidAmount (tenant-wide)
    │     └── UpsertBranchBalanceAsync(customerId, +unpaidAmount)
    ├── UpdateOrderStatsAsync(customerId, orderTotal)
    └── Commit
```

### 3. Workflow تسديد دين
```
[CustomerDetailsModal → DebtPaymentModal] → PayDebt
    ├── Validate: Open shift required
    ├── Validate: Amount > 0 && Amount <= TotalDue
    ├── Validate: Reference for non-cash payments
    ├── Transaction starts
    ├── Read customer inside transaction (fresh data)
    ├── Create DebtPayment record (with BalanceBefore/After snapshots)
    ├── Customer.TotalDue -= Amount
    ├── UpsertBranchBalanceAsync(customerId, -Amount)
    ├── If Cash → CashRegisterService.RecordTransaction(Sale, Amount)
    ├── Commit
    └── Auto-print receipt (via SignalR to printer device)
```

### 4. Workflow مرتجع يؤثر على المديونية
```
[OrderService.RefundAsync]
    ├── If order had credit (AmountDue > 0)
    │     └── ReduceCreditBalanceAsync(customerId, refundedCreditPortion)
    │           ├── Customer.TotalDue -= amount (don't go below 0)
    │           └── UpsertBranchBalanceAsync(customerId, -amount)
    └── DeductRefundStatsAsync(customerId, refundAmount, points, isFullRefund)
```

### 5. Workflow حذف عميل
```
[CustomersPage] → Delete
    ├── Guard: Customer.TotalDue > 0 → Reject
    ├── Guard: Has open orders (not Completed/Cancelled) → Reject
    ├── Soft delete: IsActive=false, IsDeleted=true
    └── If same phone re-created later → Restore (workflow 1)
```

---

## المشاكل المفصلة

### Backend

| # | المشكلة | الشدة | الملف |
|---|---------|-------|-------|
| B-1 | `UpdateCustomerRequest` لا يحتوي على `RowVersion` — رغم أن Frontend يرسله والـ Entity عنده `[Timestamp]`. هذا يعني أن `UpdateAsync` لا يتحقق من Concurrency | **P1** | `CustomerDto.cs:64`, `CustomerService.cs:148` |
| B-2 | `AddLoyaltyPoints` يستخدم `[Authorize(Roles = "Admin,Manager")]` بدل `[HasPermission]` — inconsistent مع باقي الـ endpoints | **P2** | `CustomersController.cs:141` |
| B-3 | `RedeemLoyaltyPoints` **لا يحتوي على أي permission check** — أي مستخدم authenticated يقدر يستبدل نقاط أي عميل | **P1** | `CustomersController.cs:151` |
| B-4 | `PayDebt` في Controller يستخدم `int.Parse(User.FindFirst("userId"))` — نفس pattern غير الآمن الموجود في OrdersController | P2 | `CustomersController.cs:182` |
| B-5 | `CustomerService.GetAllAsync` يعرض العملاء النشطين فقط (`IsActive`)، لكن لا يوجد endpoint لعرض العملاء المحذوفين/غير النشطين للإدارة | P3 | `CustomerService.cs:33` |
| B-6 | `Customer.TotalDue` (tenant-wide) و `CustomerBranchBalance.AmountDue` (per-branch) يمكن أن يخرجا عن التزامن (no reconciliation mechanism) | P2 | Architecture |
| B-7 | `CustomerReportService` يستخدم `_context` مباشرة بدل `IUnitOfWork` — inconsistent مع باقي Services | P3 | `CustomerReportService.cs:16` |
| B-8 | `CustomerActivityReportDto.InactiveCustomers` دائمًا = 0 (hardcoded comment: "Would need more complex logic") | P3 | `CustomerReportService.cs:428` |
| B-9 | `PayDebtAsync` يسجل `CashRegisterTransactionType.Sale` للدفع النقدي — يجب أن يكون type مختلف مثل `DebtPayment` لتمييزه عن المبيعات العادية | P2 | `CustomerService.cs:478` |

### Frontend / UX

| # | المشكلة | الشدة | الملف |
|---|---------|-------|-------|
| F-1 | `CustomerDetailsModal` يعرض `totalDue` (tenant-wide) لكن لا يعرض `BranchBalance` (per-branch) — المستخدم لا يعرف كم الدين في فرعه | **P1** | `CustomerDetailsModal.tsx` |
| F-2 | `CustomerQuickCreateModal` يستخدم `react-hot-toast` بينما باقي المشروع يستخدم `sonner` — inconsistency | P3 | `CustomerQuickCreateModal.tsx:5` |
| F-3 | `CustomersPage.tsx` لا يعرض `CreditLimit` في الجدول الرئيسي — يظهر فقط في تفاصيل العميل | P3 | `CustomersPage.tsx` |
| F-4 | `DebtPaymentModal` يعرض `customer.totalDue` (tenant-wide) — إذا كان الحد per-branch فالمستخدم يرى رقم مختلف عن الدين الفعلي في فرعه | P2 | `DebtPaymentModal.tsx:170` |
| F-5 | `CustomerFormModal` لا يسمح بتعيين `CreditLimit` عند الإنشاء — فقط عند التعديل | P3 | `CustomerFormModal.tsx:248` |
| F-6 | `DebtPaymentModal` يعرض فقط 3 طرق دفع (Cash/Card/Fawry) — لا يتضمن `BankTransfer` الموجود في الـ Backend | P2 | `DebtPaymentModal.tsx:117` |
| F-7 | `CustomerDetailsModal` credit usage progress bar يقسم على 0 إذا `creditLimit = 0` | P2 | `CustomerDetailsModal.tsx:663` |

### Information Architecture

| # | المشكلة | الشدة |
|---|---------|-------|
| A-1 | `Customer.TotalDue` (tenant-wide) vs `CustomerBranchBalance.AmountDue` (per-branch): الـ CreditLimit يُتحقق منه per-branch لكن يُعرض tenant-wide في الـ UI — confusing للمستخدم | **P1** |
| A-2 | `DebtPayment` يسجل `BranchId` لكن لا يوجد تقرير دفعات per-branch — `GetDebtPaymentHistoryAsync` يجلب كل الدفعات بدون branch filter | P3 |
| A-3 | `GetOrCreateByPhoneAsync` لا يتحقق من `IsActive` — يمكن أن يُرجع عميل غير نشط | P2 |
| A-4 | Loyalty Points: لا يوجد auto-earn mechanism — النقاط تُضاف يدويًا فقط من `AddLoyaltyPoints` (رغم أن `UpdateOrderStatsAsync` يقبل `loyaltyPoints` parameter، لا يتم تمريره من `OrderService.CompleteAsync`) | P3 |

---

## خطة التاسكات المقترحة

### المرحلة 1: أمان وسلامة البيانات (P1)
- [ ] **B-1** إضافة `RowVersion` لـ `UpdateCustomerRequest` واستخدامه في `UpdateAsync` لـ optimistic concurrency.
- [ ] **B-3** إضافة `[HasPermission(Permission.CustomersManage)]` لـ `RedeemLoyaltyPoints` endpoint.
- [ ] **A-1/F-1** إضافة `BranchBalance` (per-branch AmountDue) في `CustomerDto` وعرضه في `CustomerDetailsModal` بجانب `TotalDue`.

### المرحلة 2: اتساق الصلاحيات (P2)
- [ ] **B-2** تحويل `AddLoyaltyPoints` من `Authorize(Roles)` لـ `HasPermission(Permission.CustomersManage)`.
- [ ] **B-4** تعديل `PayDebt` في Controller ليستخدم `GetUserId()` بدل `int.Parse(User.FindFirst("userId"))`.
- [ ] **B-9** إضافة `CashRegisterTransactionType.DebtPayment` (أو استخدام `Deposit` مع reference) لتمييز تسديد الدين عن البيع العادي.

### المرحلة 3: تحسين UX (P2)
- [ ] **F-4** عرض BranchBalance في `DebtPaymentModal` بدلاً من أو بجانب tenant-wide `totalDue`.
- [ ] **F-6** إضافة `BankTransfer` كطريقة دفع في `DebtPaymentModal` ليتوافق مع Backend.
- [ ] **F-7** إصلاح division by zero في credit usage progress bar عندما `creditLimit = 0`.
- [ ] **A-3** إصلاح `GetOrCreateByPhoneAsync` ليتحقق من `IsActive` ولا يُرجع عميل غير نشط.

### المرحلة 4: تحسينات تشغيلية (P3)
- [ ] **F-2** توحيد toast library — تغيير `CustomerQuickCreateModal` من `react-hot-toast` إلى `sonner`.
- [ ] **F-5** السماح بتعيين `CreditLimit` عند إنشاء العميل (ليس فقط التعديل).
- [ ] **B-5** إضافة filter `includeInactive` في `GetAllAsync` لعرض العملاء المحذوفين/غير النشطين.
- [ ] **B-7** توحيد `CustomerReportService` ليستخدم `IUnitOfWork` بدل `AppDbContext` مباشرة.
- [ ] **B-8** تنفيذ حساب `InactiveCustomers` في `CustomerActivityReport` (عملاء لم يطلبوا في الفترة رغم أنهم طلبوا سابقًا).
- [ ] **A-4** (مستقبلي) تفعيل auto-earn loyalty points عند إتمام طلب بناءً على إعدادات الـ Tenant.

---

## ملخص التكامل مع الميزات الأخرى

| الميزة | نقطة التكامل | الحالة |
|--------|-------------|--------|
| **POS/Orders** | `OrderService.CompleteAsync` → `UpdateOrderStatsAsync` + `UpdateCreditBalanceAsync` | ✅ يعمل |
| **POS/Orders** | `OrderService.RefundAsync` → `DeductRefundStatsAsync` + `ReduceCreditBalanceAsync` | ✅ يعمل |
| **POS/Orders** | `OrderService.CompleteAsync` → `ValidateCreditLimitAsync` (per-branch) | ✅ يعمل |
| **CashRegister** | `CustomerService.PayDebtAsync` → `RecordTransactionAsync(Sale)` if Cash | ✅ يعمل (لكن Type ينبغي أن يكون مختلف) |
| **Shifts** | `PayDebtAsync` يتطلب وردية مفتوحة + يربط `DebtPayment.ShiftId` | ✅ يعمل |
| **Printing** | `PayDebt` → auto-print receipt via SignalR (مع browser fallback) | ✅ يعمل |
| **Reports** | 3 تقارير عملاء كاملة (أفضل/ديون/نشاط) | ✅ يعمل |

---

## الخلاصة

ميزة العملاء والأرصدة **من أكثر الميزات اكتمالاً في المشروع**:
- ✅ **Dual-level debt tracking**: `Customer.TotalDue` (tenant-wide) + `CustomerBranchBalance.AmountDue` (per-branch)
- ✅ **Transaction safety**: كل العمليات المالية (debt payment, credit update, refund) داخل transactions
- ✅ **Full audit trail**: `DebtPayment` entity مع `BalanceBefore/After` snapshots
- ✅ **Soft delete with guards**: لا يُحذف عميل عنده دين أو طلبات مفتوحة
- ✅ **POS integration**: بحث عميل بالهاتف + إنشاء سريع + ربط بالطلب
- ✅ **Receipt printing**: طباعة إيصال تسديد دين تلقائية مع fallback للمتصفح
- ✅ **Reports**: 3 تقارير شاملة مع Aging Analysis

**الفجوة الرئيسية**: عدم استخدام `RowVersion` في `UpdateAsync` رغم وجوده في الـ Entity + مشكلة أمنية في `RedeemLoyaltyPoints` (بدون permission check).
