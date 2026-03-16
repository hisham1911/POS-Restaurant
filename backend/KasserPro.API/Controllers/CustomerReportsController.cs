namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Application.DTOs.Reports;
using KasserPro.API.Middleware;
using KasserPro.Domain.Enums;

[Authorize]
[ApiController]
[Route("api/customer-reports")]
public class CustomerReportsController : ControllerBase
{
    private readonly ICustomerReportService _reportService;
    private readonly ILogger<CustomerReportsController> _logger;

    public CustomerReportsController(
        ICustomerReportService reportService,
        ILogger<CustomerReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get top customers report
    /// </summary>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <param name="topCount">Number of top customers to return (default: 20)</param>
    [HttpGet("top-customers")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(TopCustomersReportDto), 200)]
    public async Task<IActionResult> GetTopCustomersReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] int topCount = 20)
    {
        var result = await _reportService.GetTopCustomersReportAsync(fromDate, toDate, topCount);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get customer debts report
    /// </summary>
    [HttpGet("debts")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(CustomerDebtsReportDto), 200)]
    public async Task<IActionResult> GetCustomerDebtsReport()
    {
        var result = await _reportService.GetCustomerDebtsReportAsync();
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get customer activity report
    /// </summary>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    [HttpGet("activity")]
    [HasPermission(Permission.ReportsView)]
    [ProducesResponseType(typeof(CustomerActivityReportDto), 200)]
    public async Task<IActionResult> GetCustomerActivityReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetCustomerActivityReportAsync(fromDate, toDate);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
