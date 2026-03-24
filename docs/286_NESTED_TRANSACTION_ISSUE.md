# Nested Transaction Issue - Backend

## Problem
Some order completions fail with error:
```
SYSTEM_INTERNAL_ERROR: The connection is already in a transaction and cannot participate in another transaction.
```

## Root Cause
SQLite does not support true nested transactions. When `OrderService.CompleteAsync` starts a transaction and then calls `CashRegisterService.RecordTransactionAsync`, the latter tries to check if a transaction is active using `HasActiveTransaction`, but this check may fail in certain scenarios.

## Current Code Flow
1. `OrderService.CompleteAsync` starts transaction
2. Processes order, payments, stock, customer updates
3. Calls `CashRegisterService.RecordTransactionAsync`
4. `RecordTransactionAsync` checks `HasActiveTransaction`
5. If check fails, it tries to start a new transaction → ERROR

## Why Some Orders Succeed and Others Fail
- Orders with cash payment: Call `RecordTransactionAsync` → May fail
- Orders without cash payment: Skip `RecordTransactionAsync` → Succeed
- The `HasActiveTransaction` check works most of the time, but fails intermittently

## Solution Options

### Option 1: Restart Backend (Recommended)
The simplest solution is to restart the backend to clear any stuck transactions.

```powershell
# Stop the backend process
# Restart it
cd backend/KasserPro.API
dotnet run
```

### Option 2: Fix HasActiveTransaction Check (Code Fix)
The issue might be that `_context.Database.CurrentTransaction` doesn't always reflect the active transaction state correctly in SQLite.

**Fix in `CashRegisterService.cs`:**
Instead of relying on `HasActiveTransaction`, pass a flag from the caller:

```csharp
// In OrderService.CompleteAsync
await _cashRegisterService.RecordTransactionAsync(
    type: CashRegisterTransactionType.Sale,
    amount: cashPaymentAmount,
    description: $"مبيعات - طلب #{order.OrderNumber}",
    referenceType: "Order",
    referenceId: order.Id,
    shiftId: order.ShiftId,
    useExistingTransaction: true  // NEW PARAMETER
);
```

### Option 3: Remove Transaction from RecordTransactionAsync
Since `RecordTransactionAsync` is always called from within another transaction in `CompleteAsync`, we can remove its own transaction logic for this specific case.

## Immediate Workaround
For now, users can:
1. Complete orders with full cash payment (no credit) - these should work
2. Avoid credit sales until backend is restarted
3. If an order fails, try again - it may succeed on retry

## Files Involved
- `backend/KasserPro.Application/Services/Implementations/OrderService.cs` (line 714)
- `backend/KasserPro.Application/Services/Implementations/CashRegisterService.cs` (line 431-500)
- `backend/KasserPro.Infrastructure/Repositories/UnitOfWork.cs` (line 97)

## Next Steps
1. ✅ Frontend error display is now working correctly
2. 🔧 Need to restart backend OR apply code fix
3. 🧪 Test credit sales after backend restart
