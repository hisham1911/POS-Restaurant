using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Expenses;
using KasserPro.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasserPro.API.Controllers;

[ApiController]
[Route("api/expense-categories")]
[Authorize]
public class ExpenseCategoriesController : ControllerBase
{
    private readonly IExpenseCategoryService _expenseCategoryService;
    private readonly ILogger<ExpenseCategoriesController> _logger;

    public ExpenseCategoriesController(
        IExpenseCategoryService expenseCategoryService,
        ILogger<ExpenseCategoriesController> logger)
    {
        _expenseCategoryService = expenseCategoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var result = await _expenseCategoryService.GetAllAsync(includeInactive);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("seed")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SeedDefaultCategories()
    {
        try
        {
            await _expenseCategoryService.SeedDefaultCategoriesAsync();
            return Ok(ApiResponse<bool>.Ok(true, "تمت إضافة التصنيفات الافتراضية بنجاح"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default categories");
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, "فشل في إضافة التصنيفات الافتراضية"));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _expenseCategoryService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCategoryRequest request)
    {
        var result = await _expenseCategoryService.CreateAsync(request);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseCategoryRequest request)
    {
        var result = await _expenseCategoryService.UpdateAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _expenseCategoryService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
