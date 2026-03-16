# Transaction Management Best Practices - KasserPro

## Quick Reference for Developers

This guide explains how to properly use transactions in KasserPro to avoid nested transaction errors.

---

## Rule #1: One Transaction Per Request

✅ **DO**: Start a transaction at the top-level service method
```csharp
public async Task<ApiResponse<OrderDto>> CompleteAsync(int orderId, CompleteOrderRequest request)
{
    IDbContextTransaction? transaction = null;
    var ownsTransaction = false;

    try
    {
        transaction = await _unitOfWork.BeginTransactionAsync();
        ownsTransaction = _unitOfWork.CurrentTransaction == transaction;
        
        // ... business logic ...
        
        if (ownsTransaction && transaction != null)
        {
            await transaction.CommitAsync();
        }
    }
    finally
    {
        if (ownsTransaction && transaction != null)
        {
            await transaction.DisposeAsync();
        }
    }
}
```

❌ **DON'T**: Use `await using var` - it doesn't handle early returns properly
```csharp
// BAD - transaction not disposed on early return
await using var transaction = await _unitOfWork.BeginTransactionAsync();
if (someCondition)
    return ApiResponse.Fail(...); // Transaction still active!
```

---

## Rule #2: Sub-Services Must Check for Active Transaction

✅ **DO**: Check if transaction is already active before creating new one
```csharp
public async Task RecordTransactionAsync(...)
{
    var ownsTransaction = !_unitOfWork.HasActiveTransaction;
    IDbContextTransaction? transaction = null;
    
    if (ownsTransaction)
    {
        transaction = await _unitOfWork.BeginTransactionAsync();
    }
    
    try
    {
        // ... business logic ...
        
        if (ownsTransaction && transaction != null)
        {
            await transaction.CommitAsync();
        }
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

❌ **DON'T**: Always create a new transaction
```csharp
// BAD - creates nested transaction
await using var transaction = await _unitOfWork.BeginTransactionAsync();
```

---

## Rule #3: Always Use try-finally for Cleanup

✅ **DO**: Ensure transaction disposal in finally block
```csharp
IDbContextTransaction? transaction = null;
var ownsTransaction = false;

try
{
    transaction = await _unitOfWork.BeginTransactionAsync();
    ownsTransaction = _unitOfWork.CurrentTransaction == transaction;
    
    // Business logic
}
catch (Exception ex)
{
    if (ownsTransaction && transaction != null)
    {
        await transaction.RollbackAsync();
    }
    throw;
}
finally
{
    if (ownsTransaction && transaction != null)
    {
        await transaction.DisposeAsync();
    }
}
```

❌ **DON'T**: Rely on automatic disposal
```csharp
// BAD - early returns skip disposal
var transaction = await _unitOfWork.BeginTransactionAsync();
if (error) return; // Transaction not disposed!
```

---

## Rule #4: Don't Rollback Before Early Returns

✅ **DO**: Let the finally block handle disposal
```csharp
if (validationFailed)
{
    return ApiResponse.Fail(...); // finally block will dispose
}
```

❌ **DON'T**: Manually rollback before return
```csharp
// BAD - unnecessary and can cause issues
if (validationFailed)
{
    await transaction.RollbackAsync();
    return ApiResponse.Fail(...);
}
```

**Why?** The finally block will dispose the transaction, which automatically rolls back uncommitted transactions.

---

## Rule #5: Use ownsTransaction Flag

✅ **DO**: Track transaction ownership
```csharp
var ownsTransaction = _unitOfWork.CurrentTransaction == transaction;

