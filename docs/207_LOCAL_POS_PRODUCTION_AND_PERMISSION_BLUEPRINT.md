# LOCAL_POS_PRODUCTION_AND_PERMISSION_BLUEPRINT

**Date:** 2026-02-13  
**System:** KasserPro (ASP.NET Core + EF Core + SQLite + React)  
**Target:** Local-first Windows single-machine deployment, future SaaS-compatible architecture

---

## PART 1 — PRODUCTION HARDENING PLAN

### Step 1 — SQLite Production Configuration
**Goal**  
Stabilize concurrency and data integrity under 1–3 concurrent users.

**Why it matters**  
Without WAL + busy timeout + FK enforcement, peak usage causes lock failures and potential relational drift.

**Exact implementation approach**  
1. Update connection string in `src/KasserPro.API/appsettings.json`:
   - Keep `Data Source=kasserpro.db;Cache=Shared`
   - Add `Foreign Keys=True`
2. Apply PRAGMA on startup (after app build, before first DB usage):
   - `PRAGMA journal_mode=WAL;`
   - `PRAGMA busy_timeout=5000;`
   - `PRAGMA synchronous=NORMAL;`
   - `PRAGMA foreign_keys=ON;`
3. Add DB connection interceptor to enforce per-connection PRAGMAs (`busy_timeout`, `foreign_keys`).
4. Log and verify startup values (`journal_mode`, `foreign_keys`) once at boot.

**Order of execution**  
1st (foundation for all DB-facing hardening steps).

**Risk if skipped**  
Random `SQLITE_BUSY` failures, poor peak-hour UX, weak relational integrity guarantees.

**Estimated effort**  
1 day.

---

### Step 2 — Backup & Restore System (Local Filesystem)
**Goal**  
Provide operator-safe, auditable backup/restore with minimum operational steps.

**Why it matters**  
Single-file SQLite means corruption/disk issues become total business outage without backup.

**Exact implementation approach**  
1. Add backup root: `%ProgramData%/KasserPro/backups/{tenantId}/`.
2. Implement `IBackupService` in Infrastructure:
   - `CreateBackup(reason)`
   - `ListBackups()`
   - `RestoreBackup(fileName)`
   - `ValidateBackup(fileName)` using `PRAGMA integrity_check`.
3. Use SQLite backup API (not raw file copy while active).
4. Naming convention:
   - `kasserpro-{tenantId}-{yyyyMMdd-HHmmss}-{reason}.db`
5. Retention:
   - Keep last 14 daily backups + 4 weekly backups.
6. Add API endpoints (admin-only):
   - `POST /api/admin/backup`
   - `GET /api/admin/backup`
   - `POST /api/admin/backup/{file}/restore`
7. Restore flow:
   - Put system in maintenance mode
   - Validate backup
   - Auto-create pre-restore safety backup
   - Restore
   - Force app restart notification.
8. Add minimal UI page for backup actions and latest backup status.

**Order of execution**  
2nd.

**Risk if skipped**  
Any DB corruption, accidental deletion, or failed upgrade can be unrecoverable.

**Estimated effort**  
2–3 days.

---

### Step 3 — Pre-Migration Automatic Backup
**Goal**  
Guarantee rollback point before schema changes.

**Why it matters**  
Auto-migration without pre-backup can brick production DB on migration defect or power loss.

**Exact implementation approach**  
1. Before `MigrateAsync()`:
   - Call `GetPendingMigrationsAsync()`.
   - If pending > 0, create backup tagged `pre-migration-{appVersion}`.
2. Persist migration run record:
   - Version, start/end time, outcome, backup file name.
3. If migration fails:
   - Keep app in maintenance mode.
   - Present rollback option using captured backup.
4. Prevent parallel migration execution:
   - File-based lock `migration.lock` in app data folder.

**Order of execution**  
3rd (after backup system exists).

**Risk if skipped**  
Upgrade failures may leave partial schema and no recovery path.

**Estimated effort**  
1 day.

---

### Step 4 — Logging Strategy (File Logs + Correlation IDs)
**Goal**  
Enable post-incident diagnosis and end-to-end request tracing.

**Why it matters**  
Console-only logs are lost on restart; support cannot investigate production incidents.

**Exact implementation approach**  
1. Add Serilog with rolling files under `%ProgramData%/KasserPro/logs/`.
2. Log levels:
   - Default `Information`
   - `Warning` for framework noise
   - `Error` for failed business operations.
3. Retention:
   - 30 days local files.
4. Add correlation middleware:
   - Read header `X-Correlation-Id` if present and valid UUID.
   - Else generate GUID.
   - Attach to `HttpContext.Items`, response header, and log scope.
