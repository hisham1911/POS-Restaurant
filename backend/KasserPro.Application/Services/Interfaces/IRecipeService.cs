namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Recipes;

public interface IRecipeService
{
    Task<ApiResponse<List<RecipeListDto>>> GetAllAsync(int tenantId, int branchId);
    Task<ApiResponse<RecipeDto?>> GetByIdAsync(int id, int tenantId);
    Task<ApiResponse<RecipeDto?>> GetByProductIdAsync(int productId, int tenantId);
    Task<ApiResponse<RecipeDto>> CreateAsync(CreateRecipeRequest request, int tenantId);
    Task<ApiResponse<RecipeDto>> UpdateAsync(int id, UpdateRecipeRequest request, int tenantId);
    Task<ApiResponse<bool>> DeleteAsync(int id, int tenantId);
    Task<ApiResponse<decimal>> CalculateCostAsync(int recipeId, int tenantId);
    Task<ApiResponse<bool>> DeductIngredientsAsync(int recipeId, decimal multiplier, int branchId, int tenantId);
}
