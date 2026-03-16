namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Application.DTOs.Reports;
using KasserPro.API.Middleware;
using KasserPro.Domain.Enums;

[Authorize]
[ApiController]
[Route("api/supplier-reports")]
public class SupplierReportsController : ControllerBase
{
    private readonly ISupplierReportService _reportService;
    private readonly ILogger<SupplierReportsController> _logger;

    public SupplierReportsController(
        ISupplierReportService reportService,
        ILogger<SupplierReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get supplier purchases report
    /// </summary>
    [HttpGet("purchases")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(SupplierPurchasesReportDto), 200)]
    public async Task<IActionResult> GetSupplierPurchasesReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetSupplierPurchasesReportAsync(fromDate, toDate);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get supplier debts report
    /// </summary>
    [HttpGet("debts")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(SupplierDebtsReportDto), 200)]
    public async Task<IActionResult> GetSupplierDebtsReport()
    {
        var result = await _reportService.GetSupplierDebtsReportAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get supplier performance report
    /// </summary>
    [HttpGet("performance")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(SupplierPerformanceReportDto), 200)]
    public async Task<IActionResult> GetSupplierPerformanceReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetSupplierPerformanceReportAsync(fromDate, toDate);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