5. Include in every structured log:
   - `CorrelationId`, `TenantId`, `BranchId`, `UserId`, `Endpoint`.
6. Add audit category for financial operations:
   - `OrderComplete`, `Refund`, `CashAdjustment`, `ShiftClose`.

**Order of execution**  
4th.

**Risk if skipped**  
No forensic trail for financial disputes or production outages.

**Estimated effort**  
1 day.

---

### Step 5 — Error Handling Strategy (DB Exception Mapping)
**Goal**  
Convert low-level database failures into deterministic API responses.

**Why it matters**  
Generic 500 responses force retries and hide actionable operator guidance.

**Exact implementation approach**  
1. Extend `ExceptionMiddleware` to detect `SqliteException` and `IOException`.
2. Map key SQLite codes:
   - `5 SQLITE_BUSY` -> `503 ServiceUnavailable`, code `DB_BUSY`
   - `6 SQLITE_LOCKED` -> `503`, code `DB_LOCKED`
   - `11 SQLITE_CORRUPT` -> `500`, code `DB_CORRUPT`
   - `13 SQLITE_FULL` -> `507 InsufficientStorage`, code `DB_DISK_FULL`
3. Response envelope includes:
   - `errorCode`, `messageAr`, `messageEn`, `correlationId`.
4. Add retry guidance only for retriable classes (`BUSY`, `LOCKED`).
5. Emit structured log with full exception metadata and correlation ID.

**Order of execution**  
5th.

**Risk if skipped**  
Operators receive ambiguous failures and may create duplicate operations by manual retry.

**Estimated effort**  
0.5–1 day.

---

### Step 6 — Cash Register Integrity Validation
**Goal**  
Detect and block silent financial drift between orders, cash transactions, and shifts.

**Why it matters**  
Even with fixed transaction bugs, operational anomalies (manual edits, interrupted workflows) can desync balances.

**Exact implementation approach**  
1. Add nightly reconciliation job:
   - Recompute expected cash balance from immutable transaction ledger.
   - Compare with current register balance.
2. Add invariants:
   - Every completed cash sale must have corresponding cash transaction.
   - Every refund must have negative cash effect.
   - Auto-closed shift must write corresponding register event.
3. Add endpoint `POST /api/admin/integrity/recalculate` (admin only).
4. Add `IntegrityAnomaly` table:
   - `Id, TenantId, BranchId, Type, ReferenceId, Expected, Actual, Delta, DetectedAt, ResolvedAt`.
5. Block shift closing when unresolved critical anomaly exists.

**Order of execution**  
6th.

**Risk if skipped**  
Financial mismatch may accumulate unnoticed until month-end reconciliation.

**Estimated effort**  
1–2 days.

---

### Step 7 — Cart Persistence
**Goal**  
Prevent order loss due to browser refresh/crash.

**Why it matters**  
POS cashiers operate quickly; losing cart state creates customer-facing delays and repeated scans.

**Exact implementation approach**  
1. Persist `cart` slice using `redux-persist` with scoped key:
   - `cart:{tenantId}:{branchId}:{userId}`.
2. Persist minimal fields only:
   - item id, qty, unit price snapshot, discounts, draft customer id.
3. Add TTL:
   - expire cart after 8 hours inactivity.
4. Add `beforeunload` warning when cart has items and payment not finalized.
5. On app boot:
   - auto-restore cart and show “draft restored” banner.
6. On successful `completeOrder`:
   - clear persisted cart atomically after success response.

**Order of execution**  
7th.

**Risk if skipped**  
Active sales are lost on refresh/crash; operators may duplicate sales manually.

**Estimated effort**  
1 day.

---

### Step 8 — Deployment Checklist (Local Windows)
**Goal**  
Standardize repeatable, supportable on-site deployment.

**Why it matters**  
Most production issues in local deployments are environment/setup mistakes.

**Exact implementation approach**  
1. Prerequisites checklist:
   - Windows 10/11 Pro
   - .NET runtime (if self-contained not used)
   - writable `%ProgramData%/KasserPro`
   - correct timezone and NTP sync.
2. Install layout:
   - App binaries in `C:/Program Files/KasserPro`
   - Data/logs/backups in `%ProgramData%/KasserPro`.
3. Security:
   - NTFS ACL: standard users read binaries, write only app data folder.
   - Windows Defender exclusion for DB/log path if lock contention observed.
4. Service mode:
   - API as Windows Service (auto restart on failure).
5. Startup validation script:
   - DB open test, WAL check, backup folder writable, disk free > 5 GB.
6. Operational runbook:
   - Daily backup check
   - Weekly restore drill
   - Monthly log archive verification.

**Order of execution**  
8th (final readiness gate).

**Risk if skipped**  
Inconsistent field installations, high support load, preventable outages.

**Estimated effort**  
1 day.

