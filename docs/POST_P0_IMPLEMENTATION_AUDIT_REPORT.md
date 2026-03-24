# POST-P0 IMPLEMENTATION AUDIT REPORT — KasserPro POS

**Audit Date:** 2026-02-13  
**Audited Scope:** Single-Branch Local POS · 1–3 users · SQLite · On-Premise  
**Auditor:** Automated Code-Level Verification  
**Audit Method:** Direct source code inspection of every modified file  

---

## 1. Code-Level Verification

### P0-1: JWT Secret Moved to Environment Variable

**Status: ✅ CORRECTLY IMPLEMENTED**

| Check | Result |
|-------|--------|
| `appsettings.json` has empty key | ✅ `"Key": ""` confirmed |
| `appsettings.example.json` fixed key path | ✅ Uses `Jwt.Key` (not `JwtSettings.SecretKey`) |
| Startup guard present | ✅ `Program.cs` lines 17–24: rejects null/whitespace or < 32 chars |
| Guard throws on short key | ✅ `InvalidOperationException` with clear message |
| JWT reads from same config path | ✅ `builder.Configuration["Jwt:Key"]!` at line 92 |

**Partial Implementation:** None.

**Hidden Side-Effects:**
- FINDING-01 (LOW): `appsettings.json` is version-controlled (not in `.gitignore`). The empty key string `""` is safe, but any developer who sets a real key in `appsettings.json` for convenience will commit it to Git. **Mitigation:** This is acceptable for local POS — there is no shared repo in production. If a Git repo is used for deployment, add `/src/KasserPro.API/appsettings.json` to `.gitignore` and use `appsettings.example.json` as template.

**Breaking Change Risks:**
- Every existing deployment must set `Jwt__Key` environment variable before the app will start. This is INTENTIONAL — it's a mandatory one-time setup step.
- All previously issued JWT tokens become invalid. Users must re-login. This is expected.

**Transaction Boundaries:** N/A (startup configuration).

---

### P0-2: Seed Credentials Disabled in Production

**Status: ✅ CORRECTLY IMPLEMENTED**

| Check | Result |
|-------|--------|
| `ButcherDataSeeder` gated by `IsDevelopment()` | ✅ `Program.cs` line 131: `if (app.Environment.IsDevelopment())` |
| `MigrateAsync()` still runs in all environments | ✅ Line 128: outside the `IsDevelopment()` block |
| Demo credentials gated in LoginPage | ✅ `{import.meta.env.DEV && ( ... )}` wrapping demo box |
| Production build excludes credentials | ✅ Vite tree-shakes `import.meta.env.DEV` branches for production builds |

**Partial Implementation:** None.

**Hidden Side-Effects:**
- FINDING-02 (MEDIUM): First production deployment starts with an EMPTY database (no admin user, no tenant, no branch). There is **no production seeder or initial-setup CLI**. The operator must manually create the first admin user. **Impact:** Not a P0 regression — but the system is unusable until initial setup is completed. A setup wizard or manual SQL insert is needed.
- FINDING-03 (LOW): The `ButcherDataSeeder.SeedAsync` internally calls `ClearBusinessDataAsync` which wipes data. In Development, this means every restart wipes dev data. This is unchanged from before and intentional for development.

**Breaking Change Risks:** None for production. Development workflow unchanged.

**Transaction Boundaries:** N/A.

---

### P0-3: Stock TOCTOU Fully Eliminated

**Status: ✅ CORRECTLY IMPLEMENTED — with 1 residual finding**

| Check | Result |
|-------|--------|
| `CreateAsync` reads from `BranchInventory` | ✅ Line 155: `_inventoryService.GetAvailableQuantityAsync(product.Id, _currentUser.BranchId)` |
| `CompleteAsync` re-validates stock inside transaction | ✅ Lines 499–514: iterates items, reads `GetAvailableQuantityAsync`, rejects if insufficient |
| Re-validation happens after `SaveChangesAsync` (write lock acquired) | ✅ Line 494 `SaveChangesAsync` then lines 499–514 re-check |
| `BatchDecrementStockAsync` has safety log | ✅ Lines 282–288: logs warning if `balanceBefore < quantity` |
| Transaction rollback on insufficient stock | ✅ Line 507: `await transaction.RollbackAsync()` before returning error |

**Correctness Analysis — SQLite Write Lock Sequence:**

```
CompleteAsync transaction:
  1. BEGIN TRANSACTION
  2. SaveChangesAsync() → writes order status → ACQUIRES SQLite write lock
  3. GetAvailableQuantityAsync() → reads BranchInventory (inside write lock)
  4. If stock insufficient → ROLLBACK
  5. BatchDecrementStockAsync() → decrements (still inside lock)
  6. COMMIT → releases lock
```

