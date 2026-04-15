# State Machine Audit

Reviewed first:
- `.kiro/steering/architecture.md`
- `.kiro/skills/kasserpro-bestpractices/SKILL.md`

Scope notes:
- Audited all files under `backend/KasserPro.Domain/Entities/`, `backend/KasserPro.Domain/Enums/`, `backend/KasserPro.Application/Services/Implementations/`, and matching frontend status controls in `frontend/src/`.
- Controllers were reviewed; no controller in the audited scope adds stronger status validation than the service methods referenced below.

## Step 1: Entities With Status / State In Audited Code

| Entity | Status Source | Values Found In Code |
|--------|---------------|----------------------|
| Order | `backend/KasserPro.Domain/Enums/OrderStatus.cs:3-10` | `Draft`, `Pending`, `Completed`, `Cancelled`, `Refunded`, `PartiallyRefunded` |
| PurchaseInvoice | `backend/KasserPro.Domain/Enums/PurchaseInvoiceStatus.cs:6-41` | `Draft`, `Confirmed`, `Paid`, `PartiallyPaid`, `Cancelled`, `Returned`, `PartiallyReturned` |
| Expense | `backend/KasserPro.Domain/Enums/ExpenseStatus.cs:6-26` | `Draft`, `Approved`, `Paid`, `Rejected` |
| Shift | `backend/KasserPro.Domain/Entities/Shift.cs:16-18`, `backend/KasserPro.Domain/Entities/Shift.cs:67`, `backend/KasserPro.Domain/Entities/Shift.cs:93` | Open (`IsClosed = false`), Closed (`IsClosed = true`), ForceClosed, HandedOver |
| Customer | `backend/KasserPro.Domain/Entities/Customer.cs:33-36` | Active / Inactive |
| InventoryTransfer | `backend/KasserPro.Domain/Enums/InventoryTransferStatus.cs:6-26` | `Pending`, `Approved`, `Completed`, `Cancelled` |
| Shift-bound payment operations | `backend/KasserPro.Domain/Entities/Shift.cs:16-18` | Open Shift / No Open Shift |

### Entity: Order
**All Possible Statuses:** `Draft`, `Pending`, `Completed`, `Cancelled`, `Refunded`, `PartiallyRefunded`

**Allowed Transitions:**
| From Status | Operation | To Status | Allowed? |
|-------------|-----------|-----------|----------|
| Draft | `CompleteAsync` | Completed | Yes |
| Draft | `CancelAsync` | Cancelled | Yes |
| Pending | `CompleteAsync` | Completed | Yes |
| Pending | `CancelAsync` | Cancelled | Yes |
| Completed | `RefundAsync` | Refunded / PartiallyRefunded | Yes |
| Cancelled | Any audited transition | Any | No |
| Refunded | Any audited transition | Any | No |
| PartiallyRefunded | `RefundAsync` | Refunded / PartiallyRefunded | Yes in dedicated method |

**Violations Found:**

#### `OrderService.ValidateStateTransition` — Layer: Backend — File: `backend/KasserPro.Application/Services/Implementations/OrderService.cs` — Line 1137
- **Operation:** Generic transition validation for order status changes
- **Required Pre-condition:** entity.Status must be one of the known `OrderStatus` values, including `PartiallyRefunded`
- **Current Code Check:** MISSING
- **Frontend UI Check:** YES — `frontend/src/components/orders/OrderDetailsModal.tsx:34-39` explicitly treats `PartiallyRefunded` as a valid refundable state
- **Attack Scenario:** a partially refunded order reaches any future path that reuses the generic validator and is handled as an unknown state instead of a business-defined state
- **Financial Impact:** inconsistent lifecycle enforcement can produce unpredictable refund/cancel behavior and mismatched status-driven reports
- **Risk:** MEDIUM

### Entity: PurchaseInvoice
**All Possible Statuses:** `Draft`, `Confirmed`, `Paid`, `PartiallyPaid`, `Cancelled`, `Returned`, `PartiallyReturned`

**Allowed Transitions:**
| From Status | Operation | To Status | Allowed? |
|-------------|-----------|-----------|----------|
| Draft | `ConfirmAsync` | Confirmed | Yes |
| Draft | `DeleteAsync` | Deleted | Yes |
| Confirmed | `AddPaymentAsync` | PartiallyPaid / Paid | Should be Yes |
| Confirmed / PartiallyPaid | `CancelAsync` | Cancelled | Should be Yes |
| Paid | `DeleteAsync` | Deleted | Should be No |
| Paid | `CancelAsync` | Cancelled | Should be No |

