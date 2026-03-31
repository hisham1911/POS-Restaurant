namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.Application.Common;

/// <summary>
/// Controller for managing cashier permissions.
/// Admin-only endpoints to view and update permissions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        IPermissionService permissionService,
        ICurrentUserService currentUserService,
        ILogger<PermissionsController> logger)
    {
        _permissionService = permissionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available permissions with metadata (groups, descriptions in Arabic/English)
    /// </summary>
    /// <returns>List of all permissions with their metadata</returns>
    [HttpGet("available")]
    public IActionResult GetAvailablePermissions()
    {
        try
        {
            var permissions = _permissionService.GetAllAvailablePermissions();
            return Ok(ApiResponse<List<PermissionInfoDto>>.Ok(permissions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available permissions");
            return StatusCode(500, ApiResponse<List<PermissionInfoDto>>.Fail(ErrorCodes.INTERNAL_ERROR, "Error retrieving available permissions"));
        }
    }

    /// <summary>
    /// Get all cashiers with their assigned permissions
    /// </summary>
    /// <returns>List of all cashiers with their permissions</returns>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllCashierPermissions()
    {
        try
        {
            var cashiers = await _permissionService.GetAllCashierPermissionsAsync();
            return Ok(ApiResponse<List<UserPermissionsDto>>.Ok(cashiers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all cashier permissions");
            return StatusCode(500, ApiResponse<List<UserPermissionsDto>>.Fail(ErrorCodes.INTERNAL_ERROR, "Error retrieving cashier permissions"));
        }
    }

    /// <summary>
    /// Get permissions for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User permissions DTO</returns>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserPermissions(int userId)
    {
        try
        {
            var userPermissions = await _permissionService.GetUserPermissionsDtoAsync(userId);
            
            if (userPermissions == null)
            {
                return NotFound(ApiResponse<UserPermissionsDto>.Fail(ErrorCodes.USER_NOT_FOUND, $"User {userId} not found"));
            }

            return Ok(ApiResponse<UserPermissionsDto>.Ok(userPermissions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for user {UserId}", userId);
            return StatusCode(500, ApiResponse<UserPermissionsDto>.Fail(ErrorCodes.INTERNAL_ERROR, "Error retrieving user permissions"));
        }
    }

    /// <summary>
    /// Update permissions for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="request">List of permission keys to assign</param>
    /// <returns>Success message</returns>
    [HttpPut("user/{userId}")]
    public async Task<IActionResult> UpdateUserPermissions(
        int userId,
        [FromBody] UpdatePermissionsRequest request)
    {
        try
        {
            // Parse permission strings to enum values
            var permissions = new List<Permission>();
            foreach (var permissionKey in request.Permissions)
            {
                if (Enum.TryParse<Permission>(permissionKey, out var permission))
                {
                    permissions.Add(permission);
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, $"Invalid permission: {permissionKey}"));
                }
            }

            await _permissionService.UpdateUserPermissionsAsync(userId, permissions);

            _logger.LogInformation(
                "Admin {AdminId} updated permissions for user {UserId}",
                _currentUserService.UserId,
                userId);

            return Ok(ApiResponse<object>.Ok(null, "تم تحديث الصلاحيات بنجاح"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when updating permissions for user {UserId}", userId);
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, "Error updating user permissions"));
        }
    }
}
