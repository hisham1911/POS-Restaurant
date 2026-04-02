# KasserPro — Final End-to-End Verification Prompt

Read before running:
1. `kasserpro-api-contract.md`
2. `architecture.md`
3. `kasserpro-bestpractices/SKILL.md`

You are a QA Engineer running the final verification pass on KasserPro.
Run ALL checks in order. Do NOT skip any. Report every result.

---

## PHASE 1: Backend Build & Static Analysis

```bash
# 1.1 Clean build
dotnet build backend/KasserPro.API/KasserPro.API.csproj -c Release
# EXPECTED: Build succeeded. 0 Error(s)

# 1.2 Run all existing integration tests
dotnet test backend/KasserPro.Tests/KasserPro.Tests.csproj \
  --logger "console;verbosity=normal"
# EXPECTED: All tests pass. 0 failed.

# 1.3 Check for remaining ad-hoc controller responses
rg -n "return Ok\(new \{|return BadRequest\(new \{|return NotFound\(new \{|StatusCode\(500, new \{" \
  backend/KasserPro.API/Controllers/
# EXPECTED: 0 matches

# 1.4 Check for single-arg Fail() without ErrorCode in services
rg -n 'ApiResponse<.*>\.Fail\("[^"]+"\)' \
  backend/KasserPro.Application/Services/
# EXPECTED: 0 matches (or only ones you intentionally accept)

# 1.5 Check for Product.StockQuantity (removed field)
rg -rn "StockQuantity" backend/KasserPro.Application/ backend/KasserPro.API/
# EXPECTED: 0 matches

# 1.6 Check for missing await using on transactions
rg -n "BeginTransactionAsync\(\);" backend/KasserPro.Application/
# EXPECTED: 0 matches (all should be "await using var transaction = await ...")

# 1.7 Check for silent empty catches
rg -n "catch\s*\{\s*\}" backend/KasserPro.API/ backend/KasserPro.Application/
# EXPECTED: 0 matches
```

---

## PHASE 2: Frontend Build & Static Analysis

```bash
# 2.1 TypeScript build — no errors
cd frontend && npx tsc --noEmit
# EXPECTED: 0 errors

# 2.2 Check for manual .success checks (should use unwrap())
rg -n "\.success\s*===|\.success\s*==" frontend/src/ | grep -v "toast\.success"
# EXPECTED: 0 matches

# 2.3 Check for any type usage
rg -n ": any" frontend/src/
# EXPECTED: 0 matches (or flag each one for review)

# 2.4 Check for hardcoded tax rate
rg -n "0\.14|taxRate\s*=\s*14\b|TAX_RATE" frontend/src/
# EXPECTED: 0 matches (removed from constants.ts)

# 2.5 Check for removed endpoint calls
rg -n "credentials|migrate-inventory|auth/refresh|auth/logout|auth/change-password" \
  frontend/src/
# EXPECTED: 0 matches

# 2.6 Check for removed product.stockQuantity
rg -n "stockQuantity\b" frontend/src/
# EXPECTED: 0 matches (field is removed/deprecated)

# 2.7 Check for fictional SignalR events
rg -n "ShiftWarning|LowStockAlert|MaintenanceStarted|MaintenanceEnded|NotifyDevice" \
  frontend/src/ | grep -i "signalr\|hub\|invoke\|on("
# EXPECTED: 0 matches as SignalR events (UI components are fine)
```

---

## PHASE 3: Live API Scenario Tests (from Terminal)

Start the backend first:
```bash
cd backend && dotnet run --project KasserPro.API/KasserPro.API.csproj
# Wait for: "Now listening on http://localhost:5243"
```

Then run each scenario:

### Scenario A: Authentication Flow

```bash
BASE="http://localhost:5243"

# A1: Login with admin credentials
TOKEN=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@kasserpro.com","password":"Admin@123"}' | \
  python -c "import sys,json; r=json.load(sys.stdin); print(r['data']['token'] if r['success'] else 'FAILED:'+r['message'])")

echo "TOKEN: $TOKEN"
# EXPECTED: JWT token string (not FAILED)

# A2: Get current user info
curl -s -X GET "$BASE/api/auth/me" \
  -H "Authorization: Bearer $TOKEN" | python -m json.tool
# EXPECTED: success=true, data contains user info with role, tenantId, branchId

# A3: Access protected endpoint WITHOUT token → 401
curl -s -X GET "$BASE/api/products" | python -m json.tool
# EXPECTED: 401 Unauthorized

# A4: Login with wrong password → errorCode present
curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@kasserpro.com","password":"wrongpassword"}' | python -m json.tool
# EXPECTED: success=false, errorCode is NOT null/empty
```

### Scenario B: ApiResponse<T> Contract Verification

