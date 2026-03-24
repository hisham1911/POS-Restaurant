-- ============================================
-- Fix Missing BranchInventory Records
-- ============================================
-- This script creates BranchInventory records for products that have StockQuantity
-- but are missing from BranchInventory table
-- ============================================

-- Step 1: Check current state
SELECT 
    'Products with StockQuantity > 0' as Description,
    COUNT(*) as Count
FROM Products 
WHERE StockQuantity > 0 AND IsActive = 1;

SELECT 
    'Products in BranchInventory' as Description,
    COUNT(DISTINCT ProductId) as Count
FROM BranchInventories;

SELECT 
    'Products missing from BranchInventory' as Description,
    COUNT(*) as Count
FROM Products p
WHERE p.IsActive = 1 
  AND p.TrackInventory = 1
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id
  );

-- Step 2: Show products that will be fixed
SELECT 
    p.Id,
    p.Name,
    p.StockQuantity,
    p.LowStockThreshold,
    p.TenantId
FROM Products p
WHERE p.IsActive = 1 
  AND p.TrackInventory = 1
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id
  )
ORDER BY p.TenantId, p.Name;

-- Step 3: Create missing BranchInventory records
-- This will create records for ALL branches of each tenant
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
  AND p.TenantId = b.TenantId  -- Only create for branches in same tenant
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id AND bi.BranchId = b.Id
  );

-- Step 4: Verify the fix
SELECT 
    'Products now in BranchInventory' as Description,
    COUNT(DISTINCT ProductId) as Count
FROM BranchInventories;

SELECT 
    'Total BranchInventory records' as Description,
    COUNT(*) as Count
FROM BranchInventories;

-- Step 5: Show summary by tenant
SELECT 
    t.Name as TenantName,
    COUNT(DISTINCT bi.ProductId) as ProductsInInventory,
    COUNT(DISTINCT bi.BranchId) as BranchesWithInventory,
    SUM(bi.Quantity) as TotalStock
FROM BranchInventories bi
JOIN Tenants t ON bi.TenantId = t.Id
GROUP BY t.Id, t.Name
ORDER BY t.Name;

-- Step 6: Optional - Reset Product.StockQuantity to 0 (since we now use BranchInventory)
-- Uncomment if you want to migrate fully to BranchInventory system
-- UPDATE Products 
-- SET StockQuantity = 0, 
--     LastStockUpdate = datetime('now')
-- WHERE TrackInventory = 1 
--   AND StockQuantity > 0;