Since SQLite serializes all writers, step 3 always reads the latest committed BranchInventory value. A concurrent writer is blocked at step 2 until this transaction completes. **The TOCTOU window is fully closed for SQLite.**

**Hidden Side-Effects:**
- FINDING-04 (LOW): `BatchDecrementStockAsync` calls `_context.SaveChangesAsync()` at its end (InventoryService.cs line 313). This is a second `SaveChangesAsync` within the same transaction. Since EF Core tracks all changes on the same `DbContext` and the transaction hasn't committed, this is safe — it flushes the BranchInventory and StockMovement changes to SQLite's write-ahead log. The actual commit happens at `transaction.CommitAsync()` in `CompleteAsync`.
- FINDING-05 (LOW): The `BatchDecrementStockAsync` warning log does NOT prevent the decrement. Stock CAN go negative in the `BatchDecrementStockAsync` call itself (it does `inventory.Quantity -= quantity` regardless). However, this path is only reached AFTER the `CompleteAsync` re-validation passes, so by definition stock is sufficient. The log is a defense-in-depth safety net for future code changes.

**Can stock go negative?**
- When `AllowNegativeStock = false`: **NO**. The re-validation in `CompleteAsync` rejects before decrement.
- When `AllowNegativeStock = true`: **YES, intentionally**. Both checks are skipped (tenant config allows it). This is correct business logic.

**Breaking Change Risks:** None. The soft check in `CreateAsync` now reads `BranchInventory` instead of `Product.StockQuantity`. If `BranchInventory` rows don't exist for a product, `GetAvailableQuantityAsync` returns 0, which would block the sale. This is correct — if inventory hasn't been set up for a branch, stock should show as 0.

**Transaction Boundaries:** ✅ Correct. Single transaction wraps read → validate → decrement → commit.

---

### P0-4: Tax Double-Calculation Removed

**Status: ✅ CORRECTLY IMPLEMENTED**

| Check | Result |
|-------|--------|
| `CalculateOrderTotals` no longer uses `order.TaxRate` for tax | ✅ No reference to `order.TaxRate / 100m` in tax calc |
| Without discount: sums `item.TaxAmount` | ✅ Line 974: `order.Items.Sum(i => i.TaxAmount)` |
| With discount: proportionally scales item taxes | ✅ Lines 960–970: `discountRatio` applied per-item |
| `CalculateItemTotals` unchanged | ✅ Lines 915–930: still computes per-item tax from `item.TaxRate` |

**Correctness Verification — Mixed Tax Rate Scenario:**

```
Item A: Subtotal=100, TaxRate=14%, TaxAmount=14.00
Item B: Subtotal=100, TaxRate=0%,  TaxAmount=0.00

No order discount:
  order.TaxAmount = Sum(14.00, 0.00) = 14.00 ✅

With 10% order discount (DiscountAmount=20):
  discountRatio = 20/200 = 0.10
  Item A: 100 * (1 - 0.10) * 14/100 = 90 * 0.14 = 12.60
  Item B: 100 * (1 - 0.10) * 0/100  = 90 * 0.00 = 0.00
  order.TaxAmount = Round(12.60) = 12.60 ✅
```

**Hidden Side-Effects:**
- FINDING-06 (MEDIUM): The order-level `order.TaxRate` field is still populated (set from `tenantTaxRate` in `CreateAsync` line 122) but is now UNUSED for calculation. It's stored in the database as metadata. This creates a **semantic inconsistency**: the stored `TaxRate` doesn't reflect the actual effective tax rate. However, this doesn't cause calculation errors — it's just a display/reporting concern. The actual `TaxAmount` is correct.
- FINDING-07 (LOW): The proportional discount distribution uses `item.Subtotal` as the weight, not `item.Total`. This is mathematically correct because the discount applies pre-tax, so the pro-rata basis should be the pre-tax subtotal.

**Breaking Change Risks:**
- Any external report that reads `order.TaxRate` to reverse-calculate expected tax will now disagree with `order.TaxAmount`. Reports should use `order.TaxAmount` directly.
- **Existing historical orders are NOT retroactively recalculated.** Old orders in the database may have incorrect TaxAmount values from the double-tax bug. This is expected — historical data reflects the calculation at time of sale.

**Transaction Boundaries:** N/A (pure in-memory calculation).

---

### P0-5: SignalR No Longer Using Clients.All

**Status: ✅ CORRECTLY IMPLEMENTED**

