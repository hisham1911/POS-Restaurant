# Concurrency Handling in Credit Sales System

## Overview

The Credit Sales system handles concurrent payment attempts through **database transactions** and **SQLite's write locking mechanism**. Here's the detailed analysis:

---

## 1. Transaction Isolation in PayDebtAsync

### Code Location
`backend/KasserPro.Application/Services/Implementations/CustomerService.cs` - Line 280

### Implementation

```csharp
public async Task<ApiResponse<PayDebtResponse>> PayDebtAsync(int customerId, PayDebtRequest request, int recordedByUserId)
{
    var tenantId = _currentUser.TenantId;
    var branchId = _currentUser.BranchId;
    
    // Validate amount
    if (request.Amount <= 0)
        return ApiResponse<PayDebtResponse>.Fail("INVALID_AMOUNT", "المبلغ يجب أن يكون أكبر من صفر");
    
    // Get customer - READ OUTSIDE TRANSACTION (dirty read acceptable for validation)
    var customer = await _unitOfWork.Customers.Query()
        .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);
    
    if (customer == null)
        return ApiResponse<PayDebtResponse>.Fail("CUSTOMER_NOT_FOUND", "العميل غير موجود");
    
    // Validate payment amount doesn't exceed debt - OPTIMISTIC CHECK
    if (request.Amount > customer.TotalDue)
        return ApiResponse<PayDebtResponse>.Fail("AMOUNT_EXCEEDS_DEBT", 
            $"المبلغ ({request.Amount:F2}) أكبر من الدين المستحق ({customer.TotalDue:F2})");
    
    // ... user and shift lookup ...
    
    // ✅ BEGIN TRANSACTION - SERIALIZABLE ISOLATION
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    
    try
    {
        // ✅ CRITICAL SECTION - Protected by SQLite write lock
        var balanceBefore = customer.TotalDue;  // Read current balance
        var balanceAfter = balanceBefore - request.Amount;  // Calculate new balance
        
        // Create debt payment record
        var debtPayment = new DebtPayment
        {
            TenantId = tenantId,
            BranchId = branchId,
            CustomerId = customerId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes,
            RecordedByUserId = recordedByUserId,
            RecordedByUserName = user.Name,
            ShiftId = currentShift?.Id,
            BalanceBefore = balanceBefore,  // ✅ Audit: Balance before
            BalanceAfter = balanceAfter      // ✅ Audit: Balance after
        };
        
        await _unitOfWork.DebtPayments.AddAsync(debtPayment);
        
        // ✅ UPDATE CUSTOMER BALANCE - Atomic operation
        customer.TotalDue = balanceAfter;
        
        // ✅ SAVE ALL CHANGES - Single database write
        await _unitOfWork.SaveChangesAsync();
        
        // ✅ CASH REGISTER INTEGRATION (if Cash payment)
        if (request.PaymentMethod == Domain.Enums.PaymentMethod.Cash)
        {
            await _cashRegisterService.RecordTransactionAsync(
                type: Domain.Enums.CashRegisterTransactionType.Sale,
                amount: request.Amount,
                description: $"تسديد دين - عميل: {customer.Name ?? customer.Phone}",
                referenceType: "DebtPayment",
                referenceId: debtPayment.Id,
                shiftId: currentShift?.Id
            );
        }
        
        // ✅ COMMIT TRANSACTION - Release lock
        await transaction.CommitAsync();
        
        var response = new PayDebtResponse
        {
            PaymentId = debtPayment.Id,
            AmountPaid = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            RemainingDebt = balanceAfter,
            Message = balanceAfter == 0 
                ? "تم تسديد الدين بالكامل" 
                : $"تم تسديد {request.Amount:F2} ج.م - المتبقي: {balanceAfter:F2} ج.م"
        };
        
        return ApiResponse<PayDebtResponse>.Ok(response, response.Message);
    }
    catch (Exception ex)
    {
        // ✅ ROLLBACK ON ERROR - Undo all changes
        await transaction.RollbackAsync();
        return ApiResponse<PayDebtResponse>.Fail("SYSTEM_ERROR", 
            $"حدث خطأ أثناء تسجيل الدفع: {ex.Message}");
    }
}
```

