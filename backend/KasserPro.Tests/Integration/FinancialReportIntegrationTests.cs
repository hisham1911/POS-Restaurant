namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Application.Services.Implementations;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Repositories;
using KasserPro.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class FinancialReportIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly TimeZoneInfo EgyptTimeZone = GetEgyptTimeZone();
    private static readonly DateTime FixedUtcNoon = new(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedCairoDate = TimeZoneInfo.ConvertTimeFromUtc(FixedUtcNoon, EgyptTimeZone).Date;

    public FinancialReportIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProfitLossReport_ShouldSubtractRefundsOnMatchingTaxBasis()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (tenant, branch, user) = await SeedTenantBranchAndUserAsync(db, "profit-loss");
        var completedAt = FixedUtcNoon;

        var saleOrder = new Order
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            OrderNumber = "PL-SALE-" + Guid.NewGuid().ToString("N")[..6],
            Status = OrderStatus.Completed,
            OrderType = OrderType.DineIn,
            Subtotal = 100m,
            TaxRate = 14m,
            TaxAmount = 14m,
            Total = 114m,
            AmountPaid = 114m,
            CompletedAt = completedAt
        };
        saleOrder.Items.Add(new OrderItem
        {
            ProductName = "Taxed Item",
            UnitPrice = 100m,
            OriginalPrice = 100m,
            Quantity = 1,
            UnitCost = 60m,
            Subtotal = 100m,
            TaxRate = 14m,
            TaxAmount = 14m,
            Total = 114m
        });

        var returnOrder = new Order
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            OrderNumber = "PL-RET-" + Guid.NewGuid().ToString("N")[..6],
            Status = OrderStatus.Completed,
            OrderType = OrderType.Return,
            Subtotal = 100m,
            TaxRate = 14m,
            TaxAmount = 14m,
            Total = 114m,
            AmountPaid = 114m,
            CompletedAt = completedAt.AddMinutes(1)
        };
        returnOrder.Items.Add(new OrderItem
        {
            ProductName = "Taxed Item",
            UnitPrice = 100m,
            OriginalPrice = 100m,
            Quantity = 1,
            UnitCost = 60m,
            Subtotal = 100m,
            TaxRate = 14m,
            TaxAmount = 14m,
            Total = 114m
        });

        db.Orders.AddRange(saleOrder, returnOrder);
        await db.SaveChangesAsync();

        var service = new FinancialReportService(
            db,
            new TestCurrentUserService { TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id },
            NullLogger<FinancialReportService>.Instance);

        var result = await service.GetProfitLossReportAsync(FixedCairoDate, FixedCairoDate);

        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();
        result.Data!.NetSales.Should().Be(0m);
        result.Data.TotalRevenue.Should().Be(0m);
        result.Data.TotalTax.Should().Be(0m);
        result.Data.RefundsAmount.Should().Be(114m);
    }

    [Fact]
    public async Task CogsReport_ShouldNotFallbackToSellingPrice_WhenCostIsMissing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (tenant, branch, user) = await SeedTenantBranchAndUserAsync(db, "cogs");
        var completedAt = FixedUtcNoon.AddHours(1);

        var category = new Category
        {
            TenantId = tenant.Id,
            Name = "Missing Cost Category",
            IsActive = true
        };

        var product = new Product
        {
            TenantId = tenant.Id,
            Category = category,
            Name = "Missing Cost Product",
            Sku = "MC-" + Guid.NewGuid().ToString("N")[..6],
            Price = 100m,
            Cost = null,
            AverageCost = null,
            TaxRate = 0m,
            TaxInclusive = false,
            IsActive = true,
            TrackInventory = true,
            Type = ProductType.Physical
        };

        var order = new Order
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            OrderNumber = "COGS-" + Guid.NewGuid().ToString("N")[..6],
            Status = OrderStatus.Completed,
            OrderType = OrderType.DineIn,
            Subtotal = 100m,
            TaxRate = 0m,
            TaxAmount = 0m,
            Total = 100m,
            AmountPaid = 100m,
            CompletedAt = completedAt
        };
        order.Items.Add(new OrderItem
        {
            Product = product,
            ProductName = product.Name,
            ProductSku = product.Sku,
            UnitPrice = 100m,
            OriginalPrice = 100m,
            Quantity = 1,
            UnitCost = null,
            Subtotal = 100m,
            TaxRate = 0m,
            TaxAmount = 0m,
            Total = 100m
        });

        db.Orders.Add(order);
        db.BranchInventories.Add(new BranchInventory
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Product = product,
            Quantity = 10,
            ReorderLevel = 1,
            LastUpdatedAt = completedAt
        });
        await db.SaveChangesAsync();

        var service = new ProductReportService(
            db,
            new TestCurrentUserService { TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id },
            NullLogger<ProductReportService>.Instance);

        var result = await service.GetCogsReportAsync(FixedCairoDate, FixedCairoDate);

        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();
        result.Data!.CostOfGoodsSold.Should().Be(0m);
        result.Data.ProductsWithNoCostCount.Should().Be(1);
        result.Data.ClosingInventoryValue.Should().Be(0m);
        result.Data.ProductBreakdown.Should().ContainSingle();
        result.Data.ProductBreakdown[0].UnitCost.Should().Be(0m);
        result.Data.ProductBreakdown[0].HasMissingCost.Should().BeTrue();
        result.Data.ProductBreakdown[0].GrossProfit.Should().Be(100m);
    }

    [Fact]
    public async Task DailyReport_ShouldCalculateDeferredAgainstTaxInclusiveRevenue()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (tenant, branch, user) = await SeedTenantBranchAndUserAsync(db, "daily-report");
        var reportDate = FixedCairoDate;
        var shiftClosedAt = FixedUtcNoon;
        var shift = new Shift
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            OpeningBalance = 200m,
            ClosingBalance = 250m,
            ExpectedBalance = 250m,
            Difference = 0m,
            TotalOrders = 1,
            TotalCash = 50m,
            IsClosed = true,
            OpenedAt = shiftClosedAt.AddHours(-4),
            ClosedAt = shiftClosedAt
        };

        var order = new Order
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Shift = shift,
            UserId = user.Id,
            OrderNumber = "DR-" + Guid.NewGuid().ToString("N")[..6],
            Status = OrderStatus.Completed,
            OrderType = OrderType.DineIn,
            Subtotal = 100m,
            TaxRate = 14m,
            TaxAmount = 14m,
            Total = 114m,
            AmountPaid = 50m,
            AmountDue = 64m,
            CompletedAt = shiftClosedAt.AddHours(-1)
        };
        order.Payments.Add(new Payment
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Method = PaymentMethod.Cash,
            Amount = 50m
        });
        shift.Orders.Add(order);

        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var service = new ReportService(
            new UnitOfWork(db),
            new TestCurrentUserService { TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id },
            NullLogger<ReportService>.Instance);

        var result = await service.GetDailyReportAsync(reportDate);

        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();
        result.Data!.ActualTotalSales.Should().Be(114m);
        result.Data.TotalCollected.Should().Be(50m);
        result.Data.TotalDeferred.Should().Be(64m);
        result.Data.TotalCash.Should().Be(50m);
        result.Data.Shifts.Should().ContainSingle();
        result.Data.Shifts[0].TotalSales.Should().Be(114m);
        result.Data.Shifts[0].TotalCollected.Should().Be(50m);
        result.Data.Shifts[0].DeferredAmount.Should().Be(64m);
        result.Data.Shifts[0].CollectedCash.Should().Be(50m);
    }

    private static TimeZoneInfo GetEgyptTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
    }

    private static async Task<(Tenant Tenant, Branch Branch, User User)> SeedTenantBranchAndUserAsync(
        AppDbContext db,
        string slugPrefix)
    {
        var tenant = new Tenant
        {
            Name = $"Report Tenant {slugPrefix}",
            Slug = $"{slugPrefix}-{Guid.NewGuid():N}",
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = $"Branch {slugPrefix}",
            Code = slugPrefix[..Math.Min(slugPrefix.Length, 3)].ToUpperInvariant() + "-" + Guid.NewGuid().ToString("N")[..4],
            Address = "Reports Street",
            Phone = "01000000003",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Report User",
            Email = $"reports-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(user);

        await db.SaveChangesAsync();
        return (tenant, branch, user);
    }
}
