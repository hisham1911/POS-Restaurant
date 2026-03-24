-- ============================================
-- KasserPro: Fix Missing BranchInventory Records
-- Run this ONCE to fix products not showing in inventory
-- ============================================

-- Step 1: Check current state
.print '🔍 Checking current state...'
.print ''

SELECT '✓ Products with StockQuantity > 0: ' || COUNT(*) as Status 
FROM Products 
WHERE StockQuantity > 0 AND IsActive = 1;

SELECT '✓ Products in BranchInventory: ' || COUNT(DISTINCT ProductId) as Status
FROM BranchInventories;

SELECT '⚠ Products MISSING from BranchInventory: ' || COUNT(*) as Status
FROM Products p
WHERE p.IsActive = 1 
  AND p.TrackInventory = 1
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id
  );

.print ''
.print '📋 Sample of products that will be fixed:'
SELECT '  - ' || p.Name || ' (Stock: ' || COALESCE(p.StockQuantity, 0) || ')' as Product
FROM Products p
WHERE p.IsActive = 1 
  AND p.TrackInventory = 1
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id
  )
LIMIT 5;

.print ''
.print '🔧 Creating missing BranchInventory records...'

-- Step 2: Create missing records
INSERT INTO BranchInventories (TenantId, BranchId, ProductId, Quantity, ReorderLevel, LastUpdatedAt, CreatedAt, UpdatedAt, IsDeleted)
SELECT 
    p.TenantId,
    b.Id as BranchId,
    p.Id as ProductId,
    COALESCE(p.StockQuantity, 0) as Quantity,
    COALESCE(p.LowStockThreshold, 10) as ReorderLevel,
    COALESCE(p.LastStockUpdate, datetime('now')) as LastUpdatedAt,
    datetime('now') as CreatedAt,
    datetime('now') as UpdatedAt,
    0 as IsDeleted
FROM Products p
CROSS JOIN Branches b
WHERE p.IsActive = 1
  AND p.TrackInventory = 1
  AND p.TenantId = b.TenantId
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id AND bi.BranchId = b.Id
  );

SELECT '✅ Created ' || changes() || ' BranchInventory records' as Result;

.print ''
.print '🔍 Verification:'

-- Step 3: Verify
SELECT '✓ Products now in BranchInventory: ' || COUNT(DISTINCT ProductId) as Status
FROM BranchInventories;

SELECT '✓ Total BranchInventory records: ' || COUNT(*) as Status
FROM BranchInventories;

.print ''
.print '📊 Summary by Tenant:'
SELECT '  ' || t.Name || ': ' || COUNT(DISTINCT bi.ProductId) || ' products, ' || 
       COUNT(DISTINCT bi.BranchId) || ' branches, ' || 
       SUM(bi.Quantity) || ' total stock' as Summary
FROM BranchInventories bi
JOIN Tenants t ON bi.TenantId = t.Id
GROUP BY t.Id, t.Name;

.print ''
.print '✅ Done! All products now have BranchInventory records.'
.print 'You can now see all products in the inventory page.'
