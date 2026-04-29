# مراجعة ميزة المصروفات والخزينة

## الملخص
Backend قوي (Audit + Transactions). Frontend متكامل نسبيًا. الفجوات: (1) فواتير الشراء لا تسجل في الخزينة، (2) Admin Role بدل Permission، (3) غياب UI لـ Transfer/Reconcile/Categories.

## ما الموجود
- Entities: Expense, ExpenseCategory, ExpenseAttachment, CashRegisterTransaction (9 أنواع).
- Services: ExpenseService (CRUD + Workflow), CashRegisterService (Balance + Transactions + Transfer + Reconcile + Summary).
- Controllers: ExpensesController, CashRegisterController.
- Frontend: ExpensesPage, ExpenseDetailsPage, ExpenseFormPage, CashRegisterDashboard, CashRegisterTransactionsPage. RTK Query كامل.
- Navigation: `/expenses` (ExpensesView), `/cash-register` (CashRegisterView).

## غير موجود
- UI لـ Transfer/Reconcile/ExpenseCategories.
- PurchaseInvoiceService لا يسجل SupplierPayment في CashRegister.
- Dashboard مالي موحد.

## Workflows
- Expense: Draft → Approved → Paid (Cash يسجل في CashRegister). Reject متاح.
- CashRegister: Opening (Shift) → Sale/Refund (Order) → Deposit/Withdrawal (Manual) → Expense (Expense.Pay) → Transfer (Manual) → Reconcile (Shift.Close).

## المشاكل
| # | المشكلة | الشدة | الملف |
|---|---------|-------|-------|
| B-1 | Approve/Reject/Pay تستخدم `[Authorize(Roles="Admin")]` بدل `HasPermission` | P1 | ExpensesController.cs |
| B-2 | Reconcile/Transfer تستخدم `[Authorize(Roles="Admin")]` | P1 | CashRegisterController.cs |
| B-3 | PurchaseInvoiceService لا يسجل دفعات الموردين في CashRegister | P1 | PurchaseInvoiceService.cs |
| F-1 | لا UI لـ Cash Transfer بين الفروع | P1 | — |
| F-2 | لا UI لـ Reconcile | P1 | — |
| F-3 | لا صفحة فئات المصروفات | P2 | — |
| F-4 | shiftId في TransactionsPage input number يدوي | P2 | CashRegisterTransactionsPage.tsx |
| A-1 | لا Dashboard مالي موحد | P2 | — |

## التاسكات
### المرحلة 1: أمان (P1)
- تغيير Admin Role → HasPermission في ExpensesController و CashRegisterController.
- إضافة Permissions جديدة (ExpensesApprove, CashRegisterTransfer, CashRegisterReconcile) في enum + UI guards.

### المرحلة 2: تكامل فواتير الشراء (P1)
- تعديل PurchaseInvoiceService لاستدعاء RecordTransactionAsync(SupplierPayment) عند الدفع النقدي.

### المرحلة 3: UI مفقود (P1-P2)
- صفحة/مودال Cash Transfer بين الفروع.
- مودال Reconcile في CashRegisterDashboard.
- صفحة ExpenseCategories (CRUD + Icon/Color picker).

### المرحلة 4: UX (P2-P3)
- shiftId dropdown بدل input number.
- عرض TransferReferenceId في تفاصيل المعاملة.
- link "عرض الكل" في Dashboard بحسب نوع المعاملة.

### المرحلة 5: Dashboard مالي (P2)
- صفحة Financial Overview: رصيد خزينة + مصروفات + مبيعات نقدية + Net Cash Flow + رسم بياني.

---
*ملف مراجعة أولي. ينتظر التحقق والتوسع.*
