namespace KasserPro.Application.DTOs.Products;

using KasserPro.Domain.Enums;

public class QuickCreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Product type - defaults to Service for quick POS items
    /// </summary>
    public ProductType Type { get; set; } = ProductType.Service;
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Piece;

    public decimal InitialStock { get; set; } = 0;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
}
