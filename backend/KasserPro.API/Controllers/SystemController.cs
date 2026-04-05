namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.System;
using KasserPro.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

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
    [Authorize(Roles = "Admin,SystemOwner")]
    public IActionResult GetSystemInfo()
    {
        try
        {
            var lanIp = GetLanIpAddress();
            var localPort = HttpContext.Connection.LocalPort;
            var port = localPort > 0 ? localPort : 5243;
            var hostname = System.Net.Dns.GetHostName();
            var data = new SystemInfoDto
            {
                LanIp = lanIp,
                Hostname = hostname,
                Port = port,
                Url = $"http://{lanIp}:{port}",
                Timestamp = DateTime.UtcNow,
                IsOffline = false
            };

            return Ok(ApiResponse<SystemInfoDto>.Ok(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system info");
            return StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
        }
    }

    /// <summary>
    /// Health check endpoint (for network status monitoring)
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        }));
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
    /// Helper: Get best LAN IPv4 address, preferring physical NICs over virtual/VPN adapters.
    /// </summary>
    private static string GetLanIpAddress()
    {
        try
        {
            var candidates = new List<(IPAddress Address, int Score)>();

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel
                    or NetworkInterfaceType.Ppp)
                    continue;

                var ipProperties = networkInterface.GetIPProperties();
                var hasIpv4Gateway = ipProperties.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork
                    && !g.Address.Equals(IPAddress.Any)
                    && !g.Address.Equals(IPAddress.None));

                foreach (var unicastAddress in ipProperties.UnicastAddresses)
                {
                    var address = unicastAddress.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (IPAddress.IsLoopback(address) || IsApipa(address) || !IsPrivateIpv4(address))
                        continue;

                    var score = 0;

                    if (hasIpv4Gateway)
                        score += 100;

                    score += networkInterface.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Wireless80211 => 80,
                        NetworkInterfaceType.Ethernet => 70,
                        NetworkInterfaceType.GigabitEthernet => 70,
                        NetworkInterfaceType.FastEthernetT => 60,
                        NetworkInterfaceType.FastEthernetFx => 60,
                        _ => 20
                    };

                    if (IsLikelyVirtualInterface(networkInterface))
                        score -= 250;

                    if (IsLikelyVpnInterface(networkInterface))
                        score -= 150;

                    candidates.Add((address, score));
                }
            }

            if (candidates.Count > 0)
            {
                return candidates
                    .OrderByDescending(candidate => candidate.Score)
                    .Select(candidate => candidate.Address.ToString())
                    .First();
            }

            // Fallback to hostname enumeration if interface scoring didn't yield a candidate.
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var fallback = host.AddressList.FirstOrDefault(ip =>
                ip.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(ip)
                && !IsApipa(ip)
                && IsPrivateIpv4(ip));

            if (fallback != null)
                return fallback.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get LAN IP: {ex.Message}");
        }
        return "127.0.0.1";
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
            return false;

        return bytes[0] == 10
               || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
               || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static bool IsApipa(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    private static bool IsLikelyVirtualInterface(NetworkInterface networkInterface)
    {
        var text = $"{networkInterface.Name} {networkInterface.Description}".ToLowerInvariant();
        var virtualKeywords = new[]
        {
            "virtual",
            "vmware",
            "vbox",
            "hyper-v",
            "vethernet",
            "docker",
            "wsl",
            "loopback",
            "npcap"
        };

        return virtualKeywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));
    }

    private static bool IsLikelyVpnInterface(NetworkInterface networkInterface)
    {
        var text = $"{networkInterface.Name} {networkInterface.Description}".ToLowerInvariant();
        var vpnKeywords = new[]
        {
            "vpn",
            "wireguard",
            "zerotier",
            "hamachi",
            "tailscale",
            "tap",
            "tun"
        };

        return vpnKeywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));
    }
}

/// <summary>
/// Request DTO for password reset
/// </summary>
public class ResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}
