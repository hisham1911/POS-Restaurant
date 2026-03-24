# ARCHITECTURAL REVIEW REPORT

**Date:** 2026-02-13  
**Reviewed document:** `LOCAL_POS_PRODUCTION_AND_PERMISSION_BLUEPRINT.md`  
**Cross-referenced against:** Actual codebase at `d:\مسح\POS\`  
**Reviewer role:** Principal Software Architect — strict audit mode

---

## PART 1 — STRUCTURAL REVIEW

### Step 1: SQLite Production Configuration

**Score: SAFE**

The approach is correct. WAL + busy_timeout + foreign_keys + synchronous=NORMAL is the right set for local SQLite under low concurrency.

**Issues found:**

1. The blueprint proposes a DB connection interceptor to enforce per-connection PRAGMAs. This is necessary because EF Core can open new connections from the pool. However, `journal_mode=WAL` is a **database-level** persistent setting — once set, it survives across connections and restarts. The interceptor should only set `busy_timeout` and `foreign_keys` (which are per-connection). The blueprint doesn't distinguish between persistent vs per-connection PRAGMAs. Minor, but a developer following this literally would redundantly set WAL on every connection open.

2. `cache_size=-20000` was mentioned in an earlier draft but dropped from the final blueprint. Not a problem — default cache is fine for POS.

3. The blueprint says "Add `Foreign Keys=True` to connection string." This works for `Microsoft.Data.Sqlite` but the interceptor also sets `PRAGMA foreign_keys=ON`. Redundant but harmless. Should pick one and document why.

---

### Step 2: Backup & Restore System

**Score: RISKY**

The design is architecturally sound but has operational blind spots:

1. **Backup path uses `%ProgramData%`**. Good for Windows service mode. But the app currently runs the DB as `Data Source=kasserpro.db` (relative path). The backup service must resolve the absolute DB path from the connection string, not assume a fixed location. If the developer runs in Debug from `src/KasserPro.API/`, the DB is in the project directory. In production it could be elsewhere. The blueprint doesn't address this path resolution ambiguity.

2. **Restore requires "maintenance mode"** but no maintenance mode exists in the codebase. There is no middleware, flag, or endpoint to block incoming requests during restore. The blueprint treats this as a given without specifying implementation. A restore while the API is serving requests will cause undefined behavior with SQLite.

3. **`PRAGMA integrity_check` on a 500MB database can take 10–30 seconds.** For a POS backup on every daily run, this blocks the API thread. The blueprint should specify running integrity check on the backup copy asynchronously, not inline.

4. **Retention (14 daily + 4 weekly) on a 200MB DB = ~3.6 GB disk usage.** For a butcher shop PC with a 128 GB SSD running Windows, this is 3% of total disk. Acceptable, but the deployment checklist should flag minimum free space requirements. It mentions &gt; 5 GB threshold, which is correct.

5. **SQLite backup API with WAL mode**: `sourceConn.BackupDatabase(destConn)` is safe during active writes in WAL mode. But the blueprint doesn't mention that the backup API creates a **snapshot** — ongoing writes after backup starts are NOT included. This is correct behavior but should be documented for operator awareness.

---

### Step 3: Pre-Migration Automatic Backup

**Score: SAFE**

Correct dependency chain (requires backup service first). File-based lock is appropriate for single-machine deployment. The approach of capturing pending migrations count before backup is correct.

**Issue found:**

1. **"If migration fails, keep app in maintenance mode."** Again, no maintenance mode exists. If `MigrateAsync()` throws, the current code path will crash the startup. The blueprint should specify wrapping `MigrateAsync()` in a try-catch that either enters a recovery state or shuts down gracefully with a log message — not silently crash.

2. **No mention of `__EFMigrationsHistory` corruption scenario.** If a migration partially applies (power loss mid-DDL), the history table may not have the entry, but the schema has partial changes. The next `MigrateAsync()` will try to reapply and fail. The pre-migration backup handles rollback, but the blueprint doesn't describe the **detection** of this state. Should add: on startup, compare `__EFMigrationsHistory` count with expected migration count from the assembly.

---

### Step 4: Logging Strategy

**Score: SAFE**

Serilog + rolling files + correlation IDs is standard and correct. 30-day retention is reasonable.

**Issue found:**

1. **Correlation ID middleware reads `X-Correlation-Id` header.** This is fine for SaaS/API-gateway scenarios but unnecessary for a local POS where the only client is the React frontend on the same machine. Over-engineering for the current scope. Not harmful, but adds complexity. The correlation ID should be auto-generated per request and returned in the response header — no need to accept inbound correlation IDs.

2. **Financial audit logging is described but the storage destination is unclear.** Are financial audit entries written to the same rolling log files as general application logs? They should be in a **separate** file sink (e.g., `financial-audit-.log`) with longer retention (90+ days) and ideally also in the database (`AuditLogs` table already exists). The blueprint doesn't specify this separation.

---

### Step 5: Error Handling Strategy

**Score: SAFE**

Correct SQLite error code mapping. Correct HTTP status codes. Adding `correlationId` to the response body ties nicely to Step 4.

**Issue found:**

1. **The current `ExceptionMiddleware` returns `ApiResponse<object>.Fail(message)` format.** The blueprint proposes adding `errorCode`, `messageAr`, `messageEn`, `correlationId`. This changes the error response contract. The frontend currently expects `{ success: false, message: "..." }`. Adding new fields is non-breaking, but the blueprint should explicitly state the envelope remains backward-compatible.

2. **The `SQLITE_BUSY` mapping to `503` is correct but the frontend's error handler doesn't understand 503 specially.** The `baseQueryWithReauth` in `baseApi.ts` retries GETs on 500 but mutations are NOT retried (correct for safety). A 503 on a mutation will be shown as a toast. The blueprint should note that the frontend needs updating to show "retry in a moment" UX for 503 on mutations, not just a generic error.

---

### Step 6: Cash Register Integrity Validation

**Score: OVERBUILT**

A nightly reconciliation job, an `IntegrityAnomaly` table, blocking shift closure on anomalies, and a manual recalculation endpoint — this is enterprise-level financial audit infrastructure for a system serving 1–3 users on one machine.

**Why overbuilt:**

1. The core transaction flow is already atomic (CompleteAsync, RefundAsync). The only known integrity gap is the auto-close shift not recording a cash register transaction. **Fix that one bug directly** instead of building a surveillance system around it.

2. For a local POS, the owner reconciles the cash drawer physically at shift end. If the Difference field shows a mismatch, they see it immediately. A nightly background job recomputing from ledger adds complexity (another background service, another table, another migration) for marginal benefit.

3. **Blocking shift closure on unresolved anomaly** will cause operational lockout if the nightly job produces a false positive. The owner can't close the shift, can't go home, and calls you at 10 PM. For a single-developer product, the support cost exceeds the safety benefit.

**Recommendation:** Replace with a lightweight read-only endpoint `GET /api/admin/integrity-check` that computes the delta on-demand. No background job. No blocking. No new table. Fix the auto-close bug directly.

---

### Step 7: Cart Persistence

**Score: SAFE**

redux-persist on the cart slice with scoped key, TTL, beforeunload warning, and clear-on-success is the right approach for the problem.

**Issue found:**

1. **Price snapshot in persisted cart.** The blueprint says persist "unit price snapshot." If a cached cart is restored 6 hours later and prices changed in between, the order will be created with stale prices. This is actually the **desired** POS behavior (the price at scan time is the price charged), but the blueprint should call this out explicitly so the developer doesn't add "re-validate prices on restore" and break the workflow.

2. **Scoped key `cart:{tenantId}:{branchId}:{userId}` requires all three IDs at persist time.** On app boot, before JWT is decoded, these IDs aren't available. The cart restore must happen after auth rehydration. The blueprint should specify the initialization order: auth rehydrate → decode user context → restore cart.

---

### Step 8: Deployment Checklist

**Score: SAFE**

Correct for local Windows deployment. Service mode with auto-restart is the right call.

**Issue found:**

1. **"API as Windows Service"** but the current codebase is a standard Kestrel web app with no `UseWindowsService()` call and no Windows Service project. This is a significant implementation gap — the blueprint treats it as a checklist item but it requires actual code changes (`Microsoft.Extensions.Hosting.WindowsServices` package, `Host.CreateDefaultBuilder().UseWindowsService()`, installer/service registration script). Effort is underestimated.

2. **"Weekly restore drill"** in the operational runbook is unrealistic for a non-technical operator (butcher shop owner). They will never do this. The system should auto-validate the latest backup on a schedule instead of relying on human discipline.

---

### Migration ordering

**Score: RISKY**

7 planned migrations is high for a single release. Each migration is a schema change that runs automatically on startup.

**Issues:**

1. **Migration 6 (`SeedPermissionCatalogAndDefaultRoles`) is a data migration, not a schema migration.** EF Core migrations are designed for schema changes. Data seeding in migrations is fragile — if the seed data needs updating, you need another migration. Better approach: use a startup seeder (like the existing `ButcherDataSeeder`) gated by a version flag.

2. **Migration 7 (`BackfillLegacyAdminCashierToUserRoles`) depends on the data from Migration 6.** If Migration 6 seeds role IDs that Migration 7 references, the IDs must be deterministic (not auto-increment). The blueprint doesn't specify whether to use fixed IDs or lookup by name. Lookup by name in a migration is fragile.

3. **7 migrations in 4 weeks means roughly 2 per week.** If any one fails in production, the pre-migration backup catches it. But the developer must test the entire chain on a copy of a real customer DB before releasing. The blueprint doesn't include a "migration dry-run" test procedure.

---

### Rollback strategy

**Score: RISKY**

The rollback strategy is "restore from pre-migration backup." This is correct but incomplete:

1. **No reverse migrations.** EF Core supports `Down()` methods but they're rarely reliable. The blueprint correctly doesn't rely on them — but it also doesn't mention that downgrade is intentionally unsupported. Should be stated explicitly.

2. **Restore from backup loses all data written after the backup.** If a migration runs at 9 AM and fails, and the operator restores the 2 AM daily backup, 7 hours of sales data are lost. The pre-migration backup mitigates this (backup is seconds before migration), but the blueprint should state this explicitly: the pre-migration backup preserves data up to the moment of upgrade.

---

## PART 2 — PERMISSION SYSTEM REVIEW

### Is permission enforcement backend-first?

**Yes, by design.** The blueprint specifies `[RequirePermission]` attribute on endpoints, policy-based authorization handlers, and explicitly states "Always enforce backend checks regardless of UI state."

**But the current codebase has a critical gap the blueprint underestimates:**

The existing `[Authorize(Roles = "Admin,Manager")]` attributes reference a `Manager` role that **does not exist** in the `UserRole` enum (`Admin=0, Cashier=1, SystemOwner=2`). This means those endpoints are effectively locked to `Admin` only — `Manager` never matches. The blueprint proposes replacing the role enum with granular permissions but doesn't flag this existing dead-code bug.

---

### Can a malicious user bypass frontend restrictions?

**YES — today, trivially.** And the blueprint's mitigation is incomplete.

**Current vulnerability (confirmed by code read):**

`CurrentUserService.BranchId` reads `X-Branch-Id` header **first**, falling back to JWT only if the header is absent. There is **zero server-side validation** that the authenticated user is authorized for the header-specified branch.

**Attack scenario (works right now):**
1. Cashier logs in (JWT contains `branchId: 1`)
2. Cashier sends `X-Branch-Id: 2` header with any request
3. `CurrentUserService.BranchId` returns `2`
4. All operations (open shift, create order, cash withdrawal) execute against Branch 2
5. No check anywhere prevents this

**The blueprint addresses this** in Section 2.4 ("Protection Against X-Branch-Id Manipulation") with the correct solution: validate the header branch against `UserBranchAccess`. But it schedules this for Phase 3 (Week 3–4). This is a **live security vulnerability** that should be fixed in Phase 0, before the permission system is built. The fix is simple: validate `X-Branch-Id` against the user's `BranchId` from JWT or from the `User` record.

**The same header is trusted in 5 locations:**
- `CurrentUserService.cs` (all service calls)
- `AuditSaveChangesInterceptor.cs` (audit logs)
- `DeviceHub.cs` (SignalR)
- `DeviceTestController.cs` (direct read)
- Integration tests (explicitly validate the override works)

---

### Is JWT payload overloaded?

**No — the blueprint is correctly conservative.** It proposes putting only `sub`, `tenant_id`, `role_stamp`, `branch_scope_mode`, `jti`, `iat`, `exp` in the JWT. Full permissions fetched from DB per-request (cached). This is the right approach for a local system where the DB is always available (same machine).

**However:** The current JWT includes `ClaimTypes.Role` (role name as string). The blueprint proposes `role_stamp` (version hash) instead. This means the `[Authorize(Roles = "Admin")]` attribute pattern stops working. The migration from role-claim-based auth to stamp-based auth is a **breaking change** for every controller. The blueprint acknowledges this in the breaking change analysis but underestimates the blast radius — **every `[Authorize(Roles = ...)]` attribute across 20+ controllers must be replaced.**

---

### Is there privilege escalation risk?

**YES — today.**

`AuthService.RegisterAsync`:
```csharp
var user = new User
{
    Role = Enum.Parse<UserRole>(request.Role)
};
```

The role comes from the request body. The endpoint is guarded by `[Authorize(Roles = "Admin")]`. This means any Admin can create a `SystemOwner` account. The blueprint proposes fixing this with "Only SystemOwner can assign roles containing admin-grade permissions" — correct, but the current `RegisterAsync` has no role-level guard. An Admin creating a SystemOwner is a privilege escalation path that exists **today**.

---

### Is branch tampering fully mitigated?

**NO — not in the blueprint's proposed timeline.**

As detailed above, the `X-Branch-Id` header vulnerability is unguarded. The blueprint proposes `UserBranchAccess` table with branch authorization, but this lands in Phase 3. Phases 0–2 deploy to production with the branch tampering vulnerability open. Any cashier can operate on any branch during that window.

---

### Is tenant isolation guaranteed?

**NO — and the blueprint doesn't fully acknowledge this.**

The blueprint says: "Global query filters must include tenant isolation for all mutable aggregates."

**Reality:** The `AppDbContext` has `_currentTenantId` hardcoded to `1` and **zero tenant query filters**. All 25 query filters are soft-delete only (`!e.IsDeleted`). Tenant isolation is not enforced at the data access layer. It relies entirely on service-level code passing the correct `tenantId` from `ICurrentUserService`.

This means adding tenant query filters is a **massive change** — every entity's query filter needs to include `e.TenantId == _currentTenantId`, the DbContext needs proper tenant resolution (not hardcoded), and every existing query must be tested to ensure the filter doesn't break it (especially cross-tenant admin queries).

The blueprint lists this under "Tenant isolation guarantee" as if it's a configuration change. It's actually a **architectural retrofit** that touches every query in the system.

---

### Is there risk of permission cache inconsistency?

**YES — explicitly.**

The blueprint proposes caching effective permissions per user with "short TTL, e.g., 5 min." This means:

1. Admin revokes cashier's refund permission
2. Cashier has up to 5 minutes to perform refunds with cached permissions
3. For financial operations, 5 minutes of stale permissions is a lot of refunds

The `UserSecurityStamp + RoleStamp` mechanism in the JWT should catch this — but only if the stamp check happens on every request **before** the cache is consulted. The blueprint says "On each request (cached lookup), compare stamp" — this is contradictory. If the stamp lookup itself is cached, the window expands. If it's not cached, it's a DB read per request (which defeats the caching).

**For local deployment (DB on same machine), the latency of a per-request DB read for stamp validation is <1ms.** Cache is unnecessary. Just read the stamp on every request and eliminate the inconsistency window entirely.

---

### Is token invalidation realistic in local mode?

**PARTIALLY.** The stamp-based approach works but has a dependency the blueprint doesn't address:

The `User` entity currently has **no `SecurityStamp` field**. Adding it requires a migration. The JWT currently has no stamp claim. The `OnTokenValidated` event in `Program.cs` already queries the DB for user activity status — adding a stamp check there is straightforward. But the blueprint presents this as part of Phase 3 (permission platform). The user activity check in `OnTokenValidated` is already doing a DB query per request — extending it with a stamp check is zero additional cost and should be in Phase 0.

---

### Privilege escalation scenarios

| Scenario | Currently possible? | Blueprint fixes it? | When? |
|----------|---------------------|---------------------|-------|
| Cashier gains Admin via API manipulation | No (role in JWT, validated) | N/A | — |
| Admin creates SystemOwner via Register endpoint | **YES** | Yes (role assignment guard) | Phase 3 |
| Cashier operates on wrong branch via X-Branch-Id | **YES** | Yes (UserBranchAccess) | Phase 3 |
| Deactivated user continues with valid JWT | **No** (OnTokenValidated checks IsActive per request) | Stamp adds extra safety | Phase 3 |
| Role change not reflected until JWT expires | **YES** (24h expiry, no stamp) | Yes (stamp invalidation) | Phase 3 |
| Tenant A user accesses Tenant B data | **Unlikely** (tenantId in JWT, service code filters by it) but **no DB-level guard** | Tenant query filters | Phase 3 |

**Critical observation:** All security fixes are in Phase 3 (Week 3–4). Phases 0–2 ship with these vulnerabilities open. For a single-tenant local deployment this is acceptable. For multi-tenant it is not.

---

## PART 3 — OPERATIONAL MATURITY REVIEW

### Is backup strategy realistic?

**Mostly yes, with gaps.**

- SQLite backup API is correct for hot backups.
- Auto-daily + pre-migration is the right combination.
- Retention policy is reasonable.
- **Gap:** No backup verification notification to the operator. If the daily backup silently fails for a week (disk permission change, path deleted), the operator won't know until they need a restore. Add a "last successful backup" indicator on the main dashboard.
- **Gap:** No external backup (USB/network). All backups are on the same disk as the DB. If the disk fails, everything is lost — DB and backups. The blueprint should include an optional "copy latest backup to USB" flow.

### Is restore tested?

**Not addressed.** The blueprint describes the restore flow but includes no testing procedure. For a solo developer, the minimum viable test is: backup → inject a test record → restore → verify the test record is gone. This should be a documented manual test case, not left to assumption.

### Is logging actionable?

**Yes, if financial audit logs are separated.** The correlation ID + structured logging + Serilog file sink is actionable. But mixing financial audit entries with HTTP middleware noise in the same log file makes diagnosis harder. The blueprint should specify at minimum two sinks: general app log and financial audit log.

### Are logs rotated?

**Yes.** Serilog rolling file with 30-day retention handles this automatically.

### Is file growth considered?

**Partially.** Log retention is addressed. Backup retention is addressed. But the **database file itself** grows monotonically — SQLite does not shrink after deletes (soft-delete means nothing is ever physically deleted). A POS running for a year could have a 500MB+ database. The blueprint doesn't mention `VACUUM` or `auto_vacuum`. For local SSD, this is acceptable but should be monitored.

### Are SQLite locking issues truly mitigated?

**Mostly.** WAL mode + busy_timeout=5000 + Cache=Shared is the correct configuration for 1–3 users. Edge case: if a long-running report query (e.g., monthly sales report generating a 10MB result set) holds a read lock for 30 seconds, and WAL grows beyond the checkpoint threshold, the checkpoint will block writers. This is extremely unlikely with 1–3 users but the blueprint should mention that report endpoints should use `AsNoTracking()` and timeout guards.

### Are chaos scenarios covered?

**Partially.** Power loss, crash, disk full are addressed through SQLite ACID + backup. Missing:
- **Windows Update forced restart** — kills the Kestrel process mid-transaction. Same as power loss (safe due to ACID), but Windows Service mode should be configured with `SC failure reset=86400 actions=restart/60000` for auto-recovery.
- **Antivirus file lock** — Windows Defender or other AV can lock `kasserpro.db` during a scan, causing `SQLITE_BUSY`. The deployment checklist mentions Defender exclusion but as optional. Should be mandatory.

### Is update process safe for non-technical clients?

**No — insufficient detail.** The blueprint says "replace exe/DLL files" and auto-migration handles the rest. But:
1. Who replaces the files? An installer? Manual file copy?
2. If an installer, it must stop the Windows Service before replacing files.
3. If manual, the operator must know to stop the app first.
4. The blueprint doesn't specify an installer or update mechanism.
5. For non-technical clients, a self-updating mechanism or at minimum a `.msi` installer with service management is needed.

---

## PART 4 — SIMULATED FAILURE TEST

### 1. Power loss during order completion

**Verdict: SURVIVES**

SQLite ACID guarantee with `journal_mode=WAL` (or DELETE) ensures the transaction is either fully committed or fully rolled back. EF Core transaction wraps the entire CompleteAsync flow. Cart is lost (not persisted yet), but that's a UX issue, not data corruption.

### 2. Power loss during refund

**Verdict: SURVIVES**

Same ACID guarantee. RefundAsync is wrapped in an explicit transaction. Post-commit Notes update may be lost (cosmetic). Financial data is consistent.

### 3. Crash during migration

**Verdict: PARTIALLY SURVIVES**

With the proposed pre-migration backup: the backup exists. But:
- The app won't start (migration state is inconsistent).
- There is no auto-restore mechanism — the blueprint says "present rollback option" but the app is crashed and can't present anything.
- The operator must manually copy the backup file over the DB.
- If the operator doesn't understand file paths, they're stuck.

**Actual outcome:** Operator calls developer. Developer walks them through file copy over the phone. Data is recoverable but not self-service.

### 4. Token theft

**Verdict: PARTIALLY SURVIVES**

With current 24h expiry and no stamp validation: a stolen JWT is valid for up to 24 hours. The attacker has full access to the user's role and branch (and any branch via X-Branch-Id header). The `OnTokenValidated` event checks `IsActive`, so deactivating the user kills the stolen token on next request. But the operator must know to deactivate the user — and they won't know the token was stolen.

With the proposed stamp-based invalidation: changing the user's password or role rotates the stamp, killing the token. Better, but still reactive.

For local deployment, token theft requires physical or network access to the machine. Risk is low.

### 5. Double-click payment

**Verdict: PARTIALLY SURVIVES**

Frontend button disabled guard works in 99% of cases. IdempotencyMiddleware is active but frontend sends no key (0% protection from middleware). If two requests arrive simultaneously:
- First `createOrder` creates Order A
- Second `createOrder` creates Order B (duplicate)
- First `completeOrder(A)` succeeds
- Second `completeOrder(B)` succeeds — **double charge**

The `isCreating` flag in React state prevents this in practice (synchronous state update before async call). But on slow hardware with React concurrent mode, the edge case exists.

### 6. Branch tampering

**Verdict: FAILS**

Currently, sending `X-Branch-Id: 99` in the request header is accepted without validation. The cashier can open shifts, create orders, and withdraw cash from any branch. The blueprint fixes this in Phase 3 only.

### 7. Cashier with outdated JWT

**Verdict: PARTIALLY SURVIVES**

Scenario: Admin changes cashier from Branch 1 to Branch 2. Cashier's JWT still says `branchId: 1`. The `OnTokenValidated` event checks `IsActive` and `Tenant.IsActive` but does NOT check branchId or role freshness. The cashier continues operating on Branch 1 until the JWT expires (up to 24 hours).

With the blueprint's stamp-based approach: branch change rotates stamp, killing the JWT immediately. But this is Phase 3.

### 8. SQLite database corruption

**Verdict: PARTIALLY SURVIVES**

With backup system: latest backup is recoverable (data loss = time since last backup). Without backup system (current state): total data loss.

With the blueprint's integrity check on startup: corruption is detected and the operator is informed. But auto-restore requires the operator to interact with the system — a corrupted startup may not even reach the restore UI.

### 9. Backup restore with mismatched schema

**Verdict: FAILS**

Scenario: Running v1.1 code. Restore a backup from v1.0 (missing v1.1 columns). `MigrateAsync()` runs on next startup and applies the missing migration. **This should work** — EF Core checks `__EFMigrationsHistory` and applies only pending migrations.

**BUT:** If the v1.0 backup was created before a data migration (Migration 7 — backfill legacy roles), restoring it means the backfill hasn't run. The v1.1 code expects `UserRoles` records to exist. If the fallback window was already removed (Release C), the user can't log in.

**Mitigation:** The pre-migration backup is taken at the moment of upgrade, so the backup is v1.0-schema but the data is pre-migration. Restoring + re-running `MigrateAsync()` should reapply correctly. But this is fragile if data migrations are not idempotent.

---

## PART 5 — VERDICT

### 1. Overall production readiness score: 6/10

The blueprint correctly identifies the right problems and proposes architecturally sound solutions. But it has timing/prioritization errors (security fixes delayed to Phase 3), underestimates the blast radius of tenant isolation (treats it as a config change, it's a retrofit), overbuilds in one area (integrity validation), and has operational gaps (no installer, no maintenance mode, no external backup).

### 2. Top 5 real blockers (not theoretical)

| # | Blocker | Why it's real |
|---|---------|---------------|
| 1 | **X-Branch-Id header is completely untrusted NOW** | Any cashier can operate on any branch today. This is a live exploit, not a future risk. Must be fixed before any deployment. |
| 2 | **No maintenance mode for restore/migration** | Backup restore while API is active = corruption. Migration while requests arrive = undefined behavior. There's no way to block requests during these operations. |
| 3 | **Admin can create SystemOwner accounts** | `RegisterAsync` accepts any `UserRole` enum value. An Admin creating a SystemOwner escapes their intended privilege boundary. |
| 4 | **24h JWT with no stamp = 24h stale permissions** | Role change, branch change, deactivation — none take effect until the JWT expires. The `OnTokenValidated` checks `IsActive` but not role/branch freshness. |
| 5 | **No installer or update mechanism** | "Replace exe files" is not a deployment strategy for non-technical clients. A Windows Service + MSI installer is required. |

### 3. Things that look good

- **Core transaction safety is solid.** CompleteAsync and RefundAsync are properly atomic. F-17 is confirmed fixed.
- **SQLite configuration plan is correct.** WAL + busy_timeout + FK is exactly right for local POS.
- **Backup design using SQLite backup API** (not file copy) is the right technical choice.
- **JWT payload design is conservative and correct.** Permissions loaded from DB, not stuffed into token.
- **Cart persistence approach is right.** redux-persist with scoped key, TTL, and beforeunload is the correct pattern.
- **Error mapping for SQLite codes** is practical and solves a real UX problem.
- **Phase ordering** (hardening first, then permissions) is correct — stabilize before adding complexity.

### 4. Things that look dangerous long-term

- **Tenant isolation is service-code only, no DB-level enforcement.** A single missing `.Where(x => x.TenantId == tenantId)` in any new query leaks cross-tenant data. This is a ticking time bomb as the codebase grows.
- **Permission cache with TTL** creates a window where revoked permissions still work. For financial operations, any staleness window is a liability.
- **7 migrations in one release** is aggressive. If any migration has a bug, the cascading effect through data migrations (6 and 7) could corrupt the permission state for all users.
- **No automated tests for permission matrix.** The blueprint mentions "Permission matrix tests" as a requirement but doesn't account for the effort of writing them. A role × permission × branch matrix for 50+ permissions is a large test surface.

### 5. What MUST be fixed before selling

1. **Patch `CurrentUserService.BranchId` to validate X-Branch-Id against user's allowed branches** — immediate, before any client deployment.
2. **Add `SecurityStamp` field to User and validate in `OnTokenValidated`** — close the stale JWT window.
3. **Guard `RegisterAsync` against role escalation** — Admin cannot create SystemOwner.
4. **Implement maintenance mode** (even a simple file-flag check in middleware) — required for safe restore and migration.
5. **SQLite PRAGMAs** (WAL, busy_timeout, foreign_keys) — the foundation everything else depends on.
6. **File-based logging** — without it, production support is impossible.
7. **Pre-migration backup** — without it, upgrades are one-way tickets.

### 6. What can wait

- Full granular permission system (RBAC/PBAC) — the existing role enum (Admin/Cashier/SystemOwner) is sufficient for v1.0 local deployment.
- Integrity validation background job — fix the auto-close bug directly instead.
- Correlation IDs — nice for debugging but not blocking.
- External backup (USB) — daily on-disk backup is sufficient for v1.0.
- Tenant query filters — only needed when deploying multi-tenant. For single-tenant local POS, service-level filtering is acceptable.
- Windows Service mode — can run as a console app initially if installer is not ready.

### 7. Next logical step

1. **Immediate hotfix (1 day):** Validate `X-Branch-Id` against user record + guard role escalation in Register.
2. **Phase 0 (3 days):** SQLite PRAGMAs + Serilog file logging + SQLite exception mapping + SecurityStamp + maintenance mode flag.
3. **Phase 1 (3 days):** Backup/restore with pre-migration auto-backup.
4. **Phase 2 (2 days):** Cart persistence + auto-close cash register fix.
5. **Ship v1.0** with existing role-based auth (no granular permissions yet).
6. **Phase 3 (2 weeks):** Permission platform, tenant isolation query filters, branch access control. Ship as v1.1.
