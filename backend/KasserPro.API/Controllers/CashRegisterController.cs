using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.CashRegister;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.API.Middleware;

namespace KasserPro.API.Controllers;

/// <summary>
/// Controller for Cash Register management
/// </summary>
[ApiController]
[Route("api/cash-register")]
[Authorize]
public class CashRegisterController : ControllerBase
{
    private readonly ICashRegisterService _cashRegisterService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CashRegisterController> _logger;

    public CashRegisterController(
        ICashRegisterService cashRegisterService,
        ICurrentUserService currentUserService,
        ILogger<CashRegisterController> logger)
    {
        _cashRegisterService = cashRegisterService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get current cash balance for a branch
    /// </summary>
    [HttpGet("balance")]
    [HasPermission(Permission.CashRegisterView)]
    public async Task<IActionResult> GetBalance([FromQuery] int? branchId = null)
    {
        // Use current user's branch if not specified
        var targetBranchId = branchId ?? _currentUserService.BranchId;
        
        var result = await _cashRegisterService.GetCurrentBalanceAsync(targetBranchId);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Get cash register transactions with filtering and pagination
    /// </summary>
    [HttpGet("transactions")]
    [HasPermission(Permission.CashRegisterView)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int? branchId = null,
        [FromQuery] CashRegisterTransactionType? type = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? shiftId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _cashRegisterService.GetTransactionsAsync(
            branchId, type, fromDate, toDate, shiftId, pageNumber, pageSize);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Create a manual deposit transaction
    /// </summary>
    [HttpPost("deposit")]
    [HasPermission(Permission.CashRegisterManage)]
    public async Task<IActionResult> Deposit([FromBody] CreateCashRegisterTransactionRequest request)
    {
        // Force type to Deposit
        request.Type = CashRegisterTransactionType.Deposit;
        
        var result = await _cashRegisterService.CreateTransactionAsync(request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Create a manual withdrawal transaction
    /// </summary>
    [HttpPost("withdraw")]
    [HasPermission(Permission.CashRegisterManage)]
    public async Task<IActionResult> Withdraw([FromBody] CreateCashRegisterTransactionRequest request)
    {
        // Force type to Withdrawal
        request.Type = CashRegisterTransactionType.Withdrawal;
        
        var result = await _cashRegisterService.CreateTransactionAsync(request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Reconcile cash register at shift close (Admin only)
    /// </summary>
    [HttpPost("reconcile")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reconcile(
        [FromQuery] int shiftId,
        [FromBody] ReconcileCashRegisterRequest request)
    {
        var result = await _cashRegisterService.ReconcileAsync(shiftId, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Transfer cash between branches (Admin only)
    /// </summary>
    [HttpPost("transfer")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Transfer([FromBody] TransferCashRequest request)
    {
        var result = await _cashRegisterService.TransferCashAsync(request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Get cash register summary for a date range
    /// </summary>
    [HttpGet("summary")]
    [HasPermission(Permission.CashRegisterView)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int? branchId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        // Use current user's branch if not specified
        var targetBranchId = branchId ?? _currentUserService.BranchId;
        
        // Default to current month if dates not specified
        var from = fromDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var to = toDate ?? DateTime.UtcNow;
        
        var result = await _cashRegisterService.GetSummaryAsync(targetBranchId, from, to);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }
}
