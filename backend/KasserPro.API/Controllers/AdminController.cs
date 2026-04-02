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
    public async Task<ActionResult<ApiResponse<BackupResult>>> CreateBackup()
    {
        var userId = User.FindFirst("userId")?.Value;
        _logger.LogInformation("Manual backup requested by user {UserId}", userId);

        var result = await _backupService.CreateBackupAsync("manual");
        if (!result.Success)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<BackupResult>.Fail(
                    ErrorCodes.BACKUP_FAILED,
                    ErrorMessages.Get(ErrorCodes.BACKUP_FAILED),
                    ToErrors(result.ErrorMessage)));
        }

        return Ok(ApiResponse<BackupResult>.Ok(result));
    }

    [HttpGet("backups")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<ApiResponse<List<BackupInfo>>>> ListBackups()
    {
        var backups = await _backupService.ListBackupsAsync();
        return Ok(ApiResponse<List<BackupInfo>>.Ok(backups));
    }

    [HttpPost("restore")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<ActionResult<ApiResponse<RestoreResult>>> RestoreBackup([FromBody] RestoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BackupFileName))
        {
            return BadRequest(ApiResponse<RestoreResult>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR),
                new List<string> { "اسم ملف النسخة الاحتياطية مطلوب" }));
        }

        var backupFilePath = await _backupService.GetBackupFilePathAsync(request.BackupFileName);
        if (backupFilePath is null)
        {
            return NotFound(ApiResponse<RestoreResult>.Fail(
                ErrorCodes.BACKUP_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.BACKUP_NOT_FOUND)));
        }

        var userId = User.FindFirst("userId")?.Value;
        _logger.LogWarning("Database restore requested by user {UserId}, backup: {BackupFileName}",
            userId, request.BackupFileName);

        var result = await _restoreService.RestoreFromBackupAsync(request.BackupFileName);
        if (!result.Success)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<RestoreResult>.Fail(
                    ErrorCodes.RESTORE_FAILED,
                    ErrorMessages.Get(ErrorCodes.RESTORE_FAILED),
                    ToErrors(result.ErrorMessage)));
        }

        return Ok(ApiResponse<RestoreResult>.Ok(result));
    }

    [HttpGet("backup/{fileName}/download")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<IActionResult> DownloadBackup(string fileName)
    {
        var filePath = await _backupService.GetBackupFilePathAsync(fileName);

        if (filePath == null)
            return NotFound(ApiResponse<object>.Fail(ErrorCodes.BACKUP_NOT_FOUND, ErrorMessages.Get(ErrorCodes.BACKUP_NOT_FOUND)));

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, "application/octet-stream", fileName);
    }

    [HttpPost("restore/upload")]
    [Authorize(Roles = "Admin,SystemOwner")]
    [RequestSizeLimit(500_000_000)]
    public async Task<ActionResult<ApiResponse<RestoreResult>>> RestoreFromUpload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<RestoreResult>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR),
                new List<string> { "يرجى تحديد ملف نسخة احتياطية" }));
        }

        if (!file.FileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<RestoreResult>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR),
                new List<string> { "نوع الملف غير مدعوم - يجب أن يكون ملف .db" }));
        }

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
            if (!result.Success)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<RestoreResult>.Fail(
                        ErrorCodes.RESTORE_FAILED,
                        ErrorMessages.Get(ErrorCodes.RESTORE_FAILED),
                        ToErrors(result.ErrorMessage)));
            }

            return Ok(ApiResponse<RestoreResult>.Ok(result));
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }

    private static List<string>? ToErrors(string? errorMessage)
        => string.IsNullOrWhiteSpace(errorMessage) ? null : new List<string> { errorMessage };
}

public class RestoreRequest
{
    public string BackupFileName { get; set; } = string.Empty;
}
