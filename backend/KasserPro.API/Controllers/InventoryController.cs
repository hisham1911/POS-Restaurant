namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.DTOs.Inventory;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Get inventory for a specific branch
    /// </summary>
    [HttpGet("branch/{branchId}")]
    [HasPermission(Permission.InventoryView)]
    public async Task<IActionResult> GetBranchInventory(int branchId)
    {
        var result = await _inventoryService.GetBranchInventoryAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get inventory for a product across all branches
    /// </summary>
    [HttpGet("product/{productId}")]
    [HasPermission(Permission.InventoryView)]
    public async Task<IActionResult> GetProductInventory(int productId)
    {
        var result = await _inventoryService.GetProductInventoryAcrossBranchesAsync(productId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get low stock items (optionally filtered by branch)
    /// </summary>
    [HttpGet("low-stock")]
    [HasPermission(Permission.InventoryView)]
    public async Task<IActionResult> GetLowStockItems([FromQuery] int? branchId = null)
    {
        var result = await _inventoryService.GetLowStockItemsAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Manually adjust inventory
    /// </summary>
    [HttpPost("adjust")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<IActionResult> AdjustInventory([FromBody] AdjustInventoryRequest request)
    {
        var result = await _inventoryService.AdjustInventoryAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Create inventory transfer request
    /// </summary>
    [HttpPost("transfer")]
    [HasPermission(Permission.InventoryTransfer)]
    public async Task<IActionResult> CreateTransfer([FromBody] CreateTransferRequest request)
    {
        var result = await _inventoryService.CreateTransferAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get all transfers with optional filters
    /// </summary>
    [HttpGet("transfer")]
    [HasPermission(Permission.InventoryView)]
    public async Task<IActionResult> GetTransfers(
        [FromQuery] int? fromBranchId = null,
        [FromQuery] int? toBranchId = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _inventoryService.GetTransfersAsync(fromBranchId, toBranchId, status, pageNumber, pageSize);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get transfer by ID
    /// </summary>
    [HttpGet("transfer/{id}")]
    [HasPermission(Permission.InventoryView)]
    public async Task<IActionResult> GetTransferById(int id)
    {
        var result = await _inventoryService.GetTransferByIdAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Approve transfer - deducts from source
    /// </summary>
    [HttpPost("transfer/{id}/approve")]
    [HasPermission(Permission.InventoryTransfer)]
    public async Task<IActionResult> ApproveTransfer(int id)
    {
        var result = await _inventoryService.ApproveTransferAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Receive transfer - adds to destination
    /// </summary>
    [HttpPost("transfer/{id}/receive")]
    [HasPermission(Permission.InventoryTransfer)]
    public async Task<IActionResult> ReceiveTransfer(int id)
    {
        var result = await _inventoryService.ReceiveTransferAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Cancel transfer
    /// </summary>
    [HttpPost("transfer/{id}/cancel")]
    [HasPermission(Permission.InventoryTransfer)]
    public async Task<IActionResult> CancelTransfer(int id, [FromBody] CancelTransferRequest request)
    {
        var result = await _inventoryService.CancelTransferAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get branch-specific prices
    /// </summary>
    [HttpGet("branch-prices/{branchId}")]
    [HasPermission(Permission.InventoryView)]
    public async Task<IActionResult> GetBranchPrices(int branchId)
    {
        var result = await _inventoryService.GetBranchPricesAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Set branch-specific price
    /// </summary>
    [HttpPost("branch-prices")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<IActionResult> SetBranchPrice([FromBody] SetBranchPriceRequest request)
    {
        var result = await _inventoryService.SetBranchPriceAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Remove branch-specific price
    /// </summary>
    [HttpDelete("branch-prices/{branchId}/{productId}")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<IActionResult> RemoveBranchPrice(int branchId, int productId)
    {
        var result = await _inventoryService.RemoveBranchPriceAsync(branchId, productId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
