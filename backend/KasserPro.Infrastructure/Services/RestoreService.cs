namespace KasserPro.Infrastructure.Services;

using KasserPro.Application.DTOs.Backup;
using KasserPro.Application.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KasserPro.Infrastructure.Data;

/// <summary>
/// P2: Restore service for database recovery
/// Handles: maintenance mode, integrity check, pre-restore backup,
///          file replacement, migration application, and restart notification
/// </summary>
public class RestoreService : IRestoreService
{
    private readonly ILogger<RestoreService> _logger;
    private readonly IBackupService _backupService;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _backupDirectory;
    private readonly DataValidationService _dataValidationService;
    private readonly string _contentRootPath;

    // MaintenanceModeService is resolved dynamically to avoid circular DI
    private readonly IServiceProvider _serviceProvider;

    public RestoreService(
        ILogger<RestoreService> logger,
        IBackupService backupService,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IServiceProvider serviceProvider,
        IWebHostEnvironment environment,
        DataValidationService dataValidationService)
    {
        _logger = logger;
        _backupService = backupService;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _serviceProvider = serviceProvider;
        _dataValidationService = dataValidationService;
        _contentRootPath = environment.ContentRootPath;
        _backupDirectory = Path.Combine(_contentRootPath, "backups");
    }

    /// <summary>
    /// P2: Restores database from backup with full safety checks
    /// Flow: Validate → Integrity Check → Maintenance Mode ON → Pre-Restore Backup →
    ///       Replace DB → Apply Migrations → Maintenance Mode OFF → Return result
    /// </summary>
    public async Task<RestoreResult> RestoreFromBackupAsync(string backupFileName)
    {
        var timestamp = DateTime.UtcNow;
        var backupPath = Path.Combine(_backupDirectory, backupFileName);
        var maintenanceEnabled = false;
        int migrationsApplied = 0;

        try
        {
            _logger.LogWarning("RESTORE INITIATED: {BackupFileName}", backupFileName);

            // Step 1: Validate backup file exists
            if (!File.Exists(backupPath))
            {
                // Fallback: check bin/Debug/net8.0/backups for old backups
                var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "backups", backupFileName);
                if (File.Exists(fallbackPath))
                {
                    backupPath = fallbackPath;
                    _logger.LogInformation("Using fallback backup location: {BackupPath}", backupPath);
                }
                else
                {
                    _logger.LogError("Backup file not found: {BackupPath}", backupPath);
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = "ملف النسخة الاحتياطية غير موجود",
                        RestoreTimestamp = timestamp,
                        MaintenanceModeEnabled = false
                    };
                }
            }

            // Step 2: Run integrity check on backup
            _logger.LogInformation("Running integrity check on backup: {BackupFileName}", backupFileName);
            var rawConnectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection not configured");
            var defaultBuilder = new SqliteConnectionStringBuilder(rawConnectionString);
            var integrityCheckPassed = await RunIntegrityCheckAsync(backupPath, defaultBuilder.Password);

