namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class UserManagementService : IUserManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPermissionService permissionService,
        ILogger<UserManagementService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<ApiResponse<List<UserDto>>> GetAllUsersAsync()
    {
        try
        {
            var users = await _unitOfWork.Users
                .Query()
                .Where(u => u.TenantId == _currentUserService.TenantId && !u.IsDeleted)
                .Include(u => u.Branch)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var userDtos = users.Select(u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                TenantId = u.TenantId,
                BranchId = u.BranchId,
                BranchName = u.Branch?.Name,
                CreatedAt = u.CreatedAt
            }).ToList();

            return ApiResponse<List<UserDto>>.Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            return ApiResponse<List<UserDto>>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR));
        }
    }

    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(int userId)
    {
        try
        {
            var user = await _unitOfWork.Users
                .Query()
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
                return ApiResponse<UserDto>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

            if (user.TenantId != _currentUserService.TenantId)
                return ApiResponse<UserDto>.Fail(ErrorCodes.FORBIDDEN, ErrorMessages.Get(ErrorCodes.FORBIDDEN));

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsActive = user.IsActive,
                TenantId = user.TenantId,
                BranchId = user.BranchId,
                BranchName = user.Branch?.Name,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<UserDto>.Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            return ApiResponse<UserDto>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR));
        }
    }

    public async Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            var exists = await _unitOfWork.Users.FindAsync(u => u.Email == request.Email);
            if (exists.Any())
                return ApiResponse<UserDto>.Fail(ErrorCodes.CONFLICT, ErrorMessages.Get(ErrorCodes.CONFLICT));

            var requestedRole = Enum.Parse<UserRole>(request.Role);
            var currentUserRole = Enum.Parse<UserRole>(_currentUserService.Role!);

            if (currentUserRole == UserRole.Admin && requestedRole == UserRole.SystemOwner)
            {
                _logger.LogWarning("Admin {UserId} tried to create SystemOwner", _currentUserService.UserId);
                return ApiResponse<UserDto>.Fail(ErrorCodes.FORBIDDEN, ErrorMessages.Get(ErrorCodes.FORBIDDEN));
            }

            // Branch assignment policy:
            // - Admin/SystemOwner: no fixed branch (null)
            // - Cashier: must be attached to a specific branch
            int? resolvedBranchId;
            if (requestedRole == UserRole.Cashier)
            {
                resolvedBranchId = request.BranchId ?? _currentUserService.BranchId;
                if (!resolvedBranchId.HasValue)
                    return ApiResponse<UserDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));
            }
            else
            {
                resolvedBranchId = null;
            }

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Phone = request.Phone,
                Role = requestedRole,
                TenantId = _currentUserService.TenantId,
                BranchId = resolvedBranchId,
                IsActive = true
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            if (user.Role == UserRole.Cashier)
            {
                var defaultPermissions = _permissionService.GetDefaultCashierPermissions();
                await _permissionService.UpdateUserPermissionsAsync(user.Id, defaultPermissions);
            }

            _logger.LogInformation("User {UserId} created by {AdminId}", user.Id, _currentUserService.UserId);

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsActive = user.IsActive,
                TenantId = user.TenantId,
                BranchId = user.BranchId,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<UserDto>.Ok(userDto, "تم إنشاء المستخدم بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return ApiResponse<UserDto>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR));
        }
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAsync(int userId, UpdateUserRequest request)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
                return ApiResponse<UserDto>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

            if (user.TenantId != _currentUserService.TenantId)
                return ApiResponse<UserDto>.Fail(ErrorCodes.FORBIDDEN, ErrorMessages.Get(ErrorCodes.FORBIDDEN));

            if (user.Email != request.Email)
            {
                var emailExists = await _unitOfWork.Users.FindAsync(u => u.Email == request.Email && u.Id != userId);
                if (emailExists.Any())
                    return ApiResponse<UserDto>.Fail(ErrorCodes.CONFLICT, ErrorMessages.Get(ErrorCodes.CONFLICT));
            }

            var requestedRole = Enum.Parse<UserRole>(request.Role);
            var currentUserRole = Enum.Parse<UserRole>(_currentUserService.Role!);

            if (currentUserRole == UserRole.Admin && requestedRole == UserRole.SystemOwner)
            {
                _logger.LogWarning("Admin {UserId} tried to escalate user {TargetUserId} to SystemOwner",
                    _currentUserService.UserId, userId);
                return ApiResponse<UserDto>.Fail(ErrorCodes.FORBIDDEN, ErrorMessages.Get(ErrorCodes.FORBIDDEN));
            }

            // Branch assignment policy:
            // - Admin/SystemOwner: no fixed branch (null)
            // - Cashier: must be attached to a specific branch
            int? resolvedBranchId;
            if (requestedRole == UserRole.Cashier)
            {
                resolvedBranchId = request.BranchId ?? user.BranchId ?? _currentUserService.BranchId;
                if (!resolvedBranchId.HasValue)
                    return ApiResponse<UserDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));
            }
            else
            {
                resolvedBranchId = null;
            }

            user.Name = request.Name;
            user.Email = request.Email;
            user.Phone = request.Phone;
            user.Role = requestedRole;
            user.BranchId = resolvedBranchId;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated by {AdminId}", userId, _currentUserService.UserId);

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsActive = user.IsActive,
                TenantId = user.TenantId,
                BranchId = user.BranchId,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<UserDto>.Ok(userDto, "تم تحديث المستخدم بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return ApiResponse<UserDto>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR));
        }
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(int userId)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
                return ApiResponse<bool>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

            if (user.TenantId != _currentUserService.TenantId)
                return ApiResponse<bool>.Fail(ErrorCodes.FORBIDDEN, ErrorMessages.Get(ErrorCodes.FORBIDDEN));

            if (user.Id == _currentUserService.UserId)
                return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

            user.IsDeleted = true;
            user.UpdateSecurityStamp();
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted by {AdminId}", userId, _currentUserService.UserId);

            return ApiResponse<bool>.Ok(true, "تم حذف المستخدم بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return ApiResponse<bool>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR));
        }
    }

    public async Task<ApiResponse<bool>> ToggleUserStatusAsync(int userId, bool isActive)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
                return ApiResponse<bool>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

            if (user.TenantId != _currentUserService.TenantId)
                return ApiResponse<bool>.Fail(ErrorCodes.FORBIDDEN, ErrorMessages.Get(ErrorCodes.FORBIDDEN));

            if (user.Id == _currentUserService.UserId)
                return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

            user.IsActive = isActive;
            if (!isActive)
                user.UpdateSecurityStamp();

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User {UserId} status changed to {Status} by {AdminId}",
                userId, isActive ? "Active" : "Inactive", _currentUserService.UserId);

            return ApiResponse<bool>.Ok(true, isActive ? "تم تفعيل المستخدم" : "تم تعطيل المستخدم");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling user {UserId} status", userId);
            return ApiResponse<bool>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR));
        }
    }
}
