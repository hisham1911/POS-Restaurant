namespace KasserPro.Application.Services.Implementations;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Auth;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuthService> _logger;
    private readonly IPermissionService _permissionService;

    public AuthService(
        IUnitOfWork unitOfWork,
        IConfiguration config,
        ICurrentUserService currentUserService,
        ILogger<AuthService> logger,
        IPermissionService permissionService)
    {
        _unitOfWork = unitOfWork;
        _config = config;
        _currentUserService = currentUserService;
        _logger = logger;
        _permissionService = permissionService;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var users = await _unitOfWork.Users.FindAsync(u => u.Email == request.Email);
        var user = users.FirstOrDefault();

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return ApiResponse<LoginResponse>.Fail(
                ErrorCodes.INVALID_CREDENTIALS,
                ErrorMessages.Get(ErrorCodes.INVALID_CREDENTIALS));

        if (!user.IsActive)
            return ApiResponse<LoginResponse>.Fail(
                ErrorCodes.USER_INACTIVE,
                ErrorMessages.Get(ErrorCodes.USER_INACTIVE));

        if (user.TenantId.HasValue)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value);
            if (tenant == null || !tenant.IsActive)
                return ApiResponse<LoginResponse>.Fail(
                    ErrorCodes.TENANT_INACTIVE,
                    ErrorMessages.Get(ErrorCodes.TENANT_INACTIVE));
        }

        var token = await GenerateTokenAsync(user);
        var expiresAt = DateTime.UtcNow.AddHours(int.Parse(_config["Jwt:ExpiryInHours"]!));

        // Get permissions for the response
        var permissions = await _permissionService.GetUserPermissionsAsync(user.Id);
        var permissionStrings = permissions.Select(p => p.ToString()).ToList();

        return ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            User = new UserInfo
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.ToString(),
                Permissions = permissionStrings
            }
        });
    }

    public async Task<ApiResponse<bool>> RegisterAsync(RegisterRequest request)
    {
        var exists = await _unitOfWork.Users.FindAsync(u => u.Email == request.Email);
        if (exists.Any())
            return ApiResponse<bool>.Fail(
                ErrorCodes.CONFLICT,
                ErrorMessages.Get(ErrorCodes.CONFLICT));

        var requestedRole = Enum.Parse<UserRole>(request.Role);

        // P0 SECURITY: Role escalation guard
        if (_currentUserService.IsAuthenticated)
        {
            var currentUserRole = Enum.Parse<UserRole>(_currentUserService.Role!);

            // Admin cannot create SystemOwner
            if (currentUserRole == UserRole.Admin && requestedRole == UserRole.SystemOwner)
            {
                _logger.LogWarning(
                    "Role escalation attempt: Admin {UserId} tried to create SystemOwner account",
                    _currentUserService.UserId);
                return ApiResponse<bool>.Fail("INSUFFICIENT_PRIVILEGES", "ليس لديك صلاحية إنشاء حساب مالك النظام");
            }

            // Admin can only create Admin or Cashier
            if (currentUserRole == UserRole.Admin &&
                requestedRole != UserRole.Admin &&
                requestedRole != UserRole.Cashier)
            {
                _logger.LogWarning(
                    "Role escalation attempt: Admin {UserId} tried to create {Role} account",
                    _currentUserService.UserId, requestedRole);
                return ApiResponse<bool>.Fail("INSUFFICIENT_PRIVILEGES", "يمكنك فقط إنشاء حسابات مدير أو كاشير");
            }
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Phone = request.Phone,
            Role = requestedRole
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Assign default permissions for new cashiers
        if (user.Role == UserRole.Cashier)
        {
            var defaultPermissions = _permissionService.GetDefaultCashierPermissions();
            await _permissionService.UpdateUserPermissionsAsync(user.Id, defaultPermissions);
        }

        return ApiResponse<bool>.Ok(true, "تم إنشاء الحساب بنجاح");
    }

    public async Task<ApiResponse<UserInfo>> GetCurrentUserAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<UserInfo>.Fail(
                ErrorCodes.USER_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

        var permissions = await _permissionService.GetUserPermissionsAsync(userId);
        var permissionStrings = permissions.Select(p => p.ToString()).ToList();

        return ApiResponse<UserInfo>.Ok(new UserInfo
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            Permissions = permissionStrings
        });
    }

    private async Task<string> GenerateTokenAsync(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var claims = new List<Claim>
        {
            new("userId", user.Id.ToString()),
            new("security_stamp", user.SecurityStamp),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        if (user.BranchId.HasValue)
        {
            claims.Add(new Claim("branchId", user.BranchId.Value.ToString()));
        }

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("tenantId", user.TenantId.Value.ToString()));
        }

        // Add permissions as claims
        var permissions = await _permissionService.GetUserPermissionsAsync(user.Id);
        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission.ToString()));
        }

        var expiryHours = int.TryParse(_config["Jwt:ExpiryInHours"], out var hours) ? hours : 24;

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
