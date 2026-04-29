using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.Services.Implementations;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Repositories;
using KasserPro.Infrastructure.Services;
using KasserPro.API.Middleware;
using KasserPro.API;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Events;

var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDirectory);

// P1 PRODUCTION: Configure Serilog for file-based logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(logDirectory, "kasserpro-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("AuditType"))
        .WriteTo.File(
            path: Path.Combine(logDirectory, "financial-audit-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 90,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"))
    .CreateLogger();

try
{
    Log.Information("Starting KasserPro API");

    // License check - must pass before app starts
    LicenseService.ValidateOrCreateLicense(AppContext.BaseDirectory);

    // Force SQLCipher provider for encrypted SQLite support.
    SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher());

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory  // Required for Windows Service
});

// Explicit URL binding — launchSettings.json is NOT published, so set it here
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5243");

// Windows Service support (no-op when run as console app)
builder.Host.UseWindowsService(options => options.ServiceName = "KasserProService");

// P1 PRODUCTION: Use Serilog for logging
builder.Host.UseSerilog();

// P0-1: Fail startup if JWT secret is missing or too short
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    jwtKey = Environment.GetEnvironmentVariable("Jwt__Key");
}
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "FATAL: JWT Key is missing or too short. " +
        "Set environment variable 'Jwt__Key' to a random string of at least 32 characters. " +
        "Example PowerShell: $env:Jwt__Key = [Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "KasserPro";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "KasserPro";

// HttpContextAccessor for CurrentUserService
builder.Services.AddHttpContextAccessor();

// Current User Service (extracts TenantId, BranchId, UserId from JWT)
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// P0 SECURITY: Maintenance mode service
builder.Services.AddSingleton<MaintenanceModeService>();

// P1 PRODUCTION: SQLite configuration service
builder.Services.AddSingleton<KasserPro.Infrastructure.Data.SqliteConfigurationService>();

// Audit Interceptor
builder.Services.AddSingleton<AuditSaveChangesInterceptor>();

// Database — resolve relative SQLite path against the app base directory
// (Windows Services run with System32 as working directory, so relative paths break)
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=kasserpro.db;Cache=Shared";
var sqliteConnBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(rawConnectionString);
if (!Path.IsPathRooted(sqliteConnBuilder.DataSource))
{
    sqliteConnBuilder.DataSource = Path.Combine(AppContext.BaseDirectory, sqliteConnBuilder.DataSource);
}

// Production password source policy (cross-platform):
// 1) KASSERPRO_DB_PASSWORD env var
// 2) Windows-only ProgramData password file
// 3) Otherwise run without encryption (warning logged)
var databasePassword = ResolveDatabasePassword();

if (!string.IsNullOrWhiteSpace(databasePassword))
{
    sqliteConnBuilder.Password = databasePassword;
    sqliteConnBuilder.Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Default;
    await EnsureDatabaseEncryptedAsync(sqliteConnBuilder.DataSource, databasePassword);
    Log.Information("SQLite encryption is enabled for path: {DbPath}", sqliteConnBuilder.DataSource);
}
else
{
    Log.Information("SQLite encryption is disabled for this run");
}

var resolvedConnectionString = sqliteConnBuilder.ConnectionString;
Log.Information("SQLite database path: {DbPath}", sqliteConnBuilder.DataSource);

