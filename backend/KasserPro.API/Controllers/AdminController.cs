namespace KasserPro.API.Controllers;

using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Backup;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IBackupService backupService,
        IRestoreService restoreService,
        ILogger<AdminController> logger)
    {
        _backupService = backupService;
        _restoreService = restoreService;
        _logger = logger;
    }

    [HttpPost("backup")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<BackupResult>> CreateBackup()
    {
        var userId = User.FindFirst("userId")?.Value;
        _logger.LogInformation("Manual backup requested by user {UserId}", userId);

        var result = await _backupService.CreateBackupAsync("manual");
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("backups")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<List<BackupInfo>>> ListBackups()
    {
        var backups = await _backupService.ListBackupsAsync();
        return Ok(ApiResponse<List<BackupInfo>>.Ok(backups));
    }

    [HttpPost("restore")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<RestoreResult>> RestoreBackup([FromBody] RestoreRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        _logger.LogWarning("Database restore requested by user {UserId}, backup: {BackupFileName}",
            userId, request.BackupFileName);

        var result = await _restoreService.RestoreFromBackupAsync(request.BackupFileName);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("backup/{fileName}/download")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<IActionResult> DownloadBackup(string fileName)
    {
        var filePath = await _backupService.GetBackupFilePathAsync(fileName);

        if (filePath == null)
            return NotFound(ApiResponse<object>.Fail(ErrorCodes.NOT_FOUND, ErrorMessages.Get(ErrorCodes.NOT_FOUND)));

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, "application/octet-stream", fileName);
    }

    [HttpPost("restore/upload")]
    [Authorize(Roles = "Admin,SystemOwner")]
    [RequestSizeLimit(500_000_000)]
    public async Task<ActionResult<RestoreResult>> RestoreFromUpload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "يرجى تحديد ملف نسخة احتياطية"));

        if (!file.FileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "نوع الملف غير مدعوم - يجب أن يكون ملف .db"));

        var userId = User.FindFirst("userId")?.Value;
        _logger.LogWarning("Restore from uploaded file requested by user {UserId}, file: {FileName} ({Size} bytes)",
            userId, file.FileName, file.Length);

        var tempPath = Path.Combine(Path.GetTempPath(), $"kp-upload-{Guid.NewGuid()}.db");
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var result = await _restoreService.RestoreFromExternalFileAsync(tempPath);
            return result.Success ? Ok(result) : StatusCode(500, result);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }
}

public class RestoreRequest
{
    public string BackupFileName { get; set; } = string.Empty;
}
