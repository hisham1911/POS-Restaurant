namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs;
using KasserPro.Domain.Enums;

/// <summary>
/// Service for managing user permissions.
/// Admins and SystemOwners automatically have all permissions.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Get all permissions for a specific user.
    /// Returns all permissions for Admin/SystemOwner.
    /// </summary>
    Task<List<Permission>> GetUserPermissionsAsync(int userId);

    /// <summary>
    /// Get user permissions as DTO with user info.
    /// </summary>
    Task<UserPermissionsDto?> GetUserPermissionsDtoAsync(int userId);

    /// <summary>
    /// Get all cashier users with their permissions.
    /// </summary>
    Task<List<UserPermissionsDto>> GetAllCashierPermissionsAsync();

    /// <summary>
    /// Update permissions for a specific user.
    /// Updates SecurityStamp to force re-login.
    /// </summary>
    Task UpdateUserPermissionsAsync(int userId, List<Permission> permissions, int callerTenantId, int changedByUserId);

    /// <summary>
    /// Check if a user has a specific permission.
    /// Returns true for Admin/SystemOwner.
    /// </summary>
    Task<bool> HasPermissionAsync(int userId, Permission permission);

    /// <summary>
    /// Get default permissions for new cashier users.
    /// </summary>
    List<Permission> GetDefaultCashierPermissions();

    /// <summary>
    /// Get all available permissions with metadata (Arabic/English).
    /// </summary>
    List<PermissionInfoDto> GetAllAvailablePermissions();
}
