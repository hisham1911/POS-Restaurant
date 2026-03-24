# 🧪 POST-MIGRATION SMOKE TEST REPORT

**Test Date**: February 9, 2026  
**Tester**: Senior QA Engineer  
**System**: KasserPro - Branch Inventory System  
**Migration Status**: ✅ Executed Successfully  
**Build Status**: ✅ Clean (0 errors, 0 warnings)

---

## 📋 Test Objectives

Validate that core POS flows work correctly after inventory data migration from `Product.StockQuantity` to `BranchInventory`.

---

## 🎯 Test Scenarios

### ✅ Scenario 1: Create Order in Branch A
**Objective**: Verify inventory decrements from BranchInventory, not Product.StockQuantity

**Pre-conditions**:
- Migration executed successfully
- Branch A has inventory
- Active shift exists

**Test Steps**:
1. Query BranchInventory before order
2. Query Product.StockQuantity before order
3. Create order with 2 items (Qty: 3 each)
4. Complete order
5. Query BranchInventory after order
6. Query Product.StockQuantity after order

**Expected Results**:
- ✅ BranchInventory.Quantity decreases by 3 for each product
- ✅ Product.StockQuantity remains 0 (not used)
- ✅ StockMovement records created with Type=Sale
- ✅ Order completes successfully

**SQL Verification**:
```sql
-- Before order
SELECT ProductId, Quantity FROM BranchInventories WHERE BranchId = 1 AND ProductId IN (1, 2);
-- Product 1: 200, Product 2: 180

-- After order
SELECT ProductId, Quantity FROM BranchInventories WHERE BranchId = 1 AND ProductId IN (1, 2);
-- Product 1: 197, Product 2: 177 ✓

-- Verify Product.StockQuantity not used
SELECT Id, StockQuantity FROM Products WHERE Id IN (1, 2);
-- Both should be 0 ✓

-- Verify StockMovement
SELECT * FROM StockMovements 
WHERE ProductId IN (1, 2) 
AND Type = 1 -- Sale
ORDER BY CreatedAt DESC LIMIT 2;
-- Should show -3 quantity for each ✓
```

**Status**: ⏳ PENDING EXECUTION

---

### ✅ Scenario 2: Transfer Inventory Between Branches
**Objective**: Verify transactional inventory transfer from Branch A → Branch B

**Pre-conditions**:
- Two branches exist
- Branch A has sufficient inventory
- User has Admin role

**Test Steps**:
1. Query inventory in both branches before transfer
2. Create transfer request (Branch A → Branch B, Product 1, Qty: 10)
3. Approve transfer (deducts from Branch A)
4. Receive transfer (adds to Branch B)
5. Query inventory in both branches after transfer
6. Verify StockMovement records

**Expected Results**:
- ✅ Transfer created with Status=Pending
- ✅ After approval: Branch A quantity -10, Status=Approved
- ✅ After receive: Branch B quantity +10, Status=Completed
- ✅ StockMovement records created for both branches
- ✅ Transaction is atomic (all or nothing)

**API Calls**:
```bash
# 1. Check inventory before
GET /api/inventory/branch/1  # Branch A
GET /api/inventory/branch/2  # Branch B

# 2. Create transfer
POST /api/inventory/transfer
{
  "fromBranchId": 1,
  "toBranchId": 2,
  "productId": 1,
  "quantity": 10,
  "reason": "Stock distribution"
}
# Expected: 201 Created, transferId returned

# 3. Approve transfer
POST /api/inventory/transfer/{transferId}/approve
# Expected: 200 OK, Branch A -10

# 4. Receive transfer
POST /api/inventory/transfer/{transferId}/receive
# Expected: 200 OK, Branch B +10

# 5. Verify final state
GET /api/inventory/transfer/{transferId}
# Expected: Status = "Completed"
```

**Status**: ⏳ PENDING EXECUTION

---

### ✅ Scenario 3: Branch-Specific Pricing
**Objective**: Verify price override and fallback logic

**Pre-conditions**:
- Product has default price = 25.00
- Branch B has no price override

**Test Steps**:
1. Set branch-specific price for Product 1 in Branch A (30.00)
2. Query effective price for Product 1 in Branch A
3. Query effective price for Product 1 in Branch B
4. Create order in Branch A (should use 30.00)
5. Create order in Branch B (should use 25.00)
6. Remove branch price override
7. Query effective price again (should fallback to 25.00)

**Expected Results**:
- ✅ Branch A uses override price (30.00)
- ✅ Branch B uses default price (25.00)
- ✅ After removal, Branch A falls back to default (25.00)
- ✅ Orders calculate totals correctly

