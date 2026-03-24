# LOGGING CONFIGURATION GUIDE
## KasserPro Production Logging

**Date:** 2026-02-14  
**Phase:** Phase 1 - Production Hardening  
**Status:** ✅ ACTIVE

---

## OVERVIEW

KasserPro uses Serilog for structured logging with file-based persistence. This document describes the logging configuration, log patterns, monitoring guidelines, and operational procedures.

---

## LOG SINKS

### 1. Console Sink (Development)

**Purpose:** Real-time debugging during development

**Configuration:**
```csharp
.WriteTo.Console(
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
```

**Format:**
```
[14:23:45 INF] Starting KasserPro API
[14:23:46 WRN] SQLite WAL mode not active
[14:23:47 ERR] Failed to process order: Invalid quantity
```

**When to use:** Development only, not production

---

### 2. Application Log Sink (Production)

**Purpose:** General application logging with 30-day retention

**Configuration:**
```csharp
.WriteTo.File(
    path: "logs/kasserpro-.log",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 30,
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
```

**File Pattern:**
- `logs/kasserpro-20260214.log` (today)
- `logs/kasserpro-20260213.log` (yesterday)
- `logs/kasserpro-20260115.log` (30 days ago, will be deleted tomorrow)

**Retention:** 30 days (automatic cleanup)

**Format:**
```
2026-02-14 14:23:45.123 [INF] [KasserPro.API.Program] Starting KasserPro API {}
2026-02-14 14:23:46.456 [WRN] [KasserPro.Infrastructure.Data.SqliteConfigurationService] ⚠ Failed to enable WAL mode, current mode: DELETE {"JournalMode":"DELETE"}
2026-02-14 14:23:47.789 [ERR] [KasserPro.Application.Services.OrderService] Failed to complete order {"OrderId":123,"UserId":5,"CorrelationId":"a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4"}
```

**Fields:**
- `Timestamp`: ISO 8601 with milliseconds
- `Level`: INF, WRN, ERR, FTL
- `SourceContext`: Namespace.ClassName
- `Message`: Log message
- `Properties`: Structured JSON properties
- `Exception`: Stack trace (if present)

---

### 3. Financial Audit Log Sink (Production)

**Purpose:** Financial operations audit trail with 90-day retention

**Configuration:**
```csharp
.WriteTo.Logger(lc => lc
    .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("AuditType"))
    .WriteTo.File(
        path: "logs/financial-audit-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"))
```

**File Pattern:**
- `logs/financial-audit-20260214.log` (today)
- `logs/financial-audit-20260213.log` (yesterday)
- `logs/financial-audit-20251116.log` (90 days ago, will be deleted tomorrow)

**Retention:** 90 days (automatic cleanup)

**Filter:** Only logs with `AuditType` property

**Format:**
```
2026-02-14 14:23:45.123 [INF] Order completed {"AuditType":"OrderComplete","OrderId":123,"TotalAmount":150.50,"PaymentMethod":"Cash","UserId":5,"TenantId":1,"BranchId":2,"CorrelationId":"a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4"}
2026-02-14 14:25:30.456 [INF] Refund processed {"AuditType":"Refund","OrderId":123,"RefundAmount":150.50,"Reason":"Customer request","UserId":5,"TenantId":1,"BranchId":2,"CorrelationId":"b4g6d9e0-f3c2-g5b7-d8e9-f0a1b2c3d4e5"}
2026-02-14 14:30:15.789 [INF] Cash register transaction {"AuditType":"CashRegister","TransactionType":"ShiftClose","Amount":1500.00,"ShiftId":45,"UserId":5,"TenantId":1,"BranchId":2,"CorrelationId":"c5h7e0f1-g4d3-h6c8-e9f0-a1b2c3d4e5f6"}
```

**Audit Types:**
- `OrderComplete`: Order finalized
- `Refund`: Refund processed
- `CashRegister`: Cash register transaction
- `ShiftClose`: Shift closed
- `ShiftOpen`: Shift opened

---

## CORRELATION IDS

### Purpose
Trace a single request through the entire system (middleware → service → repository → database)

