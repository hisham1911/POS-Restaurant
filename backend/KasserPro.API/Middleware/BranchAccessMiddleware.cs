namespace KasserPro.API.Middleware;

using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
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

        // P0 SECURITY: Validate branch access
        // Admin and SystemOwner can access any branch in their tenant
        if (user.Role == Domain.Enums.UserRole.Admin || user.Role == Domain.Enums.UserRole.SystemOwner)
        {
            await _next(context);
            return;
        }

        // Cashiers can only access their assigned branch
        if (user.BranchId.HasValue && user.BranchId.Value != requestedBranchId)
        {
            _logger.LogWarning(
                "SECURITY: Branch access denied - User {UserId} (authorized: Branch {AuthorizedBranch}) attempted to access Branch {RequestedBranch}",
                userId, user.BranchId.Value, requestedBranchId);

            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            
            var response = ApiResponse<object>.Fail("BRANCH_ACCESS_DENIED", "ليس لديك صلاحية الوصول لهذا الفرع");
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        if (!user.BranchId.HasValue)
        {
            _logger.LogWarning(
                "SECURITY: Branch header rejected - User {UserId} has no assigned branch but requested Branch {RequestedBranch}",
                userId,
                requestedBranchId);

            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var noBranchResponse = ApiResponse<object>.Fail("BRANCH_ACCESS_DENIED", "لا يوجد فرع مرتبط بالمستخدم");
            await context.Response.WriteAsync(JsonSerializer.Serialize(noBranchResponse));
            return;
        }

        await _next(context);
    }
}
