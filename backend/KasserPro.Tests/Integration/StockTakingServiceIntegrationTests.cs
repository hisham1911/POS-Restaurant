namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class StockTakingServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StockTakingServiceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLatestCompletedAsync_ShouldCalculateDecimalDifferenceWithoutSqliteAggregateFailure()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedStockTakingContextAsync(db, "latest-completed");
        var service = CreateService(db, data.Tenant.Id, data.Branch.Id, data.User.Id);

        var stockTaking = new StockTaking
        {
            TenantId = data.Tenant.Id,
            BranchId = data.Branch.Id,
            StockTakingNumber = $"ST-{Guid.NewGuid():N}",
            Type = StockTakingType.Full,
            Status = StockTakingStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            CreatedByUserId = data.User.Id,
            CompletedByUserId = data.User.Id
        };
        db.StockTakings.Add(stockTaking);
        await db.SaveChangesAsync();

        db.StockTakingItems.AddRange(
            new StockTakingItem
            {
                StockTakingId = stockTaking.Id,
                ProductId = data.Product.Id,
                SystemQuantity = 10.25m,
                ActualQuantity = 12.75m
            },
            new StockTakingItem
            {
                StockTakingId = stockTaking.Id,
                ProductId = data.Product.Id,
                SystemQuantity = 3.5m,
                ActualQuantity = 2.25m,
                BatchId = null
            });
        await db.SaveChangesAsync();

        var result = await service.GetLatestCompletedAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(stockTaking.Id);
        result.ItemCount.Should().Be(2);
        result.TotalDifference.Should().Be(1.25m);
        result.CreatedByUserName.Should().Be(data.User.Name);
        result.CompletedByUserName.Should().Be(data.User.Name);
    }

    private static StockTakingService CreateService(AppDbContext db, int tenantId, int branchId, int userId)
        => new(
            db,
            new TestCurrentUserService
            {
                TenantId = tenantId,
                BranchId = branchId,
                UserId = userId,
                Email = "stock-taking-tests@test.com"
            });

    private static async Task<StockTakingSeedData> SeedStockTakingContextAsync(AppDbContext db, string slug)
    {
        var tenant = new Tenant
        {
            Name = $"Stock Taking Tenant {slug}",
            Slug = $"{slug}-{Guid.NewGuid():N}",
            IsActive = true,
            IsTaxEnabled = true,
            TaxRate = 14m
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = $"Stock Taking Branch {slug}",
            Code = $"ST-{Guid.NewGuid().ToString("N")[..4]}",
            CurrencyCode = "EGP",
            DefaultTaxRate = 14m,
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Stock Taking Admin",
            Email = $"stock-taking-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        var category = new Category
        {
            TenantId = tenant.Id,
            Name = $"Stock Taking Category {slug}",
            IsActive = true
        };
        db.Users.Add(user);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = $"Flour {slug}",
            Price = 0m,
            Cost = 10m,
            Type = ProductType.RawMaterial,
            Unit = UnitOfMeasure.Kilogram,
            TrackInventory = true,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return new StockTakingSeedData(tenant, branch, user, product);
    }

    private sealed record StockTakingSeedData(
        Tenant Tenant,
        Branch Branch,
        User User,
        Product Product);
}
