namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Upload logo image file
    /// </summary>
    [HttpPost("current/logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "لم يتم اختيار ملف" });

        // Validate file type
        var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { success = false, message = "نوع الملف غير مدعوم. الأنواع المدعومة: PNG, JPG, GIF, WebP, SVG" });

        // Max 2MB
        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new { success = false, message = "حجم الملف يجب أن لا يتجاوز 2 ميجابايت" });

        try
        {
            // Save to wwwroot/uploads/logos/
            var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", "logos");
            Directory.CreateDirectory(uploadsDir);

            // Generate unique filename
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Delete old logo files
            foreach (var oldFile in Directory.GetFiles(uploadsDir, "logo_*"))
            {
                try { System.IO.File.Delete(oldFile); } 
                catch (Exception ex) 
                { 
                    _logger.LogDebug(ex, "Failed to delete old logo file {FilePath}", oldFile); 
                }
            }

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Build URL
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var logoUrl = $"{baseUrl}/uploads/logos/{fileName}";

            // Update tenant logoUrl
            var updateDto = new UpdateTenantDto { LogoUrl = logoUrl };
            await _tenantService.UpdateCurrentTenantAsync(updateDto);

            return Ok(new { success = true, data = new { logoUrl }, message = "تم رفع اللوجو بنجاح" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"فشل في رفع الملف: {ex.Message}" });
        }
    }
}
