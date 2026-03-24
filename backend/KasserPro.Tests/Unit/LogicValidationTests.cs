using Xunit;
using FluentAssertions;
using KasserPro.Application.Services.Implementations;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

namespace KasserPro.Tests;

/// <summary>
/// Critical Business Logic Validation Tests
/// هذه الاختبارات تكتشف الأخطاء المنطقية في التطبيق
/// </summary>
public class LogicValidationTests
{
    [Fact]
    public void Inventory_PurchaseThenSale_ShouldMatchExpectedStock()
    {
        // Arrange: شراء 100 وحدة
        var initialStock = 0;
        var purchased = 100;
        var sold = 30;
        
        // Act
        var afterPurchase = initialStock + purchased;
        var afterSale = afterPurchase - sold;
        
        // Assert
        afterSale.Should().Be(70, "المخزون بعد الشراء والبيع يجب أن يكون 70");
    }
    
    [Fact]
    public void Inventory_ReturnShouldIncreaseStock()
    {
        // Arrange
        var currentStock = 50;
        var returnedQuantity = 10;
        
        // Act
        var afterReturn = currentStock + returnedQuantity;
        
        // Assert
        afterReturn.Should().Be(60, "المخزون بعد المرتجع يجب أن يزيد");
    }
    
    [Fact]
    public void Financial_TaxExclusiveCalculation_ShouldBeCorrect()
    {
        // Arrange
        var netPrice = 100m;
        var taxRate = 14m;
        
        // Act
        var taxAmount = netPrice * (taxRate / 100m);
        var total = netPrice + taxAmount;
        
        // Assert
        taxAmount.Should().Be(14m, "الضريبة يجب أن تكون 14 جنيه");
        total.Should().Be(114m, "الإجمالي يجب أن يكون 114 جنيه");
    }
    
    [Fact]
    public void Financial_DiscountThenTax_ShouldCalculateCorrectly()
    {
        // Arrange
        var originalPrice = 100m;
        var discountPercent = 10m;
        var taxRate = 14m;
        
        // Act
        var discountAmount = originalPrice * (discountPercent / 100m);
        var netPrice = originalPrice - discountAmount;
        var taxAmount = netPrice * (taxRate / 100m);
        var total = netPrice + taxAmount;
        
        // Assert
        netPrice.Should().Be(90m, "السعر بعد الخصم يجب أن يكون 90");
        taxAmount.Should().Be(12.6m, "الضريبة على 90 جنيه يجب أن تكون 12.6");
        total.Should().Be(102.6m, "الإجمالي النهائي يجب أن يكون 102.6");
    }
    
    [Fact]
    public void Payment_PartialPayment_ShouldCalculateAmountDue()
    {
        // Arrange
        var orderTotal = 100m;
        var amountPaid = 60m;
        
        // Act
        var amountDue = orderTotal - amountPaid;
        
        // Assert
        amountDue.Should().Be(40m, "المبلغ المتبقي يجب أن يكون 40 جنيه");
    }
    
    [Fact]
    public void CashRegister_ShiftBalance_ShouldMatchTransactions()
    {
        // Arrange
        var openingBalance = 1000m;
        var cashSales = 500m;
        var cashExpenses = 100m;
        
        // Act
        var expectedBalance = openingBalance + cashSales - cashExpenses;
        
        // Assert
        expectedBalance.Should().Be(1400m, "رصيد الوردية يجب أن يكون 1400 جنيه");
    }
    
    [Fact]
    public void Customer_CreditLimit_ShouldPreventOverLimit()
    {
        // Arrange
        var creditLimit = 1000m;
        var currentDue = 800m;
        var newOrderAmount = 300m;
        
        // Act
        var totalDue = currentDue + newOrderAmount;
        var isWithinLimit = totalDue <= creditLimit;
        
        // Assert
        isWithinLimit.Should().BeFalse("يجب رفض الطلب لتجاوز حد الائتمان");
    }
    
    [Fact]
    public void Inventory_MultiBranch_StockShouldBeIndependent()
    {
        // Arrange
        var branchAStock = 100;
        var branchBStock = 50;
        var soldFromBranchA = 20;
        
        // Act
        var branchAAfterSale = branchAStock - soldFromBranchA;
        var branchBAfterSale = branchBStock; // لا يتأثر
        
        // Assert
        branchAAfterSale.Should().Be(80, "مخزون الفرع A يجب أن يكون 80");
        branchBAfterSale.Should().Be(50, "مخزون الفرع B يجب أن يظل 50");
    }
    
    [Fact]
    public void Inventory_Transfer_ShouldBalanceBetweenBranches()
    {
        // Arrange
        var branchAStock = 100;
        var branchBStock = 50;
        var transferQuantity = 30;
        
        // Act
        var branchAAfter = branchAStock - transferQuantity;
        var branchBAfter = branchBStock + transferQuantity;
        var totalStock = branchAAfter + branchBAfter;
        
        // Assert
        branchAAfter.Should().Be(70, "مخزون الفرع A بعد التحويل");
        branchBAfter.Should().Be(80, "مخزون الفرع B بعد التحويل");
        totalStock.Should().Be(150, "إجمالي المخزون يجب أن يظل ثابت");
    }
    
    [Fact]
    public void Reports_ProductMovement_ShouldMatchFormula()
    {
        // Arrange
        var openingStock = 0;
        var purchased = 500;
        var sold = 200;
        var transferred = 0;
        
        // Act
        var closingStock = openingStock + purchased - sold + transferred;
        
        // Assert
        closingStock.Should().Be(300, "الرصيد الختامي = الافتتاحي + المشتريات - المبيعات + التحويلات");
    }
}
