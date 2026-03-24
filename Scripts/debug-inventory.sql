-- Debug: Check what's actually in the database

.print '=== 1. Check Tenants ==='
SELECT * FROM Tenants;

.print ''
.print '=== 2. Check Branches ==='
SELECT * FROM Branches;

.print ''
.print '=== 3. Check Products ==='
SELECT Id, Name, IsActive, TrackInventory, StockQuantity, TenantId FROM Products LIMIT 10;

.print ''
.print '=== 4. Check BranchInventories ==='
SELECT bi.Id, bi.TenantId, bi.BranchId, bi.ProductId, bi.Quantity, bi.IsDeleted,
       p.Name as ProductName, b.Name as BranchName
FROM BranchInventories bi
LEFT JOIN Products p ON bi.ProductId = p.Id
LEFT JOIN Branches b ON bi.BranchId = b.Id
LIMIT 10;

.print ''
.print '=== 5. Count Records ==='
SELECT 'Total Products' as Type, COUNT(*) as Count FROM Products
UNION ALL
SELECT 'Active Products', COUNT(*) FROM Products WHERE IsActive = 1
UNION ALL
SELECT 'Total Branches', COUNT(*) FROM Branches
UNION ALL
SELECT 'Total BranchInventories', COUNT(*) FROM BranchInventories
UNION ALL
SELECT 'BranchInventories (IsDeleted=0)', COUNT(*) FROM BranchInventories WHERE IsDeleted = 0;

.print ''
.print '=== 6. Check for TenantId/BranchId Mismatch ==='
SELECT 
    'Mismatch: ' || COUNT(*) || ' records' as Issue
FROM BranchInventories bi
WHERE NOT EXISTS (
    SELECT 1 FROM Branches b 
    WHERE b.Id = bi.BranchId AND b.TenantId = bi.TenantId
);
