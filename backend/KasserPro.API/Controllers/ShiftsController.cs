namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Shifts;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService) => _shiftService = shiftService;

    private int GetUserId()
    {
        var claim = User.FindFirst("userId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    [HttpGet("current")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetCurrent()
    {
        var userId = GetUserId();
        if (userId <= 0)
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));

        var result = await _shiftService.GetCurrentAsync(userId);
        return Ok(result);
    }

    [HttpPost("open")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> Open([FromBody] OpenShiftRequest request)
    {
        var userId = GetUserId();
        if (userId <= 0)
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));

        var result = await _shiftService.OpenAsync(request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("close")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> Close([FromBody] CloseShiftRequest request)
    {
        var userId = GetUserId();
        if (userId <= 0)
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));

        var result = await _shiftService.CloseAsync(request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("history")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetHistory()
    {
        var userId = GetUserId();
        if (userId <= 0)
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));

        var result = await _shiftService.GetUserShiftsAsync(userId);
        return Ok(result);
    }

    [HttpPost("{id}/force-close")]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.ShiftsManage)]
    public async Task<IActionResult> ForceClose(int id, [FromBody] ForceCloseShiftRequest request)
    {
        var result = await _shiftService.ForceCloseAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/handover")]
    [HasPermission(Permission.ShiftsManage)]
    public async Task<IActionResult> Handover(int id, [FromBody] HandoverShiftRequest request)
    {
        var result = await _shiftService.HandoverAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/update-activity")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> UpdateActivity(int id)
    {
        var result = await _shiftService.UpdateActivityAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("active")]
    [HasPermission(Permission.ShiftsManage)]
    public async Task<IActionResult> GetActiveShifts()
    {
        var result = await _shiftService.GetActiveShiftsAsync();
        return Ok(result);
    }

    [HttpGet("warnings")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetShiftWarnings()
    {
        var userId = GetUserId();
        if (userId <= 0)
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));

        var result = await _shiftService.GetShiftWarningsAsync(userId);
        return Ok(result);
    }
}
