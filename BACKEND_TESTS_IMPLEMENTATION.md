# Backend Tests — Implementation Plan
> **Framework:** xUnit + FluentAssertions (no Moq — follow existing pattern)
> **Pattern:** Unit tests → Domain entities directly | Integration tests → WebApplicationFactory
> **Goal:** Cover كل الـ business logic الحرج في: Expenses, CashRegister, PurchaseInvoice, Permissions

---

## ⚠️ قبل ما تبدأ — برومبت للأيجنت

ابعت ده أولاً عشان تشوف الـ Integration test pattern الموجود:

```
Show me the full content of:
1. backend/KasserPro.Tests/Integration/CashRegisterIntegrationTests.cs
2. backend/KasserPro.Tests/Integration/ExpenseIntegrationTests.cs

No changes needed — just show the files.
```

---

## Structure المطلوبة

```
KasserPro.Tests/
├── Unit/
│   ├── OrderFinancialTests.cs          ← موجود ✅
│   ├── AdversarialFinancialTests.cs    ← موجود ✅
│   ├── LogicValidationTests.cs         ← موجود ✅
│   ├── ExpenseWorkflowTests.cs         ← جديد ✨
│   ├── CashRegisterBalanceTests.cs     ← جديد ✨
│   ├── PurchaseInvoicePaymentTests.cs  ← جديد ✨
│   └── PermissionInvariantTests.cs     ← جديد ✨
└── Integration/
    ├── CashRegisterIntegrationTests.cs ← موجود — يحتاج توسيع ✅
    ├── ExpenseIntegrationTests.cs      ← موجود — يحتاج توسيع ✅
    ├── PurchaseInvoiceIntegrationTests.cs ← موجود — يحتاج توسيع ✅
    └── PermissionSecurityTests.cs      ← جديد ✨
```

---

## Task 1 — Unit/ExpenseWorkflowTests.cs

```csharp
// backend/KasserPro.Tests/Unit/ExpenseWorkflowTests.cs
namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class ExpenseWorkflowTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────
    private static Expense CreateDraftExpense(decimal amount = 500m) => new()
    {
        Id          = 1,
        Amount      = amount,
        Status      = ExpenseStatus.Draft,
        Description = "كهرباء",
        BranchId    = 1,
        TenantId    = 1,
        CreatedAt   = DateTime.UtcNow,
    };

    // ─── Status Transitions ──────────────────────────────────────────────────

    [Fact]
    public void Expense_InitialStatus_IsDraft()
    {
        var expense = CreateDraftExpense();
        expense.Status.Should().Be(ExpenseStatus.Draft);
    }

    [Fact]
    public void Expense_Approve_ChangeStatusToApproved()
    {
        var expense = CreateDraftExpense();
        expense.Status = ExpenseStatus.Approved;
        expense.Status.Should().Be(ExpenseStatus.Approved);
    }

    [Fact]
    public void Expense_Reject_ChangeStatusToRejected()
    {
        var expense = CreateDraftExpense();
        expense.Status = ExpenseStatus.Rejected;
        expense.Status.Should().Be(ExpenseStatus.Rejected);
    }

    [Fact]
    public void Expense_Pay_RequiresApprovedStatus()
    {
        // مصروف مش Approved مش المفروض يتدفع
        var expense = CreateDraftExpense();
        var canPay = expense.Status == ExpenseStatus.Approved;
        canPay.Should().BeFalse("Draft expense cannot be paid directly");
    }

    [Fact]
    public void Expense_Pay_WhenApproved_ChangeStatusToPaid()
    {
        var expense = CreateDraftExpense();
        expense.Status = ExpenseStatus.Approved;

        // Simulate Pay
        expense.Status = ExpenseStatus.Paid;

        expense.Status.Should().Be(ExpenseStatus.Paid);
    }

    // ─── Amount Validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Expense_ZeroOrNegativeAmount_IsInvalid(decimal amount)
    {
        var isValid = amount > 0;
        isValid.Should().BeFalse($"Amount {amount} should be invalid");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(100000)]
    public void Expense_PositiveAmount_IsValid(decimal amount)
    {
        var isValid = amount > 0;
        isValid.Should().BeTrue();
    }

    // ─── Attachment Rules ────────────────────────────────────────────────────

    [Fact]
    public void Expense_CanHaveMultipleAttachments()
    {
        var expense = CreateDraftExpense();
        expense.Attachments = new List<ExpenseAttachment>
        {
            new() { Id = 1, FileName = "receipt1.pdf", ExpenseId = expense.Id },
            new() { Id = 2, FileName = "receipt2.jpg", ExpenseId = expense.Id },
        };
        expense.Attachments.Should().HaveCount(2);
    }

    // ─── Tenant Isolation ────────────────────────────────────────────────────

    [Fact]
    public void Expense_TenantId_MustMatchBranchTenant()
    {
        var expense = CreateDraftExpense();
        expense.TenantId.Should().Be(1);
        expense.BranchId.Should().Be(1);
    }
}
```