**API Calls**:
```bash
# 1. Set branch price
POST /api/inventory/branch-prices
{
  "branchId": 1,
  "productId": 1,
  "price": 30.00,
  "effectiveFrom": "2026-02-09T00:00:00Z"
}
# Expected: 200 OK

# 2. Get branch prices
GET /api/inventory/branch-prices/1
# Expected: Shows Product 1 with price 30.00

# 3. Create order in Branch A
POST /api/orders
{
  "items": [{"productId": 1, "quantity": 1}]
}
# Expected: Item price = 30.00

# 4. Remove branch price
DELETE /api/inventory/branch-prices/1/1
# Expected: 200 OK

# 5. Verify fallback
GET /api/inventory/branch-prices/1
# Expected: Empty or Product 1 not listed
```

**Status**: ⏳ PENDING EXECUTION

---

### ✅ Scenario 4: Low Stock Alert
**Objective**: Verify low stock detection and alerts

**Pre-conditions**:
- Product has ReorderLevel = 10
- Product currently has Quantity = 50

**Test Steps**:
1. Query low stock items (should be empty)
2. Adjust inventory to 9 (below reorder level)
3. Query low stock items (should include product)
4. Adjust inventory back to 50
5. Query low stock items (should be empty again)

**Expected Results**:
- ✅ Low stock query returns empty when Quantity > ReorderLevel
- ✅ Low stock query includes product when Quantity ≤ ReorderLevel
- ✅ IsLowStock flag is correctly calculated

**API Calls**:
```bash
# 1. Check low stock (should be empty)
GET /api/inventory/low-stock?branchId=1
# Expected: []

# 2. Reduce inventory below reorder level
POST /api/inventory/adjust
{
  "branchId": 1,
  "productId": 1,
  "quantityChange": -41,  # 50 - 41 = 9
  "reason": "Testing low stock alert"
}
# Expected: 200 OK

# 3. Check low stock again
GET /api/inventory/low-stock?branchId=1
# Expected: [{"productId": 1, "quantity": 9, "reorderLevel": 10, "isLowStock": true}]

# 4. Restore inventory
POST /api/inventory/adjust
{
  "branchId": 1,
  "productId": 1,
  "quantityChange": 41,
  "reason": "Restoring stock"
}

# 5. Verify low stock cleared
GET /api/inventory/low-stock?branchId=1
# Expected: []
```

**Status**: ⏳ PENDING EXECUTION

---

### ✅ Scenario 5: Unauthorized Access Control
**Objective**: Ensure non-admin users cannot perform admin-only operations

**Pre-conditions**:
- Cashier user authenticated (ahmed@kasserpro.com)
- Admin user authenticated (admin@kasserpro.com)

**Test Steps**:
1. As Cashier: Attempt to create inventory transfer
2. As Cashier: Attempt to adjust inventory
3. As Cashier: Attempt to set branch price
4. As Cashier: Query inventory (should succeed)
5. As Admin: Create inventory transfer (should succeed)

**Expected Results**:
- ❌ Cashier cannot create transfer (403 Forbidden)
- ❌ Cashier cannot adjust inventory (403 Forbidden)
- ❌ Cashier cannot set branch price (403 Forbidden)
- ✅ Cashier can query inventory (200 OK)
- ✅ Admin can perform all operations (200 OK)

**API Calls**:
```bash
# As Cashier (ahmed@kasserpro.com)
POST /api/inventory/transfer
Authorization: Bearer <cashier-token>
# Expected: 403 Forbidden

POST /api/inventory/adjust
Authorization: Bearer <cashier-token>
# Expected: 403 Forbidden

POST /api/inventory/branch-prices
Authorization: Bearer <cashier-token>
# Expected: 403 Forbidden

GET /api/inventory/branch/1
Authorization: Bearer <cashier-token>
# Expected: 200 OK ✓

# As Admin (admin@kasserpro.com)
POST /api/inventory/transfer
Authorization: Bearer <admin-token>
# Expected: 200 OK ✓
```

**Status**: ⏳ PENDING EXECUTION

---

## 🔍 Additional Verification Tests

### Test 6: Migration Idempotency
**Objective**: Verify migration cannot be run twice

**Test Steps**:
```bash
POST /api/migration/inventory-data
# Expected: "Migration already executed - skipping"
```

**Status**: ⏳ PENDING EXECUTION

---

### Test 7: Stock Movement Audit Trail
**Objective**: Verify all inventory changes are logged

**Test Steps**:
1. Perform various operations (order, transfer, adjustment)
2. Query StockMovements table
3. Verify all operations logged with correct Type

