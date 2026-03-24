# 🧪 QA Test Execution Guide - Post-Migration Smoke Tests

## 📋 Overview

This guide provides step-by-step instructions for executing post-migration smoke tests to validate the branch inventory system.

---

## 🎯 Test Objectives

1. ✅ Verify core POS flows work after migration
2. ✅ Validate inventory decrements from BranchInventory (not Product.StockQuantity)
3. ✅ Test inventory transfers between branches
4. ✅ Verify branch-specific pricing
5. ✅ Test low stock alerts
6. ✅ Validate authorization controls

---

## 🛠️ Prerequisites

### 1. Backend Running
```bash
cd src/KasserPro.API
dotnet run
```
Expected: Server running on `https://localhost:5243`

### 2. Migration Executed
```bash
curl https://localhost:5243/api/migration/inventory-data/status
```
Expected: `"migrationExecuted": true`

### 3. Test Data Available
- At least 2 branches exist
- Products have inventory in BranchInventories table
- Admin user: admin@kasserpro.com / Admin@123
- Cashier user: ahmed@kasserpro.com / 123456

---

## 🚀 Quick Start (3 Options)

### Option 1: Swagger UI (Easiest)

1. Open `https://localhost:5243/swagger`
2. Click "Authorize" button
3. Login as Admin to get token
4. Execute each test endpoint manually
5. Verify responses match expected results

### Option 2: REST Client (VS Code)

1. Install "REST Client" extension in VS Code
2. Open `test-inventory-migration.http`
3. Update tokens after authentication
4. Click "Send Request" for each test
5. Review responses in VS Code

### Option 3: Postman

1. Import `test-inventory-migration.http` into Postman
2. Set environment variables for tokens
3. Execute requests in order
4. Verify responses

---

## 📝 Test Execution Steps

### Step 1: Authentication

**Get Admin Token**:
```http
POST /api/auth/login
{
  "email": "admin@kasserpro.com",
  "password": "Admin@123"
}
```

**Get Cashier Token**:
```http
POST /api/auth/login
{
  "email": "ahmed@kasserpro.com",
  "password": "123456"
}
```

**Save both tokens** for use in subsequent requests.

---

### Step 2: Verify Migration Status

```http
GET /api/migration/inventory-data/status
Authorization: Bearer {adminToken}
```

**Expected Response**:
```json
{
  "migrationExecuted": true,
  "branchInventoryRecords": 42,
  "productsWithOldStock": 0,
  "needsMigration": false,
  "message": "✅ Migration already executed"
}
```

**✅ PASS Criteria**: `migrationExecuted = true` and `productsWithOldStock = 0`

---

### Step 3: Test Order Creation (CRITICAL)

**3.1 Get inventory before order**:
```http
GET /api/inventory/branch/1
Authorization: Bearer {adminToken}
```

**Record**: Product 1 quantity (e.g., 200)

**3.2 Create order**:
```http
POST /api/orders
Authorization: Bearer {cashierToken}
{
  "orderType": "DineIn",
  "items": [
    {"productId": 1, "quantity": 3}
  ]
}
```

**Expected**: 200 OK, order created

**3.3 Get inventory after order**:
```http
GET /api/inventory/branch/1
Authorization: Bearer {adminToken}
```

**✅ PASS Criteria**:
- Product 1 quantity decreased by 3 (e.g., 200 → 197)
- Response includes updated `lastUpdatedAt`

**3.4 Verify Product.StockQuantity not used**:
```sql
SELECT Id, StockQuantity FROM Products WHERE Id = 1;
```

**✅ PASS Criteria**: `StockQuantity = 0` (not decremented)

---

### Step 4: Test Inventory Transfer

**4.1 Get initial inventory**:
```http
GET /api/inventory/branch/1  # Branch A
GET /api/inventory/branch/2  # Branch B
```

**Record**: Product 1 quantities in both branches

**4.2 Create transfer**:
```http
POST /api/inventory/transfer
Authorization: Bearer {adminToken}
{
  "fromBranchId": 1,
  "toBranchId": 2,
  "productId": 1,
  "quantity": 10,
  "reason": "Stock distribution"
}
```

**Expected**: 200 OK, returns `transferId`

**4.3 Approve transfer**:
```http
POST /api/inventory/transfer/{transferId}/approve
Authorization: Bearer {adminToken}
```

**Expected**: 200 OK, status = "Approved"

**4.4 Receive transfer**:
```http
POST /api/inventory/transfer/{transferId}/receive
Authorization: Bearer {adminToken}
```

**Expected**: 200 OK, status = "Completed"

**4.5 Verify final inventory**:
```http
GET /api/inventory/branch/1  # Should be -10
GET /api/inventory/branch/2  # Should be +10
```

**✅ PASS Criteria**:
- Branch A: Product 1 quantity decreased by 10
- Branch B: Product 1 quantity increased by 10
- Transfer status = "Completed"

---

### Step 5: Test Branch-Specific Pricing

**5.1 Set branch price override**:
```http
POST /api/inventory/branch-prices
Authorization: Bearer {adminToken}
{
  "branchId": 1,
  "productId": 1,
  "price": 30.00,
  "effectiveFrom": "2026-02-09T00:00:00Z"
}
```

**Expected**: 200 OK

**5.2 Verify price override**:
```http
GET /api/inventory/branch-prices/1
Authorization: Bearer {adminToken}
```

**✅ PASS Criteria**: Product 1 shows price = 30.00