---

## 2. SQLite Write Lock Mechanism

### How SQLite Handles Concurrency

SQLite uses a **single-writer, multiple-reader** locking model:

1. **BEGIN TRANSACTION** acquires a **RESERVED lock**
2. First **write operation** upgrades to **EXCLUSIVE lock**
3. **EXCLUSIVE lock** blocks ALL other writers
4. **COMMIT** releases the lock

### Timeline Example: Two Concurrent Payment Attempts

```
Time    Thread A (User 1)                    Thread B (User 2)
----    ---------------------------------    ---------------------------------
T0      BEGIN TRANSACTION                    
T1      Read Customer.TotalDue = 1000        
T2                                           BEGIN TRANSACTION
T3                                           Read Customer.TotalDue = 1000
T4      Create DebtPayment (500)             
T5      Update Customer.TotalDue = 500       ← EXCLUSIVE LOCK ACQUIRED
T6                                           Create DebtPayment (300) ← BLOCKED
T7      SaveChangesAsync()                   ← BLOCKED
T8      COMMIT                               ← LOCK RELEASED
T9                                           ← UNBLOCKED, continues
T10                                          Update Customer.TotalDue = 700 ❌ WRONG!
T11                                          SaveChangesAsync()
T12                                          COMMIT
```

### ⚠️ PROBLEM IDENTIFIED: Lost Update

In the current implementation, **Thread B reads stale data** before Thread A commits. This causes a **lost update** problem.

---

## 3. The Concurrency Bug

### Scenario: Two Cashiers Pay Same Customer's Debt Simultaneously

**Initial State:**
- Customer TotalDue: 1000 ج.م

**Actions:**
- Cashier A: Pays 500 ج.م
- Cashier B: Pays 300 ج.م (at the same time)

**Expected Result:**
- TotalDue = 1000 - 500 - 300 = 200 ج.م

