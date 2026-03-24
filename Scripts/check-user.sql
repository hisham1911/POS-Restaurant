-- Check users and their TenantId
SELECT Id, Email, TenantId, BranchId, Role FROM Users;

-- Check which tenant has the BranchInventories
SELECT DISTINCT bi.TenantId, t.Name as TenantName, COUNT(*) as InventoryCount
FROM BranchInventories bi
JOIN Tenants t ON bi.TenantId = t.Id
GROUP BY bi.TenantId, t.Name;