```bash
# Get branchId from the me endpoint
BRANCH_ID=$(curl -s "$BASE/api/auth/me" \
  -H "Authorization: Bearer $TOKEN" | \
  python -c "import sys,json; r=json.load(sys.stdin); print(r['data']['branchId'])")
echo "BRANCH_ID: $BRANCH_ID"

# B1: Products list — must return ApiResponse<PagedResult<ProductDto>>
curl -s "$BASE/api/products" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | python -m json.tool
# EXPECTED: {"success":true,"data":{"items":[...],"totalCount":...,"page":1,...}}

# B2: Product not found — must return errorCode, not null
curl -s "$BASE/api/products/999999" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | python -m json.tool
# EXPECTED: {"success":false,"errorCode":"PRODUCT_NOT_FOUND","message":"..."}
# FAIL if errorCode is null or missing

# B3: Branches endpoint — must be wrapped in ApiResponse<T>
curl -s "$BASE/api/branches" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | python -m json.tool
# EXPECTED: {"success":true,"data":[...]} — NOT raw array

# B4: Health check — AllowAnonymous, must NOT expose dbPath
HEALTH=$(curl -s "$BASE/api/health")
echo $HEALTH | python -m json.tool
echo $HEALTH | python -c "import sys,json; r=json.load(sys.stdin); \
  assert 'dbPath' not in str(r), 'FAIL: dbPath exposed in anonymous health!'; \
  print('PASS: dbPath not exposed')"
```

### Scenario C: Financial Flow (Core Business)

```bash
# C1: Check current shift status
curl -s "$BASE/api/shifts/current" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | python -m json.tool
# EXPECTED: success=true, data is null (no open shift) or shift object

# C2: Open a shift (if none open)
SHIFT=$(curl -s -X POST "$BASE/api/shifts/open" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" \
  -H "Content-Type: application/json" \
  -d '{"openingBalance":500}')
echo $SHIFT | python -m json.tool
# EXPECTED: success=true, data contains shift object

SHIFT_STATUS=$(echo $SHIFT | python -c "import sys,json; r=json.load(sys.stdin); print(r['success'])")

# C3: Try to open shift AGAIN → should fail with SHIFT_ALREADY_OPEN
curl -s -X POST "$BASE/api/shifts/open" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" \
  -H "Content-Type: application/json" \
  -d '{"openingBalance":500}' | python -m json.tool
# EXPECTED: success=false, errorCode="SHIFT_ALREADY_OPEN"

# C4: Create an order
ORDER=$(curl -s -X POST "$BASE/api/orders" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: $(python -c 'import uuid; print(uuid.uuid4())')" \
  -d '{"orderType":"Takeaway"}')
echo $ORDER | python -m json.tool
# EXPECTED: success=true, data is order id (number)

ORDER_ID=$(echo $ORDER | python -c "import sys,json; r=json.load(sys.stdin); print(r['data'] if r['success'] else 'FAILED')")
echo "ORDER_ID: $ORDER_ID"

# C5: Get first product id for adding to order
PRODUCT_ID=$(curl -s "$BASE/api/products?pageSize=1" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | \
  python -c "import sys,json; r=json.load(sys.stdin); items=r['data']['items']; print(items[0]['id'] if items else 'NO_PRODUCTS')")
echo "PRODUCT_ID: $PRODUCT_ID"

# C6: Add item to order
curl -s -X POST "$BASE/api/orders/$ORDER_ID/items" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: $(python -c 'import uuid; print(uuid.uuid4())')" \
  -d "{\"productId\":$PRODUCT_ID,\"quantity\":1}" | python -m json.tool
# EXPECTED: success=true
```

### Scenario D: Security Verification (Multi-Tenant Isolation)

```bash
# D1: Login as Cashier
CASHIER_TOKEN=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"ahmed@kasserpro.com","password":"123456"}' | \
  python -c "import sys,json; r=json.load(sys.stdin); print(r['data']['token'] if r['success'] else 'FAILED')")

# D2: Cashier tries to access admin-only endpoint → 403
curl -s "$BASE/api/system/users" \
  -H "Authorization: Bearer $CASHIER_TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | python -m json.tool
# EXPECTED: 403 Forbidden

# D3: No token → 401
curl -s "$BASE/api/orders" | python -m json.tool
# EXPECTED: 401

# D4: Wrong Branch-Id → 403
curl -s "$BASE/api/orders" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: 99999" | python -m json.tool
# EXPECTED: 403 BRANCH_ACCESS_DENIED

# D5: PaymentsController — verify TenantId isolation (no cross-tenant leak)
# Try to get payments for an order from different context
curl -s "$BASE/api/payments?orderId=1" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | python -m json.tool
# EXPECTED: success=true with filtered results (not ALL payments from DB)
```

### Scenario E: Backup System

