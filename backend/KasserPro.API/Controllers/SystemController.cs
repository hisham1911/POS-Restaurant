namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using KasserPro.Application.DTOs.System;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Infrastructure.Data;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/system")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly InventoryDataMigration _inventoryMigration;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        ITenantService tenantService,
        InventoryDataMigration inventoryMigration,
        ILogger<SystemController> logger)
    {
        _tenantService = tenantService;
        _inventoryMigration = inventoryMigration;
        _logger = logger;
    }

    /// <summary>
    /// Create a new tenant with admin user and default branch (SystemOwner only)
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
                    lanIp = lanIp,
                    hostname = hostname,
                    port = 5243,
                    url = $"http://{lanIp}:5243",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    timestamp = DateTime.UtcNow,
                    isOffline = false // Will be set by frontend based on API connectivity
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system info");
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to retrieve system information"
            });
        }
    }

    /// <summary>
    /// Health check endpoint (for network status monitoring)
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new
        {
            success = true,
            status = "healthy",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Migrate Product.StockQuantity to BranchInventory (Admin only)
    /// This is a one-time migration to fix products missing from inventory
    /// </summary>
    [HttpPost("migrate-inventory")]
    [Authorize(Roles = "Admin,SystemOwner")]
    public async Task<IActionResult> MigrateInventory()
    {
        try
        {
            _logger.LogInformation("Starting inventory migration...");
            var result = await _inventoryMigration.ExecuteAsync();
            
            if (result.Success)
            {
                _logger.LogInformation("Inventory migration completed successfully");
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    summary = result.GetSummary(),
                    data = new
                    {
                        productsMigrated = result.ProductsMigrated,
                        inventoriesCreated = result.InventoriesCreated,
                        productsWithStock = result.ProductsWithStock,
                        totalStockBefore = result.TotalStockBefore,
                        totalStockAfter = result.TotalStockAfter,
                        durationMs = result.DurationMs,
                        alreadyMigrated = result.AlreadyMigrated
                    }
                });
            }
            else
            {
                _logger.LogError("Inventory migration failed: {Message}", result.Message);
                return BadRequest(new
                {
                    success = false,
                    message = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inventory migration");
            return StatusCode(500, new
            {
                success = false,
                message = $"Migration failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get all users with their credentials (SystemOwner only - for demo purposes)
    /// WARNING: This endpoint exposes plain passwords and should only be used in demo/development
    /// </summary>
    [HttpGet("credentials")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> GetAllCredentials()
    {
        try
        {
            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            
            var users = await db.Users
                .Include(u => u.Tenant)
                .Include(u => u.Branch)
                .Where(u => u.IsActive)
                .OrderBy(u => u.TenantId)
                .ThenBy(u => u.Role)
                .ToListAsync();

            var userList = users.Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                Role = u.Role.ToString(),
                TenantId = u.TenantId,
                TenantName = u.Tenant != null ? u.Tenant.Name : "System",
                BranchId = u.BranchId,
                BranchName = u.Branch != null ? u.Branch.Name : null,
                // For demo purposes, we'll show the known passwords
                Password = GetDemoPassword(u.Email, u.Role),
                u.IsActive,
                u.CreatedAt
            }).ToList();

            // Group by tenant for better organization
            var groupedByTenant = userList
                .GroupBy(u => new { u.TenantId, u.TenantName })
                .Select(g => new
                {
                    TenantId = g.Key.TenantId,
                    TenantName = g.Key.TenantName,
                    Users = g.ToList()
                })
                .ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalUsers = userList.Count,
                    tenants = groupedByTenant
                },
                message = "⚠️ WARNING: This endpoint is for demo purposes only. Never expose passwords in production!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving credentials");
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to retrieve credentials"
            });
        }
    }

    /// <summary>
    /// Get all users across all tenants (SystemOwner only)
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            _logger.LogInformation("GetAllUsers called");
            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            _logger.LogInformation("Querying users from database");
            var users = await db.Users
                .Include(u => u.Tenant)
                .Include(u => u.Branch)
                .OrderBy(u => u.TenantId)
                .ThenBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();

            _logger.LogInformation("Found {Count} users in database", users.Count);

            var userList = users.Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Phone,
                Role = u.Role.ToString(),
                TenantId = u.TenantId,
                TenantName = u.Tenant != null ? u.Tenant.Name : "System",
                BranchId = u.BranchId,
                BranchName = u.Branch != null ? u.Branch.Name : null,
                u.IsActive,
                u.CreatedAt,
                u.UpdatedAt
            }).ToList();

            _logger.LogInformation("Returning {Count} users", userList.Count);

            return Ok(new
            {
                success = true,
                data = userList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to retrieve users"
            });
        }
    }

    /// <summary>
    /// Update user information (SystemOwner only)
    /// </summary>
    [HttpPut("users/{userId:int}")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateSystemUserRequest request)
    {
        try
        {
            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found"
                });
            }

            // Update user fields
            if (!string.IsNullOrWhiteSpace(request.Name))
                user.Name = request.Name;

            if (!string.IsNullOrWhiteSpace(request.Email))
                user.Email = request.Email;

            if (!string.IsNullOrWhiteSpace(request.Phone))
                user.Phone = request.Phone;

            if (request.IsActive.HasValue)
                user.IsActive = request.IsActive.Value;

            user.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "User updated successfully",
                data = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.Phone,
                    user.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to update user"
            });
        }
    }

    /// <summary>
    /// Toggle user active status (SystemOwner only)
    /// </summary>
    [HttpPatch("users/{userId:int}/toggle-status")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> ToggleUserStatus(int userId)
    {
        try
        {
            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found"
                });
            }

            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"User {(user.IsActive ? "activated" : "deactivated")} successfully",
                data = new
                {
                    user.Id,
                    user.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling user status {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to toggle user status"
            });
        }
    }

    /// <summary>
    /// Reset user password (SystemOwner only)
    /// </summary>
    [HttpPost("users/{userId:int}/reset-password")]
    [Authorize(Roles = "SystemOwner")]
    public async Task<IActionResult> ResetUserPassword(int userId, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found"
                });
            }

            // Hash the new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Password reset successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to reset password"
            });
        }
    }


    /// <summary>
    /// Helper method to return known demo passwords
    /// </summary>
    private static string GetDemoPassword(string email, Domain.Enums.UserRole role)
    {
        // Return known passwords for demo accounts
        return email switch
        {
            // System Owner
            "owner@kasserpro.com" => "Owner@123",
            
            // Tenant 1: مجزر الأمانة
            "admin@kasserpro.com" => "Admin@123",
            "mohamed@kasserpro.com" => "123456",
            "ali@kasserpro.com" => "123456",
            
            // Tenant 2: محل أدوات منزلية
            "samy@homeappliances.com" => "Admin@123",
            "nour@homeappliances.com" => "123456",
            "hoda@homeappliances.com" => "123456",
            
            // Tenant 3: سوبر ماركت
            "karim@supermarket.com" => "Admin@123",
            "fatma@supermarket.com" => "123456",
            "zainab@supermarket.com" => "123456",
            "mariam@supermarket.com" => "123456",
            
            // Tenant 4: مطعم
            "tarek@restaurant.com" => "Admin@123",
            "omar@restaurant.com" => "123456",
            "youssef@restaurant.com" => "123456",
            
            // Default
            _ => role == Domain.Enums.UserRole.Admin ? "Admin@123" : "123456"
        };
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
                {
                    return ip.ToString();
                }
            }
        }
        catch { }
        
        return "127.0.0.1";
    }
}

// Request DTOs
public class UpdateSystemUserRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
