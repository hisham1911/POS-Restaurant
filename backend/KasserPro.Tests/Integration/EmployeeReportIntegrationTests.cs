namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class EmployeeReportIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EmployeeReportIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DetailedShiftsReport_ShouldUseOrderTotalsNotPaymentBuckets()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = "Employee Report Tenant",
            Slug = "employee-report-" + Guid.NewGuid().ToString("N"),
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Employee Branch",
            Code = "EMP-" + Guid.NewGuid().ToString("N")[..6],
            Address = "1 Employee Street",
            Phone = "01000000009",
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
            Name = "Shift Cashier",
            Email = "shift-cashier-" + Guid.NewGuid().ToString("N")[..8] + "@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var shift = new Shift
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            OpeningBalance = 200m,
            ClosingBalance = 700m,
            ExpectedBalance = 700m,
            Difference = 0m,
            TotalOrders = 4,
            IsClosed = true,
            OpenedAt = DateTime.UtcNow.AddHours(-8),
            ClosedAt = DateTime.UtcNow.AddHours(-1)
        };

        var cashSale = CreateShiftOrder(tenant.Id, branch.Id, user.Id, shift, "SHIFT-CASH", 300m, 300m, 0m, OrderType.DineIn);
        cashSale.Payments.Add(new Payment { TenantId = tenant.Id, BranchId = branch.Id, Method = PaymentMethod.Cash, Amount = 300m });

        var cardSale = CreateShiftOrder(tenant.Id, branch.Id, user.Id, shift, "SHIFT-CARD", 200m, 200m, 0m, OrderType.DineIn);
        cardSale.Payments.Add(new Payment { TenantId = tenant.Id, BranchId = branch.Id, Method = PaymentMethod.Card, Amount = 200m });

        var creditSale = CreateShiftOrder(tenant.Id, branch.Id, user.Id, shift, "SHIFT-CREDIT", 400m, 0m, 400m, OrderType.DineIn);
        var returnOrder = CreateShiftOrder(tenant.Id, branch.Id, user.Id, shift, "SHIFT-RETURN", 100m, 0m, 0m, OrderType.Return);

        shift.Orders.Add(cashSale);
        shift.Orders.Add(cardSale);
        shift.Orders.Add(creditSale);
        shift.Orders.Add(returnOrder);

        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var service = new EmployeeReportService(
            db,
            new TestCurrentUserService { TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id },
            NullLogger<EmployeeReportService>.Instance);

        var result = await service.GetDetailedShiftsReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();
        result.Data!.TotalRevenue.Should().Be(800m);
        result.Data.Shifts.Should().ContainSingle();
        result.Data.Shifts[0].TotalSales.Should().Be(800m);
        result.Data.Shifts[0].TotalCollected.Should().Be(500m);
        result.Data.Shifts[0].DeferredAmount.Should().Be(300m);
    }

    private static Order CreateShiftOrder(
        int tenantId,
        int branchId,
        int userId,
        Shift shift,
        string orderNumberPrefix,
        decimal total,
        decimal amountPaid,
        decimal amountDue,
        OrderType orderType)
    {
        return new Order
        {
            TenantId = tenantId,
            BranchId = branchId,
            UserId = userId,
            Shift = shift,
            OrderNumber = orderNumberPrefix + "-" + Guid.NewGuid().ToString("N")[..6],
            Status = OrderStatus.Completed,
            OrderType = orderType,
            Subtotal = total,
            TaxRate = 0m,
            TaxAmount = 0m,
            Total = total,
            AmountPaid = amountPaid,
            AmountDue = amountDue,
            CompletedAt = DateTime.UtcNow.AddHours(-2)
        };
    }
}
