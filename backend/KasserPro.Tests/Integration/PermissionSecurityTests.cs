namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class PermissionSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PermissionSecurityTests(CustomWebApplicationFactory factory)
        => _factory = factory;

    // Expenses Endpoints

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public async Task ExpenseApprove_NonAdmin_Returns403(string role)
    {
        var (tenantId, branchId, userId, client) = await SeedUserAndCreateClientAsync(role);
        var response = await client.PostAsync("/api/expenses/1/approve",
            JsonContent.Create(new { notes = "test" }));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExpenseApprove_Admin_DoesNotReturn403()
    {
        var (tenantId, branchId, userId, client) = await SeedUserAndCreateClientAsync("Admin");
        var response = await client.PostAsync("/api/expenses/999/approve",
            JsonContent.Create(new { notes = "test" }));
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // CashRegister Endpoints

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public async Task CashRegisterTransfer_NonAdmin_Returns403(string role)
    {
        var (_, _, _, client) = await SeedUserAndCreateClientAsync(role);
        var response = await client.PostAsJsonAsync("/api/cash-register/transfer",
            new { sourceBranchId = 1, targetBranchId = 2, amount = 100 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public async Task CashRegisterReconcile_NonAdmin_Returns403(string role)
    {
        var (tenantId, _, userId, client) = await SeedUserAndCreateClientAsync(role);
        // Seed open shift so reconcile endpoint gets past missing-shift errors
        await SeedOpenShiftAsync(tenantId, userId);
        var response = await client.PostAsJsonAsync("/api/cash-register/reconcile?shiftId=1",
            new { actualBalance = 1000m });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Tenant Isolation

    [Fact]
    public async Task Transfer_CrossTenant_ReturnsError()
    {
        var (_, _, _, client) = await SeedUserAndCreateClientAsync("Admin");
        var response = await client.PostAsJsonAsync("/api/cash-register/transfer",
            new { sourceBranchId = 1, targetBranchId = 99, amount = 100 });
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    // Unauthenticated

    [Theory]
    [InlineData("/api/expenses")]
    [InlineData("/api/cash-register/balance")]
    [InlineData("/api/purchaseinvoices")]
    public async Task ProtectedEndpoint_NoToken_Returns401(string endpoint)
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync(endpoint);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Helpers

    private async Task<(int TenantId, int BranchId, int UserId, HttpClient Client)> SeedUserAndCreateClientAsync(string role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = $"PermSec Tenant {Guid.NewGuid():N}",
            Slug = $"perm-{Guid.NewGuid():N}",
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "PermSec Branch",
            Code = $"PSB-{Guid.NewGuid().ToString("N")[..4]}",
            Address = "PermSec Street",
            Phone = "01000000044",
            CurrencyCode = "EGP",
            DefaultTaxRate = 14m,
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var userRole = role == "Admin" ? UserRole.Admin : UserRole.Cashier;
        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = $"PermSec {role}",
            Email = $"perm-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = userRole,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: user.Id,
            tenantId: tenant.Id,
            branchId: branch.Id,
            email: user.Email,
            name: user.Name,
            role: role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", branch.Id.ToString());
        return (tenant.Id, branch.Id, user.Id, client);
    }

    private async Task SeedOpenShiftAsync(int tenantId, int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return;

        var shift = new Shift
        {
            TenantId = tenantId,
            BranchId = user.BranchId.Value,
            UserId = userId,
            IsClosed = false,
            OpenedAt = DateTime.UtcNow,
            OpeningBalance = 5000m
        };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();
    }
}