**Violations Found:**

#### `PurchaseInvoiceService.DeleteAsync` — Layer: Backend — File: `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs` — Line 332
- **Operation:** Delete purchase invoice
- **Required Pre-condition:** entity.Status must be `Draft`
- **Current Code Check:** MISSING
- **Frontend UI Check:** YES — `frontend/src/pages/purchase-invoices/PurchaseInvoiceDetailsPage.tsx:266-269` shows delete only for `Draft`
- **Attack Scenario:** a paid or partially paid invoice can still be soft-deleted by direct API use if its status is not `Confirmed`
- **Financial Impact:** purchase liabilities and their supporting document trail can be hidden from operational screens
- **Risk:** HIGH

#### `PurchaseInvoiceService.CancelAsync` — Layer: Backend — File: `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs` — Line 466
- **Operation:** Cancel purchase invoice
- **Required Pre-condition:** entity.Status must be `Confirmed` or `PartiallyPaid`, and fully paid invoices should be blocked from cancellation without a stronger reversal flow
- **Current Code Check:** MISSING
- **Frontend UI Check:** YES — `frontend/src/pages/purchase-invoices/PurchaseInvoiceDetailsPage.tsx:271-275` only exposes cancel for `Confirmed` or `PartiallyPaid`
- **Attack Scenario:** a direct API call cancels a fully paid invoice and optionally rolls inventory back without reversing the payment trail first
- **Financial Impact:** supplier balances, stock value, and invoice state can diverge from each other
- **Risk:** CRITICAL

#### `PurchaseInvoiceService.AddPaymentAsync` — Layer: Backend — File: `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs` — Line 572
- **Operation:** Add payment to purchase invoice
- **Required Pre-condition:** entity.Status must transition from `Confirmed` to `PartiallyPaid` or `Paid` after updating `AmountPaid` / `AmountDue`
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — `frontend/src/pages/purchase-invoices/PurchaseInvoicesPage.tsx:167-170` and `frontend/src/pages/purchase-invoices/PurchaseInvoicesPage.tsx:81-82` assume `Paid` / `PartiallyPaid` are real workflow states
- **Attack Scenario:** repeated payments keep the invoice in `Confirmed`, so status-based filters and downstream actions treat settled invoices as still merely confirmed
- **Financial Impact:** AP workflow, paid invoice counts, and any status-based review screens become inaccurate
- **Risk:** HIGH

#### `PurchaseInvoiceService.DeletePaymentAsync` — Layer: Backend — File: `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs` — Line 595
- **Operation:** Delete purchase invoice payment
- **Required Pre-condition:** entity.Status must be in a reversible state and payment deletion must not be allowed for cancelled/settled invoices without an explicit reversal workflow
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — no audited frontend payment-delete path was found
- **Attack Scenario:** a payment is hard-deleted from a settled invoice by direct API use, reducing `AmountPaid` without a protected state workflow
- **Financial Impact:** vendor settlement history and balances can be rewritten after the fact
- **Risk:** CRITICAL

### Entity: Expense
**All Possible Statuses:** `Draft`, `Approved`, `Paid`, `Rejected`

**Allowed Transitions:**
| From Status | Operation | To Status | Allowed? |
|-------------|-----------|-----------|----------|
| Draft | `ApproveAsync` | Approved | Yes |
| Draft | `RejectAsync` | Rejected | Yes |
| Draft | `DeleteAsync` | Deleted | Yes |
| Approved | `PayAsync` | Paid | Yes |
| Paid / Rejected | `DeleteAsync` | Deleted | No |

**Violations Found:**

No status-transition violations were found in the audited expense lifecycle methods. `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:255-257`, `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:286-288`, `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:338-340`, and `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:388-390` consistently gate delete/approve/reject/pay by status.

### Entity: Shift
**All Possible Statuses:** Open, Closed, ForceClosed, HandedOver

**Allowed Transitions:**
| From Status | Operation | To Status | Allowed? |
|-------------|-----------|-----------|----------|
| Open | `CloseAsync` | Closed | Yes |
| Open | `ForceCloseAsync` | Closed + ForceClosed | Yes |
| Open | `HandoverAsync` | Open + HandedOver | Yes |
| Closed | `HandoverAsync` | HandedOver | No |

**Violations Found:**

