namespace KasserPro.API.Middleware;

using System.Net;
using System.Text.Json;
using KasserPro.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (SqliteException sqliteEx)
        {
            await HandleSqliteExceptionAsync(context, sqliteEx);
        }
        catch (IOException ioEx)
        {
            await HandleIoExceptionAsync(context, ioEx);
        }
        catch (DbUpdateConcurrencyException concurrencyEx)
        {
            await HandleConcurrencyExceptionAsync(context, concurrencyEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// P1 PRODUCTION: Maps SQLite error codes to actionable Arabic error messages
    /// </summary>
    private async Task HandleSqliteExceptionAsync(HttpContext context, SqliteException sqliteEx)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        var (statusCode, errorCode, message) = sqliteEx.SqliteErrorCode switch
        {
            5 => (503, "SQLITE_BUSY", "النظام مشغول، حاول مرة أخرى بعد لحظات"),
            6 => (503, "SQLITE_LOCKED", "النظام مشغول، انتظر لحظة"),
            11 => (500, "SQLITE_CORRUPT", "خطأ في قاعدة البيانات. اتصل بالدعم الفني"),
            13 => (507, "SQLITE_FULL", "القرص ممتلئ! أوقف العمل واتصل بالدعم"),
            _ => (500, "SQLITE_ERROR", "خطأ في قاعدة البيانات")
        };

        _logger.LogError(sqliteEx,
            "SQLite error {Code}: {Message} [CorrelationId: {CorrelationId}]",
            sqliteEx.SqliteErrorCode, sqliteEx.Message, correlationId);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            success = false,
            errorCode,
            message,
            correlationId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, GetJsonSerializerOptions(context)));
    }

    /// <summary>
    /// P1 PRODUCTION: Handles IO exceptions (disk full, permission issues)
    /// </summary>
    private async Task HandleIoExceptionAsync(HttpContext context, IOException ioEx)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogCritical(ioEx,
            "IO Error - possible disk full or permission issue [CorrelationId: {CorrelationId}]",
            correlationId);

        context.Response.StatusCode = 507;
        context.Response.ContentType = "application/json";

        var response = new
        {
            success = false,
            errorCode = "DISK_ERROR",
            message = "مشكلة في القرص. تحقق من المساحة المتوفرة",
            correlationId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, GetJsonSerializerOptions(context)));
    }

    /// <summary>
    /// P1 PRODUCTION: Handles concurrency conflicts
    /// </summary>
    private async Task HandleConcurrencyExceptionAsync(HttpContext context, DbUpdateConcurrencyException concurrencyEx)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogWarning(concurrencyEx,
            "Concurrency conflict [CorrelationId: {CorrelationId}]",
            correlationId);

        context.Response.StatusCode = 409;
        context.Response.ContentType = "application/json";

        var response = new
        {
            success = false,
            errorCode = "CONCURRENCY_CONFLICT",
            message = "تم تعديل البيانات من قبل مستخدم آخر. يرجى المحاولة مرة أخرى",
            correlationId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, GetJsonSerializerOptions(context)));
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            KeyNotFoundException => ((int)HttpStatusCode.NotFound, exception.Message),
            UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "غير مصرح"),
            _ => ((int)HttpStatusCode.InternalServerError, "حدث خطأ داخلي")
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            success = false,
            message,
            correlationId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, GetJsonSerializerOptions(context)));
    }

    private static JsonSerializerOptions GetJsonSerializerOptions(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }
}