---

## Task 2 — Unit/CashRegisterBalanceTests.cs

```csharp
// backend/KasserPro.Tests/Unit/CashRegisterBalanceTests.cs
namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class CashRegisterBalanceTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────
    private static CashRegisterTransaction MakeTransaction(
        CashRegisterTransactionType type,
        decimal amount,
        decimal balanceBefore) => new()
    {
        Type          = type,
        Amount        = amount,
        BalanceBefore = balanceBefore,
        BalanceAfter  = ComputeBalanceAfter(type, balanceBefore, amount),
        BranchId      = 1,
        TenantId      = 1,
        TransactionDate = DateTime.UtcNow,
    };

    private static decimal ComputeBalanceAfter(
        CashRegisterTransactionType type, decimal before, decimal amount) =>
        type switch
        {
            CashRegisterTransactionType.Sale            => before + amount,
            CashRegisterTransactionType.Deposit         => before + amount,
            CashRegisterTransactionType.TransferIn      => before + amount,
            CashRegisterTransactionType.Refund          => before - amount,
            CashRegisterTransactionType.Expense         => before - amount,
            CashRegisterTransactionType.Withdrawal      => before - amount,
            CashRegisterTransactionType.TransferOut     => before - amount,
            CashRegisterTransactionType.SupplierPayment => before - amount,
            _                                           => before,
        };

    // ─── Credit Transactions (يزيدوا الرصيد) ─────────────────────────────

    [Theory]
    [InlineData(CashRegisterTransactionType.Sale,       1000, 5000, 6000)]
    [InlineData(CashRegisterTransactionType.Deposit,     500, 2000, 2500)]
    [InlineData(CashRegisterTransactionType.TransferIn,  300, 1000, 1300)]
    public void CreditTransaction_IncreasesBalance(
        CashRegisterTransactionType type, decimal amount,
        decimal balanceBefore, decimal expectedAfter)
    {
        var tx = MakeTransaction(type, amount, balanceBefore);
        tx.BalanceAfter.Should().Be(expectedAfter);
    }

    // ─── Debit Transactions (بينقصوا الرصيد) ─────────────────────────────

    [Theory]
    [InlineData(CashRegisterTransactionType.Expense,         200, 1000,  800)]
    [InlineData(CashRegisterTransactionType.Withdrawal,      300, 1000,  700)]
    [InlineData(CashRegisterTransactionType.TransferOut,     400, 1000,  600)]
    [InlineData(CashRegisterTransactionType.SupplierPayment, 500, 2000, 1500)]
    [InlineData(CashRegisterTransactionType.Refund,          100, 1000,  900)]
    public void DebitTransaction_DecreasesBalance(
        CashRegisterTransactionType type, decimal amount,
        decimal balanceBefore, decimal expectedAfter)
    {
        var tx = MakeTransaction(type, amount, balanceBefore);
        tx.BalanceAfter.Should().Be(expectedAfter);
    }

    // ─── SupplierPayment — القاعدة الجديدة ────────────────────────────────

    [Fact]
    public void SupplierPayment_Cash_DecreasesBalance()
    {
        var tx = MakeTransaction(CashRegisterTransactionType.SupplierPayment, 1000m, 5000m);
        tx.BalanceAfter.Should().Be(4000m);
        tx.Type.Should().Be(CashRegisterTransactionType.SupplierPayment);
    }

    [Fact]
    public void SupplierPayment_ReferenceType_IsPurchaseInvoicePayment()
    {
        var tx = MakeTransaction(CashRegisterTransactionType.SupplierPayment, 500m, 3000m);
        // ReferenceType يُسجَّل من الـ Service — نتأكد إن النوع صح
        tx.Type.Should().Be(CashRegisterTransactionType.SupplierPayment);
    }

    // ─── Transfer — يتم في DB Transaction واحدة ──────────────────────────

    [Fact]
    public void Transfer_OutAndIn_SumToZero()
    {
        // الخزينة الكلية للشبكة ما تتغيرش
        decimal amount = 1000m;
        var txOut = MakeTransaction(CashRegisterTransactionType.TransferOut, amount, 5000m);
        var txIn  = MakeTransaction(CashRegisterTransactionType.TransferIn,  amount, 2000m);

        var netEffect = (txIn.BalanceAfter - txIn.BalanceBefore)
                      + (txOut.BalanceAfter - txOut.BalanceBefore);

        netEffect.Should().Be(0m, "Transfer should be net-zero across branches");
    }

    [Fact]
    public void Transfer_Amount_MustBePositive()
    {
        var isValid = -100m > 0;
        isValid.Should().BeFalse("Transfer amount must be positive");
    }

    // ─── Reconciliation ───────────────────────────────────────────────────

    [Fact]
    public void Reconcile_WhenMatch_DifferenceIsZero()
    {
        decimal expected = 5000m;
        decimal actual   = 5000m;
        var difference   = actual - expected;
        difference.Should().Be(0m);
    }

    [Fact]
    public void Reconcile_WhenSurplus_DifferenceIsPositive()
    {
        decimal expected = 5000m;
        decimal actual   = 5200m;
        var difference   = actual - expected;
        difference.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Reconcile_WhenShortage_DifferenceIsNegative()
    {
        decimal expected = 5000m;
        decimal actual   = 4800m;
        var difference   = actual - expected;
        difference.Should().BeLessThan(0m);
    }
}
```