**5.3 Create order (should use override price)**:
```http
POST /api/orders
Authorization: Bearer {cashierToken}
{
  "orderType": "DineIn",
  "items": [{"productId": 1, "quantity": 1}]
}
```

**Expected**: Order total calculated with 30.00 (not default 25.00)

**5.4 Remove price override**:
```http
DELETE /api/inventory/branch-prices/1/1
Authorization: Bearer {adminToken}
```

**Expected**: 200 OK

**5.5 Verify fallback to default**:
```http
GET /api/inventory/branch-prices/1
```

**✅ PASS Criteria**: Product 1 not listed (uses default price)

---

### Step 6: Test Low Stock Alert

**6.1 Check initial low stock**:
```http
GET /api/inventory/low-stock?branchId=1
Authorization: Bearer {adminToken}
```

**Expected**: Empty array or products already low

**6.2 Reduce inventory below reorder level**:
```http
POST /api/inventory/adjust
Authorization: Bearer {adminToken}
{
  "branchId": 1,
  "productId": 3,
  "quantityChange": -180,
  "reason": "Testing low stock"
}
```

**Expected**: 200 OK

**6.3 Check low stock again**:
```http
GET /api/inventory/low-stock?branchId=1
Authorization: Bearer {adminToken}
```

**✅ PASS Criteria**: Product 3 appears with `isLowStock: true`

**6.4 Restore inventory**:
```http
POST /api/inventory/adjust
Authorization: Bearer {adminToken}
{
  "branchId": 1,
  "productId": 3,
  "quantityChange": 180,
  "reason": "Restoring"
}
```

**6.5 Verify low stock cleared**:
```http
GET /api/inventory/low-stock?branchId=1
```

**✅ PASS Criteria**: Product 3 no longer in list

---

### Step 7: Test Authorization (CRITICAL)

**7.1 Cashier attempts transfer (should FAIL)**:
```http
POST /api/inventory/transfer
Authorization: Bearer {cashierToken}
{
  "fromBranchId": 1,
  "toBranchId": 2,
  "productId": 1,
  "quantity": 5,
  "reason": "Test"
}
```

**✅ PASS Criteria**: 403 Forbidden

**7.2 Cashier attempts adjustment (should FAIL)**:
```http
POST /api/inventory/adjust
Authorization: Bearer {cashierToken}
{
  "branchId": 1,
  "productId": 1,
  "quantityChange": 10,
  "reason": "Test"
}
```

**✅ PASS Criteria**: 403 Forbidden

**7.3 Cashier queries inventory (should SUCCEED)**:
```http
GET /api/inventory/branch/1
Authorization: Bearer {cashierToken}
```

**✅ PASS Criteria**: 200 OK, returns inventory data

**7.4 Admin creates transfer (should SUCCEED)**:
```http
POST /api/inventory/transfer
Authorization: Bearer {adminToken}
{
  "fromBranchId": 1,
  "toBranchId": 2,
  "productId": 2,
  "quantity": 5,
  "reason": "Admin transfer"
}
```

**✅ PASS Criteria**: 200 OK, transfer created

---

### Step 8: Test Migration Idempotency

```http
POST /api/migration/inventory-data
Authorization: Bearer {adminToken}
```

**✅ PASS Criteria**: 
- Response: `"alreadyMigrated": true`
- Message: "Migration already executed - skipping"
- No data changes

---

## 📊 Test Results Template

Copy this template to document your results:

```
=== POST-MIGRATION SMOKE TEST RESULTS ===

Date: _______________
Tester: _______________
Environment: Development

Test 1: Order Creation
Status: [ ] PASS [ ] FAIL
Notes: _________________________________

Test 2: Inventory Transfer
Status: [ ] PASS [ ] FAIL
Notes: _________________________________

Test 3: Branch Pricing
Status: [ ] PASS [ ] FAIL
Notes: _________________________________

Test 4: Low Stock Alert
Status: [ ] PASS [ ] FAIL
Notes: _________________________________

Test 5: Authorization
Status: [ ] PASS [ ] FAIL
Notes: _________________________________

Test 6: Migration Idempotency
Status: [ ] PASS [ ] FAIL
Notes: _________________________________

BLOCKING ISSUES FOUND:
_________________________________

NON-BLOCKING ISSUES FOUND:
_________________________________

OVERALL STATUS: [ ] APPROVED [ ] REJECTED
Sign-off: _______________
```

---

## 🐛 Common Issues & Solutions

### Issue: "Migration not executed"
**Solution**: Run `POST /api/migration/inventory-data` first

### Issue: "403 Forbidden"
**Solution**: Verify you're using correct token (Admin vs Cashier)

### Issue: "Inventory not decremented"
**Solution**: Check if order was completed successfully

### Issue: "Transfer fails"
**Solution**: Verify source branch has sufficient inventory

### Issue: "Price override not applied"
**Solution**: Check effectiveFrom date is not in future

---

## ✅ Sign-Off Checklist

Before approving for production:

- [ ] All 8 test scenarios executed
- [ ] Order creation works (Test 1) - BLOCKING
- [ ] Authorization works (Test 5) - BLOCKING
- [ ] No data integrity issues found
- [ ] No regressions in existing features
- [ ] Test results documented
- [ ] Issues logged (if any)

---

## 📞 Support

If you encounter issues during testing:

1. Check logs in console output
2. Verify database state with SQL queries
3. Review `POST_MIGRATION_SMOKE_TEST_REPORT.md`
4. Document issue with steps to reproduce

---

**Document Version**: 1.0  
**Last Updated**: February 9, 2026  
**Status**: Ready for Execution
