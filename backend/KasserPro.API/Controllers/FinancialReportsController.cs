namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Application.DTOs.Reports;
using KasserPro.API.Middleware;
using KasserPro.Domain.Enums;

[Authorize]
[ApiController]
[Route("api/financial-reports")]
public class FinancialReportsController : ControllerBase
{
    private readonly IFinancialReportService _reportService;
    private readonly ILogger<FinancialReportsController> _logger;

    public FinancialReportsController(
        IFinancialReportService reportService,
        ILogger<FinancialReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get Profit & Loss report
    /// </summary>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    [HttpGet("profit-loss")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(ProfitLossReportDto), 200)]
    public async Task<IActionResult> GetProfitLossReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetProfitLossReportAsync(fromDate, toDate);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get Expenses report
    /// </summary>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    [HttpGet("expenses")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(ExpensesReportDto), 200)]
    public async Task<IActionResult> GetExpensesReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetExpensesReportAsync(fromDate, toDate);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