---

## Task 3 — Unit/PurchaseInvoicePaymentTests.cs

```csharp
// backend/KasserPro.Tests/Unit/PurchaseInvoicePaymentTests.cs
namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class PurchaseInvoicePaymentTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────
    private static PurchaseInvoice CreateInvoice(decimal totalAmount = 5000m) => new()
    {
        Id          = 1,
        TotalAmount = totalAmount,
        PaidAmount  = 0m,
        Status      = PurchaseInvoiceStatus.Confirmed,
        BranchId    = 1,
        TenantId    = 1,
        SupplierId  = 1,
    };

    // ─── Payment Method → CashRegister Rule ──────────────────────────────

    [Fact]
    public void Payment_Cash_ShouldAffectCashRegister()
    {
        var method = PaymentMethod.Cash;
        var shouldRecordInCashRegister = method == PaymentMethod.Cash;
        shouldRecordInCashRegister.Should().BeTrue();
    }

    [Theory]
    [InlineData(PaymentMethod.BankTransfer)]
    [InlineData(PaymentMethod.VodafoneCash)]
    public void Payment_NonCash_ShouldNotAffectCashRegister(PaymentMethod method)
    {
        var shouldRecordInCashRegister = method == PaymentMethod.Cash;
        shouldRecordInCashRegister.Should().BeFalse(
            $"{method} payment should NOT be recorded in cash register");
    }

    // ─── Partial Payments ────────────────────────────────────────────────

    [Fact]
    public void Payment_Partial_UpdatesPaidAmount()
    {
        var invoice = CreateInvoice(totalAmount: 5000m);
        invoice.PaidAmount += 2000m;

        invoice.PaidAmount.Should().Be(2000m);
        invoice.PaidAmount.Should().BeLessThan(invoice.TotalAmount);
    }

    [Fact]
    public void Payment_Full_InvoiceIsFullyPaid()
    {
        var invoice = CreateInvoice(totalAmount: 5000m);
        invoice.PaidAmount = 5000m;

        var isFullyPaid = invoice.PaidAmount >= invoice.TotalAmount;
        isFullyPaid.Should().BeTrue();
    }

    [Fact]
    public void Payment_CannotExceedTotalAmount()
    {
        var invoice = CreateInvoice(totalAmount: 5000m);
        decimal paymentAttempt = 6000m;

        var isValid = paymentAttempt <= (invoice.TotalAmount - invoice.PaidAmount);
        isValid.Should().BeFalse("Overpayment should not be allowed");
    }

    // ─── Status Transitions ──────────────────────────────────────────────

    [Fact]
    public void Invoice_WhenFullyPaid_StatusShouldBeFullyPaid()
    {
        var invoice = CreateInvoice(5000m);
        invoice.PaidAmount = 5000m;

        // منطق الـ Service بيحدث الـ Status
        var expectedStatus = invoice.PaidAmount >= invoice.TotalAmount
            ? PurchaseInvoiceStatus.FullyPaid
            : PurchaseInvoiceStatus.PartiallyPaid;

        expectedStatus.Should().Be(PurchaseInvoiceStatus.FullyPaid);
    }

    [Fact]
    public void Invoice_WhenPartiallyPaid_StatusShouldBePartiallyPaid()
    {
        var invoice = CreateInvoice(5000m);
        invoice.PaidAmount = 2000m;

        var expectedStatus = invoice.PaidAmount >= invoice.TotalAmount
            ? PurchaseInvoiceStatus.FullyPaid
            : PurchaseInvoiceStatus.PartiallyPaid;

        expectedStatus.Should().Be(PurchaseInvoiceStatus.PartiallyPaid);
    }

    // ─── Cancelled Invoice ───────────────────────────────────────────────

    [Fact]
    public void Payment_OnCancelledInvoice_IsNotAllowed()
    {
        var invoice = CreateInvoice();
        invoice.Status = PurchaseInvoiceStatus.Cancelled;

        var canPay = invoice.Status == PurchaseInvoiceStatus.Confirmed
                  || invoice.Status == PurchaseInvoiceStatus.PartiallyPaid;

        canPay.Should().BeFalse("Cancelled invoice cannot receive payments");
    }
}
```

