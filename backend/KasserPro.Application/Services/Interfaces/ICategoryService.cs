namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Categories;
using KasserPro.Application.DTOs.Common;

public interface ICategoryService
{
    Task<ApiResponse<List<CategoryDto>>> GetAllAsync(string? search = null, bool? isActive = null, int page = 1, int pageSize = 20);
    Task<ApiResponse<CategoryDto>> GetByIdAsync(int id);
    Task<ApiResponse<CategoryDto>> CreateAsync(CreateCategoryRequest request);
    Task<ApiResponse<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}
