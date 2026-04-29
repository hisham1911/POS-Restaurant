using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KasserPro.Domain.Enums;
using System.Security.Claims;

namespace KasserPro.API.Middleware;

/// <summary>
/// Authorization attribute to check specific permission.
/// Admins/SystemOwners bypass automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class HasPermissionAttribute : TypeFilterAttribute
{
    public HasPermissionAttribute(Permission permission)
        : base(typeof(HasPermissionFilter))
    {
        Arguments = new object[] { permission };
    }
}

public class HasPermissionFilter : IAsyncAuthorizationFilter
{
    private readonly Permission _permission;
    private static readonly Dictionary<Permission, Permission[]> PermissionImplications = new()
    {
        [Permission.OrdersCreate] = new[] { Permission.OrdersView },
        [Permission.ProductsManage] = new[] { Permission.ProductsView },
        [Permission.CategoriesManage] = new[] { Permission.CategoriesView },
        [Permission.ExpensesCreate] = new[] { Permission.ExpensesView },
        [Permission.ExpensesManage] = new[] { Permission.ExpensesView },
        [Permission.InventoryManage] = new[] { Permission.InventoryView },
        [Permission.InventoryTransfer] = new[] { Permission.InventoryView },
        [Permission.CashRegisterManage] = new[] { Permission.CashRegisterView },
        [Permission.SuppliersManage] = new[] { Permission.SuppliersView },
        [Permission.PurchaseInvoicesManage] = new[] { Permission.PurchaseInvoicesView },
        [Permission.UsersManage] = new[] { Permission.UsersView },
        [Permission.DeliveryManage] = new[] { Permission.DeliveryView },
        [Permission.PosSell] = new[]
        {
            Permission.OrdersView,
            Permission.OrdersCreate,
            Permission.ProductsView,
            Permission.CategoriesView,
            Permission.InventoryView,
            Permission.BranchesView,
        },
    };

    public HasPermissionFilter(Permission permission)
    {
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Not authenticated
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Admin & SystemOwner bypass all permission checks
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Admin" || role == "SystemOwner")
            return;

        // Check permission in JWT claims
        var permissions = user.FindAll("permission")
            .Select(c => c.Value)
            .ToList();

        if (!HasEffectivePermission(permissions, _permission))
        {
            context.Result = new ForbidResult();
            return;
        }

        await Task.CompletedTask;
    }

    private static bool HasEffectivePermission(IEnumerable<string> permissionClaims, Permission requiredPermission)
    {
        var permissions = permissionClaims
            .Select(claim => Enum.TryParse<Permission>(claim, out var permission) ? permission : (Permission?)null)
            .Where(permission => permission.HasValue)
            .Select(permission => permission!.Value)
            .ToHashSet();

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var permission in permissions.ToList())
            {
                if (!PermissionImplications.TryGetValue(permission, out var impliedPermissions))
                    continue;

                foreach (var impliedPermission in impliedPermissions)
                {
                    if (permissions.Add(impliedPermission))
                        changed = true;
                }
            }
        }

        return permissions.Contains(requiredPermission);
    }
}
