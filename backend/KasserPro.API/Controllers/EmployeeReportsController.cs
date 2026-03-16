namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Application.DTOs.Reports;
using KasserPro.API.Middleware;
using KasserPro.Domain.Enums;

[Authorize]
[ApiController]
[Route("api/employee-reports")]
public class EmployeeReportsController : ControllerBase
{
    private readonly IEmployeeReportService _reportService;
    private readonly ILogger<EmployeeReportsController> _logger;

    public EmployeeReportsController(
        IEmployeeReportService reportService,
        ILogger<EmployeeReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get cashier performance report
    /// </summary>
    [HttpGet("cashier-performance")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(CashierPerformanceReportDto), 200)]
    public async Task<IActionResult> GetCashierPerformanceReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetCashierPerformanceReportAsync(fromDate, toDate);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get detailed shifts report
    /// </summary>
    [HttpGet("shifts")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(DetailedShiftsReportDto), 200)]
    public async Task<IActionResult> GetDetailedShiftsReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] int? userId = null)
    {
        var result = await _reportService.GetDetailedShiftsReportAsync(fromDate, toDate, userId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get sales by employee report
    /// </summary>
    [HttpGet("sales")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(SalesByEmployeeReportDto), 200)]
    public async Task<IActionResult> GetSalesByEmployeeReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetSalesByEmployeeReportAsync(fromDate, toDate);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
