namespace KasserPro.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.Domain.Entities;
using KasserPro.Infrastructure.Data;
using static KasserPro.Application.Common.DateTimeHelper;

public class ProductReportService : IProductReportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ProductReportService> _logger;

    public ProductReportService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ProductReportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<ProductMovementReportDto>> GetProductMovementReportAsync(
        DateTime fromDate, DateTime toDate, int? categoryId = null)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.BranchInventories)
                .Where(p => p.TenantId == tenantId && p.IsActive);

            if (categoryId.HasValue)
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);

            var products = await productsQuery.ToListAsync();

            // Get order items in the period (include all completed states, exclude returns)
            var orderItems = await _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .SelectMany(o => o.Items)
                .ToListAsync();

            // Get return order items for refund deductions
            var returnItems = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType == OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .SelectMany(o => o.Items)
                .ToListAsync();

            // Get purchase invoice items in the period
            var purchaseItems = await _context.PurchaseInvoices
                .Where(pi => pi.TenantId == tenantId
                          && pi.BranchId == branchId
                          && pi.Status != PurchaseInvoiceStatus.Cancelled
                          && pi.InvoiceDate >= utcFrom
                          && pi.InvoiceDate < utcTo)
                .SelectMany(pi => pi.Items)
                .ToListAsync();

            // Get transfers
            var transfersIn = await _context.InventoryTransfers
                .Where(t => t.TenantId == tenantId
                          && t.ToBranchId == branchId
                          && t.CreatedAt >= utcFrom
                          && t.CreatedAt < utcTo)
                .ToListAsync();

            var transfersOut = await _context.InventoryTransfers
                .Where(t => t.TenantId == tenantId
                          && t.FromBranchId == branchId
                          && t.CreatedAt >= utcFrom
                          && t.CreatedAt < utcTo)
                .ToListAsync();

            var productMovements = new List<ProductMovementDetailDto>();
            var productsSold = 0;

            foreach (var product in products)
            {
                var soldItems = orderItems.Where(oi => oi.ProductId == product.Id).ToList();
                var returnedItems = returnItems.Where(oi => oi.ProductId == product.Id).ToList();
                var qtySold = soldItems.Sum(oi => oi.Quantity) - Math.Abs(returnedItems.Sum(oi => oi.Quantity));
                var revenue = soldItems.Sum(oi => oi.Total) - Math.Abs(returnedItems.Sum(oi => oi.Total));
                // Use OrderItem.UnitCost snapshot for historical accuracy
                var cost = soldItems.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity)
                         - returnedItems.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity));

                var purchased = purchaseItems.Where(pi => pi.ProductId == product.Id).Sum(pi => pi.Quantity);
                var tIn = transfersIn.Where(t => t.ProductId == product.Id).Sum(t => t.Quantity);
                var tOut = transfersOut.Where(t => t.ProductId == product.Id).Sum(t => t.Quantity);

                var branchInv = product.BranchInventories.FirstOrDefault(bi => bi.BranchId == branchId);
                var currentStock = branchInv?.Quantity ?? 0;

                if (qtySold > 0) productsSold++;

                productMovements.Add(new ProductMovementDetailDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    CategoryName = product.Category?.Name,
                    QuantitySold = qtySold,
                    TotalRevenue = revenue,
                    TotalCost = cost,
                    GrossProfit = revenue - cost,
                    ProfitMargin = revenue > 0 ? Math.Round((revenue - cost) / revenue * 100, 2) : 0,
                    OpeningStock = currentStock + qtySold + tOut - purchased - tIn,
                    PurchasedQuantity = purchased,
                    TransferredIn = tIn,
                    TransferredOut = tOut,
                    ClosingStock = currentStock,
                    TurnoverRate = currentStock > 0 ? Math.Round((decimal)qtySold / currentStock, 2) : 0,
                    DaysToSellOut = qtySold > 0
                        ? (int)Math.Ceiling((decimal)currentStock / ((decimal)qtySold / Math.Max(1, (decimal)(toDate - fromDate).TotalDays)))
                        : 999
                });
            }

            var report = new ProductMovementReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalProducts = products.Count,
                ProductsSold = productsSold,
                ProductsNotSold = products.Count - productsSold,
                TotalRevenue = productMovements.Sum(pm => pm.TotalRevenue),
                ProductMovements = productMovements.OrderByDescending(pm => pm.QuantitySold).ToList()
            };

            return ApiResponse<ProductMovementReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating product movement report");
            return ApiResponse<ProductMovementReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير حركة المنتجات");
        }
    }

    public async Task<ApiResponse<ProfitableProductsReportDto>> GetProfitableProductsReportAsync(
        DateTime fromDate, DateTime toDate, int topCount = 10)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var orderItems = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .SelectMany(o => o.Items)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p!.Category)
                .ToListAsync();

            // Query return orders for netting
            var returnItemsProfit = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && o.OrderType == OrderType.Return
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .SelectMany(o => o.Items)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p!.Category)
                .ToListAsync();

            // Group return items by product for netting
            var returnsByProduct = returnItemsProfit
                .Where(oi => oi.Product != null && oi.ProductId.HasValue)
                .GroupBy(oi => oi.ProductId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => (Qty: Math.Abs(g.Sum(oi => oi.Quantity)),
                          Revenue: Math.Abs(g.Sum(oi => oi.Total)),
                          Cost: g.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity))));

            var productGroups = orderItems
                .Where(oi => oi.Product != null)
                .GroupBy(oi => new
                {
                    oi.ProductId,
                    oi.Product!.Name,
                    CategoryName = oi.Product.Category?.Name
                })
                .Select(g =>
                {
                    var qty = g.Sum(oi => oi.Quantity);
                    var revenue = g.Sum(oi => oi.Total);
                    // Use OrderItem.UnitCost snapshot for historical accuracy
                    var cost = g.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity);

                    // Net out returns for this product
                    if (g.Key.ProductId.HasValue && returnsByProduct.TryGetValue(g.Key.ProductId.Value, out var ret))
                    {
                        qty -= ret.Qty;
                        revenue -= ret.Revenue;
                        cost -= ret.Cost;
                    }

                    var profit = revenue - cost;
                    return new ProfitableProductDetailDto
                    {
                        ProductId = g.Key.ProductId ?? 0,
                        ProductName = g.Key.Name,
                        CategoryName = g.Key.CategoryName,
                        QuantitySold = qty,
                        Revenue = revenue,
                        Cost = cost,
                        Profit = profit,
                        ProfitMargin = revenue > 0 ? Math.Round(profit / revenue * 100, 2) : 0,
                        AverageSellingPrice = qty > 0 ? Math.Round(revenue / qty, 2) : 0,
                        AverageCost = qty > 0 ? Math.Round(cost / qty, 2) : 0
                    };
                })
                .ToList();

            var totalRevenue = productGroups.Sum(p => p.Revenue);
            var totalCost = productGroups.Sum(p => p.Cost);
            var totalProfit = totalRevenue - totalCost;

            var report = new ProfitableProductsReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                TotalProfit = totalProfit,
                AverageProfitMargin = totalRevenue > 0 ? Math.Round(totalProfit / totalRevenue * 100, 2) : 0,
                TopProfitableProducts = productGroups.OrderByDescending(p => p.Profit).Take(topCount).ToList(),
                LeastProfitableProducts = productGroups.OrderBy(p => p.Profit).Take(topCount).ToList()
            };

            return ApiResponse<ProfitableProductsReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating profitable products report");
            return ApiResponse<ProfitableProductsReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير المنتجات الأكثر ربحية");
        }
    }

    public async Task<ApiResponse<SlowMovingProductsReportDto>> GetSlowMovingProductsReportAsync(
        int daysThreshold = 30)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var cutoffDate = DateTime.UtcNow.AddDays(-daysThreshold);

            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.BranchInventories)
                .Where(p => p.TenantId == tenantId && p.IsActive && p.TrackInventory)
                .ToListAsync();

            // Get recent sales per product (load then group in memory – SQLite doesn't support APPLY)
            var recentOrderItems = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.CompletedAt >= cutoffDate)
                .SelectMany(o => o.Items)
                .Where(oi => oi.ProductId != null)
                .GroupBy(oi => oi.ProductId!.Value)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(oi => oi.Quantity) })
                .ToListAsync();

            var recentSalesDict = recentOrderItems.ToDictionary(s => s.ProductId, s => s.Qty);

            // Get last sale date per product (join in DB, group in memory for SQLite compat)
            var orderItemsWithDate = await _context.OrderItems
                .Where(oi => oi.Order.TenantId == tenantId
                          && oi.Order.BranchId == branchId
                          && (oi.Order.Status == OrderStatus.Completed
                              || oi.Order.Status == OrderStatus.PartiallyRefunded
                              || oi.Order.Status == OrderStatus.Refunded)
                          && oi.Order.OrderType != OrderType.Return
                          && oi.ProductId != null)
                .Select(oi => new { ProductId = oi.ProductId!.Value, oi.Order.CompletedAt })
                .ToListAsync();

            var lastSaleDict = orderItemsWithDate
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.CompletedAt));

            var slowMoving = new List<SlowMovingProductDetailDto>();

            foreach (var product in products)
            {
                var branchInv = product.BranchInventories.FirstOrDefault(bi => bi.BranchId == branchId);
                var currentStock = branchInv?.Quantity ?? 0;
                if (currentStock <= 0) continue;

                var qtySold = recentSalesDict.GetValueOrDefault(product.Id, 0);
                var lastSaleDate = lastSaleDict.GetValueOrDefault(product.Id);
                var daysSinceLastSale = lastSaleDate.HasValue
                    ? (int)(DateTime.UtcNow - lastSaleDate.Value).TotalDays
                    : 999;

                // Only include if slow-moving
                if (daysSinceLastSale < daysThreshold && qtySold > 0) continue;

                var avgDailySales = qtySold > 0 ? (decimal)qtySold / daysThreshold : 0;
                var daysOfStock = avgDailySales > 0 ? (int)(currentStock / avgDailySales) : 999;
                var stockValue = currentStock * ResolveInventoryUnitCost(product);

                // Fixed thresholds so status stays consistent regardless of filter
                var status = daysSinceLastSale >= 90 ? "Dead Stock" :
                             daysSinceLastSale >= 30 ? "Very Slow" : "Slow";

                slowMoving.Add(new SlowMovingProductDetailDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CategoryName = product.Category?.Name,
                    CurrentStock = currentStock,
                    QuantitySold = qtySold,
                    AverageDailySales = Math.Round(avgDailySales, 2),
                    DaysOfStock = daysOfStock,
                    LastSoldDate = lastSaleDate,
                    DaysSinceLastSale = daysSinceLastSale,
                    StockValue = stockValue,
                    MovementStatus = status
                });
            }

            var report = new SlowMovingProductsReportDto
            {
                FromDate = cutoffDate,
                ToDate = DateTime.UtcNow,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalSlowMovingProducts = slowMoving.Count,
                TotalValueAtRisk = slowMoving.Sum(s => s.StockValue),
                TotalQuantityAtRisk = slowMoving.Sum(s => s.CurrentStock),
                SlowMovingProducts = slowMoving.OrderByDescending(s => s.DaysSinceLastSale).ToList()
            };

            return ApiResponse<SlowMovingProductsReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating slow-moving products report");
            return ApiResponse<SlowMovingProductsReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير المنتجات بطيئة الحركة");
        }
    }

    public async Task<ApiResponse<CogsReportDto>> GetCogsReportAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            // Get sold items with cost (include all completed states, exclude returns)
            var orderItems = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .SelectMany(o => o.Items)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p!.Category)
                .ToListAsync();

            // Get return order items for COGS netting
            var returnItemsCogs = await _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType == OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .SelectMany(o => o.Items)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p!.Category)
                .ToListAsync();

            // Get purchases in period
            var purchases = await _context.PurchaseInvoices
                .AsNoTracking()
                .Where(pi => pi.TenantId == tenantId
                          && pi.BranchId == branchId
                          && pi.Status != PurchaseInvoiceStatus.Cancelled
                          && pi.InvoiceDate >= utcFrom
                          && pi.InvoiceDate < utcTo)
                .ToListAsync();

            var totalPurchases = Math.Round(purchases.Sum(p => p.Total), 2);
            var totalRevenue = Math.Round(
                orderItems.Sum(oi => oi.Total) - Math.Abs(returnItemsCogs.Sum(oi => oi.Total)),
                2);
            // Use OrderItem.UnitCost snapshot for historical accuracy
            var totalCost = Math.Round(
                orderItems.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity)
                - returnItemsCogs.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity)),
                2);

            // Estimate opening & closing inventory values
            var currentInventory = await _context.BranchInventories
                .AsNoTracking()
                .Include(bi => bi.Product)
                .Where(bi => bi.BranchId == branchId && bi.Product.TenantId == tenantId)
                .ToListAsync();

            var closingInventoryValue = Math.Round(currentInventory.Sum(bi =>
                bi.Quantity * ResolveInventoryUnitCost(bi.Product)), 2);
            var productsWithNoCostCount = currentInventory.Count(bi =>
                bi.Product.AverageCost is null && (bi.Product.Cost ?? 0m) == 0m);
            var openingInventoryValue = Math.Round(
                Math.Max(0m, closingInventoryValue + totalCost - totalPurchases),
                2);

            var cogs = Math.Round(
                Math.Max(0m, openingInventoryValue + totalPurchases - closingInventoryValue),
                2);
            var grossProfit = Math.Round(totalRevenue - cogs, 2);

            // Group return items by category for netting
            var returnsByCat = returnItemsCogs
                .Where(oi => oi.Product != null)
                .GroupBy(oi => oi.Product!.CategoryId)
                .ToDictionary(
                    g => g.Key,
                    g => (Revenue: Math.Abs(g.Sum(oi => oi.Total)),
                          Cost: g.Sum(oi => (oi.UnitCost ?? 0) * Math.Abs(oi.Quantity))));

            // Category breakdown
            var categoryBreakdown = orderItems
                .Where(oi => oi.Product != null)
                .GroupBy(oi => new
                {
                    CategoryId = oi.Product!.CategoryId,
                    CategoryName = oi.Product.Category?.Name ?? "بدون تصنيف"
                })
                .Select(g =>
                {
                    var catRevenue = g.Sum(oi => oi.Total);
                    // Use OrderItem.UnitCost snapshot
                    var catCost = g.Sum(oi => (oi.UnitCost ?? 0) * oi.Quantity);
                    // Net out returns for this category
                    if (returnsByCat.TryGetValue(g.Key.CategoryId, out var retCat))
                    {
                        catRevenue -= retCat.Revenue;
                        catCost -= retCat.Cost;
                    }
                    return new CogsCategoryBreakdownDto
                    {
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.CategoryName,
                        OpeningValue = 0,
                        Purchases = 0,
                        ClosingValue = 0,
                        Cogs = catCost,
                        Revenue = catRevenue,
                        GrossProfit = catRevenue - catCost,
                        GrossProfitMargin = catRevenue > 0 ? Math.Round((catRevenue - catCost) / catRevenue * 100, 2) : 0
                    };
                })
                .OrderByDescending(c => c.Revenue)
                .ToList();

            var returnsByProduct = returnItemsCogs
                .Where(oi => oi.Product != null && oi.ProductId.HasValue)
                .GroupBy(oi => oi.ProductId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Quantity = Math.Abs(g.Sum(oi => oi.Quantity)),
                        Revenue = Math.Abs(g.Sum(oi => oi.Total)),
                        Cost = g.Sum(oi => (oi.UnitCost ?? 0m) * Math.Abs(oi.Quantity))
                    });

            var productBreakdown = orderItems
                .Where(oi => oi.Product != null && oi.ProductId.HasValue)
                .GroupBy(oi => new
                {
                    ProductId = oi.ProductId!.Value,
                    oi.Product!.Name,
                    CategoryName = oi.Product.Category?.Name,
                    HasMissingCost = oi.Product.AverageCost is null && oi.Product.Cost is null
                })
                .Select(g =>
                {
                    var quantitySold = g.Sum(oi => oi.Quantity);
                    var revenue = g.Sum(oi => oi.Total);
                    var cost = g.Sum(oi => (oi.UnitCost ?? 0m) * oi.Quantity);

                    if (returnsByProduct.TryGetValue(g.Key.ProductId, out var productReturn))
                    {
                        quantitySold -= productReturn.Quantity;
                        revenue -= productReturn.Revenue;
                        cost -= productReturn.Cost;
                    }

                    var unitCost = quantitySold > 0 ? Math.Round(cost / quantitySold, 2) : 0m;

                    return new CogsProductBreakdownDto
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.Name,
                        CategoryName = g.Key.CategoryName,
                        QuantitySold = quantitySold,
                        Revenue = Math.Round(revenue, 2),
                        UnitCost = unitCost,
                        Cost = Math.Round(cost, 2),
                        GrossProfit = Math.Round(revenue - cost, 2),
                        HasMissingCost = g.Key.HasMissingCost
                    };
                })
                .OrderByDescending(p => p.Revenue)
                .ToList();

            var report = new CogsReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                OpeningInventoryValue = openingInventoryValue,
                TotalPurchases = totalPurchases,
                ClosingInventoryValue = closingInventoryValue,
                ProductsWithNoCostCount = productsWithNoCostCount,
                CostOfGoodsSold = cogs,
                TotalRevenue = totalRevenue,
                GrossProfit = grossProfit,
                GrossProfitMargin = totalRevenue > 0 ? Math.Round(grossProfit / totalRevenue * 100, 2) : 0,
                CategoryBreakdown = categoryBreakdown,
                ProductBreakdown = productBreakdown
            };

            return ApiResponse<CogsReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating COGS report");
            return ApiResponse<CogsReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير تكلفة البضاعة المباعة");
        }
    }

    private static decimal ResolveInventoryUnitCost(Product product) =>
        product.AverageCost ?? product.Cost ?? 0m;
}
