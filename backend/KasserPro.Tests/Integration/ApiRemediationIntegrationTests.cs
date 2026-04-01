namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KasserPro.Application.DTOs.Auth;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class ApiRemediationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiRemediationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateOrder_WithoutOrdersCreatePermission_ReturnsForbidden()
    {
        var testData = await SeedOrderPermissionDataAsync();
        using var client = _factory.CreateClient();

        var token = await LoginAndGetTokenAsync(client, testData.Email, "Test123!");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", testData.BranchId.ToString());

        var request = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testData.ProductId, Quantity = 1 }
            }
        };

        var response = await client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPayments_ForAnotherTenantOrder_ReturnsEmptyList()
    {
        var testData = await SeedCrossTenantPaymentDataAsync();
        using var client = _factory.CreateClient();

        var token = await LoginAndGetTokenAsync(client, testData.RequestingUserEmail, "Test123!");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", testData.RequestingBranchId.ToString());

        var response = await client.GetAsync($"/api/payments/order/{testData.ForeignOrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<List<PaymentDto>>>(content, _jsonOptions);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    private async Task<(int TenantId, int BranchId, int UserId, int ProductId, string Email)> SeedOrderPermissionDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = "Permission Tenant",
            Slug = "permission-" + Guid.NewGuid().ToString()[..8],
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Permission Branch",
            Code = "PB-" + Guid.NewGuid().ToString()[..6],
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var email = "permission-" + Guid.NewGuid().ToString()[..8] + "@test.com";

        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Permission Cashier",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Cashier,
            IsActive = true
        };
        db.Users.Add(user);

        var category = new Category
        {
            TenantId = tenant.Id,
            Name = "Permission Category",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = "Permission Product",
            Sku = "PERM-" + Guid.NewGuid().ToString()[..6],
            Price = 50m,
            Cost = 25m,
            TaxRate = 14m,
            TaxInclusive = false,
            IsActive = true,
            TrackInventory = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return (tenant.Id, branch.Id, user.Id, product.Id, email);
    }

    private async Task<(int RequestingTenantId, int RequestingBranchId, int RequestingUserId, string RequestingUserEmail, int ForeignOrderId)> SeedCrossTenantPaymentDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantA = new Tenant
        {
            Name = "Tenant A",
            Slug = "tenant-a-" + Guid.NewGuid().ToString()[..6],
            IsActive = true
        };
        var tenantB = new Tenant
        {
            Name = "Tenant B",
            Slug = "tenant-b-" + Guid.NewGuid().ToString()[..6],
            IsActive = true
        };
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync();

        var branchA = new Branch
        {
            TenantId = tenantA.Id,
            Name = "Branch A",
            Code = "BA-" + Guid.NewGuid().ToString()[..6],
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        var branchB = new Branch
        {
            TenantId = tenantB.Id,
            Name = "Branch B",
            Code = "BB-" + Guid.NewGuid().ToString()[..6],
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();

        var requestingUserEmail = "tenant-a-" + Guid.NewGuid().ToString()[..8] + "@test.com";
        var foreignUserEmail = "tenant-b-" + Guid.NewGuid().ToString()[..8] + "@test.com";

        var requestingUser = new User
        {
            TenantId = tenantA.Id,
            BranchId = branchA.Id,
            Name = "Tenant A Cashier",
            Email = requestingUserEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Cashier,
            IsActive = true
        };
        var foreignUser = new User
        {
            TenantId = tenantB.Id,
            BranchId = branchB.Id,
            Name = "Tenant B Cashier",
            Email = foreignUserEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Cashier,
            IsActive = true
        };
        db.Users.AddRange(requestingUser, foreignUser);
        await db.SaveChangesAsync();

        db.UserPermissions.Add(new UserPermission
        {
            UserId = requestingUser.Id,
            Permission = Permission.OrdersView
        });
        await db.SaveChangesAsync();

        var foreignOrder = new Order
        {
            TenantId = tenantB.Id,
            BranchId = branchB.Id,
            BranchName = branchB.Name,
            OrderNumber = "ORD-" + Guid.NewGuid().ToString()[..8],
            Status = OrderStatus.Completed,
            OrderType = OrderType.DineIn,
            Subtotal = 100m,
            TaxRate = 14m,
            TaxAmount = 14m,
            Total = 114m,
            AmountPaid = 114m,
            AmountDue = 0m,
            UserId = foreignUser.Id,
            UserName = foreignUser.Name,
            CompletedAt = DateTime.UtcNow
        };
        db.Orders.Add(foreignOrder);
        await db.SaveChangesAsync();

        var foreignPayment = new Payment
        {
            TenantId = tenantB.Id,
            BranchId = branchB.Id,
            OrderId = foreignOrder.Id,
            Method = PaymentMethod.Cash,
            Amount = 114m,
            Reference = "PAY-" + Guid.NewGuid().ToString()[..6]
        };
        db.Payments.Add(foreignPayment);
        await db.SaveChangesAsync();

        return (tenantA.Id, branchA.Id, requestingUser.Id, requestingUserEmail, foreignOrder.Id);
    }

    private async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(content, _jsonOptions);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();

        return result.Data.AccessToken;
    }
}
