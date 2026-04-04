namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Categories;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CategoryService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<CategoryDto>>> GetAllAsync(string? search = null, int page = 1, int pageSize = 20)
    {
        var tenantId = _currentUser.TenantId;
        var query = _unitOfWork.Categories.Query()
            .Where(c => c.TenantId == tenantId && c.IsActive);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) ||
                                    (c.NameEn != null && c.NameEn.Contains(search)));
        }

        // Apply pagination and get categories with product count
        var categories = await query
            .OrderBy(c => c.SortOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                    .Count(p => p.CategoryId == c.Id && !p.IsDeleted && p.TenantId == tenantId)
            })
            .ToListAsync();

        return ApiResponse<List<CategoryDto>>.Ok(categories);
    }

    public async Task<ApiResponse<CategoryDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var category = await _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
        if (category == null)
            return ApiResponse<CategoryDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));

        return ApiResponse<CategoryDto>.Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameEn = category.NameEn,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive
        });
    }

    public async Task<ApiResponse<CategoryDto>> CreateAsync(CreateCategoryRequest request)
    {
        var category = new Category
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            NameEn = request.NameEn,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            SortOrder = request.SortOrder
        };

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CategoryDto>.Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameEn = category.NameEn,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive
        }, "تم إنشاء التصنيف بنجاح");
    }

    public async Task<ApiResponse<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var category = await _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
        if (category == null)
            return ApiResponse<CategoryDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));

        category.Name = request.Name;
        category.NameEn = request.NameEn;
        category.Description = request.Description;
        category.ImageUrl = request.ImageUrl;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CategoryDto>.Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameEn = category.NameEn,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive
        }, "تم تحديث التصنيف بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var category = await _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
        if (category == null)
            return ApiResponse<bool>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));

        // VALIDATION: Cannot delete category with active products
        var hasProducts = await _unitOfWork.Products.Query()
            .AnyAsync(p => p.CategoryId == id && p.TenantId == tenantId && !p.IsDeleted);
        if (hasProducts)
            return ApiResponse<bool>.Fail(ErrorCodes.CATEGORY_HAS_PRODUCTS, "لا يمكن حذف تصنيف يحتوي على منتجات");

        category.IsDeleted = true;
        _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف التصنيف بنجاح");
    }
}
