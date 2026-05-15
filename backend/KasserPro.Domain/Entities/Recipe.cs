namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class Recipe : BaseEntity
{
    public int TenantId { get; set; }
    public int ProductId { get; set; }          // The final manufactured product
    public decimal YieldQuantity { get; set; } = 1; // Production yield quantity
    public int? PreparationTimeMinutes { get; set; }
    public int? CookingTimeMinutes { get; set; }
    public string? Instructions { get; set; }
    public decimal TotalCost { get; set; }      // Calculated automatically
    public bool AutoDeductIngredients { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}