### Implementation
**CorrelationIdMiddleware:**
```csharp
public class CorrelationIdMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString();
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("X-Correlation-Id", correlationId);
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

### Usage

**Client receives correlation ID in response:**
```http
HTTP/1.1 200 OK
X-Correlation-Id: a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4
Content-Type: application/json
```

**All logs for that request include correlation ID:**
```
2026-02-14 14:23:45.123 [INF] [KasserPro.API.Controllers.OrdersController] Creating order {"CorrelationId":"a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4"}
2026-02-14 14:23:45.456 [INF] [KasserPro.Application.Services.OrderService] Validating order items {"CorrelationId":"a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4"}
2026-02-14 14:23:45.789 [ERR] [KasserPro.Application.Services.OrderService] Product not found {"ProductId":999,"CorrelationId":"a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4"}
```

**Trace entire request:**
```bash
grep "a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4" logs/kasserpro-*.log
```

---

## LOG LEVELS

### Information (INF)
**When to use:** Normal operations, successful actions

**Examples:**
- Application startup
- SQLite configuration applied
- Order completed
- Shift opened/closed
- Backup created

**Code:**
```csharp
_logger.LogInformation("Order completed {OrderId}", orderId);
```

---

### Warning (WRN)
**When to use:** Unexpected but recoverable situations

**Examples:**
- SQLite WAL mode not active
- Product out of stock
- Concurrency conflict (retry possible)
- Old backups deleted

**Code:**
```csharp
_logger.LogWarning("Product out of stock {ProductId}", productId);
```

---

### Error (ERR)
**When to use:** Failed operations, business logic errors

**Examples:**
- Order validation failed
- Payment processing failed
- SQLite BUSY error
- Backup integrity check failed

**Code:**
```csharp
_logger.LogError(ex, "Failed to complete order {OrderId}", orderId);
```

---

### Critical (FTL)
**When to use:** System-level failures requiring immediate attention

**Examples:**
- Disk full (SQLITE_FULL)
- Database corruption (SQLITE_CORRUPT)
- IO errors (disk permission issues)
- Application startup failure

**Code:**
```csharp
_logger.LogCritical(ex, "Disk full - cannot write to database");
```

---

## MONITORING GUIDELINES

### Daily Checks

**1. Check for CRITICAL errors:**
```bash
grep "\[FTL\]" logs/kasserpro-$(date +%Y%m%d).log
```

**Expected:** No results (critical errors require immediate action)

---

**2. Check disk space:**
```bash
df -h .
```

**Expected:** > 5 GB free

---

**3. Check log file sizes:**
```bash
ls -lh logs/
```

**Expected:** 
- Application logs: ~10-50 MB/day
- Financial audit logs: ~1-5 MB/day

---

### Weekly Checks

**1. Review SQLite errors:**
```bash
grep "SQLite error" logs/kasserpro-*.log | tail -20
```

**Expected:** Rare BUSY/LOCKED errors (< 10/day), no CORRUPT/FULL errors

---

**2. Review security violations:**
```bash
grep "SECURITY:" logs/kasserpro-*.log | tail -20
```

**Expected:** No branch tampering, no role escalation attempts

---

**3. Verify log rotation:**
```bash
ls -lt logs/ | head -10
```

**Expected:** Daily log files, oldest file ~30 days old (application), ~90 days old (audit)

---

### Monthly Checks

**1. Verify retention policies:**
```bash
# Application logs should not exceed 30 days
find logs/ -name "kasserpro-*.log" -mtime +30

