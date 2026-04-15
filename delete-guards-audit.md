# Delete Guards Audit

Reviewed first:
- `.kiro/steering/architecture.md`
- `.kiro/skills/kasserpro-bestpractices/SKILL.md`

Scope notes:
- Audited all files under `backend/KasserPro.Application/Services/Implementations/`, `backend/KasserPro.API/Controllers/`, and `backend/KasserPro.Domain/Entities/`.
- Audited matching frontend delete/deactivate controls under `frontend/src/`.
- Controllers were reviewed; for the operations below they delegate to the listed services and do not add stronger pre-delete guards.

### [CategoryService / CategoriesPage] → [DeleteAsync / handleDelete]
- **File:** `backend/KasserPro.Application/Services/Implementations/CategoryService.cs`
- **Layer:** Both
- **Entity:** Category
- **Operation:** SOFT-DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/CategoryService.cs:133-136` checks the category exists in the current tenant.
  - `backend/KasserPro.Application/Services/Implementations/CategoryService.cs:139-142` blocks deletion when any non-deleted product still references the category.
- **Missing Guards:**
  - NONE
- **Frontend Gap (إن وُجد):** `frontend/src/pages/categories/CategoriesPage.tsx:86-95` uses a generic confirm only and does not preview impacted products before deletion.
- **Risk Level:** LOW
- **Risk Reason:** الباك-إند يفرض guard أساسي صحيح، لكن الـ UI لا يوضح للمستخدم أثر الحذف على التصنيف والمنتجات المرتبطة.

### [CategoryService] → [UpdateAsync]
- **File:** `backend/KasserPro.Application/Services/Implementations/CategoryService.cs`
- **Layer:** Backend
- **Entity:** Category
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/CategoryService.cs:105-108` checks the category exists in the current tenant.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Product.cs:65-76`: no dependency check before setting `category.IsActive = request.IsActive` at `backend/KasserPro.Application/Services/Implementations/CategoryService.cs:110-115`, so a category can be deactivated while active products still rely on it.
- **Frontend Gap (إن وُجد):** No audited frontend control in `frontend/src/` was found for category deactivation.
- **Risk Level:** MEDIUM
- **Risk Reason:** تعطيل تصنيف ما زالت منتجات فعالة تستخدمه يخلق تضارباً تشغيلياً بين الكتالوج والتصنيفات المعروضة.

### [SupplierService / SuppliersPage] → [DeleteAsync / handleDeleteSupplier]
- **File:** `backend/KasserPro.Application/Services/Implementations/SupplierService.cs`
- **Layer:** Both
- **Entity:** Supplier
- **Operation:** SOFT-DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/SupplierService.cs:159-163` checks the supplier exists in the current tenant and is not already deleted.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Supplier.cs:23-28`: no check for non-zero `TotalDue` / `TotalPaid` before soft-delete.
  - `backend/KasserPro.Domain/Entities/Supplier.cs:44`: no check for linked `PurchaseInvoices` before soft-delete.
  - `backend/KasserPro.Domain/Entities/Product.cs:72`: no check for linked `SupplierProducts`.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/suppliers/SuppliersPage.tsx:47-58` uses a generic confirm only and does not disable the delete action while the request is in flight.
- **Risk Level:** HIGH
- **Risk Reason:** حذف مورد له فواتير شراء أو أرصدة قائمة يخفي طرفاً محاسبياً مؤثراً في التتبع والمراجعة.

### [SupplierService / SupplierFormModal] → [UpdateAsync / submit]
- **File:** `backend/KasserPro.Application/Services/Implementations/SupplierService.cs`
- **Layer:** Both
- **Entity:** Supplier
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/SupplierService.cs:120-124` checks the supplier exists in the current tenant and is not deleted.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Supplier.cs:23-28`: no check for outstanding supplier balance before `supplier.IsActive = request.IsActive` at `backend/KasserPro.Application/Services/Implementations/SupplierService.cs:126-135`.
  - `backend/KasserPro.Domain/Entities/Supplier.cs:44`: no check for open/partially paid purchase invoices before deactivation.
- **Frontend Gap (إن وُجد):** `frontend/src/components/suppliers/SupplierFormModal.tsx:223-238` exposes the active checkbox with no warning about unpaid invoices or linked purchase history.
- **Risk Level:** HIGH
- **Risk Reason:** تعطيل مورد ما زالت عليه التزامات شراء أو سداد يجعل البيانات المرجعية غير متاحة أثناء العمل اليومي.