            if (!integrityCheckPassed)
            {
                _logger.LogError("Backup integrity check FAILED: {BackupFileName}", backupFileName);
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = "ملف النسخة الاحتياطية تالف - فشل فحص السلامة",
                    RestoreTimestamp = timestamp,
                    MaintenanceModeEnabled = false
                };
            }

            // Step 3: Enable maintenance mode (blocks all API requests)
            EnableMaintenanceMode("restore");
            maintenanceEnabled = true;
            _logger.LogWarning("Maintenance mode ENABLED for restore operation");

            // Step 4: Create pre-restore backup (safety net)
            _logger.LogInformation("Creating pre-restore backup");
            var preRestoreBackup = await _backupService.CreateBackupAsync("pre-restore");

            if (!preRestoreBackup.Success)
            {
                _logger.LogError("Pre-restore backup FAILED - aborting restore");
                DisableMaintenanceMode();
                maintenanceEnabled = false;
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = "فشل إنشاء نسخة احتياطية قبل الاستعادة",
                    RestoreTimestamp = timestamp,
                    MaintenanceModeEnabled = false
                };
            }

            _logger.LogInformation("Pre-restore backup created: {BackupPath}", preRestoreBackup.BackupPath);

            // Step 5: Clear ALL connection pools (critical for SQLite file replacement)
            _logger.LogInformation("Clearing SQLite connection pools");
            SqliteConnection.ClearAllPools();
            await Task.Delay(2000); // Wait for connections to fully close

            // Step 6: Replace database file
            var builder = defaultBuilder;
            var dbPath = builder.DataSource;

            if (string.IsNullOrEmpty(dbPath))
            {
                throw new InvalidOperationException("Cannot determine database file path");
            }

            _logger.LogWarning("Replacing database file: {DbPath}", dbPath);

            // Delete WAL and SHM files if they exist (SQLite journal files)
            var walPath = $"{dbPath}-wal";
            var shmPath = $"{dbPath}-shm";

            if (File.Exists(walPath))
            {
                File.Delete(walPath);
                _logger.LogInformation("Deleted WAL file: {WalPath}", walPath);
            }

            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
                _logger.LogInformation("Deleted SHM file: {ShmPath}", shmPath);
            }

            // Replace main database file
            File.Copy(backupPath, dbPath, overwrite: true);
            _logger.LogInformation("Database file replaced successfully from backup");

            // Step 7: Apply pending migrations to the restored database
            // THIS IS CRITICAL: If the backup is from an older version,
            // the new app code expects new tables/columns that don't exist yet.
            // Running migrations upgrades the old schema to match the current code.
            _logger.LogInformation("Checking for pending migrations on restored database...");
            migrationsApplied = await ApplyMigrationsAsync();

            if (migrationsApplied > 0)
            {
                _logger.LogWarning("Applied {MigrationCount} migrations to restored database", migrationsApplied);
            }
            else
            {
                _logger.LogInformation("No pending migrations - restored database schema is current");
            }

            // Step 7b: Validate data integrity after migrations
            _logger.LogInformation("Validating data integrity after restore and migrations...");
            var validationIssues = await _dataValidationService.ValidateRestoredDataAsync(dbPath, builder.Password);

            if (validationIssues.Count > 0)
            {
                var errorIssues = validationIssues.Where(i => i.Severity == "ERROR").ToList();
                if (errorIssues.Count > 0)
                {
                    _logger.LogError(
                        "Found {Count} critical data issues after restore: {Issues}",
                        errorIssues.Count,
                        string.Join("; ", errorIssues.Select(i => i.Message)));
                }

                var warningIssues = validationIssues.Where(i => i.Severity == "WARNING").ToList();
                if (warningIssues.Count > 0)
                {
                    _logger.LogWarning(
                        "Found {Count} data warnings after restore: {Issues}",
                        warningIssues.Count,
                        string.Join("; ", warningIssues.Select(i => i.Message)));
                }
            }
            else
            {
                _logger.LogInformation("✓ Data integrity validation PASSED - no issues found");
            }

            // Step 8: Disable maintenance mode
            DisableMaintenanceMode();
            maintenanceEnabled = false;
            _logger.LogInformation("Maintenance mode DISABLED - restore complete");

            _logger.LogWarning(
                "RESTORE COMPLETED: {BackupFileName} -> {DbPath} (Pre-restore: {PreRestore}, Migrations: {MigrationCount}, Issues: {IssueCount})",
                backupFileName,
                dbPath,
                preRestoreBackup.BackupPath,
                migrationsApplied,
                validationIssues.Count);

            return new RestoreResult
            {
                Success = true,
                RestoredFromPath = backupPath,
                PreRestoreBackupPath = preRestoreBackup.BackupPath,
                RestoreTimestamp = timestamp,
                MaintenanceModeEnabled = false,
                RequiresRestart = true, // Always recommend restart after restore
                MigrationsApplied = migrationsApplied,
                DataValidationIssuesFound = validationIssues.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "RESTORE FAILED: {BackupFileName}", backupFileName);

            // Keep maintenance mode enabled on failure for safety
            if (maintenanceEnabled)
            {
                _logger.LogWarning("Maintenance mode remains ENABLED due to restore failure. Manual intervention required.");
            }

            return new RestoreResult
            {
                Success = false,
                ErrorMessage = $"فشلت عملية الاستعادة: {ex.Message}",
                RestoreTimestamp = timestamp,
                MaintenanceModeEnabled = maintenanceEnabled,
                RequiresRestart = maintenanceEnabled // If maintenance is stuck, restart needed
            };
        }
    }

    /// <summary>
    /// Applies pending EF Core migrations to the restored database.
    /// This is critical when restoring old backups after schema updates.
    /// </summary>
    private async Task<int> ApplyMigrationsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            var migrationList = pendingMigrations.ToList();

            if (migrationList.Count == 0)
            {
                return 0;
            }

            _logger.LogWarning("Found {Count} pending migrations after restore: {Migrations}",
                migrationList.Count,
                string.Join(", ", migrationList));

            await context.Database.MigrateAsync();

            _logger.LogInformation("Successfully applied {Count} migrations to restored database", migrationList.Count);
            return migrationList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply migrations after restore - app restart required");
            throw; // Let the caller handle this - it's critical
        }
    }

    /// <summary>
    /// Enables maintenance mode by creating the lock file
    /// </summary>
    private void EnableMaintenanceMode(string reason)
    {
        try
        {
            var lockFilePath = Path.Combine(_contentRootPath, "maintenance.lock");
            File.WriteAllText(lockFilePath, $"{DateTime.UtcNow:O}|{reason}");
            _logger.LogWarning("Maintenance mode ENABLED: {Reason}", reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable maintenance mode");
        }
    }

    /// <summary>
    /// Disables maintenance mode by deleting the lock file
    /// </summary>
    private void DisableMaintenanceMode()
    {
        try
        {
            var lockFilePath = Path.Combine(_contentRootPath, "maintenance.lock");
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
                _logger.LogInformation("Maintenance mode DISABLED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable maintenance mode");
        }
    }

    /// <summary>
    /// P2: Runs SQLite integrity check on backup file
    /// </summary>
    private async Task<bool> RunIntegrityCheckAsync(string backupPath, string? password)
    {
        try
        {
            var backupBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = backupPath
            };

            if (!string.IsNullOrWhiteSpace(password))
            {
                backupBuilder.Password = password;
            }

            using var connection = new SqliteConnection(backupBuilder.ConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";

            var result = await command.ExecuteScalarAsync();
            var integrityResult = result?.ToString() ?? string.Empty;

            if (integrityResult.Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Integrity check PASSED: {BackupPath}", backupPath);
                return true;
            }
            else
            {
                _logger.LogError("Integrity check FAILED: {BackupPath}, Result: {Result}",
                    backupPath, integrityResult);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Integrity check error: {BackupPath}", backupPath);
            return false;
        }
    }

    /// <summary>
    /// Restores database from an externally uploaded file.
    /// The file is copied to the backups directory first then the standard restore flow is used.
    /// </summary>
    public async Task<RestoreResult> RestoreFromExternalFileAsync(string uploadedFilePath)
    {
        var timestamp = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(uploadedFilePath) || !File.Exists(uploadedFilePath))
        {
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = "ملف الاستعادة غير موجود أو المسار غير صحيح",
                RestoreTimestamp = timestamp,
                MaintenanceModeEnabled = false
            };
        }

        // Copy uploaded file to backups directory with a recognisable external-upload name
        var importedFileName = $"kasserpro-backup-{timestamp:yyyyMMdd-HHmmss}-external-upload.db";
        var importedPath = Path.Combine(_backupDirectory, importedFileName);

        try
        {
            File.Copy(uploadedFilePath, importedPath, overwrite: true);
            _logger.LogInformation("External backup file copied to backups directory: {FileName}", importedFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy uploaded backup file to backups directory");
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = $"فشل نسخ الملف المرفوع: {ex.Message}",
                RestoreTimestamp = timestamp,
                MaintenanceModeEnabled = false
            };
        }

        // Run the standard restore flow against the copied file
        return await RestoreFromBackupAsync(importedFileName);
    }
}
