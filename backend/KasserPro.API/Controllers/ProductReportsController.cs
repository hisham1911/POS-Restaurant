namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Application.DTOs.Reports;
using KasserPro.API.Middleware;
using KasserPro.Domain.Enums;

[Authorize]
[ApiController]
[Route("api/product-reports")]
public class ProductReportsController : ControllerBase
{
    private readonly IProductReportService _reportService;
    private readonly ILogger<ProductReportsController> _logger;

    public ProductReportsController(
        IProductReportService reportService,
        ILogger<ProductReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get product movement report
    /// </summary>
    [HttpGet("movement")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(ProductMovementReportDto), 200)]
    public async Task<IActionResult> GetProductMovementReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] int? categoryId = null)
    {
        var result = await _reportService.GetProductMovementReportAsync(fromDate, toDate, categoryId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get most profitable products report
    /// </summary>
    [HttpGet("profitability")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(ProfitableProductsReportDto), 200)]
    public async Task<IActionResult> GetProfitableProductsReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] int topCount = 10)
    {
        var result = await _reportService.GetProfitableProductsReportAsync(fromDate, toDate, topCount);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get slow-moving products report
    /// </summary>
    [HttpGet("slow")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(SlowMovingProductsReportDto), 200)]
    public async Task<IActionResult> GetSlowMovingProductsReport(
        [FromQuery] int daysThreshold = 30)
    {
        var result = await _reportService.GetSlowMovingProductsReportAsync(daysThreshold);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get Cost of Goods Sold (COGS) report
    /// </summary>
    [HttpGet("cogs")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(CogsReportDto), 200)]
    public async Task<IActionResult> GetCogsReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetCogsReportAsync(fromDate, toDate);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
