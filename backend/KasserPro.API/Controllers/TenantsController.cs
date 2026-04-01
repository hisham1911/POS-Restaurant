namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Tenants;
using KasserPro.Application.Services.Interfaces;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(ITenantService tenantService, IWebHostEnvironment env, ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _env = env;
        _logger = logger;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var result = await _tenantService.GetCurrentTenantAsync();
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("current")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateTenantDto dto)
    {
        var result = await _tenantService.UpdateCurrentTenantAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("current/logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "لم يتم اختيار ملف"));

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "نوع الملف غير مدعوم. الأنواع المدعومة: PNG, JPG, GIF, WebP, SVG"));

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "حجم الملف يجب أن لا يتجاوز 2 ميجابايت"));

        try
        {
            var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", "logos");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            foreach (var oldFile in Directory.GetFiles(uploadsDir, "logo_*"))
            {
                try
                {
                    System.IO.File.Delete(oldFile);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to delete old logo file {FilePath}", oldFile);
                }
            }

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var logoUrl = $"{baseUrl}/uploads/logos/{fileName}";

            var updateDto = new UpdateTenantDto { LogoUrl = logoUrl };
            await _tenantService.UpdateCurrentTenantAsync(updateDto);

            return Ok(ApiResponse<LogoUploadResult>.Ok(new LogoUploadResult { LogoUrl = logoUrl }, "تم رفع اللوجو بنجاح"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload tenant logo");
            return StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
        }
    }
}
