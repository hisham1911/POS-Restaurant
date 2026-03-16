namespace KasserPro.Infrastructure.Data;

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using KasserPro.Domain.Common;
using KasserPro.Domain.Entities;

/// <summary>
/// Interceptor that handles audit logging and tenant/branch context enforcement.
/// 
/// Security Features:
/// 1. Automatically injects TenantId/BranchId for entities that support it
/// 2. Throws UnauthorizedAccessException if user context is invalid
/// 3. Excludes sensitive properties from audit logs
/// 4. Captures user info and IP address for audit trail
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly List<AuditEntry> _auditEntries = new();

    // Entities to audit
    private static readonly HashSet<string> AuditedEntities = new()
    {
        nameof(Order),
        nameof(Product),
        nameof(Category),
        nameof(User),
        nameof(Branch),
        nameof(Shift),
        nameof(Payment)
    };

    // Sensitive properties that should NEVER be logged
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "Password",
        "PinCode",
        "Pin",
        "Secret",
        "Token",
        "RefreshToken",
        "ApiKey",
        "PrivateKey",
        "CreditCardNumber",
        "CVV",
        "SecurityCode"
    };

    // Entities that require TenantId enforcement
    private static readonly HashSet<string> TenantScopedEntities = new()
    {
        nameof(Order),
        nameof(Product),
        nameof(Category),
        nameof(User),
        nameof(Branch),
        nameof(Shift),
        nameof(Payment)
    };

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) 
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        // Get current user context
        var (userId, userName, tenantId, branchId, isAuthenticated) = GetCurrentUserContext();

        _auditEntries.Clear();
        var entries = eventData.Context.ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && 
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            
            // SECURITY: Enforce TenantId/BranchId for tenant-scoped entities
            if (TenantScopedEntities.Contains(entityName) && entry.State == EntityState.Added)
            {
                EnforceTenantContext(entry, tenantId, branchId, isAuthenticated);
            }

            // Only audit specific entities
            if (!AuditedEntities.Contains(entityName))
                continue;

            var auditEntry = new AuditEntry
            {
                EntityType = entityName,
                Action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => "Unknown"
                },
                Entity = entry.Entity as BaseEntity
            };

            if (entry.Entity is BaseEntity baseEntity)
            {
                auditEntry.EntityId = baseEntity.Id > 0 ? baseEntity.Id : null;
                
                // Get TenantId if exists
                var tenantIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TenantId");
                if (tenantIdProp?.CurrentValue is int tid)
                    auditEntry.TenantId = tid;

                // Get BranchId if exists
                var branchIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "BranchId");
                if (branchIdProp?.CurrentValue is int bid)
                    auditEntry.BranchId = bid;
            }

            // Capture old and new values (excluding sensitive data)
            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey()) continue;
                
                var propertyName = property.Metadata.Name;
                
                // SECURITY: Skip sensitive properties
                if (IsSensitiveProperty(propertyName)) continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[propertyName] = SanitizeValue(property.CurrentValue);
                        break;
                    case EntityState.Deleted:
                        oldValues[propertyName] = SanitizeValue(property.OriginalValue);
                        break;
                    case EntityState.Modified when property.IsModified:
                        oldValues[propertyName] = SanitizeValue(property.OriginalValue);
                        newValues[propertyName] = SanitizeValue(property.CurrentValue);
                        break;
                }
            }

            if (oldValues.Count > 0)
                auditEntry.OldValues = JsonSerializer.Serialize(oldValues);
            if (newValues.Count > 0)
                auditEntry.NewValues = JsonSerializer.Serialize(newValues);

            _auditEntries.Add(auditEntry);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Enforces TenantId and BranchId for new entities.
    /// Throws UnauthorizedAccessException if context is invalid.
    /// </summary>
    private static void EnforceTenantContext(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, 
        int tenantId, 
        int branchId, 
        bool isAuthenticated)
    {
        var tenantIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TenantId");
        var branchIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "BranchId");

        // Check if entity has TenantId property
        if (tenantIdProp != null)
        {
            var currentTenantId = tenantIdProp.CurrentValue as int? ?? 0;
            
            // Special case: Allow System Owner (User with Role = SystemOwner) to have null TenantId
            if (entry.Entity is Domain.Entities.User user && user.Role == Domain.Enums.UserRole.SystemOwner)
            {
                // System Owner can have null TenantId - skip enforcement
                return;
            }
            
            // If TenantId is 0 or not set, we need valid context
            if (currentTenantId == 0)
            {
                // SECURITY: Require authenticated user with valid TenantId
                if (!isAuthenticated || tenantId <= 0)
                {
                    throw new UnauthorizedAccessException(
                        $"Cannot save {entry.Entity.GetType().Name} without valid TenantId. " +
                        "User must be authenticated with a valid tenant context.");
                }
                
                // Auto-inject TenantId from current user context
                tenantIdProp.CurrentValue = tenantId;
            }
        }

        // Check if entity has BranchId property (optional for some entities)
        if (branchIdProp != null)
        {
            var currentBranchId = branchIdProp.CurrentValue as int? ?? 0;
            
            // If BranchId is 0 and we have a valid branch context, inject it
            if (currentBranchId == 0 && branchId > 0)
            {
                branchIdProp.CurrentValue = branchId;
            }
        }
    }

    /// <summary>
    /// Check if a property name is sensitive and should be excluded from logs
    /// </summary>
    private static bool IsSensitiveProperty(string propertyName)
    {
        return SensitiveProperties.Any(s => 
            propertyName.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sanitize values before logging (truncate long strings, etc.)
    /// </summary>
    private static object? SanitizeValue(object? value)
    {
        if (value is string str && str.Length > 500)
            return str[..500] + "...[truncated]";
        
        return value;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext context && _auditEntries.Count > 0)
        {
            var (userId, userName, tenantId, _, _) = GetCurrentUserContext();
            var ipAddress = GetClientIpAddress();

            foreach (var auditEntry in _auditEntries)
            {
                var entityId = auditEntry.EntityId ?? auditEntry.Entity?.Id ?? 0;

                var auditLog = new AuditLog
                {
                    // Use entity's TenantId if available, otherwise use current user's
                    TenantId = auditEntry.TenantId ?? tenantId,
                    BranchId = auditEntry.BranchId,
                    UserId = userId > 0 ? userId : null,
                    UserName = userName,
                    Action = auditEntry.Action,
                    EntityType = auditEntry.EntityType,
                    EntityId = entityId,
                    OldValues = auditEntry.OldValues,
                    NewValues = auditEntry.NewValues,
                    IpAddress = ipAddress
                };

                context.AuditLogs.Add(auditLog);
            }

            await context.SaveChangesAsync(cancellationToken);
            _auditEntries.Clear();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Extract complete user context from JWT claims and headers
    /// </summary>
    private (int UserId, string? UserName, int TenantId, int BranchId, bool IsAuthenticated) GetCurrentUserContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        // Get UserId
        var userIdClaim = user?.FindFirst("userId") ?? user?.FindFirst(ClaimTypes.NameIdentifier);
        var userId = int.TryParse(userIdClaim?.Value, out var uid) ? uid : 0;

        // Get UserName
        var userName = user?.FindFirst("name")?.Value 
                    ?? user?.FindFirst(ClaimTypes.Name)?.Value
                    ?? user?.FindFirst("userName")?.Value
                    ?? user?.Identity?.Name;

        // Get TenantId from JWT
        var tenantIdClaim = user?.FindFirst("tenantId");
        var tenantId = int.TryParse(tenantIdClaim?.Value, out var tid) ? tid : 1;

        // Get BranchId from header first, then JWT
        var branchId = 1;
        var headerValue = httpContext?.Request.Headers["X-Branch-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerValue) && int.TryParse(headerValue, out var hid))
        {
            branchId = hid;
        }
        else
        {
            var branchIdClaim = user?.FindFirst("branchId");
            if (int.TryParse(branchIdClaim?.Value, out var bid))
                branchId = bid;
        }

        return (userId, userName, tenantId, branchId, isAuthenticated);
    }

    /// <summary>
    /// Extract client IP address from HttpContext
    /// </summary>
    private string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        // Check X-Real-IP header
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private class AuditEntry
    {
        public string EntityType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public int? TenantId { get; set; }
        public int? BranchId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public BaseEntity? Entity { get; set; }
    }
}
