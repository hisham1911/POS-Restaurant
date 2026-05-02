namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KasserPro.Application.DTOs.CashRegister;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Shifts;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class CashRegisterIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CashRegisterIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TransferCash_Summary_ShouldSeparateInboundAndOutboundTotals()
    {
        var testData = await SeedCashRegisterDataAsync();
        using var client = CreateAuthenticatedAdminClient(testData.TenantId, testData.AdminUserId, testData.SourceBranchId);

        await OpenShiftAsync(client, 5000m);

        var transferDate = DateTime.UtcNow;
        var transferResponse = await client.PostAsJsonAsync(
            "/api/cash-register/transfer",
            new TransferCashRequest
            {
                SourceBranchId = testData.SourceBranchId,
                TargetBranchId = testData.TargetBranchId,
                Amount = 5000m,
                Description = "Branch transfer summary regression",
                TransactionDate = transferDate
            });

        transferResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeResponse<bool>(transferResponse)).Success.Should().BeTrue();

        var fromDate = Uri.EscapeDataString(transferDate.AddMinutes(-5).ToString("O"));
        var toDate = Uri.EscapeDataString(transferDate.AddMinutes(5).ToString("O"));

        var sourceSummaryResponse = await client.GetAsync(
            $"/api/cash-register/summary?branchId={testData.SourceBranchId}&fromDate={fromDate}&toDate={toDate}");
        sourceSummaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sourceSummary = await DeserializeResponse<CashRegisterSummaryDto>(sourceSummaryResponse);
        sourceSummary.Success.Should().BeTrue(because: sourceSummary.Message);
        sourceSummary.Data.Should().NotBeNull();
        sourceSummary.Data!.TotalTransfersOut.Should().Be(5000m);
        sourceSummary.Data.TotalTransfersIn.Should().Be(0m);

        var targetSummaryResponse = await client.GetAsync(
            $"/api/cash-register/summary?branchId={testData.TargetBranchId}&fromDate={fromDate}&toDate={toDate}");
        targetSummaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var targetSummary = await DeserializeResponse<CashRegisterSummaryDto>(targetSummaryResponse);
        targetSummary.Success.Should().BeTrue(because: targetSummary.Message);
        targetSummary.Data.Should().NotBeNull();
        targetSummary.Data!.TotalTransfersIn.Should().Be(5000m);
        targetSummary.Data.TotalTransfersOut.Should().Be(0m);
    }

    [Fact]
    public async Task CloseShift_ShouldUseCashRegisterBalanceForExpectedBalance()
    {
        var testData = await SeedCashRegisterDataAsync();
        using var client = CreateAuthenticatedAdminClient(testData.TenantId, testData.AdminUserId, testData.SourceBranchId);

        await OpenShiftAsync(client, 500m);

        var withdrawResponse = await client.PostAsJsonAsync(
            "/api/cash-register/withdraw",
            new CreateCashRegisterTransactionRequest
            {
                Amount = 100m,
                Description = "Shift expected balance regression",
                TransactionDate = DateTime.UtcNow
            });

        withdrawResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeResponse<CashRegisterTransactionDto>(withdrawResponse)).Success.Should().BeTrue();

        var closeShiftResponse = await client.PostAsJsonAsync(
            "/api/shifts/close",
            new CloseShiftRequest
            {
                ClosingBalance = 400m,
                Notes = "Close shift expected balance regression"
            });

        closeShiftResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var closeShiftResult = await DeserializeResponse<ShiftDto>(closeShiftResponse);
        closeShiftResult.Success.Should().BeTrue(because: closeShiftResult.Message);
        closeShiftResult.Data.Should().NotBeNull();
        closeShiftResult.Data!.ExpectedBalance.Should().Be(400m);
        closeShiftResult.Data.Difference.Should().Be(0m);
    }

    [Fact]
    public async Task GetCurrentShift_ShouldUseCashRegisterBalanceForOpenShiftExpectedBalance()
    {
        var testData = await SeedCashRegisterDataAsync();
        using var client = CreateAuthenticatedAdminClient(testData.TenantId, testData.AdminUserId, testData.SourceBranchId);

        await OpenShiftAsync(client, 500m);

        var withdrawResponse = await client.PostAsJsonAsync(
            "/api/cash-register/withdraw",
            new CreateCashRegisterTransactionRequest
            {
                Amount = 100m,
                Description = "Open shift expected balance regression",
                TransactionDate = DateTime.UtcNow
            });

        withdrawResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeserializeResponse<CashRegisterTransactionDto>(withdrawResponse)).Success.Should().BeTrue();

        var currentShiftResponse = await client.GetAsync("/api/shifts/current");
        currentShiftResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var currentShiftResult = await DeserializeResponse<ShiftDto>(currentShiftResponse);
        currentShiftResult.Success.Should().BeTrue(because: currentShiftResult.Message);
        currentShiftResult.Data.Should().NotBeNull();
        currentShiftResult.Data!.ExpectedBalance.Should().Be(400m);
    }

    private HttpClient CreateAuthenticatedAdminClient(int tenantId, int userId, int branchId)
    {
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: userId,
            tenantId: tenantId,
            branchId: branchId,
            email: "cash-register-admin@test.com",
            name: "Cash Register Admin",
            role: "Admin");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
        return client;
    }

    private async Task OpenShiftAsync(HttpClient client, decimal openingBalance)
    {
        var response = await client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest
        {
            OpeningBalance = openingBalance
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<ShiftDto>(response);
        result.Success.Should().BeTrue(because: result.Message);
    }

    private async Task<ApiResponse<T>> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
        result.Should().NotBeNull(because: $"Response should deserialize successfully. Raw content: {content}");
        return result!;
    }

    private async Task<(int TenantId, int SourceBranchId, int TargetBranchId, int AdminUserId)> SeedCashRegisterDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = "Cash Register Tenant",
            Slug = "cash-register-" + Guid.NewGuid().ToString()[..8],
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var sourceBranch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Source Branch",
            Code = "SRC-" + Guid.NewGuid().ToString()[..6],
            Address = "1 Source Street",
            Phone = "01000000001",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };

        var targetBranch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Target Branch",
            Code = "TRG-" + Guid.NewGuid().ToString()[..6],
            Address = "2 Target Street",
            Phone = "01000000002",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };

        db.Branches.AddRange(sourceBranch, targetBranch);
        await db.SaveChangesAsync();

        var adminUser = new User
        {
            TenantId = tenant.Id,
            BranchId = sourceBranch.Id,
            Name = "Cash Register Admin",
            Email = "cash-register-admin-" + Guid.NewGuid().ToString()[..8] + "@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        return (tenant.Id, sourceBranch.Id, targetBranch.Id, adminUser.Id);
    }

    // ---- HTTP-based integration tests (added) ----

    [Fact]
    public async Task Transfer_WithoutPermission_Returns403()
    {
        var testData = await SeedCashRegisterDataAsync();
        var cashier = await CreateCashierAsync(testData.TenantId, testData.SourceBranchId);
        using var client = CreateClientWithRole(cashier.Id, testData.TenantId, testData.SourceBranchId, "Cashier");

        var response = await client.PostAsJsonAsync("/api/cash-register/transfer", new
        {
            sourceBranchId  = testData.SourceBranchId,
            targetBranchId  = testData.TargetBranchId,
            amount          = 500,
            transactionDate = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Transfer_ValidRequest_CreatesTwoLinkedTransactions()
    {
        var testData = await SeedCashRegisterDataAsync();
        using var client = CreateAuthenticatedAdminClient(testData.TenantId, testData.AdminUserId, testData.SourceBranchId);

        await OpenShiftAsync(client, 5000m);

        var response = await client.PostAsJsonAsync("/api/cash-register/transfer", new TransferCashRequest
        {
            SourceBranchId = testData.SourceBranchId,
            TargetBranchId = testData.TargetBranchId,
            Amount = 1000m,
            Description = "تحويل تست",
            TransactionDate = DateTime.UtcNow
        });

        response.EnsureSuccessStatusCode();

        var txBranch1 = await client.GetAsync($"/api/cash-register/transactions?branchId={testData.SourceBranchId}");
        var result1   = await DeserializeResponse<PagedResult<CashRegisterTransactionDto>>(txBranch1);
        result1!.Data!.Items.Should().Contain(t =>
            t.Type == CashRegisterTransactionType.Transfer.ToString() &&
            t.Amount == 1000m);

        var txBranch2 = await client.GetAsync($"/api/cash-register/transactions?branchId={testData.TargetBranchId}");
        var result2   = await DeserializeResponse<PagedResult<CashRegisterTransactionDto>>(txBranch2);
        result2!.Data!.Items.Should().Contain(t =>
            t.Type == CashRegisterTransactionType.Transfer.ToString() &&
            t.Amount == 1000m);
    }

    [Fact]
    public async Task Reconcile_WithoutPermission_Returns403()
    {
        var testData = await SeedCashRegisterDataAsync();
        var cashier = await CreateCashierAsync(testData.TenantId, testData.SourceBranchId);
        using var client = CreateClientWithRole(cashier.Id, testData.TenantId, testData.SourceBranchId, "Cashier");

        await OpenShiftAsync(client, 1000m);

        var response = await client.PostAsJsonAsync($"/api/cash-register/reconcile?shiftId=1", new ReconcileCashRegisterRequest
        {
            ActualBalance = 1000m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reconcile_WhenBalanceMatches_ReturnsZeroDifference()
    {
        var testData = await SeedCashRegisterDataAsync();
        using var client = CreateAuthenticatedAdminClient(testData.TenantId, testData.AdminUserId, testData.SourceBranchId);

        await OpenShiftAsync(client, 5000m);

        var withdrawResponse = await client.PostAsJsonAsync(
            "/api/cash-register/withdraw",
            new CreateCashRegisterTransactionRequest
            {
                Amount = 100m,
                Description = "Reconcile test withdrawal",
                TransactionDate = DateTime.UtcNow
            });
        withdrawResponse.EnsureSuccessStatusCode();

        var currentShiftResponse = await client.GetAsync("/api/shifts/current");
        currentShiftResponse.EnsureSuccessStatusCode();
        var currentShift = await DeserializeResponse<ShiftDto>(currentShiftResponse);
        var shiftId = currentShift.Data!.Id;

        var response = await client.PostAsJsonAsync($"/api/cash-register/reconcile?shiftId={shiftId}", new ReconcileCashRegisterRequest
        {
            ActualBalance = 4900m,
        });

        response.EnsureSuccessStatusCode();
        var result = await DeserializeResponse<bool>(response);
        result!.Data.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shift = await db.Shifts.FindAsync(shiftId);
        shift.Should().NotBeNull();
        shift!.Difference.Should().Be(0m);
    }

    private async Task<User> CreateCashierAsync(int tenantId, int branchId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cashier = new User
        {
            TenantId = tenantId,
            BranchId = branchId,
            Name = "Cashier",
            Email = $"cashier-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Cashier,
            IsActive = true
        };
        db.Users.Add(cashier);
        await db.SaveChangesAsync();
        return cashier;
    }

    private HttpClient CreateClientWithRole(int userId, int tenantId, int branchId, string role)
    {
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: userId,
            tenantId: tenantId,
            branchId: branchId,
            email: $"{role.ToLowerInvariant()}-test@test.com",
            name: $"{role} Test User",
            role: role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
        return client;
    }
}
