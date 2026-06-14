namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class SystemSeedServiceTests
{
    [Fact]
    public async Task EnsureSystemOwnerAsync_OnFreshDatabaseWithAuditInterceptor_CreatesOwner()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var auditInterceptor = new AuditSaveChangesInterceptor(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(auditInterceptor)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new SystemSeedService(context, NullLogger<SystemSeedService>.Instance);

        var act = async () => await service.EnsureSystemOwnerAsync();

        await act.Should().NotThrowAsync();

        var owner = await context.Users.SingleAsync(user => user.Email == "owner@kasserpro.com");
        owner.Role.Should().Be(UserRole.SystemOwner);
        owner.TenantId.Should().BeNull();
        owner.BranchId.Should().BeNull();
        owner.IsActive.Should().BeTrue();

        var auditLogCount = await context.AuditLogs.CountAsync();
        auditLogCount.Should().Be(0);
    }

    [Fact]
    public async Task SeedRestaurantDemoAsync_CreatesAmSalamaWithServiceProducts()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var auditInterceptor = new AuditSaveChangesInterceptor(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(auditInterceptor)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new SystemSeedService(context, NullLogger<SystemSeedService>.Instance);

        var result = await service.SeedRestaurantDemoAsync();

        result.Success.Should().BeTrue();
        result.Data!.SeededTenantSlugs.Should().Contain(RestaurantDemoSeeder.TenantSlug);

        var tenant = await context.Tenants.SingleAsync(t => t.Slug == RestaurantDemoSeeder.TenantSlug);
        tenant.Name.Should().Be("مطعم عم سلامة");
        tenant.IsTaxEnabled.Should().BeFalse();

        var branch = await context.Branches.SingleAsync(b => b.TenantId == tenant.Id);
        branch.Name.Should().Be("الفرع الرئيسي");

        var admin = await context.Users.SingleAsync(u => u.Email == RestaurantDemoSeeder.DemoEmail);
        admin.TenantId.Should().Be(tenant.Id);
        admin.BranchId.Should().Be(branch.Id);
        admin.Role.Should().Be(UserRole.Admin);
        BCrypt.Net.BCrypt.Verify(RestaurantDemoSeeder.DemoPassword, admin.PasswordHash).Should().BeTrue();

        var products = await context.Products
            .Where(p => p.TenantId == tenant.Id)
            .ToListAsync();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.Type == ProductType.Service);
        products.Should().OnlyContain(p => !p.TrackInventory);
        products.Should().OnlyContain(p => !p.IsBatchTracked);

        var inventoryCount = await context.BranchInventories.CountAsync(i => i.TenantId == tenant.Id);
        inventoryCount.Should().Be(0);
    }
}
