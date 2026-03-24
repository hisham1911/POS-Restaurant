-- Fix Inventory Data Script
-- This script ensures all products have BranchInventory records
-- Run this after deploying the backend fixes

-- Step 1: Create BranchInventory records for products that don't have them
INSERT INTO BranchInventories (TenantId, BranchId, ProductId, Quantity, ReorderLevel, LastUpdatedAt, CreatedAt, IsDeleted)
SELECT 
    p.TenantId,
    b.Id as BranchId,
    p.Id as ProductId,
    COALESCE(p.StockQuantity, 0) as Quantity,
    COALESCE(p.LowStockThreshold, 10) as ReorderLevel,
    datetime('now') as LastUpdatedAt,
    datetime('now') as CreatedAt,
    0 as IsDeleted
FROM Products p
CROSS JOIN Branches b
WHERE p.TrackInventory = 1
  AND p.IsDeleted = 0
  AND b.IsDeleted = 0
  AND p.TenantId = b.TenantId
  AND NOT EXISTS (
    SELECT 1 FROM BranchInventories bi
    WHERE bi.ProductId = p.Id 
      AND bi.BranchId = b.Id
      AND bi.TenantId = p.TenantId
  );

-- Step 2: Update existing BranchInventory records from Product.StockQuantity if they're zero
UPDATE BranchInventories
SET Quantity = (
    SELECT COALESCE(p.StockQuantity, 0)
    FROM Products p
    WHERE p.Id = BranchInventories.ProductId
      AND p.TenantId = BranchInventories.TenantId
),
LastUpdatedAt = datetime('now')
WHERE Quantity = 0
  AND EXISTS (
    SELECT 1 FROM Products p
    WHERE p.Id = BranchInventories.ProductId
      AND p.TenantId = BranchInventories.TenantId
      AND p.TrackInventory = 1
      AND COALESCE(p.StockQuantity, 0) > 0
  );

-- Step 3: Verify the fix
SELECT 
    'Products without BranchInventory' as Issue,
    COUNT(*) as Count
FROM Products p
WHERE p.TrackInventory = 1
  AND p.IsDeleted = 0
  AND NOT EXISTS (
    SELECT 1 FROM BranchInventories bi
    WHERE bi.ProductId = p.Id
      AND bi.TenantId = p.TenantId
  )
UNION ALL
SELECT 
    'BranchInventory records created' as Issue,
    COUNT(*) as Count
FROM BranchInventories
WHERE IsDeleted = 0;
