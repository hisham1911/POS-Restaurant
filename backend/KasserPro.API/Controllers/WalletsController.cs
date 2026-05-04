namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.DTOs.Wallets;
using KasserPro.Application.Services.Interfaces;
using KasserPro.API.Middleware;
using KasserPro.Domain.Enums;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletsController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet]
    [HasPermission(Permission.WalletView)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _walletService.GetAllAsync(ct);
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permission.PosSell)]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await _walletService.GetActiveAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permission.WalletView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _walletService.GetByIdAsync(id, ct);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permission.WalletManage)]
    public async Task<IActionResult> Create([FromBody] CreateWalletRequest request, CancellationToken ct)
    {
        var result = await _walletService.CreateAsync(request, ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permission.WalletManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWalletRequest request, CancellationToken ct)
    {
        var result = await _walletService.UpdateAsync(id, request, ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permission.WalletManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _walletService.DeleteAsync(id, ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id:int}/deposit")]
    [HasPermission(Permission.WalletManage)]
    public async Task<IActionResult> Deposit(int id, [FromBody] WalletDepositWithdrawRequest request, CancellationToken ct)
    {
        var result = await _walletService.DepositAsync(id, request, ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id:int}/withdraw")]
    [HasPermission(Permission.WalletManage)]
    public async Task<IActionResult> Withdraw(int id, [FromBody] WalletDepositWithdrawRequest request, CancellationToken ct)
    {
        var result = await _walletService.WithdrawAsync(id, request, ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{id:int}/transactions")]
    [HasPermission(Permission.WalletView)]
    public async Task<IActionResult> GetTransactions(int id, [FromQuery] WalletTransactionFilters filters, CancellationToken ct)
    {
        var result = await _walletService.GetTransactionsAsync(id, filters, ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
