namespace KasserPro.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;

public class InventoryReportService : IInventoryReportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InventoryReportService> _logger;

    public InventoryReportService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<InventoryReportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<BranchInventoryReportDto>> GetBranchInventoryReportAsync(
        int branchId,
        int? categoryId = null,
        bool? lowStockOnly = null)
    {
        try
        {
            if (!CanAccessBranch(branchId))
                return ApiResponse<BranchInventoryReportDto>.Fail(ErrorCodes.FORBIDDEN, "ليس لديك صلاحية الوصول لهذا الفرع");

            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId && b.TenantId == _currentUserService.TenantId);

            if (branch == null)
                return ApiResponse<BranchInventoryReportDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, "الفرع غير موجود");

            // Query branch inventory with products
            var query = _context.BranchInventories
                .Where(bi => bi.BranchId == branchId && bi.TenantId == _currentUserService.TenantId)
                .Include(bi => bi.Product)
                    .ThenInclude(p => p.Category)
                .AsQueryable();

            // Apply filters
            if (categoryId.HasValue)
                query = query.Where(bi => bi.Product.CategoryId == categoryId.Value);

            if (lowStockOnly == true)
                query = query.Where(bi => bi.Quantity <= bi.ReorderLevel);

            var inventoryItems = await query.ToListAsync();

            // Build report
            var items = inventoryItems.Select(bi => new BranchInventoryItemDto
            {
                ProductId = bi.ProductId,
                ProductName = bi.Product.Name,
                ProductSku = bi.Product.Sku,
                CategoryName = bi.Product.Category?.Name,
                Quantity = bi.Quantity,
                ReorderLevel = bi.ReorderLevel,
                IsLowStock = bi.Quantity <= bi.ReorderLevel,
                AverageCost = bi.Product.AverageCost,
                TotalValue = bi.Quantity * (bi.Product.AverageCost ?? 0),
                LastUpdatedAt = bi.LastUpdatedAt
            }).ToList();

            var report = new BranchInventoryReportDto
            {
                BranchId = branchId,
                BranchName = branch.Name,
                TotalProducts = items.Count,
                TotalQuantity = items.Sum(i => i.Quantity),
                LowStockCount = items.Count(i => i.IsLowStock),
                TotalValue = items.Sum(i => i.TotalValue ?? 0),
                Items = items
            };

            return ApiResponse<BranchInventoryReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating branch inventory report for branch {BranchId}", branchId);
            return ApiResponse<BranchInventoryReportDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ في إنشاء التقرير");
        }
    }

    public async Task<ApiResponse<List<UnifiedInventoryReportDto>>> GetUnifiedInventoryReportAsync(
        int? categoryId = null,
        bool? lowStockOnly = null)
    {
        try
        {
            // Get all products with their branch inventories
            var query = _context.Products
                .Where(p => p.TenantId == _currentUserService.TenantId && p.IsActive)
                .Include(p => p.Category)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            var products = await query.ToListAsync();

            // Get all branch inventories for these products
            var productIds = products.Select(p => p.Id).ToList();
            var branchInventories = await _context.BranchInventories
                .Where(bi => productIds.Contains(bi.ProductId) && bi.TenantId == _currentUserService.TenantId)
                .Include(bi => bi.Branch)
                .ToListAsync();

            // Group by product
            var groupedInventories = branchInventories.GroupBy(bi => bi.ProductId);

            var reports = new List<UnifiedInventoryReportDto>();

            foreach (var product in products)
            {
                var productInventories = groupedInventories
                    .FirstOrDefault(g => g.Key == product.Id)?
                    .ToList() ?? new List<Domain.Entities.BranchInventory>();

                var totalQuantity = productInventories.Sum(bi => bi.Quantity);
                var lowStockBranchCount = productInventories.Count(bi => bi.Quantity <= bi.ReorderLevel);

                // Apply low stock filter
                if (lowStockOnly == true && lowStockBranchCount == 0)
                    continue;

                var branchStocks = productInventories.Select(bi => new BranchStockDto
                {
                    BranchId = bi.BranchId,
                    BranchName = bi.Branch.Name,
                    Quantity = bi.Quantity,
                    ReorderLevel = bi.ReorderLevel,
                    IsLowStock = bi.Quantity <= bi.ReorderLevel
                }).ToList();

                reports.Add(new UnifiedInventoryReportDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSku = product.Sku,
                    CategoryName = product.Category?.Name,
                    TotalQuantity = totalQuantity,
                    AverageCost = product.AverageCost,
                    TotalValue = totalQuantity * (product.AverageCost ?? 0),
                    BranchCount = productInventories.Count,
                    LowStockBranchCount = lowStockBranchCount,
                    BranchStocks = branchStocks
                });
            }

            return ApiResponse<List<UnifiedInventoryReportDto>>.Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating unified inventory report");
            return ApiResponse<List<UnifiedInventoryReportDto>>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ في إنشاء التقرير");
        }
    }

    public async Task<ApiResponse<TransferHistoryReportDto>> GetTransferHistoryReportAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? branchId = null)
    {
        try
        {
            // Non-admin users are always scoped to their own branch.
            if (!IsPrivilegedRole())
            {
                if (branchId.HasValue && !CanAccessBranch(branchId.Value))
                    return ApiResponse<TransferHistoryReportDto>.Fail(ErrorCodes.FORBIDDEN, "ليس لديك صلاحية الوصول لهذا الفرع");

                branchId = _currentUserService.BranchId;
            }

            // Default date range: last 30 days
            var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
            var to = toDate ?? DateTime.UtcNow;

            // Query transfers
            var query = _context.InventoryTransfers
                .Where(t => t.TenantId == _currentUserService.TenantId &&
                           t.CreatedAt >= from &&
                           t.CreatedAt <= to)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.Product)
                .AsQueryable();

            // Filter by branch (either source or destination)
            if (branchId.HasValue)
                query = query.Where(t => t.FromBranchId == branchId.Value || t.ToBranchId == branchId.Value);

            var transfers = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            // Build transfer summaries
            var transferSummaries = transfers.Select(t => new TransferSummaryDto
            {
                Id = t.Id,
                TransferNumber = t.TransferNumber,
                CreatedAt = t.CreatedAt,
                FromBranchName = t.FromBranch.Name,
                ToBranchName = t.ToBranch.Name,
                ProductName = t.Product.Name,
                Quantity = t.Quantity,
                Status = t.Status.ToString(),
                Reason = t.Reason,
                CompletedAt = t.Status == InventoryTransferStatus.Completed ? t.ReceivedAt : null
            }).ToList();

            // Calculate statistics
            var totalTransfers = transfers.Count;
            var completedTransfers = transfers.Count(t => t.Status == InventoryTransferStatus.Completed);
            var pendingTransfers = transfers.Count(t => t.Status == InventoryTransferStatus.Pending ||
                                                        t.Status == InventoryTransferStatus.Approved);
            var cancelledTransfers = transfers.Count(t => t.Status == InventoryTransferStatus.Cancelled);
            var totalQuantityTransferred = transfers
                .Where(t => t.Status == InventoryTransferStatus.Completed)
                .Sum(t => t.Quantity);

            // Calculate branch statistics
            var branchStats = new List<BranchTransferStatsDto>();
            var allBranchIds = transfers.Select(t => t.FromBranchId)
                .Union(transfers.Select(t => t.ToBranchId))
                .Distinct();

            foreach (var bId in allBranchIds)
            {
                var branch = await _context.Branches.FindAsync(bId);
                if (branch == null) continue;

                var sent = transfers.Where(t => t.FromBranchId == bId &&
                                               t.Status == InventoryTransferStatus.Completed);
                var received = transfers.Where(t => t.ToBranchId == bId &&
                                                   t.Status == InventoryTransferStatus.Completed);

                var quantitySent = sent.Sum(t => t.Quantity);
                var quantityReceived = received.Sum(t => t.Quantity);

                branchStats.Add(new BranchTransferStatsDto
                {
                    BranchId = bId,
                    BranchName = branch.Name,
                    TransfersSent = sent.Count(),
                    TransfersReceived = received.Count(),
                    QuantitySent = quantitySent,
                    QuantityReceived = quantityReceived,
                    NetChange = quantityReceived - quantitySent
                });
            }

            var report = new TransferHistoryReportDto
            {
                FromDate = from,
                ToDate = to,
                TotalTransfers = totalTransfers,
                CompletedTransfers = completedTransfers,
                PendingTransfers = pendingTransfers,
                CancelledTransfers = cancelledTransfers,
                TotalQuantityTransferred = totalQuantityTransferred,
                Transfers = transferSummaries,
                BranchStats = branchStats
            };

            return ApiResponse<TransferHistoryReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating transfer history report");
            return ApiResponse<TransferHistoryReportDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ في إنشاء التقرير");
        }
    }

    public async Task<ApiResponse<LowStockSummaryReportDto>> GetLowStockSummaryReportAsync(int? branchId = null)
    {
        try
        {
            // Non-admin users are always scoped to their own branch.
            if (!IsPrivilegedRole())
            {
                if (branchId.HasValue && !CanAccessBranch(branchId.Value))
                    return ApiResponse<LowStockSummaryReportDto>.Fail(ErrorCodes.FORBIDDEN, "ليس لديك صلاحية الوصول لهذا الفرع");

                branchId = _currentUserService.BranchId;
            }

            // Query low stock items
            var query = _context.BranchInventories
                .Where(bi => bi.TenantId == _currentUserService.TenantId &&
                            bi.Quantity <= bi.ReorderLevel)
                .Include(bi => bi.Branch)
                .Include(bi => bi.Product)
                    .ThenInclude(p => p.Category)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(bi => bi.BranchId == branchId.Value);

            var lowStockInventories = await query.ToListAsync();

            // Group by product
            var groupedByProduct = lowStockInventories.GroupBy(bi => bi.ProductId);

            var items = new List<LowStockItemDto>();

            foreach (var group in groupedByProduct)
            {
                var product = group.First().Product;
                var branchDetails = group.Select(bi => new BranchLowStockDetailDto
                {
                    BranchId = bi.BranchId,
                    BranchName = bi.Branch.Name,
                    Quantity = bi.Quantity,
                    ReorderLevel = bi.ReorderLevel,
                    Shortage = Math.Max(0, bi.ReorderLevel - bi.Quantity),
                    IsCritical = bi.Quantity == 0
                }).ToList();

                var totalQuantity = branchDetails.Sum(bd => bd.Quantity);
                var totalReorderLevel = branchDetails.Sum(bd => bd.ReorderLevel);
                var shortage = Math.Max(0, totalReorderLevel - totalQuantity);

                items.Add(new LowStockItemDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSku = product.Sku,
                    CategoryName = product.Category?.Name,
                    TotalQuantity = totalQuantity,
                    TotalReorderLevel = totalReorderLevel,
                    Shortage = shortage,
                    AverageCost = product.AverageCost,
                    EstimatedRestockCost = shortage * (product.AverageCost ?? 0),
                    BranchDetails = branchDetails
                });
            }

            // Calculate branch statistics
            var branchStatsQuery = lowStockInventories.GroupBy(bi => bi.BranchId);
            var branchStats = branchStatsQuery.Select(g =>
            {
                var branch = g.First().Branch;
                var criticalCount = g.Count(bi => bi.Quantity == 0);
                var estimatedValue = g.Sum(bi =>
                    Math.Max(0, bi.ReorderLevel - bi.Quantity) * (bi.Product.AverageCost ?? 0));

                return new BranchLowStockStatsDto
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    LowStockCount = g.Count(),
                    CriticalCount = criticalCount,
                    EstimatedRestockValue = estimatedValue
                };
            }).ToList();

            var report = new LowStockSummaryReportDto
            {
                TotalLowStockItems = items.Count,
                AffectedBranches = branchStats.Count,
                CriticalItems = items.Count(i => i.BranchDetails.Any(bd => bd.IsCritical)),
                EstimatedRestockValue = items.Sum(i => i.EstimatedRestockCost ?? 0),
                Items = items,
                BranchStats = branchStats
            };

            return ApiResponse<LowStockSummaryReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating low stock summary report");
            return ApiResponse<LowStockSummaryReportDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ في إنشاء التقرير");
        }
    }

    private bool CanAccessBranch(int branchId)
    {
        if (IsPrivilegedRole())
            return true;

        return _currentUserService.BranchId > 0 && _currentUserService.BranchId == branchId;
    }

    private bool IsPrivilegedRole()
    {
        return string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_currentUserService.Role, "SystemOwner", StringComparison.OrdinalIgnoreCase);
    }
}