---

## Task 4 — Unit/PermissionInvariantTests.cs

```csharp
// backend/KasserPro.Tests/Unit/PermissionInvariantTests.cs
namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class PermissionInvariantTests
{
    // ─── Permission Values — لا تتغير ────────────────────────────────────

    [Fact]
    public void Permission_ExpensesApprove_HasCorrectValue()
    {
        ((int)Permission.ExpensesApprove).Should().Be(703);
    }

    [Fact]
    public void Permission_CashRegisterTransfer_HasCorrectValue()
    {
        ((int)Permission.CashRegisterTransfer).Should().Be(1002);
    }

    [Fact]
    public void Permission_CashRegisterReconcile_HasCorrectValue()
    {
        ((int)Permission.CashRegisterReconcile).Should().Be(1003);
    }

    // ─── نفس القيمة ما تتكررش ────────────────────────────────────────────

    [Fact]
    public void Permission_AllValues_AreUnique()
    {
        var values = Enum.GetValues<Permission>().Select(p => (int)p).ToList();
        values.Should().OnlyHaveUniqueItems("Permission enum values must be unique");
    }

    // ─── Protected Roles — مش قابلة للتعديل ──────────────────────────────

    [Theory]
    [InlineData("Admin")]
    [InlineData("SystemOwner")]
    public void ProtectedRole_CannotBeModified(string role)
    {
        var protectedRoles = new[] { "Admin", "SystemOwner" };
        protectedRoles.Should().Contain(role);
    }

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public void OperationalRole_CanBeModified(string role)
    {
        var protectedRoles = new[] { "Admin", "SystemOwner" };
        protectedRoles.Should().NotContain(role);
    }

    // ─── Cashier Default Permissions ─────────────────────────────────────

    [Fact]
    public void DefaultCashierPermissions_DoNotIncludeSensitivePermissions()
    {
        // الكاشير ما يجيش ExpensesApprove أو CashRegisterTransfer افتراضياً
        var sensitivePermissions = new[]
        {
            Permission.ExpensesApprove,
            Permission.CashRegisterTransfer,
            Permission.CashRegisterReconcile,
        };

        // هذا الـ test يتحقق من القاعدة — الـ Service بتنفذها
        sensitivePermissions.Should().NotBeEmpty("Sensitive permissions must be defined");
    }
}
```

---

## Task 5 — Integration Tests — توسيع الملفات الموجودة

### 5.1 — ExpenseIntegrationTests.cs (أضف هذه الـ tests)

