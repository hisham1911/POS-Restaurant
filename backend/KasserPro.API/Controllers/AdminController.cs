namespace KasserPro.API.Controllers;

using KasserPro.Application.DTOs.Backup;
using KasserPro.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// P2: Admin endpoints for backup, restore, and system management
/// </summary>
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

    /// <summary>
    /// P2: Creates a manual backup
    /// Requires: Admin or SystemOwner role
    /// </summary>
    [HttpPost("backup")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<BackupResult>> CreateBackup()
    {
        var userId = User.FindFirst("userId")?.Value;
        _logger.LogInformation("Manual backup requested by user {UserId}", userId);

        var result = await _backupService.CreateBackupAsync("manual");

        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return StatusCode(500, result);
        }
    }

    /// <summary>
    /// P2: Lists all available backups
    /// Requires: Admin or SystemOwner role
    /// </summary>
    [HttpGet("backups")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<List<BackupInfo>>> ListBackups()
    {
        var backups = await _backupService.ListBackupsAsync();
        return Ok(backups);
    }

    /// <summary>
    /// P2: Restores database from backup
    /// Requires: Admin or SystemOwner role (critical operation)
    /// </summary>
    [HttpPost("restore")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<RestoreResult>> RestoreBackup([FromBody] RestoreRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        _logger.LogWarning("Database restore requested by user {UserId}, backup: {BackupFileName}",
            userId, request.BackupFileName);

        var result = await _restoreService.RestoreFromBackupAsync(request.BackupFileName);

        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return StatusCode(500, result);
        }
    }

    /// <summary>
    /// Downloads a specific backup file to the client
    /// Requires: Admin or SystemOwner role
    /// </summary>
    [HttpGet("backup/{fileName}/download")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<IActionResult> DownloadBackup(string fileName)
    {
        var filePath = await _backupService.GetBackupFilePathAsync(fileName);

        if (filePath == null)
        {
            return NotFound(new { message = "ملف النسخة الاحتياطية غير موجود" });
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, "application/octet-stream", fileName);
    }

    /// <summary>
    /// Restores database from an uploaded backup file (from any location on client machine)
    /// Requires: Admin or SystemOwner role (critical operation)
    /// </summary>
    [HttpPost("restore/upload")]
    [Authorize(Roles = "Admin,SystemOwner")]
    [RequestSizeLimit(500_000_000)] // 500 MB max
    public async Task<ActionResult<RestoreResult>> RestoreFromUpload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "يرجى تحديد ملف نسخة احتياطية" });
        }

        if (!file.FileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "نوع الملف غير مدعوم - يجب أن يكون ملف .db" });
        }

        var userId = User.FindFirst("userId")?.Value;
        _logger.LogWarning("Restore from uploaded file requested by user {UserId}, file: {FileName} ({Size} bytes)",
            userId, file.FileName, file.Length);

        // Save uploaded file to a temp path
        var tempPath = Path.Combine(Path.GetTempPath(), $"kp-upload-{Guid.NewGuid()}.db");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var result = await _restoreService.RestoreFromExternalFileAsync(tempPath);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return StatusCode(500, result);
            }
        }
        finally
        {
            // Clean up temp file
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }
}

/// <summary>
/// P2: Request to restore from backup
/// </summary>
public class RestoreRequest
{
    public string BackupFileName { get; set; } = string.Empty;
}
