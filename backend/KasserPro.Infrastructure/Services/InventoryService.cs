namespace KasserPro.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Inventory;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<InventoryService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<List<BranchInventoryDto>>> GetBranchInventoryAsync(int branchId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;

            _logger.LogInformation("GetBranchInventory called: BranchId={BranchId}, TenantId={TenantId}, UserId={UserId}",
                branchId, tenantId, userId);

            var inventories = await _context.BranchInventories
                .Where(i => i.TenantId == tenantId && i.BranchId == branchId)
                .Include(i => i.Branch)
                .Include(i => i.Product)
                .OrderBy(i => i.Product.Name)
                .ToListAsync();

            _logger.LogInformation("Found {Count} inventory records for BranchId={BranchId}, TenantId={TenantId}",
                inventories.Count, branchId, tenantId);

            var dtos = inventories.Select(i => new BranchInventoryDto
            {
                Id = i.Id,
                BranchId = i.BranchId,
                BranchName = i.Branch.Name,
                ProductId = i.ProductId,
                ProductName = i.Product.Name,
                ProductSku = i.Product.Sku,
                ProductBarcode = i.Product.Barcode,
                Quantity = i.Quantity,
                ReorderLevel = i.ReorderLevel,
                IsLowStock = i.Quantity <= i.ReorderLevel,
                LastUpdatedAt = i.LastUpdatedAt
            }).ToList();

            return ApiResponse<List<BranchInventoryDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branch inventory for branch {BranchId}", branchId);
            return ApiResponse<List<BranchInventoryDto>>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء جلب المخزون");
        }
    }

    public async Task<ApiResponse<BranchInventorySummaryDto>> GetProductInventoryAcrossBranchesAsync(int productId)
    {
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId &&
                                        p.TenantId == _currentUserService.TenantId);

            if (product == null)
                return ApiResponse<BranchInventorySummaryDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

            var inventories = await _context.BranchInventories
                .Where(i => i.TenantId == _currentUserService.TenantId && i.ProductId == productId)
                .Include(i => i.Branch)
                .Include(i => i.Product)
                .OrderBy(i => i.Branch.Name)
                .ToListAsync();

            var summary = new BranchInventorySummaryDto
            {
                ProductId = productId,
                ProductName = product.Name,
                ProductSku = product.Sku,
                TotalQuantity = inventories.Sum(i => i.Quantity),
                BranchInventories = inventories
                    .Where(i => i.Branch != null && i.Product != null)
                    .Select(i => new BranchInventoryDto
                {
                    Id = i.Id,
                    BranchId = i.BranchId,
                    BranchName = i.Branch.Name,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSku = i.Product.Sku,
                    ProductBarcode = i.Product.Barcode,
                    Quantity = i.Quantity,
                    ReorderLevel = i.ReorderLevel,
                    IsLowStock = i.Quantity <= i.ReorderLevel,
                    LastUpdatedAt = i.LastUpdatedAt
                }).ToList()
            };

            return ApiResponse<BranchInventorySummaryDto>.Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product inventory for product {ProductId}", productId);
            return ApiResponse<BranchInventorySummaryDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء جلب المخزون");
        }
    }

    public async Task<ApiResponse<List<BranchInventoryDto>>> GetLowStockItemsAsync(int? branchId = null)
    {
        try
        {
            var query = _context.BranchInventories
                .Where(i => i.TenantId == _currentUserService.TenantId &&
                           i.Quantity <= i.ReorderLevel);

            if (branchId.HasValue)
                query = query.Where(i => i.BranchId == branchId.Value);

            var inventories = await query
                .Include(i => i.Branch)
                .Include(i => i.Product)
                .OrderBy(i => i.Branch.Name)
                .ThenBy(i => i.Product.Name)
                .ToListAsync();

            var dtos = inventories
                .Where(i => i.Branch != null && i.Product != null)
                .Select(i => new BranchInventoryDto
            {
                Id = i.Id,
                BranchId = i.BranchId,
                BranchName = i.Branch.Name,
                ProductId = i.ProductId,
                ProductName = i.Product.Name,
                ProductSku = i.Product.Sku,
                ProductBarcode = i.Product.Barcode,
                Quantity = i.Quantity,
                ReorderLevel = i.ReorderLevel,
                IsLowStock = true,
                LastUpdatedAt = i.LastUpdatedAt
            }).ToList();

            return ApiResponse<List<BranchInventoryDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low stock items");
            return ApiResponse<List<BranchInventoryDto>>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء جلب المنتجات المنخفضة");
        }
    }

    public async Task<ApiResponse<BranchInventoryDto>> AdjustInventoryAsync(AdjustInventoryRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == request.BranchId && b.TenantId == _currentUserService.TenantId);
            if (branch == null)
                return ApiResponse<BranchInventoryDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, "الفرع غير موجود");

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == _currentUserService.TenantId);
            if (product == null)
                return ApiResponse<BranchInventoryDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

            var inventory = await _context.BranchInventories
                .FirstOrDefaultAsync(i => i.BranchId == request.BranchId && i.ProductId == request.ProductId);

            if (inventory == null)
            {
                inventory = new BranchInventory
                {
                    TenantId = _currentUserService.TenantId,
                    BranchId = request.BranchId,
                    ProductId = request.ProductId,
                    Quantity = 0,
                    ReorderLevel = product.ReorderPoint ?? 10,
                    LastUpdatedAt = DateTime.UtcNow
                };
                _context.BranchInventories.Add(inventory);
            }

            var newQuantity = inventory.Quantity + request.QuantityChange;
            if (newQuantity < 0)
                return ApiResponse<BranchInventoryDto>.Fail(ErrorCodes.INVENTORY_INSUFFICIENT_STOCK, "الكمية المتوفرة في المخزون غير كافية");

            var balanceBefore = inventory.Quantity;
            inventory.Quantity = newQuantity;
            inventory.LastUpdatedAt = DateTime.UtcNow;

            var movement = new StockMovement
            {
                TenantId = _currentUserService.TenantId,
                BranchId = request.BranchId,
                ProductId = request.ProductId,
                Type = StockMovementType.Adjustment,
                Quantity = request.QuantityChange,
                ReferenceType = "ManualAdjustment",
                ReferenceId = 0,
                BalanceBefore = balanceBefore,
                BalanceAfter = newQuantity,
                Reason = $"{request.Reason}{(string.IsNullOrEmpty(request.Notes) ? "" : $" - {request.Notes}")}",
                UserId = _currentUserService.UserId
            };
            _context.StockMovements.Add(movement);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _context.Entry(inventory).Reference(i => i.Branch).LoadAsync();
            await _context.Entry(inventory).Reference(i => i.Product).LoadAsync();

            var dto = new BranchInventoryDto
            {
                Id = inventory.Id,
                BranchId = inventory.BranchId,
                BranchName = inventory.Branch.Name,
                ProductId = inventory.ProductId,
                ProductName = inventory.Product.Name,
                ProductSku = inventory.Product.Sku,
                ProductBarcode = inventory.Product.Barcode,
                Quantity = inventory.Quantity,
                ReorderLevel = inventory.ReorderLevel,
                IsLowStock = inventory.Quantity <= inventory.ReorderLevel,
                LastUpdatedAt = inventory.LastUpdatedAt
            };

            return ApiResponse<BranchInventoryDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adjusting inventory");
            return ApiResponse<BranchInventoryDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء تعديل المخزون");
        }
    }

    public async Task<decimal> GetEffectivePriceAsync(int productId, int branchId)
    {
        var branchPrice = await _context.BranchProductPrices
            .Where(bp => bp.ProductId == productId &&
                        bp.BranchId == branchId &&
                        bp.IsActive &&
                        bp.EffectiveFrom <= DateTime.UtcNow &&
                        (bp.EffectiveTo == null || bp.EffectiveTo > DateTime.UtcNow))
            .OrderByDescending(bp => bp.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (branchPrice != null)
            return branchPrice.Price;

        var product = await _context.Products.FindAsync(productId);
        return product?.Price ?? 0;
    }

    public async Task<int> GetAvailableQuantityAsync(int productId, int branchId)
    {
        var inventory = await _context.BranchInventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.BranchId == branchId);

        return inventory?.Quantity ?? 0;
    }

    // Legacy compatibility methods for OrderService
    public async Task BatchDecrementStockAsync(List<(int ProductId, int Quantity)> items, int orderId)
    {
        var branchId = _currentUserService.BranchId;

        foreach (var (productId, quantity) in items)
        {
            // GUARD: Check if product tracks inventory
            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.TrackInventory)
            {
                _logger.LogInformation(
                    "Skipping stock decrement for Product={ProductId} (not found or TrackInventory=false)",
                    productId);
                continue; // Skip inventory operations for service products
            }

            var inventory = await _context.BranchInventories
                .FirstOrDefaultAsync(i => i.ProductId == productId && i.BranchId == branchId);

            if (inventory != null)
            {
                var balanceBefore = inventory.Quantity;

                // P0-3: Log warning if stock would go negative.
                // The real enforcement is in CompleteAsync's re-validation.
                // This is a defense-in-depth safety net.
                if (balanceBefore < quantity)
                {
                    _logger.LogWarning(
                        "Stock would go negative: Product={ProductId}, Branch={BranchId}, " +
                        "Available={Available}, Requested={Requested}",
                        productId, branchId, balanceBefore, quantity);
                }

                inventory.Quantity -= quantity;
                inventory.LastUpdatedAt = DateTime.UtcNow;

                // Record stock movement
                var movement = new StockMovement
                {
                    TenantId = _currentUserService.TenantId,
                    BranchId = branchId,
                    ProductId = productId,
                    Type = StockMovementType.Sale,
                    Quantity = -quantity,
                    ReferenceType = "Order",
                    ReferenceId = orderId,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = inventory.Quantity,
                    Reason = "بيع منتج",
                    UserId = _currentUserService.UserId
                };
                _context.StockMovements.Add(movement);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCurrentStockAsync(int productId)
    {
        var branchId = _currentUserService.BranchId;
        return await GetAvailableQuantityAsync(productId, branchId);
    }

    public async Task<int> IncrementStockAsync(int productId, int quantity, int referenceId)
    {
        var branchId = _currentUserService.BranchId;

        // GUARD: Check if product tracks inventory
        var product = await _context.Products.FindAsync(productId);
        if (product == null || !product.TrackInventory)
        {
            _logger.LogInformation(
                "Skipping stock increment for Product={ProductId} (not found or TrackInventory=false)",
                productId);
            return 0; // Return 0 for service products
        }

        var inventory = await _context.BranchInventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.BranchId == branchId);

        if (inventory == null)
        {
            inventory = new BranchInventory
            {
                TenantId = _currentUserService.TenantId,
                BranchId = branchId,
                ProductId = productId,
                Quantity = 0,
                ReorderLevel = product.ReorderPoint ?? 10,
                LastUpdatedAt = DateTime.UtcNow
            };
            _context.BranchInventories.Add(inventory);
        }

        var balanceBefore = inventory.Quantity;
        inventory.Quantity += quantity;
        inventory.LastUpdatedAt = DateTime.UtcNow;

        // Record stock movement
        var movement = new StockMovement
        {
            TenantId = _currentUserService.TenantId,
            BranchId = branchId,
            ProductId = productId,
            Type = StockMovementType.Refund,
            Quantity = quantity,
            ReferenceType = "OrderRefund",
            ReferenceId = referenceId,
            BalanceBefore = balanceBefore,
            BalanceAfter = inventory.Quantity,
            Reason = "إرجاع منتج",
            UserId = _currentUserService.UserId
        };
        _context.StockMovements.Add(movement);

        await _context.SaveChangesAsync();

        return inventory.Quantity;
    }

    public async Task<int> GetRestorableQuantityAsync(int productId, int orderId)
    {
        var tenantId = _currentUserService.TenantId;
        var branchId = _currentUserService.BranchId;

        // Actual decremented quantity from sale movements for this order/product.
        var actualDecremented = await _context.StockMovements
            .Where(m => m.TenantId == tenantId &&
                        m.BranchId == branchId &&
                        m.ProductId == productId &&
                        m.ReferenceType == "Order" &&
                        m.ReferenceId == orderId &&
                        m.Quantity < 0)
            .SumAsync(m => (int?)(-m.Quantity)) ?? 0;

        // Already restored quantity from refund movements for this order/product.
        var alreadyRestored = await _context.StockMovements
            .Where(m => m.TenantId == tenantId &&
                        m.BranchId == branchId &&
                        m.ProductId == productId &&
                        m.ReferenceType == "OrderRefund" &&
                        m.ReferenceId == orderId &&
                        m.Quantity > 0)
            .SumAsync(m => (int?)m.Quantity) ?? 0;

        var restorable = actualDecremented - alreadyRestored;
        return restorable > 0 ? restorable : 0;
    }

    public async Task<ApiResponse<InventoryTransferDto>> CreateTransferAsync(CreateTransferRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Validate branches
            var fromBranch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == request.FromBranchId && b.TenantId == _currentUserService.TenantId);
            if (fromBranch == null)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, "الفرع المصدر غير موجود");

            var toBranch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == request.ToBranchId && b.TenantId == _currentUserService.TenantId);
            if (toBranch == null)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, "الفرع الوجهة غير موجود");

            if (request.FromBranchId == request.ToBranchId)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_SAME_BRANCH, "لا يمكن نقل المخزون لنفس الفرع");

            // Validate product
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == _currentUserService.TenantId);
            if (product == null)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

            // Check source inventory
            var sourceInventory = await _context.BranchInventories
                .FirstOrDefaultAsync(i => i.BranchId == request.FromBranchId && i.ProductId == request.ProductId);

            if (sourceInventory == null || sourceInventory.Quantity < request.Quantity)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_INSUFFICIENT_STOCK, "الكمية المتوفرة في المخزون غير كافية");

            // Generate transfer number
            var transferCount = await _context.InventoryTransfers.CountAsync(t => t.TenantId == _currentUserService.TenantId);
            var transferNumber = $"IT-{DateTime.UtcNow:yyyy}-{(transferCount + 1):D4}";

            // Get current user
            var user = await _context.Users.FindAsync(_currentUserService.UserId);

            // Create transfer
            var transfer = new InventoryTransfer
            {
                TenantId = _currentUserService.TenantId,
                TransferNumber = transferNumber,
                FromBranchId = request.FromBranchId,
                ToBranchId = request.ToBranchId,
                ProductId = request.ProductId,
                ProductName = product.Name,
                ProductSku = product.Sku,
                Quantity = request.Quantity,
                Status = InventoryTransferStatus.Pending,
                Reason = request.Reason,
                Notes = request.Notes,
                CreatedByUserId = _currentUserService.UserId,
                CreatedByUserName = user?.Name ?? "Unknown"
            };

            _context.InventoryTransfers.Add(transfer);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Load navigation properties
            await _context.Entry(transfer).Reference(t => t.FromBranch).LoadAsync();
            await _context.Entry(transfer).Reference(t => t.ToBranch).LoadAsync();
            await _context.Entry(transfer).Reference(t => t.Product).LoadAsync();
            await _context.Entry(transfer).Reference(t => t.CreatedByUser).LoadAsync();

            var dto = MapTransferToDto(transfer);
            return ApiResponse<InventoryTransferDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating inventory transfer");
            return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء إنشاء عملية النقل");
        }
    }

    public async Task<ApiResponse<InventoryTransferDto>> ApproveTransferAsync(int transferId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var transfer = await _context.InventoryTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.Product)
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == transferId && t.TenantId == _currentUserService.TenantId);

            if (transfer == null)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_NOT_FOUND, "عملية النقل غير موجودة");

            if (transfer.Status != InventoryTransferStatus.Pending)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_ALREADY_APPROVED, "عملية النقل تمت الموافقة عليها بالفعل");

            // Check source inventory again
            var sourceInventory = await _context.BranchInventories
                .FirstOrDefaultAsync(i => i.BranchId == transfer.FromBranchId && i.ProductId == transfer.ProductId);

            if (sourceInventory == null || sourceInventory.Quantity < transfer.Quantity)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_INSUFFICIENT_STOCK, "الكمية المتوفرة في المخزون غير كافية");

            // Deduct from source
            var balanceBefore = sourceInventory.Quantity;
            sourceInventory.Quantity -= transfer.Quantity;
            sourceInventory.LastUpdatedAt = DateTime.UtcNow;

            // Record stock movement for source
            var sourceMovement = new StockMovement
            {
                TenantId = _currentUserService.TenantId,
                BranchId = transfer.FromBranchId,
                ProductId = transfer.ProductId,
                Type = StockMovementType.Transfer,
                Quantity = -transfer.Quantity,
                ReferenceType = "InventoryTransfer",
                ReferenceId = transfer.Id,
                BalanceBefore = balanceBefore,
                BalanceAfter = sourceInventory.Quantity,
                Reason = $"نقل إلى {transfer.ToBranch.Name} - {transfer.Reason}",
                UserId = _currentUserService.UserId
            };
            _context.StockMovements.Add(sourceMovement);

            // Get current user
            var user = await _context.Users.FindAsync(_currentUserService.UserId);

            // Update transfer status
            transfer.Status = InventoryTransferStatus.Approved;
            transfer.ApprovedByUserId = _currentUserService.UserId;
            transfer.ApprovedByUserName = user?.Name ?? "Unknown";
            transfer.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Load approved user
            await _context.Entry(transfer).Reference(t => t.ApprovedByUser).LoadAsync();

            var dto = MapTransferToDto(transfer);
            return ApiResponse<InventoryTransferDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error approving inventory transfer {TransferId}", transferId);
            return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء الموافقة على عملية النقل");
        }
    }

    public async Task<ApiResponse<InventoryTransferDto>> ReceiveTransferAsync(int transferId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var transfer = await _context.InventoryTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ApprovedByUser)
                .FirstOrDefaultAsync(t => t.Id == transferId && t.TenantId == _currentUserService.TenantId);

            if (transfer == null)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_NOT_FOUND, "عملية النقل غير موجودة");

            if (transfer.Status != InventoryTransferStatus.Approved)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_NOT_APPROVED, "عملية النقل غير موافق عليها");

            // Get or create destination inventory
            var destInventory = await _context.BranchInventories
                .FirstOrDefaultAsync(i => i.BranchId == transfer.ToBranchId && i.ProductId == transfer.ProductId);

            if (destInventory == null)
            {
                destInventory = new BranchInventory
                {
                    TenantId = _currentUserService.TenantId,
                    BranchId = transfer.ToBranchId,
                    ProductId = transfer.ProductId,
                    Quantity = 0,
                    ReorderLevel = transfer.Product.ReorderPoint ?? 10,
                    LastUpdatedAt = DateTime.UtcNow
                };
                _context.BranchInventories.Add(destInventory);
            }

            // Add to destination
            var balanceBefore = destInventory.Quantity;
            destInventory.Quantity += transfer.Quantity;
            destInventory.LastUpdatedAt = DateTime.UtcNow;

            // Record stock movement for destination
            var destMovement = new StockMovement
            {
                TenantId = _currentUserService.TenantId,
                BranchId = transfer.ToBranchId,
                ProductId = transfer.ProductId,
                Type = StockMovementType.Transfer,
                Quantity = transfer.Quantity,
                ReferenceType = "InventoryTransfer",
                ReferenceId = transfer.Id,
                BalanceBefore = balanceBefore,
                BalanceAfter = destInventory.Quantity,
                Reason = $"استلام من {transfer.FromBranch.Name} - {transfer.Reason}",
                UserId = _currentUserService.UserId
            };
            _context.StockMovements.Add(destMovement);

            // Get current user
            var user = await _context.Users.FindAsync(_currentUserService.UserId);

            // Update transfer status
            transfer.Status = InventoryTransferStatus.Completed;
            transfer.ReceivedByUserId = _currentUserService.UserId;
            transfer.ReceivedByUserName = user?.Name ?? "Unknown";
            transfer.ReceivedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Load received user
            await _context.Entry(transfer).Reference(t => t.ReceivedByUser).LoadAsync();

            var dto = MapTransferToDto(transfer);
            return ApiResponse<InventoryTransferDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error receiving inventory transfer {TransferId}", transferId);
            return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء استلام عملية النقل");
        }
    }

    public async Task<ApiResponse<InventoryTransferDto>> CancelTransferAsync(int transferId, CancelTransferRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var transfer = await _context.InventoryTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ApprovedByUser)
                .FirstOrDefaultAsync(t => t.Id == transferId && t.TenantId == _currentUserService.TenantId);

            if (transfer == null)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_NOT_FOUND, "عملية النقل غير موجودة");

            if (transfer.Status == InventoryTransferStatus.Completed || transfer.Status == InventoryTransferStatus.Cancelled)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_ALREADY_COMPLETED, "عملية النقل مكتملة أو ملغاة بالفعل");

            // If approved, return stock to source
            if (transfer.Status == InventoryTransferStatus.Approved)
            {
                var sourceInventory = await _context.BranchInventories
                    .FirstOrDefaultAsync(i => i.BranchId == transfer.FromBranchId && i.ProductId == transfer.ProductId);

                if (sourceInventory != null)
                {
                    var balanceBefore = sourceInventory.Quantity;
                    sourceInventory.Quantity += transfer.Quantity;
                    sourceInventory.LastUpdatedAt = DateTime.UtcNow;

                    // Record stock movement
                    var movement = new StockMovement
                    {
                        TenantId = _currentUserService.TenantId,
                        BranchId = transfer.FromBranchId,
                        ProductId = transfer.ProductId,
                        Type = StockMovementType.Adjustment,
                        Quantity = transfer.Quantity,
                        ReferenceType = "InventoryTransferCancellation",
                        ReferenceId = transfer.Id,
                        BalanceBefore = balanceBefore,
                        BalanceAfter = sourceInventory.Quantity,
                        Reason = $"إلغاء نقل - {request.Reason}",
                        UserId = _currentUserService.UserId
                    };
                    _context.StockMovements.Add(movement);
                }
            }

            // Get current user
            var user = await _context.Users.FindAsync(_currentUserService.UserId);

            // Update transfer status
            transfer.Status = InventoryTransferStatus.Cancelled;
            transfer.CancelledByUserId = _currentUserService.UserId;
            transfer.CancelledByUserName = user?.Name ?? "Unknown";
            transfer.CancelledAt = DateTime.UtcNow;
            transfer.CancellationReason = request.Reason;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = MapTransferToDto(transfer);
            return ApiResponse<InventoryTransferDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error cancelling inventory transfer {TransferId}", transferId);
            return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء إلغاء عملية النقل");
        }
    }

    public async Task<ApiResponse<InventoryTransferDto>> GetTransferByIdAsync(int transferId)
    {
        try
        {
            var transfer = await _context.InventoryTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ApprovedByUser)
                .Include(t => t.ReceivedByUser)
                .FirstOrDefaultAsync(t => t.Id == transferId && t.TenantId == _currentUserService.TenantId);

            if (transfer == null)
                return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INVENTORY_TRANSFER_NOT_FOUND, "عملية النقل غير موجودة");

            var dto = MapTransferToDto(transfer);
            return ApiResponse<InventoryTransferDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory transfer {TransferId}", transferId);
            return ApiResponse<InventoryTransferDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء جلب عملية النقل");
        }
    }

    public async Task<ApiResponse<PaginatedResponse<InventoryTransferDto>>> GetTransfersAsync(
        int? fromBranchId = null, int? toBranchId = null, string? status = null,
        int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            var query = _context.InventoryTransfers
                .Where(t => t.TenantId == _currentUserService.TenantId)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.Product)
                .Include(t => t.CreatedByUser)
                .AsQueryable();

            if (fromBranchId.HasValue)
                query = query.Where(t => t.FromBranchId == fromBranchId.Value);

            if (toBranchId.HasValue)
                query = query.Where(t => t.ToBranchId == toBranchId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<InventoryTransferStatus>(status, true, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            var totalCount = await query.CountAsync();
            var transfers = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = transfers.Select(MapTransferToDto).ToList();

            var response = new PaginatedResponse<InventoryTransferDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return ApiResponse<PaginatedResponse<InventoryTransferDto>>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory transfers");
            return ApiResponse<PaginatedResponse<InventoryTransferDto>>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء جلب عمليات النقل");
        }
    }

    public async Task<ApiResponse<List<BranchProductPriceDto>>> GetBranchPricesAsync(int branchId)
    {
        try
        {
            var prices = await _context.BranchProductPrices
                .Where(bp => bp.BranchId == branchId &&
                            bp.TenantId == _currentUserService.TenantId &&
                            bp.IsActive)
                .Include(bp => bp.Product)
                .Include(bp => bp.Branch)
                .OrderBy(bp => bp.Product.Name)
                .ToListAsync();

            var dtos = prices.Select(bp => new BranchProductPriceDto
            {
                Id = bp.Id,
                BranchId = bp.BranchId,
                BranchName = bp.Branch.Name,
                ProductId = bp.ProductId,
                ProductName = bp.Product.Name,
                Price = bp.Price,
                DefaultPrice = bp.Product.Price,
                EffectiveFrom = bp.EffectiveFrom,
                EffectiveTo = bp.EffectiveTo,
                IsActive = bp.IsActive
            }).ToList();

            return ApiResponse<List<BranchProductPriceDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branch prices for branch {BranchId}", branchId);
            return ApiResponse<List<BranchProductPriceDto>>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء جلب أسعار الفرع");
        }
    }

    public async Task<ApiResponse<BranchProductPriceDto>> SetBranchPriceAsync(SetBranchPriceRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Validate branch
            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == request.BranchId && b.TenantId == _currentUserService.TenantId);
            if (branch == null)
                return ApiResponse<BranchProductPriceDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, "الفرع غير موجود");

            // Validate product
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == _currentUserService.TenantId);
            if (product == null)
                return ApiResponse<BranchProductPriceDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

            if (request.Price < 0)
                return ApiResponse<BranchProductPriceDto>.Fail(ErrorCodes.VALIDATION_ERROR, "السعر يجب أن يكون أكبر من أو يساوي صفر");

            // Deactivate existing active prices
            var existingPrices = await _context.BranchProductPrices
                .Where(bp => bp.BranchId == request.BranchId &&
                            bp.ProductId == request.ProductId &&
                            bp.IsActive)
                .ToListAsync();

            foreach (var existing in existingPrices)
            {
                existing.IsActive = false;
                existing.EffectiveTo = DateTime.UtcNow;
            }

            // Create new price
            var branchPrice = new BranchProductPrice
            {
                TenantId = _currentUserService.TenantId,
                BranchId = request.BranchId,
                ProductId = request.ProductId,
                Price = request.Price,
                EffectiveFrom = request.EffectiveFrom ?? DateTime.UtcNow,
                EffectiveTo = null,
                IsActive = true
            };

            _context.BranchProductPrices.Add(branchPrice);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _context.Entry(branchPrice).Reference(bp => bp.Product).LoadAsync();
            await _context.Entry(branchPrice).Reference(bp => bp.Branch).LoadAsync();

            var dto = new BranchProductPriceDto
            {
                Id = branchPrice.Id,
                BranchId = branchPrice.BranchId,
                BranchName = branchPrice.Branch.Name,
                ProductId = branchPrice.ProductId,
                ProductName = branchPrice.Product.Name,
                Price = branchPrice.Price,
                DefaultPrice = branchPrice.Product.Price,
                EffectiveFrom = branchPrice.EffectiveFrom,
                EffectiveTo = branchPrice.EffectiveTo,
                IsActive = branchPrice.IsActive
            };

            return ApiResponse<BranchProductPriceDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error setting branch price");
            return ApiResponse<BranchProductPriceDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء تعيين سعر الفرع");
        }
    }

    public async Task<ApiResponse<bool>> RemoveBranchPriceAsync(int branchId, int productId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var prices = await _context.BranchProductPrices
                .Where(bp => bp.BranchId == branchId &&
                            bp.ProductId == productId &&
                            bp.TenantId == _currentUserService.TenantId &&
                            bp.IsActive)
                .ToListAsync();

            if (!prices.Any())
                return ApiResponse<bool>.Fail(ErrorCodes.BRANCH_PRICE_NOT_FOUND, "سعر الفرع غير موجود");

            foreach (var price in prices)
            {
                price.IsActive = false;
                price.EffectiveTo = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error removing branch price");
            return ApiResponse<bool>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء حذف سعر الفرع");
        }
    }

    private InventoryTransferDto MapTransferToDto(InventoryTransfer transfer)
    {
        return new InventoryTransferDto
        {
            Id = transfer.Id,
            TransferNumber = transfer.TransferNumber,
            FromBranchId = transfer.FromBranchId,
            FromBranchName = transfer.FromBranch?.Name ?? "",
            ToBranchId = transfer.ToBranchId,
            ToBranchName = transfer.ToBranch?.Name ?? "",
            ProductId = transfer.ProductId,
            ProductName = transfer.Product?.Name ?? transfer.ProductName,
            ProductSku = transfer.Product?.Sku ?? transfer.ProductSku,
            Quantity = transfer.Quantity,
            Status = transfer.Status.ToString(),
            Reason = transfer.Reason,
            Notes = transfer.Notes,
            CreatedByUserName = transfer.CreatedByUserName,
            CreatedAt = transfer.CreatedAt,
            ApprovedByUserName = transfer.ApprovedByUserName,
            ApprovedAt = transfer.ApprovedAt,
            ReceivedByUserName = transfer.ReceivedByUserName,
            ReceivedAt = transfer.ReceivedAt,
            CancelledByUserName = transfer.CancelledByUserName,
            CancelledAt = transfer.CancelledAt,
            CancellationReason = transfer.CancellationReason
        };
    }
}
