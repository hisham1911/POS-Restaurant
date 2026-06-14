namespace KasserPro.Infrastructure.Services;

using System.Diagnostics;
using System.Threading;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class SystemSeedService : ISystemSeedService
{
    private static readonly SemaphoreSlim SeedExecutionLock = new(1, 1);

    private readonly AppDbContext _context;
    private readonly ILogger<SystemSeedService> _logger;

    public SystemSeedService(AppDbContext context, ILogger<SystemSeedService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureSystemOwnerAsync(CancellationToken cancellationToken = default)
    {
        var hasSystemOwner = await _context.Users
            .AnyAsync(user => user.Role == UserRole.SystemOwner, cancellationToken);

        if (hasSystemOwner)
        {
            return;
        }

        var systemOwnerPassword = SeedSystemOwnerPasswordResolver.ResolveWithSource(out var passwordSource);

        var systemOwner = new User
        {
            TenantId = null,
            BranchId = null,
            Name = "System Owner",
            Email = "owner@kasserpro.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(systemOwnerPassword),
            Role = UserRole.SystemOwner,
            IsActive = true
        };

        _context.Users.Add(systemOwner);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "System Owner created (password source: {PasswordSource})",
            FormatPasswordSource(passwordSource));
    }

    public async Task<ApiResponse<SystemSeedRunResultDto>> RunFullSeedPipelineAsync(CancellationToken cancellationToken = default)
    {
        if (!await SeedExecutionLock.WaitAsync(0, cancellationToken))
        {
            return ApiResponse<SystemSeedRunResultDto>.Fail(
                ErrorCodes.CONFLICT,
                ErrorMessages.Get(ErrorCodes.CONFLICT),
                new List<string> { "عملية تحميل البيانات التجريبية قيد التنفيذ حالياً" });
        }

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var optionalWarnings = new List<string>();
        var inventorySynchronizationTriggered = false;

        try
        {
            await EnsureSystemOwnerAsync(cancellationToken);

            await ButcherDataSeeder.SeedAsync(_context);
            _context.ChangeTracker.Clear();

            await MultiTenantSeeder.SeedAsync(_context);
            _context.ChangeTracker.Clear();

            // Optional seeds should not fail the entire pipeline.
            try
            {
                await RealisticDataSeeder.SeedAsync(_context);
            }
            catch (Exception ex)
            {
                optionalWarnings.Add("تعذر تحميل البيانات الواقعية، تم الاستمرار بباقي الخطوات");
                _logger.LogWarning(ex, "Realistic data seeding failed; pipeline will continue");
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }

            try
            {
                await SeedCatalogIconSynchronizer.SynchronizeAsync(_context);
            }
            catch (Exception ex)
            {
                optionalWarnings.Add("تعذر مزامنة أيقونات البيانات التجريبية");
                _logger.LogWarning(ex, "Seed icon synchronization failed after seeding");
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }

            if (await ShouldSynchronizeSeedInventoryAsync(cancellationToken))
            {
                try
                {
                    await SeedInventorySynchronizer.SynchronizeAsync(_context);
                    inventorySynchronizationTriggered = true;
                }
                catch (Exception ex)
                {
                    optionalWarnings.Add("تعذر مزامنة المخزون لبعض بيانات السيدر");
                    _logger.LogWarning(ex, "Branch inventory synchronization failed after seeding");
                }
                finally
                {
                    _context.ChangeTracker.Clear();
                }
            }

            try
            {
                await MultiTenantSeeder.CloseOpenShiftsForTargetTenantsAsync(_context);
            }
            catch (Exception ex)
            {
                optionalWarnings.Add("تعذر إغلاق بعض الورديات المفتوحة بعد تحميل السيدر");
                _logger.LogWarning(ex, "Seed shift close synchronization failed after seeding");
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }

            var seededTenantSlugs = await _context.Tenants
                .AsNoTracking()
                .Where(tenant => SeedTenantRegistry.Slugs.Contains(tenant.Slug))
                .OrderBy(tenant => tenant.Slug)
                .Select(tenant => tenant.Slug)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            var result = new SystemSeedRunResultDto
            {
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTime.UtcNow,
                DurationMs = stopwatch.ElapsedMilliseconds,
                InventorySynchronizationTriggered = inventorySynchronizationTriggered,
                PreservedExistingData = true,
                SeededTenantSlugs = seededTenantSlugs,
                OptionalWarnings = optionalWarnings
            };

            var message = optionalWarnings.Count == 0
                ? "تم تشغيل السيدر بنجاح"
                : "تم تشغيل السيدر مع بعض التحذيرات";

            return ApiResponse<SystemSeedRunResultDto>.Ok(result, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seed pipeline failed");

            return ApiResponse<SystemSeedRunResultDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR),
                new List<string> { ex.Message });
        }
        finally
        {
            SeedExecutionLock.Release();
        }
    }

    public async Task<ApiResponse<SystemSeedRunResultDto>> SeedRestaurantDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!await SeedExecutionLock.WaitAsync(0, cancellationToken))
        {
            return ApiResponse<SystemSeedRunResultDto>.Fail(
                ErrorCodes.CONFLICT,
                ErrorMessages.Get(ErrorCodes.CONFLICT),
                new List<string> { "عملية تحميل بيانات العرض قيد التنفيذ حالياً" });
        }

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await RestaurantDemoSeeder.SeedAmSalamaAsync(_context, cancellationToken);
            _context.ChangeTracker.Clear();

            stopwatch.Stop();

            var result = new SystemSeedRunResultDto
            {
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTime.UtcNow,
                DurationMs = stopwatch.ElapsedMilliseconds,
                InventorySynchronizationTriggered = false,
                PreservedExistingData = true,
                SeededTenantSlugs = new List<string> { RestaurantDemoSeeder.TenantSlug }
            };

            return ApiResponse<SystemSeedRunResultDto>.Ok(result, "تم تجهيز حساب مطعم عم سلامة التجريبي بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restaurant demo seed failed");

            return ApiResponse<SystemSeedRunResultDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR),
                new List<string> { ex.Message });
        }
        finally
        {
            SeedExecutionLock.Release();
        }
    }

    private async Task<bool> ShouldSynchronizeSeedInventoryAsync(CancellationToken cancellationToken)
    {
        var seedTenantIds = await _context.Tenants
            .AsNoTracking()
            .Where(tenant => SeedTenantRegistry.Slugs.Contains(tenant.Slug))
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);

        foreach (var seedTenantId in seedTenantIds)
        {
            var expectedInventoryRows = await (
                from product in _context.Products.AsNoTracking()
                join branch in _context.Branches.AsNoTracking() on product.TenantId equals branch.TenantId
                where product.TenantId == seedTenantId
                    && product.IsActive
                    && product.TrackInventory
                    && branch.IsActive
                select 1
            ).CountAsync(cancellationToken);

            if (expectedInventoryRows == 0)
            {
                continue;
            }

            var existingInventoryRows = await _context.BranchInventories
                .AsNoTracking()
                .CountAsync(inventory => inventory.TenantId == seedTenantId, cancellationToken);

            var hasPositiveInventory = await _context.BranchInventories
                .AsNoTracking()
                .AnyAsync(inventory => inventory.TenantId == seedTenantId && inventory.Quantity > 0, cancellationToken);

            if (existingInventoryRows < expectedInventoryRows || !hasPositiveInventory)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatPasswordSource(SeedSystemOwnerPasswordSource source)
    {
        return source switch
        {
            SeedSystemOwnerPasswordSource.Environment => $"env {SeedSystemOwnerPasswordResolver.EnvironmentVariableName}",
            _ => "fixed default"
        };
    }
}