**SQL Verification**:
```sql
SELECT 
    Type,
    Quantity,
    ReferenceType,
    Reason,
    BalanceBefore,
    BalanceAfter,
    CreatedAt
FROM StockMovements
WHERE ProductId = 1 AND BranchId = 1
ORDER BY CreatedAt DESC
LIMIT 10;
```

**Expected**: All operations logged with correct types (Sale, Transfer, Adjustment)

**Status**: ⏳ PENDING EXECUTION

---

### Test 8: Concurrent Order Processing
**Objective**: Verify no race conditions in inventory updates

**Test Steps**:
1. Create 2 simultaneous orders for same product
2. Verify both complete successfully
3. Verify inventory decremented correctly (no lost updates)

**Expected**: 
- Both orders complete
- Inventory = Initial - (Order1.Qty + Order2.Qty)
- No deadlocks or race conditions

**Status**: ⏳ PENDING EXECUTION

---

## 📊 Test Execution Plan

### Prerequisites
1. ✅ Backend running on port 5243
2. ✅ Database migrated successfully
3. ✅ Test data seeded
4. ✅ Admin and Cashier users exist
5. ✅ At least 2 branches exist

### Execution Order
1. Scenario 5 (Authorization) - Verify security first
2. Scenario 1 (Order Creation) - Core POS flow
3. Scenario 4 (Low Stock) - Alert system
4. Scenario 3 (Pricing) - Price override logic
5. Scenario 2 (Transfer) - Inter-branch operations
6. Additional Tests - Edge cases

### Tools Required
- Postman or curl for API testing
- SQL client for database verification
- Admin and Cashier JWT tokens

---

## 🚨 Known Risks

### High Risk
- **Inventory race conditions**: Multiple simultaneous orders
- **Transaction rollback**: Transfer approval/receive failures
- **Data integrity**: Stock totals mismatch

### Medium Risk
- **Authorization bypass**: Role-based access control
- **Price calculation**: Branch override not applied
- **Low stock alerts**: False positives/negatives

### Low Risk
- **Performance**: Slow queries on large datasets
- **Logging**: Missing audit trail entries

---

## 📝 Test Execution Instructions

### Step 1: Start Backend
```bash
cd src/KasserPro.API
dotnet run
```

### Step 2: Verify Migration Status
```bash
curl https://localhost:5243/api/migration/inventory-data/status
```

Expected:
```json
{
  "migrationExecuted": true,
  "branchInventoryRecords": 42,
  "productsWithOldStock": 0,
  "needsMigration": false
}
```

### Step 3: Authenticate
```bash
# Get Admin token
POST /api/auth/login
{
  "email": "admin@kasserpro.com",
  "password": "Admin@123"
}

# Get Cashier token
POST /api/auth/login
{
  "email": "ahmed@kasserpro.com",
  "password": "123456"
}
```

### Step 4: Execute Test Scenarios
Follow each scenario's API calls in order, verifying expected results.

### Step 5: Document Results
Update this report with PASS/FAIL for each scenario.

---

## 📋 Test Results Summary

| Scenario | Status | Blocking | Notes |
|----------|--------|----------|-------|
| 1. Order Creation | ⏳ PENDING | YES | Core POS flow |
| 2. Inventory Transfer | ⏳ PENDING | NO | Admin feature |
| 3. Branch Pricing | ⏳ PENDING | NO | Optional feature |
| 4. Low Stock Alert | ⏳ PENDING | NO | Monitoring feature |
| 5. Authorization | ⏳ PENDING | YES | Security critical |
| 6. Migration Idempotency | ⏳ PENDING | NO | Safety check |
| 7. Audit Trail | ⏳ PENDING | NO | Compliance |
| 8. Concurrency | ⏳ PENDING | YES | Data integrity |

**Overall Status**: ⏳ READY FOR EXECUTION

---

## 🐛 Issues Found

### Blocking Issues
*None found yet - pending test execution*

### Non-Blocking Issues
*None found yet - pending test execution*

### Observations
*To be filled during testing*

---

## ✅ Sign-Off Criteria

Before approving for production:
- [ ] All blocking scenarios PASS
- [ ] No data integrity issues
- [ ] Authorization working correctly
- [ ] Core POS flow (order creation) works
- [ ] No regressions in existing features

---

## 📞 Next Steps

1. **Execute all test scenarios** using Swagger or Postman
2. **Document results** (PASS/FAIL) for each scenario
3. **Report any issues** found during testing
4. **Verify fixes** if issues are found
5. **Sign off** when all blocking tests pass

---

**Report Status**: 📝 DRAFT - Awaiting Test Execution  
**Prepared By**: Senior QA Engineer  
**Review Required**: Yes  
**Approval Required**: Yes (after execution)
