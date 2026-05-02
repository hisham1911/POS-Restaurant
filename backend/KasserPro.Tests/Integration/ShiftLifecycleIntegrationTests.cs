namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.DTOs.Shifts;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;

/// <summary>
/// SOLID GROUND CERTIFICATION TEST
/// 
/// This integration test validates the complete shift lifecycle including:
/// - Shift opening/closing
/// - Product validation (inactive products cannot be sold)
/// - Payment validation (overpayment limit)
/// - Tax calculation (14% Tax Exclusive/Additive)
/// - Financial totals verification
/// 
/// If this test passes, the system is certified "Production Ready".
/// </summary>
public class ShiftLifecycleIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ShiftLifecycleIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// COMPLETE SHIFT LIFECYCLE TEST
    /// 
    /// Flow:
    /// 1. Setup: Create Tenant (Tax=14%, Enabled), Branch, Cashier, Products (1 Active, 1 Inactive)
    /// 2. Open Shift with 500 EGP opening balance
    /// 3. VALIDATION TEST 1: Try to sell Inactive Product → Expect FAIL
    /// 4. VALIDATION TEST 2: Try to overpay (5000 EGP for 114 EGP order) → Expect FAIL
    /// 5. VALID ORDER: Sell Active Product (100 EGP Net) → Verify Tax Calculation
    /// 6. Complete Order with Cash Payment
    /// 7. Close Shift → Verify Totals
    /// </summary>
    [Fact]
    public async Task ShiftLifecycle_CompleteFlow_ShouldPassAllValidations()
    {
        // ═══════════════════════════════════════════════════════════════════
        // PHASE 1: SETUP - Seed Test Data
        // ═══════════════════════════════════════════════════════════════════
        var testData = await SeedTestDataAsync();
        
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: testData.UserId,
            tenantId: testData.TenantId,
            branchId: testData.BranchId,
            email: "cashier@kasserpro.test",
            name: "Test Cashier",
            role: "Cashier"
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // ═══════════════════════════════════════════════════════════════════
        // PHASE 2: OPEN SHIFT
        // ═══════════════════════════════════════════════════════════════════
        var openShiftRequest = new OpenShiftRequest { OpeningBalance = 500m };
        var openShiftResponse = await client.PostAsJsonAsync("/api/shifts/open", openShiftRequest);
        
        openShiftResponse.IsSuccessStatusCode.Should().BeTrue(
            because: "Shift should open successfully");
        
        var openShiftResult = await DeserializeResponse<ShiftDto>(openShiftResponse);
        openShiftResult.Success.Should().BeTrue(because: $"Open shift failed: {openShiftResult.Message}");
        openShiftResult.Data.Should().NotBeNull();
        
        var shiftId = openShiftResult.Data!.Id;
        openShiftResult.Data.OpeningBalance.Should().Be(500m);
        openShiftResult.Data.IsClosed.Should().BeFalse();

        // ═══════════════════════════════════════════════════════════════════
        // PHASE 3: VALIDATION TEST 1 - Inactive Product Cannot Be Sold
        // ═══════════════════════════════════════════════════════════════════
        var inactiveProductOrder = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            CustomerName = "Test Customer",
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testData.InactiveProductId, Quantity = 1 }
            }
        };

        var inactiveProductResponse = await client.PostAsJsonAsync("/api/orders", inactiveProductOrder);
        var inactiveProductResult = await DeserializeResponse<OrderDto>(inactiveProductResponse);
        
        inactiveProductResult.Success.Should().BeFalse(
            because: "Selling inactive product should be rejected");
        inactiveProductResult.Message.Should().Contain("غير متاح",
            because: "Error message should indicate product is not available");

        // ═══════════════════════════════════════════════════════════════════
        // PHASE 4: CREATE VALID ORDER (Active Product)
        // ═══════════════════════════════════════════════════════════════════
        var validOrderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            CustomerName = "Valid Customer",
            CustomerPhone = "01234567890",
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testData.ActiveProductId, Quantity = 1 }
            }
        };

        var createOrderResponse = await client.PostAsJsonAsync("/api/orders", validOrderRequest);
        createOrderResponse.IsSuccessStatusCode.Should().BeTrue(
            because: "Valid order should be created successfully");
        
        var createOrderResult = await DeserializeResponse<OrderDto>(createOrderResponse);
        createOrderResult.Success.Should().BeTrue(because: $"Create order failed: {createOrderResult.Message}");
        createOrderResult.Data.Should().NotBeNull();

        var order = createOrderResult.Data!;
        var orderId = order.Id;

        // ═══════════════════════════════════════════════════════════════════
        // VERIFY TAX CALCULATION (Tax Exclusive/Additive)
        // Product Price = 100 EGP (Net, excluding tax)
        // Tax Rate = 14%
        // Expected:
        //   Subtotal = 100 EGP
        //   Tax = 100 * 0.14 = 14 EGP
        //   Total = 100 + 14 = 114 EGP
        // ═══════════════════════════════════════════════════════════════════
        order.Subtotal.Should().Be(100m, because: "Subtotal should be Net price (100 EGP)");
        order.TaxAmount.Should().Be(14m, because: "Tax should be 14% of Subtotal (14 EGP)");
        order.Total.Should().Be(114m, because: "Total should be Subtotal + Tax (114 EGP)");
        order.TaxRate.Should().Be(14m, because: "Tax rate should be 14%");

        // Verify item-level calculations
        order.Items.Should().HaveCount(1);
        var orderItem = order.Items[0];
        orderItem.UnitPrice.Should().Be(100m, because: "Unit price should be Net price");
        orderItem.Subtotal.Should().Be(100m, because: "Item subtotal = UnitPrice * Quantity");
        orderItem.TaxAmount.Should().Be(14m, because: "Item tax = Subtotal * 14%");
        orderItem.Total.Should().Be(114m, because: "Item total = Subtotal + Tax");

        // ═══════════════════════════════════════════════════════════════════
        // PHASE 5: VALIDATION TEST 2 - Overpayment Limit
        // Order Total = 114 EGP
        // Max Allowed = 114 * 2 = 228 EGP
        // Attempted Payment = 5000 EGP → Should FAIL
        // ═══════════════════════════════════════════════════════════════════
        var overpaymentRequest = new CompleteOrderRequest
        {
            Payments = new List<PaymentRequest>
            {
                new() { Method = "Cash", Amount = 5000m }
            }
        };

        var overpaymentResponse = await client.PostAsJsonAsync($"/api/orders/{orderId}/complete", overpaymentRequest);
        var overpaymentResult = await DeserializeResponse<OrderDto>(overpaymentResponse);
        
        overpaymentResult.Success.Should().BeFalse(
            because: "Overpayment (5000 EGP for 114 EGP order) should be rejected");
        overpaymentResult.Message.Should().Contain("يتجاوز",
            because: "Error message should indicate payment exceeds limit");

        // ═══════════════════════════════════════════════════════════════════
        // PHASE 6: COMPLETE ORDER WITH VALID PAYMENT
        // Pay 120 EGP Cash for 114 EGP order → Change = 6 EGP
        // ═══════════════════════════════════════════════════════════════════
        var validPaymentRequest = new CompleteOrderRequest
        {
            Payments = new List<PaymentRequest>
            {
                new() { Method = "Cash", Amount = 120m }
            }
        };

        var completeOrderResponse = await client.PostAsJsonAsync($"/api/orders/{orderId}/complete", validPaymentRequest);
        completeOrderResponse.IsSuccessStatusCode.Should().BeTrue(
            because: "Valid payment should complete the order");
        
        var completeOrderResult = await DeserializeResponse<OrderDto>(completeOrderResponse);
        completeOrderResult.Success.Should().BeTrue(because: $"Complete order failed: {completeOrderResult.Message}");
        completeOrderResult.Data.Should().NotBeNull();

        var completedOrder = completeOrderResult.Data!;
        completedOrder.Status.Should().Be("Completed", because: "Order status should be Completed");
        completedOrder.AmountPaid.Should().Be(114m, because: "Applied payment should equal the order total");
        completedOrder.ChangeAmount.Should().Be(6m, because: "Change should be 120 - 114 = 6 EGP");
        completedOrder.AmountDue.Should().Be(0m, because: "Amount due must never go negative");

        // ═══════════════════════════════════════════════════════════════════
        // PHASE 7: CLOSE SHIFT AND VERIFY TOTALS
        // Opening Balance = 500 EGP
        // Cash Sales = 114 EGP net cash received after change
        // Expected Balance = Opening + TotalCash = 500 + 114 = 614 EGP
        // ═══════════════════════════════════════════════════════════════════
        var closeShiftRequest = new CloseShiftRequest
        {
            ClosingBalance = 614m, // 500 + 114 (actual cash in drawer after giving change)
            Notes = "Solid Ground Certification Test - Shift Closed Successfully"
        };

        var closeShiftResponse = await client.PostAsJsonAsync("/api/shifts/close", closeShiftRequest);
        closeShiftResponse.IsSuccessStatusCode.Should().BeTrue(
            because: "Shift should close successfully");
        
        var closeShiftResult = await DeserializeResponse<ShiftDto>(closeShiftResponse);
        closeShiftResult.Success.Should().BeTrue(because: $"Close shift failed: {closeShiftResult.Message}");
        closeShiftResult.Data.Should().NotBeNull();

        var closedShift = closeShiftResult.Data!;
        closedShift.IsClosed.Should().BeTrue(because: "Shift should be marked as closed");
        closedShift.OpeningBalance.Should().Be(500m);
        closedShift.ClosingBalance.Should().Be(614m);
        closedShift.TotalOrders.Should().Be(1, because: "One order was completed");
        closedShift.TotalCash.Should().Be(114m, because: "Total cash should be the applied sale amount after change");
        closedShift.TotalCard.Should().Be(0m, because: "No card payments were made");
        
        // Expected balance = Opening + TotalCash = 500 + 114 = 614
        closedShift.ExpectedBalance.Should().Be(614m,
            because: "Expected = Opening (500) + applied cash sale (114)");
        closedShift.Difference.Should().Be(0m,
            because: "Change is not counted as collected sales cash");

        // ═══════════════════════════════════════════════════════════════════
        // 🎉 ALL TESTS PASSED - SYSTEM IS PRODUCTION READY 🎉
        // ═══════════════════════════════════════════════════════════════════
    }

    /// <summary>
    /// Verify shift details endpoint returns the full financial payload used by the frontend drawer.
    /// </summary>
    [Fact]
    public async Task GetShiftById_ShouldReturnDetailedFinancialSnapshot()
    {
        var testData = await SeedTestDataAsync();

        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: testData.UserId,
            tenantId: testData.TenantId,
            branchId: testData.BranchId,
            role: "Cashier"
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var openShiftResponse = await client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest
        {
            OpeningBalance = 500m
        });
        var openShiftResult = await DeserializeResponse<ShiftDto>(openShiftResponse);
        openShiftResult.Success.Should().BeTrue();
        openShiftResult.Data.Should().NotBeNull();

        var shiftId = openShiftResult.Data!.Id;

        var createOrderResponse = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            CustomerName = "Shift Details Test",
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testData.ActiveProductId, Quantity = 1 }
            }
        });
        var createOrderResult = await DeserializeResponse<OrderDto>(createOrderResponse);
        createOrderResult.Success.Should().BeTrue();
        createOrderResult.Data.Should().NotBeNull();

        await client.PostAsJsonAsync($"/api/orders/{createOrderResult.Data!.Id}/complete", new CompleteOrderRequest
        {
            Payments = new List<PaymentRequest>
            {
                new() { Method = "Cash", Amount = 120m }
            }
        });

        var closeShiftResponse = await client.PostAsJsonAsync("/api/shifts/close", new CloseShiftRequest
        {
            ClosingBalance = 614m,
            Notes = "Shift details API verification",
            RowVersion = openShiftResult.Data.RowVersion
        });
        var closeShiftResult = await DeserializeResponse<ShiftDto>(closeShiftResponse);
        closeShiftResult.Success.Should().BeTrue();

        var detailsResponse = await client.GetAsync($"/api/shifts/{shiftId}");
        detailsResponse.IsSuccessStatusCode.Should().BeTrue();

        var detailsJson = await detailsResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"SHIFT_DETAILS_JSON: {detailsJson}");

        var detailsResult = JsonSerializer.Deserialize<ApiResponse<ShiftDto>>(detailsJson, _jsonOptions);
        detailsResult.Should().NotBeNull();
        detailsResult!.Success.Should().BeTrue();
        detailsResult.Data.Should().NotBeNull();

        var detailedShift = detailsResult.Data!;
        detailedShift.Id.Should().Be(shiftId);
        detailedShift.TotalSales.Should().Be(114m);
        detailedShift.TotalCash.Should().Be(114m);
        detailedShift.TotalCollected.Should().Be(114m);
        detailedShift.ExpectedBalance.Should().Be(614m);
        detailedShift.Difference.Should().Be(0m);
        detailedShift.TotalRefunds.Should().Be(0m);
        detailedShift.TotalExpenses.Should().Be(0m);
        detailedShift.TotalOrders.Should().Be(1);
        detailedShift.TotalOrdersCount.Should().Be(1);
        detailedShift.CompletedOrdersCount.Should().Be(1);
        detailedShift.Orders.Should().HaveCount(1);
        detailedShift.Orders[0].PaymentMethod.Should().Be("Cash");

        var historyResponse = await client.GetAsync("/api/shifts/history");
        historyResponse.IsSuccessStatusCode.Should().BeTrue();

        var historyResult = await DeserializeResponse<List<ShiftDto>>(historyResponse);
        historyResult.Success.Should().BeTrue();
        historyResult.Data.Should().NotBeNullOrEmpty();
        historyResult.Data![0].Id.Should().Be(shiftId);
        historyResult.Data[0].TotalSales.Should().Be(114m);
        historyResult.Data[0].TotalOrdersCount.Should().Be(1);
    }

    /// <summary>
    /// Additional test: Verify empty order is rejected
    /// </summary>
    [Fact]
    public async Task CreateOrder_WithNoItems_ShouldFail()
    {
        var testData = await SeedTestDataAsync();
        
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: testData.UserId,
            tenantId: testData.TenantId,
            branchId: testData.BranchId,
            role: "Cashier"
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Open shift first
        await client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest { OpeningBalance = 100m });

        // Try to create empty order
        var emptyOrderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            CustomerName = "Empty Order Test",
            Items = new List<CreateOrderItemRequest>() // Empty!
        };

        var response = await client.PostAsJsonAsync("/api/orders", emptyOrderRequest);
        var result = await DeserializeResponse<OrderDto>(response);
        
        result.Success.Should().BeFalse(because: "Empty orders should be rejected");
        result.Message.Should().Contain("فارغ", because: "Error should mention empty order");
    }

    /// <summary>
    /// Additional test: Verify negative quantity is rejected
    /// </summary>
    [Fact]
    public async Task CreateOrder_WithNegativeQuantity_ShouldFail()
    {
        var testData = await SeedTestDataAsync();
        
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: testData.UserId,
            tenantId: testData.TenantId,
            branchId: testData.BranchId,
            role: "Cashier"
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Open shift first
        await client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest { OpeningBalance = 100m });

        var negativeQuantityRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testData.ActiveProductId, Quantity = -5 }
            }
        };

        var response = await client.PostAsJsonAsync("/api/orders", negativeQuantityRequest);
        var result = await DeserializeResponse<OrderDto>(response);
        
        result.Success.Should().BeFalse(because: "Negative quantity should be rejected");
        result.Message.Should().Contain("أكبر من صفر", because: "Error should mention quantity must be positive");
    }

    /// <summary>
    /// Additional test: Verify insufficient payment is rejected
    /// </summary>
    [Fact]
    public async Task CompleteOrder_WithInsufficientPayment_ShouldFail()
    {
        var testData = await SeedTestDataAsync();
        
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: testData.UserId,
            tenantId: testData.TenantId,
            branchId: testData.BranchId,
            role: "Cashier"
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Open shift
        await client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest { OpeningBalance = 100m });

        // Create order (Total = 114 EGP)
        var orderRequest = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = testData.ActiveProductId, Quantity = 1 }
            }
        };
        var orderResponse = await client.PostAsJsonAsync("/api/orders", orderRequest);
        var orderResult = await DeserializeResponse<OrderDto>(orderResponse);
        var orderId = orderResult.Data!.Id;

        // Try to pay only 50 EGP for 114 EGP order
        var insufficientPayment = new CompleteOrderRequest
        {
            Payments = new List<PaymentRequest>
            {
                new() { Method = "Cash", Amount = 50m }
            }
        };

        var response = await client.PostAsJsonAsync($"/api/orders/{orderId}/complete", insufficientPayment);
        var result = await DeserializeResponse<OrderDto>(response);
        
        result.Success.Should().BeFalse(because: "Insufficient payment should be rejected");
        result.Message.Should().Contain("أقل من", because: "Error should mention payment is less than total");
    }

    /// <summary>
    /// Seeds test data for the shift lifecycle test
    /// </summary>
    private async Task<TestData> SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create Tenant with Tax Settings (14% Tax Enabled)
        var tenant = new Tenant
        {
            Name = "Solid Ground Test Tenant",
            Slug = "solid-ground-" + Guid.NewGuid().ToString()[..8],
            TaxRate = 14m,
            IsTaxEnabled = true,
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Create Branch
        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Test Branch",
            Code = "TST-" + Guid.NewGuid().ToString()[..8],
            Address = "123 Test Street, Cairo",
            Phone = "01000000000",
            DefaultTaxRate = 14m,
            DefaultTaxInclusive = false, // Tax Exclusive
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        // Create Cashier User
        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Test Cashier",
            Email = "cashier-" + Guid.NewGuid().ToString()[..8] + "@kasserpro.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Cashier,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Seed cashier permissions required by service-layer checks
        db.UserPermissions.AddRange(
            new UserPermission { UserId = user.Id, Permission = Permission.OrdersCreate },
            new UserPermission { UserId = user.Id, Permission = Permission.PosCreditSale }
        );
        await db.SaveChangesAsync();

        // Create Category
        var category = new Category
        {
            TenantId = tenant.Id,
            Name = "اختبار التصنيف",
            NameEn = "Test Category",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        // Create Active Product (Price = 100 EGP Net)
        var activeProduct = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = "منتج نشط",
            NameEn = "Active Product",
            Sku = "ACTIVE-" + Guid.NewGuid().ToString()[..8],
            Price = 100m, // Net price (excluding tax)
            Cost = 70m,
            TaxRate = 14m,
            TaxInclusive = false, // Tax Exclusive
            IsActive = true,
            TrackInventory = false
        };
        db.Products.Add(activeProduct);

        // Create Inactive Product
        var inactiveProduct = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = "منتج غير نشط",
            NameEn = "Inactive Product",
            Sku = "INACTIVE-" + Guid.NewGuid().ToString()[..8],
            Price = 200m,
            Cost = 150m,
            TaxRate = 14m,
            TaxInclusive = false,
            IsActive = false, // INACTIVE!
            TrackInventory = false
        };
        db.Products.Add(inactiveProduct);
        await db.SaveChangesAsync();

        return new TestData
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            CategoryId = category.Id,
            ActiveProductId = activeProduct.Id,
            InactiveProductId = inactiveProduct.Id
        };
    }

    private async Task<ApiResponse<T>> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions) 
               ?? new ApiResponse<T> { Success = false, Message = "Failed to deserialize response" };
    }

    private record TestData
    {
        public int TenantId { get; init; }
        public int BranchId { get; init; }
        public int UserId { get; init; }
        public int CategoryId { get; init; }
        public int ActiveProductId { get; init; }
        public int InactiveProductId { get; init; }
    }
}