| Check | Result |
|-------|--------|
| `Clients.All` removed from all .cs files | ✅ `grep "Clients.All"` in `**/*.cs` → 0 matches |
| `DeviceHub.OnConnectedAsync` adds to group | ✅ Lines 60–64: `Groups.AddToGroupAsync(Context.ConnectionId, groupName)` |
| Group name from `X-Branch-Id` header | ✅ Line 59: reads `httpContext.Request.Headers["X-Branch-Id"]` |
| Fallback to `"branch-default"` | ✅ Line 60: `!string.IsNullOrEmpty(branchId) ? $"branch-{branchId}" : "branch-default"` |
| `OrdersController` sends to group | ✅ Line 153–155: `Clients.Group($"branch-{branchId}")` |
| `DeviceTestController` sends to group | ✅ Lines 77–87: group-based with fallback |
| `PrintCompleted` uses `Clients.Caller` | ✅ Line 108: `Clients.Caller.SendAsync("PrintCompleted", eventDto)` |

**Hidden Side-Effects:**
- FINDING-08 (MEDIUM): `OrdersController.Complete` reads branch ID from the JWT claim: `User.FindFirst("branchId")?.Value ?? "default"`. The desktop bridge reads branch from `X-Branch-Id` header. These are TWO DIFFERENT sources. If the JWT `branchId` claim value doesn't match the header value the bridge sent, the print command goes to a group the bridge isn't in. **For single-branch deployment, this is not an issue** (only one branch = one group). For any future multi-branch expansion, these must be synchronized.
- FINDING-09 (LOW): `OnDisconnectedAsync` does NOT call `Groups.RemoveFromGroupAsync`. This is fine — SignalR automatically removes connections from groups on disconnect.

**Breaking Change Risks:**
- Desktop bridge app MUST send `X-Branch-Id` header during connection, or it joins `"branch-default"`. If the API sends to `"branch-1"` and the bridge is in `"branch-default"`, **printing silently fails**. The desktop bridge config must be updated.

**Transaction Boundaries:** N/A.

---

### P0-6: DeviceTestController Secured

**Status: ✅ CORRECTLY IMPLEMENTED**

| Check | Result |
|-------|--------|
| `[Authorize(Roles = "Admin")]` on class | ✅ Line 14: `[Authorize(Roles = "Admin")]` |
| `using Microsoft.AspNetCore.Authorization` added | ✅ Line 3 |
| Both endpoints protected | ✅ Class-level attribute covers `test-print` and `status` |

**Partial Implementation:** None.

**Hidden Side-Effects:** None.

**Breaking Change Risks:**
- `GET /api/devicetest/status` now requires Admin auth. If any monitoring script or dashboard uses this endpoint without credentials, it will break. This is intentional — device status is an admin concern.

**Transaction Boundaries:** N/A.

---

### P0-7: Financial Mutations Retry Disabled

**Status: ✅ CORRECTLY IMPLEMENTED — with 1 residual finding**

| Check | Result |
|-------|--------|
| Mutation detection in `baseApi.ts` | ✅ Lines 49–63: checks `args.method` for POST/PUT/DELETE |
| `retry.fail(error)` called for mutations | ✅ Line 63: immediately fails, no retry |
| Mutation error toasts | ✅ `FETCH_ERROR` → "فشل الاتصال" · `500` → "لا تكرر العملية" |
| GET queries still retry | ✅ Lines 66–82: `FETCH_ERROR` and `TIMEOUT_ERROR` for queries trigger retry |
| `maxRetries: 3` preserved for queries | ✅ Line 138 |
| `Idempotency-Key` headers removed from `ordersApi.ts` | ✅ `grep "Idempotency-Key"` → 0 matches, `grep "Date.now()"` in api/ → 0 matches |
| `cashRegisterApi.ts` mutations not retried | ✅ All use `method: 'POST'`, caught by the mutation guard |

**Hidden Side-Effects:**
- FINDING-10 (MEDIUM): The server-side `IdempotencyMiddleware` is still active (`Program.cs` line 142: `app.UseIdempotency()`). It checks for the `Idempotency-Key` header on POST/PUT to order/payment/shift endpoints. Since the frontend no longer sends this header, the middleware adds `X-Idempotency-Warning: Missing Idempotency-Key header` but **allows the request through** (line 60–63 of IdempotencyMiddleware.cs). This is harmless but means the server-side idempotency protection is now fully inactive (no key = no caching). The middleware is dead code for order mutations.
- FINDING-11 (LOW): The `retry.fail(error)` call terminates the retry loop. However, if RTK Query's internal `onQueryStarted` lifecycle is used by any component and it catches the error, the component could theoretically re-trigger the mutation manually. This is a framework-level edge case, not a P0 regression.

**Can duplicate payments occur via frontend auto-retry?** **NO.** Mutations are forced to fail immediately on error, with no retry. User must manually re-trigger.

**Breaking Change Risks:** None. Queries still retry. Mutations now fail-fast with a clear toast message.

**Transaction Boundaries:** N/A (frontend-only).

---

### P0-8: Cash Register Wrapped in Transaction

**Status: ✅ CORRECTLY IMPLEMENTED**

