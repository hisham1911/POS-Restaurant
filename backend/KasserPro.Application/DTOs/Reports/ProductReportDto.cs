namespace KasserPro.Application.DTOs.Reports;

/// <summary>
/// Product Movement Report
/// </summary>
public class ProductMovementReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public int TotalProducts { get; set; }
    public int ProductsSold { get; set; }
    public int ProductsNotSold { get; set; }
    public decimal TotalRevenue { get; set; }
    
    public List<ProductMovementDetailDto> ProductMovements { get; set; } = new();
}

public class ProductMovementDetailDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? CategoryName { get; set; }
    
    // Sales
    public int QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal ProfitMargin { get; set; }
    
    // Inventory
    public int OpeningStock { get; set; }
    public int PurchasedQuantity { get; set; }
    public int TransferredIn { get; set; }
    public int TransferredOut { get; set; }
    public int ClosingStock { get; set; }
    
    // Performance
    public decimal TurnoverRate { get; set; }
    public int DaysToSellOut { get; set; }
}

/// <summary>
/// Most Profitable Products Report
/// </summary>
public class ProfitableProductsReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal AverageProfitMargin { get; set; }
    
    public List<ProfitableProductDetailDto> TopProfitableProducts { get; set; } = new();
    public List<ProfitableProductDetailDto> LeastProfitableProducts { get; set; } = new();
}

public class ProfitableProductDetailDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal AverageSellingPrice { get; set; }
    public decimal AverageCost { get; set; }
}

/// <summary>
/// Slow-Moving Products Report
/// </summary>
public class SlowMovingProductsReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public int TotalSlowMovingProducts { get; set; }
    public decimal TotalValueAtRisk { get; set; }
    public int TotalQuantityAtRisk { get; set; }
    
    public List<SlowMovingProductDetailDto> SlowMovingProducts { get; set; } = new();
}

public class SlowMovingProductDetailDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int CurrentStock { get; set; }
    public int QuantitySold { get; set; }
    public decimal AverageDailySales { get; set; }
    public int DaysOfStock { get; set; }
    public DateTime? LastSoldDate { get; set; }
    public int DaysSinceLastSale { get; set; }
    public decimal StockValue { get; set; }
    public string MovementStatus { get; set; } = string.Empty; // "Slow", "Very Slow", "Dead Stock"
}

/// <summary>
/// Cost of Goods Sold (COGS) Report
/// </summary>
public class CogsReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    // Opening Inventory
    public decimal OpeningInventoryValue { get; set; }
    
    // Purchases
    public decimal TotalPurchases { get; set; }
    
    // Closing Inventory
    public decimal ClosingInventoryValue { get; set; }
    
    // COGS Calculation
    public decimal CostOfGoodsSold { get; set; } // Opening + Purchases - Closing
    
    // Sales
    public decimal TotalRevenue { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitMargin { get; set; }
    
    // Breakdown by Category
    public List<CogsCategoryBreakdownDto> CategoryBreakdown { get; set; } = new();
}

public class CogsCategoryBreakdownDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal OpeningValue { get; set; }
    public decimal Purchases { get; set; }
    public decimal ClosingValue { get; set; }
    public decimal Cogs { get; set; }
    public decimal Revenue { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitMargin { get; set; }
}
