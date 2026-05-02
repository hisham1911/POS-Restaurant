namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.ProductBatches;
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
            .OrderBy(pb => pb.PurchaseDate)
            .ThenBy(pb => pb.CreatedAt)
            .ThenBy(pb => pb.Id)
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
        var batchNumber = string.IsNullOrWhiteSpace(dto.BatchNumber)
            ? null
            : dto.BatchNumber.Trim();

        var product = await _unitOfWork.Products.Query()
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.TenantId == tenantId);

        if (product == null)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

        if (dto.Quantity <= 0)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.VALIDATION_ERROR, "كمية الدفعة مطلوبة");

        if (!dto.CostPrice.HasValue || dto.CostPrice.Value < 0)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.VALIDATION_ERROR, "سعر تكلفة الدفعة مطلوب");

        if (!dto.SellingPrice.HasValue || dto.SellingPrice.Value <= 0)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.VALIDATION_ERROR, "سعر بيع الدفعة مطلوب");

        if (!string.IsNullOrWhiteSpace(batchNumber))
        {
            var batchExists = await _unitOfWork.ProductBatches.Query()
                .AnyAsync(b => b.BatchNumber == batchNumber
                            && b.ProductId == dto.ProductId
                            && b.BranchId == branchId
                            && b.TenantId == tenantId
                            && !b.IsDeleted
                            && b.Status != BatchStatus.Depleted);
            if (batchExists)
            {
                return ApiResponse<ProductBatchDto>.Fail(
                    ErrorCodes.BATCH_NUMBER_DUPLICATE,
                    ErrorMessages.Get(ErrorCodes.BATCH_NUMBER_DUPLICATE));
            }
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var batch = new ProductBatch
            {
                TenantId = tenantId, BranchId = branchId, ProductId = dto.ProductId,
                BatchNumber = batchNumber, Quantity = dto.Quantity, InitialQuantity = dto.Quantity,
                ExpiryDate = dto.ExpiryDate, PurchaseDate = DateTime.UtcNow,
                ProductionDate = dto.ProductionDate, CostPrice = dto.CostPrice,
                SellingPrice = dto.SellingPrice,
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

            // T-4: Stock movement - use Receiving type for batch creation
            var sm = new StockMovement
            {
                TenantId = tenantId, BranchId = branchId, ProductId = dto.ProductId,
                Type = StockMovementType.Receiving, Quantity = dto.Quantity,
                ReferenceType = "BatchManual", BalanceBefore = bi.Quantity - dto.Quantity,
                BalanceAfter = bi.Quantity, Reason = $"Batch created: {batchNumber}",
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
        var today = DateTime.UtcNow.Date;
        if (alertDays <= 0)
            return ApiResponse<BatchExpirySummaryDto>.Ok(new BatchExpirySummaryDto
            {
                TotalBatches = batches.Count,
                ExpiredBatches = batches.Count(b => (b.ExpiryDate.HasValue && b.ExpiryDate.Value.Date < today) || b.Status == BatchStatus.Expired),
                NearExpiryBatches = 0,
                Alerts = new List<BatchExpiryAlertDto>()
            });
        var alerts = new List<BatchExpiryAlertDto>();

        foreach (var batch in batches)
        {
            if (!batch.ExpiryDate.HasValue)
                continue;

            var days = (batch.ExpiryDate.Value.Date - today).Days;
            if (days < 0 || days <= alertDays)
            {
                var level = days < 0 || days <= 7 ? "critical" : "warning";
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

        return ApiResponse<BatchExpirySummaryDto>.Ok(new BatchExpirySummaryDto
        {
            TotalBatches = batches.Count,
            ExpiredBatches = batches.Count(b => (b.ExpiryDate.HasValue && b.ExpiryDate.Value.Date < today) || b.Status == BatchStatus.Expired),
            NearExpiryBatches = alerts.Count(a => a.AlertLevel == "warning"),
            Alerts = alerts.OrderBy(a => a.DaysUntilExpiry).ToList()
        });
    }

    public async Task<ApiResponse<int>> UpdateExpiredBatchesStatusAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var expiredBatches = await _unitOfWork.ProductBatches.Query()
            .Where(b => b.TenantId == _currentUser.TenantId
                     && !b.IsDeleted
                     && b.Status == BatchStatus.Active
                     && b.ExpiryDate.HasValue
                     && b.ExpiryDate.Value.Date < today)
            .ToListAsync(ct);

        foreach (var batch in expiredBatches)
        {
            batch.Status = BatchStatus.Expired;
            batch.StatusUpdatedAt = DateTime.UtcNow;
            batch.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ProductBatches.Update(batch);
        }

        if (expiredBatches.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return ApiResponse<int>.Ok(expiredBatches.Count);
    }

    public async Task<ApiResponse<List<ProductBatchDto>>> GetByProductAsync(int productId, int? branchId = null)
    {
        var tenantId = _currentUser.TenantId;
        var query = _unitOfWork.ProductBatches.Query()
            .Where(pb => pb.TenantId == tenantId && pb.ProductId == productId && !pb.IsDeleted && pb.Status != BatchStatus.Depleted);

        if (branchId.HasValue)
            query = query.Where(pb => pb.BranchId == branchId.Value);

        var batches = await query
            .OrderBy(pb => pb.PurchaseDate)
            .ThenBy(pb => pb.CreatedAt)
            .ThenBy(pb => pb.Id)
            .Include(pb => pb.Product)
            .ToListAsync();
        var items = batches.Select(MapToDto).ToList();
        return ApiResponse<List<ProductBatchDto>>.Ok(items);
    }

    public async Task<ApiResponse<List<ProductBatchDto>>> GetAvailableBatchesAsync(int productId, int branchId)
    {
        var tenantId = _currentUser.TenantId;
        
        // Get only Active batches with quantity > 0, ordered by FIFO (oldest received first)
        var batches = await _unitOfWork.ProductBatches.Query()
            .AsNoTracking() // ✅ Performance - read-only query
            .Where(pb => pb.TenantId == tenantId
                && pb.BranchId == branchId
                && pb.ProductId == productId
                && !pb.IsDeleted
                && pb.Status == BatchStatus.Active
                && pb.Quantity > 0)
            .OrderBy(pb => pb.PurchaseDate)
            .ThenBy(pb => pb.CreatedAt)
            .ThenBy(pb => pb.Id)
            .Include(pb => pb.Product)
            .Include(pb => pb.Branch)
            .ToListAsync();

        var items = batches.Select(MapToDto).ToList();

        // Mark first batch as recommended (FIFO)
        if (items.Count > 0)
            items[0].IsRecommended = true;

        return ApiResponse<List<ProductBatchDto>>.Ok(items);
    }

    private static ProductBatchDto MapToDto(ProductBatch pb)
    {
        int? days = pb.ExpiryDate.HasValue
            ? (pb.ExpiryDate.Value.Date - DateTime.UtcNow.Date).Days
            : null;
        return new ProductBatchDto
        {
            Id = pb.Id, BatchNumber = pb.BatchNumber,
            ProductId = pb.ProductId, ProductName = pb.Product?.Name ?? "",
            Quantity = pb.Quantity, InitialQuantity = pb.InitialQuantity,
            ExpiryDate = pb.ExpiryDate, PurchaseDate = pb.PurchaseDate,
            ProductionDate = pb.ProductionDate, CostPrice = pb.CostPrice,
            SellingPrice = pb.SellingPrice,
            SupplierName = pb.SupplierName, Status = pb.Status.ToString(),
            Notes = pb.Notes, DaysUntilExpiry = days,
            BranchId = pb.BranchId, BranchName = pb.Branch?.Name ?? ""
        };
    }

    public async Task<ApiResponse<ProductBatchDto>> UpdateAsync(int id, UpdateProductBatchDto dto)
    {
        var tenantId = _currentUser.TenantId;
        var batch = await _unitOfWork.ProductBatches.Query()
            .FirstOrDefaultAsync(pb => pb.Id == id && pb.TenantId == tenantId && !pb.IsDeleted);

        if (batch == null)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.NOT_FOUND, "الباتش غير موجود");

        if (!dto.SellingPrice.HasValue || dto.SellingPrice.Value <= 0)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.VALIDATION_ERROR, "سعر بيع الدفعة مطلوب");

        // Update allowed fields only (Quantity is NOT allowed here - use Adjustment instead)
        batch.BatchNumber = string.IsNullOrWhiteSpace(dto.BatchNumber)
            ? null
            : dto.BatchNumber.Trim();
        batch.ExpiryDate = dto.ExpiryDate;
        batch.ProductionDate = dto.ProductionDate;
        batch.SellingPrice = dto.SellingPrice;
        batch.Notes = dto.Notes;
        batch.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ProductBatches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ApiResponse<ProductBatchDto>> HoldAsync(int id, string reason)
    {
        var tenantId = _currentUser.TenantId;
        var batch = await _unitOfWork.ProductBatches.Query()
            .FirstOrDefaultAsync(pb => pb.Id == id && pb.TenantId == tenantId && !pb.IsDeleted);

        if (batch == null)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.NOT_FOUND, "الباتش غير موجود");

        if (batch.Status == BatchStatus.OnHold)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.VALIDATION_ERROR, "الباتش موقوف بالفعل");

        var oldStatus = batch.Status;
        batch.Status = BatchStatus.OnHold;
        batch.StatusUpdatedAt = DateTime.UtcNow;
        batch.Notes = string.IsNullOrWhiteSpace(batch.Notes)
            ? $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] تم الإيقاف: {reason}"
            : $"{batch.Notes}\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] تم الإيقاف من {oldStatus}: {reason}";
        batch.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ProductBatches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<ApiResponse<ProductBatchDto>> ReleaseAsync(int id, string reason)
    {
        var tenantId = _currentUser.TenantId;
        var batch = await _unitOfWork.ProductBatches.Query()
            .FirstOrDefaultAsync(pb => pb.Id == id && pb.TenantId == tenantId && !pb.IsDeleted);

        if (batch == null)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.NOT_FOUND, "الباتش غير موجود");

        if (batch.Status != BatchStatus.OnHold)
            return ApiResponse<ProductBatchDto>.Fail(ErrorCodes.VALIDATION_ERROR, "الباتش ليس موقوفاً");

        // Determine new status based on current state
        var newStatus = BatchStatus.Active;
        if (batch.Quantity <= 0)
            newStatus = BatchStatus.Depleted;
        else if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            newStatus = BatchStatus.Expired;

        batch.Status = newStatus;
        batch.StatusUpdatedAt = DateTime.UtcNow;
        batch.Notes = string.IsNullOrWhiteSpace(batch.Notes)
            ? $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] تم التفعيل: {reason}"
            : $"{batch.Notes}\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] تم التفعيل إلى {newStatus}: {reason}";
        batch.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ProductBatches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id);
    }
}

