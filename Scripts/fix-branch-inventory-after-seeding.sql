-- Fix missing BranchInventory records after seeding
-- This script creates BranchInventory records for all products that don't have them

INSERT INTO BranchInventory (TenantId, BranchId, ProductId, Quantity, ReorderLevel, LastUpdatedAt, IsDeleted, CreatedAt, UpdatedAt)
SELECT 
    p.TenantId,
    b.Id as BranchId,
    p.Id as ProductId,
    p.StockQuantity as Quantity,
    COALESCE(p.LowStockThreshold, 10) as ReorderLevel,
    datetime('now') as LastUpdatedAt,
    0 as IsDeleted,
    datetime('now') as CreatedAt,
    datetime('now') as UpdatedAt
FROM Products p
CROSS JOIN Branches b
WHERE p.TenantId = b.TenantId
  AND p.IsDeleted = 0
  AND b.IsDeleted = 0
  AND NOT EXISTS (
    SELECT 1 FROM BranchInventory bi
    WHERE bi.ProductId = p.Id 
      AND bi.BranchId = b.Id
      AND bi.IsDeleted = 0
  );

-- Show results
SELECT 
    'Products' as TableName,
    COUNT(*) as Count
FROM Products
WHERE IsDeleted = 0

UNION ALL

SELECT 
    'BranchInventory' as TableName,
    COUNT(*) as Count
FROM BranchInventory
WHERE IsDeleted = 0;