# Financial audit logs should not exceed 90 days
find logs/ -name "financial-audit-*.log" -mtime +90
```

**Expected:** No results (Serilog auto-deletes old files)

---

**2. Review disk space trends:**
```bash
du -sh logs/
```

**Expected:** < 2 GB total

---

## TROUBLESHOOTING SCENARIOS

### Scenario 1: User reports "System busy" error

**Step 1: Get correlation ID from user**
```
User sees: "النظام مشغول، حاول مرة أخرى بعد لحظات"
Response includes: X-Correlation-Id: a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4
```

**Step 2: Search logs**
```bash
grep "a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4" logs/kasserpro-*.log
```

**Step 3: Identify root cause**
```
2026-02-14 14:23:45.123 [ERR] [KasserPro.API.Middleware.ExceptionMiddleware] SQLite error 5: database is locked [CorrelationId: a3f5c8d9-e2b1-f4a6-c7d8-e9f0a1b2c3d4]
```

**Step 4: Check for concurrent operations**
```bash
grep "SQLITE_BUSY\|SQLITE_LOCKED" logs/kasserpro-$(date +%Y%m%d).log | wc -l
```

**Resolution:**
- If < 10/day: Normal, user should retry
- If > 50/day: Investigate long-running transactions, consider increasing busy_timeout

---

### Scenario 2: Order not completed, user claims payment made

**Step 1: Get order ID or timestamp**
```
User: "Order at 2:30 PM didn't complete"
```

**Step 2: Search financial audit logs**
```bash
grep "14:3" logs/financial-audit-$(date +%Y%m%d).log
```

**Step 3: Check for OrderComplete audit entry**
```
2026-02-14 14:30:15.789 [INF] Order completed {"AuditType":"OrderComplete","OrderId":123,"TotalAmount":150.50,"PaymentMethod":"Cash","UserId":5,"TenantId":1,"BranchId":2,"CorrelationId":"c5h7e0f1-g4d3-h6c8-e9f0-a1b2c3d4e5f6"}
```

**Step 4: Trace full request**
```bash
grep "c5h7e0f1-g4d3-h6c8-e9f0-a1b2c3d4e5f6" logs/kasserpro-*.log
```

**Resolution:**
- If OrderComplete found: Order succeeded, check database
- If OrderComplete not found: Order failed, check error logs for correlation ID

---

### Scenario 3: Disk full warning

**Step 1: Check critical logs**
```bash
grep "\[FTL\]" logs/kasserpro-$(date +%Y%m%d).log
```

**Expected:**
```
2026-02-14 14:23:45.123 [FTL] [KasserPro.API.Middleware.ExceptionMiddleware] IO Error - possible disk full or permission issue [CorrelationId: ...]
```

**Step 2: Check disk space**
```bash
df -h .
```

**Step 3: Free up space**
```bash
# Delete old backups (keep last 14)
ls -t backups/*.db | tail -n +15 | xargs rm

# Delete old logs manually (if needed)
find logs/ -name "*.log" -mtime +90 -delete
```

**Step 4: Restart API**
```bash
dotnet run --project src/KasserPro.API
```

---

## LOG PATTERNS

### Successful Order Flow
```
[INF] Creating order {"CorrelationId":"..."}
[INF] Validating order items {"CorrelationId":"..."}
[INF] Calculating totals {"CorrelationId":"..."}
[INF] Saving order {"CorrelationId":"..."}
[INF] Order completed {"AuditType":"OrderComplete","OrderId":123,"CorrelationId":"..."}
```

---

### Failed Order Flow
```
[INF] Creating order {"CorrelationId":"..."}
[INF] Validating order items {"CorrelationId":"..."}
[ERR] Product not found {"ProductId":999,"CorrelationId":"..."}
```

---

### SQLite BUSY Error
```
[ERR] SQLite error 5: database is locked [CorrelationId: ...]
```

---

### Branch Tampering Attempt
```
[WRN] SECURITY: Branch access denied {"UserId":5,"RequestedBranchId":3,"AuthorizedBranchId":2,"CorrelationId":"..."}
```

---

### Role Escalation Attempt
```
[WRN] SECURITY: Role escalation attempt {"UserId":5,"CurrentRole":"Admin","RequestedRole":"SystemOwner","CorrelationId":"..."}
```

---

### Token Invalidation
```
[WRN] JWT validation failed: TOKEN_INVALIDATED {"UserId":5,"CorrelationId":"..."}
```

---

## BEST PRACTICES

### 1. Always include correlation ID
```csharp
// ✅ Good
_logger.LogInformation("Order completed {OrderId}", orderId);

// ❌ Bad (correlation ID missing - but it's auto-added by middleware)
_logger.LogInformation("Order completed");
```

---

### 2. Use structured logging
```csharp
// ✅ Good
_logger.LogInformation("Order completed {OrderId} {TotalAmount}", orderId, totalAmount);

// ❌ Bad (string interpolation loses structure)
_logger.LogInformation($"Order {orderId} completed with total {totalAmount}");
```

---

### 3. Include context in financial logs
```csharp
// ✅ Good
_logger.LogInformation("Order completed {OrderId} {TotalAmount} {PaymentMethod} {UserId} {TenantId} {BranchId}", 
    orderId, totalAmount, paymentMethod, userId, tenantId, branchId);

// ❌ Bad (missing context)
_logger.LogInformation("Order completed {OrderId}", orderId);
```

---

### 4. Add AuditType for financial operations
```csharp
// ✅ Good
using (_logger.BeginScope(new Dictionary<string, object> { ["AuditType"] = "OrderComplete" }))
{
    _logger.LogInformation("Order completed {OrderId} {TotalAmount}", orderId, totalAmount);
}

// ❌ Bad (won't appear in financial audit logs)
_logger.LogInformation("Order completed {OrderId} {TotalAmount}", orderId, totalAmount);
```

---

### 5. Log exceptions with full details
```csharp
// ✅ Good
_logger.LogError(ex, "Failed to complete order {OrderId}", orderId);

// ❌ Bad (loses stack trace)
_logger.LogError("Failed to complete order {OrderId}: {Message}", orderId, ex.Message);
```

---

## CONFIGURATION REFERENCE

**File:** `src/KasserPro.API/Program.cs`

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/kasserpro-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("AuditType"))
        .WriteTo.File(
            path: "logs/financial-audit-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 90,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"))
    .CreateLogger();
```

---

**Document Version:** 1.0  
**Last Updated:** 2026-02-14  
**Status:** ✅ PRODUCTION READY
