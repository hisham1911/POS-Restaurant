# 📦 Inventory Data Migration Guide

## Overview

This guide explains how to execute the **ONE-TIME data migration** that moves existing inventory from `Product.StockQuantity` to the new `BranchInventory` system.

---

## ⚠️ Important Information

### What This Migration Does

1. ✅ Reads all existing `Product.StockQuantity` values
2. ✅ Creates `BranchInventory` records for each product in the default branch
3. ✅ Sets `Product.StockQuantity = 0` (marks as migrated)
4. ✅ Preserves all stock data (zero data loss)
5. ✅ Validates totals match before committing

### Safety Features

- **Transactional**: All changes in one transaction - rolls back on any error
- **Idempotent**: Safe to run multiple times - checks if already executed
- **Validated**: Verifies stock totals match before/after
- **Logged**: Complete audit trail of the migration process
- **No Schema Changes**: Only data migration, no schema modifications

---

## 🚀 How to Execute

### Option 1: Via API (Recommended)

1. **Start the backend**:
   ```bash
   cd src/KasserPro.API
   dotnet run
   ```

2. **Check migration status**:
   ```bash
   GET https://localhost:5243/api/migration/inventory-data/status
   ```
   
   Response:
   ```json
   {
     "migrationExecuted": false,
     "branchInventoryRecords": 0,
     "productsWithOldStock": 42,
     "needsMigration": true,
     "message": "⚠️ Migration needed - products have stock in old format"
   }
   ```

3. **Execute migration** (Admin only):
   ```bash
   POST https://localhost:5243/api/migration/inventory-data
   Authorization: Bearer <admin-token>
   ```

4. **Review results**:
   ```json
   {
     "success": true,
     "alreadyMigrated": false,
     "message": "Migration completed successfully",
     "summary": "✅ INVENTORY DATA MIGRATION COMPLETED SUCCESSFULLY...",
     "data": {
       "productsMigrated": 42,
       "inventoriesCreated": 42,
       "productsWithStock": 35,
       "totalStockBefore": 8450,
       "totalStockAfter": 8450,
       "durationMs": 234
     }
   }
   ```

### Option 2: Via Swagger UI

1. Navigate to `https://localhost:5243/swagger`
2. Authenticate as Admin
3. Find `Migration` section
4. Execute `POST /api/migration/inventory-data`
5. Review the response

### Option 3: Via Code (for testing)

```csharp
var migration = new InventoryDataMigration(context, logger);
var result = await migration.ExecuteAsync();

if (result.Success)
{
    Console.WriteLine(result.GetSummary());
}
```

---

## 📋 Migration Logic

### Step-by-Step Process

1. **Check if already executed**
   - If `BranchInventories` table has data → Skip migration
   - Returns success with "already migrated" message

2. **Get all tenants**
   - Processes each tenant separately
   - Ensures multi-tenancy isolation

3. **Identify default branch**
   - Uses first branch (ordered by ID) as default
   - Logs branch name and ID

4. **Get all products**
   - Retrieves all products for the tenant
   - Includes products with zero stock

5. **Calculate total stock before**
   - Sums all `Product.StockQuantity` values
   - Used for validation later

6. **Create BranchInventory records**
   ```csharp
   foreach (var product in products)
   {
       var inventory = new BranchInventory
       {
           TenantId = tenant.Id,
           BranchId = defaultBranch.Id,
           ProductId = product.Id,
           Quantity = product.StockQuantity ?? 0,
           ReorderLevel = product.ReorderPoint ?? 10,
           LastUpdatedAt = product.LastStockUpdate ?? DateTime.UtcNow
       };
       
       // Mark product as migrated
       product.StockQuantity = 0;
   }
   ```

7. **Save changes**
   - All changes saved in one transaction

8. **Validate migration**
   - ✅ Total stock before == Total stock after
   - ✅ No duplicate (BranchId, ProductId) records
   - ❌ Rollback if validation fails

9. **Commit transaction**
   - Only commits if all validations pass

---

## ✅ Validation Checks

### Automatic Validations

1. **Stock Total Match**
   ```
   SUM(Product.StockQuantity) == SUM(BranchInventory.Quantity)
   ```
   - Ensures no stock was lost or duplicated

2. **No Duplicates**
   ```sql
   SELECT BranchId, ProductId, COUNT(*)
   FROM BranchInventories
   GROUP BY BranchId, ProductId
   HAVING COUNT(*) > 1
   ```
   - Ensures unique constraint is maintained

3. **Transaction Integrity**
   - All changes in one transaction
   - Automatic rollback on any error

### Manual Verification (Optional)

After migration, you can verify:

```sql
-- Check total stock matches
SELECT 
    (SELECT SUM(Quantity) FROM BranchInventories) as BranchTotal,
    (SELECT SUM(StockQuantity) FROM Products) as ProductTotal;
-- ProductTotal should be 0 after migration

-- Check all products have inventory records
SELECT COUNT(*) FROM Products WHERE TenantId = 1;
SELECT COUNT(*) FROM BranchInventories WHERE TenantId = 1;
-- Counts should match

-- Check for duplicates
SELECT BranchId, ProductId, COUNT(*) as Count
FROM BranchInventories
GROUP BY BranchId, ProductId
HAVING COUNT(*) > 1;
-- Should return 0 rows
```

