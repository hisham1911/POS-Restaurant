namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;

/// <summary>
/// SystemOwner-only service for managing all users across all tenants.
/// Bypasses tenant isolation by design — only callable by SystemOwner role.
/// </summary>
public class SystemUserService : ISystemUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SystemUserService> _logger;

    public SystemUserService(IUnitOfWork unitOfWork, ILogger<SystemUserService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<List<SystemUserDto>>> GetAllUsersAsync()
    {
        var users = await _unitOfWork.Users.Query()
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
            .OrderBy(u => u.TenantId)
            .ThenBy(u => u.Role)
            .ThenBy(u => u.Name)
            .Select(u => new SystemUserDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                Role = u.Role.ToString(),
                TenantId = u.TenantId,
                TenantName = u.Tenant != null ? u.Tenant.Name : "System",
                BranchId = u.BranchId,
                BranchName = u.Branch != null ? u.Branch.Name : null,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        return ApiResponse<List<SystemUserDto>>.Ok(users);
    }

    public async Task<ApiResponse<SystemUserDto>> UpdateUserAsync(int userId, UpdateSystemUserRequest request)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<SystemUserDto>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email;

        if (!string.IsNullOrWhiteSpace(request.Phone))
            user.Phone = request.Phone;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("SystemOwner updated user {UserId}: {UserName}", user.Id, user.Name);

        // Map to DTO manually (no AutoMapper per AGENTS.md)
        var dto = new SystemUserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role.ToString(),
            TenantId = user.TenantId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        return ApiResponse<SystemUserDto>.Ok(dto, "تم تحديث المستخدم بنجاح");
    }

    public async Task<ApiResponse<bool>> ToggleUserStatusAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<bool>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        // SECURITY: Invalidate all sessions when deactivating user
        if (!user.IsActive)
            user.UpdateSecurityStamp();

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("SystemOwner toggled user {UserId} status to {IsActive}", userId, user.IsActive);

        return ApiResponse<bool>.Ok(user.IsActive,
            user.IsActive ? "تم تفعيل المستخدم بنجاح" : "تم تعطيل المستخدم بنجاح");
    }

    public async Task<ApiResponse<bool>> ResetUserPasswordAsync(int userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, "كلمة المرور يجب أن تكون 6 أحرف على الأقل");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<bool>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdateSecurityStamp(); // SECURITY: Force re-login
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogWarning("SystemOwner reset password for user {UserId}: {UserName}", userId, user.Name);

        return ApiResponse<bool>.Ok(true, "تم إعادة تعيين كلمة المرور بنجاح");
    }
}