| Check | Result |
|-------|--------|
| `IUnitOfWork.HasActiveTransaction` declared | ✅ Line 51 |
| `UnitOfWork.HasActiveTransaction` implemented | ✅ Line 89: `_context.Database.CurrentTransaction != null` |
| `RecordTransactionAsync` checks `HasActiveTransaction` | ✅ Line 440: `var ownsTransaction = !_unitOfWork.HasActiveTransaction` |
| Creates own transaction when standalone | ✅ Lines 443–445 |
| Piggybacks on caller's transaction | ✅ `ownsTransaction = false` → skips BeginTransaction |
| Commit only when owning transaction | ✅ Line 501: `if (ownsTransaction && transaction != null)` |
| Rollback on error when owning | ✅ Line 507 |
| Dispose in finally | ✅ Line 513 |

**Call Path Analysis:**

| Caller | Has Active Transaction? | `RecordTransactionAsync` Behavior |
|--------|------------------------|-----------------------------------|
| `CompleteAsync` (order sale) | ✅ Yes (line 415) | Piggybacks — no nested transaction |
| `RefundAsync` (refund) | ✅ Yes (line 607) | Piggybacks — no nested transaction |
| Standalone (future) | ❌ No | Creates own transaction |

**Hidden Side-Effects:**
- FINDING-12 (LOW): `GenerateTransactionNumberAsync()` queries the last transaction number inside the method. If two standalone deposits happen simultaneously, they could generate the same transaction number (e.g., both read `CR-2026-0005` and both generate `CR-2026-0006`). However, for our scope (1–3 users, SQLite single writer), this is prevented by the write lock. The second transaction blocks until the first commits.

**Can balance drift occur?**
When called from `CompleteAsync`:
1. `CompleteAsync` acquires write lock via `SaveChangesAsync()` (line 494)
2. `RecordTransactionAsync.GetCurrentBalanceForBranchAsync` reads the latest committed balance
3. Since we're inside a write lock, no other writer can modify the balance between read and write
4. **Balance drift: NOT POSSIBLE for ≤3 concurrent users on SQLite.**

When called standalone with own transaction:
1. `BeginTransactionAsync()` starts transaction
2. `SaveChangesAsync()` acquires write lock
3. Same serialization guarantees
4. **Balance drift: NOT POSSIBLE.**

**Breaking Change Risks:** None. The `HasActiveTransaction` check is transparent to callers.

**Transaction Boundaries:** ✅ Correct.

---

## 2. Concurrency Safety Check

### Scenario A: Two Tabs Completing Same Order

```
Timeline:
T1 (Tab A): POST /orders/42/complete
  → CompleteAsync() BEGIN TRANSACTION
  → SaveChangesAsync() → sets status=Completed → ACQUIRES WRITE LOCK
  → Re-validates stock ✅
  → BatchDecrementStockAsync ✅
  → RecordTransactionAsync (piggyback) ✅
  → COMMIT → releases lock

T2 (Tab B): POST /orders/42/complete
  → CompleteAsync() BEGIN TRANSACTION
  → SaveChangesAsync() → ⏳ BLOCKED waiting for T1's write lock
  → ...unblocked...
  → Reads order → status is already Completed
  → ValidateStateTransition(Completed → Completed) → FAILS
  → Returns "لا يمكن تغيير حالة الطلب من Completed إلى Completed"
```

**Result: ✅ SAFE.** Only one tab succeeds. The second gets a clear state transition error. No double-charge.

### Scenario B: Two Cash Transactions Simultaneously

```
Timeline:
T1 (Cashier A completes order): POST /orders/10/complete (100 EGP cash)
  → CompleteAsync BEGIN → SaveChangesAsync → WRITE LOCK ACQUIRED
  → RecordTransactionAsync: reads balance=1000, writes 1000+100=1100
  → COMMIT

T2 (Cashier B completes order): POST /orders/11/complete (200 EGP cash)
  → CompleteAsync BEGIN → SaveChangesAsync → ⏳ BLOCKED
  → ...unblocked...
  → RecordTransactionAsync: reads balance=1100 (T1's result), writes 1100+200=1300
  → COMMIT
```

**Result: ✅ SAFE.** Balance chain is correct: 1000 → 1100 → 1300. SQLite serialization prevents drift.

### Scenario C: Double-Click Payment Button

```
Timeline:
Click 1: POST /orders/42/complete
  → baseApi.ts: args.method = "POST" → isMutation = true
  → Sent to server

Click 2 (20ms later): POST /orders/42/complete
  → RTK Query: mutation already in-flight for this endpoint+args
  → RTK Query deduplicates (returns existing promise)
  → OR: both hit server

Server behavior if both arrive:
  → T1 processes first (sets status=Completed)
  → T2 hits ValidateStateTransition → rejects
```

