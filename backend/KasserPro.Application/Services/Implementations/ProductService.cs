namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Products;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ProductService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<ProductDto>>> GetAllAsync(int? categoryId = null, string? search = null, bool? isActive = null, bool? lowStock = null)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        
        var query = _unitOfWork.Products.Query()
            .Include(p => p.Category)
            .Where(p => p.TenantId == tenantId);

        // Filter by category
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        // Filter by search (name or SKU or barcode)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.NameEn != null && p.NameEn.ToLower().Contains(searchLower)) ||
                (p.Sku != null && p.Sku.ToLower().Contains(searchLower)) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(searchLower))
            );
        }

        // Filter by active status
        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var products = await query.ToListAsync();

        // Get BranchInventory for current branch
        var productIds = products.Select(p => p.Id).ToList();
        var branchInventories = await _unitOfWork.BranchInventories.Query()
            .Where(bi => bi.TenantId == tenantId && bi.BranchId == branchId && productIds.Contains(bi.ProductId))
            .ToDictionaryAsync(bi => bi.ProductId, bi => bi.Quantity);

        var productDtos = products.Select(p =>
        {
            var stockQuantity = p.TrackInventory && branchInventories.ContainsKey(p.Id)
                ? branchInventories[p.Id]
                : (p.TrackInventory ? 0 : (int?)null);

            return new ProductDto
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
                StockQuantity = stockQuantity,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name,
                LowStockThreshold = p.LowStockThreshold,
                ReorderPoint = p.ReorderPoint,
                LastStockUpdate = p.LastStockUpdate
            };
        }).ToList();

        // Filter by low stock (after getting BranchInventory data)
        if (lowStock.HasValue && lowStock.Value)
        {
            productDtos = productDtos
                .Where(p => p.TrackInventory && (p.StockQuantity ?? 0) < (p.LowStockThreshold ?? 5))
                .ToList();
        }

        return ApiResponse<List<ProductDto>>.Ok(productDtos);
    }

    public async Task<ApiResponse<ProductDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        
        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (product == null)
            return ApiResponse<ProductDto>.Fail("المنتج غير موجود");

        // Get BranchInventory for current branch
        var branchInventory = await _unitOfWork.BranchInventories.Query()
            .FirstOrDefaultAsync(bi => bi.TenantId == tenantId && bi.BranchId == branchId && bi.ProductId == product.Id);

        var stockQuantity = product.TrackInventory && branchInventory != null
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
            StockQuantity = stockQuantity,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            LowStockThreshold = product.LowStockThreshold,
            ReorderPoint = product.ReorderPoint,
            LastStockUpdate = product.LastStockUpdate
        });
    }

    public async Task<ApiResponse<ProductDto>> CreateAsync(CreateProductRequest request)
    {
        // Validation: Price must be non-negative
        if (request.Price < 0)
            return ApiResponse<ProductDto>.Fail("سعر المنتج لا يمكن أن يكون سالباً");

        // Validation: Category must exist
        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
        if (category == null)
            return ApiResponse<ProductDto>.Fail("التصنيف غير موجود");

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
            StockQuantity = 0, // Set to 0, actual stock will be in BranchInventory
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
                // Use branch-specific quantity if provided, otherwise use default
                var quantity = request.BranchStockQuantities?.ContainsKey(branch.Id) == true
                    ? request.BranchStockQuantities[branch.Id]
                    : request.StockQuantity;

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
            }

            await _unitOfWork.SaveChangesAsync();
        }

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
            StockQuantity = request.StockQuantity, // Return requested quantity for consistency
            CategoryId = product.CategoryId
        }, "تم إنشاء المنتج بنجاح");
    }

    public async Task<ApiResponse<ProductDto>> UpdateAsync(int id, UpdateProductRequest request)
    {
        // Validation: Price must be non-negative
        if (request.Price < 0)
            return ApiResponse<ProductDto>.Fail("سعر المنتج لا يمكن أن يكون سالباً");

        var tenantId = _currentUser.TenantId;
        var product = await _unitOfWork.Products.Query()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (product == null)
            return ApiResponse<ProductDto>.Fail("المنتج غير موجود");

        // Validation: Category must exist
        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
        if (category == null)
            return ApiResponse<ProductDto>.Fail("التصنيف غير موجود");

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
        product.TrackInventory = request.Type == Domain.Enums.ProductType.Physical;
        product.StockQuantity = 0; // Keep at 0, use BranchInventory
        product.LowStockThreshold = request.LowStockThreshold;
        product.ReorderPoint = request.ReorderPoint;
        product.LastStockUpdate = DateTime.UtcNow;

        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

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
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId
        }, "تم تحديث المنتج بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var product = await _unitOfWork.Products.Query()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (product == null)
            return ApiResponse<bool>.Fail("المنتج غير موجود");

        product.IsDeleted = true;
        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف المنتج بنجاح");
    }

    public async Task<ApiResponse<StockAdjustResultDto>> AdjustStockAsync(int id, AdjustStockRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var product = await _unitOfWork.Products.Query()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (product == null)
            return ApiResponse<StockAdjustResultDto>.Fail("المنتج غير موجود");

        var previousBalance = product.StockQuantity ?? 0;
        var newBalance = previousBalance + request.Quantity;

        if (newBalance < 0)
            return ApiResponse<StockAdjustResultDto>.Fail("لا يمكن أن يكون المخزون سالباً");

        product.StockQuantity = newBalance;
        product.LastStockUpdate = DateTime.UtcNow;

        _unitOfWork.Products.Update(product);
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
                return ApiResponse<ProductDto>.Fail("اسم المنتج مطلوب");

            if (request.Name.Length > 200)
                return ApiResponse<ProductDto>.Fail("اسم المنتج يجب ألا يتجاوز 200 حرف");

            // Validation: Price must be non-negative
            if (request.Price < 0)
                return ApiResponse<ProductDto>.Fail("السعر يجب أن يكون أكبر من أو يساوي صفر");

            // Validation: InitialStock must be non-negative
            if (request.InitialStock < 0)
                return ApiResponse<ProductDto>.Fail("الكمية الأولية يجب أن تكون أكبر من أو تساوي صفر");

            // Validation: Category must exist
            var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
            if (category == null)
                return ApiResponse<ProductDto>.Fail("التصنيف غير موجود");

            var product = new Product
            {
                TenantId = _currentUser.TenantId,
                Name = request.Name,
                Sku = request.Sku,
                Barcode = request.Barcode,
                Price = request.Price,
                CategoryId = request.CategoryId,
                // Quick create defaults
                IsActive = true,
                Type = request.Type,
                // TrackInventory is automatically set based on Type
                TrackInventory = request.Type == Domain.Enums.ProductType.Physical,
                StockQuantity = 0,
                LowStockThreshold = 5,
                TaxInclusive = false, // Default to tax exclusive
                LastStockUpdate = DateTime.UtcNow
            };

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            // Create BranchInventory records ONLY if TrackInventory is enabled (Physical products)
            if (product.TrackInventory)
            {
                // Quick create from POS should only add stock to current branch
                var branchInventory = new BranchInventory
                {
                    TenantId = _currentUser.TenantId,
                    BranchId = _currentUser.BranchId, // Only current branch
                    ProductId = product.Id,
                    Quantity = request.InitialStock,
                    ReorderLevel = 5,
                    LastUpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.BranchInventories.AddAsync(branchInventory);
                await _unitOfWork.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return ApiResponse<ProductDto>.Ok(new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                Type = product.Type,
                TrackInventory = product.TrackInventory,
                IsActive = product.IsActive,
                CategoryId = product.CategoryId,
                StockQuantity = request.InitialStock
            }, "تم إنشاء المنتج بنجاح");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResponse<ProductDto>.Fail($"حدث خطأ أثناء إنشاء المنتج: {ex.Message}");
        }
    }
}
