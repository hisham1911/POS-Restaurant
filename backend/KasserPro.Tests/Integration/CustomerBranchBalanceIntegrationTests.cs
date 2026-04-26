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

public class CustomerBranchBalanceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CustomerBranchBalanceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidateCreditLimit_ShouldUseCurrentBranchBalance_NotGlobalTotalDue()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seed = await SeedTenantBranchesAndCustomerAsync(db);

        db.CustomerBranchBalances.Add(new CustomerBranchBalance
        {
            TenantId = seed.Tenant.Id,
            BranchId = seed.BranchA.Id,
            CustomerId = seed.Customer.Id,
            AmountDue = 600m
        });
        seed.Customer.TotalDue = 600m;
        await db.SaveChangesAsync();

        var branchAService = new CustomerService(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchA.Id,
                UserId = seed.User.Id
            },
            null!);

        var branchBService = new CustomerService(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchB.Id,
                UserId = seed.User.Id
            },
            null!);

        (await branchAService.ValidateCreditLimitAsync(seed.Customer.Id, 500m)).Should().BeFalse();
        (await branchBService.ValidateCreditLimitAsync(seed.Customer.Id, 600m)).Should().BeTrue();
    }

    [Fact]
    public async Task CustomerDebtsReport_ShouldShowOnlyCurrentBranchBalance()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seed = await SeedTenantBranchesAndCustomerAsync(db);

        var branchAService = new CustomerService(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchA.Id,
                UserId = seed.User.Id
            },
            null!);

        var branchBService = new CustomerService(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchB.Id,
                UserId = seed.User.Id
            },
            null!);

        await branchAService.UpdateCreditBalanceAsync(seed.Customer.Id, 600m);
        await db.SaveChangesAsync();

        await branchBService.UpdateCreditBalanceAsync(seed.Customer.Id, 300m);
        await db.SaveChangesAsync();

        var branchAReportService = new CustomerReportService(
            db,
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchA.Id,
                UserId = seed.User.Id
            },
            NullLogger<CustomerReportService>.Instance);

        var branchBReportService = new CustomerReportService(
            db,
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchB.Id,
                UserId = seed.User.Id
            },
            NullLogger<CustomerReportService>.Instance);

        var branchAReport = await branchAReportService.GetCustomerDebtsReportAsync();
        branchAReport.Success.Should().BeTrue(because: branchAReport.Message);
        branchAReport.Data.Should().NotBeNull();
        branchAReport.Data!.TotalOutstandingAmount.Should().Be(600m);
        branchAReport.Data.CustomerDebts.Should().ContainSingle();
        branchAReport.Data.CustomerDebts[0].TotalDue.Should().Be(600m);

        var branchBReport = await branchBReportService.GetCustomerDebtsReportAsync();
        branchBReport.Success.Should().BeTrue(because: branchBReport.Message);
        branchBReport.Data.Should().NotBeNull();
        branchBReport.Data!.TotalOutstandingAmount.Should().Be(300m);
        branchBReport.Data.CustomerDebts.Should().ContainSingle();
        branchBReport.Data.CustomerDebts[0].TotalDue.Should().Be(300m);

        var customer = await db.Customers.FindAsync(seed.Customer.Id);
        customer.Should().NotBeNull();
        customer!.TotalDue.Should().Be(900m);
    }

    [Fact]
    public async Task TopCustomersReport_ShouldUseCurrentBranchOutstandingBalance()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seed = await SeedTenantBranchesAndCustomerAsync(db);
        var reportDate = DateTime.UtcNow;

        var branchAService = new CustomerService(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchA.Id,
                UserId = seed.User.Id
            },
            null!);

        var branchBService = new CustomerService(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchB.Id,
                UserId = seed.User.Id
            },
            null!);

        await branchAService.UpdateCreditBalanceAsync(seed.Customer.Id, 600m);
        await db.SaveChangesAsync();

        await branchBService.UpdateCreditBalanceAsync(seed.Customer.Id, 300m);
        await db.SaveChangesAsync();

        db.Orders.AddRange(
            new Order
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchA.Id,
                OrderNumber = "TOP-A-" + Guid.NewGuid().ToString("N")[..8],
                Status = OrderStatus.Completed,
                OrderType = OrderType.DineIn,
                Subtotal = 600m,
                Total = 600m,
                AmountPaid = 0m,
                AmountDue = 600m,
                CustomerId = seed.Customer.Id,
                CustomerName = seed.Customer.Name,
                CustomerPhone = seed.Customer.Phone,
                UserId = seed.User.Id,
                UserName = seed.User.Name,
                CompletedAt = reportDate
            },
            new Order
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchB.Id,
                OrderNumber = "TOP-B-" + Guid.NewGuid().ToString("N")[..8],
                Status = OrderStatus.Completed,
                OrderType = OrderType.DineIn,
                Subtotal = 300m,
                Total = 300m,
                AmountPaid = 0m,
                AmountDue = 300m,
                CustomerId = seed.Customer.Id,
                CustomerName = seed.Customer.Name,
                CustomerPhone = seed.Customer.Phone,
                UserId = seed.User.Id,
                UserName = seed.User.Name,
                CompletedAt = reportDate
            });
        await db.SaveChangesAsync();

        var fromDate = reportDate.AddDays(-1);
        var toDate = reportDate.AddDays(1);

        var branchAReportService = new CustomerReportService(
            db,
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchA.Id,
                UserId = seed.User.Id
            },
            NullLogger<CustomerReportService>.Instance);

        var branchBReportService = new CustomerReportService(
            db,
            new TestCurrentUserService
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchB.Id,
                UserId = seed.User.Id
            },
            NullLogger<CustomerReportService>.Instance);

        var branchAReport = await branchAReportService.GetTopCustomersReportAsync(fromDate, toDate, 10);
        branchAReport.Success.Should().BeTrue(because: branchAReport.Message);
        branchAReport.Data.Should().NotBeNull();
        branchAReport.Data!.TopCustomers.Should().ContainSingle();
        branchAReport.Data.TopCustomers[0].OutstandingBalance.Should().Be(600m);

        var branchBReport = await branchBReportService.GetTopCustomersReportAsync(fromDate, toDate, 10);
        branchBReport.Success.Should().BeTrue(because: branchBReport.Message);
        branchBReport.Data.Should().NotBeNull();
        branchBReport.Data!.TopCustomers.Should().ContainSingle();
        branchBReport.Data.TopCustomers[0].OutstandingBalance.Should().Be(300m);
    }

    private static async Task<(Tenant Tenant, Branch BranchA, Branch BranchB, User User, Customer Customer)>
        SeedTenantBranchesAndCustomerAsync(AppDbContext db)
    {
        var tenant = new Tenant
        {
            Name = "Customer Branch Tenant",
            Slug = "customer-branch-" + Guid.NewGuid().ToString("N"),
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branchA = new Branch
        {
            TenantId = tenant.Id,
            Name = "Branch A",
            Code = "CBA-" + Guid.NewGuid().ToString("N")[..4],
            Address = "Branch A Street",
            Phone = "01000000011",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };

        var branchB = new Branch
        {
            TenantId = tenant.Id,
            Name = "Branch B",
            Code = "CBB-" + Guid.NewGuid().ToString("N")[..4],
            Address = "Branch B Street",
            Phone = "01000000012",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };

        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branchA.Id,
            Name = "Customer Branch Admin",
            Email = "customer-branch-" + Guid.NewGuid().ToString("N") + "@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        var customer = new Customer
        {
            TenantId = tenant.Id,
            Phone = "010" + Random.Shared.Next(10000000, 99999999),
            Name = "Branch Debt Customer",
            IsActive = true,
            CreditLimit = 1000m,
            TotalDue = 0m
        };

        db.Users.Add(user);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (tenant, branchA, branchB, user, customer);
    }
}
