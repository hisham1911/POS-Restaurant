namespace KasserPro.API.Middleware;

using System.Reflection;
using KasserPro.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Enforces branch ownership for branch identifiers coming from route/query/body.
/// Non-admin users are restricted to their assigned branch.
/// </summary>
public sealed class BranchScopeAuthorizationFilter : IAsyncActionFilter
{
    private static readonly string[] BranchIdParameterNames =
    [
        "branchId",
        "fromBranchId",
        "toBranchId"
    ];

    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<BranchScopeAuthorizationFilter> _logger;

    public BranchScopeAuthorizationFilter(
        ICurrentUserService currentUser,
        ILogger<BranchScopeAuthorizationFilter> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_currentUser.IsAuthenticated)
        {
            await next();
            return;
        }

        if (IsPrivilegedRole(_currentUser.Role))
        {
            await next();
            return;
        }

        var assignedBranchId = _currentUser.BranchId;
        if (assignedBranchId <= 0)
        {
            context.Result = new ObjectResult(new { success = false, message = "لا يوجد فرع مرتبط بالمستخدم" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        var requestedBranchIds = ExtractRequestedBranchIds(context);
        foreach (var requestedBranchId in requestedBranchIds)
        {
            if (requestedBranchId <= 0)
            {
                continue;
            }

            if (requestedBranchId != assignedBranchId)
            {
                _logger.LogWarning(
                    "SECURITY: Branch scope denied in action {ActionName} - User {UserId} assigned to Branch {AssignedBranchId} attempted Branch {RequestedBranchId}",
                    context.ActionDescriptor.DisplayName,
                    _currentUser.UserId,
                    assignedBranchId,
                    requestedBranchId);

                context.Result = new ObjectResult(new { success = false, message = "ليس لديك صلاحية الوصول لهذا الفرع" })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        await next();
    }

    private static bool IsPrivilegedRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("SystemOwner", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<int> ExtractRequestedBranchIds(ActionExecutingContext context)
    {
        var ids = new HashSet<int>();

        // 1) Direct scalar action parameters (route/query/body bound)
        foreach (var name in BranchIdParameterNames)
        {
            if (context.ActionArguments.TryGetValue(name, out var value) && TryGetInt(value, out var id))
            {
                ids.Add(id);
            }
        }

        // 2) Query string fallback
        foreach (var name in BranchIdParameterNames)
        {
            if (context.HttpContext.Request.Query.TryGetValue(name, out var values)
                && int.TryParse(values.FirstOrDefault(), out var id))
            {
                ids.Add(id);
            }
        }

        // 3) Complex body DTOs (e.g., CreateTransferRequest, TransferCashRequest)
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null)
            {
                continue;
            }

            ExtractBranchIdsFromObject(argument, ids);
        }

        return ids;
    }

    private static void ExtractBranchIdsFromObject(object obj, ISet<int> ids)
    {
        var type = obj.GetType();
        if (type == typeof(string) || type.IsPrimitive)
        {
            return;
        }

        foreach (var name in BranchIdParameterNames)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                continue;
            }

            var value = property.GetValue(obj);
            if (TryGetInt(value, out var id))
            {
                ids.Add(id);
            }
        }
    }

    private static bool TryGetInt(object? value, out int id)
    {
        switch (value)
        {
            case int v:
                id = v;
                return true;
            case long v when v is >= int.MinValue and <= int.MaxValue:
                id = (int)v;
                return true;
            case string s when int.TryParse(s, out var parsed):
                id = parsed;
                return true;
            default:
                id = 0;
                return false;
        }
    }
}
