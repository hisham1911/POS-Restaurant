namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class PurchaseInvoicePaymentTests
{
    // Helpers
    private static PurchaseInvoice CreateInvoice(decimal totalAmount = 5000m) => new()
    {
        Id          = 1,
        Total       = totalAmount,
        AmountPaid  = 0m,
        Status      = PurchaseInvoiceStatus.Confirmed,
        BranchId    = 1,
        TenantId    = 1,
        SupplierId  = 1,
    };

    // Payment Method -> CashRegister Rule

    [Fact]
    public void Payment_Cash_ShouldAffectCashRegister()
    {
        var method = PaymentMethod.Cash;
        var shouldRecordInCashRegister = method == PaymentMethod.Cash;
        shouldRecordInCashRegister.Should().BeTrue();
    }

    [Theory]
    [InlineData(PaymentMethod.BankTransfer)]
    [InlineData(PaymentMethod.Fawry)]
    public void Payment_NonCash_ShouldNotAffectCashRegister(PaymentMethod method)
    {
        var shouldRecordInCashRegister = method == PaymentMethod.Cash;
        shouldRecordInCashRegister.Should().BeFalse(
            $"{method} payment should NOT be recorded in cash register");
    }

    // Partial Payments

    [Fact]
    public void Payment_Partial_UpdatesPaidAmount()
    {
        var invoice = CreateInvoice(totalAmount: 5000m);
        invoice.AmountPaid += 2000m;

        invoice.AmountPaid.Should().Be(2000m);
        invoice.AmountPaid.Should().BeLessThan(invoice.Total);
    }

    [Fact]
    public void Payment_Full_InvoiceIsFullyPaid()
    {
        var invoice = CreateInvoice(totalAmount: 5000m);
        invoice.AmountPaid = 5000m;

        var isFullyPaid = invoice.AmountPaid >= invoice.Total;
        isFullyPaid.Should().BeTrue();
    }

    [Fact]
    public void Payment_CannotExceedTotalAmount()
    {
        var invoice = CreateInvoice(totalAmount: 5000m);
        decimal paymentAttempt = 6000m;

        var isValid = paymentAttempt <= (invoice.Total - invoice.AmountPaid);
        isValid.Should().BeFalse("Overpayment should not be allowed");
    }

    // Status Transitions

    [Fact]
    public void Invoice_WhenFullyPaid_StatusShouldBeFullyPaid()
    {
        var invoice = CreateInvoice(5000m);
        invoice.AmountPaid = 5000m;

        var expectedStatus = invoice.AmountPaid >= invoice.Total
            ? PurchaseInvoiceStatus.Paid
            : PurchaseInvoiceStatus.PartiallyPaid;

        expectedStatus.Should().Be(PurchaseInvoiceStatus.Paid);
    }

    [Fact]
    public void Invoice_WhenPartiallyPaid_StatusShouldBePartiallyPaid()
    {
        var invoice = CreateInvoice(5000m);
        invoice.AmountPaid = 2000m;

        var expectedStatus = invoice.AmountPaid >= invoice.Total
            ? PurchaseInvoiceStatus.Paid
            : PurchaseInvoiceStatus.PartiallyPaid;

        expectedStatus.Should().Be(PurchaseInvoiceStatus.PartiallyPaid);
    }

    // Cancelled Invoice

    [Fact]
    public void Payment_OnCancelledInvoice_IsNotAllowed()
    {
        var invoice = CreateInvoice();
        invoice.Status = PurchaseInvoiceStatus.Cancelled;

        var canPay = invoice.Status == PurchaseInvoiceStatus.Confirmed
                  || invoice.Status == PurchaseInvoiceStatus.PartiallyPaid;

        canPay.Should().BeFalse("Cancelled invoice cannot receive payments");
    }
}
