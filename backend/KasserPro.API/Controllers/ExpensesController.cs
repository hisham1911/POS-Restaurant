using KasserPro.Application.DTOs.Expenses;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.API.Middleware;

namespace KasserPro.API.Controllers;

/// <summary>
/// Controller for Expense management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(
        IExpenseService expenseService,
        ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses with filtering and pagination
    /// </summary>
    [HttpGet]
    [HasPermission(Permission.ExpensesView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId = null,
        [FromQuery] ExpenseStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? shiftId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _expenseService.GetAllAsync(
            categoryId, status, fromDate, toDate, shiftId, pageNumber, pageSize);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    [HasPermission(Permission.ExpensesView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _expenseService.GetByIdAsync(id);
        
        if (!result.Success)
            return NotFound(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Create a new expense (Draft status)
    /// </summary>
    [HttpPost]
    [HasPermission(Permission.ExpensesCreate)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request)
    {
        var result = await _expenseService.CreateAsync(request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an expense (Draft only)
    /// </summary>
    [HttpPut("{id}")]
    [HasPermission(Permission.ExpensesManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        var result = await _expenseService.UpdateAsync(id, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Delete an expense (Draft only)
    /// </summary>
    [HttpDelete("{id}")]
    [HasPermission(Permission.ExpensesManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _expenseService.DeleteAsync(id);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Approve an expense (Admin only)
    /// </summary>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveExpenseRequest request)
    {
        var result = await _expenseService.ApproveAsync(id, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Reject an expense (Admin only)
    /// </summary>
    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectExpenseRequest request)
    {
        var result = await _expenseService.RejectAsync(id, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    /// <summary>
    /// Pay an expense (Admin only)
    /// </summary>
    [HttpPost("{id}/pay")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Pay(int id, [FromBody] PayExpenseRequest request)
    {
        var result = await _expenseService.PayAsync(id, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }
}