### [BranchService / BranchesPage] → [DeleteAsync / handleDelete]
- **File:** `backend/KasserPro.Application/Services/Implementations/BranchService.cs`
- **Layer:** Both
- **Entity:** Branch
- **Operation:** SOFT-DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/BranchService.cs:169-173` checks the branch exists in the current tenant.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Branch.cs:25-31`: no checks for linked orders, shifts, users, inventories, product prices, or inventory transfers before soft-delete.
  - `backend/KasserPro.Domain/Entities/Shift.cs:138-140`: no check for open shifts or cash register history tied to the branch.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/branches/BranchesPage.tsx:31-45` warns only that related data will not be deleted, but does not enumerate open shifts, inventory, or users that will remain tied to the branch.
- **Risk Level:** CRITICAL
- **Risk Reason:** حذف فرع ما زالت عليه حركات مالية أو مخزنية يضرب السياق التشغيلي الذي تعتمد عليه التقارير والعزل بين الفروع.

### [BranchService / BranchFormModal] → [UpdateAsync / submit]
- **File:** `backend/KasserPro.Application/Services/Implementations/BranchService.cs`
- **Layer:** Both
- **Entity:** Branch
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/BranchService.cs:128-132` checks the branch exists in the current tenant.
  - `backend/KasserPro.Application/Services/Implementations/BranchService.cs:135-139` checks branch code uniqueness.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Branch.cs:25-31`: no guard against deactivating a branch that still has active users, open shifts, inventory, or transfer flows before `branch.IsActive = dto.IsActive` at `backend/KasserPro.Application/Services/Implementations/BranchService.cs:141-145`.
- **Frontend Gap (إن وُجد):** `frontend/src/components/branches/BranchFormModal.tsx:169-198` allows switching the branch to inactive with no confirmation and no impact summary.
- **Risk Level:** HIGH
- **Risk Reason:** تعطيل فرع نشط بدون فحص التبعيات قد يترك مستخدمين أو ورديات أو مخزوناً مرتبطاً بفرع غير صالح للتشغيل.

### [ProductService / ProductsPage] → [DeleteAsync / handleDelete]
- **File:** `backend/KasserPro.Application/Services/Implementations/ProductService.cs`
- **Layer:** Both
- **Entity:** Product
- **Operation:** SOFT-DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/ProductService.cs:330-333` checks the product exists in the current tenant.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Product.cs:70-76`: no check for linked order items, purchase invoice items, stock movements, branch inventory, branch prices, or transfers before soft-delete.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/products/ProductsPage.tsx:126-140` uses a generic confirm only and does not preview inventory or historical dependencies.
- **Risk Level:** HIGH
- **Risk Reason:** حذف منتج له أثر على المخزون أو المشتريات أو المبيعات يضعف التتبع المرجعي لعناصر مالية ومخزنية تاريخية.

### [ProductService / ProductFormModal] → [UpdateAsync / submit]
- **File:** `backend/KasserPro.Application/Services/Implementations/ProductService.cs`
- **Layer:** Both
- **Entity:** Product
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/ProductService.cs:263-270` checks valid price and product existence.
  - `backend/KasserPro.Application/Services/Implementations/ProductService.cs:272-275` checks category existence.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Product.cs:70-76`: no dependency check before `product.IsActive = request.IsActive` at `backend/KasserPro.Application/Services/Implementations/ProductService.cs:277-285` for live branch inventory, transfer flows, or draft selling contexts.
- **Frontend Gap (إن وُجد):** `frontend/src/components/products/ProductFormModal.tsx:184-205` and `frontend/src/components/products/ProductFormModal.tsx:503-529` allow deactivation inside the edit form with no confirmation or operational impact preview.
- **Risk Level:** MEDIUM
- **Risk Reason:** تعطيل منتج متداول قد يوقف البيع فجأة بينما لا يزال له مخزون وتسعير قائمين.

