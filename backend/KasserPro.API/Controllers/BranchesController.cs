namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.DTOs.Branches;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService) => _branchService = branchService;

    [HttpGet]
    [HasPermission(Permission.BranchesView)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _branchService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission(Permission.BranchesView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _branchService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateBranchDto dto)
    {
        var result = await _branchService.CreateAsync(dto);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBranchDto dto)
    {
        var result = await _branchService.UpdateAsync(id, dto);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _branchService.DeleteAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
