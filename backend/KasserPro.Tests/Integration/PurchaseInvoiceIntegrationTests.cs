namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.PurchaseInvoices;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class PurchaseInvoiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PurchaseInvoiceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatePurchaseInvoice_ShouldPersistTotalIncludingTenantTax()
    {
        var testData = await SeedPurchaseInvoiceDataAsync(14m);
        using var client = CreateAuthenticatedAdminClient(testData);

        var createRequest = new CreatePurchaseInvoiceRequest
        {
            SupplierId = testData.SupplierId,
            InvoiceDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseInvoiceItemRequest>
            {
                new() { ProductId = testData.ProductId, Quantity = 1, PurchasePrice = 500m, SellingPrice = 650m },
                new() { ProductId = testData.ProductId, Quantity = 1, PurchasePrice = 500m, SellingPrice = 650m }
            },
            Notes = "Tax preview regression"
        };

        var response = await client.PostAsJsonAsync("/api/purchaseinvoices", createRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await DeserializeResponse<PurchaseInvoiceDto>(response);
        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();

        var invoice = result.Data!;
        invoice.Subtotal.Should().Be(1000m);
        invoice.TaxRate.Should().Be(14m);
        invoice.TaxAmount.Should().Be(140m);
        invoice.Total.Should().Be(1140m);
        invoice.AmountDue.Should().Be(1140m);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedInvoice = await db.PurchaseInvoices.SingleAsync(pi => pi.Id == invoice.Id);

        persistedInvoice.Subtotal.Should().Be(1000m);
        persistedInvoice.TaxRate.Should().Be(14m);
        persistedInvoice.TaxAmount.Should().Be(140m);
        persistedInvoice.Total.Should().Be(1140m);
        persistedInvoice.AmountDue.Should().Be(1140m);
    }

    [Fact]
    public async Task PreparePurchaseInvoice_ShouldReturnBackendCalculatedTotals()
    {
        var testData = await SeedPurchaseInvoiceDataAsync(14m);
        using var client = CreateAuthenticatedAdminClient(testData);

        var request = new CreatePurchaseInvoiceRequest
        {
            SupplierId = testData.SupplierId,
            InvoiceDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseInvoiceItemRequest>
            {
                new() { ProductId = testData.ProductId, Quantity = 2, PurchasePrice = 250m, SellingPrice = 350m },
                new() { ProductId = testData.ProductId, Quantity = 1, PurchasePrice = 500m, SellingPrice = 650m }
            },
            Notes = "Prepare preview regression"
        };

        var response = await client.PostAsJsonAsync("/api/purchaseinvoices/prepare", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<PurchaseInvoicePreviewDto>(response);
        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();
        result.Data!.Subtotal.Should().Be(1000m);
        result.Data.TaxRate.Should().Be(14m);
        result.Data.TaxAmount.Should().Be(140m);
        result.Data.Total.Should().Be(1140m);
    }

    [Fact]
    public async Task CreatePurchaseInvoice_ShouldNotReuseNumberFromSoftDeletedDraft()
    {
        var testData = await SeedPurchaseInvoiceDataAsync(0m);
        using var client = CreateAuthenticatedAdminClient(testData);

        var firstInvoice = await CreateInvoiceAsync(client, testData.SupplierId, testData.ProductId, 250m);

        var deleteResponse = await client.DeleteAsync($"/api/purchaseinvoices/{firstInvoice.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondInvoice = await CreateInvoiceAsync(client, testData.SupplierId, testData.ProductId, 300m);

        secondInvoice.InvoiceNumber.Should().NotBe(firstInvoice.InvoiceNumber);
        secondInvoice.InvoiceNumber.Should().EndWith("0002");
    }

    [Fact]
    public async Task PurchaseInvoicePayments_ShouldRecalculateStatusAcrossFullDeleteAndPartialFlows()
    {
        var testData = await SeedPurchaseInvoiceDataAsync(0m);
        using var client = CreateAuthenticatedAdminClient(testData);

        var invoice = await CreateInvoiceAsync(client, testData.SupplierId, testData.ProductId, 1000m);
        await ConfirmInvoiceAsync(client, invoice.Id);
        var fullPaymentAmount = invoice.Total;
        var partialPaymentAmount = Math.Round(invoice.Total / 2m, 2);

        var fullPaymentResponse = await client.PostAsJsonAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments",
            new AddPaymentRequest
            {
                Amount = fullPaymentAmount,
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Cash,
                Notes = "Full payment"
            });

        fullPaymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var fullPaymentResult = await DeserializeResponse<PurchaseInvoicePaymentDto>(fullPaymentResponse);
        fullPaymentResult.Success.Should().BeTrue(because: fullPaymentResult.Message);
        fullPaymentResult.Data.Should().NotBeNull();

        var afterFullPayment = await GetInvoiceAsync(client, invoice.Id);
        afterFullPayment.Status.Should().Be("Paid");
        afterFullPayment.AmountPaid.Should().Be(fullPaymentAmount);
        afterFullPayment.AmountDue.Should().Be(0m);

        var deletePaymentResponse = await client.DeleteAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments/{fullPaymentResult.Data!.Id}");

        deletePaymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterDelete = await GetInvoiceAsync(client, invoice.Id);
        afterDelete.Status.Should().Be("Confirmed",
            because: "the real enum has Confirmed/Paid/PartiallyPaid and does not contain Pending");
        afterDelete.AmountPaid.Should().Be(0m);
        afterDelete.AmountDue.Should().Be(invoice.Total);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedAfterDelete = await db.PurchaseInvoices.SingleAsync(pi => pi.Id == invoice.Id);
            persistedAfterDelete.Status.Should().Be(PurchaseInvoiceStatus.Confirmed);
            persistedAfterDelete.AmountPaid.Should().Be(0m);
            persistedAfterDelete.AmountDue.Should().Be(invoice.Total);
        }

        var partialPaymentResponse = await client.PostAsJsonAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments",
            new AddPaymentRequest
            {
                Amount = partialPaymentAmount,
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Cash,
                Notes = "Partial payment"
            });

        partialPaymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var afterPartialPayment = await GetInvoiceAsync(client, invoice.Id);
        afterPartialPayment.Status.Should().Be("PartiallyPaid");
        afterPartialPayment.AmountPaid.Should().Be(partialPaymentAmount);
        afterPartialPayment.AmountDue.Should().Be(Math.Round(invoice.Total - partialPaymentAmount, 2));

        var finalPaymentResponse = await client.PostAsJsonAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments",
            new AddPaymentRequest
            {
                Amount = Math.Round(invoice.Total - partialPaymentAmount, 2),
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Cash,
                Notes = "Final payment"
            });

        finalPaymentResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "PartiallyPaid invoices should still accept the remaining payment");

        var afterFinalPayment = await GetInvoiceAsync(client, invoice.Id);
        afterFinalPayment.Status.Should().Be("Paid");
        afterFinalPayment.AmountPaid.Should().Be(invoice.Total);
        afterFinalPayment.AmountDue.Should().Be(0m);
    }

    [Fact]
    public async Task DeletePayment_ForCancelledPurchaseInvoice_ShouldBeRejected()
    {
        var testData = await SeedPurchaseInvoiceDataAsync(0m);
        using var client = CreateAuthenticatedAdminClient(testData);

        var invoice = await CreateInvoiceAsync(client, testData.SupplierId, testData.ProductId, 1000m);
        await ConfirmInvoiceAsync(client, invoice.Id);

        var paymentResponse = await client.PostAsJsonAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments",
            new AddPaymentRequest
            {
                Amount = 250m,
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Cash,
                Notes = "Payment before cancellation"
            });

        var paymentResult = await DeserializeResponse<PurchaseInvoicePaymentDto>(paymentResponse);
        paymentResult.Success.Should().BeTrue();
        paymentResult.Data.Should().NotBeNull();

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/purchaseinvoices/{invoice.Id}/cancel",
            new CancelInvoiceRequest
            {
                Reason = "Cancellation guard regression",
                AdjustInventory = false
            });

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments/{paymentResult.Data!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var deleteResult = await DeserializeResponse<bool>(deleteResponse);
        deleteResult.Success.Should().BeFalse();
        deleteResult.ErrorCode.Should().Be("PURCHASE_INVOICE_NOT_EDITABLE");
    }

    [Fact]
    public async Task PurchaseInvoiceLifecycle_ShouldKeepSupplierTotalDueInSync()
    {
        var testData = await SeedPurchaseInvoiceDataAsync(0m);
        using var client = CreateAuthenticatedAdminClient(testData);

        var invoice = await CreateInvoiceAsync(client, testData.SupplierId, testData.ProductId, 1000m);
        var invoiceTotal = invoice.AmountDue;

        await ConfirmInvoiceAsync(client, invoice.Id);
        await AssertSupplierTotalDueAsync(testData.SupplierId, invoiceTotal);

        var addPaymentResponse = await client.PostAsJsonAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments",
            new AddPaymentRequest
            {
                Amount = 400m,
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Cash,
                Notes = "Supplier balance sync test"
            });

        addPaymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var addPaymentResult = await DeserializeResponse<PurchaseInvoicePaymentDto>(addPaymentResponse);
        addPaymentResult.Success.Should().BeTrue(because: addPaymentResult.Message);
        addPaymentResult.Data.Should().NotBeNull();

        await AssertSupplierTotalDueAsync(testData.SupplierId, Math.Round(invoiceTotal - 400m, 2));

        var deletePaymentResponse = await client.DeleteAsync(
            $"/api/purchaseinvoices/{invoice.Id}/payments/{addPaymentResult.Data!.Id}");

        deletePaymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertSupplierTotalDueAsync(testData.SupplierId, invoiceTotal);

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/purchaseinvoices/{invoice.Id}/cancel",
            new CancelInvoiceRequest
            {
                Reason = "Supplier debt rollback test",
                AdjustInventory = false
            });

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertSupplierTotalDueAsync(testData.SupplierId, 0m);
    }

    [Fact]
    public async Task ConfirmPurchaseInvoice_ShouldRoundWeightedAverageCostToFourDecimals()
    {
        var testData = await SeedPurchaseInvoiceDataAsync(0m);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.SingleAsync(p => p.Id == testData.ProductId);
            product.AverageCost = 10m;
            db.BranchInventories.Add(new BranchInventory
            {
                TenantId = testData.TenantId,
                BranchId = testData.BranchId,
                ProductId = testData.ProductId,
                Quantity = 3,
                ReorderLevel = 1,
                LastUpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedAdminClient(testData);
        var invoice = await CreateInvoiceAsync(client, testData.SupplierId, testData.ProductId, 10.01m);
        await ConfirmInvoiceAsync(client, invoice.Id);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedProduct = await verificationDb.Products.SingleAsync(p => p.Id == testData.ProductId);

        updatedProduct.AverageCost.Should().Be(10.0025m);
    }

    private HttpClient CreateAuthenticatedAdminClient(
        (int TenantId, int BranchId, int AdminUserId, int SupplierId, int ProductId) testData)
    {
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: testData.AdminUserId,
            tenantId: testData.TenantId,
            branchId: testData.BranchId,
            email: "admin@purchase-invoice.test",
            name: "Purchase Invoice Admin",
            role: "Admin");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", testData.BranchId.ToString());
        return client;
    }

    private async Task<PurchaseInvoiceDto> CreateInvoiceAsync(
        HttpClient client,
        int supplierId,
        int productId,
        decimal purchasePrice)
    {
        var response = await client.PostAsJsonAsync(
            "/api/purchaseinvoices",
            new CreatePurchaseInvoiceRequest
            {
                SupplierId = supplierId,
                InvoiceDate = DateTime.UtcNow,
                Items = new List<CreatePurchaseInvoiceItemRequest>
                {
                    new()
                    {
                        ProductId = productId,
                        Quantity = 1,
                        PurchasePrice = purchasePrice,
                        SellingPrice = purchasePrice + 100m
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await DeserializeResponse<PurchaseInvoiceDto>(response);
        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();

        return result.Data!;
    }

    private async Task ConfirmInvoiceAsync(HttpClient client, int invoiceId)
    {
        var response = await client.PostAsync($"/api/purchaseinvoices/{invoiceId}/confirm", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<PurchaseInvoiceDto>(response);
        result.Success.Should().BeTrue(because: result.Message);
    }

    private async Task<PurchaseInvoiceDto> GetInvoiceAsync(HttpClient client, int invoiceId)
    {
        var response = await client.GetAsync($"/api/purchaseinvoices/{invoiceId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<PurchaseInvoiceDto>(response);
        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();

        return result.Data!;
    }

    private async Task<ApiResponse<T>> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
        result.Should().NotBeNull(because: $"Response should deserialize successfully. Raw content: {content}");
        return result!;
    }

    private async Task AssertSupplierTotalDueAsync(int supplierId, decimal expectedTotalDue)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var supplier = await db.Suppliers.SingleAsync(s => s.Id == supplierId);
        supplier.TotalDue.Should().Be(expectedTotalDue);
    }

    private async Task<(int TenantId, int BranchId, int AdminUserId, int SupplierId, int ProductId)> SeedPurchaseInvoiceDataAsync(decimal taxRate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = "Purchase Invoice Tenant",
            Slug = "purchase-invoice-" + Guid.NewGuid().ToString()[..8],
            IsActive = true,
            TaxRate = taxRate,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Purchase Branch",
            Code = "PIB-" + Guid.NewGuid().ToString()[..6],
            Address = "123 Purchase Street",
            Phone = "01000000000",
            DefaultTaxRate = taxRate,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var adminUser = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Purchase Admin",
            Email = "purchase-admin-" + Guid.NewGuid().ToString()[..8] + "@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(adminUser);

        var category = new Category
        {
            TenantId = tenant.Id,
            Name = "Purchase Category",
            NameEn = "Purchase Category",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var supplier = new Supplier
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Purchase Supplier",
            Phone = "01111111111",
            IsActive = true
        };
        db.Suppliers.Add(supplier);

        var product = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = "Purchase Product",
            NameEn = "Purchase Product",
            Sku = "PUR-" + Guid.NewGuid().ToString()[..8],
            Price = 1500m,
            Cost = 900m,
            TaxRate = null,
            TaxInclusive = false,
            IsActive = true,
            Type = ProductType.Physical,
            TrackInventory = true
        };
        db.Products.Add(product);

        await db.SaveChangesAsync();

        return (tenant.Id, branch.Id, adminUser.Id, supplier.Id, product.Id);
    }
}
