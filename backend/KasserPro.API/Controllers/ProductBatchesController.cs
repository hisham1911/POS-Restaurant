namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.ProductBatches;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductBatchesController : ControllerBase
{
    private readonly IProductBatchService _service;
    public ProductBatchesController(IProductBatchService service) => _service = service;

    [HttpGet]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductBatchDto>>>> GetAll(
        [FromQuery] int? productId, [FromQuery] int? branchId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _service.GetAllAsync(productId, branchId, status, page, pageSize));

    [HttpGet("{id:int}")]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<ProductBatchDto>>> GetById(int id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("product/{productId:int}")]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<List<ProductBatchDto>>>> GetByProduct(int productId, [FromQuery] int? branchId)
        => Ok(await _service.GetByProductAsync(productId, branchId));

    [HttpGet("available")]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<List<ProductBatchDto>>>> GetAvailableBatches([FromQuery] int productId, [FromQuery] int branchId)
        => Ok(await _service.GetAvailableBatchesAsync(productId, branchId));

    [HttpPost]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<ProductBatchDto>>> Create([FromBody] CreateProductBatchDto dto)
        => Ok(await _service.CreateAsync(dto));

    [HttpPut("{id:int}")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<ProductBatchDto>>> Update(int id, [FromBody] UpdateProductBatchDto dto)
        => Ok(await _service.UpdateAsync(id, dto));

    [HttpPatch("{id:int}/hold")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<ProductBatchDto>>> Hold(int id, [FromBody] HoldBatchRequest request)
        => Ok(await _service.HoldAsync(id, request.Reason));

    [HttpPatch("{id:int}/release")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<ProductBatchDto>>> Release(int id, [FromBody] HoldBatchRequest request)
        => Ok(await _service.ReleaseAsync(id, request.Reason));

    [HttpDelete("{id:int}")]
    [HasPermission(Permission.InventoryManage)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        => Ok(await _service.DeleteAsync(id));

    [HttpGet("alerts/expiry")]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<BatchExpirySummaryDto>>> GetExpiryAlerts([FromQuery] int? branchId)
        => Ok(await _service.GetExpiryAlertsAsync(branchId));

    [HttpPost("update-expired")]
    [HasPermission(Permission.InventoryView)]
    public async Task<ActionResult<ApiResponse<int>>> UpdateExpiredBatches(CancellationToken ct)
        => Ok(await _service.UpdateExpiredBatchesStatusAsync(ct));
}