### [CustomerService / CustomersPage] → [DeleteAsync / handleDelete]
- **File:** `backend/KasserPro.Application/Services/Implementations/CustomerService.cs`
- **Layer:** Both
- **Entity:** Customer
- **Operation:** SOFT-DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/CustomerService.cs:535-539` checks the customer exists in the current tenant.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Customer.cs:62-67`: no guard against deleting a customer with non-zero `TotalDue`.
  - `backend/KasserPro.Domain/Entities/Customer.cs:78`: no guard against linked open orders or historical order activity before setting `IsActive = false` and `IsDeleted = true` at `backend/KasserPro.Application/Services/Implementations/CustomerService.cs:541-543`.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/customers/CustomersPage.tsx:68-77` and `frontend/src/pages/customers/CustomersPage.tsx:365-400` confirm deletion but do not show current debt, open orders, or payment history impact.
- **Risk Level:** CRITICAL
- **Risk Reason:** حذف عميل عليه دين أو نشاط بيع قائم يمكن أن يخفي ذمم مدينة ويضرب traceability لسجل العميل المالي.

### [CustomerService] → [UpdateAsync]
- **File:** `backend/KasserPro.Application/Services/Implementations/CustomerService.cs`
- **Layer:** Backend
- **Entity:** Customer
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/CustomerService.cs:118-122` checks the customer exists in the current tenant.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Customer.cs:62-67`: no check for outstanding debt before `customer.IsActive = request.IsActive.Value` at `backend/KasserPro.Application/Services/Implementations/CustomerService.cs:133-136`.
  - `backend/KasserPro.Domain/Entities/Customer.cs:78`: no check for linked open orders or unresolved credit activity.
- **Frontend Gap (إن وُجد):** No audited frontend control in `frontend/src/` was found for customer deactivation.
- **Risk Level:** HIGH
- **Risk Reason:** تعطيل عميل له مديونية أو طلبات مفتوحة يمكن أن يعطل عمليات التحصيل والمتابعة الائتمانية.

### [ExpenseCategoryService] → [DeleteAsync]
- **File:** `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs`
- **Layer:** Backend
- **Entity:** ExpenseCategory
- **Operation:** DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs:168-172` checks the category exists in the current tenant.
  - `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs:174-176` blocks deleting system categories.
  - `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs:179-183` blocks deletion when expenses reference the category.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/ExpenseCategory.cs:10` / `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs:179-180`: the dependency check omits `TenantId`, so another tenant's expense can block deletion.
- **Frontend Gap (إن وُجد):** No audited frontend delete control for expense categories was found in `frontend/src/`.
- **Risk Level:** MEDIUM
- **Risk Reason:** فحص التبعية بدون `TenantId` يكسر العزل بين الشركات ويؤدي إلى رفض حذف قانوني بشكل مضلل.

### [ExpenseCategoryService] → [UpdateAsync]
- **File:** `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs`
- **Layer:** Backend
- **Entity:** ExpenseCategory
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs:128-129` blocks editing system categories.
  - `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs:132-138` checks duplicate names inside the current tenant.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/ExpenseCategory.cs:54`: no check before `category.IsActive = request.IsActive` at `backend/KasserPro.Application/Services/Implementations/ExpenseCategoryService.cs:140-147` for active draft/approved expenses still using the category.
- **Frontend Gap (إن وُجد):** No audited frontend deactivation control for expense categories was found in `frontend/src/`.
- **Risk Level:** MEDIUM
- **Risk Reason:** تعطيل فئة مصروفات مستخدمة فعلياً يربك الإدخال والتصنيف المالي لاحقاً.

### [ExpenseService / ExpensesPage] → [DeleteAsync / handleDelete]
- **File:** `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs`
- **Layer:** Both
- **Entity:** Expense
- **Operation:** DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:247-253` checks the expense exists in the current tenant and branch.
  - `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:255-257` allows deletion only while status is `Draft`.
- **Missing Guards:**
  - NONE
- **Frontend Gap (إن وُجد):** `frontend/src/pages/expenses/ExpensesPage.tsx:64-73` uses a generic confirm only and does not preview attachments or approval history.
- **Risk Level:** LOW
- **Risk Reason:** الباك-إند يمنع حذف المصروف بعد خروجه من حالة المسودة، لكن الـ UI لا يشرح أثر الحذف للمستخدم.

### [ExpenseService / ExpenseDetailsPage] → [DeleteAttachmentAsync / handleDeleteAttachment]
- **File:** `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs`
- **Layer:** Both
- **Entity:** ExpenseAttachment
- **Operation:** DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:526-535` checks the parent expense exists in the current tenant and branch and is still `Draft`.
  - `backend/KasserPro.Application/Services/Implementations/ExpenseService.cs:537-542` checks the attachment exists for the targeted expense.
- **Missing Guards:**
  - NONE
- **Frontend Gap (إن وُجد):** `frontend/src/pages/expenses/ExpenseDetailsPage.tsx:137-147` uses a generic confirm only and does not show file metadata impact before deletion.
- **Risk Level:** LOW
- **Risk Reason:** الحذف محكوم بحالة المسودة فقط، لكن واجهة المستخدم لا تقدم تحذيراً غنياً قبل حذف مستند داعم.

### [ShiftService] → [DeleteAsync]
- **File:** `backend/KasserPro.Application/Services/Implementations/ShiftService.cs`
- **Layer:** Backend
- **Entity:** Shift
- **Operation:** DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/ShiftService.cs:237-243` hard-blocks deletion بالكامل لحماية السجل المالي.
- **Missing Guards:**
  - NONE