// Override connection string in configuration so all services (BackupService, etc.) use the resolved path
builder.Configuration["ConnectionStrings:DefaultConnection"] = resolvedConnectionString;

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlite(resolvedConnectionString, sqliteOptions =>
    {
        // Improves performance for heavy Include graphs.
        sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPaymentQueryService, PaymentQueryService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISystemUserService, SystemUserService>();
builder.Services.AddScoped<ISystemSeedService, SystemSeedService>();

// Sellable V1: New services for inventory and customer management
builder.Services.AddScoped<IInventoryService, KasserPro.Infrastructure.Services.InventoryService>();
builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IPurchaseInvoiceService, PurchaseInvoiceService>();
builder.Services.AddScoped<IProductBatchService, ProductBatchService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IStockTakingService, StockTakingService>();

// Report Services
builder.Services.AddScoped<IFinancialReportService, FinancialReportService>();
builder.Services.AddScoped<ICustomerReportService, CustomerReportService>();
builder.Services.AddScoped<IEmployeeReportService, EmployeeReportService>();
builder.Services.AddScoped<IProductReportService, ProductReportService>();
builder.Services.AddScoped<ISupplierReportService, SupplierReportService>();

// Expenses and Cash Register services
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
builder.Services.AddScoped<ICashRegisterService, CashRegisterService>();

// Device Command Service for SignalR
builder.Services.AddScoped<IDeviceCommandService, DeviceCommandService>();

// P2: Backup and Restore services
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IRestoreService, RestoreService>();
builder.Services.AddScoped<DataValidationService>();

// Background Services
// AutoCloseShiftBackgroundService disabled - shifts are managed manually by users
// builder.Services.AddHostedService<KasserPro.Infrastructure.Services.AutoCloseShiftBackgroundService>();
builder.Services.AddHostedService<KasserPro.Infrastructure.Services.ShiftWarningBackgroundService>();
builder.Services.AddHostedService<KasserPro.Infrastructure.Services.DailyBackupBackgroundService>();

// SignalR
builder.Services.AddSignalR();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdClaim = context.Principal?.FindFirst("userId")?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    context.Fail("Invalid token payload");
                    return;
                }

                var tokenStamp = context.Principal?.FindFirst("security_stamp")?.Value;

                // P1 PERFORMANCE: Use MemoryCache to avoid database queries on every request
                var cache = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var cacheKey = $"user_validation_{userId}_{tokenStamp}";

                // Try to get cached validation result
                if (cache.TryGetValue(cacheKey, out bool isValid))
                {
                    if (!isValid)
                    {
                        context.Fail("User is inactive or token invalidated (cached)");
                    }
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null || !user.IsActive)
                {
                    cache.Set(cacheKey, false, TimeSpan.FromMinutes(1));
                    context.Fail("User is inactive or not found");
                    return;
                }

                // P0 SECURITY: Validate SecurityStamp
                if (!string.IsNullOrEmpty(tokenStamp) && user.SecurityStamp != tokenStamp)
                {
                    cache.Set(cacheKey, false, TimeSpan.FromMinutes(1));
                    context.Fail("TOKEN_INVALIDATED");
                    return;
                }

                if (user.TenantId.HasValue)
                {
                    var tenantCacheKey = $"tenant_active_{user.TenantId.Value}";
                    if (!cache.TryGetValue(tenantCacheKey, out bool tenantActive))
                    {
                        var tenant = await db.Tenants
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value);

                        tenantActive = tenant != null && tenant.IsActive;
                        cache.Set(tenantCacheKey, tenantActive, TimeSpan.FromMinutes(5));
                    }

                    if (!tenantActive)
                    {
                        cache.Set(cacheKey, false, TimeSpan.FromMinutes(1));
                        context.Fail("Tenant is inactive");
                        return;
                    }
                }

                // Cache successful validation for 30 seconds
                cache.Set(cacheKey, true, TimeSpan.FromSeconds(30));
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("SystemTenantCreation", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(10);
        limiterOptions.QueueLimit = 0;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddControllers(options =>
    {
        // Enforce branch ownership on route/query/body branch identifiers for all actions.
        options.Filters.Add<BranchScopeAuthorizationFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddMemoryCache(); // For Idempotency

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:3000" };

    // If AllowedOrigins contains "*", allow any origin (LAN multi-device mode)
    var allowAll = allowedOrigins.Contains("*");

    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowAll)
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("X-Print-Attempted", "X-Print-Delivered");
        }
        else
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("X-Print-Attempted", "X-Print-Delivered");
        }
    });

    options.AddPolicy("SignalRPolicy", policy =>
    {
        if (allowAll)
            // AllowCredentials() cannot be used with AllowAnyOrigin() - use SetIsOriginAllowed instead
            policy.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        else
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

var app = builder.Build();

// Initialize Database (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // P1 PRODUCTION: Configure SQLite for production use
        var sqliteConfig = scope.ServiceProvider.GetRequiredService<KasserPro.Infrastructure.Data.SqliteConfigurationService>();
        await sqliteConfig.ConfigureAsync(context.Database.GetDbConnection());

        // P2: Pre-migration backup (if migrations pending)
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            if (appliedMigrations.Any())
            {
                var migrationCount = pendingMigrations.Count();
                Log.Warning("Detected {MigrationCount} pending migrations - creating pre-migration backup", migrationCount);

                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                var backupResult = await backupService.CreateBackupAsync("pre-migration");

                if (!backupResult.Success)
                {
                    Log.Fatal("Pre-migration backup FAILED - aborting startup: {ErrorMessage}", backupResult.ErrorMessage);
                    throw new InvalidOperationException($"Pre-migration backup failed: {backupResult.ErrorMessage}");
                }

                Log.Information("Pre-migration backup created: {BackupPath} ({SizeMB:F2} MB)",
                    backupResult.BackupPath,
                    backupResult.BackupSizeBytes / 1024.0 / 1024.0);
            }
            else
            {
                Log.Information("Skipping pre-migration backup: database is fresh with no applied migrations yet");
            }
        }

        // Apply migrations
        await context.Database.MigrateAsync();

        var systemSeedService = scope.ServiceProvider.GetRequiredService<ISystemSeedService>();

        // Always ensure bootstrap access exists even when demo seeding is disabled.
        await systemSeedService.EnsureSystemOwnerAsync();

        // Auto-seeding is disabled by default.
        // Set Seeding__AutoRunOnStartup=true only when explicitly needed.
        var autoRunSeedOnStartup = app.Configuration.GetValue("Seeding:AutoRunOnStartup", false);
        if (autoRunSeedOnStartup)
        {
            var seedResult = await systemSeedService.RunFullSeedPipelineAsync();

            if (seedResult.Success)
            {
                Log.Information(
                    "Startup seed pipeline completed with {WarningCount} warnings",
                    seedResult.Data?.OptionalWarnings.Count ?? 0);
            }
            else
            {
                Log.Warning(
                    "Startup seed pipeline failed: {ErrorCode} - {Message}",
                    seedResult.ErrorCode,
                    seedResult.Message);
            }
        }
        else
        {
            Log.Information("Automatic startup seeding is disabled (Seeding:AutoRunOnStartup=false)");
        }
    }
}