---

## PART 2 — ADVANCED PERMISSIONS SYSTEM

### 2.1 Authorization Model (Target Roles)
- **SystemOwner (Company Owner / Super Admin)**
  - Cross-tenant operations
  - Create tenant admins
  - Create/update permission templates
- **TenantAdmin**
  - Manage users inside own tenant
  - Assign predefined tenant roles
  - Restrict branch access
- **Cashier**
  - Only assigned operational permissions

Core principle: **Role = container, Permission = enforcement unit.**

---

### 2.2 Database Schema Changes

#### New tables
1. `Roles`
   - `Id, TenantId (nullable for global), Name, IsSystem, IsActive, CreatedAt`
   - Unique: `(TenantId, Name)`
2. `Permissions`
   - `Id, Code, Module, Action, Description, IsActive`
   - Unique: `Code`
   - Example code: `orders.complete`, `cash.withdraw`, `users.create`
3. `RolePermissions`
   - `RoleId, PermissionId, Granted` (bool, default true)
   - Unique composite `(RoleId, PermissionId)`
4. `UserRoles`
   - `UserId, RoleId, TenantId, AssignedBy, AssignedAt`
   - Unique composite `(UserId, RoleId)`
5. `UserBranchAccess`
   - `UserId, BranchId, TenantId, CanSell, CanRefund, CanManageShift`
   - Unique composite `(UserId, BranchId)`
6. `PermissionAuditLogs`
   - permission assignment/revocation trail

#### Required constraints/indexes
- FK on all Tenant/Branch/User links.
- Query indexes:
  - `UserRoles(UserId, TenantId)`
  - `UserBranchAccess(UserId, BranchId)`
  - `RolePermissions(RoleId)`
- Optional row version column for role mutation concurrency.

---

### 2.3 Backend Architecture

#### A) Authorization policies
1. Register policy provider with dynamic permission policies:
   - Policy name format: `perm:{permissionCode}`.
2. Custom requirement handlers:
   - `PermissionRequirement(permissionCode)`
   - `BranchAccessRequirement(branchAction)`

#### B) Custom attribute
- Add `[RequirePermission("orders.complete")]`.
- Attribute maps to policy at endpoint level.
- Mandatory on all financial/admin endpoints.

#### C) Permission middleware/service
1. Resolve effective permissions per request from:
   - User roles + role permissions + user branch access.
2. Cache per user/tenant (short TTL, e.g., 5 min).
3. Expose effective permission set in request context.

#### D) Tenant isolation guarantee
- Never trust tenant/branch from client body or header alone.
- Derive `TenantId` from JWT claim only.
- Validate requested `BranchId` against `UserBranchAccess`.
- Global query filters must include tenant isolation for all mutable aggregates.

---

### 2.4 Protection Against X-Branch-Id Manipulation

1. Keep `X-Branch-Id` optional hint, not authority.
2. Enforcement flow:
   - Read `userId/tenantId` from JWT.
   - Resolve allowed branches for user.
   - If header branch not in allowed set -> `403 BRANCH_FORBIDDEN`.
3. For cashiers with single branch assignment:
   - Ignore incoming header and force assigned branch server-side.
4. Log tampering attempts in security log with correlation ID.

---

### 2.5 JWT Structure

#### Put inside token
- `sub` (user id)
- `tenant_id`
- `role_stamp` (version hash for role membership)
- `branch_scope_mode` (`single|multi`)
- `jti`, `iat`, `exp`

#### Do NOT put inside token
- Full permission list (stale and too large)
- Raw branch IDs list for large tenants (token bloat)
- Mutable profile/business data

#### Token invalidation strategy
1. Maintain `UserSecurityStamp` + `RoleStamp` in DB.
2. Include stamp hash in JWT.
3. On each request (cached lookup), compare stamp:
   - mismatch -> reject token (`401 TOKEN_REVOKED`).
4. Trigger stamp rotation on:
   - password reset
   - role assignment change
   - branch access change
   - manual logout-all.

---

### 2.6 Frontend Authorization Design

1. Fetch effective permissions after login:
   - `GET /api/me/access-profile`
2. Store in memory + short-lived cache (not source of truth).
3. Route guard:
   - Block route if required permission missing.
4. Button-level control:
   - **Hide** when feature should be invisible (security-sensitive actions).
   - **Disable** with reason tooltip when visibility is useful but action forbidden.
5. Always enforce backend checks regardless of UI state.

---

### 2.7 Migration Strategy (Admin/Cashier -> Granular)

1. Seed permission catalog.
2. Create default roles per tenant:
   - `TenantAdmin`, `Cashier`, optional `Supervisor`.
3. Map existing enum users:
   - old `Admin` -> `TenantAdmin`
   - old `Cashier` -> `Cashier`
