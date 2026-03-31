namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KasserPro.Application.DTOs.System;
using KasserPro.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/system")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ISystemUserService _systemUserService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        ITenantService tenantService,
        ISystemUserService systemUserService,
        ILogger<SystemController> logger)
    {
        _tenantService = tenantService;
        _systemUserService = systemUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all tenants (SystemOwner only)
    /// </summary>
    [HttpGet("tenants")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> GetTenants()
    {
        var result = await _tenantService.GetAllTenantsForSystemOwnerAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Activate/Deactivate a tenant (SystemOwner only)
    /// </summary>
    [HttpPatch("tenants/{tenantId:int}/status")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> SetTenantStatus(int tenantId, [FromBody] SetTenantStatusRequest request)
    {
        var result = await _tenantService.SetTenantActiveStatusAsync(tenantId, request.IsActive);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Create a new tenant with admin user and default branch (SystemOwner only)
    /// </summary>
    [HttpPost("tenants")]
    [Authorize(Roles = "SystemOwner")]
    [EnableRateLimiting("SystemTenantCreation")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _tenantService.CreateTenantWithAdminAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get system information (IP, Network status, etc.)
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public IActionResult GetSystemInfo()
    {
        try
        {
            var lanIp = GetLanIpAddress();
            var hostname = System.Net.Dns.GetHostName();

            return Ok(new
            {
                success = true,
                data = new
                {
                    lanIp,
                    hostname,
                    port = 5243,
                    url = $"http://{lanIp}:5243",
                    timestamp = DateTime.UtcNow,
                    isOffline = false
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system info");
            return StatusCode(500, new { success = false, message = "Failed to retrieve system information" });
        }
    }

    /// <summary>
    /// Health check endpoint (for network status monitoring)
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { success = true, status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Get all users across all tenants (SystemOwner only)
    /// Uses service layer — not direct DbContext access
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> GetAllUsers()
    {
        var result = await _systemUserService.GetAllUsersAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Update user information (SystemOwner only)
    /// </summary>
    [HttpPut("users/{userId:int}")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> UpdateUser(int userId, [FromBody] Application.Services.Interfaces.UpdateSystemUserRequest request)
    {
        var result = await _systemUserService.UpdateUserAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Toggle user active status (SystemOwner only)
    /// </summary>
    [HttpPatch("users/{userId:int}/toggle-status")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> ToggleUserStatus(int userId)
    {
        var result = await _systemUserService.ToggleUserStatusAsync(userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reset user password (SystemOwner only)
    /// </summary>
    [HttpPost("users/{userId:int}/reset-password")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> ResetUserPassword(int userId, [FromBody] ResetPasswordDto request)
    {
        var result = await _systemUserService.ResetUserPasswordAsync(userId, request.NewPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Helper: Get LAN IP address
    /// </summary>
    private static string GetLanIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get LAN IP: {ex.Message}");
        }
        return "127.0.0.1";
    }
}

/// <summary>
/// Request DTO for password reset
/// </summary>
public class ResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}
