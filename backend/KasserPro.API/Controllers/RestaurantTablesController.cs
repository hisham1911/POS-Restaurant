namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.API.Middleware;
using KasserPro.Application.DTOs.RestaurantTables;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;

[ApiController]
[Route("api/restaurant-tables")]
[Authorize]
public class RestaurantTablesController : ControllerBase
{
    private readonly IRestaurantTableService _restaurantTableService;

    public RestaurantTablesController(IRestaurantTableService restaurantTableService)
    {
        _restaurantTableService = restaurantTableService;
    }

    [HttpGet]
    [HasPermission(Permission.PosSell)]
    public async Task<IActionResult> GetAll([FromQuery] int branchId, CancellationToken ct)
        => Ok(await _restaurantTableService.GetAllAsync(branchId, ct));

    [HttpGet("{id}")]
    [HasPermission(Permission.PosSell)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _restaurantTableService.GetByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [HasPermission(Permission.SettingsManage)]
    public async Task<IActionResult> Create([FromBody] CreateRestaurantTableRequest request, CancellationToken ct)
    {
        var result = await _restaurantTableService.CreateAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [HasPermission(Permission.SettingsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRestaurantTableRequest request, CancellationToken ct)
    {
        var result = await _restaurantTableService.UpdateAsync(id, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    [HasPermission(Permission.SettingsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _restaurantTableService.DeleteAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/status")]
    [HasPermission(Permission.SettingsManage)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetRestaurantTableStatusRequest request, CancellationToken ct)
    {
        var result = await _restaurantTableService.SetStatusAsync(id, request.Status, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
