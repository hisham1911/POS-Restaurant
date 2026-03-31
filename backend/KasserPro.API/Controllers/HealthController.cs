namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using KasserPro.Infrastructure.Data;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        AppDbContext context,
        ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint for production monitoring
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Check database connection
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");

            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                database = new
                {
                    status = "connected"
                },
                uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            
            return StatusCode(503, new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                database = "disconnected"
            });
        }
    }

    /// <summary>
    /// Deep health check - validates all critical services
    /// </summary>
    [HttpGet("deep")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<IActionResult> DeepCheck()
    {
        var checks = new Dictionary<string, object>();

        // Database check
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");
            checks["database"] = new { status = "ok" };
        }
        catch (Exception ex)
        {
            checks["database"] = new { status = "error", message = ex.Message };
        }

        // Disk space check
        try
        {
            var dbPath = _context.Database.GetDbConnection().DataSource;
            var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(dbPath)!);
            var freeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
            
            checks["diskSpace"] = new
            {
                status = freeSpaceGB > 1 ? "ok" : "warning",
                freeSpaceGB,
                totalSpaceGB = drive.TotalSize / 1024 / 1024 / 1024
            };
        }
        catch (Exception ex)
        {
            checks["diskSpace"] = new { status = "error", message = ex.Message };
        }

        // Backup directory check
        try
        {
            var backupDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "backups");
            var backupExists = Directory.Exists(backupDir);
            var backupCount = backupExists ? Directory.GetFiles(backupDir).Length : 0;
            
            checks["backups"] = new
            {
                status = backupExists ? "ok" : "warning",
                directory = backupDir,
                backupCount
            };
        }
        catch (Exception ex)
        {
            checks["backups"] = new { status = "error", message = ex.Message };
        }

        // Logs directory check
        try
        {
            var logsDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "logs");
            var logsExist = Directory.Exists(logsDir);
            var logCount = logsExist ? Directory.GetFiles(logsDir).Length : 0;
            
            checks["logs"] = new
            {
                status = logsExist ? "ok" : "warning",
                directory = logsDir,
                logCount
            };
        }
        catch (Exception ex)
        {
            checks["logs"] = new { status = "error", message = ex.Message };
        }

        var allHealthy = checks.Values.All(v =>
        {
            dynamic check = v;
            return check.status == "ok";
        });

        return allHealthy 
            ? Ok(new { status = "healthy", timestamp = DateTime.UtcNow, checks })
            : StatusCode(503, new { status = "degraded", timestamp = DateTime.UtcNow, checks });
    }
}
