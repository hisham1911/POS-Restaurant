namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;

public interface ISystemUserService
{
    Task<ApiResponse<List<SystemUserDto>>> GetAllUsersAsync();
    Task<ApiResponse<SystemUserDto>> UpdateUserAsync(int userId, UpdateSystemUserRequest request);
    Task<ApiResponse<bool>> ToggleUserStatusAsync(int userId);
    Task<ApiResponse<bool>> ResetUserPasswordAsync(int userId, string newPassword);
}

// DTOs co-located with interface (small enough)
public class SystemUserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateSystemUserRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
}