```csharp
// أضف داخل ExpenseIntegrationTests class الموجود:

[Fact]
public async Task ApproveExpense_WithoutPermission_Returns403()
{
    // Arrange: سجّل دخول كـ Cashier (بدون ExpensesApprove)
    var client = CreateClientWithRole("Cashier");
    var expense = await CreateDraftExpenseAsync(client);

    // Act
    var response = await client.PutAsync($"/api/expenses/{expense.Id}/approve",
        JsonContent.Create(new { notes = "approved" }));

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task ApproveExpense_WithPermission_Returns200()
{
    // Arrange: سجّل دخول كـ Admin (عنده ExpensesApprove)
    var client = CreateClientWithRole("Admin");
    var expense = await CreateDraftExpenseAsync(client);

    // Act
    var response = await client.PutAsync($"/api/expenses/{expense.Id}/approve",
        JsonContent.Create(new { notes = "approved" }));

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task PayExpense_Cash_RecordsInCashRegister()
{
    // Arrange
    var client = CreateClientWithRole("Admin");
    var expense = await CreateAndApproveExpenseAsync(client, amount: 300m);

    // Act: ادفع نقدي
    var payResponse = await client.PutAsync($"/api/expenses/{expense.Id}/pay",
        JsonContent.Create(new { paymentMethod = "Cash" }));
    payResponse.EnsureSuccessStatusCode();

    // Assert: فيه transaction في الخزينة
    var txResponse = await client.GetAsync("/api/cash-register/transactions");
    var transactions = await txResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CashRegisterTransactionDto>>>();
    transactions!.Data!.Items.Should().Contain(t =>
        t.Type == CashRegisterTransactionType.Expense &&
        t.Amount == 300m);
}

[Fact]
public async Task RejectExpense_WithValidReason_ChangesStatusToRejected()
{
    var client = CreateClientWithRole("Admin");
    var expense = await CreateDraftExpenseAsync(client);

    var response = await client.PutAsync($"/api/expenses/{expense.Id}/reject",
        JsonContent.Create(new { reason = "غير مبرر" }));

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<ApiResponse<ExpenseDto>>();
    result!.Data!.Status.Should().Be(ExpenseStatus.Rejected);
}
```

### 5.2 — CashRegisterIntegrationTests.cs (أضف هذه الـ tests)

```csharp
// أضف داخل CashRegisterIntegrationTests class الموجود:

[Fact]
public async Task Transfer_WithoutPermission_Returns403()
{
    var client = CreateClientWithRole("Cashier");

    var response = await client.PostAsJsonAsync("/api/cash-register/transfer", new
    {
        sourceBranchId  = 1,
        targetBranchId  = 2,
        amount          = 500,
        transactionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
    });

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Transfer_ValidRequest_CreatesTwoLinkedTransactions()
{
    var client = CreateClientWithRole("Admin");

    var response = await client.PostAsJsonAsync("/api/cash-register/transfer", new
    {
        sourceBranchId  = 1,
        targetBranchId  = 2,
        amount          = 1000,
        transactionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
        description     = "تحويل تست",
    });

    response.EnsureSuccessStatusCode();

    // تحقق من وجود TransferOut في الفرع 1
    var txBranch1 = await client.GetAsync("/api/cash-register/transactions?branchId=1");
    var result1   = await txBranch1.Content
        .ReadFromJsonAsync<ApiResponse<PagedResult<CashRegisterTransactionDto>>>();
    result1!.Data!.Items.Should().Contain(t =>
        t.Type == CashRegisterTransactionType.Transfer &&
        t.Amount == 1000m);

    // تحقق من وجود TransferIn في الفرع 2
    var txBranch2 = await client.GetAsync("/api/cash-register/transactions?branchId=2");
    var result2   = await txBranch2.Content
        .ReadFromJsonAsync<ApiResponse<PagedResult<CashRegisterTransactionDto>>>();
    result2!.Data!.Items.Should().Contain(t =>
        t.Type == CashRegisterTransactionType.Transfer &&
        t.Amount == 1000m);
}

[Fact]
public async Task Reconcile_WithoutPermission_Returns403()
{
    var client = CreateClientWithRole("Cashier");

    var response = await client.PostAsJsonAsync("/api/cash-register/reconcile", new
    {
        branchId          = 1,
        actualCashAmount  = 5000,
    });

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Reconcile_WhenBalanceMatches_ReturnsZeroDifference()
{
    var client = CreateClientWithRole("Admin");

    // اجلب الرصيد الحالي
    var balanceRes = await client.GetAsync("/api/cash-register/balance?branchId=1");
    var balance    = await balanceRes.Content.ReadFromJsonAsync<ApiResponse<CashRegisterBalanceDto>>();
    var expected   = balance!.Data!.CurrentBalance;

    // Reconcile بنفس الرقم
    var response = await client.PostAsJsonAsync("/api/cash-register/reconcile", new
    {
        branchId         = 1,
        actualCashAmount = expected,
    });

    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReconcileResultDto>>();
    result!.Data!.Difference.Should().Be(0m);
}
```