#### `ShiftService.HandoverAsync` — Layer: Both — File: `backend/KasserPro.Application/Services/Implementations/ShiftService.cs` — Line 343
- **Operation:** Handover shift to another user
- **Required Pre-condition:** entity.Status must be Open, owned by the caller, and the target user must belong to the same tenant/branch context
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — `frontend/src/pages/shifts/ShiftPage.tsx:300-304` passes mock users, and `frontend/src/components/shifts/HandoverShiftModal.tsx:42-50` validates only target selection and non-negative balance
- **Attack Scenario:** a user hands over a shift they do not own, or hands it to a non-authoritative target from a stale/mock UI list
- **Financial Impact:** cashier accountability and shift ownership in audit trails can be reassigned incorrectly
- **Risk:** HIGH

### Entity: Customer
**All Possible Statuses:** Active, Inactive

**Allowed Transitions:**
| From Status | Operation | To Status | Allowed? |
|-------------|-----------|-----------|----------|
| Active | `UpdateAsync` (`IsActive=false`) | Inactive | Should be conditional |
| Active | `DeleteAsync` | Inactive + Deleted | Should be conditional |
| Inactive | `UpdateAsync` (`IsActive=true`) | Active | Yes |

**Violations Found:**

#### `CustomerService.UpdateAsync` — Layer: Backend — File: `backend/KasserPro.Application/Services/Implementations/CustomerService.cs` — Line 133
- **Operation:** Toggle customer active status through update
- **Required Pre-condition:** entity.Status must remain `Active` while the customer has outstanding debt or open orders
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — no audited frontend customer deactivation control was found
- **Attack Scenario:** direct API use deactivates a customer who still owes money or still has operational activity
- **Financial Impact:** debt collection and customer-credit workflows lose a valid active counterparty while balances remain outstanding
- **Risk:** HIGH

### Entity: InventoryTransfer
**All Possible Statuses:** `Pending`, `Approved`, `Completed`, `Cancelled`

**Allowed Transitions:**
| From Status | Operation | To Status | Allowed? |
|-------------|-----------|-----------|----------|
| Pending | `ApproveTransferAsync` | Approved | Yes |
| Pending / Approved | `CancelTransferAsync` | Cancelled | Yes |
| Approved | `ReceiveTransferAsync` | Completed | Yes |
| Completed / Cancelled | `CancelTransferAsync` | Cancelled | No |

**Violations Found:**

#### `InventoryTransferList / inventory.types` — Layer: Frontend — File: `frontend/src/types/inventory.types.ts` — Line 50
- **Operation:** Frontend status handling for transfer lifecycle
- **Required Pre-condition:** entity.Status contract must match backend terminal state `Completed`
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — `frontend/src/components/inventory/InventoryTransferList.tsx:102-107` and `frontend/src/components/inventory/InventoryTransferList.tsx:233-237` use `Received` instead of backend `Completed`, while backend sets `Completed` at `backend/KasserPro.Infrastructure/Services/InventoryService.cs:648`
- **Attack Scenario:** completed transfers fail frontend filtering/badging because the UI looks for a status value the API never returns
- **Financial Impact:** stock transfer completion becomes operationally ambiguous, which affects stock reconciliation between branches
- **Risk:** HIGH

### Entity: Shift-Bound Payment Operations
**All Possible Statuses:** Open Shift / No Open Shift

**Allowed Transitions:**
| From Status | Operation | To Status | Allowed? |
|-------------|-----------|-----------|----------|
| Open Shift | Complete sale | Financial records posted inside shift | Yes |
| Open Shift | Pay debt | Financial records posted inside shift | Yes |
| Open Shift | Pay expense | Financial records posted inside shift | Yes |
| Open Shift | Deposit / Withdrawal | Cash register movement posted inside shift | Yes |
| No Open Shift | Any of the above | Posted without shift context | Should be No |

**Violations Found:**

#### `OrderService.CompleteAsync` — Layer: Backend — File: `backend/KasserPro.Application/Services/Implementations/OrderService.cs` — Line 506
- **Operation:** Complete sale and record payments / stock / cash register
- **Required Pre-condition:** entity.Status must be Open Shift for the acting cashier before posting sale payment
- **Current Code Check:** MISSING
- **Frontend UI Check:** YES — `frontend/src/pages/pos/POSWorkspacePage.tsx:595-615` and `frontend/src/pages/pos/POSPage.tsx:186-209` block POS use when no active shift exists
- **Attack Scenario:** a direct API request completes an order after shift closure because the backend accepts completion without requiring an open shift
- **Financial Impact:** sales and cash entries can land outside any accountable shift, breaking drawer reconciliation and cashier audit trails
- **Risk:** CRITICAL

