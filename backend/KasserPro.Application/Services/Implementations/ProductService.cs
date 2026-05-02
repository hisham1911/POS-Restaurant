namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Products;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ProductService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<ProductDto>>> GetAllAsync(
        int? categoryId = null,
        string? search = null,
        bool? isActive = null,
        bool? lowStock = null,
        int page = 1,
        int pageSize = 20)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 1000);

        var branchInventoryQuery = _unitOfWork.BranchInventories.Query()
            .Where(bi => bi.TenantId == tenantId && bi.BranchId == branchId);
        var query = _unitOfWork.Products.Query()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted);

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.NameEn != null && p.NameEn.ToLower().Contains(searchLower)) ||
                (p.Sku != null && p.Sku.ToLower().Contains(searchLower)) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(searchLower)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var projectedQuery = query.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            NameEn = p.NameEn,
            Description = p.Description,
            Sku = p.Sku,
            Barcode = p.Barcode,
            Price = p.Price,
            Cost = p.Cost,
            TaxRate = p.TaxRate,
            TaxInclusive = p.TaxInclusive,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive,
            Type = p.Type,
            TrackInventory = p.TrackInventory,
            IsBatchTracked = p.IsBatchTracked,
            CurrentBranchStock = p.TrackInventory
                ? branchInventoryQuery
                    .Where(bi => bi.ProductId == p.Id)
                    .Select(bi => (int?)bi.Quantity)
                    .FirstOrDefault() ?? 0
                : null,
            CategoryId = p.CategoryId,
            CategoryName = p.Category != null ? p.Category.Name : null,
            LowStockThreshold = p.LowStockThreshold,
            ReorderPoint = p.ReorderPoint,
            LastStockUpdate = p.LastStockUpdate
        });

        if (lowStock == true)
        {
            projectedQuery = projectedQuery.Where(p =>
                p.TrackInventory && (p.CurrentBranchStock ?? 0) < (p.LowStockThreshold ?? 5));
        }

        var totalCount = await projectedQuery.CountAsync();
        var pagedItems = await projectedQuery
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pagedResult = new PagedResult<ProductDto>(pagedItems, totalCount, page, pageSize);
        return ApiResponse<PagedResult<ProductDto>>.Ok(pagedResult);
    }

    public async Task<ApiResponse<ProductDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (product == null)
            return ApiResponse<ProductDto>.Fail(
                ErrorCodes.PRODUCT_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

        // Get BranchInventory for current branch
        var branchInventory = await _unitOfWork.BranchInventories.Query()
            .FirstOrDefaultAsync(bi => bi.TenantId == tenantId && bi.BranchId == branchId && bi.ProductId == product.Id);

        var branchQuantity = product.TrackInventory && branchInventory != null
            ? branchInventory.Quantity
            : (product.TrackInventory ? 0 : (int?)null);

        return ApiResponse<ProductDto>.Ok(new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            NameEn = product.NameEn,
            Description = product.Description,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Price = product.Price,
            Cost = product.Cost,
            TaxRate = product.TaxRate,
            TaxInclusive = product.TaxInclusive,
            ImageUrl = product.ImageUrl,
            IsActive = product.IsActive,
            Type = product.Type,
            TrackInventory = product.TrackInventory,
            IsBatchTracked = product.IsBatchTracked,
            CurrentBranchStock = branchQuantity,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            LowStockThreshold = product.LowStockThreshold,
            ReorderPoint = product.ReorderPoint,
            LastStockUpdate = product.LastStockUpdate
        });
    }

    public async Task<ApiResponse<ProductDto>> CreateAsync(CreateProductRequest request)
    {
        // T-3: Wrap in transaction
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Validation: Price must be non-negative
            if (request.Price < 0)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.PRODUCT_INVALID_PRICE, ErrorMessages.Get(ErrorCodes.PRODUCT_INVALID_PRICE));

            // Validation: Category must exist
            var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
            if (category == null)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));

            // T-5: SKU uniqueness validation
            if (!string.IsNullOrWhiteSpace(request.Sku))
            {
                var skuExists = await _unitOfWork.Products.Query()
                    .AnyAsync(p => p.Sku == request.Sku.Trim()
                                && p.TenantId == _currentUser.TenantId
                                && !p.IsDeleted);
                if (skuExists)
                    return ApiResponse<ProductDto>.Fail(
                        ErrorCodes.PRODUCT_SKU_DUPLICATE,
                        ErrorMessages.Get(ErrorCodes.PRODUCT_SKU_DUPLICATE));
            }

            // T-5: Barcode uniqueness validation
            if (!string.IsNullOrWhiteSpace(request.Barcode))
            {
                var barcodeExists = await _unitOfWork.Products.Query()
                    .AnyAsync(p => p.Barcode == request.Barcode.Trim()
                                && p.TenantId == _currentUser.TenantId
                                && !p.IsDeleted);
                if (barcodeExists)
                    return ApiResponse<ProductDto>.Fail(
                        ErrorCodes.PRODUCT_BARCODE_DUPLICATE,
                        ErrorMessages.Get(ErrorCodes.PRODUCT_BARCODE_DUPLICATE));
            }

            var product = new Product
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            NameEn = request.NameEn,
            Description = request.Description,
            Sku = request.Sku,
            Barcode = request.Barcode,
            Price = request.Price,
            Cost = request.Cost,
            ImageUrl = request.ImageUrl,
            CategoryId = request.CategoryId,
            // Tax settings
            TaxRate = request.TaxRate,
            TaxInclusive = request.TaxInclusive,
            // Product Type determines inventory behavior
            Type = request.Type,
            // TrackInventory is automatically set based on Type
            TrackInventory = request.Type == Domain.Enums.ProductType.Physical,
            IsBatchTracked = request.IsBatchTracked,
            LowStockThreshold = request.LowStockThreshold,
            ReorderPoint = request.ReorderPoint,
            LastStockUpdate = DateTime.UtcNow
        };

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Create BranchInventory records ONLY if TrackInventory is enabled (Physical products)
        if (product.TrackInventory)
        {
            var branches = await _unitOfWork.Branches.Query()
                .Where(b => b.TenantId == _currentUser.TenantId)
                .ToListAsync();

            foreach (var branch in branches)
            {
                // If branch-specific quantities provided, use them
                // Otherwise: current branch gets the requested quantity, other branches get 0
                int quantity;
                if (request.BranchStockQuantities?.ContainsKey(branch.Id) == true)
                {
                    quantity = request.BranchStockQuantities[branch.Id];
                }
                else
                {
                    // Only current branch gets the initial stock, others get 0
                    quantity = branch.Id == _currentUser.BranchId ? request.InitialBranchStock : 0;
                }

                var branchInventory = new BranchInventory
                {
                    TenantId = _currentUser.TenantId,
                    BranchId = branch.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    ReorderLevel = request.LowStockThreshold,
                    LastUpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.BranchInventories.AddAsync(branchInventory);

                // T-1: Create StockMovement for initial stock (only for current branch with quantity > 0)
                if (quantity > 0 && branch.Id == _currentUser.BranchId)
                {
                    var movement = new StockMovement
                    {
                        ProductId = product.Id,
                        BranchId = branch.Id,
                        TenantId = _currentUser.TenantId,
                        Type = StockMovementType.Receiving,
                        Quantity = quantity,
                        BalanceBefore = 0,
                        BalanceAfter = quantity,
                        ReferenceType = "InitialStock",
                        Reason = "مخزون أولي عند إنشاء المنتج",
                        UserId = _currentUser.UserId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.StockMovements.AddAsync(movement);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

            await transaction.CommitAsync();

            return ApiResponse<ProductDto>.Ok(new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                NameEn = product.NameEn,
                Price = product.Price,
                Cost = product.Cost,
                TaxRate = product.TaxRate,
                TaxInclusive = product.TaxInclusive,
                IsActive = product.IsActive,
                Type = product.Type,
                TrackInventory = product.TrackInventory,
                IsBatchTracked = product.IsBatchTracked,
                CurrentBranchStock = request.InitialBranchStock, // Return requested quantity for consistency
                CategoryId = product.CategoryId
            }, "تم إنشاء المنتج بنجاح");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<ProductDto>> UpdateAsync(int id, UpdateProductRequest request)
    {
        // Validation: Price must be non-negative
        if (request.Price < 0)
            return ApiResponse<ProductDto>.Fail(ErrorCodes.PRODUCT_INVALID_PRICE, ErrorMessages.Get(ErrorCodes.PRODUCT_INVALID_PRICE));

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var tenantId = _currentUser.TenantId;
            var product = await _unitOfWork.Products.Query()
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
            if (product == null)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

            // Validation: Category must exist
            var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
            if (category == null)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));

            // T-5: SKU uniqueness validation (exclude current product)
            if (!string.IsNullOrWhiteSpace(request.Sku))
            {
                var skuExists = await _unitOfWork.Products.Query()
                    .AnyAsync(p => p.Sku == request.Sku.Trim()
                                && p.TenantId == _currentUser.TenantId
                                && !p.IsDeleted
                                && p.Id != id);
                if (skuExists)
                    return ApiResponse<ProductDto>.Fail(
                        ErrorCodes.PRODUCT_SKU_DUPLICATE,
                        ErrorMessages.Get(ErrorCodes.PRODUCT_SKU_DUPLICATE));
            }

            // T-5: Barcode uniqueness validation (exclude current product)
            if (!string.IsNullOrWhiteSpace(request.Barcode))
            {
                var barcodeExists = await _unitOfWork.Products.Query()
                    .AnyAsync(p => p.Barcode == request.Barcode.Trim()
                                && p.TenantId == _currentUser.TenantId
                                && !p.IsDeleted
                                && p.Id != id);
                if (barcodeExists)
                    return ApiResponse<ProductDto>.Fail(
                        ErrorCodes.PRODUCT_BARCODE_DUPLICATE,
                        ErrorMessages.Get(ErrorCodes.PRODUCT_BARCODE_DUPLICATE));
            }

            if (request.Type != product.Type)
            {
                var hasStock = await _unitOfWork.BranchInventories.Query()
                    .AnyAsync(b => b.ProductId == product.Id
                                && b.TenantId == _currentUser.TenantId
                                && !b.IsDeleted
                                && b.Quantity > 0);
                var hasActiveBatches = await _unitOfWork.ProductBatches.Query()
                    .AnyAsync(b => b.ProductId == product.Id
                                && b.TenantId == _currentUser.TenantId
                                && !b.IsDeleted
                                && b.Status == BatchStatus.Active);
                if (hasStock || hasActiveBatches)
                {
                    return ApiResponse<ProductDto>.Fail(
                        ErrorCodes.PRODUCT_TYPE_CANNOT_CHANGE,
                        ErrorMessages.Get(ErrorCodes.PRODUCT_TYPE_CANNOT_CHANGE));
                }
            }

            var wasBatchTracked = product.IsBatchTracked;
            var willTrackInventory = request.Type == Domain.Enums.ProductType.Physical;

            if (wasBatchTracked && !request.IsBatchTracked)
            {
                var hasActiveBatches = await _unitOfWork.ProductBatches.Query()
                    .AnyAsync(b => b.ProductId == product.Id
                                && b.TenantId == tenantId
                                && !b.IsDeleted
                                && b.Status == BatchStatus.Active
                                && b.Quantity > 0);
                if (hasActiveBatches)
                {
                    return ApiResponse<ProductDto>.Fail(
                        ErrorCodes.VALIDATION_ERROR,
                        "لا يمكن إلغاء تتبع الدفعات طالما توجد دفعات نشطة بها كمية.");
                }
            }

            product.Name = request.Name;
            product.NameEn = request.NameEn;
            product.Description = request.Description;
            product.Sku = request.Sku;
            product.Barcode = request.Barcode;
            product.Price = request.Price;
            product.Cost = request.Cost;
            product.ImageUrl = request.ImageUrl;
            product.IsActive = request.IsActive;
            product.CategoryId = request.CategoryId;
            // Tax settings
            product.TaxRate = request.TaxRate;
            product.TaxInclusive = request.TaxInclusive;
            // Product Type determines inventory behavior
            product.Type = request.Type;
            // TrackInventory is automatically set based on Type
            product.TrackInventory = willTrackInventory;
            product.LowStockThreshold = request.LowStockThreshold;
            product.ReorderPoint = request.ReorderPoint;
            product.IsBatchTracked = request.IsBatchTracked;
            product.LastStockUpdate = DateTime.UtcNow;

            _unitOfWork.Products.Update(product);

            // Convert existing branch inventory into opening batches when enabling batch tracking.
            if (!wasBatchTracked && request.IsBatchTracked && willTrackInventory)
            {
                var now = DateTime.UtcNow;
                var branchInventories = await _unitOfWork.BranchInventories.Query()
                    .Where(bi => bi.TenantId == tenantId
                              && bi.ProductId == product.Id
                              && !bi.IsDeleted
                              && bi.Quantity > 0)
                    .ToListAsync();

                foreach (var inventoryRow in branchInventories)
                {
                    var branchSellingPrice = await _unitOfWork.BranchProductPrices.Query()
                        .Where(bp => bp.TenantId == tenantId
                                  && bp.ProductId == product.Id
                                  && bp.BranchId == inventoryRow.BranchId
                                  && bp.IsActive
                                  && !bp.IsDeleted
                                  && bp.EffectiveFrom <= now
                                  && (bp.EffectiveTo == null || bp.EffectiveTo >= now))
                        .OrderByDescending(bp => bp.EffectiveFrom)
                        .Select(bp => (decimal?)bp.Price)
                        .FirstOrDefaultAsync();

                    var openingBatch = new ProductBatch
                    {
                        TenantId = tenantId,
                        BranchId = inventoryRow.BranchId,
                        ProductId = product.Id,
                        BatchNumber = $"رصيد افتتاحي - تفعيل الباتش - {now:yyyyMMddHHmmssfff}",
                        Quantity = inventoryRow.Quantity,
                        InitialQuantity = inventoryRow.Quantity,
                        CostPrice = product.Cost,
                        SellingPrice = branchSellingPrice ?? product.Price,
                        PurchaseDate = now,
                        Status = BatchStatus.Active,
                        Notes = "Batch generated automatically from existing branch stock when batch tracking was enabled."
                    };

                    await _unitOfWork.ProductBatches.AddAsync(openingBatch);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();

            // Get current branch inventory for response
            var branchId = _currentUser.BranchId;
            var branchInventory = await _unitOfWork.BranchInventories.Query()
                .FirstOrDefaultAsync(bi => bi.TenantId == tenantId && bi.BranchId == branchId && bi.ProductId == product.Id);

            var branchQuantity = product.TrackInventory && branchInventory != null
                ? branchInventory.Quantity
                : (product.TrackInventory ? 0 : (int?)null);

            return ApiResponse<ProductDto>.Ok(new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                NameEn = product.NameEn,
                Price = product.Price,
                Cost = product.Cost,
                TaxRate = product.TaxRate,
                TaxInclusive = product.TaxInclusive,
                IsActive = product.IsActive,
                Type = product.Type,
                TrackInventory = product.TrackInventory,
                IsBatchTracked = product.IsBatchTracked,
                CurrentBranchStock = branchQuantity,
                CategoryId = product.CategoryId
            }, "تم تحديث المنتج بنجاح");
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
        var product = await _unitOfWork.Products.Query()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId && !p.IsDeleted);
        if (product == null)
            return ApiResponse<bool>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

        var hasInventory = await _unitOfWork.BranchInventories.Query()
            .AnyAsync(bi => bi.TenantId == tenantId
                         && !bi.IsDeleted
                         && bi.ProductId == product.Id
                         && bi.Quantity > 0);

        if (hasInventory)
        {
            return ApiResponse<bool>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "لا يمكن حذف هذا المنتج. لا يزال يوجد مخزون منه في الفرع. يرجى تصفية المخزون أولاً أو نقله.");
        }

        var hasOpenOrders = await _unitOfWork.Orders.Query()
            .AnyAsync(o => o.TenantId == tenantId
                        && !o.IsDeleted
                        && o.Status != OrderStatus.Completed
                        && o.Status != OrderStatus.Cancelled
                        && o.Items.Any(oi => !oi.IsDeleted && oi.ProductId == product.Id));

        if (hasOpenOrders)
        {
            return ApiResponse<bool>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "لا يمكن حذف هذا المنتج. يوجد طلبات مفتوحة تحتوي عليه. يرجى إغلاق أو إلغاء هذه الطلبات أولاً.");
        }

        product.IsDeleted = true;
        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف المنتج بنجاح");
    }

    public async Task<ApiResponse<StockAdjustResultDto>> AdjustStockAsync(int id, AdjustStockRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var product = await _unitOfWork.Products.Query()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (product == null)
            return ApiResponse<StockAdjustResultDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

        // Get or create BranchInventory
        var branchInventory = await _unitOfWork.BranchInventories.Query()
            .FirstOrDefaultAsync(bi => bi.ProductId == id && bi.BranchId == branchId && bi.TenantId == tenantId);

        if (branchInventory == null)
        {
            return ApiResponse<StockAdjustResultDto>.Fail(ErrorCodes.INVENTORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.INVENTORY_NOT_FOUND));
        }

        var previousBalance = branchInventory.Quantity;
        var newBalance = previousBalance + request.Quantity;
        var adjustmentReason = string.IsNullOrWhiteSpace(request.Reason)
            ? "تسوية مخزون يدوية"
            : request.Reason;

        if (newBalance < 0)
            return ApiResponse<StockAdjustResultDto>.Fail(ErrorCodes.INVENTORY_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.INVENTORY_INVALID_QUANTITY));

        branchInventory.Quantity = newBalance;
        branchInventory.LastUpdatedAt = DateTime.UtcNow;
        product.LastStockUpdate = DateTime.UtcNow;

        _unitOfWork.BranchInventories.Update(branchInventory);
        _unitOfWork.Products.Update(product);

        // T-2: Create StockMovement for adjustment
        var movement = new StockMovement
        {
            ProductId = id,
            BranchId = branchId,
            TenantId = tenantId,
            Type = StockMovementType.Adjustment,
            Quantity = request.Quantity,
            BalanceBefore = previousBalance,
            BalanceAfter = newBalance,
            ReferenceType = "ManualAdjustment",
            Reason = adjustmentReason,
            UserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.StockMovements.AddAsync(movement);

        // Record a completed stock taking entry for manual adjustments
        if (string.Equals(request.AdjustmentType, "Adjustment", StringComparison.OrdinalIgnoreCase))
        {
            var stockTakingCount = await _unitOfWork.StockTakings.Query()
                .CountAsync(st => st.TenantId == tenantId);
            var stockTakingNumber = $"ST-{DateTime.UtcNow:yyyy}-{(stockTakingCount + 1):D4}";

            var stockTaking = new StockTaking
            {
                TenantId = tenantId,
                BranchId = branchId,
                StockTakingNumber = stockTakingNumber,
                Type = StockTakingType.Partial,
                CategoryId = product.CategoryId,
                Status = StockTakingStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                CreatedByUserId = _currentUser.UserId,
                CompletedByUserId = _currentUser.UserId,
                Notes = adjustmentReason
            };

            stockTaking.Items.Add(new StockTakingItem
            {
                ProductId = product.Id,
                SystemQuantity = previousBalance,
                ActualQuantity = newBalance,
                Reason = adjustmentReason
            });

            await _unitOfWork.StockTakings.AddAsync(stockTaking);
        }

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<StockAdjustResultDto>.Ok(new StockAdjustResultDto
        {
            NewBalance = newBalance,
            PreviousBalance = previousBalance,
            Change = request.Quantity
        }, "تم تعديل المخزون بنجاح");
    }

    public async Task<ApiResponse<ProductDto>> QuickCreateAsync(QuickCreateProductRequest request)
    {
        // Use transaction for atomicity
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Validation: Name is required
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<ProductDto>.Fail(ErrorCodes.PRODUCT_NAME_REQUIRED, ErrorMessages.Get(ErrorCodes.PRODUCT_NAME_REQUIRED));

            if (request.Name.Length > 200)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.PRODUCT_NAME_TOO_LONG, ErrorMessages.Get(ErrorCodes.PRODUCT_NAME_TOO_LONG));

            // Validation: Price must be non-negative
            if (request.Price < 0)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.PRODUCT_INVALID_PRICE, ErrorMessages.Get(ErrorCodes.PRODUCT_INVALID_PRICE));

            // Validation: InitialStock must be non-negative
            if (request.InitialStock < 0)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.INVENTORY_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.INVENTORY_INVALID_QUANTITY));

            // Validation: Category must exist
            var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
            if (category == null)
                return ApiResponse<ProductDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));

            var product = new Product
            {
                TenantId = _currentUser.TenantId,
                Name = request.Name,
                Sku = request.Sku,
                Barcode = request.Barcode,
                Price = request.Price,
                ImageUrl = request.ImageUrl,
                CategoryId = request.CategoryId,
                // Quick create defaults
                IsActive = true,
                Type = request.Type,
                // TrackInventory is automatically set based on Type
                TrackInventory = request.Type == Domain.Enums.ProductType.Physical,
                LowStockThreshold = 5,
                TaxInclusive = false, // Default to tax exclusive
                LastStockUpdate = DateTime.UtcNow
            };

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            // Create BranchInventory records ONLY if TrackInventory is enabled (Physical products)
            if (product.TrackInventory)
            {
                // Create BranchInventory for all branches, but only current branch gets the initial stock
                var branches = await _unitOfWork.Branches.Query()
                    .Where(b => b.TenantId == _currentUser.TenantId)
                    .ToListAsync();

                foreach (var branch in branches)
                {
                    // Only current branch gets the initial stock, others get 0
                    var quantity = branch.Id == _currentUser.BranchId ? request.InitialStock : 0;

                    var branchInventory = new BranchInventory
                    {
                        TenantId = _currentUser.TenantId,
                        BranchId = branch.Id,
                        ProductId = product.Id,
                        Quantity = quantity,
                        ReorderLevel = 5,
                        LastUpdatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.BranchInventories.AddAsync(branchInventory);
                }

                await _unitOfWork.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return ApiResponse<ProductDto>.Ok(new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Type = product.Type,
                TrackInventory = product.TrackInventory,
                IsBatchTracked = product.IsBatchTracked,
                IsActive = product.IsActive,
                CategoryId = product.CategoryId,
                CurrentBranchStock = request.InitialStock
            }, "تم إنشاء المنتج بنجاح");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ApiResponse<ProductDto>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR));
        }
    }
}
