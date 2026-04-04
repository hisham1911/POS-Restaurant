namespace KasserPro.API.Middleware;

using System.Text.Json;
using KasserPro.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// P0 SECURITY: Blocks incoming requests during critical operations (restore, migration)
/// Checks for maintenance.lock file in application root
/// </summary>
public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MaintenanceModeMiddleware> _logger;
    private readonly string _lockFilePath;

    public MaintenanceModeMiddleware(
        RequestDelegate next, 
        ILogger<MaintenanceModeMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _lockFilePath = Path.Combine(env.ContentRootPath, "maintenance.lock");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (File.Exists(_lockFilePath))
        {
            // Allow health checks during maintenance
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await _next(context);
                return;
            }

            _logger.LogInformation(
                "Request blocked due to maintenance mode: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = 503;
            context.Response.ContentType = "application/json";
            
            var response = new
            {
                success = false,
                message = "النظام قيد الصيانة. يرجى المحاولة لاحقاً",
                retryAfter = 60
            };
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, GetJsonSerializerOptions(context)));
            return;
        }

        await _next(context);
    }

    private static JsonSerializerOptions GetJsonSerializerOptions(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }
}

/// <summary>
/// Service to control maintenance mode
/// </summary>
public class MaintenanceModeService
{
    private readonly string _lockFilePath;
    private readonly ILogger<MaintenanceModeService> _logger;

    public MaintenanceModeService(IWebHostEnvironment env, ILogger<MaintenanceModeService> logger)
    {
        _lockFilePath = Path.Combine(env.ContentRootPath, "maintenance.lock");
        _logger = logger;
    }

    public void Enable(string reason)
    {
        File.WriteAllText(_lockFilePath, $"{DateTime.UtcNow:O}|{reason}");
        _logger.LogWarning("Maintenance mode enabled: {Reason}", reason);
    }

    public void Disable()
    {
        if (File.Exists(_lockFilePath))
        {
            File.Delete(_lockFilePath);
            _logger.LogInformation("Maintenance mode disabled");
        }
    }

    public bool IsEnabled() => File.Exists(_lockFilePath);
}
