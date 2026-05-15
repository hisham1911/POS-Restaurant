namespace KasserPro.API.Controllers;

using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Recipes;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipeService;
    private readonly ICurrentUserService _currentUser;

    public RecipesController(IRecipeService recipeService, ICurrentUserService currentUser)
    {
        _recipeService = recipeService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RecipeListDto>>>> GetAll()
    {
        var result = await _recipeService.GetAllAsync(_currentUser.TenantId, _currentUser.BranchId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<RecipeDto?>>> GetById(int id)
    {
        var result = await _recipeService.GetByIdAsync(id, _currentUser.TenantId);
        return Ok(result);
    }

    [HttpGet("product/{productId}")]
    public async Task<ActionResult<ApiResponse<RecipeDto?>>> GetByProductId(int productId)
    {
        var result = await _recipeService.GetByProductIdAsync(productId, _currentUser.TenantId);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permission.RecipesManage)]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Create([FromBody] CreateRecipeRequest request)
    {
        var result = await _recipeService.CreateAsync(request, _currentUser.TenantId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [HasPermission(Permission.RecipesManage)]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Update(int id, [FromBody] UpdateRecipeRequest request)
    {
        var result = await _recipeService.UpdateAsync(id, request, _currentUser.TenantId);
        if (!result.Success)
            return result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound(result) : BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [HasPermission(Permission.RecipesManage)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _recipeService.DeleteAsync(id, _currentUser.TenantId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("{id}/cost")]
    public async Task<ActionResult<ApiResponse<decimal>>> GetCost(int id)
    {
        var result = await _recipeService.CalculateCostAsync(id, _currentUser.TenantId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }
}
