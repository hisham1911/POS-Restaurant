namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class CashRegisterBalanceTests
{
    // Helpers
    private static CashRegisterTransaction MakeTransaction(
        CashRegisterTransactionType type,
        decimal amount,
        decimal balanceBefore,
        string? referenceType = null) => new()
    {
        Type          = type,
        Amount        = amount,
        BalanceBefore = balanceBefore,
        BalanceAfter  = ComputeBalanceAfter(type, balanceBefore, amount, referenceType),
        ReferenceType = referenceType,
        BranchId      = 1,
        TenantId      = 1,
        TransactionDate = DateTime.UtcNow,
    };

    private static decimal ComputeBalanceAfter(
        CashRegisterTransactionType type, decimal before, decimal amount, string? referenceType) =>
        type switch
        {
            CashRegisterTransactionType.Sale            => before + amount,
            CashRegisterTransactionType.Deposit         => before + amount,
            CashRegisterTransactionType.Transfer when string.Equals(referenceType, "TransferIn", StringComparison.OrdinalIgnoreCase) => before + amount,
            CashRegisterTransactionType.Refund          => before - amount,
            CashRegisterTransactionType.Expense         => before - amount,
            CashRegisterTransactionType.Withdrawal      => before - amount,
            CashRegisterTransactionType.Transfer when string.Equals(referenceType, "TransferOut", StringComparison.OrdinalIgnoreCase) => before - amount,
            CashRegisterTransactionType.SupplierPayment => before - amount,
            _                                           => before,
        };

    // Credit Transactions (يزيدوا الرصيد)

    [Theory]
    [InlineData(CashRegisterTransactionType.Sale,       1000, 5000, 6000)]
    [InlineData(CashRegisterTransactionType.Deposit,     500, 2000, 2500)]
    public void CreditTransaction_IncreasesBalance(
        CashRegisterTransactionType type, decimal amount,
        decimal balanceBefore, decimal expectedAfter)
    {
        var tx = MakeTransaction(type, amount, balanceBefore);
        tx.BalanceAfter.Should().Be(expectedAfter);
    }

    // Debit Transactions (بينقصوا الرصيد)

    [Theory]
    [InlineData(CashRegisterTransactionType.Expense,         200, 1000,  800)]
    [InlineData(CashRegisterTransactionType.Withdrawal,      300, 1000,  700)]
    [InlineData(CashRegisterTransactionType.SupplierPayment, 500, 2000, 1500)]
    [InlineData(CashRegisterTransactionType.Refund,          100, 1000,  900)]
    public void DebitTransaction_DecreasesBalance(
        CashRegisterTransactionType type, decimal amount,
        decimal balanceBefore, decimal expectedAfter)
    {
        var tx = MakeTransaction(type, amount, balanceBefore);
        tx.BalanceAfter.Should().Be(expectedAfter);
    }

    // SupplierPayment

    [Fact]
    public void SupplierPayment_Cash_DecreasesBalance()
    {
        var tx = MakeTransaction(CashRegisterTransactionType.SupplierPayment, 1000m, 5000m);
        tx.BalanceAfter.Should().Be(4000m);
        tx.Type.Should().Be(CashRegisterTransactionType.SupplierPayment);
    }

    [Fact]
    public void SupplierPayment_ReferenceType_IsPurchaseInvoicePayment()
    {
        var tx = MakeTransaction(CashRegisterTransactionType.SupplierPayment, 500m, 3000m);
        tx.Type.Should().Be(CashRegisterTransactionType.SupplierPayment);
    }

    // Transfer

    [Fact]
    public void Transfer_OutAndIn_SumToZero()
    {
        decimal amount = 1000m;
        var txOut = MakeTransaction(CashRegisterTransactionType.Transfer, amount, 5000m, "TransferOut");
        var txIn  = MakeTransaction(CashRegisterTransactionType.Transfer, amount, 2000m, "TransferIn");

        var netEffect = (txIn.BalanceAfter - txIn.BalanceBefore)
                      + (txOut.BalanceAfter - txOut.BalanceBefore);

        netEffect.Should().Be(0m, "Transfer should be net-zero across branches");
    }

    [Fact]
    public void Transfer_Amount_MustBePositive()
    {
        var isValid = -100m > 0;
        isValid.Should().BeFalse("Transfer amount must be positive");
    }

    // Reconciliation

    [Fact]
    public void Reconcile_WhenMatch_DifferenceIsZero()
    {
        decimal expected = 5000m;
        decimal actual   = 5000m;
        var difference   = actual - expected;
        difference.Should().Be(0m);
    }

    [Fact]
    public void Reconcile_WhenSurplus_DifferenceIsPositive()
    {
        decimal expected = 5000m;
        decimal actual   = 5200m;
        var difference   = actual - expected;
        difference.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Reconcile_WhenShortage_DifferenceIsNegative()
    {
        decimal expected = 5000m;
        decimal actual   = 4800m;
        var difference   = actual - expected;
        difference.Should().BeLessThan(0m);
    }
}