- **Frontend Gap (إن وُجد):** No audited frontend delete control for shifts was found in `frontend/src/`.
- **Risk Level:** LOW
- **Risk Reason:** الكود يمنع حذف الورديات صراحةً، وهو السلوك الآمن هنا.

### [UserManagementService / UserManagementCard] → [DeleteUserAsync / handleDelete]
- **File:** `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs`
- **Layer:** Both
- **Entity:** User
- **Operation:** SOFT-DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:261-263` checks the user exists and is not already deleted.
  - `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:265-266` enforces tenant match.
  - `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:268-269` blocks self-deletion.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/User.cs:23-24`: no check for linked open orders or open shifts before `user.IsDeleted = true` at `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:271-274`.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/users/components/UserManagementCard.tsx:23-31` and `frontend/src/pages/users/components/UserManagementCard.tsx:161-166` use a generic confirm and do not disable the delete button while the mutation is running.
- **Risk Level:** HIGH
- **Risk Reason:** حذف مستخدم ما زالت عليه وردية أو عمليات بيع مفتوحة يترك نشاطاً مالياً مرتبطاً به بدون عامل تشغيل صالح.

### [UserManagementService / UserManagementCard] → [ToggleUserStatusAsync / handleToggleStatus]
- **File:** `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs`
- **Layer:** Both
- **Entity:** User
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:291-293` checks the user exists and is not deleted.
  - `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:295-296` enforces tenant match.
  - `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:298-299` blocks self-deactivation.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/User.cs:24`: no check for open shifts before `user.IsActive = isActive` at `backend/KasserPro.Application/Services/Implementations/UserManagementService.cs:301-305`.
  - `backend/KasserPro.Domain/Entities/User.cs:23`: no check for in-flight operational ownership before deactivation.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/users/components/UserManagementCard.tsx:34-40` and `frontend/src/pages/users/components/UserManagementCard.tsx:146-160` toggle status with no confirmation and no loading-disabled state.
- **Risk Level:** HIGH
- **Risk Reason:** تعطيل كاشير أثناء وردية مفتوحة أو أثناء تشغيل فعلي يفتح باباً لتعطيل العمليات ونقص المساءلة التشغيلية.

### [SystemUserService / SystemUsersPage] → [ToggleUserStatusAsync / handleToggleStatus]
- **File:** `backend/KasserPro.Application/Services/Implementations/SystemUserService.cs`
- **Layer:** Both
- **Entity:** User
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/SystemUserService.cs:97-99` checks only that the user exists.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/User.cs:8-9`: no tenant/branch ownership validation before toggling status.
  - `backend/KasserPro.Domain/Entities/User.cs:24`: no check for open shifts.
  - `backend/KasserPro.Application/Services/Implementations/SystemUserService.cs:101-108`: no self-protection or business guard beyond existence.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/system/SystemUsersPage.tsx:59-66` and `frontend/src/pages/system/SystemUsersPage.tsx:286-303` toggle status without confirmation; only `SystemOwner` UI disable exists.
- **Risk Level:** HIGH
- **Risk Reason:** تعطيل مستخدم نظامي بدون فحص السياق التشغيلي أو العزل المؤسسي قد يعطل الوصول ويؤثر على ورديات وعمليات جارية.

### [TenantService / TenantCreationPage] → [SetTenantActiveStatusAsync / handleToggleTenantStatus]
- **File:** `backend/KasserPro.Application/Services/Implementations/TenantService.cs`
- **Layer:** Both
- **Entity:** Tenant
- **Operation:** DEACTIVATE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/TenantService.cs:318-320` checks the tenant exists.
  - `backend/KasserPro.Application/Services/Implementations/TenantService.cs:322-323` returns early on no-op.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Entities/Tenant.cs:68-72`: no guard against deactivating a tenant with active branches, users, products, or categories.
  - `backend/KasserPro.Domain/Entities/Branch.cs:25-31` / `backend/KasserPro.Domain/Entities/Shift.cs:138-140`: no guard for open shifts, branch inventory, or financial operations still running under the tenant.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/owner/TenantCreationPage.tsx:133-158` and `frontend/src/pages/owner/TenantCreationPage.tsx:498-511` confirm the action, but do not preview open branches, users, or financial impact.