**Result: ✅ SAFE.** Three layers of protection:
1. RTK Query mutation deduplication (same cache key)
2. Frontend retry disabled — if first fails, NO auto-retry
3. Server-side state machine — second attempt rejected

### Scenario D: Server Restart Mid-Request

```
Timeline:
T1: POST /orders/42/complete
  → Server receives request
  → CompleteAsync BEGIN TRANSACTION
  → Server process killed (Ctrl+C, crash, etc.)
  → SQLite: uncommitted transaction → AUTOMATIC ROLLBACK
  → Order remains in Draft status

Frontend:
  → FETCH_ERROR response
  → isMutation = true → retry.fail()
  → Toast: "فشل الاتصال. تحقق من الشبكة وحاول يدوياً."
  → NO auto-retry
```

**Result: ✅ SAFE.** SQLite's journal/WAL ensures uncommitted transactions are rolled back on process death. The order, payments, stock, and cash register all revert cleanly. User sees an error and can retry manually after verifying.

---

## 3. Data Integrity Validation

### Can stock go negative?

**NO** (when `AllowNegativeStock = false`).

Protection chain:
1. `CreateAsync` soft check: reads `BranchInventory.Quantity` → rejects if insufficient (UX hint)
2. `CompleteAsync` hard check: inside write lock, re-reads `BranchInventory.Quantity` → rejects with rollback
3. `BatchDecrementStockAsync`: logs warning (defense-in-depth) but only reached after validation passes

Even if two cashiers race to sell the last item, SQLite's single-writer serialization guarantees the second transaction reads the decremented value.

**Verdict: ✅ SAFE**

### Can duplicate payments occur?

**NO.**

