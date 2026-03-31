namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Application.DTOs.Reports;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[Authorize]
[ApiController]
[Route("api/inventory-reports")]
[HasPermission(Permission.ReportsView)]
public class InventoryReportsController : ControllerBase
{
    private readonly IInventoryReportService _reportService;
    private readonly ILogger<InventoryReportsController> _logger;

    public InventoryReportsController(
        IInventoryReportService reportService,
        ILogger<InventoryReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get inventory report for a specific branch
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <param name="categoryId">Optional category filter</param>
    /// <param name="lowStockOnly">Show only low stock items</param>
    [HttpGet("branch/{branchId}")]
    [ProducesResponseType(typeof(BranchInventoryReportDto), 200)]
    public async Task<IActionResult> GetBranchInventoryReport(
        int branchId,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? lowStockOnly = null)
    {
        var result = await _reportService.GetBranchInventoryReportAsync(branchId, categoryId, lowStockOnly);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get unified inventory view across all branches
    /// </summary>
    /// <param name="categoryId">Optional category filter</param>
    /// <param name="lowStockOnly">Show only products with low stock in any branch</param>
    [HttpGet("unified")]
    [Authorize(Roles = "Admin,SystemOwner")]
    [ProducesResponseType(typeof(List<UnifiedInventoryReportDto>), 200)]
    public async Task<IActionResult> GetUnifiedInventoryReport(
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? lowStockOnly = null)
    {
        var result = await _reportService.GetUnifiedInventoryReportAsync(categoryId, lowStockOnly);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get transfer history report
    /// </summary>
    /// <param name="fromDate">Start date (default: 30 days ago)</param>
    /// <param name="toDate">End date (default: today)</param>
    /// <param name="branchId">Optional branch filter (shows transfers from or to this branch)</param>
    [HttpGet("transfer-history")]
    [ProducesResponseType(typeof(TransferHistoryReportDto), 200)]
    public async Task<IActionResult> GetTransferHistoryReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? branchId = null)
    {
        var result = await _reportService.GetTransferHistoryReportAsync(fromDate, toDate, branchId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get low stock summary report
    /// </summary>
    /// <param name="branchId">Optional branch filter</param>
    [HttpGet("low-stock-summary")]
    [ProducesResponseType(typeof(LowStockSummaryReportDto), 200)]
    public async Task<IActionResult> GetLowStockSummaryReport([FromQuery] int? branchId = null)
    {
        var result = await _reportService.GetLowStockSummaryReportAsync(branchId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Export branch inventory report to CSV
    /// </summary>
    [HttpGet("branch/{branchId}/export")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public async Task<IActionResult> ExportBranchInventoryReport(
        int branchId,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? lowStockOnly = null)
    {
        var result = await _reportService.GetBranchInventoryReportAsync(branchId, categoryId, lowStockOnly);

        if (!result.Success)
            return BadRequest(result);

        var report = result.Data!;
        var csv = GenerateBranchInventoryCsv(report);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

        return File(bytes, "text/csv", $"branch-inventory-{branchId}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Export unified inventory report to CSV
    /// </summary>
    [HttpGet("unified/export")]
    [Authorize(Roles = "Admin,SystemOwner")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public async Task<IActionResult> ExportUnifiedInventoryReport(
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? lowStockOnly = null)
    {
        var result = await _reportService.GetUnifiedInventoryReportAsync(categoryId, lowStockOnly);

        if (!result.Success)
            return BadRequest(result);

        var csv = GenerateUnifiedInventoryCsv(result.Data!);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

        return File(bytes, "text/csv", $"unified-inventory-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private string GenerateBranchInventoryCsv(BranchInventoryReportDto report)
    {
        var csv = new System.Text.StringBuilder();

        // Header
        csv.AppendLine($"Branch Inventory Report - {report.BranchName}");
        csv.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        csv.AppendLine($"Total Products: {report.TotalProducts}");
        csv.AppendLine($"Total Quantity: {report.TotalQuantity}");
        csv.AppendLine($"Low Stock Count: {report.LowStockCount}");
        csv.AppendLine($"Total Value: {report.TotalValue:F2}");
        csv.AppendLine();

        // Column headers
        csv.AppendLine("Product Name,SKU,Category,Quantity,Reorder Level,Status,Average Cost,Total Value,Last Updated");

        // Data rows
        foreach (var item in report.Items)
        {
            csv.AppendLine($"\"{item.ProductName}\",\"{item.ProductSku}\",\"{item.CategoryName}\"," +
                          $"{item.Quantity},{item.ReorderLevel}," +
                          $"\"{(item.IsLowStock ? "Low Stock" : "Available")}\"," +
                          $"{item.AverageCost:F2},{item.TotalValue:F2}," +
                          $"\"{item.LastUpdatedAt:yyyy-MM-dd HH:mm:ss}\"");
        }

        return csv.ToString();
    }

    private string GenerateUnifiedInventoryCsv(List<UnifiedInventoryReportDto> reports)
    {
        var csv = new System.Text.StringBuilder();

        // Header
        csv.AppendLine("Unified Inventory Report");
        csv.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        csv.AppendLine();

        // Column headers
        csv.AppendLine("Product Name,SKU,Category,Total Quantity,Branch Count,Low Stock Branches,Average Cost,Total Value");

        // Data rows
        foreach (var item in reports)
        {
            csv.AppendLine($"\"{item.ProductName}\",\"{item.ProductSku}\",\"{item.CategoryName}\"," +
                          $"{item.TotalQuantity},{item.BranchCount},{item.LowStockBranchCount}," +
                          $"{item.AverageCost:F2},{item.TotalValue:F2}");
        }

        return csv.ToString();
    }
}
