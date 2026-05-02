namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using KasserPro.Application.DTOs.CashRegister;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Expenses;
using KasserPro.Application.Services.Implementations;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class ExpenseIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ExpenseIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnTotalAmountAcrossAllMatchingExpenses()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (tenant, branch, user, category) = await SeedExpenseContextAsync(db, "expense-total");

        db.Expenses.AddRange(
            CreateExpense(tenant.Id, branch.Id, category.Id, user.Id, 20m, "EXP-TOTAL-001"),
            CreateExpense(tenant.Id, branch.Id, category.Id, user.Id, 30m, "EXP-TOTAL-002"),
            CreateExpense(tenant.Id, branch.Id, category.Id, user.Id, 50m, "EXP-TOTAL-003"));

        db.Expenses.Add(CreateExpense(tenant.Id, branch.Id, category.Id, user.Id, 999m, "EXP-TOTAL-004", ExpenseStatus.Draft));

        var otherBranch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Other Expense Branch",
            Code = "OEB-" + Guid.NewGuid().ToString("N")[..4],
            Address = "Other Street",
            Phone = "01000000044",
            CurrencyCode = "EGP",
            DefaultTaxRate = 14m,
            IsActive = true
        };
        db.Branches.Add(otherBranch);
        await db.SaveChangesAsync();

        db.Expenses.Add(CreateExpense(tenant.Id, otherBranch.Id, category.Id, user.Id, 75m, "EXP-TOTAL-005"));
        await db.SaveChangesAsync();

        var service = CreateService(db, tenant.Id, branch.Id, user.Id);

        var result = await service.GetAllAsync(
            status: ExpenseStatus.Paid,
            pageNumber: 1,
            pageSize: 2);

        result.Success.Should().BeTrue(because: result.Message);
        result.Data.Should().NotBeNull();
        result.Data!.TotalCount.Should().Be(3);
        result.Data.TotalAmount.Should().Be(100m);
        result.Data.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectNonPositiveAmount()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (tenant, branch, user, category) = await SeedExpenseContextAsync(db, "expense-invalid");
        var service = CreateService(db, tenant.Id, branch.Id, user.Id);

        var result = await service.CreateAsync(new CreateExpenseRequest
        {
            CategoryId = category.Id,
            Amount = 0m,
            Description = "Invalid expense",
            ExpenseDate = DateTime.UtcNow.Date
        });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("EXPENSE_INVALID_AMOUNT");
        result.Message.Should().Be("مبلغ المصروف يجب أن يكون أكبر من صفر");
    }

    private static ExpenseService CreateService(AppDbContext db, int tenantId, int branchId, int userId)
        => new(
            new UnitOfWork(db),
            new TestCurrentUserService
            {
                TenantId = tenantId,
                BranchId = branchId,
                UserId = userId,
                Email = "expense-tests@test.com"
            },
            new NoOpCashRegisterService(),
            NullLogger<ExpenseService>.Instance);

    private static Expense CreateExpense(
        int tenantId,
        int branchId,
        int categoryId,
        int userId,
        decimal amount,
        string expenseNumber,
        ExpenseStatus status = ExpenseStatus.Paid)
        => new()
        {
            TenantId = tenantId,
            BranchId = branchId,
            CategoryId = categoryId,
            ExpenseNumber = expenseNumber,
            Amount = amount,
            ExpenseDate = DateTime.UtcNow.Date,
            Description = "Seeded expense",
            Status = status,
            CreatedByUserId = userId,
            CreatedByUserName = "Expense Tester"
        };

    private static async Task<(Tenant Tenant, Branch Branch, User User, ExpenseCategory Category)> SeedExpenseContextAsync(
        AppDbContext db,
        string slugPrefix)
    {
        var tenant = new Tenant
        {
            Name = $"Expense Tenant {slugPrefix}",
            Slug = $"{slugPrefix}-{Guid.NewGuid():N}",
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = $"Expense Branch {slugPrefix}",
            Code = slugPrefix[..Math.Min(slugPrefix.Length, 3)].ToUpperInvariant() + "-" + Guid.NewGuid().ToString("N")[..4],
            Address = "Expense Street",
            Phone = "01000000033",
            CurrencyCode = "EGP",
            DefaultTaxRate = 14m,
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Expense Admin",
            Email = $"expense-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(user);

        var category = new ExpenseCategory
        {
            TenantId = tenant.Id,
            Name = $"Expense Category {slugPrefix}",
            IsActive = true
        };
        db.ExpenseCategories.Add(category);

        await db.SaveChangesAsync();
        return (tenant, branch, user, category);
    }

    // ---- HTTP-based integration tests (added) ----

    [Fact]
    public async Task ApproveExpense_WithoutPermission_Returns403()
    {
        var data = await SeedExpenseHttpDataAsync("Cashier", new[] { "ExpensesView", "ExpensesCreate" });
        var expenseId = await CreateDraftExpenseDirectlyAsync(data);
        var response = await data.Client.PostAsync($"/api/expenses/{expenseId}/approve",
            JsonContent.Create(new ApproveExpenseRequest { Notes = "approved" }));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApproveExpense_WithPermission_Returns200()
    {
        var data = await SeedExpenseHttpDataAsync("Admin", new[] { "ExpensesView", "ExpensesCreate", "ExpensesApprove" });
        var expenseId = await CreateDraftExpenseDirectlyAsync(data);
        var response = await data.Client.PostAsync($"/api/expenses/{expenseId}/approve",
            JsonContent.Create(new ApproveExpenseRequest { Notes = "approved" }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PayExpense_Cash_RecordsInCashRegister()
    {
        var data = await SeedExpenseHttpDataAsync("Admin", new[] { "ExpensesView", "ExpensesCreate", "ExpensesApprove" });
        var expenseId = await CreateDraftExpenseDirectlyAsync(data);

        var approveResponse = await data.Client.PostAsync($"/api/expenses/{expenseId}/approve",
            JsonContent.Create(new ApproveExpenseRequest { Notes = "approved" }));
        approveResponse.EnsureSuccessStatusCode();

        var payResponse = await data.Client.PostAsync($"/api/expenses/{expenseId}/pay",
            JsonContent.Create(new PayExpenseRequest { PaymentMethod = PaymentMethod.Cash, PaymentDate = DateTime.UtcNow }));
        payResponse.EnsureSuccessStatusCode();

        var txResponse = await data.Client.GetAsync("/api/cash-register/transactions");
        var transactions = await txResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CashRegisterTransactionDto>>>();
        transactions!.Data!.Items.Should().Contain(t =>
            t.Type == CashRegisterTransactionType.Expense.ToString() &&
            t.Amount == 100m);
    }

    [Fact]
    public async Task RejectExpense_WithValidReason_ChangesStatusToRejected()
    {
        var data = await SeedExpenseHttpDataAsync("Admin", new[] { "ExpensesView", "ExpensesCreate", "ExpensesApprove" });
        var expenseId = await CreateDraftExpenseDirectlyAsync(data);

        var response = await data.Client.PostAsync($"/api/expenses/{expenseId}/reject",
            JsonContent.Create(new RejectExpenseRequest { RejectionReason = "غير مبرر" }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ExpenseDto>>();
        result!.Data!.Status.Should().Be(ExpenseStatus.Rejected.ToString());
    }

    private async Task<ExpenseHttpData> SeedExpenseHttpDataAsync(string role, string[] permissions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = $"Expense HTTP Tenant {Guid.NewGuid():N}",
            Slug = $"exp-http-{Guid.NewGuid():N}",
            IsActive = true,
            TaxRate = 14m,
            IsTaxEnabled = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Expense HTTP Branch",
            Code = $"EHB-{Guid.NewGuid().ToString("N")[..4]}",
            Address = "Expense Street",
            Phone = "01000000033",
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
            Name = $"Expense {role}",
            Email = $"expense-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = userRole,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var category = new ExpenseCategory
        {
            TenantId = tenant.Id,
            Name = "Expense HTTP Category",
            IsActive = true
        };
        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync();

        var shift = new Shift
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            IsClosed = false,
            OpenedAt = DateTime.UtcNow,
            OpeningBalance = 5000m
        };
        db.Shifts.Add(shift);

        db.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            TransactionNumber = $"CR-SEED-{Guid.NewGuid().ToString("N")[..6]}",
            Type = CashRegisterTransactionType.Opening,
            Amount = 5000m,
            BalanceBefore = 0,
            BalanceAfter = 5000m,
            TransactionDate = DateTime.UtcNow,
            Description = "Seed opening balance",
            UserId = user.Id,
            UserName = user.Name
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: user.Id,
            tenantId: tenant.Id,
            branchId: branch.Id,
            email: user.Email,
            name: user.Name,
            role: role,
            permissions: permissions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Branch-Id", branch.Id.ToString());

        return new ExpenseHttpData(tenant.Id, branch.Id, user.Id, category.Id, client);
    }

    private async Task<int> CreateDraftExpenseDirectlyAsync(ExpenseHttpData data)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.UserId == data.UserId && !s.IsClosed);
        shift.Should().NotBeNull("An open shift must exist for the test user");

        var expense = new Expense
        {
            TenantId = data.TenantId,
            BranchId = data.BranchId,
            ExpenseNumber = $"EXP-TEST-{Guid.NewGuid().ToString("N")[..8]}",
            CategoryId = data.CategoryId,
            Amount = 100m,
            Description = "Test expense",
            ExpenseDate = DateTime.UtcNow,
            Status = ExpenseStatus.Draft,
            ShiftId = shift!.Id,
            CreatedByUserId = data.UserId,
            CreatedByUserName = "Test User"
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return expense.Id;
    }

    private record ExpenseHttpData(int TenantId, int BranchId, int UserId, int CategoryId, HttpClient Client);

    private sealed class NoOpCashRegisterService : ICashRegisterService
    {
        public Task<ApiResponse<KasserPro.Application.DTOs.CashRegister.CashRegisterBalanceDto>> GetCurrentBalanceAsync(int branchId)
            => throw new NotSupportedException();

        public Task<ApiResponse<PagedResult<KasserPro.Application.DTOs.CashRegister.CashRegisterTransactionDto>>> GetTransactionsAsync(
            int? branchId = null,
            CashRegisterTransactionType? type = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? shiftId = null,
            int pageNumber = 1,
            int pageSize = 20)
            => throw new NotSupportedException();

        public Task<ApiResponse<KasserPro.Application.DTOs.CashRegister.CashRegisterTransactionDto>> CreateTransactionAsync(
            KasserPro.Application.DTOs.CashRegister.CreateCashRegisterTransactionRequest request)
            => throw new NotSupportedException();

        public Task<ApiResponse<bool>> ReconcileAsync(
            int shiftId,
            KasserPro.Application.DTOs.CashRegister.ReconcileCashRegisterRequest request)
            => throw new NotSupportedException();

        public Task<ApiResponse<bool>> TransferCashAsync(KasserPro.Application.DTOs.CashRegister.TransferCashRequest request)
            => throw new NotSupportedException();

        public Task<ApiResponse<KasserPro.Application.DTOs.CashRegister.CashRegisterSummaryDto>> GetSummaryAsync(
            int branchId,
            DateTime fromDate,
            DateTime toDate)
            => throw new NotSupportedException();

        public Task RecordTransactionAsync(
            CashRegisterTransactionType type,
            decimal amount,
            string description,
            string? referenceType = null,
            int? referenceId = null,
            int? shiftId = null,
            int? branchId = null)
            => throw new NotSupportedException();
    }
}