if (ownsTransaction && transaction != null)
{
    await transaction.CommitAsync();
}
```

❌ **DON'T**: Always commit/rollback
```csharp
// BAD - sub-service shouldn't commit parent's transaction
await transaction.CommitAsync();
```

---

## Common Patterns

### Pattern 1: Top-Level Service with Transaction

```csharp
public async Task<ApiResponse<T>> ProcessAsync(...)
{
    IDbContextTransaction? transaction = null;
    var ownsTransaction = false;

    try
    {
        transaction = await _unitOfWork.BeginTransactionAsync();
        ownsTransaction = _unitOfWork.CurrentTransaction == transaction;
        
        // Call sub-services (they will participate in this transaction)
        await _subService1.DoWorkAsync(...);
        await _subService2.DoWorkAsync(...);
        
        if (ownsTransaction && transaction != null)
        {
            await transaction.CommitAsync();
        }
        
        return ApiResponse<T>.Ok(...);
    }
    catch (Exception ex)
    {
        if (ownsTransaction && transaction != null)
        {
            await transaction.RollbackAsync();
        }
        return ApiResponse<T>.Fail(...);
    }
    finally
    {
        if (ownsTransaction && transaction != null)
        {
            await transaction.DisposeAsync();
        }
    }
}
```

### Pattern 2: Sub-Service That May Be Called Independently

```csharp
public async Task DoWorkAsync(...)
{
    var ownsTransaction = !_unitOfWork.HasActiveTransaction;
    IDbContextTransaction? transaction = null;
    
    if (ownsTransaction)
    {
        transaction = await _unitOfWork.BeginTransactionAsync();
    }
    
    try
    {
        // Business logic
        await _unitOfWork.SaveChangesAsync();
        
        if (ownsTransaction && transaction != null)
        {
            await transaction.CommitAsync();
        }
    }
    catch (Exception ex)
    {
        if (ownsTransaction && transaction != null)
        {
            await transaction.RollbackAsync();
        }
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

### Pattern 3: Read-Only Operations (No Transaction Needed)

```csharp
public async Task<T> GetDataAsync(...)
{
    // No transaction needed for read-only operations
    return await _unitOfWork.SomeRepository.Query()
        .Where(...)
        .FirstOrDefaultAsync();
}
```

---

## Debugging Transaction Issues

### Check Active Transaction
```csharp
var hasTransaction = _unitOfWork.HasActiveTransaction;
var currentTransaction = _unitOfWork.CurrentTransaction;
_logger.LogInformation("Transaction active: {HasTransaction}, ID: {TransactionId}", 
    hasTransaction, currentTransaction?.TransactionId);
```

### Log Transaction Lifecycle
```csharp
_logger.LogInformation("Beginning transaction");
transaction = await _unitOfWork.BeginTransactionAsync();

_logger.LogInformation("Committing transaction");
await transaction.CommitAsync();

_logger.LogInformation("Transaction disposed");
```

---

## Testing Transaction Behavior

### Unit Test: Verify Transaction Participation
```csharp
[Fact]
public async Task SubService_ShouldParticipateInExistingTransaction()
{
    // Arrange
    var transaction = await _unitOfWork.BeginTransactionAsync();
    
    // Act
    await _subService.DoWorkAsync();
    
    // Assert
    Assert.True(_unitOfWork.HasActiveTransaction);
    Assert.Equal(transaction, _unitOfWork.CurrentTransaction);
}
```

### Integration Test: Verify Rollback on Error
```csharp
[Fact]
public async Task CompleteOrder_ShouldRollbackOnError()
{
    // Arrange
    var orderId = 123;
    
    // Act
    var result = await _orderService.CompleteAsync(orderId, invalidRequest);
    
    // Assert
    Assert.False(result.Success);
    Assert.False(_unitOfWork.HasActiveTransaction); // Transaction cleaned up
}
```

---

## Performance Considerations

### Transaction Duration
- Keep transactions as short as possible
- Don't perform I/O operations inside transactions
- Don't call external APIs inside transactions

### Good Practice
```csharp
// Prepare data outside transaction
var externalData = await _externalApi.GetDataAsync();

// Start transaction only for database operations
var transaction = await _unitOfWork.BeginTransactionAsync();
try
{
    // Fast database operations only
    await _unitOfWork.SaveChangesAsync();
    await transaction.CommitAsync();
}
finally
{
    await transaction.DisposeAsync();
}
```

### Bad Practice
```csharp
var transaction = await _unitOfWork.BeginTransactionAsync();
try
{
    // BAD - external API call inside transaction
    var externalData = await _externalApi.GetDataAsync();
    await _unitOfWork.SaveChangesAsync();
    await transaction.CommitAsync();
}
finally
{
    await transaction.DisposeAsync();
}
```

---

## Checklist for New Code

Before committing code that uses transactions:

- [ ] Transaction started at appropriate level (top-level service)
- [ ] Sub-services check `HasActiveTransaction` before creating transaction
- [ ] `ownsTransaction` flag used to track ownership
- [ ] `try-finally` block ensures disposal
- [ ] No manual rollback before early returns
- [ ] Transaction duration minimized
- [ ] No external I/O inside transaction
- [ ] Logging added for debugging
- [ ] Unit tests verify transaction behavior

---

## Common Mistakes to Avoid

### Mistake 1: Forgetting to Check HasActiveTransaction
```csharp
// BAD
public async Task SubServiceMethod()
{
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    // This creates nested transaction!
}
```

### Mistake 2: Not Disposing Transaction
```csharp
// BAD
var transaction = await _unitOfWork.BeginTransactionAsync();
if (error) return; // Transaction not disposed!
```

### Mistake 3: Sub-Service Committing Parent Transaction
```csharp
// BAD
public async Task SubServiceMethod()
{
    // This commits the parent's transaction!
    await _unitOfWork.CurrentTransaction.CommitAsync();
}
```

### Mistake 4: Long-Running Transactions
```csharp
// BAD
var transaction = await _unitOfWork.BeginTransactionAsync();
await Task.Delay(5000); // Holding transaction for 5 seconds!
await _unitOfWork.SaveChangesAsync();
await transaction.CommitAsync();
```

---

## Questions?

If you're unsure about transaction usage:
1. Check existing patterns in `OrderService.cs` and `CashRegisterService.cs`
2. Review `TRANSACTION_MANAGEMENT_FIX.md` for detailed explanation
3. Ask the team lead for code review

---

## Summary

**Golden Rules:**
1. One transaction per request
2. Sub-services check `HasActiveTransaction`
3. Always use `try-finally` for cleanup
4. Track ownership with `ownsTransaction` flag
5. Keep transactions short

Follow these patterns and you'll never see "already in a transaction" errors again!
