namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class PermissionService : IPermissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PermissionService> _logger;
    private readonly ICurrentUserService _currentUserService;

    private static readonly List<Permission> DefaultCashierPermissions = new()
    {
        Permission.PosSell,
        Permission.OrdersView,
    };

    public PermissionService(
        IUnitOfWork unitOfWork,
        ILogger<PermissionService> logger,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public List<Permission> GetDefaultCashierPermissions()
        => DefaultCashierPermissions.ToList();

    public async Task<List<Permission>> GetUserPermissionsAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found when getting permissions", userId);
            return new List<Permission>();
        }

        // Admin and SystemOwner get all permissions automatically
        if (user.Role == UserRole.Admin || user.Role == UserRole.SystemOwner)
        {
            return Enum.GetValues<Permission>().ToList();
        }

        // Get cashier permissions from database
        var userPermissions = await _unitOfWork.UserPermissions
            .Query()
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission)
            .ToListAsync();

        return userPermissions;
    }

    public async Task<UserPermissionsDto?> GetUserPermissionsDtoAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found when getting permissions DTO", userId);
            return null;
        }

        var permissions = await GetUserPermissionsAsync(userId);

        return new UserPermissionsDto
        {
            UserId = user.Id,
            UserName = user.Name,
            Email = user.Email,
            Permissions = permissions.Select(p => p.ToString()).ToList()
        };
    }

    public async Task<List<UserPermissionsDto>> GetAllCashierPermissionsAsync()
    {
        var currentTenantId = _currentUserService.TenantId;
        
        var cashiers = await _unitOfWork.Users
            .Query()
            .Where(u => u.Role == UserRole.Cashier 
                && u.IsActive 
                && u.TenantId == currentTenantId)
            .ToListAsync();

        var result = new List<UserPermissionsDto>();

        foreach (var cashier in cashiers)
        {
            var permissions = await GetUserPermissionsAsync(cashier.Id);
            result.Add(new UserPermissionsDto
            {
                UserId = cashier.Id,
                UserName = cashier.Name,
                Email = cashier.Email,
                Permissions = permissions.Select(p => p.ToString()).ToList()
            });
        }

        return result;
    }

    public async Task UpdateUserPermissionsAsync(int userId, List<Permission> permissions)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found when updating permissions", userId);
            throw new InvalidOperationException($"User {userId} not found");
        }

        // Don't allow updating permissions for Admin or SystemOwner
        if (user.Role == UserRole.Admin || user.Role == UserRole.SystemOwner)
        {
            _logger.LogWarning("Attempted to update permissions for Admin/SystemOwner user {UserId}", userId);
            throw new InvalidOperationException("Cannot update permissions for Admin or SystemOwner");
        }

        // Remove existing permissions
        var existingPermissions = await _unitOfWork.UserPermissions
            .Query()
            .Where(up => up.UserId == userId)
            .ToListAsync();

        foreach (var existing in existingPermissions)
        {
            _unitOfWork.UserPermissions.Delete(existing);
        }

        // Add new permissions
        foreach (var permission in permissions)
        {
            var userPermission = new UserPermission
            {
                UserId = userId,
                Permission = permission
            };
            await _unitOfWork.UserPermissions.AddAsync(userPermission);
        }

        // Update SecurityStamp to force re-login
        user.UpdateSecurityStamp();
        _unitOfWork.Users.Update(user);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Updated permissions for user {UserId}. New permissions: {Permissions}",
            userId,
            string.Join(", ", permissions));
    }

    public async Task<bool> HasPermissionAsync(int userId, Permission permission)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        // Admin and SystemOwner have all permissions
        if (user.Role == UserRole.Admin || user.Role == UserRole.SystemOwner)
        {
            return true;
        }

        // Check if cashier has the specific permission
        var hasPermission = await _unitOfWork.UserPermissions
            .Query()
            .AnyAsync(up => up.UserId == userId && up.Permission == permission);

        return hasPermission;
    }

    public List<PermissionInfoDto> GetAllAvailablePermissions()
    {
        return new List<PermissionInfoDto>
        {
            // Point of Sale
            new PermissionInfoDto
            {
                Key = Permission.PosSell.ToString(),
                Group = "Point of Sale",
                GroupAr = "نقطة البيع",
                Description = "Sell from POS",
                DescriptionAr = "البيع من نقطة البيع",
                IsDefault = true
            },
            new PermissionInfoDto
            {
                Key = Permission.PosApplyDiscount.ToString(),
                Group = "Point of Sale",
                GroupAr = "نقطة البيع",
                Description = "Apply discounts",
                DescriptionAr = "تطبيق خصومات",
                IsDefault = false
            },

            // Orders
            new PermissionInfoDto
            {
                Key = Permission.OrdersView.ToString(),
                Group = "Orders",
                GroupAr = "الطلبات",
                Description = "View orders",
                DescriptionAr = "عرض الطلبات",
                IsDefault = true
            },
            new PermissionInfoDto
            {
                Key = Permission.OrdersRefund.ToString(),
                Group = "Orders",
                GroupAr = "الطلبات",
                Description = "Process refunds",
                DescriptionAr = "عمل مرتجعات",
                IsDefault = false
            },

            // Products
            new PermissionInfoDto
            {
                Key = Permission.ProductsView.ToString(),
                Group = "Products",
                GroupAr = "المنتجات",
                Description = "View products",
                DescriptionAr = "عرض المنتجات",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.ProductsManage.ToString(),
                Group = "Products",
                GroupAr = "المنتجات",
                Description = "Add/Edit/Delete products",
                DescriptionAr = "إضافة/تعديل/حذف منتجات",
                IsDefault = false
            },

            // Categories
            new PermissionInfoDto
            {
                Key = Permission.CategoriesView.ToString(),
                Group = "Categories",
                GroupAr = "التصنيفات",
                Description = "View categories",
                DescriptionAr = "عرض التصنيفات",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.CategoriesManage.ToString(),
                Group = "Categories",
                GroupAr = "التصنيفات",
                Description = "Add/Edit/Delete categories",
                DescriptionAr = "إضافة/تعديل/حذف تصنيفات",
                IsDefault = false
            },

            // Customers
            new PermissionInfoDto
            {
                Key = Permission.CustomersView.ToString(),
                Group = "Customers",
                GroupAr = "العملاء",
                Description = "View customers",
                DescriptionAr = "عرض العملاء",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.CustomersManage.ToString(),
                Group = "Customers",
                GroupAr = "العملاء",
                Description = "Add/Edit/Delete customers",
                DescriptionAr = "إضافة/تعديل/حذف عملاء",
                IsDefault = false
            },

            // Reports
            new PermissionInfoDto
            {
                Key = Permission.ReportsView.ToString(),
                Group = "Reports",
                GroupAr = "التقارير",
                Description = "View reports",
                DescriptionAr = "عرض التقارير",
                IsDefault = false
            },

            // Expenses
            new PermissionInfoDto
            {
                Key = Permission.ExpensesView.ToString(),
                Group = "Expenses",
                GroupAr = "المصروفات",
                Description = "View expenses",
                DescriptionAr = "عرض المصروفات",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.ExpensesCreate.ToString(),
                Group = "Expenses",
                GroupAr = "المصروفات",
                Description = "Create expense",
                DescriptionAr = "إنشاء مصروف",
                IsDefault = false
            },

            // Inventory
            new PermissionInfoDto
            {
                Key = Permission.InventoryView.ToString(),
                Group = "Inventory",
                GroupAr = "المخزون",
                Description = "View inventory",
                DescriptionAr = "عرض المخزون",
                IsDefault = false
            },

            // Shifts
            new PermissionInfoDto
            {
                Key = Permission.ShiftsManage.ToString(),
                Group = "Shifts",
                GroupAr = "الورديات",
                Description = "Manage all shifts",
                DescriptionAr = "إدارة الورديات (كل الورديات)",
                IsDefault = false
            },

            // Cash Register
            new PermissionInfoDto
            {
                Key = Permission.CashRegisterView.ToString(),
                Group = "Cash Register",
                GroupAr = "الخزينة",
                Description = "View cash register",
                DescriptionAr = "عرض الخزينة",
                IsDefault = false
            },
        };
    }
}
