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
        Permission.OrdersCreate,
        Permission.ProductsView,
        Permission.CategoriesView,
        Permission.InventoryView,
        Permission.BranchesView,
    };

    private static readonly Dictionary<Permission, Permission[]> PermissionImplications = new()
    {
        [Permission.OrdersCreate] = new[] { Permission.OrdersView },
        [Permission.ProductsManage] = new[] { Permission.ProductsView },
        [Permission.CategoriesManage] = new[] { Permission.CategoriesView },
        [Permission.ExpensesCreate] = new[] { Permission.ExpensesView },
        [Permission.ExpensesManage] = new[] { Permission.ExpensesView },
        [Permission.ExpensesApprove] = new[] { Permission.ExpensesView },
        [Permission.CashRegisterTransfer] = new[] { Permission.CashRegisterView },
        [Permission.CashRegisterReconcile] = new[] { Permission.CashRegisterView },
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

        return ExpandPermissions(userPermissions);
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
        var defaultPermissions = user.Role == UserRole.Cashier
            ? DefaultCashierPermissions.Select(p => p.ToString()).ToList()
            : new List<string>();

        var currentPermissionStrings = permissions.Select(p => p.ToString()).ToList();
        var isCustomized = user.Role == UserRole.Cashier
            && !currentPermissionStrings.ToHashSet().SetEquals(defaultPermissions);

        return new UserPermissionsDto
        {
            UserId = user.Id,
            UserName = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsCustomized = isCustomized,
            Permissions = currentPermissionStrings,
            DefaultPermissions = defaultPermissions,
            TenantId = user.TenantId ?? 0
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

    public async Task UpdateUserPermissionsAsync(int userId, List<Permission> permissions, int callerTenantId, int changedByUserId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found when updating permissions", userId);
            throw new InvalidOperationException($"User {userId} not found");
        }

        // Tenant isolation check
        if (user.TenantId != callerTenantId)
        {
            _logger.LogWarning(
                "Cross-tenant permission update attempt: User {ChangedBy} tried to update user {TargetUserId} in tenant {TargetTenantId}",
                changedByUserId,
                userId,
                user.TenantId);
            throw new InvalidOperationException("Cross-tenant access denied");
        }

        // Don't allow updating permissions for Admin or SystemOwner
        if (user.Role == UserRole.Admin || user.Role == UserRole.SystemOwner)
        {
            _logger.LogWarning("Attempted to update permissions for Admin/SystemOwner user {UserId}", userId);
            throw new InvalidOperationException("Cannot update permissions for Admin or SystemOwner");
        }

        // Get previous permissions for audit
        var previousPermissions = await _unitOfWork.UserPermissions
            .Query()
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission)
            .ToListAsync();

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

        // Audit log
        var addedPermissions = permissions.Except(previousPermissions).ToList();
        var removedPermissions = previousPermissions.Except(permissions).ToList();

        _logger.LogInformation(
            "Permission audit: User {ChangedByUserId} updated permissions for user {TargetUserId} in tenant {TenantId}. Added: {Added}. Removed: {Removed}.",
            changedByUserId,
            userId,
            user.TenantId,
            addedPermissions.Any() ? string.Join(", ", addedPermissions) : "none",
            removedPermissions.Any() ? string.Join(", ", removedPermissions) : "none");

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

        var permissions = await _unitOfWork.UserPermissions
            .Query()
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission)
            .ToListAsync();

        return ExpandPermissions(permissions).Contains(permission);
    }

    private static List<Permission> ExpandPermissions(IEnumerable<Permission> permissions)
    {
        var effectivePermissions = permissions.ToHashSet();
        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var permission in effectivePermissions.ToList())
            {
                if (!PermissionImplications.TryGetValue(permission, out var impliedPermissions))
                    continue;

                foreach (var impliedPermission in impliedPermissions)
                {
                    if (effectivePermissions.Add(impliedPermission))
                        changed = true;
                }
            }
        }

        return effectivePermissions
            .OrderBy(permission => (int)permission)
            .ToList();
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
            new PermissionInfoDto
            {
                Key = Permission.PosCreditSale.ToString(),
                Group = "Point of Sale",
                GroupAr = "نقطة البيع",
                Description = "Sell on credit (partial payment)",
                DescriptionAr = "البيع الآجل (دفع جزئي)",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.PosEditPrice.ToString(),
                Group = "Point of Sale",
                GroupAr = "نقطة البيع",
                Description = "Edit item price during sale",
                DescriptionAr = "تعديل سعر المنتج أثناء البيع",
                IsDefault = false,
                IsSensitive = true
            },
            new PermissionInfoDto
            {
                Key = Permission.PosDeleteItem.ToString(),
                Group = "Point of Sale",
                GroupAr = "نقطة البيع",
                Description = "Delete items from order",
                DescriptionAr = "حذف صنف من الطلب",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.PosCancelOrder.ToString(),
                Group = "Point of Sale",
                GroupAr = "نقطة البيع",
                Description = "Cancel orders",
                DescriptionAr = "إلغاء الطلبات",
                IsDefault = false,
                IsSensitive = true
            },
            new PermissionInfoDto
            {
                Key = Permission.PosChangeBatch.ToString(),
                Group = "Point of Sale",
                GroupAr = "Ù†Ù‚Ø·Ø© Ø§Ù„Ø¨ÙŠØ¹",
                Description = "Change selected product batch during sale",
                DescriptionAr = "ØªØºÙŠÙŠØ± Ø¯ÙØ¹Ø© Ø§Ù„Ù…Ù†ØªØ¬ Ø£Ø«Ù†Ø§Ø¡ Ø§Ù„Ø¨ÙŠØ¹",
                IsDefault = false,
                IsSensitive = true
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
                IsDefault = false,
                IsSensitive = true
            },
            new PermissionInfoDto
            {
                Key = Permission.OrdersCreate.ToString(),
                Group = "Orders",
                GroupAr = "الطلبات",
                Description = "Create and update orders",
                DescriptionAr = "إنشاء وتعديل الطلبات",
                IsDefault = true
            },

            // Products
            new PermissionInfoDto
            {
                Key = Permission.ProductsView.ToString(),
                Group = "Products",
                GroupAr = "المنتجات",
                Description = "View products",
                DescriptionAr = "عرض المنتجات",
                IsDefault = true
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
                IsDefault = true
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
            new PermissionInfoDto
            {
                Key = Permission.ExpensesManage.ToString(),
                Group = "Expenses",
                GroupAr = "المصروفات",
                Description = "Manage expenses",
                DescriptionAr = "إدارة المصروفات",
                IsDefault = false,
                IsSensitive = true
            },
            new PermissionInfoDto
            {
                Key = Permission.ExpensesApprove.ToString(),
                Group = "Expenses",
                GroupAr = "المصروفات",
                Description = "Approve, reject and pay expenses",
                DescriptionAr = "اعتماد المصروفات",
                IsDefault = false,
                IsSensitive = true
            },

            // Inventory
            new PermissionInfoDto
            {
                Key = Permission.InventoryView.ToString(),
                Group = "Inventory",
                GroupAr = "المخزون",
                Description = "View inventory",
                DescriptionAr = "عرض المخزون",
                IsDefault = true
            },
            new PermissionInfoDto
            {
                Key = Permission.InventoryManage.ToString(),
                Group = "Inventory",
                GroupAr = "المخزون",
                Description = "Manage inventory (adjustments, stock taking)",
                DescriptionAr = "إدارة المخزون (تعديل، جرد)",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.InventoryTransfer.ToString(),
                Group = "Inventory",
                GroupAr = "المخزون",
                Description = "Transfer inventory between branches",
                DescriptionAr = "تحويل مخزون بين الفروع",
                IsDefault = false,
                IsSensitive = true
            },

            // Shifts
            new PermissionInfoDto
            {
                Key = Permission.ShiftsManage.ToString(),
                Group = "Shifts",
                GroupAr = "الورديات",
                Description = "Manage all shifts",
                DescriptionAr = "إدارة الورديات (كل الورديات)",
                IsDefault = false,
                IsSensitive = true
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
            new PermissionInfoDto
            {
                Key = Permission.CashRegisterManage.ToString(),
                Group = "Cash Register",
                GroupAr = "الخزينة",
                Description = "Manage cash register transactions",
                DescriptionAr = "إدارة حركات الخزينة",
                IsDefault = false,
                IsSensitive = true
            },
            new PermissionInfoDto
            {
                Key = Permission.CashRegisterTransfer.ToString(),
                Group = "Cash Register",
                GroupAr = "الخزينة",
                Description = "Transfer cash between branches",
                DescriptionAr = "تحويل نقدي بين الفروع",
                IsDefault = false,
                IsSensitive = true
            },
            new PermissionInfoDto
            {
                Key = Permission.CashRegisterReconcile.ToString(),
                Group = "Cash Register",
                GroupAr = "الخزينة",
                Description = "Reconcile and close shift",
                DescriptionAr = "مطابقة وإغلاق الشيفت",
                IsDefault = false,
                IsSensitive = true
            },

            // Branches
            new PermissionInfoDto
            {
                Key = Permission.BranchesView.ToString(),
                Group = "Branches",
                GroupAr = "الفروع",
                Description = "View branches",
                DescriptionAr = "عرض الفروع",
                IsDefault = true
            },

            // Suppliers
            new PermissionInfoDto
            {
                Key = Permission.SuppliersView.ToString(),
                Group = "Suppliers",
                GroupAr = "الموردين",
                Description = "View suppliers",
                DescriptionAr = "عرض الموردين",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.SuppliersManage.ToString(),
                Group = "Suppliers",
                GroupAr = "الموردين",
                Description = "Add/Edit/Delete suppliers",
                DescriptionAr = "إضافة/تعديل/حذف موردين",
                IsDefault = false
            },

            // Purchase Invoices
            new PermissionInfoDto
            {
                Key = Permission.PurchaseInvoicesView.ToString(),
                Group = "Purchase Invoices",
                GroupAr = "فواتير الشراء",
                Description = "View purchase invoices",
                DescriptionAr = "عرض فواتير الشراء",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.PurchaseInvoicesManage.ToString(),
                Group = "Purchase Invoices",
                GroupAr = "فواتير الشراء",
                Description = "Create/Edit/Delete purchase invoices",
                DescriptionAr = "إنشاء/تعديل/حذف فواتير الشراء",
                IsDefault = false
            },

            // Users
            new PermissionInfoDto
            {
                Key = Permission.UsersView.ToString(),
                Group = "Users",
                GroupAr = "المستخدمين",
                Description = "View users",
                DescriptionAr = "عرض المستخدمين",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.UsersManage.ToString(),
                Group = "Users",
                GroupAr = "المستخدمين",
                Description = "Add/Edit/Delete users",
                DescriptionAr = "إضافة/تعديل/حذف مستخدمين",
                IsDefault = false,
                IsSensitive = true
            },

            // Settings
            new PermissionInfoDto
            {
                Key = Permission.SettingsManage.ToString(),
                Group = "Settings",
                GroupAr = "الإعدادات",
                Description = "Manage system settings",
                DescriptionAr = "إدارة إعدادات النظام",
                IsDefault = false,
                IsSensitive = true
            },

            // Delivery
            new PermissionInfoDto
            {
                Key = Permission.DeliveryView.ToString(),
                Group = "Delivery",
                GroupAr = "التوصيل",
                Description = "View delivery orders",
                DescriptionAr = "عرض طلبات التوصيل",
                IsDefault = false
            },
            new PermissionInfoDto
            {
                Key = Permission.DeliveryManage.ToString(),
                Group = "Delivery",
                GroupAr = "التوصيل",
                Description = "Manage delivery persons and orders",
                DescriptionAr = "إدارة المناديب وطلبات التوصيل",
                IsDefault = false,
                IsSensitive = true
            },
        };
    }
}
