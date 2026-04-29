namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Inventory;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Application.Common;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockTakingController : ControllerBase
{
    private readonly IStockTakingService _stockTakingService;

    public StockTakingController(IStockTakingService stockTakingService)
    {
        _stockTakingService = stockTakingService;
    }

    [HttpGet]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<PagedResult<StockTakingDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var result = await _stockTakingService.GetAllAsync(page, pageSize, status);
        return Ok(ApiResponse<PagedResult<StockTakingDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<StockTakingDto>>> GetById(int id)
    {
        var result = await _stockTakingService.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<StockTakingDto>.Fail(ErrorCodes.NOT_FOUND, "جلسة الجرد غير موجودة"));

        return Ok(ApiResponse<StockTakingDto>.Ok(result));
    }

    [HttpPost]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<StockTakingDto>>> Create([FromBody] CreateStockTakingRequest request)
    {
        var result = await _stockTakingService.CreateAsync(request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("{id}/items")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<StockTakingItemDto>>> UpsertItem(int id, [FromBody] UpsertStockTakingItemRequest request)
    {
        var result = await _stockTakingService.UpsertItemAsync(id, request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpDelete("{id}/items/{itemId}")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveItem(int id, int itemId)
    {
        var result = await _stockTakingService.RemoveItemAsync(id, itemId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("{id}/complete")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<StockTakingDto>>> Complete(int id, [FromBody] CompleteStockTakingRequest request)
    {
        var result = await _stockTakingService.CompleteAsync(id, request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("{id}/cancel")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<bool>>> Cancel(int id)
    {
        var result = await _stockTakingService.CancelAsync(id);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
