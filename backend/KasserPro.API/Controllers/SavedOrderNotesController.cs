namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.API.Middleware;
using KasserPro.Application.DTOs.SavedOrderNotes;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;

[ApiController]
[Route("api/saved-order-notes")]
[Authorize]
public class SavedOrderNotesController : ControllerBase
{
    private readonly ISavedOrderNoteService _savedOrderNoteService;

    public SavedOrderNotesController(ISavedOrderNoteService savedOrderNoteService)
    {
        _savedOrderNoteService = savedOrderNoteService;
    }

    [HttpGet]
    [HasPermission(Permission.PosSell)]
    public async Task<IActionResult> GetAll([FromQuery] int branchId, CancellationToken ct)
        => Ok(await _savedOrderNoteService.GetAllAsync(branchId, ct));

    [HttpPost]
    [HasPermission(Permission.SettingsManage)]
    public async Task<IActionResult> Create([FromBody] CreateSavedOrderNoteRequest request, CancellationToken ct)
    {
        var result = await _savedOrderNoteService.CreateAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [HasPermission(Permission.SettingsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSavedOrderNoteRequest request, CancellationToken ct)
    {
        var result = await _savedOrderNoteService.UpdateAsync(id, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    [HasPermission(Permission.SettingsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _savedOrderNoteService.DeleteAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
