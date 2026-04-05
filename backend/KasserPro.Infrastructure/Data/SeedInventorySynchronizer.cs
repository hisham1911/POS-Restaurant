namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Synchronizes BranchInventory after direct seed inserts.
/// Seeders bypass service-layer stock handlers, so we reconcile once at the end.
/// </summary>
public static class SeedInventorySynchronizer
{
    private sealed record ProductSeedInfo(int ProductId, int ReorderLevel);
    private sealed record QuantityAggregate(int BranchId, int ProductId, int Quantity);

    public static async Task SynchronizeAsync(AppDbContext context)
    {
        Console.WriteLine("🔄 مزامنة مخزون الفروع مع بيانات السييد...");

        var tenantIds = await context.Tenants
            .AsNoTracking()
            .Select(t => t.Id)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var createdCount = 0;
        var updatedCount = 0;

        foreach (var tenantId in tenantIds)
        {
            var branchIds = await context.Branches
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync();

            if (branchIds.Count == 0)
            {
                continue;
            }

            var products = await context.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive && p.TrackInventory)
                .Select(p => new ProductSeedInfo(
                    p.Id,
                    p.ReorderPoint ?? p.LowStockThreshold ?? 10))
                .ToListAsync();

            if (products.Count == 0)
            {
                continue;
            }

            var productIds = products.Select(p => p.ProductId).ToList();

            var purchaseIncoming = await context.PurchaseInvoiceItems
                .AsNoTracking()
                .Where(i => productIds.Contains(i.ProductId)
                    && i.PurchaseInvoice.TenantId == tenantId
                    && branchIds.Contains(i.PurchaseInvoice.BranchId)
                    && (i.PurchaseInvoice.Status == PurchaseInvoiceStatus.Confirmed
                        || i.PurchaseInvoice.Status == PurchaseInvoiceStatus.Paid
                        || i.PurchaseInvoice.Status == PurchaseInvoiceStatus.PartiallyPaid))
                .GroupBy(i => new { i.PurchaseInvoice.BranchId, i.ProductId })
                .Select(g => new QuantityAggregate(g.Key.BranchId, g.Key.ProductId, g.Sum(x => x.Quantity)))
                .ToListAsync();

            var netOrderConsumption = await context.OrderItems
                .AsNoTracking()
                .Where(i => i.ProductId.HasValue
                    && productIds.Contains(i.ProductId.Value)
                    && i.Order.TenantId == tenantId
                    && branchIds.Contains(i.Order.BranchId)
                    && (i.Order.Status == OrderStatus.Completed
                        || i.Order.Status == OrderStatus.Refunded
                        || i.Order.Status == OrderStatus.PartiallyRefunded))
                .GroupBy(i => new { i.Order.BranchId, ProductId = i.ProductId!.Value })
                .Select(g => new QuantityAggregate(g.Key.BranchId, g.Key.ProductId, g.Sum(x => x.Quantity)))
                .ToListAsync();

            var transferOutgoing = await context.Set<InventoryTransfer>()
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId
                    && productIds.Contains(t.ProductId)
                    && branchIds.Contains(t.FromBranchId)
                    && (t.Status == InventoryTransferStatus.Approved
                        || t.Status == InventoryTransferStatus.Completed))
                .GroupBy(t => new { BranchId = t.FromBranchId, t.ProductId })
                .Select(g => new QuantityAggregate(g.Key.BranchId, g.Key.ProductId, g.Sum(x => x.Quantity)))
                .ToListAsync();

            var transferIncoming = await context.Set<InventoryTransfer>()
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId
                    && productIds.Contains(t.ProductId)
                    && branchIds.Contains(t.ToBranchId)
                    && t.Status == InventoryTransferStatus.Completed)
                .GroupBy(t => new { BranchId = t.ToBranchId, t.ProductId })
                .Select(g => new QuantityAggregate(g.Key.BranchId, g.Key.ProductId, g.Sum(x => x.Quantity)))
                .ToListAsync();

            var existingInventories = await context.BranchInventories
                .Where(i => i.TenantId == tenantId
                    && branchIds.Contains(i.BranchId)
                    && productIds.Contains(i.ProductId))
                .ToListAsync();

            var existingMap = existingInventories.ToDictionary(i => (i.BranchId, i.ProductId));
            var purchaseMap = purchaseIncoming.ToDictionary(i => (i.BranchId, i.ProductId), i => i.Quantity);
            var orderMap = netOrderConsumption.ToDictionary(i => (i.BranchId, i.ProductId), i => i.Quantity);
            var transferOutMap = transferOutgoing.ToDictionary(i => (i.BranchId, i.ProductId), i => i.Quantity);
            var transferInMap = transferIncoming.ToDictionary(i => (i.BranchId, i.ProductId), i => i.Quantity);

            foreach (var branchId in branchIds)
            {
                foreach (var product in products)
                {
                    var key = (branchId, product.ProductId);

                    purchaseMap.TryGetValue(key, out var purchaseQty);
                    orderMap.TryGetValue(key, out var orderQty);
                    transferOutMap.TryGetValue(key, out var transferOutQty);
                    transferInMap.TryGetValue(key, out var transferInQty);

                    var baselineQty = purchaseQty > 0
                        ? 0
                        : Math.Max(product.ReorderLevel * 12, 50);

                    var calculatedQty = baselineQty
                        + purchaseQty
                        + transferInQty
                        - orderQty
                        - transferOutQty;

                    var finalQty = Math.Max(0, calculatedQty);

                    if (existingMap.TryGetValue(key, out var inventory))
                    {
                        if (inventory.Quantity != finalQty || inventory.ReorderLevel != product.ReorderLevel)
                        {
                            inventory.Quantity = finalQty;
                            inventory.ReorderLevel = product.ReorderLevel;
                            inventory.LastUpdatedAt = now;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        context.BranchInventories.Add(new BranchInventory
                        {
                            TenantId = tenantId,
                            BranchId = branchId,
                            ProductId = product.ProductId,
                            Quantity = finalQty,
                            ReorderLevel = product.ReorderLevel,
                            LastUpdatedAt = now
                        });
                        createdCount++;
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        Console.WriteLine($"   ✓ مزامنة المخزون تمت: جديد {createdCount} | محدث {updatedCount}");
    }
}