4. Seed branch access:
   - for existing users, grant current branch.
5. Keep backward compatibility window:
   - if user has no `UserRoles`, fallback to old enum for one release only.
6. Remove enum fallback in next release after data migration confirmed.

---

### 2.8 Security Validation Checklist

#### Prevent privilege escalation
- Only SystemOwner can assign roles containing admin-grade permissions.
- TenantAdmin cannot assign SystemOwner/global roles.
- Self-role elevation prohibited by service-level guard.

#### Prevent cross-tenant access
- Tenant resolved from JWT only.
- Every repository query includes tenant scope.
- Endpoint tests for tenant boundary on read/write.

#### Prevent branch tampering
- Branch checked against `UserBranchAccess` before service call.
- Cash/shift/order endpoints require branch authorization policy.

---

## PART 3 — IMPLEMENTATION ORDER

### 3.1 Phased execution plan

### Phase 0 — Foundations (Week 1)
1. SQLite production config (Step 1)
2. Logging + correlation IDs (Step 4)
3. DB exception mapping (Step 5)

**Exit criteria**  
- WAL verified at startup
- Correlation ID appears in all API logs
- SQLite busy/full/corrupt mapped responses validated

### Phase 1 — Data Safety (Week 1–2)
1. Backup/restore service + API (Step 2)
2. Pre-migration auto-backup (Step 3)
3. Windows deployment checklist artifacts (Step 8)

**Exit criteria**  
- Manual backup works
- Restore dry-run works on staging copy
- Migration failure rollback tested

### Phase 2 — Financial Integrity (Week 2)
1. Cash register integrity validation (Step 6)
2. Cart persistence (Step 7)

**Exit criteria**  
- Nightly reconciliation detects injected anomaly
- Cart survives refresh/crash and clears after successful payment

### Phase 3 — Permission Platform (Week 3–4)
1. Permission schema migrations
2. Backend policy/attribute/middleware
3. Frontend route/button guards
4. Legacy role migration and fallback window

**Exit criteria**  
- Permission matrix tests pass
- Branch tampering attempts rejected with 403
- Existing clients upgraded without account lockout

---

### 3.2 Database migrations count (planned)

**Hardening stream (3 migrations):**
1. `AddBackupAndMigrationAuditTables`
2. `AddIntegrityAnomalyTable`
3. `AddSecurityAuditIndexes`

**Permission stream (4 migrations):**
4. `AddRolesPermissionsCore`
5. `AddUserRolesAndBranchAccess`
6. `SeedPermissionCatalogAndDefaultRoles`
7. `BackfillLegacyAdminCashierToUserRoles`

**Total planned migrations:** **7**.

---

### 3.3 Breaking change analysis

1. **Auth behavior change** (medium risk)
   - Endpoints now require explicit permissions, not just role enum.
2. **Branch enforcement tightening** (medium risk)
   - Requests with forged `X-Branch-Id` start failing (expected).
3. **Legacy role fallback removal** (high risk, delayed)
   - Must happen only after all users migrated to `UserRoles`.
4. **Operational scripts needed** (low risk)
   - Backup folder ACL and service account permissions.

---

### 3.4 Rollout strategy for existing clients

1. **Release A (non-breaking prep):**
   - Deploy hardening + permission tables + seed catalog.
   - Keep old Admin/Cashier behavior as fallback.
2. **Release B (enforcement):**
   - Enable permission checks on sensitive endpoints.
   - Monitor denied-access logs.
3. **Release C (cleanup):**
   - Remove enum fallback.
   - Enforce full RBAC+PBAC model.

Per client rollout steps:
1. Pre-upgrade backup
2. Upgrade binaries
3. Auto migration + validation
4. Post-upgrade access smoke test (owner/admin/cashier)
5. Sign-off checklist.

---

### 3.5 Regression risk areas

1. Order completion authorization paths (`orders.complete`, `payments.create`).
2. Refund and cash adjustment permissions.
3. Shift open/close under branch restrictions.
4. User management endpoints after role model migration.
5. Frontend hidden/disabled controls vs backend 403 behavior.
6. Startup migration sequence with pre-backup lock handling.
7. Backup restore flow impact on running sessions.

Required regression suite:
- Financial transaction integration tests
- Permission matrix tests (role x feature x branch)
- Tenant boundary tests
- Backup/restore smoke tests
- Migration forward compatibility tests.

---

## FINAL EXECUTION PRIORITY

1. SQLite config + logging + exception mapping
2. Backup/restore + pre-migration backup
3. Integrity validation + cart persistence
4. Permission schema + policy enforcement + branch hardening
5. Legacy role deprecation

This order minimizes production risk first, then introduces authorization complexity safely.
