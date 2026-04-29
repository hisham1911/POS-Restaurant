namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class ProductBatchService : IProductBatchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ProductBatchService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<ProductBatchDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var batch = await _unitOfWork.ProductBatches.Query()
            .Where(pb => pb.Id == id && pb.TenantId == tenantId && !pb.IsDeleted)
            .Include(pb => pb.Product)
            .Select(pb => MapToDto(pb))
            .FirstOrDefaultAsync();

        if (batch == null)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.NOT_FOUND, "الباتش غير موجود");

        return ApiResponse<ProductBatchDto>.Ok(batch);
    }

    public async Task<ApiResponse<PagedResult<ProductBatchDto>>> GetAllAsync(
        int? productId = null, int? branchId = null, string? status = null,
        int pageNumber = 1, int pageSize = 20)
    {
        var tenantId = _currentUser.TenantId;
        var query = _unitOfWork.ProductBatches.Query()
            .Where(pb => pb.TenantId == tenantId && !pb.IsDeleted);

        if (productId.HasValue)
            query = query.Where(pb => pb.ProductId == productId.Value);

        if (branchId.HasValue)
            query = query.Where(pb => pb.BranchId == branchId.Value);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<BatchStatus>(status, out var batchStatus))
            query = query.Where(pb => pb.Status == batchStatus);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(pb => pb.ExpiryDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(pb => pb.Product)
            .Include(pb => pb.Branch)
            .Select(pb => MapToDto(pb))
            .ToListAsync();

        return ApiResponse<PagedResult<ProductBatchDto>>.Ok(
            new PagedResult<ProductBatchDto>(items, totalCount, pageNumber, pageSize));
    }

    public async Task<ApiResponse<ProductBatchDto>> CreateAsync(CreateProductBatchDto dto)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var userId = _currentUser.UserId;

        var product = await _unitOfWork.Products.Query()
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.TenantId == tenantId);

        if (product == null)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var batch = new ProductBatch
            {
                TenantId = tenantId, BranchId = branchId, ProductId = dto.ProductId,
                BatchNumber = dto.BatchNumber, Quantity = dto.Quantity, InitialQuantity = dto.Quantity,
                ExpiryDate = dto.ExpiryDate, PurchaseDate = DateTime.UtcNow,
                ProductionDate = dto.ProductionDate, CostPrice = dto.CostPrice,
                SupplierName = dto.SupplierName, Status = BatchStatus.Active,
                Notes = dto.Notes, CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.ProductBatches.AddAsync(batch);

            // Update branch inventory
            var bi = await _unitOfWork.BranchInventories.Query()
                .FirstOrDefaultAsync(x => x.ProductId == dto.ProductId && x.BranchId == branchId && x.TenantId == tenantId);
            if (bi == null)
            {
                bi = new BranchInventory
                {
                    TenantId = tenantId, BranchId = branchId, ProductId = dto.ProductId,
                    Quantity = dto.Quantity, ReorderLevel = product.LowStockThreshold ?? 10,
                    LastUpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.BranchInventories.AddAsync(bi);
            }
            else
            {
                bi.Quantity += dto.Quantity; bi.LastUpdatedAt = DateTime.UtcNow;
                _unitOfWork.BranchInventories.Update(bi);
            }

            // Stock movement
            var sm = new StockMovement
            {
                TenantId = tenantId, BranchId = branchId, ProductId = dto.ProductId,
                Type = StockMovementType.Adjustment, Quantity = dto.Quantity,
                ReferenceType = "BatchManual", BalanceBefore = bi.Quantity - dto.Quantity,
                BalanceAfter = bi.Quantity, Reason = $"إنشاء باتش: {dto.BatchNumber}",
                UserId = userId, CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.StockMovements.AddAsync(sm);
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetByIdAsync(batch.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var batch = await _unitOfWork.ProductBatches.Query()
            .FirstOrDefaultAsync(pb => pb.Id == id && pb.TenantId == tenantId && !pb.IsDeleted);

        if (batch == null)
            return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "الباتش غير موجود");

        if (batch.Quantity > 0)
            return ApiResponse<bool>.Fail(ErrorCodes.BATCH_HAS_QUANTITY, ErrorMessages.Get(ErrorCodes.BATCH_HAS_QUANTITY));

        var hasOrderItems = await _unitOfWork.OrderItems.Query()
            .AnyAsync(oi => oi.BatchId == id && !oi.IsDeleted);
        if (hasOrderItems)
            return ApiResponse<bool>.Fail(ErrorCodes.BATCH_HAS_ORDERS, ErrorMessages.Get(ErrorCodes.BATCH_HAS_ORDERS));

        var hasStockMovements = await _unitOfWork.StockMovements.Query()
            .AnyAsync(sm => sm.BatchId == id && !sm.IsDeleted);
        if (hasStockMovements)
            return ApiResponse<bool>.Fail(ErrorCodes.BATCH_HAS_MOVEMENTS, ErrorMessages.Get(ErrorCodes.BATCH_HAS_MOVEMENTS));

        batch.IsDeleted = true; batch.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.ProductBatches.Update(batch);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<BatchExpirySummaryDto>> GetExpiryAlertsAsync(int? branchId = null)
    {
        var tenantId = _currentUser.TenantId;
        var query = _unitOfWork.ProductBatches.Query()
            .Where(pb => pb.TenantId == tenantId && !pb.IsDeleted && pb.Status != BatchStatus.Depleted);

        if (branchId.HasValue)
            query = query.Where(pb => pb.BranchId == branchId.Value);

        var batches = await query.Include(pb => pb.Product).ToListAsync();
        var tenant = await _unitOfWork.Tenants.Query().FirstOrDefaultAsync(t => t.Id == tenantId);
        var alertDays = tenant?.ExpiryAlertDays ?? 30;
        if (alertDays <= 0)
            return ApiResponse<BatchExpirySummaryDto>.Ok(new BatchExpirySummaryDto
            {
                TotalBatches = batches.Count,
                ExpiredBatches = 0,
                NearExpiryBatches = 0,
                Alerts = new List<BatchExpiryAlertDto>()
            });
        var alerts = new List<BatchExpiryAlertDto>();

        foreach (var batch in batches)
        {
            var days = (batch.ExpiryDate.Date - DateTime.UtcNow.Date).Days;
            if (days < 0 || days <= alertDays)
            {
                var level = days < 0 || days <= 7 ? "critical" : "warning";
                if (days < 0 && batch.Status == BatchStatus.Active)
                { batch.Status = BatchStatus.Expired; _unitOfWork.ProductBatches.Update(batch); }
                alerts.Add(new BatchExpiryAlertDto
                {
                    Id = batch.Id, BatchNumber = batch.BatchNumber,
                    ProductId = batch.ProductId,
                    ProductName = batch.Product?.Name ?? "",
                    Quantity = batch.Quantity,
                    ExpiryDate = batch.ExpiryDate,
                    DaysUntilExpiry = days, AlertLevel = level
                });
            }
        }
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<BatchExpirySummaryDto>.Ok(new BatchExpirySummaryDto
        {
            TotalBatches = batches.Count,
            ExpiredBatches = batches.Count(b => b.Status == BatchStatus.Expired),
            NearExpiryBatches = alerts.Count(a => a.AlertLevel == "warning"),
            Alerts = alerts.OrderBy(a => a.DaysUntilExpiry).ToList()
        });
    }

    public async Task<ApiResponse<List<ProductBatchDto>>> GetByProductAsync(int productId, int? branchId = null)
    {
        var tenantId = _currentUser.TenantId;
        var query = _unitOfWork.ProductBatches.Query()
            .Where(pb => pb.TenantId == tenantId && pb.ProductId == productId && !pb.IsDeleted && pb.Status != BatchStatus.Depleted);

        if (branchId.HasValue)
            query = query.Where(pb => pb.BranchId == branchId.Value);

        var items = await query
            .OrderBy(pb => pb.ExpiryDate)
            .Include(pb => pb.Product)
            .Select(pb => MapToDto(pb))
            .ToListAsync();
        return ApiResponse<List<ProductBatchDto>>.Ok(items);
    }

    private static ProductBatchDto MapToDto(ProductBatch pb)
    {
        var days = (pb.ExpiryDate.Date - DateTime.UtcNow.Date).Days;
        return new ProductBatchDto
        {
            Id = pb.Id, BatchNumber = pb.BatchNumber,
            ProductId = pb.ProductId, ProductName = pb.Product?.Name ?? "",
            Quantity = pb.Quantity, InitialQuantity = pb.InitialQuantity,
            ExpiryDate = pb.ExpiryDate, PurchaseDate = pb.PurchaseDate,
            ProductionDate = pb.ProductionDate, CostPrice = pb.CostPrice,
            SupplierName = pb.SupplierName, Status = pb.Status.ToString(),
            Notes = pb.Notes, DaysUntilExpiry = days,
            BranchId = pb.BranchId, BranchName = pb.Branch?.Name ?? ""
        };
    }
}