```bash
# E1: Create manual backup
BACKUP=$(curl -s -X POST "$BASE/api/admin/backup" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" \
  -H "Content-Type: application/json" \
  -d '{}')
echo $BACKUP | python -m json.tool
# EXPECTED: success=true, data wrapped in ApiResponse<BackupResult>
# FAIL if returns raw object without success/errorCode structure

# E2: List backups — must be ApiResponse<T> not raw array
curl -s "$BASE/api/admin/backups" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" | python -m json.tool
# EXPECTED: {"success":true,"data":[...]} NOT raw [...]

# E3: Backup not found → proper errorCode
curl -s "$BASE/api/admin/restore/nonexistent-backup.db" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Branch-Id: $BRANCH_ID" \
  -H "Content-Type: application/json" \
  -d '{}' | python -m json.tool
# EXPECTED: success=false, errorCode="BACKUP_NOT_FOUND"
```

---

## PHASE 4: Contract Completeness Check

Run this to verify contract vs code alignment:

```bash
# 4.1 Count controllers in code
ls backend/KasserPro.API/Controllers/*.cs | wc -l
# Record this number

# 4.2 Check every controller has [HasPermission] or documented exception
rg -l "\[Authorize\]" backend/KasserPro.API/Controllers/ | \
  xargs grep -L "HasPermission"
# EXPECTED: Only controllers that INTENTIONALLY use role-based auth (AdminController, SystemController)
# Flag any others

# 4.3 Verify SignalR hub has only real events
cat backend/KasserPro.API/Hubs/DeviceHub.cs
# EXPECTED: Only PrintReceipt and PrintCompleted events
# No: ShiftWarning, LowStockAlert, MaintenanceStarted, etc.

# 4.4 Verify ErrorCodes includes new backup codes
rg "BACKUP_NOT_FOUND|BACKUP_FAILED|RESTORE_FAILED" \
  backend/KasserPro.Application/Common/ErrorCodes.cs
# EXPECTED: All 3 found

# 4.5 Verify no Guid vs int mismatch on product IDs
rg "ApiResponse<Guid>" backend/KasserPro.API/Controllers/ProductsController.cs
# EXPECTED: 0 matches — products use int not Guid
rg "ApiResponse<int>" backend/KasserPro.API/Controllers/ProductsController.cs
# EXPECTED: matches for POST endpoint
```

---

## PHASE 5: Frontend Runtime Verification

```bash
# 5.1 Start frontend dev server
cd frontend && npm run dev
# Wait for: "Local: http://localhost:3000"

# 5.2 Open browser and run these manual checks:
```

**Manual Browser Checklist:**

```
Login Flow:
- [ ] Login with admin@kasserpro.com / Admin@123 → success, redirects to dashboard
- [ ] Login with wrong password → shows error message (from errorCode, not message)
- [ ] Logout → clears token, redirects to login

POS Flow:
- [ ] Open shift → success
- [ ] Search for product → shows product WITHOUT stockQuantity field confusion
- [ ] Add product to cart → updates cart correctly
- [ ] Complete order with Cash payment → success, shows receipt

Products Page:
- [ ] Products list loads correctly
- [ ] Stock column shows currentBranchStock (with deprecation note visible in code)
- [ ] Creating product without price → shows proper Arabic error from errorCode

Permissions:
- [ ] Login as Cashier (ahmed@kasserpro.com / 123456)
- [ ] Cashier cannot see Admin-only menu items
- [ ] Cashier can create orders (has PosSell permission)

Error Handling:
- [ ] Disconnect internet → shows error toast, NOT "undefined" or raw object
- [ ] Force 401: clear token in localStorage, make request → redirects to login
```

---

## PHASE 6: Final Report

After running all phases, generate a report in this format:

```
## KasserPro Verification Report
Date: [today]
Tester: [AI agent name]

### Phase 1 — Backend Static: [PASS/FAIL]
- Failed checks: [list or "none"]

### Phase 2 — Frontend Static: [PASS/FAIL]
- Failed checks: [list or "none"]

### Phase 3 — Live API Scenarios: [PASS/FAIL]
- Scenario A (Auth): [PASS/FAIL] — [notes]
- Scenario B (Contract): [PASS/FAIL] — [notes]
- Scenario C (Financial): [PASS/FAIL] — [notes]
- Scenario D (Security): [PASS/FAIL] — [notes]
- Scenario E (Backup): [PASS/FAIL] — [notes]

### Phase 4 — Contract Completeness: [PASS/FAIL]
- Failed checks: [list or "none"]

### Phase 5 — Frontend Runtime: [PASS/FAIL]
- Failed checks: [list or "none"]

### Overall Status: [PRODUCTION READY / NEEDS FIXES]

### Remaining Issues (if any):
1. [issue] — [file:line] — [severity]

### What's Working Well:
- [positive findings]
```
