namespace KasserPro.Application.Services.Implementations;

using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Tenants;
using KasserPro.Application.DTOs.System;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

public class TenantService : ITenantService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TenantService> _logger;

    public TenantService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILogger<TenantService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ApiResponse<TenantDto>> GetCurrentTenantAsync()
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentUser.TenantId);
        if (tenant == null)
            return ApiResponse<TenantDto>.Fail("الشركة غير موجودة");

        return ApiResponse<TenantDto>.Ok(MapToDto(tenant));
    }

    public async Task<ApiResponse<TenantDto>> UpdateCurrentTenantAsync(UpdateTenantDto dto)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentUser.TenantId);
        if (tenant == null)
            return ApiResponse<TenantDto>.Fail("الشركة غير موجودة");

        // Update basic info
        tenant.Name = dto.Name;
        tenant.NameEn = dto.NameEn;
        tenant.LogoUrl = dto.LogoUrl;
        tenant.Currency = dto.Currency;
        tenant.Timezone = dto.Timezone;

        // Update tax settings if provided
        if (dto.TaxRate.HasValue)
        {
            // Validate tax rate (0-100)
            if (dto.TaxRate.Value < 0 || dto.TaxRate.Value > 100)
                return ApiResponse<TenantDto>.Fail("نسبة الضريبة يجب أن تكون بين 0 و 100");

            tenant.TaxRate = dto.TaxRate.Value;
        }

        if (dto.IsTaxEnabled.HasValue)
        {
            tenant.IsTaxEnabled = dto.IsTaxEnabled.Value;
        }

        // Update inventory settings if provided
        if (dto.AllowNegativeStock.HasValue)
        {
            tenant.AllowNegativeStock = dto.AllowNegativeStock.Value;
        }

        // Update receipt settings if provided
        if (dto.ReceiptPaperSize != null)
            tenant.ReceiptPaperSize = dto.ReceiptPaperSize;
        if (dto.ReceiptCustomWidth.HasValue)
            tenant.ReceiptCustomWidth = dto.ReceiptCustomWidth.Value;
        if (dto.ReceiptHeaderFontSize.HasValue)
            tenant.ReceiptHeaderFontSize = dto.ReceiptHeaderFontSize.Value;
        if (dto.ReceiptBodyFontSize.HasValue)
            tenant.ReceiptBodyFontSize = dto.ReceiptBodyFontSize.Value;
        if (dto.ReceiptTotalFontSize.HasValue)
            tenant.ReceiptTotalFontSize = dto.ReceiptTotalFontSize.Value;
        if (dto.ReceiptShowBranchName.HasValue)
            tenant.ReceiptShowBranchName = dto.ReceiptShowBranchName.Value;
        if (dto.ReceiptShowCashier.HasValue)
            tenant.ReceiptShowCashier = dto.ReceiptShowCashier.Value;
        if (dto.ReceiptShowThankYou.HasValue)
            tenant.ReceiptShowThankYou = dto.ReceiptShowThankYou.Value;
        if (dto.ReceiptFooterMessage != null)
            tenant.ReceiptFooterMessage = dto.ReceiptFooterMessage;
        if (dto.ReceiptPhoneNumber != null)
            tenant.ReceiptPhoneNumber = dto.ReceiptPhoneNumber;
        if (dto.ReceiptShowCustomerName.HasValue)
            tenant.ReceiptShowCustomerName = dto.ReceiptShowCustomerName.Value;
        if (dto.ReceiptShowLogo.HasValue)
            tenant.ReceiptShowLogo = dto.ReceiptShowLogo.Value;

        _unitOfWork.Tenants.Update(tenant);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<TenantDto>.Ok(MapToDto(tenant), "تم تحديث بيانات الشركة بنجاح");
    }

    public async Task<ApiResponse<CreateTenantResponse>> CreateTenantWithAdminAsync(CreateTenantRequest request)
    {
        var normalizedTenantName = request.TenantName.Trim();
        var normalizedBranchName = request.BranchName.Trim();
        var normalizedAdminEmail = request.AdminEmail.Trim().ToLowerInvariant();

        _logger.LogInformation(
            "SystemOwner tenant creation attempt started by UserId={UserId} for TenantName={TenantName}, AdminEmail={AdminEmail}",
            _currentUser.UserId,
            normalizedTenantName,
            normalizedAdminEmail);

        if (!IsStrongPassword(request.AdminPassword))
        {
            _logger.LogWarning(
                "SystemOwner tenant creation rejected due to weak password policy for AdminEmail={AdminEmail}",
                normalizedAdminEmail);
            return ApiResponse<CreateTenantResponse>.Fail("كلمة المرور لا تحقق متطلبات الأمان");
        }

        // Validate tenant name uniqueness
        var existingTenantByName = await _unitOfWork.Tenants.Query()
            .FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedTenantName.ToLower());

        if (existingTenantByName != null)
        {
            _logger.LogWarning(
                "SystemOwner tenant creation rejected because tenant name already exists. TenantName={TenantName}",
                normalizedTenantName);
            return ApiResponse<CreateTenantResponse>.Fail("اسم الشركة مستخدم بالفعل");
        }

        // Validate email uniqueness
        var existingUser = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedAdminEmail);

        if (existingUser != null)
        {
            _logger.LogWarning(
                "SystemOwner tenant creation rejected because admin email already exists. AdminEmail={AdminEmail}",
                normalizedAdminEmail);
            return ApiResponse<CreateTenantResponse>.Fail("البريد الإلكتروني مستخدم بالفعل");
        }

        // Generate unique slug from tenant name
        var slug = GenerateSlug(normalizedTenantName);
        var existingTenant = await _unitOfWork.Tenants.Query()
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (existingTenant != null)
        {
            // Add random suffix if slug exists
            slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";
        }

        // Begin transaction
        var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // 1. Create Tenant
            var tenant = new Tenant
            {
                Name = normalizedTenantName,
                Slug = slug,
                IsActive = true,
                TaxRate = 14.0m,
                IsTaxEnabled = true,
                AllowNegativeStock = false,
                Currency = "EGP",
                Timezone = "Africa/Cairo"
            };

            await _unitOfWork.Tenants.AddAsync(tenant);
            await _unitOfWork.SaveChangesAsync(); // Flush to get DB-generated tenant.Id (transaction still open)

            // 2. Create Branch
            var branch = new Branch
            {
                TenantId = tenant.Id,
                Name = normalizedBranchName,
                Code = "MAIN",
                IsActive = true,
                DefaultTaxRate = 14,
                DefaultTaxInclusive = true,
                CurrencyCode = "EGP",
                IsWarehouse = false
            };

            await _unitOfWork.Branches.AddAsync(branch);
            await _unitOfWork.SaveChangesAsync();

            // 3. Create Admin User
            var adminUser = new User
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "مدير النظام",
                Email = normalizedAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                Role = UserRole.Admin,
                IsActive = true
            };

            await _unitOfWork.Users.AddAsync(adminUser);
            await _unitOfWork.SaveChangesAsync();

            // 4) Log audit entry in same transaction
            var auditLog = new AuditLog
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                UserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
                UserName = _currentUser.Email,
                Action = "SystemOwner.CreateTenant",
                EntityType = "Tenant",
                EntityId = tenant.Id,
                NewValues = JsonSerializer.Serialize(new
                {
                    tenantId = tenant.Id,
                    tenantName = tenant.Name,
                    branchId = branch.Id,
                    branchName = branch.Name,
                    adminUserId = adminUser.Id,
                    adminEmail = adminUser.Email
                })
            };

            await _unitOfWork.AuditLogs.AddAsync(auditLog);
            await _unitOfWork.SaveChangesAsync(); // Final flush before commit

            // Commit via UnitOfWork - ensures ghost-reference nullification
            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation(
                "SystemOwner tenant creation succeeded for TenantId={TenantId}, AdminUserId={AdminUserId}",
                tenant.Id,
                adminUser.Id);

            return ApiResponse<CreateTenantResponse>.Ok(new CreateTenantResponse
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                TenantSlug = tenant.Slug,
                BranchId = branch.Id,
                BranchName = branch.Name,
                AdminUserId = adminUser.Id,
                AdminEmail = adminUser.Email
            }, "تم إنشاء الشركة بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex,
                "SystemOwner tenant creation failed and rolled back for TenantName={TenantName}, AdminEmail={AdminEmail}",
                normalizedTenantName,
                normalizedAdminEmail);
            return ApiResponse<CreateTenantResponse>.Fail("فشل في إنشاء الشركة. الرجاء المحاولة مرة أخرى");
        }
    }

    public async Task<ApiResponse<List<SystemTenantSummaryDto>>> GetAllTenantsForSystemOwnerAsync()
    {
        var tenants = await _unitOfWork.Tenants.Query()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new SystemTenantSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                IsActive = t.IsActive,
                BranchesCount = t.Branches.Count(),
                ActiveBranchesCount = t.Branches.Count(b => b.IsActive),
                UsersCount = t.Users.Count(),
                ActiveUsersCount = t.Users.Count(u => u.IsActive),
                InactiveUsersCount = t.Users.Count(u => !u.IsActive),
                AdminsCount = t.Users.Count(u => u.Role == UserRole.Admin),
                CashiersCount = t.Users.Count(u => u.Role == UserRole.Cashier),
                Currency = t.Currency,
                Timezone = t.Timezone,
                TaxRate = t.TaxRate,
                IsTaxEnabled = t.IsTaxEnabled,
                AllowNegativeStock = t.AllowNegativeStock,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return ApiResponse<List<SystemTenantSummaryDto>>.Ok(tenants);
    }

    public async Task<ApiResponse<bool>> SetTenantActiveStatusAsync(int tenantId, bool isActive)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
        if (tenant == null)
            return ApiResponse<bool>.Fail("الشركة غير موجودة");

        if (tenant.IsActive == isActive)
            return ApiResponse<bool>.Ok(true, isActive ? "الشركة مفعلة بالفعل" : "الشركة معطلة بالفعل");

        tenant.IsActive = isActive;
        _unitOfWork.Tenants.Update(tenant);

        var auditLog = new AuditLog
        {
            TenantId = tenant.Id,
            BranchId = null,
            UserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            UserName = _currentUser.Email,
            Action = "SystemOwner.UpdateTenantStatus",
            EntityType = "Tenant",
            EntityId = tenant.Id,
            NewValues = JsonSerializer.Serialize(new
            {
                tenantId = tenant.Id,
                tenantName = tenant.Name,
                isActive = tenant.IsActive
            })
        };

        await _unitOfWork.AuditLogs.AddAsync(auditLog);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, isActive ? "تم تفعيل الشركة" : "تم تعطيل الشركة");
    }

    private static bool IsStrongPassword(string password)
        => Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,100}$");

    private static string GenerateSlug(string name)
    {
        // Simple slug generation: lowercase, replace spaces with hyphens
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
    }

    private static TenantDto MapToDto(Domain.Entities.Tenant tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        NameEn = tenant.NameEn,
        Slug = tenant.Slug,
        LogoUrl = tenant.LogoUrl,
        Currency = tenant.Currency,
        Timezone = tenant.Timezone,
        IsActive = tenant.IsActive,
        TaxRate = tenant.TaxRate,
        IsTaxEnabled = tenant.IsTaxEnabled,
        AllowNegativeStock = tenant.AllowNegativeStock,
        ReceiptPaperSize = tenant.ReceiptPaperSize,
        ReceiptCustomWidth = tenant.ReceiptCustomWidth,
        ReceiptHeaderFontSize = tenant.ReceiptHeaderFontSize,
        ReceiptBodyFontSize = tenant.ReceiptBodyFontSize,
        ReceiptTotalFontSize = tenant.ReceiptTotalFontSize,
        ReceiptShowBranchName = tenant.ReceiptShowBranchName,
        ReceiptShowCashier = tenant.ReceiptShowCashier,
        ReceiptShowThankYou = tenant.ReceiptShowThankYou,
        ReceiptFooterMessage = tenant.ReceiptFooterMessage,
        ReceiptPhoneNumber = tenant.ReceiptPhoneNumber,
        ReceiptShowCustomerName = tenant.ReceiptShowCustomerName,
        ReceiptShowLogo = tenant.ReceiptShowLogo,
        CreatedAt = tenant.CreatedAt
    };
}
