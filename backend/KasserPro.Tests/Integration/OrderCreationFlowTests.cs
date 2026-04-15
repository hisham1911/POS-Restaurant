namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;

/// <summary>
/// Integration tests for Order creation flow with multi-branch context.
/// Tests that X-Branch-Id header takes precedence over JWT branchId claim.
/// </summary>
public class OrderCreationFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrderCreationFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// CRITICAL TEST: Verifies that X-Branch-Id header overrides JWT branchId claim.
    /// 
    /// Scenario:
    /// - User has JWT with branchId: 1
    /// - Request sends X-Branch-Id: 2 header
    /// - Expected: Order is created with BranchId = 2 (header wins)
    /// </summary>
    [Fact]
    public async Task CreateOrder_WithBranchHeader_ShouldUseBranchFromHeader()
    {
        // Arrange - Seed test data
        var (testUserId, testProductId, branch1Id, branch2Id) = await SeedTestDataAsync();
        
        const int tenantId = 1;

        // Generate JWT token with branchId = 1
        var token = TestHelpers.GenerateTestToken(
            userId: testUserId,
            tenantId: tenantId,
            branchId: branch1Id,
            email: "cashier@test.com",
            name: "Test Cashier",
            role: "Cashier"
        );

        // Setup request with Authorization header
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // CRITICAL: Override branch context with X-Branch-Id header
        client.DefaultRequestHeaders.Add("X-Branch-Id", branch2Id.ToString());

        var createOrderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            CustomerName = "Test Customer",
            CustomerPhone = "01234567890",
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testProductId, Quantity = 2 }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", createOrderRequest);

        // Assert - Response should be successful (201 Created or 200 OK)
        response.IsSuccessStatusCode.Should().BeTrue(
            because: "Order creation should succeed with valid token and branch header");

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue(because: $"API should return success. Error: {apiResponse.Message}");
        apiResponse.Data.Should().NotBeNull();

        var order = apiResponse.Data!;

        // CRITICAL ASSERTIONS
        // 1. BranchId should be from header (branch2Id), NOT from JWT (branch1Id)
        order.BranchId.Should().Be(branch2Id, 
            because: "X-Branch-Id header should take precedence over JWT branchId claim");

        // 2. BranchName should be Branch 2's name
        order.BranchName.Should().Be("Branch 2", 
            because: "Order should have Branch 2's name snapshot");

        // 3. UserName should match the test user
        order.UserName.Should().Be("Test Cashier",
            because: "Order should have the user's name snapshot");

        // 4. Order should have items
        order.Items.Should().HaveCount(1);
        order.Items[0].Quantity.Should().Be(2);

        // Cleanup
        await CleanupOrderAsync(order.Id);
    }

    /// <summary>
    /// Verifies that when no X-Branch-Id header is sent, JWT branchId is used.
    /// </summary>
    [Fact]
    public async Task CreateOrder_WithoutBranchHeader_ShouldUseBranchFromJwt()
    {
        // Arrange - Seed test data
        var (testUserId, testProductId, branch1Id, branch2Id) = await SeedTestDataAsync();
        
        const int tenantId = 1;

        var token = TestHelpers.GenerateTestToken(
            userId: testUserId,
            tenantId: tenantId,
            branchId: branch1Id,
            email: "cashier@test.com",
            name: "Test Cashier",
            role: "Cashier"
        );

        // Create a new client without the X-Branch-Id header
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Note: NOT adding X-Branch-Id header

        var createOrderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            CustomerName = "Another Customer",
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testProductId, Quantity = 1 }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", createOrderRequest);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();

        var order = apiResponse.Data!;

        // BranchId should be from JWT (branch1Id) since no header was sent
        order.BranchId.Should().Be(branch1Id,
            because: "Without X-Branch-Id header, JWT branchId should be used");

        order.BranchName.Should().Be("Branch 1",
            because: "Order should have Branch 1's name snapshot");

        // Cleanup
        await CleanupOrderAsync(order.Id);
    }

    [Fact]
    public async Task CreateOrder_WithItemDiscount_ShouldPersistDiscountAndMatchTotals()
    {
        var (testUserId, testProductId, branch1Id, _) = await SeedTestDataAsync();

        const int tenantId = 1;
        var token = TestHelpers.GenerateTestToken(
            userId: testUserId,
            tenantId: tenantId,
            branchId: branch1Id,
            email: "cashier@test.com",
            name: "Test Cashier",
            role: "Cashier"
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createOrderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testProductId, Quantity = 1 },
                new()
                {
                    ProductId = testProductId,
                    Quantity = 1,
                    DiscountType = "percentage",
                    DiscountValue = 20m,
                    DiscountReason = "Release blocker regression test"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/orders", createOrderRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue(because: apiResponse.Message);
        apiResponse.Data.Should().NotBeNull();

        var createdOrder = apiResponse.Data!;
        createdOrder.Items.Should().HaveCount(2);
        createdOrder.Total.Should().Be(205.2m, because: "POS should total 114 + 91.2 when one 100 EGP line gets a 20% item discount");

        var discountedResponseItem = createdOrder.Items.Single(i => i.DiscountAmount > 0);
        discountedResponseItem.DiscountType.Should().Be("percentage");
        discountedResponseItem.DiscountValue.Should().Be(20m);
        discountedResponseItem.DiscountAmount.Should().Be(20m);
        discountedResponseItem.Total.Should().Be(91.2m, because: "receipt and UI line totals rely on the discounted item total");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persistedOrder = await db.Orders
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == createdOrder.Id);

        persistedOrder.Subtotal.Should().Be(200m);
        persistedOrder.TaxAmount.Should().Be(25.2m);
        persistedOrder.Total.Should().Be(205.2m);

        var discountedPersistedItem = persistedOrder.Items.Single(i => i.DiscountAmount > 0);
        discountedPersistedItem.DiscountType.Should().Be("percentage");
        discountedPersistedItem.DiscountValue.Should().Be(20m);
        discountedPersistedItem.DiscountAmount.Should().Be(20m);
        discountedPersistedItem.DiscountReason.Should().Be("Release blocker regression test");
        discountedPersistedItem.Subtotal.Should().Be(100m);
        discountedPersistedItem.Total.Should().Be(91.2m);

        await CleanupOrderAsync(createdOrder.Id);
    }

    [Fact]
    public async Task CreateOrder_WithItemAndOrderDiscounts_ShouldPersistBackendTotalThatMatchesPosFormula()
    {
        var (testUserId, testProductId, branch1Id, _) = await SeedTestDataAsync();

        const int tenantId = 1;
        var token = TestHelpers.GenerateTestToken(
            userId: testUserId,
            tenantId: tenantId,
            branchId: branch1Id,
            email: "cashier@test.com",
            name: "Test Cashier",
            role: "Cashier"
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createOrderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            DiscountType = "Percentage",
            DiscountValue = 10m,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testProductId, Quantity = 1 },
                new()
                {
                    ProductId = testProductId,
                    Quantity = 1,
                    DiscountType = "percentage",
                    DiscountValue = 20m,
                    DiscountReason = "Combined-discount verification"
                },
                new() { ProductId = testProductId, Quantity = 1 }
            }
        };

        var response = await client.PostAsJsonAsync("/api/orders", createOrderRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue(because: apiResponse.Message);
        apiResponse.Data.Should().NotBeNull();

        var createdOrder = apiResponse.Data!;
        createdOrder.Subtotal.Should().Be(300m);
        createdOrder.DiscountAmount.Should().Be(28m, because: "10% order discount should apply after the 20 EGP item-level discount");
        createdOrder.TaxAmount.Should().Be(35.28m);
        createdOrder.Total.Should().Be(287.28m);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persistedOrder = await db.Orders
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == createdOrder.Id);

        persistedOrder.Subtotal.Should().Be(300m);
        persistedOrder.DiscountAmount.Should().Be(28m);
        persistedOrder.TaxAmount.Should().Be(35.28m);
        persistedOrder.Total.Should().Be(287.28m);
        persistedOrder.Items.Sum(i => i.DiscountAmount).Should().Be(20m);

        await CleanupOrderAsync(createdOrder.Id);
    }

    [Fact]
    public async Task CreateOrder_WithTaxInclusiveProduct_ShouldPersistNetUnitPriceAndExpectedTotals()
    {
        var (testUserId, testProductId, branch1Id, _) = await SeedTestDataAsync();
        var taxInclusiveProductId = 0;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seededProduct = await db.Products.SingleAsync(p => p.Id == testProductId);

            var taxInclusiveProduct = new Product
            {
                TenantId = seededProduct.TenantId,
                CategoryId = seededProduct.CategoryId,
                Name = "Tax Inclusive Product",
                NameEn = "Tax Inclusive Product",
                Sku = "INC-" + Guid.NewGuid().ToString()[..8],
                Price = 114m,
                Cost = 50m,
                TaxRate = 14m,
                TaxInclusive = true,
                IsActive = true
            };

            db.Products.Add(taxInclusiveProduct);
            await db.SaveChangesAsync();
            taxInclusiveProductId = taxInclusiveProduct.Id;
        }

        const int tenantId = 1;
        var token = TestHelpers.GenerateTestToken(
            userId: testUserId,
            tenantId: tenantId,
            branchId: branch1Id,
            email: "cashier@test.com",
            name: "Test Cashier",
            role: "Cashier"
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createOrderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = taxInclusiveProductId, Quantity = 1 },
                new() { ProductId = testProductId, Quantity = 1 }
            }
        };

        var response = await client.PostAsJsonAsync("/api/orders", createOrderRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue(because: apiResponse.Message);
        apiResponse.Data.Should().NotBeNull();

        var createdOrder = apiResponse.Data!;
        createdOrder.Subtotal.Should().Be(200m);
        createdOrder.TaxAmount.Should().Be(28m);
        createdOrder.Total.Should().Be(228m);

        var taxInclusiveResponseItem = createdOrder.Items.Single(i => i.ProductId == taxInclusiveProductId);
        taxInclusiveResponseItem.TaxInclusive.Should().BeTrue();
        taxInclusiveResponseItem.OriginalPrice.Should().Be(114m);
        taxInclusiveResponseItem.UnitPrice.Should().Be(100m);
        taxInclusiveResponseItem.Subtotal.Should().Be(100m);
        taxInclusiveResponseItem.TaxAmount.Should().Be(14m);
        taxInclusiveResponseItem.Total.Should().Be(114m);

        var taxExclusiveResponseItem = createdOrder.Items.Single(i => i.ProductId == testProductId);
        taxExclusiveResponseItem.TaxInclusive.Should().BeFalse();
        taxExclusiveResponseItem.OriginalPrice.Should().Be(100m);
        taxExclusiveResponseItem.UnitPrice.Should().Be(100m);
        taxExclusiveResponseItem.Subtotal.Should().Be(100m);
        taxExclusiveResponseItem.TaxAmount.Should().Be(14m);
        taxExclusiveResponseItem.Total.Should().Be(114m);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedOrder = await verificationDb.Orders
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == createdOrder.Id);

        persistedOrder.Subtotal.Should().Be(200m);
        persistedOrder.TaxAmount.Should().Be(28m);
        persistedOrder.Total.Should().Be(228m);

        var taxInclusivePersistedItem = persistedOrder.Items.Single(i => i.ProductId == taxInclusiveProductId);
        taxInclusivePersistedItem.TaxInclusive.Should().BeTrue();
        taxInclusivePersistedItem.OriginalPrice.Should().Be(114m);
        taxInclusivePersistedItem.UnitPrice.Should().Be(100m);
        taxInclusivePersistedItem.Subtotal.Should().Be(100m);
        taxInclusivePersistedItem.TaxAmount.Should().Be(14m);
        taxInclusivePersistedItem.Total.Should().Be(114m);

        var taxExclusivePersistedItem = persistedOrder.Items.Single(i => i.ProductId == testProductId);
        taxExclusivePersistedItem.TaxInclusive.Should().BeFalse();
        taxExclusivePersistedItem.OriginalPrice.Should().Be(100m);
        taxExclusivePersistedItem.UnitPrice.Should().Be(100m);
        taxExclusivePersistedItem.Subtotal.Should().Be(100m);
        taxExclusivePersistedItem.TaxAmount.Should().Be(14m);
        taxExclusivePersistedItem.Total.Should().Be(114m);

        await CleanupOrderAsync(createdOrder.Id);
    }

    /// <summary>
    /// Seeds test data: Tenant, Branches, User, Category, Product
    /// Returns (userId, productId, branch1Id, branch2Id)
    /// </summary>
    private async Task<(int userId, int productId, int branch1Id, int branch2Id)> SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create Tenant
        var tenant = new Tenant
        {
            Name = "Test Tenant",
            Slug = "test-" + Guid.NewGuid().ToString()[..8],
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Create Branch 1 (user's default branch in JWT)
        var branch1 = new Branch
        {
            TenantId = tenant.Id,
            Name = "Branch 1",
            Code = "BR1-" + Guid.NewGuid().ToString()[..8],
            Address = "123 Test Street",
            Phone = "01111111111",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch1);

        // Create Branch 2 (branch to switch to via header)
        var branch2 = new Branch
        {
            TenantId = tenant.Id,
            Name = "Branch 2",
            Code = "BR2-" + Guid.NewGuid().ToString()[..8],
            Address = "456 Test Avenue",
            Phone = "02222222222",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch2);
        await db.SaveChangesAsync();

        // Create User (belongs to tenant, default branch is 1)
        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch1.Id,
            Name = "Test Cashier",
            Email = "cashier-" + Guid.NewGuid().ToString()[..8] + "@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Cashier,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Create Category
        var category = new Category
        {
            TenantId = tenant.Id,
            Name = "Test Category",
            NameEn = "Test Category",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        // Create Product
        var product = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = "Test Product",
            NameEn = "Test Product",
            Sku = "TEST-" + Guid.NewGuid().ToString()[..8],
            Price = 100m,
            Cost = 50m,
            TaxRate = 14m,
            TaxInclusive = false,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return (user.Id, product.Id, branch1.Id, branch2.Id);
    }

    private async Task CleanupOrderAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.FindAsync(orderId);
        if (order != null)
        {
            db.Orders.Remove(order);
            await db.SaveChangesAsync();
        }
    }
}