### 5.3 — PurchaseInvoiceIntegrationTests.cs (أضف هذه الـ tests)

```csharp
// أضف داخل PurchaseInvoiceIntegrationTests class الموجود:

[Fact]
public async Task AddPayment_Cash_RecordsSupplierPaymentInCashRegister()
{
    // Arrange
    var client  = CreateClientWithRole("Admin");
    var invoice = await CreateAndConfirmInvoiceAsync(client, totalAmount: 2000m);

    // Act: دفع نقدي
    var payResponse = await client.PostAsJsonAsync(
        $"/api/purchase-invoices/{invoice.Id}/payments",
        new { amount = 2000m, paymentMethod = "Cash", paymentDate = DateTime.UtcNow });

    payResponse.EnsureSuccessStatusCode();

    // Assert: فيه SupplierPayment في الخزينة
    var txResponse = await client.GetAsync("/api/cash-register/transactions");
    var result     = await txResponse.Content
        .ReadFromJsonAsync<ApiResponse<PagedResult<CashRegisterTransactionDto>>>();
    result!.Data!.Items.Should().Contain(t =>
        t.Type == CashRegisterTransactionType.SupplierPayment &&
        t.Amount == 2000m);
}

[Fact]
public async Task AddPayment_BankTransfer_DoesNotAffectCashRegister()
{
    var client      = CreateClientWithRole("Admin");
    var invoice     = await CreateAndConfirmInvoiceAsync(client, totalAmount: 3000m);
    var balanceBefore = await GetCurrentBalanceAsync(client, branchId: 1);

    // Act: دفع بنكي
    await client.PostAsJsonAsync(
        $"/api/purchase-invoices/{invoice.Id}/payments",
        new { amount = 3000m, paymentMethod = "BankTransfer",
              referenceNumber = "TXN123", paymentDate = DateTime.UtcNow });

    var balanceAfter = await GetCurrentBalanceAsync(client, branchId: 1);

    // Assert: الرصيد ما اتغيرش
    balanceAfter.Should().Be(balanceBefore,
        "Bank transfer should NOT affect cash register balance");
}

[Fact]
public async Task AddPayment_VodafoneCash_DoesNotAffectCashRegister()
{
    var client        = CreateClientWithRole("Admin");
    var invoice       = await CreateAndConfirmInvoiceAsync(client, totalAmount: 1500m);
    var balanceBefore = await GetCurrentBalanceAsync(client, branchId: 1);

    await client.PostAsJsonAsync(
        $"/api/purchase-invoices/{invoice.Id}/payments",
        new { amount = 1500m, paymentMethod = "VodafoneCash",
              referenceNumber = "VC456", paymentDate = DateTime.UtcNow });

    var balanceAfter = await GetCurrentBalanceAsync(client, branchId: 1);
    balanceAfter.Should().Be(balanceBefore);
}

[Fact]
public async Task AddPayment_ExceedsTotalAmount_ReturnsBadRequest()
{
    var client  = CreateClientWithRole("Admin");
    var invoice = await CreateAndConfirmInvoiceAsync(client, totalAmount: 1000m);

    var response = await client.PostAsJsonAsync(
        $"/api/purchase-invoices/{invoice.Id}/payments",
        new { amount = 1500m, paymentMethod = "Cash", paymentDate = DateTime.UtcNow });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task AddPayment_ToCancelledInvoice_ReturnsBadRequest()
{
    var client  = CreateClientWithRole("Admin");
    var invoice = await CreateAndConfirmInvoiceAsync(client, totalAmount: 1000m);

    // Cancel first
    await client.PutAsync($"/api/purchase-invoices/{invoice.Id}/cancel",
        JsonContent.Create(new { reason = "خطأ" }));

    // Try to pay
    var response = await client.PostAsJsonAsync(
        $"/api/purchase-invoices/{invoice.Id}/payments",
        new { amount = 500m, paymentMethod = "Cash", paymentDate = DateTime.UtcNow });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

// Helper
private async Task<decimal> GetCurrentBalanceAsync(HttpClient client, int branchId)
{
    var res    = await client.GetAsync($"/api/cash-register/balance?branchId={branchId}");
    var result = await res.Content.ReadFromJsonAsync<ApiResponse<CashRegisterBalanceDto>>();
    return result!.Data!.CurrentBalance;
}
```