- **Risk Level:** CRITICAL
- **Risk Reason:** تعطيل شركة كاملة بدون فحص العمليات النشطة قد يوقف النظام أثناء وجود ذمم وورديات وحركات مالية قائمة.

### [PurchaseInvoiceService / PurchaseInvoicesPage, PurchaseInvoiceDetailsPage] → [DeleteAsync / handleDelete]
- **File:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
- **Layer:** Both
- **Entity:** PurchaseInvoice
- **Operation:** SOFT-DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:326-330` checks the invoice exists in the current tenant and is not deleted.
  - `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:332-333` blocks deleting only when status is `Confirmed`.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Enums/PurchaseInvoiceStatus.cs:21-41`: no delete guard for `Paid`, `PartiallyPaid`, `Cancelled`, `Returned`, or `PartiallyReturned`.
  - `backend/KasserPro.Domain/Entities/PurchaseInvoice.cs:95-96`: no dependency guard for linked items and payments before soft-delete.
- **Frontend Gap (إن وُجد):** `frontend/src/pages/purchase-invoices/PurchaseInvoicesPage.tsx:41-50` and `frontend/src/pages/purchase-invoices/PurchaseInvoiceDetailsPage.tsx:52-63` use generic confirms only and do not show payment or stock impact.
- **Risk Level:** HIGH
- **Risk Reason:** حذف فاتورة شراء خارج المسودة يمكن أن يخفي التزام شراء أو أثر مخزني أو دفعات سابقة من واجهات التشغيل.

### [PurchaseInvoiceService] → [DeletePaymentAsync]
- **File:** `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs`
- **Layer:** Backend
- **Entity:** PurchaseInvoicePayment
- **Operation:** DELETE
- **Existing Guards:**
  - `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:599-603` checks the parent invoice exists in the current tenant and is not deleted.
  - `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:605-609` checks the payment exists for that invoice.
- **Missing Guards:**
  - `backend/KasserPro.Domain/Enums/PurchaseInvoiceStatus.cs:21-41`: no status guard before hard-deleting a payment from paid / partially paid / cancelled purchase invoices.
  - `backend/KasserPro.Domain/Entities/PurchaseInvoice.cs:54-59`: no post-delete state reconciliation to restore `Status` consistently after `AmountPaid` / `AmountDue` changes at `backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs:611-617`.
- **Frontend Gap (إن وُجد):** No audited frontend payment-delete control for purchase invoice payments was found in `frontend/src/`.
- **Risk Level:** CRITICAL
- **Risk Reason:** حذف دفعة شراء فعلياً من قاعدة البيانات بدون audit-safe guard يخرّب trail السداد ويمكن أن يغيّر الذمم الدائنة تاريخياً.

| Entity | Layer | Operation | Missing Guards Count | Max Risk |
|--------|-------|-----------|---------------------|----------|
| Category | Both | SOFT-DELETE | 0 | LOW |
| Category | Backend | DEACTIVATE | 1 | MEDIUM |
| Supplier | Both | SOFT-DELETE | 3 | HIGH |
| Supplier | Both | DEACTIVATE | 2 | HIGH |
| Branch | Both | SOFT-DELETE | 2 | CRITICAL |
| Branch | Both | DEACTIVATE | 1 | HIGH |
| Product | Both | SOFT-DELETE | 1 | HIGH |
| Product | Both | DEACTIVATE | 1 | MEDIUM |
| Customer | Both | SOFT-DELETE | 2 | CRITICAL |
| Customer | Backend | DEACTIVATE | 2 | HIGH |
| ExpenseCategory | Backend | DELETE | 1 | MEDIUM |
| ExpenseCategory | Backend | DEACTIVATE | 1 | MEDIUM |
| Expense | Both | DELETE | 0 | LOW |
| ExpenseAttachment | Both | DELETE | 0 | LOW |
| Shift | Backend | DELETE | 0 | LOW |
| User | Both | SOFT-DELETE | 1 | HIGH |
| User | Both | DEACTIVATE | 2 | HIGH |
| User | Both | DEACTIVATE | 3 | HIGH |
| Tenant | Both | DEACTIVATE | 2 | CRITICAL |
| PurchaseInvoice | Both | SOFT-DELETE | 2 | HIGH |
| PurchaseInvoicePayment | Backend | DELETE | 2 | CRITICAL |

Total operations audited: 21
Operations with missing guards: 17
CRITICAL issues: 4
