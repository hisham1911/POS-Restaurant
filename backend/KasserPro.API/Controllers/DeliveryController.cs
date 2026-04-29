namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.API.Middleware;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Delivery;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;

    public DeliveryController(IDeliveryService deliveryService)
    {
        _deliveryService = deliveryService;
    }

    [HttpGet("persons")]
    [HasPermission(Permission.DeliveryView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _deliveryService.GetAllAsync(page, pageSize, search, ct);
        return Ok(ApiResponse<PagedResult<DeliveryPersonDto>>.Ok(result));
    }

    [HttpGet("persons/{id}")]
    [HasPermission(Permission.DeliveryView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _deliveryService.GetByIdAsync(id, ct);
        return result != null
            ? Ok(ApiResponse<DeliveryPersonDto>.Ok(result))
            : NotFound(ApiResponse<object>.Fail(ErrorCodes.NOT_FOUND, "المندوب غير موجود"));
    }

    [HttpGet("persons/active")]
    [HasPermission(Permission.DeliveryView)]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await _deliveryService.GetActiveDeliveryPersonsAsync(ct);
        return Ok(ApiResponse<List<DeliveryPersonDto>>.Ok(result));
    }

    [HttpGet("orders")]
    [HasPermission(Permission.DeliveryView)]
    public async Task<IActionResult> GetDeliveryOrders(
        [FromQuery] DeliveryOrderFilters filters,
        CancellationToken ct)
        => Ok(await _deliveryService.GetDeliveryOrdersAsync(filters, ct));

    [HttpPost("persons")]
    [HasPermission(Permission.DeliveryManage)]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryPersonRequest request, CancellationToken ct)
    {
        var result = await _deliveryService.CreateAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("persons/{id}")]
    [HasPermission(Permission.DeliveryManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeliveryPersonRequest request, CancellationToken ct)
    {
        var result = await _deliveryService.UpdateAsync(id, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("persons/{id}")]
    [HasPermission(Permission.DeliveryManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _deliveryService.DeleteAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("orders/{orderId}/assign")]
    [HasPermission(Permission.DeliveryManage)]
    public async Task<IActionResult> AssignDeliveryPerson(
        int orderId,
        [FromBody] AssignDeliveryRequest request,
        CancellationToken ct)
    {
        var result = await _deliveryService.AssignDeliveryPersonAsync(orderId, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("orders/{orderId}/status")]
    [HasPermission(Permission.DeliveryManage)]
    public async Task<IActionResult> UpdateDeliveryStatus(
        int orderId,
        [FromBody] UpdateDeliveryStatusRequest request,
        CancellationToken ct)
    {
        var result = await _deliveryService.UpdateDeliveryStatusAsync(orderId, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
