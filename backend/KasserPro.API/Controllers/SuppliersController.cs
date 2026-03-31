namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.DTOs.Suppliers;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService) 
        => _supplierService = supplierService;

    /// <summary>
    /// Get all suppliers for the current tenant
    /// </summary>
    [HttpGet]
    [HasPermission(Permission.SuppliersView)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _supplierService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// Get supplier by ID
    /// </summary>
    [HttpGet("{id}")]
    [HasPermission(Permission.SuppliersView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _supplierService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Create a new supplier (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.SuppliersManage)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
    {
        var result = await _supplierService.CreateAsync(request);
        return result.Success 
            ? CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result) 
            : BadRequest(result);
    }

    /// <summary>
    /// Update an existing supplier (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.SuppliersManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSupplierRequest request)
    {
        var result = await _supplierService.UpdateAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Delete a supplier (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.SuppliersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _supplierService.DeleteAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
