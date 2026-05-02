namespace KasserPro.Application.Services.Implementations;

using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Categories;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CategoryService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<CategoryDto>>> GetAllAsync(string? search = null, bool? isActive = null, int page = 1, int pageSize = 20)
    {
        var tenantId = _currentUser.TenantId;
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var productsQuery = _unitOfWork.Products.Query()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted);
        var query = _unitOfWork.Categories.Query()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted);

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(c => c.Name.Contains(searchTerm) ||
                                     (c.NameEn != null && c.NameEn.Contains(searchTerm)));
        }

        var categories = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .GroupJoin(
                productsQuery,
                category => category.Id,
                product => product.CategoryId,
                (category, products) => new CategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    NameEn = category.NameEn,
                    Description = category.Description,
                    ImageUrl = category.ImageUrl,
                    SortOrder = category.SortOrder,
                    IsActive = category.IsActive,
                    ProductCount = products.Count()
                })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ApiResponse<List<CategoryDto>>.Ok(categories);
    }

    public async Task<ApiResponse<CategoryDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var category = await _unitOfWork.Categories.Query()
            .Where(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                NameEn = c.NameEn,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                ProductCount = _unitOfWork.Products.Query()
                    .Count(p => p.CategoryId == c.Id && p.TenantId == tenantId && !p.IsDeleted)
            })
            .FirstOrDefaultAsync();
        if (category == null)
        {
            return ApiResponse<CategoryDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));
        }

        return ApiResponse<CategoryDto>.Ok(category);
    }

    public async Task<ApiResponse<CategoryDto>> CreateAsync(CreateCategoryRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var normalizedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            return ApiResponse<CategoryDto>.Fail(
                ErrorCodes.CATEGORY_NAME_REQUIRED,
                ErrorMessages.Get(ErrorCodes.CATEGORY_NAME_REQUIRED));
        }

        var nameExists = await _unitOfWork.Categories.Query()
            .AnyAsync(c => c.TenantId == tenantId
                        && !c.IsDeleted
                        && c.Name == normalizedName);
        if (nameExists)
        {
            return ApiResponse<CategoryDto>.Fail(
                ErrorCodes.CATEGORY_NAME_DUPLICATE,
                ErrorMessages.Get(ErrorCodes.CATEGORY_NAME_DUPLICATE));
        }

        var category = new Category
        {
            TenantId = tenantId,
            Name = normalizedName,
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CategoryDto>.Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameEn = category.NameEn,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            ProductCount = 0
        }, "تم إنشاء التصنيف بنجاح");
    }

    public async Task<ApiResponse<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var normalizedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            return ApiResponse<CategoryDto>.Fail(
                ErrorCodes.CATEGORY_NAME_REQUIRED,
                ErrorMessages.Get(ErrorCodes.CATEGORY_NAME_REQUIRED));
        }

        var category = await _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted);
        if (category == null)
        {
            return ApiResponse<CategoryDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));
        }

        var nameExists = await _unitOfWork.Categories.Query()
            .AnyAsync(c => c.TenantId == tenantId
                        && !c.IsDeleted
                        && c.Id != id
                        && c.Name == normalizedName);
        if (nameExists)
        {
            return ApiResponse<CategoryDto>.Fail(
                ErrorCodes.CATEGORY_NAME_DUPLICATE,
                ErrorMessages.Get(ErrorCodes.CATEGORY_NAME_DUPLICATE));
        }

        category.Name = normalizedName;
        category.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync();

        var productCount = await _unitOfWork.Products.Query()
            .CountAsync(p => p.CategoryId == id && p.TenantId == tenantId && !p.IsDeleted);

        return ApiResponse<CategoryDto>.Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameEn = category.NameEn,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            ProductCount = productCount
        }, "تم تحديث التصنيف بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var category = await _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
        if (category == null)
            return ApiResponse<bool>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));

        var hasProducts = await _unitOfWork.Products.Query()
            .AnyAsync(p => p.CategoryId == id && p.TenantId == tenantId && !p.IsDeleted);
        if (hasProducts)
            return ApiResponse<bool>.Fail(ErrorCodes.CATEGORY_HAS_PRODUCTS, "لا يمكن حذف هذا التصنيف. يحتوي على منتجات مرتبطة به. يرجى نقل المنتجات إلى تصنيف آخر أو حذفها أولاً.");

        category.IsDeleted = true;
        _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف التصنيف بنجاح");
    }
}
