namespace KasserPro.Infrastructure.Services;

using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Recipes;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class RecipeService : IRecipeService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RecipeService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<RecipeListDto>>> GetAllAsync(int tenantId, int branchId)
    {
        var recipes = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Product)
            .Include(r => r.Ingredients)
            .ThenInclude(i => i.RawMaterial)
            .ToListAsync();

        var dtos = recipes.Select(r =>
        {
            var totalCost = CalculateTotalCost(r);
            return new RecipeListDto
        {
            Id = r.Id,
            ProductId = r.ProductId,
            ProductName = r.Product?.Name ?? "",
            YieldQuantity = r.YieldQuantity,
            TotalCost = totalCost,
            ProfitMargin = CalculateProfitMargin(r, totalCost),
            IsActive = r.IsActive,
            IngredientCount = r.Ingredients.Count
        };
        }).ToList();

        return ApiResponse<List<RecipeListDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<RecipeDto?>> GetByIdAsync(int id, int tenantId)
    {
        var recipe = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Product)
            .Include(r => r.Ingredients)
            .ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync();

        if (recipe == null)
            return ApiResponse<RecipeDto?>.Fail(ErrorCodes.NOT_FOUND, "الوصفة غير موجودة");

        return ApiResponse<RecipeDto?>.Ok(MapToDto(recipe));
    }

    public async Task<ApiResponse<RecipeDto?>> GetByProductIdAsync(int productId, int tenantId)
    {
        var recipe = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
            .Include(r => r.Product)
            .Include(r => r.Ingredients)
            .ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync();

        if (recipe == null)
            return ApiResponse<RecipeDto?>.Ok(null);

        return ApiResponse<RecipeDto?>.Ok(MapToDto(recipe));
    }

    public async Task<ApiResponse<RecipeDto>> CreateAsync(CreateRecipeRequest request, int tenantId)
    {
        if (request.YieldQuantity <= 0 || request.Ingredients == null || request.Ingredients.Count == 0)
            return ApiResponse<RecipeDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == tenantId && !p.IsDeleted);

        if (product == null)
            return ApiResponse<RecipeDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

        if (product.Type != ProductType.Manufactured)
            return ApiResponse<RecipeDto>.Fail(ErrorCodes.VALIDATION_ERROR, "يجب أن يكون المنتج من نوع منتج مصنع");

        var existingRecipe = await _context.Recipes
            .AnyAsync(r => r.ProductId == request.ProductId && r.TenantId == tenantId && !r.IsDeleted);

        if (existingRecipe)
            return ApiResponse<RecipeDto>.Fail(ErrorCodes.VALIDATION_ERROR, "يوجد وصفة مسبقا لهذا المنتج");

        var ingredientsValidation = await ValidateIngredientsAsync(request.Ingredients, tenantId);
        if (!ingredientsValidation.Success)
            return ApiResponse<RecipeDto>.Fail(ingredientsValidation.ErrorCode!, ingredientsValidation.Message!);

        var rawMaterials = ingredientsValidation.Data!;
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var recipe = new Recipe
        {
            TenantId = tenantId,
            ProductId = request.ProductId,
            YieldQuantity = request.YieldQuantity,
            PreparationTimeMinutes = request.PreparationTimeMinutes,
            CookingTimeMinutes = request.CookingTimeMinutes,
            Instructions = request.Instructions,
            AutoDeductIngredients = request.AutoDeductIngredients,
            IsActive = true,
            TotalCost = 0
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        foreach (var ingredientRequest in request.Ingredients)
        {
            var rawMaterial = rawMaterials[ingredientRequest.RawMaterialProductId];
            _context.RecipeIngredients.Add(CreateIngredient(recipe.Id, ingredientRequest, rawMaterial));
        }

        await _context.SaveChangesAsync();

        recipe.TotalCost = await CalculateTotalCostAsync(recipe.Id);
        await _context.SaveChangesAsync();

        if (ownsTransaction && transaction != null)
        {
            await transaction.CommitAsync();
        }

        var result = await LoadRecipeAsync(recipe.Id);
        return ApiResponse<RecipeDto>.Ok(MapToDto(result), "تم إنشاء الوصفة بنجاح");
    }

    public async Task<ApiResponse<RecipeDto>> UpdateAsync(int id, UpdateRecipeRequest request, int tenantId)
    {
        var recipe = await _context.Recipes
            .Where(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync();

        if (recipe == null)
            return ApiResponse<RecipeDto>.Fail(ErrorCodes.NOT_FOUND, "الوصفة غير موجودة");

        if (request.YieldQuantity <= 0 || request.Ingredients == null || request.Ingredients.Count == 0)
            return ApiResponse<RecipeDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        var ingredientsValidation = await ValidateIngredientsAsync(request.Ingredients, tenantId);
        if (!ingredientsValidation.Success)
            return ApiResponse<RecipeDto>.Fail(ingredientsValidation.ErrorCode!, ingredientsValidation.Message!);

        var rawMaterials = ingredientsValidation.Data!;
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;

        recipe.YieldQuantity = request.YieldQuantity;
        recipe.PreparationTimeMinutes = request.PreparationTimeMinutes;
        recipe.CookingTimeMinutes = request.CookingTimeMinutes;
        recipe.Instructions = request.Instructions;
        recipe.AutoDeductIngredients = request.AutoDeductIngredients;
        recipe.IsActive = request.IsActive;

        _context.RecipeIngredients.RemoveRange(recipe.Ingredients);

        foreach (var ingredientRequest in request.Ingredients)
        {
            var rawMaterial = rawMaterials[ingredientRequest.RawMaterialProductId];
            _context.RecipeIngredients.Add(CreateIngredient(recipe.Id, ingredientRequest, rawMaterial));
        }

        await _context.SaveChangesAsync();

        recipe.TotalCost = await CalculateTotalCostAsync(recipe.Id);
        await _context.SaveChangesAsync();

        if (ownsTransaction && transaction != null)
        {
            await transaction.CommitAsync();
        }

        var result = await LoadRecipeAsync(recipe.Id);
        return ApiResponse<RecipeDto>.Ok(MapToDto(result), "تم تحديث الوصفة بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id, int tenantId)
    {
        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);

        if (recipe == null)
            return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "الوصفة غير موجودة");

        recipe.IsDeleted = true;
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف الوصفة بنجاح");
    }

    public async Task<ApiResponse<decimal>> CalculateCostAsync(int recipeId, int tenantId)
    {
        var recipe = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Id == recipeId && r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Ingredients)
            .ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync();

        if (recipe == null)
            return ApiResponse<decimal>.Fail(ErrorCodes.NOT_FOUND, "الوصفة غير موجودة");

        var totalCost = CalculateTotalCost(recipe);

        return ApiResponse<decimal>.Ok(totalCost);
    }

    public async Task<ApiResponse<bool>> DeductIngredientsAsync(int recipeId, decimal multiplier, int branchId, int tenantId)
    {
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;
        try
        {
            var recipe = await _context.Recipes
                .Where(r => r.Id == recipeId && r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
                .Include(r => r.Product)
                .Include(r => r.Ingredients)
                .ThenInclude(i => i.RawMaterial)
                .FirstOrDefaultAsync();

            if (recipe == null)
                return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "الوصفة غير موجودة أو غير نشطة");

            if (recipe.YieldQuantity <= 0 || multiplier == 0)
                return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

            foreach (var ingredient in recipe.Ingredients)
            {
                var quantityInProductUnit = NormalizeToProductUnit(
                    ingredient.Quantity,
                    ingredient.Unit,
                    ingredient.RawMaterial.Unit);
                var requiredQty = Math.Round((quantityInProductUnit / recipe.YieldQuantity) * multiplier, 4);

                var inventory = await _context.BranchInventories
                    .FirstOrDefaultAsync(bi => bi.ProductId == ingredient.RawMaterialProductId
                                            && bi.BranchId == branchId
                                            && bi.TenantId == tenantId
                                            && !bi.IsDeleted);

                if (inventory == null || inventory.Quantity < requiredQty)
                {
                    if (ownsTransaction && transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }

                    return ApiResponse<bool>.Fail(
                        ErrorCodes.INVENTORY_INSUFFICIENT_STOCK,
                        $"كمية غير كافية من '{ingredient.RawMaterial?.Name ?? "مادة خام"}' في المخزون");
                }

                var balanceBefore = inventory.Quantity;
                inventory.Quantity -= requiredQty;
                inventory.LastUpdatedAt = DateTime.UtcNow;

                var movement = new StockMovement
                {
                    TenantId = tenantId,
                    BranchId = branchId,
                    ProductId = ingredient.RawMaterialProductId,
                    Type = StockMovementType.Sale,
                    Quantity = -requiredQty,
                    ReferenceType = "Recipe",
                    ReferenceId = recipeId,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = inventory.Quantity,
                    Reason = $"استخدام في وصفة: {recipe.Product?.Name ?? "منتج مصنع"} x {multiplier}",
                    UserId = _currentUser.UserId
                };

                _context.StockMovements.Add(movement);
            }

            await _context.SaveChangesAsync();

            if (ownsTransaction && transaction != null)
            {
                await transaction.CommitAsync();
            }

            return ApiResponse<bool>.Ok(true, "تم خصم المكونات من المخزون");
        }
        catch (Exception ex)
        {
            if (ownsTransaction && transaction != null)
            {
                await transaction.RollbackAsync();
            }

            return ApiResponse<bool>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR, $"حدث خطأ أثناء خصم المكونات: {ex.Message}");
        }
    }

    private async Task<decimal> CalculateTotalCostAsync(int recipeId)
    {
        var ingredients = await _context.RecipeIngredients
            .AsNoTracking()
            .Where(ri => ri.RecipeId == recipeId && !ri.IsDeleted)
            .Include(ri => ri.RawMaterial)
            .ToListAsync();

        return ingredients.Sum(i =>
            ResolveMaterialUnitCost(i.RawMaterial) *
            NormalizeToProductUnit(i.Quantity, i.Unit, i.RawMaterial?.Unit ?? i.Unit));
    }

    private async Task<ApiResponse<Dictionary<int, Product>>> ValidateIngredientsAsync(
        List<CreateRecipeIngredientRequest> ingredients,
        int tenantId)
    {
        if (ingredients.Count == 0 || ingredients.Any(i => i.RawMaterialProductId <= 0 || i.Quantity <= 0))
            return ApiResponse<Dictionary<int, Product>>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        var rawMaterialIds = ingredients.Select(i => i.RawMaterialProductId).Distinct().ToList();
        if (rawMaterialIds.Count != ingredients.Count)
            return ApiResponse<Dictionary<int, Product>>.Fail(ErrorCodes.VALIDATION_ERROR, "لا يمكن تكرار نفس المادة الخام في الوصفة");

        var rawMaterials = await _context.Products
            .Where(p => rawMaterialIds.Contains(p.Id) && p.TenantId == tenantId && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id);

        if (rawMaterials.Count != rawMaterialIds.Count)
            return ApiResponse<Dictionary<int, Product>>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "أحد المواد الخام غير موجود");

        foreach (var ingredient in ingredients)
        {
            var rawMaterial = rawMaterials[ingredient.RawMaterialProductId];
            if (rawMaterial.Type != ProductType.RawMaterial)
                return ApiResponse<Dictionary<int, Product>>.Fail(ErrorCodes.VALIDATION_ERROR, $"'{rawMaterial.Name}' ليس مادة خام");

            if (!CanConvertUnit(ingredient.Unit, rawMaterial.Unit))
                return ApiResponse<Dictionary<int, Product>>.Fail(ErrorCodes.VALIDATION_ERROR, $"وحدة '{rawMaterial.Name}' غير متوافقة مع وحدة المخزون");
        }

        return ApiResponse<Dictionary<int, Product>>.Ok(rawMaterials);
    }

    private static RecipeIngredient CreateIngredient(
        int recipeId,
        CreateRecipeIngredientRequest ingredientRequest,
        Product rawMaterial)
    {
        var quantityInProductUnit = NormalizeToProductUnit(
            ingredientRequest.Quantity,
            ingredientRequest.Unit,
            rawMaterial.Unit);

        return new RecipeIngredient
        {
            RecipeId = recipeId,
            RawMaterialProductId = ingredientRequest.RawMaterialProductId,
            Quantity = ingredientRequest.Quantity,
            Unit = ingredientRequest.Unit,
            Cost = ResolveMaterialUnitCost(rawMaterial) * quantityInProductUnit,
            Notes = ingredientRequest.Notes
        };
    }

    private static decimal CalculateTotalCost(Recipe recipe)
    {
        return recipe.Ingredients.Sum(i =>
            ResolveMaterialUnitCost(i.RawMaterial) *
            NormalizeToProductUnit(i.Quantity, i.Unit, i.RawMaterial?.Unit ?? i.Unit));
    }

    private static decimal ResolveMaterialUnitCost(Product? rawMaterial)
        => rawMaterial?.AverageCost > 0
            ? rawMaterial.AverageCost.Value
            : rawMaterial?.Cost ?? 0;

    private async Task<Recipe> LoadRecipeAsync(int recipeId)
    {
        return await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Id == recipeId)
            .Include(r => r.Product)
            .Include(r => r.Ingredients)
            .ThenInclude(i => i.RawMaterial)
            .FirstAsync();
    }

    private static bool CanConvertUnit(UnitOfMeasure fromUnit, UnitOfMeasure toUnit)
        => fromUnit == toUnit
        || (IsWeight(fromUnit) && IsWeight(toUnit))
        || (IsVolume(fromUnit) && IsVolume(toUnit));

    private static decimal NormalizeToProductUnit(decimal quantity, UnitOfMeasure fromUnit, UnitOfMeasure productUnit)
    {
        if (fromUnit == productUnit)
            return quantity;

        if (IsWeight(fromUnit) && IsWeight(productUnit))
        {
            var grams = fromUnit == UnitOfMeasure.Kilogram ? quantity * 1000m : quantity;
            return productUnit == UnitOfMeasure.Kilogram ? grams / 1000m : grams;
        }

        if (IsVolume(fromUnit) && IsVolume(productUnit))
        {
            var milliliters = fromUnit == UnitOfMeasure.Liter ? quantity * 1000m : quantity;
            return productUnit == UnitOfMeasure.Liter ? milliliters / 1000m : milliliters;
        }

        throw new InvalidOperationException("Incompatible recipe ingredient unit.");
    }

    private static bool IsWeight(UnitOfMeasure unit)
        => unit is UnitOfMeasure.Kilogram or UnitOfMeasure.Gram;

    private static bool IsVolume(UnitOfMeasure unit)
        => unit is UnitOfMeasure.Liter or UnitOfMeasure.Milliliter;

    private static decimal CalculateProfitMargin(Recipe recipe, decimal? totalCostOverride = null)
    {
        if (recipe.Product == null || recipe.Product.Price <= 0)
            return 0;

        if (recipe.YieldQuantity <= 0)
            return 0;

        var costPerUnit = (totalCostOverride ?? recipe.TotalCost) / recipe.YieldQuantity;
        var profit = recipe.Product.Price - costPerUnit;
        return recipe.Product.Price > 0 ? Math.Round((profit / recipe.Product.Price) * 100, 2) : 0;
    }

    private static RecipeDto MapToDto(Recipe recipe)
    {
        var totalCost = CalculateTotalCost(recipe);

        return new RecipeDto
        {
            Id = recipe.Id,
            ProductId = recipe.ProductId,
            ProductName = recipe.Product?.Name ?? "",
            YieldQuantity = recipe.YieldQuantity,
            PreparationTimeMinutes = recipe.PreparationTimeMinutes,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Instructions = recipe.Instructions,
            TotalCost = totalCost,
            AutoDeductIngredients = recipe.AutoDeductIngredients,
            IsActive = recipe.IsActive,
            ProfitMargin = CalculateProfitMargin(recipe, totalCost),
            Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
            {
                Id = i.Id,
                RawMaterialProductId = i.RawMaterialProductId,
                RawMaterialName = i.RawMaterial?.Name ?? "",
                Quantity = i.Quantity,
                Unit = i.Unit,
                Cost = ResolveMaterialUnitCost(i.RawMaterial) *
                    NormalizeToProductUnit(i.Quantity, i.Unit, i.RawMaterial?.Unit ?? i.Unit),
                Notes = i.Notes
            }).ToList()
        };
    }
}
