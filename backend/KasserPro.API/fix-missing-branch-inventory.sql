-- Fix Missing BranchInventory Records
-- This script ensures every product has a BranchInventory record for every branch in its tenant

-- Step 1: Insert missing BranchInventory records
INSERT INTO BranchInventories (ProductId, BranchId, TenantId, Quantity, ReorderLevel, LastUpdatedAt, CreatedAt, UpdatedAt, IsDeleted)
SELECT 
    p.Id as ProductId,
    b.Id as BranchId,
    p.TenantId,
    0 as Quantity,
    COALESCE(p.ReorderPoint, 5) as ReorderLevel,
    datetime('now') as LastUpdatedAt,
    datetime('now') as CreatedAt,
    datetime('now') as UpdatedAt,
    0 as IsDeleted
FROM Products p
CROSS JOIN Branches b
WHERE p.TenantId = b.TenantId
  AND NOT EXISTS (
    SELECT 1 
    FROM BranchInventories bi 
    WHERE bi.ProductId = p.Id 
      AND bi.BranchId = b.Id
  );

-- Step 2: Verify the fix
SELECT 
    'Total Products' as Metric,
    COUNT(*) as Count
FROM Products
UNION ALL
SELECT 
    'Total Branches' as Metric,
    COUNT(*) as Count
FROM Branches
UNION ALL
SELECT 
    'Total BranchInventories' as Metric,
    COUNT(*) as Count
FROM BranchInventories
UNION ALL
SELECT 
    'Expected BranchInventories' as Metric,
    (SELECT COUNT(*) FROM Products) * (SELECT COUNT(DISTINCT Id) FROM Branches WHERE TenantId IN (SELECT DISTINCT TenantId FROM Products)) as Count
FROM (SELECT 1);

-- Step 3: Show products with missing inventory (should be empty after fix)
SELECT 
    p.Id,
    p.Name,
    COUNT(bi.Id) as BranchCount,
    (SELECT COUNT(*) FROM Branches WHERE TenantId = p.TenantId) as ExpectedCount
FROM Products p
LEFT JOIN BranchInventories bi ON p.Id = bi.ProductId
GROUP BY p.Id
HAVING COUNT(bi.Id) != (SELECT COUNT(*) FROM Branches WHERE TenantId = p.TenantId)
LIMIT 10;
