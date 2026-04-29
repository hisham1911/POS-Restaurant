namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Delivery;
using KasserPro.Application.Services.Implementations;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class DeliveryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DeliveryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllAsync_And_GetByIdAsync_ShouldRespectBranchIsolation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seed = await SeedTenantBranchesAndUsersAsync(db, "delivery-branch-scope");
        var branchAPerson = new DeliveryPerson
        {
            TenantId = seed.Tenant.Id,
            BranchId = seed.BranchA.Id,
            Name = "Branch A Rider",
            Phone = "01000000001",
            IsActive = true
        };
        var branchBPerson = new DeliveryPerson
        {
            TenantId = seed.Tenant.Id,
            BranchId = seed.BranchB.Id,
            Name = "Branch B Rider",
            Phone = "01000000002",
            IsActive = true
        };

        db.DeliveryPersons.AddRange(branchAPerson, branchBPerson);
        await db.SaveChangesAsync();

        var service = CreateService(db, seed.Tenant.Id, seed.BranchA.Id, seed.UserA.Id);

        var listResult = await service.GetAllAsync(ct: default);
        var branchBLookup = await service.GetByIdAsync(branchBPerson.Id, default);

        listResult.Items.Should().ContainSingle();
        listResult.Items[0].Id.Should().Be(branchAPerson.Id);
        branchBLookup.Should().BeNull();
    }

    [Fact]
    public async Task AssignDeliveryPersonAsync_ShouldRejectInactiveDeliveryPerson()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seed = await SeedTenantBranchesAndUsersAsync(db, "delivery-inactive-person");
        var inactivePerson = new DeliveryPerson
        {
            TenantId = seed.Tenant.Id,
            BranchId = seed.BranchA.Id,
            Name = "Inactive Rider",
            Phone = "01000000003",
            IsActive = false
        };
        var order = CreateDeliveryOrder(seed.Tenant.Id, seed.BranchA.Id, seed.UserA.Id, "DEL-INACTIVE-001");

        db.DeliveryPersons.Add(inactivePerson);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var service = CreateService(db, seed.Tenant.Id, seed.BranchA.Id, seed.UserA.Id);

        var result = await service.AssignDeliveryPersonAsync(
            order.Id,
            new AssignDeliveryRequest { DeliveryPersonId = inactivePerson.Id },
            default);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.DELIVERY_PERSON_INACTIVE);
    }

    [Fact]
    public async Task UpdateDeliveryStatusAsync_ShouldRejectOutForDeliveryWithoutAssignedPerson()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seed = await SeedTenantBranchesAndUsersAsync(db, "delivery-status-no-person");
        var order = CreateDeliveryOrder(
            seed.Tenant.Id,
            seed.BranchA.Id,
            seed.UserA.Id,
            "DEL-STATUS-001",
            deliveryStatus: DeliveryStatus.PendingAssignment);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var service = CreateService(db, seed.Tenant.Id, seed.BranchA.Id, seed.UserA.Id);

        var result = await service.UpdateDeliveryStatusAsync(
            order.Id,
            new UpdateDeliveryStatusRequest { DeliveryStatus = "OutForDelivery" },
            default);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.DELIVERY_NO_PERSON_ASSIGNED);
    }

    [Fact]
    public async Task GetDeliveryOrdersAsync_ShouldReturnOnlyCurrentBranchDeliveryOrders_AndApplyFilters()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seed = await SeedTenantBranchesAndUsersAsync(db, "delivery-orders-query");
        var rider = new DeliveryPerson
        {
            TenantId = seed.Tenant.Id,
            BranchId = seed.BranchA.Id,
            Name = "Assigned Rider",
            Phone = "01000000004",
            IsActive = true
        };
        db.DeliveryPersons.Add(rider);
        await db.SaveChangesAsync();

        var filterDate = DateTime.UtcNow.Date;
        db.Orders.AddRange(
            CreateDeliveryOrder(
                seed.Tenant.Id,
                seed.BranchA.Id,
                seed.UserA.Id,
                "DEL-LIST-001",
                deliveryStatus: DeliveryStatus.Assigned,
                deliveryPersonId: rider.Id,
                createdAt: filterDate.AddHours(10)),
            CreateDeliveryOrder(
                seed.Tenant.Id,
                seed.BranchA.Id,
                seed.UserA.Id,
                "DEL-LIST-002",
                deliveryStatus: DeliveryStatus.PendingAssignment,
                createdAt: filterDate.AddHours(11)),
            CreateDeliveryOrder(
                seed.Tenant.Id,
                seed.BranchB.Id,
                seed.UserB.Id,
                "DEL-LIST-003",
                deliveryStatus: DeliveryStatus.Assigned,
                deliveryPersonId: rider.Id,
                createdAt: filterDate.AddHours(12)),
            new Order
            {
                TenantId = seed.Tenant.Id,
                BranchId = seed.BranchA.Id,
                OrderNumber = "ORD-NON-DELIVERY",
                Status = OrderStatus.Pending,
                OrderType = OrderType.DineIn,
                Subtotal = 120m,
                Total = 120m,
                AmountDue = 120m,
                UserId = seed.UserA.Id,
                UserName = seed.UserA.Name,
                CreatedAt = filterDate.AddHours(13)
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, seed.Tenant.Id, seed.BranchA.Id, seed.UserA.Id);

        var result = await service.GetDeliveryOrdersAsync(
            new DeliveryOrderFilters
            {
                Status = "Assigned",
                DeliveryPersonId = rider.Id,
                Date = filterDate,
                Page = 1,
                PageSize = 20
            },
            default);

        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items[0].OrderNumber.Should().Be("DEL-LIST-001");
        result.Data.TotalCount.Should().Be(1);
    }

    private static DeliveryService CreateService(AppDbContext db, int tenantId, int branchId, int userId)
        => new(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = tenantId,
                BranchId = branchId,
                UserId = userId,
                Email = "delivery-tests@test.com"
            });

    private static Order CreateDeliveryOrder(
        int tenantId,
        int branchId,
        int userId,
        string orderNumber,
        DeliveryStatus deliveryStatus = DeliveryStatus.PendingAssignment,
        int? deliveryPersonId = null,
        DateTime? createdAt = null)
        => new()
        {
            TenantId = tenantId,
            BranchId = branchId,
            OrderNumber = orderNumber,
            Status = OrderStatus.Pending,
            OrderType = OrderType.Delivery,
            Subtotal = 150m,
            Total = 165m,
            AmountDue = 165m,
            DeliveryFee = 15m,
            DeliveryAddress = "Test Address",
            DeliveryStatus = deliveryStatus,
            DeliveryPersonId = deliveryPersonId,
            UserId = userId,
            UserName = "Delivery Tester",
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

    private static async Task<(
        Tenant Tenant,
        Branch BranchA,
        Branch BranchB,
        User UserA,
        User UserB)> SeedTenantBranchesAndUsersAsync(AppDbContext db, string slug)
    {
        var tenant = new Tenant
        {
            Name = $"Delivery Tenant {slug}",
            Slug = $"{slug}-{Guid.NewGuid():N}",
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branchA = new Branch
        {
            TenantId = tenant.Id,
            Name = "Delivery Branch A",
            Code = "DBA-" + Guid.NewGuid().ToString("N")[..4],
            Address = "Branch A Street",
            Phone = "01000000011",
            CurrencyCode = "EGP",
            DefaultTaxRate = 14m,
            IsActive = true
        };
        var branchB = new Branch
        {
            TenantId = tenant.Id,
            Name = "Delivery Branch B",
            Code = "DBB-" + Guid.NewGuid().ToString("N")[..4],
            Address = "Branch B Street",
            Phone = "01000000012",
            CurrencyCode = "EGP",
            DefaultTaxRate = 14m,
            IsActive = true
        };

        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();

        var userA = new User
        {
            TenantId = tenant.Id,
            BranchId = branchA.Id,
            Name = "Delivery Admin A",
            Email = $"delivery-a-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };
        var userB = new User
        {
            TenantId = tenant.Id,
            BranchId = branchB.Id,
            Name = "Delivery Admin B",
            Email = $"delivery-b-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        return (tenant, branchA, branchB, userA, userB);
    }
}