#### `CustomerService.PayDebtAsync` — Layer: Both — File: `backend/KasserPro.Application/Services/Implementations/CustomerService.cs` — Line 343
- **Operation:** Record customer debt payment
- **Required Pre-condition:** entity.Status must be Open Shift before receiving debt cash/card and linking it to cash activity
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — `frontend/src/components/customers/DebtPaymentModal.tsx:38-45` validates amount only
- **Attack Scenario:** a cashier records a debt payment while no shift is open, and the payment is saved with `ShiftId = null`
- **Financial Impact:** customer collections can exist without shift accountability, weakening cash reconciliation
- **Risk:** HIGH

#### `ExpenseService.PayAsync` — Layer: Both — File: `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs` — Line 388
- **Operation:** Mark expense paid and optionally record a cash transaction
- **Required Pre-condition:** entity.Status must be Open Shift before cash leaves the drawer
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — `frontend/src/pages/expenses/ExpenseDetailsPage.tsx:208-213` exposes pay based only on expense status
- **Attack Scenario:** an expense is paid after shift closure, and the cash movement is recorded without an active shift context
- **Financial Impact:** cash-out activity can be booked outside accountable shift windows
- **Risk:** HIGH

#### `CashRegisterService.CreateTransactionAsync` — Layer: Both — File: `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs` — Line 171
- **Operation:** Manual deposit / withdrawal
- **Required Pre-condition:** entity.Status must be Open Shift before allowing manual drawer movements
- **Current Code Check:** MISSING
- **Frontend UI Check:** NO — `frontend/src/pages/cash-register/CashRegisterDashboard.tsx:78-129` validates only amount and description
- **Attack Scenario:** a user performs deposit/withdrawal directly from the cash-register screen while no shift is open
- **Financial Impact:** manual drawer movements lose shift ownership and can no longer be reconciled cleanly per cashier
- **Risk:** HIGH

| Entity | Total Operations Checked | Backend Violations | Frontend Violations | CRITICAL Count |
|--------|--------------------------|-------------------|--------------------|----------------|
| Order | 6 | 1 | 0 | 0 |
| PurchaseInvoice | 6 | 4 | 1 | 2 |
| Expense | 5 | 0 | 0 | 0 |
| Shift | 4 | 1 | 1 | 0 |
| Customer | 2 | 1 | 0 | 0 |
| InventoryTransfer | 3 | 0 | 1 | 0 |
| Shift-Bound Payment Operations | 4 | 4 | 3 | 1 |

Total violations: 11

## Questions for Human Review

1. `OrderStatus.PartiallyRefunded`: هل يجب أن يدخل في جدول `ValidTransitions` كحالة رسمية كاملة، أم المطلوب أن يظل محصوراً داخل `RefundAsync` فقط؟ الكود الحالي منقسم بين `backend/KasserPro.Application/Services/Implementations/OrderService.cs:22-29` و `backend/KasserPro.Application/Services/Implementations/OrderService.cs:743-746`.
2. `PurchaseInvoice`: هل أي فاتورة عليها دفعات يمكن إلغاؤها أو حذفها إطلاقاً، أم يلزم flow عكسي منفصل؟ الكود الحالي يسمح بإلغاء واسع عند `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:466-480` ويحذف دفعات مباشرة عند `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:595-617`.
3. `PurchaseInvoice` state update: هل المعيار الرسمي هو الانتقال إلى `PartiallyPaid` و `Paid` بمجرد تغير `AmountDue`؟ الواجهة تفترض ذلك في `frontend/src/pages/purchase-invoices/PurchaseInvoicesPage.tsx:167-170` بينما الخدمة لا تحدث `Status` بعد الدفع.
4. `Customer` deactivation: هل وجود `TotalDue > 0` أو طلبات مفتوحة يجب أن يمنع تعطيل العميل تماماً، أم يكتفي بتحذير إداري؟ الكود الحالي لا يفرض أي guard في `backend/KasserPro.Application/Services/Implementations/CustomerService.cs:133-136`.
5. `Shift-bound payments`: هل فتح وردية شرط إلزامي لكل تحصيل دين، دفع مصروف، وإيداع/سحب خزنة، أم الشرط يقتصر على مبيعات الـ POS فقط؟ الكود الحالي يسمح بهذه العمليات بدون وردية مفتوحة في عدة خدمات.
6. `InventoryTransfer` contract: هل الحالة النهائية الرسمية اسمها `Completed` أم `Received`؟ الباك-إند يستخدم `Completed` في `backend/KasserPro.Domain/Enums/InventoryTransferStatus.cs:18-21` بينما بعض الواجهة تستخدم `Received` في `frontend/src/types/inventory.types.ts:50`.
