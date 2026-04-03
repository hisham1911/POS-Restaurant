namespace KasserPro.API.Middleware;

using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

/// <summary>
/// P0 SECURITY: Validates X-Branch-Id header against user's authorized branch
/// Prevents branch tampering attacks
/// </summary>
public class BranchAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BranchAccessMiddleware> _logger;

    public BranchAccessMiddleware(RequestDelegate next, ILogger<BranchAccessMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        // Skip for anonymous endpoints
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        var headerBranchId = context.Request.Headers["X-Branch-Id"].FirstOrDefault();

        // If no header, continue (will use JWT branch)
        if (string.IsNullOrEmpty(headerBranchId))
        {
            await _next(context);
            return;
        }

        // Validate header branch against user's authorized branch
        if (!int.TryParse(headerBranchId, out var requestedBranchId))
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";

            var badRequestResponse = ApiResponse<object>.Fail("INVALID_BRANCH_HEADER", "قيمة X-Branch-Id غير صحيحة");
            await context.Response.WriteAsync(JsonSerializer.Serialize(badRequestResponse));
            return;
        }

        var userIdClaim = context.User.FindFirst("userId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var unauthorizedResponse = ApiResponse<object>.Fail("UNAUTHORIZED", "بيانات المستخدم غير صالحة");
            await context.Response.WriteAsync(JsonSerializer.Serialize(unauthorizedResponse));
            return;
        }

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var userNotFoundResponse = ApiResponse<object>.Fail("UNAUTHORIZED", "المستخدم غير موجود أو غير مفعل");
            await context.Response.WriteAsync(JsonSerializer.Serialize(userNotFoundResponse));
            return;
        }

        // Validate requested branch exists within the authenticated user's tenant.
        if (!user.TenantId.HasValue || user.TenantId.Value <= 0)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var invalidTenantContext = ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED));
            await context.Response.WriteAsync(JsonSerializer.Serialize(invalidTenantContext));
            return;
        }

        var tenantId = user.TenantId.Value;

        var branchExistsInTenant = await unitOfWork.Branches.Query()
            .AnyAsync(b => b.Id == requestedBranchId && b.TenantId == tenantId);

        if (!branchExistsInTenant)
        {
            _logger.LogWarning(
                "SECURITY: Branch access denied - User {UserId} requested invalid Branch {RequestedBranch} for Tenant {TenantId}",
                userId,
                requestedBranchId,
                tenantId);

            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                ErrorCodes.BRANCH_ACCESS_DENIED,
                ErrorMessages.Get(ErrorCodes.BRANCH_ACCESS_DENIED));
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        await _next(context);
    }
}
