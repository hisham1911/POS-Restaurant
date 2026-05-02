namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Customers;
using KasserPro.Application.DTOs.Shifts;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class CustomerDebtPaymentIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CustomerDebtPaymentIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PayDebt_WithoutOpenShift_ShouldReturnNoOpenShift()
    {
        var testData = await SeedCustomerDebtPaymentDataAsync();
        using var client = CreateAuthenticatedAdminClient(testData);

        var response = await client.PostAsJsonAsync(
            $"/api/customers/{testData.CustomerId}/pay-debt",
            new PayDebtRequest
            {
                Amount = 250m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "No open shift regression"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await DeserializeResponse<PayDebtResponse>(response);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_OPEN_SHIFT");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var debtPayments = await db.DebtPayments
            .Where(dp => dp.TenantId == testData.TenantId && dp.CustomerId == testData.CustomerId)
            .ToListAsync();

        debtPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task PayDebt_WithOpenShift_ShouldPersistPaymentAndLinkCashFlowToShift()
    {
        var testData = await SeedCustomerDebtPaymentDataAsync();
        using var client = CreateAuthenticatedAdminClient(testData);

        var openShiftResponse = await client.PostAsJsonAsync(
            "/api/shifts/open",
            new OpenShiftRequest { OpeningBalance = 300m });

        openShiftResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var openShiftResult = await DeserializeResponse<ShiftDto>(openShiftResponse);
        openShiftResult.Success.Should().BeTrue(because: openShiftResult.Message);
        openShiftResult.Data.Should().NotBeNull();

        var shiftId = openShiftResult.Data!.Id;

        var payDebtResponse = await client.PostAsJsonAsync(
            $"/api/customers/{testData.CustomerId}/pay-debt",
            new PayDebtRequest
            {
                Amount = 250m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "Open shift success regression"
            });

        payDebtResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payDebtResult = await DeserializeResponse<PayDebtResponse>(payDebtResponse);
        payDebtResult.Success.Should().BeTrue(because: payDebtResult.Message);
        payDebtResult.Data.Should().NotBeNull();
        payDebtResult.Data!.BalanceBefore.Should().Be(1000m);
        payDebtResult.Data.BalanceAfter.Should().Be(750m);
        payDebtResult.Data.RemainingDebt.Should().Be(750m);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.SingleAsync(c => c.Id == testData.CustomerId);
        customer.TotalDue.Should().Be(750m);

        var debtPayment = await db.DebtPayments
            .SingleAsync(dp => dp.Id == payDebtResult.Data.PaymentId);
        debtPayment.Amount.Should().Be(250m);
        debtPayment.ShiftId.Should().Be(shiftId);
        debtPayment.RecordedByUserId.Should().Be(testData.AdminUserId);

        var cashTransaction = await db.CashRegisterTransactions
            .SingleAsync(t => t.ReferenceType == "DebtPayment" && t.ReferenceId == debtPayment.Id);
        cashTransaction.Type.Should().Be(CashRegisterTransactionType.DebtPayment);
        cashTransaction.Amount.Should().Be(250m);
        cashTransaction.ShiftId.Should().Be(shiftId);
    }

    private HttpClient CreateAuthenticatedAdminClient(
        (int TenantId, int BranchId, int AdminUserId, int CustomerId) testData)
    {
        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: testData.AdminUserId,
            tenantId: testData.TenantId,
            branchId: testData.BranchId,
            email: "debt-admin@test.com",
            name: "Debt Admin",
            role: "Admin");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", testData.BranchId.ToString());
        return client;
    }

    private async Task<ApiResponse<T>> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
        result.Should().NotBeNull(because: $"Response should deserialize successfully. Raw content: {content}");
        return result!;
    }

    private async Task<(int TenantId, int BranchId, int AdminUserId, int CustomerId)> SeedCustomerDebtPaymentDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = "Customer Debt Tenant",
            Slug = "customer-debt-" + Guid.NewGuid().ToString()[..8],
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Debt Branch",
            Code = "DBT-" + Guid.NewGuid().ToString()[..6],
            Address = "123 Debt Street",
            Phone = "01000000001",
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var adminUser = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Debt Admin",
            Email = "debt-admin-" + Guid.NewGuid().ToString()[..8] + "@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        var customer = new Customer
        {
            TenantId = tenant.Id,
            Phone = "010" + Random.Shared.Next(10000000, 99999999),
            Name = "Debt Customer",
            IsActive = true,
            TotalDue = 1000m,
            CreditLimit = 2000m
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (tenant.Id, branch.Id, adminUser.Id, customer.Id);
    }
}
