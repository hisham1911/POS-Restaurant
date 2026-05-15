namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

public class RecipeIngredient : BaseEntity
{
    public int RecipeId { get; set; }
    public int RawMaterialProductId { get; set; } // Product of type RawMaterial
    public decimal Quantity { get; set; }
    public UnitOfMeasure Unit { get; set; }
    public decimal Cost { get; set; }             // Calculated automatically
    public string? Notes { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public Product RawMaterial { get; set; } = null!;
}