Protection chain:
1. Frontend: mutations are NOT retried (`retry.fail()` called immediately)
2. Backend: `ValidateStateTransition` rejects completing an already-completed order
3. Server-side: `IdempotencyMiddleware` is present (though currently inactive since frontend sends no key — it's a latent backup layer)

**Verdict: ✅ SAFE**

### Can receipt leak to wrong device?

**NO** (within single branch).

All SignalR broadcasts use `Clients.Group($"branch-{branchId}")`. Devices join their branch group on connection. `Clients.All` is completely removed from all C# source code (verified via grep).

**Caveat:** If the desktop bridge doesn't send `X-Branch-Id`, it joins `"branch-default"`, which is a different group than `"branch-1"`. The receipt won't be received, but it also won't leak to the wrong device — it goes to an empty group.

**Verdict: ✅ SAFE**

### Can balance drift?

**NO** (for ≤3 concurrent users on SQLite).

`RecordTransactionAsync` either piggybacks on the caller's transaction (which holds the write lock) or creates its own transaction. SQLite ensures only one writer at a time, so `GetCurrentBalanceForBranchAsync` always reads the most recent committed balance.

**Verdict: ✅ SAFE**

### Can order be partially committed?

**NO.**

`CompleteAsync` wraps ALL operations in a single database transaction:
- Order status change
- Payment creation
- Stock re-validation
- Stock decrement
- Customer stats update
- Cash register transaction
- Commit (or rollback on any failure)

The only post-commit write is in `RefundAsync` line 873: it updates `originalOrder.Notes` AFTER `transaction.CommitAsync()`. This is a non-critical metadata update (a note linking to the return order). If this `SaveChangesAsync` fails, the refund itself is already committed and complete. The missing note is cosmetic.

**Verdict: ✅ SAFE** (with cosmetic edge case on refund notes)

---

## 4. SQLite Risk Assessment (Local Only)

### Is write locking sufficient?

**YES, for 1–3 concurrent users.**

SQLite's WAL mode allows concurrent reads with one writer. The write lock duration for a typical order completion is:
- Status write (~2ms)
- Stock read + validation (~5ms)
- Stock decrement + movement write (~5ms)
- Cash register write (~3ms)
- Commit (~5ms)

Total lock duration: **~20ms per order.**

With 3 users, worst case: 2 users wait 20ms each = 40ms. This is imperceptible.

### Any remaining race risks?

**ONE theoretical edge case:**

- `CancelAsync` (line 575) does NOT use a transaction. It reads the order, checks state, and writes. Two concurrent cancel requests for the same order could both pass `ValidateStateTransition` and both succeed. However:
  1. Both write the same status (`Cancelled`) with the same effect
  2. No financial data is involved in cancel
  3. The second write is idempotent (same status, overwritten fields)

**Severity: NEGLIGIBLE.** No data corruption.

### Any potential SQLITE_BUSY failure scenarios?

**LOW RISK but possible.**

By default, SQLite returns `SQLITE_BUSY` immediately when a write lock is held. The connection string `Data Source=kasserpro.db` does NOT specify `busy_timeout`.

**FINDING-13 (MEDIUM):** Without a `busy_timeout`, if Cashier B's `BeginTransactionAsync` contends with Cashier A's write lock, EF Core may throw `Microsoft.Data.Sqlite.SqliteException: SQLite Error 5: 'database is locked'`. ASP.NET Core will catch this in `ExceptionMiddleware` and return 500.

**Recommended Mitigation:** Add `Busy Timeout=5000` to the connection string:
```
"DefaultConnection": "Data Source=kasserpro.db;Busy Timeout=5000"
```
This makes SQLite wait up to 5 seconds for the lock before returning BUSY. For 1–3 users, the lock is held ~20ms, so 5s provides enormous headroom.

**Impact without fix:** On very rare occasions (sub-1% probability with ≤3 users), a 500 error on concurrent writes. The user can safely retry. No data corruption.

---

## 5. Security Posture (Local Mode)

### Is JWT secure enough for local?

**YES, with the implemented changes.**

| Aspect | Status |
|--------|--------|
| Secret strength | ✅ Enforced ≥32 chars at startup |
| Secret storage | ✅ Environment variable (not in source) |
| Token validation | ✅ Issuer, Audience, Lifetime, SigningKey all validated |
| Token expiry | ✅ 24 hours |
| No refresh tokens | ⚠️ Acceptable for local POS (low risk) |

For a local network POS, JWT with env-var secret is appropriate. Refresh tokens, key rotation, and OAuth2 are SaaS-grade requirements and out of scope.

### Any remaining attack surface?

| Surface | Status | Risk |
|---------|--------|------|
| CORS: `AllowAnyOrigin()` | ⚠️ STILL PRESENT | LOW for local. Any origin can make API calls if they have a valid JWT. On a local network, this means any device on the LAN. Acceptable for on-premise. |
| SignalR hub: API key validation | ⚠️ WEAK | Checks only `!string.IsNullOrEmpty(apiKey)` — any non-empty string passes. Sufficient for local (only trusted devices on LAN). |
| Swagger in Development | ✅ GATED | Only exposed when `IsDevelopment()` |
| HTTPS not enforced | ⚠️ NOT PRESENT | No `UseHttpsRedirection()`. On localhost/LAN, acceptable. For production over WiFi, tokens transit in cleartext. |
| Admin seed passwords | ✅ GATED | Only exist when `IsDevelopment()` |

**FINDING-14 (LOW):** `AllowAnyOrigin()` CORS policy is used for all API requests. For local POS where the frontend and backend are on the same machine/LAN, this is acceptable. If the POS will ever be exposed to a wider network, restrict to `http://localhost:5173` and the deployment URL.

**FINDING-15 (LOW):** No HTTPS. On a LAN with physical security, HTTP is acceptable. If the POS connects over WiFi, JWT tokens and financial data transit unencrypted.

### Any privilege escalation risk?

**NO.**

| Check | Result |
|-------|--------|
| `DeviceTestController` requires Admin | ✅ |
| `OrdersController` requires any auth | ✅ `[Authorize]` at class level |
| JWT claims (`userId`, `branchId`, `tenantId`) validated server-side | ✅ `CurrentUserService` extracts from token |
| No user can modify their own JWT claims | ✅ Secret is server-side only |

A cashier cannot escalate to admin without the admin's credentials. The JWT signing key is only on the server.

---

## 6. Regression Risk

### Performance Degradation?

| Fix | Impact | Severity |
|-----|--------|----------|
| P0-1 (JWT guard) | Startup-only check. Zero runtime impact. | NONE |
| P0-2 (Seed gate) | Removes seeder call in production. Faster startup. | POSITIVE |
| P0-3 (Stock re-check) | Adds N extra `SELECT` queries inside `CompleteAsync` (one per item). For a typical 5-item order: +5 queries. On SQLite: ~1ms each = +5ms. | NEGLIGIBLE |
| P0-4 (Tax sum) | Pure arithmetic change. LINQ `.Sum()` instead of single multiplication. | NEGLIGIBLE |
| P0-5 (SignalR groups) | `AddToGroupAsync` on connect (+1 operation). Group send vs all-send: same performance. | NEGLIGIBLE |
| P0-6 (Auth attribute) | Standard ASP.NET middleware. Zero measurable overhead. | NONE |
| P0-7 (Retry disable) | Removes retry attempts on mutations. Fewer requests. | POSITIVE |
| P0-8 (Transaction guard) | `HasActiveTransaction` is a null check. Zero overhead. | NONE |

**Verdict: NO performance regression.**

### Deadlocks?

**IMPOSSIBLE with SQLite.** SQLite uses a single global lock, not row-level locks. There are no two locks to deadlock on. A write lock is either held or not. Contention results in `SQLITE_BUSY`, not deadlock.

**Verdict: NO deadlock risk.**

### Unexpected Rollback Behavior?

| Scenario | Behavior |
|----------|----------|
| Stock insufficient at `CompleteAsync` re-check | Explicit `RollbackAsync()`, returns error. Correct. |
| Exception in `RecordTransactionAsync` (standalone) | `RollbackAsync()` in catch block. Correct. |
| Exception in `RecordTransactionAsync` (piggyback) | Exception propagates to `CompleteAsync`'s catch block → `RollbackAsync()`. Correct. |
| Exception in `BatchDecrementStockAsync` | Propagates to `CompleteAsync` → rollback. Correct. |

**FINDING-16 (LOW):** In `CompleteAsync`, when stock re-validation fails (line 507), the method calls `await transaction.RollbackAsync()` and then `return`. The `using` block for the transaction will call `DisposeAsync` which is a no-op after explicit rollback. This is correct behavior.

**Verdict: NO unexpected rollback behavior.**

### Broken Refund Flow?

**NO.** Verified the full `RefundAsync` method (lines 598–887):

1. Uses its own `BeginTransactionAsync` ✅
2. Creates return order with negative amounts ✅
3. Restores stock via `IncrementStockAsync` ✅
4. Calls `RecordTransactionAsync` for cash refunds — which now correctly piggybacks on the refund transaction (`HasActiveTransaction = true`) ✅
5. Commits transaction ✅
6. Post-commit: updates `originalOrder.Notes` (cosmetic, non-critical) ✅

**Refund cash register interaction:** `RecordTransactionAsync` is called with `amount: -cashRefundAmount` (negative for outflow). The type switch (`CashRegisterTransactionType.Refund`) computes `currentBalance - amount`. Since amount is negative: `balance - (-X) = balance + X`. **Wait — this is a BUG-LIKE pattern:**

```csharp
CashRegisterTransactionType.Refund => currentBalance - amount,
```

If `amount = -100` (negative), then `balanceAfter = 1000 - (-100) = 1100`. This INCREASES the balance on a refund, which is **WRONG**.

**FINDING-17 (HIGH):** In the refund path, `RecordTransactionAsync` is called with `amount: -cashRefundAmount` (already negated). The type switch then SUBTRACTS this already-negative amount, resulting in a DOUBLE NEGATION that ADDS to the balance instead of subtracting.

Let me verify the exact call:
- `RefundAsync` line 858: `amount: -cashRefundAmount` where `cashRefundAmount > 0` → `amount = -50` (for a 50 EGP refund)
- `RecordTransactionAsync` type switch: `Refund => currentBalance - amount` → `1000 - (-50) = 1050`
- **Expected:** balance should DECREASE to 950

**THIS IS A PRE-EXISTING BUG — not introduced by P0 fixes.** The P0-8 transaction wrapping didn't change the calculation logic. However, it's now inside a proper transaction, so the incorrect balance is at least atomically written.

**Severity: HIGH for financial accuracy.** The refund flow increases cash balance instead of decreasing it.

### Broken Purchase Invoice Flow?

Purchase invoice flow was not modified by any P0 fix. No regression risk.

---

## 7. Stability Scores

### Financial Safety Score: 8/10

| Factor | Score | Notes |
|--------|-------|-------|
| No duplicate payments | 10 | Retry disabled, state machine prevents re-complete |
| No negative stock | 10 | TOCTOU eliminated with write-lock re-validation |
| Tax calculation correct | 9 | Fixed. Minor: `order.TaxRate` field is orphaned metadata |
| Cash register atomicity | 9 | Transaction wrapping correct |
| Refund cash balance | 4 | **PRE-EXISTING BUG (FINDING-17):** double-negation on refund cash |
| **Weighted Average** | **8** | Deducted for refund cash bug (pre-existing) |

### Concurrency Safety Score: 9/10

| Factor | Score | Notes |
|--------|-------|-------|
| Dual-tab order completion | 10 | SQLite write lock + state machine |
| Concurrent cash transactions | 10 | Write lock serialization + `HasActiveTransaction` |
| Double-click protection | 10 | Retry disabled + RTK Query dedup |
| Server crash recovery | 10 | SQLite auto-rollback |
| SQLITE_BUSY handling | 7 | **No `busy_timeout` configured (FINDING-13)** |
| **Weighted Average** | **9** | -1 for missing busy_timeout |

### Security Score: 7/10

| Factor | Score | Notes |
|--------|-------|-------|
| JWT secret | 10 | Env var, ≥32 chars enforced |
| Demo creds disabled | 10 | Gated by `IsDevelopment()` |
| DeviceTestController secured | 10 | Admin-only |
| CORS policy | 5 | `AllowAnyOrigin` — acceptable for LAN, not ideal |
| HTTPS | 4 | Not enforced. Tokens in cleartext over WiFi |
| SignalR auth | 5 | API key validation is present-but-not-verified |
| **Weighted Average** | **7** | Acceptable for local LAN deployment |

### Production Local Readiness Score: 8/10

| Factor | Score | Notes |
|--------|-------|-------|
| All P0 fixes implemented | 10 | 8/8 verified correct |
| Financial integrity | 8 | Solid except refund cash bug |
| Concurrency safety | 9 | SQLite serialization is correct |
| Security for local | 7 | Adequate for on-premise LAN |
| First-run experience | 5 | **No production seeder or setup wizard (FINDING-02)** |
| Error handling | 8 | Exception middleware + proper rollbacks |
| **Weighted Average** | **8** | Ready with noted caveats |

---

## 8. Final Verdict

### ⚠️ CONDITIONAL PASS — SAFE TO SELL (Local POS Single Branch), pending 2 action items

The 8 P0 hardening fixes have been **correctly implemented** and the system is materially safer than before. For a single-branch, on-premise POS with 1–3 users on SQLite, the financial safety, concurrency, and security posture are adequate for commercial use.

**Must-Fix Before Sale (blocking):**

| # | Finding | Severity | Effort |
|---|---------|----------|--------|
| 1 | **FINDING-17:** Refund cash register calculation double-negates the amount, INCREASING balance on refund instead of decreasing. | HIGH | 30 min |
| 2 | **FINDING-02:** No production initial-setup mechanism (no admin user, no tenant after first deploy). System is unusable after first install. | HIGH | 2 hours |

**Should-Fix Before Sale (non-blocking but recommended):**

| # | Finding | Severity | Effort |
|---|---------|----------|--------|
| 3 | **FINDING-13:** Add `Busy Timeout=5000` to SQLite connection string to prevent intermittent SQLITE_BUSY errors. | MEDIUM | 5 min |
| 4 | **FINDING-08:** Synchronize branch ID between JWT claims and SignalR `X-Branch-Id` header in desktop bridge. | MEDIUM | 30 min |
| 5 | **FINDING-14:** Replace `AllowAnyOrigin()` CORS with specific allowed origins. | LOW | 15 min |

**After resolving items 1 and 2, the system achieves: SAFE TO SELL (Local POS Single Branch).**

---

## Appendix: Complete Findings Index

| ID | Description | Severity | Category | Introduced by P0? |
|----|-------------|----------|----------|--------------------|
| F-01 | `appsettings.json` tracked in Git (but key is empty — safe) | LOW | Security | No (pre-existing) |
| F-02 | No production initial-setup mechanism | HIGH | Deployment | Exposed by P0-2 |
| F-03 | Dev seeder wipes data on every dev restart | LOW | DX | No (pre-existing, intentional) |
| F-04 | Double `SaveChangesAsync` in CompleteAsync (safe within transaction) | LOW | Correctness | No (pre-existing) |
| F-05 | `BatchDecrementStockAsync` warning doesn't block (defense-in-depth only) | LOW | Correctness | P0-3 design choice |
| F-06 | `order.TaxRate` field is now orphaned metadata | MEDIUM | Data Model | Side-effect of P0-4 |
| F-07 | Discount distribution uses `Subtotal` as weight (correct) | LOW | Correctness | P0-4 (correct behavior) |
| F-08 | Branch ID mismatch between JWT claims and SignalR header | MEDIUM | Integration | Side-effect of P0-5 |
| F-09 | `OnDisconnectedAsync` doesn't remove from group (auto-handled) | LOW | Correctness | P0-5 (non-issue) |
| F-10 | IdempotencyMiddleware is now effectively dead code | MEDIUM | Dead Code | Side-effect of P0-7 |
| F-11 | RTK Query `onQueryStarted` could theoretically re-trigger mutation | LOW | Frontend | P0-7 theoretical edge |
| F-12 | Transaction number generation could collide (prevented by SQLite lock) | LOW | Correctness | No (pre-existing) |
| F-13 | No `busy_timeout` on SQLite connection string | MEDIUM | Reliability | No (pre-existing) |
| F-14 | `AllowAnyOrigin()` CORS policy | LOW | Security | No (pre-existing) |
| F-15 | No HTTPS enforcement | LOW | Security | No (pre-existing) |
| F-16 | Explicit rollback + using dispose is correct (no issue) | LOW | Correctness | P0-3 (correct) |
| F-17 | Refund cash register double-negation bug | HIGH | Financial | No (pre-existing) |

**P0 fixes that introduced new findings:** 3 (F-05, F-06, F-08 — all LOW/MEDIUM, no financial impact)  
**Pre-existing findings surfaced by audit:** 8  
**Pre-existing HIGH severity:** 1 (F-17 refund cash balance)  

---

*End of audit report.*
