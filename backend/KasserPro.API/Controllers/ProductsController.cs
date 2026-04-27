namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Products;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService) => _productService = productService;

    [HttpGet]
    [HasPermission(Permission.ProductsView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] bool? lowStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _productService.GetAllAsync(categoryId, search, isActive, lowStock, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission(Permission.ProductsView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _productService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.ProductsManage)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var result = await _productService.CreateAsync(request);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.ProductsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request)
    {
        var result = await _productService.UpdateAsync(id, request);
        if (!result.Success)
        {
            if (result.ErrorCode == ErrorCodes.PRODUCT_NOT_FOUND)
                return NotFound(result);
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.ProductsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteAsync(id);
        if (!result.Success)
        {
            if (result.ErrorCode == ErrorCodes.PRODUCT_NOT_FOUND)
                return NotFound(result);
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("{id}/adjust-stock")]
    [HasPermission(Permission.ProductsManage)]
    public async Task<IActionResult> AdjustStock(int id, [FromBody] AdjustStockRequest request)
    {
        var result = await _productService.AdjustStockAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Quick create product from POS - simplified product creation for cashiers
    /// </summary>
    [HttpPost("quick-create")]
    [HasPermission(Permission.ProductsCreateFromPOS)]
    public async Task<IActionResult> QuickCreate([FromBody] QuickCreateProductRequest request)
    {
        var result = await _productService.QuickCreateAsync(request);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result) : BadRequest(result);
    }
}