// P0 SECURITY: Maintenance mode middleware (FIRST - blocks all requests during critical operations)
app.UseMiddleware<MaintenanceModeMiddleware>();

// P1 PRODUCTION: Correlation ID middleware (SECOND - adds correlation ID to all requests)
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseMiddleware<ExceptionMiddleware>();
app.UseIdempotency(); // Idempotency for critical operations

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Log all requests + status codes for debugging
    app.Use(async (context, next) =>
    {
        await next();
        if (context.Response.StatusCode >= 400)
        {
            Log.Warning("[HTTP] {Method} {Path} → {StatusCode}",
                context.Request.Method,
                context.Request.Path + context.Request.QueryString,
                context.Response.StatusCode);
        }
    });
}

// Serve Frontend static files (index.html + assets)
app.UseDefaultFiles(); // Serve index.html as default file
app.UseStaticFiles(); // Serve uploaded logos + Frontend static files from wwwroot
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();

// P0 SECURITY: Branch access validation (AFTER authentication, BEFORE authorization)
app.UseMiddleware<BranchAccessMiddleware>();

app.UseAuthorization();
app.MapControllers();

// Map SignalR Hub
app.MapHub<KasserPro.API.Hubs.DeviceHub>("/hubs/devices");

// Fallback to index.html for SPA routing (React Router)
app.MapFallbackToFile("index.html");

// PRODUCTION: Auto-open browser (disabled in Development mode)
if (!app.Environment.IsDevelopment())
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(2000); // Wait for server to start

        try
        {
            // Open browser on localhost
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/c start http://localhost:5243",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(startInfo);

            Log.Information("Browser opened - Application ready at http://localhost:5243");
        }
        catch (Exception ex)
        {
            Log.Warning("Could not open browser automatically: {Exception}", ex.Message);
        }
    });
}

