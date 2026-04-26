namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Expenses;
using KasserPro.Application.Services.Implementations;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Repositories;
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
            int? shiftId = null)
            => throw new NotSupportedException();
    }
}