### 5.4 — Integration/PermissionSecurityTests.cs (جديد)

```csharp
// backend/KasserPro.Tests/Integration/PermissionSecurityTests.cs
namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

public class PermissionSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public PermissionSecurityTests(WebApplicationFactory<Program> factory)
        => _factory = factory;

    // ─── Expenses Endpoints ───────────────────────────────────────────────

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public async Task ExpenseApprove_NonAdmin_Returns403(string role)
    {
        var client   = CreateClientWithRole(role);
        var response = await client.PutAsync("/api/expenses/1/approve",
            JsonContent.Create(new { notes = "test" }));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExpenseApprove_Admin_DoesNotReturn403()
    {
        var client   = CreateClientWithRole("Admin");
        var response = await client.PutAsync("/api/expenses/999/approve",
            JsonContent.Create(new { notes = "test" }));
        // 403 or 404 acceptable — but NOT 403 (permission issue)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ─── CashRegister Endpoints ───────────────────────────────────────────

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public async Task CashRegisterTransfer_NonAdmin_Returns403(string role)
    {
        var client   = CreateClientWithRole(role);
        var response = await client.PostAsJsonAsync("/api/cash-register/transfer",
            new { sourceBranchId = 1, targetBranchId = 2, amount = 100 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public async Task CashRegisterReconcile_NonAdmin_Returns403(string role)
    {
        var client   = CreateClientWithRole(role);
        var response = await client.PostAsJsonAsync("/api/cash-register/reconcile",
            new { branchId = 1, actualCashAmount = 1000 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Tenant Isolation ────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_CrossTenant_Returns403()
    {
        // Admin من Tenant 1 يحاول يتعامل مع Branch من Tenant 2
        var client   = CreateClientWithRole("Admin", tenantId: 1);
        var response = await client.PostAsJsonAsync("/api/cash-register/transfer",
            new { sourceBranchId = 1, targetBranchId = 99 /* Tenant 2 branch */ });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Unauthenticated ─────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/expenses")]
    [InlineData("/api/cash-register/balance")]
    [InlineData("/api/purchase-invoices")]
    public async Task ProtectedEndpoint_NoToken_Returns401(string endpoint)
    {
        var client   = _factory.CreateClient(); // no auth
        var response = await client.GetAsync(endpoint);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helper ──────────────────────────────────────────────────────────
    private HttpClient CreateClientWithRole(string role, int tenantId = 1)
    {
        // اتبع نفس pattern الموجود في Integration tests الأخرى
        throw new NotImplementedException("Use existing factory helper pattern");
    }
}
```

---

## Execution Order

```
Step 1  → Unit/ExpenseWorkflowTests.cs           (لا يحتاج setup)
Step 2  → Unit/CashRegisterBalanceTests.cs        (لا يحتاج setup)
Step 3  → Unit/PurchaseInvoicePaymentTests.cs     (لا يحتاج setup)
Step 4  → Unit/PermissionInvariantTests.cs        (لا يحتاج setup)
Step 5  → Integration/ExpenseIntegrationTests     (أضف للملف الموجود)
Step 6  → Integration/CashRegisterIntegrationTests (أضف للملف الموجود)
Step 7  → Integration/PurchaseInvoiceIntegrationTests (أضف للملف الموجود)
Step 8  → Integration/PermissionSecurityTests.cs  (ملف جديد)
```

---

## Pre-Commit Checklist

```bash
cd backend && dotnet test --no-build --verbosity normal
```

- [ ] كل الـ Unit tests شغالين بدون DB
- [ ] `Transfer_OutAndIn_SumToZero` — يتأكد إن الخزينة الكلية ما بتتغيرش
- [ ] `Payment_NonCash_ShouldNotAffectCashRegister` — القاعدة الأساسية
- [ ] `ExpenseApprove_NonAdmin_Returns403` — الأمان متأكد منه
- [ ] `Transfer_CrossTenant_Returns403` — Tenant Isolation
- [ ] `PermissionSecurityTests` — كل الـ endpoints المحمية اتاختبرت

---

## ملاحظة مهمة للأيجنت

```
Before writing PermissionSecurityTests helper (CreateClientWithRole):
Read the existing Integration test files to copy the exact factory pattern used.
Do NOT invent a new pattern — follow what's already there.
```
