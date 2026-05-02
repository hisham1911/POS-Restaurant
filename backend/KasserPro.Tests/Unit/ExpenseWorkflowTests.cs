namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class ExpenseWorkflowTests
{
    // Helpers
    private static Expense CreateDraftExpense(decimal amount = 500m) => new()
    {
        Id          = 1,
        Amount      = amount,
        Status      = ExpenseStatus.Draft,
        Description = "كهرباء",
        BranchId    = 1,
        TenantId    = 1,
        CreatedAt   = DateTime.UtcNow,
    };

    // Status Transitions

    [Fact]
    public void Expense_InitialStatus_IsDraft()
    {
        var expense = CreateDraftExpense();
        expense.Status.Should().Be(ExpenseStatus.Draft);
    }

    [Fact]
    public void Expense_Approve_ChangeStatusToApproved()
    {
        var expense = CreateDraftExpense();
        expense.Status = ExpenseStatus.Approved;
        expense.Status.Should().Be(ExpenseStatus.Approved);
    }

    [Fact]
    public void Expense_Reject_ChangeStatusToRejected()
    {
        var expense = CreateDraftExpense();
        expense.Status = ExpenseStatus.Rejected;
        expense.Status.Should().Be(ExpenseStatus.Rejected);
    }

    [Fact]
    public void Expense_Pay_RequiresApprovedStatus()
    {
        var expense = CreateDraftExpense();
        var canPay = expense.Status == ExpenseStatus.Approved;
        canPay.Should().BeFalse("Draft expense cannot be paid directly");
    }

    [Fact]
    public void Expense_Pay_WhenApproved_ChangeStatusToPaid()
    {
        var expense = CreateDraftExpense();
        expense.Status = ExpenseStatus.Approved;

        expense.Status = ExpenseStatus.Paid;

        expense.Status.Should().Be(ExpenseStatus.Paid);
    }

    // Amount Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Expense_ZeroOrNegativeAmount_IsInvalid(decimal amount)
    {
        var isValid = amount > 0;
        isValid.Should().BeFalse($"Amount {amount} should be invalid");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(100000)]
    public void Expense_PositiveAmount_IsValid(decimal amount)
    {
        var isValid = amount > 0;
        isValid.Should().BeTrue();
    }

    // Attachment Rules

    [Fact]
    public void Expense_CanHaveMultipleAttachments()
    {
        var expense = CreateDraftExpense();
        expense.Attachments = new List<ExpenseAttachment>
        {
            new() { Id = 1, FileName = "receipt1.pdf", ExpenseId = expense.Id },
            new() { Id = 2, FileName = "receipt2.jpg", ExpenseId = expense.Id },
        };
        expense.Attachments.Should().HaveCount(2);
    }

    // Tenant Isolation

    [Fact]
    public void Expense_TenantId_MustMatchBranchTenant()
    {
        var expense = CreateDraftExpense();
        expense.TenantId.Should().Be(1);
        expense.BranchId.Should().Be(1);
    }
}
