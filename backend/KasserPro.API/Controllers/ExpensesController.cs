using KasserPro.Application.DTOs.Expenses;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
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
    private readonly IWebHostEnvironment _env;

    public ExpensesController(
        IExpenseService expenseService,
        ILogger<ExpensesController> logger,
        IWebHostEnvironment env)
    {
        _expenseService = expenseService;
        _logger = logger;
        _env = env;
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

    /// <summary>
    /// Upload an attachment for an expense (Draft only)
    /// </summary>
    [HttpPost("{id}/attachments")]
    [HasPermission(Permission.ExpensesCreate)]
    public async Task<IActionResult> UploadAttachment(int id, IFormFile file)
    {
        if (file == null || file.Length <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR)));
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "application/pdf" };
        if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.EXPENSE_ATTACHMENT_INVALID_TYPE,
                ErrorMessages.Get(ErrorCodes.EXPENSE_ATTACHMENT_INVALID_TYPE)));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.EXPENSE_ATTACHMENT_TOO_LARGE,
                ErrorMessages.Get(ErrorCodes.EXPENSE_ATTACHMENT_TOO_LARGE)));
        }

        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", "expenses");
        Directory.CreateDirectory(uploadsDir);

        var extension = Path.GetExtension(file.FileName);
        var storedName = $"expense_{id}_{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(uploadsDir, storedName);
        var relativePath = $"/uploads/expenses/{storedName}";

        await using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var result = await _expenseService.UploadAttachmentAsync(
            id,
            file.FileName,
            relativePath,
            file.Length,
            file.ContentType);

        if (!result.Success)
        {
            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete an attachment from an expense (Draft only)
    /// </summary>
    [HttpDelete("{expenseId}/attachments/{attachmentId}")]
    [HasPermission(Permission.ExpensesManage)]
    public async Task<IActionResult> DeleteAttachment(int expenseId, int attachmentId)
    {
        var result = await _expenseService.DeleteAttachmentAsync(expenseId, attachmentId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
