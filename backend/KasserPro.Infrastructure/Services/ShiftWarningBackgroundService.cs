namespace KasserPro.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using KasserPro.Infrastructure.Data;

/// <summary>
/// Background service that monitors open shifts and sends warnings when they've been open too long.
/// - After 12 hours: Warning notification
/// - After 24 hours: Critical warning + Admin notification
/// Does NOT auto-close shifts - only warns users.
/// </summary>
public class ShiftWarningBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShiftWarningBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30); // Check every 30 minutes

    public ShiftWarningBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ShiftWarningBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Shift Warning Background Service started");

        try
        {
            // Wait 1 minute before first check to allow system to fully start
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckShiftWarningsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking shift warnings");
                }

                // Wait for the next check interval
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when application is shutting down
            _logger.LogInformation("Shift Warning Background Service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Shift Warning Background Service");
        }

        _logger.LogInformation("Shift Warning Background Service stopped");
    }

    private async Task CheckShiftWarningsAsync(CancellationToken cancellationToken)
    {
        // Check if warnings are enabled
        var isEnabled = _configuration.GetValue<bool>("ShiftWarnings:Enabled", true);
        if (!isEnabled)
        {
            _logger.LogDebug("Shift warnings are disabled in configuration");
            return;
        }

        var warningHours = _configuration.GetValue<int>("ShiftWarnings:WarningHours", 12);
        var criticalHours = _configuration.GetValue<int>("ShiftWarnings:CriticalHours", 24);

        var warningCutoff = DateTime.UtcNow.AddHours(-warningHours);
        var criticalCutoff = DateTime.UtcNow.AddHours(-criticalHours);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find all open shifts
        var openShifts = await context.Shifts
            .Include(s => s.User)
            .Include(s => s.Branch)
            .Where(s => !s.IsClosed)
            .ToListAsync(cancellationToken);

        if (openShifts.Count == 0)
        {
            _logger.LogDebug("No open shifts found");
            return;
        }

        _logger.LogInformation("Checking {Count} open shift(s) for warnings", openShifts.Count);

        foreach (var shift in openShifts)
        {
            try
            {
                var hoursOpen = (DateTime.UtcNow - shift.OpenedAt).TotalHours;
                
                if (hoursOpen >= criticalHours)
                {
                    // Critical warning (24+ hours)
                    await LogCriticalWarningAsync(context, shift, hoursOpen, cancellationToken);
                }
                else if (hoursOpen >= warningHours)
                {
                    // Standard warning (12+ hours)
                    await LogWarningAsync(context, shift, hoursOpen, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process warnings for shift {ShiftId}", shift.Id);
            }
        }
    }

    private async Task LogWarningAsync(
        AppDbContext context, 
        Domain.Entities.Shift shift, 
        double hoursOpen,
        CancellationToken cancellationToken)
    {
        // Check if we already logged a warning in the last hour to avoid spam
        var lastWarning = await context.AuditLogs
            .Where(a => a.EntityType == "Shift" 
                     && a.EntityId == shift.Id
                     && a.Action == "ShiftWarning"
                     && a.CreatedAt >= DateTime.UtcNow.AddHours(-1))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastWarning != null)
        {
            _logger.LogDebug("Warning already logged for shift {ShiftId} in the last hour", shift.Id);
            return;
        }

        _logger.LogWarning(
            "⚠️ Shift {ShiftId} for user {UserName} (Branch: {BranchName}) has been open for {Hours:F1} hours",
            shift.Id, shift.User?.Name ?? "Unknown", shift.Branch?.Name ?? "Unknown", hoursOpen);

        // Log audit entry
        var auditLog = new Domain.Entities.AuditLog
        {
            TenantId = shift.TenantId,
            BranchId = shift.BranchId,
            UserId = shift.UserId,
            UserName = shift.User?.Name ?? "Unknown",
            Action = "ShiftWarning",
            EntityType = "Shift",
            EntityId = shift.Id,
            OldValues = null,
            NewValues = $"⚠️ الوردية مفتوحة منذ {hoursOpen:F1} ساعة. يُنصح بإغلاقها وفتح وردية جديدة.",
            IpAddress = "System"
        };

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Logged warning for shift {ShiftId}", shift.Id);
    }

    private async Task LogCriticalWarningAsync(
        AppDbContext context, 
        Domain.Entities.Shift shift, 
        double hoursOpen,
        CancellationToken cancellationToken)
    {
        // Check if we already logged a critical warning in the last 2 hours
        var lastCriticalWarning = await context.AuditLogs
            .Where(a => a.EntityType == "Shift" 
                     && a.EntityId == shift.Id
                     && a.Action == "ShiftCriticalWarning"
                     && a.CreatedAt >= DateTime.UtcNow.AddHours(-2))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastCriticalWarning != null)
        {
            _logger.LogDebug("Critical warning already logged for shift {ShiftId} in the last 2 hours", shift.Id);
            return;
        }

        _logger.LogError(
            "🚨 CRITICAL: Shift {ShiftId} for user {UserName} (Branch: {BranchName}) has been open for {Hours:F1} hours!",
            shift.Id, shift.User?.Name ?? "Unknown", shift.Branch?.Name ?? "Unknown", hoursOpen);

        // Log critical audit entry
        var auditLog = new Domain.Entities.AuditLog
        {
            TenantId = shift.TenantId,
            BranchId = shift.BranchId,
            UserId = shift.UserId,
            UserName = shift.User?.Name ?? "Unknown",
            Action = "ShiftCriticalWarning",
            EntityType = "Shift",
            EntityId = shift.Id,
            OldValues = null,
            NewValues = $"🚨 تحذير شديد: الوردية مفتوحة منذ {hoursOpen:F1} ساعة! يجب إغلاقها فوراً.",
            IpAddress = "System"
        };

        context.AuditLogs.Add(auditLog);

        // Also notify admins - create audit log for admin notification
        var adminUsers = await context.Users
            .Where(u => u.TenantId == shift.TenantId 
                     && u.Role == Domain.Enums.UserRole.Admin 
                     && !u.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var admin in adminUsers)
        {
            var adminNotification = new Domain.Entities.AuditLog
            {
                TenantId = shift.TenantId,
                BranchId = shift.BranchId,
                UserId = admin.Id,
                UserName = admin.Name,
                Action = "AdminNotification",
                EntityType = "Shift",
                EntityId = shift.Id,
                OldValues = null,
                NewValues = $"🚨 إشعار للمدير: الوردية #{shift.Id} للمستخدم {shift.User?.Name} في فرع {shift.Branch?.Name} مفتوحة منذ {hoursOpen:F1} ساعة",
                IpAddress = "System"
            };

            context.AuditLogs.Add(adminNotification);
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Logged critical warning for shift {ShiftId} and notified {AdminCount} admin(s)", 
            shift.Id, adminUsers.Count);
    }
}