app.Run();
// Helper: Get LAN IP address (reserved for future network features)
// static string GetLanIpAddress()
// {
//     try
//     {
//         var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
//         foreach (var ip in host.AddressList)
//         {
//             if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
//             {
//                 return ip.ToString();
//             }
//         }
//     }
//     catch { }
//
//     return "192.168.1.X (unknown)";
// }

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static string? ResolveDatabasePassword()
{
    var environmentPassword = Environment.GetEnvironmentVariable("KASSERPRO_DB_PASSWORD");
    if (!string.IsNullOrWhiteSpace(environmentPassword))
    {
        Log.Information("SQLite encryption password source: environment variable KASSERPRO_DB_PASSWORD");
        return environmentPassword;
    }

    if (OperatingSystem.IsWindows())
    {
        var passwordFilePassword = TryResolvePasswordFileDatabasePassword();
        if (!string.IsNullOrWhiteSpace(passwordFilePassword))
        {
            Log.Information("SQLite encryption password source: ProgramData password file");
            return passwordFilePassword;
        }
    }

    Log.Warning(
        "SQLite encryption password was not found. Set KASSERPRO_DB_PASSWORD or (Windows only) " +
        @"C:\ProgramData\KasserPro\secrets\svc.dat. Running without encryption.");

    return null;
}

static string? TryResolvePasswordFileDatabasePassword()
{
    var passwordFile = @"C:\ProgramData\KasserPro\secrets\svc.dat";
    if (!File.Exists(passwordFile))
    {
        return null;
    }

    try
    {
        var password = File.ReadAllText(passwordFile).Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            Log.Warning("Password file exists but is empty: {PasswordFile}", passwordFile);
            return null;
        }

        return password;
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to read database password from {PasswordFile}", passwordFile);
        return null;
    }
}

static async Task EnsureDatabaseEncryptedAsync(string dbPath, string password)
{
    if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
    {
        return;
    }

    var canOpenPlainText = await CanOpenDatabaseAsync(dbPath, null);
    if (!canOpenPlainText)
    {
        if (await CanOpenDatabaseAsync(dbPath, password))
        {
            return;
        }

        throw new InvalidOperationException("Database exists but cannot be opened with configured password or without password");
    }

    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var backupPath = dbPath + $".pre-encryption-{timestamp}.bak";
    File.Copy(dbPath, backupPath, overwrite: false);

    var tempEncryptedPath = dbPath + ".enc";
    if (File.Exists(tempEncryptedPath))
    {
        File.Delete(tempEncryptedPath);
    }

    var sourceBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    {
        DataSource = dbPath,
        Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Default,
        Pooling = false
    };

    using (var sourceConnection = new Microsoft.Data.Sqlite.SqliteConnection(sourceBuilder.ConnectionString))
    {
        await sourceConnection.OpenAsync();

        var escapedTempPath = tempEncryptedPath.Replace("'", "''");
        var escapedPassword = password.Replace("'", "''");

        using (var prepareCommand = sourceConnection.CreateCommand())
        {
            prepareCommand.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await prepareCommand.ExecuteNonQueryAsync();
        }

        using (var exportCommand = sourceConnection.CreateCommand())
        {
            exportCommand.CommandText =
                $"ATTACH DATABASE '{escapedTempPath}' AS encrypted KEY '{escapedPassword}'; " +
                "SELECT sqlcipher_export('encrypted'); " +
                "DETACH DATABASE encrypted;";
            await exportCommand.ExecuteNonQueryAsync();
        }
    }

    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

    var walPath = dbPath + "-wal";
    var shmPath = dbPath + "-shm";
    if (File.Exists(walPath))
    {
        File.Delete(walPath);
    }
    if (File.Exists(shmPath))
    {
        File.Delete(shmPath);
    }

    File.Copy(tempEncryptedPath, dbPath, overwrite: true);
    File.Delete(tempEncryptedPath);

    if (!await CanOpenDatabaseAsync(dbPath, password))
    {
        throw new InvalidOperationException("Encrypted database verification failed after conversion");
    }
}

static async Task<bool> CanOpenDatabaseAsync(string dbPath, string? password)
{
    try
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Default,
            Pooling = false
        };

        if (!string.IsNullOrWhiteSpace(password))
        {
            builder.Password = password;
        }

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
        _ = await command.ExecuteScalarAsync();
        return true;
    }
    catch
    {
        return false;
    }
}