---

## 🔄 What Happens to Existing Data

### Products Table
- **Before**: `StockQuantity = 150`
- **After**: `StockQuantity = 0` (marked as migrated)
- **Note**: Column remains for backward compatibility

### BranchInventories Table (New)
- **Created**: One record per product per branch
- **Quantity**: Copied from `Product.StockQuantity`
- **ReorderLevel**: Copied from `Product.ReorderPoint` or default 10

### Orders & Sales
- ✅ **Continue working normally**
- OrderService uses legacy compatibility methods
- Stock deducted from `BranchInventory` automatically

---

## 🎯 Expected Results

### Successful Migration

```
✅ INVENTORY DATA MIGRATION COMPLETED SUCCESSFULLY

📊 Summary:
- Products migrated: 42
- Inventories created: 42
- Products with stock: 35
- Total stock before: 8450
- Total stock after: 8450
- Duration: 234ms

✅ Validation:
- Stock totals match: ✓
- No duplicate records: ✓
- Transaction committed: ✓
```

### Already Migrated

```
✅ Migration already executed - no action needed
```

### Failed Migration

```
❌ Migration failed: Stock mismatch (difference: 5)

The migration was rolled back. No changes were made.
```

---

## 🚨 Troubleshooting

### Issue: "Migration already executed"

**Cause**: BranchInventories table already has data

**Solution**: This is normal! The migration is idempotent. No action needed.

### Issue: "Stock mismatch"

**Cause**: Validation failed - totals don't match

**Solution**: 
1. Check logs for details
2. Verify database integrity
3. Contact support if issue persists

### Issue: "No branches found for tenant"

**Cause**: Tenant has no branches

**Solution**: Create at least one branch before migrating

### Issue: "Duplicate records found"

**Cause**: Unique constraint violation

**Solution**: 
1. Check for existing BranchInventory records
2. Clear table if needed: `DELETE FROM BranchInventories`
3. Re-run migration

---

## 📊 Performance

### Expected Duration

| Products | Duration |
|----------|----------|
| 50 | ~200ms |
| 100 | ~400ms |
| 500 | ~2s |
| 1000 | ~4s |

### Database Impact

- **Reads**: All Products (1 query per tenant)
- **Writes**: N BranchInventory inserts + N Product updates
- **Transaction**: Single transaction for all changes
- **Locks**: Row-level locks during transaction

---

## 🔐 Security

- **Admin Only**: Only users with Admin role can execute
- **Logged**: All actions logged with timestamps
- **Audited**: Complete audit trail in logs
- **Safe**: Transactional with automatic rollback

---

## 📝 Post-Migration Checklist

After successful migration:

- [ ] Verify migration status endpoint shows `migrationExecuted: true`
- [ ] Test creating a new order (should work normally)
- [ ] Test inventory queries via API
- [ ] Check low stock alerts
- [ ] Verify reports show correct stock levels
- [ ] Monitor logs for any issues

---

## 🎓 Example Scenarios

### Scenario 1: Fresh System (No Stock)

```
Before: 0 products with stock
After: 0 BranchInventory records created
Result: ✅ No migration needed
```

### Scenario 2: Existing Stock

```
Before: 42 products, 35 with stock, total 8450 units
After: 42 BranchInventory records, total 8450 units
Result: ✅ Migration successful
```

### Scenario 3: Multiple Branches

```
Before: 2 branches, 42 products
After: 42 BranchInventory records in Branch 1 (default)
Note: Other branches start with 0 stock
Action: Use inventory transfers to distribute stock
```

---

## 🔗 Related Documentation

- `BRANCH_INVENTORY_BACKEND_COMPLETE.md` - Backend implementation details
- `NEXT_STEPS_BRANCH_INVENTORY.md` - What to do after migration
- `BRANCH_INVENTORY_PROGRESS_REPORT.md` - Overall progress

---

## ❓ FAQ

**Q: Can I run this migration multiple times?**  
A: Yes! It's idempotent. If already executed, it returns immediately.

**Q: Will this break existing orders?**  
A: No! OrderService has legacy compatibility methods.

**Q: What if I have multiple branches?**  
A: Stock goes to the first branch. Use inventory transfers to distribute.

**Q: Can I rollback the migration?**  
A: The migration itself rolls back on error. To manually rollback:
1. Copy `BranchInventory.Quantity` back to `Product.StockQuantity`
2. Delete `BranchInventory` records

**Q: How long does it take?**  
A: Usually < 1 second for typical datasets (< 100 products)

**Q: Is my data safe?**  
A: Yes! Transactional with validation. Rolls back on any issue.

---

**Created**: February 9, 2026  
**Status**: Ready for execution  
**Build**: ✅ SUCCESS
