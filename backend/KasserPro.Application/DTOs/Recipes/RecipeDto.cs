namespace KasserPro.Application.DTOs.Recipes;

using KasserPro.Domain.Enums;

public class RecipeDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal YieldQuantity { get; set; }
    public int? PreparationTimeMinutes { get; set; }
    public int? CookingTimeMinutes { get; set; }
    public string? Instructions { get; set; }
    public decimal TotalCost { get; set; }
    public bool AutoDeductIngredients { get; set; }
    public bool IsActive { get; set; }
    public decimal ProfitMargin { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
}

public class RecipeIngredientDto
{
    public int Id { get; set; }
    public int RawMaterialProductId { get; set; }
    public string RawMaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public UnitOfMeasure Unit { get; set; }
    public string UnitName => Unit.ToString();
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
}

public class RecipeListDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal YieldQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ProfitMargin { get; set; }
    public bool IsActive { get; set; }
    public int IngredientCount { get; set; }
}

public class CreateRecipeRequest
{
    public int ProductId { get; set; }
    public decimal YieldQuantity { get; set; } = 1;
    public int? PreparationTimeMinutes { get; set; }
    public int? CookingTimeMinutes { get; set; }
    public string? Instructions { get; set; }
    public bool AutoDeductIngredients { get; set; } = true;
    public List<CreateRecipeIngredientRequest> Ingredients { get; set; } = new();
}

public class CreateRecipeIngredientRequest
{
    public int RawMaterialProductId { get; set; }
    public decimal Quantity { get; set; }
    public UnitOfMeasure Unit { get; set; }
    public string? Notes { get; set; }
}

public class UpdateRecipeRequest
{
    public decimal YieldQuantity { get; set; } = 1;
    public int? PreparationTimeMinutes { get; set; }
    public int? CookingTimeMinutes { get; set; }
    public string? Instructions { get; set; }
    public bool AutoDeductIngredients { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public List<CreateRecipeIngredientRequest> Ingredients { get; set; } = new();
}