**Actual Result (with current code):**
- Thread A: TotalDue = 1000 - 500 = 500 ج.م ✅
- Thread B: TotalDue = 1000 - 300 = 700 ج.م ❌ (overwrites Thread A's update)
- **Final TotalDue = 700 ج.م** (lost 500 ج.م payment!)

---

## 4. The Fix: Read Inside Transaction

### Current Code (VULNERABLE)

```csharp
// ❌ Read OUTSIDE transaction
var customer = await _unitOfWork.Customers.Query()
    .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

// Validate
if (request.Amount > customer.TotalDue)
    return ApiResponse<PayDebtResponse>.Fail(...);

// BEGIN TRANSACTION
await using var transaction = await _unitOfWork.BeginTransactionAsync();

try
{
    // ❌ Uses stale customer.TotalDue value
    var balanceBefore = customer.TotalDue;
    var balanceAfter = balanceBefore - request.Amount;
    
    customer.TotalDue = balanceAfter;
    await _unitOfWork.SaveChangesAsync();
    await transaction.CommitAsync();
}
```

### Fixed Code (SAFE)

```csharp
// Preliminary validation (can use stale data)
var customerCheck = await _unitOfWork.Customers.Query()
    .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

if (customerCheck == null)
    return ApiResponse<PayDebtResponse>.Fail("CUSTOMER_NOT_FOUND", "العميل غير موجود");

// BEGIN TRANSACTION
await using var transaction = await _unitOfWork.BeginTransactionAsync();

try
{
    // ✅ RE-READ customer INSIDE transaction (gets fresh data with lock)
    var customer = await _unitOfWork.Customers.Query()
        .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);
    
    if (customer == null)
    {
        await transaction.RollbackAsync();
        return ApiResponse<PayDebtResponse>.Fail("CUSTOMER_NOT_FOUND", "العميل غير موجود");
    }
    
    // ✅ VALIDATE with fresh data
    if (request.Amount > customer.TotalDue)
    {
        await transaction.RollbackAsync();
        return ApiResponse<PayDebtResponse>.Fail("AMOUNT_EXCEEDS_DEBT", 
            $"المبلغ ({request.Amount:F2}) أكبر من الدين المستحق ({customer.TotalDue:F2})");
    }
    
    // ✅ Use fresh balance
    var balanceBefore = customer.TotalDue;
    var balanceAfter = balanceBefore - request.Amount;
    
    // Create debt payment record
    var debtPayment = new DebtPayment { ... };
    await _unitOfWork.DebtPayments.AddAsync(debtPayment);
    
    // Update customer balance
    customer.TotalDue = balanceAfter;
    
    await _unitOfWork.SaveChangesAsync();
    
    // Cash register integration...
    
    await transaction.CommitAsync();
}
catch (Exception ex)
{
    await transaction.RollbackAsync();
    return ApiResponse<PayDebtResponse>.Fail("SYSTEM_ERROR", $"حدث خطأ: {ex.Message}");
}
```

---

## 5. Corrected Timeline with Fix

```
Time    Thread A (User 1)                    Thread B (User 2)
----    ---------------------------------    ---------------------------------
T0      BEGIN TRANSACTION                    
T1      Read Customer.TotalDue = 1000        
T2      Validate: 500 <= 1000 ✅             
T3                                           BEGIN TRANSACTION
T4                                           Read Customer.TotalDue = 1000 ← BLOCKED
T5      Create DebtPayment (500)             ← BLOCKED
T6      Update Customer.TotalDue = 500       ← BLOCKED
T7      SaveChangesAsync()                   ← BLOCKED
T8      COMMIT                               ← LOCK RELEASED
T9                                           ← UNBLOCKED, reads fresh data
T10                                          Read Customer.TotalDue = 500 ✅
T11                                          Validate: 300 <= 500 ✅
T12                                          Create DebtPayment (300)
T13                                          Update Customer.TotalDue = 200 ✅
T14                                          SaveChangesAsync()
T15                                          COMMIT
```

**Final Result:** TotalDue = 200 ج.م ✅ CORRECT

---

## 6. Additional Concurrency Protections

### A. Optimistic Concurrency with RowVersion (Future Enhancement)

```csharp
public class Customer : BaseEntity
{
    // ... existing fields ...
    
    [Timestamp]
    public byte[] RowVersion { get; set; }  // EF Core concurrency token
}
```

**How it works:**
1. Read customer with RowVersion = v1
2. Update customer
3. SaveChanges checks: `WHERE Id = @id AND RowVersion = @v1`
4. If RowVersion changed → throws `DbUpdateConcurrencyException`
5. Application retries with fresh data

### B. Pessimistic Locking (Not Needed for SQLite)

SQLite's EXCLUSIVE lock already provides pessimistic locking during writes.

### C. Application-Level Locking (Overkill)

```csharp
private static readonly SemaphoreSlim _customerLocks = new SemaphoreSlim(1, 1);

await _customerLocks.WaitAsync();
try
{
    // Process payment
}
finally
{
    _customerLocks.Release();
}
```

**Not recommended:** Doesn't scale across multiple servers.

---

## 7. Cash Register Concurrency

### CashRegisterService.RecordTransactionAsync

**Code Location:** `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs` - Line 431

```csharp
public async Task RecordTransactionAsync(
    CashRegisterTransactionType type,
    decimal amount,
    string description,
    string? referenceType = null,
    int? referenceId = null,
    int? shiftId = null)
{
    // ✅ CHECK: If already in a transaction, piggyback on it
    var ownsTransaction = !_unitOfWork.HasActiveTransaction;
    IDbContextTransaction? transaction = null;
    
    if (ownsTransaction)
    {
        // ✅ Create new transaction if not already in one
        transaction = await _unitOfWork.BeginTransactionAsync();
    }
    
    try
    {
        // ✅ Read current balance INSIDE transaction (protected by lock)
        var currentBalance = await GetCurrentBalanceForBranchAsync(_currentUserService.BranchId);

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

        var transactionNumber = await GenerateTransactionNumberAsync();

        // ✅ Calculate new balance
        var balanceAfter = type switch
        {
            CashRegisterTransactionType.Sale => currentBalance + amount,
            CashRegisterTransactionType.Deposit => currentBalance + amount,
            CashRegisterTransactionType.Opening => amount,
            CashRegisterTransactionType.Refund => currentBalance - amount,
            CashRegisterTransactionType.Withdrawal => currentBalance - amount,
            CashRegisterTransactionType.Expense => currentBalance - amount,
            CashRegisterTransactionType.SupplierPayment => currentBalance - amount,
            CashRegisterTransactionType.Adjustment => currentBalance + amount,
            CashRegisterTransactionType.ShiftClose => amount,
            _ => currentBalance
        };

        // ✅ Create transaction record
        var cashTransaction = new CashRegisterTransaction
        {
            TenantId = _currentUserService.TenantId,
            BranchId = _currentUserService.BranchId,
            TransactionNumber = transactionNumber,
            Type = type,
            Amount = amount,
            BalanceBefore = currentBalance,  // ✅ Audit
            BalanceAfter = balanceAfter,     // ✅ Audit
            TransactionDate = DateTime.UtcNow,
            Description = description,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ShiftId = shiftId,
            UserId = _currentUserService.UserId,
            UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
        };

        await _unitOfWork.CashRegisterTransactions.AddAsync(cashTransaction);
        await _unitOfWork.SaveChangesAsync();
        
        // ✅ Commit only if we own the transaction
        if (ownsTransaction && transaction != null)
        {
            await transaction.CommitAsync();
        }

        _logger.LogInformation("Cash register transaction recorded: {Type} - {Amount}", type, amount);
    }
    catch (Exception ex)
    {
        // ✅ Rollback only if we own the transaction
        if (ownsTransaction && transaction != null)
        {
            await transaction.RollbackAsync();
        }
        _logger.LogError(ex, "Error recording cash register transaction");
        throw;
    }
    finally
    {
        if (ownsTransaction)
        {
            transaction?.Dispose();
        }
    }
}
```

**Key Points:**
1. ✅ Checks if already in a transaction (nested transaction support)
2. ✅ Reads balance INSIDE transaction (protected by lock)
3. ✅ Commits only if it owns the transaction
4. ✅ Rollback only if it owns the transaction

---

## 8. Summary: Current Concurrency Protection

### ✅ What's Protected

1. **Database-level isolation:** SQLite EXCLUSIVE lock prevents concurrent writes
2. **Transaction atomicity:** All-or-nothing commits
3. **Audit trail:** BalanceBefore/BalanceAfter captured
4. **Nested transaction support:** Cash register piggybacks on parent transaction

### ⚠️ What's NOT Protected (BUG)

1. **Lost updates:** Reading customer OUTSIDE transaction allows stale data
2. **Race condition:** Two threads can read same TotalDue, both calculate wrong balance

### 🔧 Required Fix

**Move customer read INSIDE transaction:**

```csharp
// BEFORE (current code)
var customer = await _unitOfWork.Customers.Query()...  // Outside transaction
await using var transaction = await _unitOfWork.BeginTransactionAsync();

// AFTER (fixed code)
await using var transaction = await _unitOfWork.BeginTransactionAsync();
var customer = await _unitOfWork.Customers.Query()...  // Inside transaction
```

---

## 9. Testing Concurrency

### Manual Test Script

```csharp
// Simulate concurrent payments
var tasks = new List<Task>();

for (int i = 0; i < 10; i++)
{
    tasks.Add(Task.Run(async () =>
    {
        await PayDebtAsync(customerId: 123, amount: 100, userId: i);
    }));
}

await Task.WhenAll(tasks);

// Verify: Customer.TotalDue should be reduced by 1000 (10 x 100)
```

### Expected Behavior (with fix)

- Initial TotalDue: 5000 ج.م
- 10 concurrent payments of 100 ج.م each
- Final TotalDue: 4000 ج.م ✅

### Actual Behavior (without fix)

- Final TotalDue: 4900 ج.م ❌ (lost 9 payments)

---

## 10. Recommendation

### CRITICAL: Apply Fix Before Production

**File:** `backend/KasserPro.Application/Services/Implementations/CustomerService.cs`

**Method:** `PayDebtAsync` (Line 280)

**Change Required:**

```diff
public async Task<ApiResponse<PayDebtResponse>> PayDebtAsync(int customerId, PayDebtRequest request, int recordedByUserId)
{
    var tenantId = _currentUser.TenantId;
    var branchId = _currentUser.BranchId;
    
    // Validate amount
    if (request.Amount <= 0)
        return ApiResponse<PayDebtResponse>.Fail("INVALID_AMOUNT", "المبلغ يجب أن يكون أكبر من صفر");
    
-   // Get customer
-   var customer = await _unitOfWork.Customers.Query()
-       .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);
-   
-   if (customer == null)
-       return ApiResponse<PayDebtResponse>.Fail("CUSTOMER_NOT_FOUND", "العميل غير موجود");
-   
-   // Validate payment amount doesn't exceed debt
-   if (request.Amount > customer.TotalDue)
-       return ApiResponse<PayDebtResponse>.Fail("AMOUNT_EXCEEDS_DEBT", 
-           $"المبلغ ({request.Amount:F2}) أكبر من الدين المستحق ({customer.TotalDue:F2})");
    
    // Get user for snapshot
    var user = await _unitOfWork.Users.GetByIdAsync(recordedByUserId);
    if (user == null)
        return ApiResponse<PayDebtResponse>.Fail("USER_NOT_FOUND", "المستخدم غير موجود");
    
    // Get current shift (optional)
    var currentShift = await _unitOfWork.Shifts.Query()
        .FirstOrDefaultAsync(s => s.TenantId == tenantId 
                                   && s.BranchId == branchId 
                                   && s.UserId == recordedByUserId
                                   && !s.IsClosed);
    
    // Use transaction for atomicity
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    
    try
    {
+       // ✅ FIX: Read customer INSIDE transaction with fresh data
+       var customer = await _unitOfWork.Customers.Query()
+           .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);
+       
+       if (customer == null)
+       {
+           await transaction.RollbackAsync();
+           return ApiResponse<PayDebtResponse>.Fail("CUSTOMER_NOT_FOUND", "العميل غير موجود");
+       }
+       
+       // ✅ FIX: Validate with fresh data
+       if (request.Amount > customer.TotalDue)
+       {
+           await transaction.RollbackAsync();
+           return ApiResponse<PayDebtResponse>.Fail("AMOUNT_EXCEEDS_DEBT", 
+               $"المبلغ ({request.Amount:F2}) أكبر من الدين المستحق ({customer.TotalDue:F2})");
+       }
        
        var balanceBefore = customer.TotalDue;
        var balanceAfter = balanceBefore - request.Amount;
        
        // ... rest of the method ...
    }
}
```

---

## Conclusion

The current implementation has a **race condition** that can cause **lost updates** in concurrent scenarios. The fix is simple: **move the customer read inside the transaction**. This ensures that each thread reads fresh data protected by SQLite's EXCLUSIVE lock, preventing lost updates.

**Priority:** 🔴 CRITICAL - Must fix before production deployment.
