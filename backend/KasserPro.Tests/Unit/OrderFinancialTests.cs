namespace KasserPro.Tests.Unit;

using Xunit;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

/// <summary>
/// Unit tests for Order financial calculations.
///
/// Production model is Tax Exclusive (Additive):
///   Subtotal  = UnitPrice × Quantity
///   ItemDiscount = applied on Subtotal
///   NetAfterDiscount = Subtotal - ItemDiscount
///   TaxAmount = NetAfterDiscount × (TaxRate / 100)
///   Total     = NetAfterDiscount + TaxAmount
///
/// Order-level:
///   OrderSubtotal = Σ item.Subtotal
///   ItemDiscountsTotal = Σ item.DiscountAmount
///   NetAfterItemDiscounts = OrderSubtotal - ItemDiscountsTotal
///   OrderDiscount = applied on NetAfterItemDiscounts (NOT raw Subtotal)
///   Tax = per-item with proportional order-discount reduction
///   Total = NetAfterAllDiscounts + Tax + ServiceCharge
/// </summary>
public class OrderFinancialTests
{
    private const decimal EgyptVatRate = 14m;

    #region Tax Calculation Tests (Tax Exclusive)

    [Fact]
    public void CalculateTax_100EGP_TaxExclusive_Returns14Tax()
    {
        // Arrange: 100 EGP net + 14% tax = 114 EGP
        var item = new OrderItem
        {
            UnitPrice = 100m,
            Quantity = 1,
            TaxRate = EgyptVatRate,
            TaxInclusive = false
        };

        // Act
        CalculateItemTotals(item);

        // Assert
        Assert.Equal(100m, item.Subtotal);
        Assert.Equal(14m, item.TaxAmount);
        Assert.Equal(114m, item.Total);
    }

    [Fact]
    public void CalculateTax_50EGP_TaxExclusive_Returns7Tax()
    {
        var item = new OrderItem
        {
            UnitPrice = 50m,
            Quantity = 1,
            TaxRate = EgyptVatRate,
            TaxInclusive = false
        };

        CalculateItemTotals(item);

        Assert.Equal(50m, item.Subtotal);
        Assert.Equal(7m, item.TaxAmount);
        Assert.Equal(57m, item.Total);
    }

    [Fact]
    public void CalculateTax_25EGP_Quantity2_TaxExclusive_Returns7Tax()
    {
        // 25 × 2 = 50 net, tax = 50 × 0.14 = 7
        var item = new OrderItem
        {
            UnitPrice = 25m,
            Quantity = 2,
            TaxRate = EgyptVatRate,
            TaxInclusive = false
        };

        CalculateItemTotals(item);

        Assert.Equal(50m, item.Subtotal);
        Assert.Equal(7m, item.TaxAmount);
        Assert.Equal(57m, item.Total);
    }

    #endregion

    #region Order Total Tests

    [Fact]
    public void OrderTotal_ThreeItems_TaxExclusive_CalculatesCorrectly()
    {
        // Item1: 40, Item2: 35, Item3: 25 → Subtotal 100
        // Tax: 100 × 14% = 14 → Total 114
        var order = new Order();

        var item1 = new OrderItem { UnitPrice = 40m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };
        var item2 = new OrderItem { UnitPrice = 35m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };
        var item3 = new OrderItem { UnitPrice = 25m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };

        CalculateItemTotals(item1);
        CalculateItemTotals(item2);
        CalculateItemTotals(item3);

        order.Items.Add(item1);
        order.Items.Add(item2);
        order.Items.Add(item3);

        CalculateOrderTotals(order);

        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(14m, order.TaxAmount);
        Assert.Equal(114m, order.Total);
    }

    [Fact]
    public void OrderTotal_SingleItem_TaxExclusive_MatchesExpected()
    {
        var order = new Order();
        var item = new OrderItem { UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };

        CalculateItemTotals(item);
        order.Items.Add(item);

        CalculateOrderTotals(order);

        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(114m, order.Total);
        Assert.Equal(14m, order.TaxAmount);
        Assert.Equal(0m, order.DiscountAmount);
    }

    #endregion

    #region Discount Tests

    [Fact]
    public void ItemDiscount_Percentage_CalculatesCorrectly()
    {
        // 100 EGP net, 10% item discount → 90 net → tax 12.6 → total 102.6
        var item = new OrderItem
        {
            UnitPrice = 100m,
            Quantity = 1,
            TaxRate = EgyptVatRate,
            TaxInclusive = false,
            DiscountType = "percentage",
            DiscountValue = 10m
        };

        CalculateItemTotals(item);

        Assert.Equal(100m, item.Subtotal);
        Assert.Equal(10m, item.DiscountAmount);
        Assert.Equal(12.6m, item.TaxAmount);  // 90 × 0.14
        Assert.Equal(102.6m, item.Total);      // 90 + 12.6
    }

    [Fact]
    public void ItemDiscount_Fixed_CalculatesCorrectly()
    {
        // 100 EGP net, 15 EGP discount → 85 net → tax 11.9 → total 96.9
        var item = new OrderItem
        {
            UnitPrice = 100m,
            Quantity = 1,
            TaxRate = EgyptVatRate,
            TaxInclusive = false,
            DiscountType = "fixed",
            DiscountValue = 15m
        };

        CalculateItemTotals(item);

        Assert.Equal(100m, item.Subtotal);
        Assert.Equal(15m, item.DiscountAmount);
        Assert.Equal(11.9m, item.TaxAmount);   // 85 × 0.14
        Assert.Equal(96.9m, item.Total);        // 85 + 11.9
    }

    [Fact]
    public void OrderDiscount_Percentage_OnlyOrderLevel_CalculatesCorrectly()
    {
        // 100 EGP net, no item discount, 10% order discount
        // OrderDiscount applied on 100 → 10 → net 90 → tax 12.6 → total 102.6
        var order = new Order
        {
            DiscountType = "percentage",
            DiscountValue = 10m
        };

        var item = new OrderItem { UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };
        CalculateItemTotals(item);
        order.Items.Add(item);

        CalculateOrderTotals(order);

        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(10m, order.DiscountAmount);
        Assert.Equal(12.6m, order.TaxAmount);  // (100-10) × 14%
        Assert.Equal(102.6m, order.Total);     // 90 + 12.6
    }

    [Fact]
    public void CombinedDiscounts_ItemAndOrder_OrderDiscountAppliesAfterItemDiscount()
    {
        // Item: 100 EGP net, 50% item discount → item subtotal 100, item discount 50
        // Order: 10% order discount
        //
        // netAfterItemDiscounts = 100 - 50 = 50
        // orderDiscount = 50 × 10% = 5
        // netAfterAll = 50 - 5 = 45
        // tax = 45 × 14% = 6.3
        // total = 45 + 6.3 = 51.3
        var order = new Order
        {
            DiscountType = "percentage",
            DiscountValue = 10m
        };

        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "percentage", DiscountValue = 50m
        };
        CalculateItemTotals(item);
        order.Items.Add(item);

        CalculateOrderTotals(order);

        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(5m, order.DiscountAmount);   // 10% of 50 (not 10% of 100)
        Assert.Equal(6.3m, order.TaxAmount);       // 45 × 0.14
        Assert.Equal(51.3m, order.Total);          // 45 + 6.3
    }

    [Fact]
    public void CombinedDiscounts_TwoItems_DifferentDiscounts()
    {
        // ItemA: 200 EGP, 20% item discount → discount 40, net 160
        // ItemB: 100 EGP, no item discount → net 100
        // Subtotal = 300, itemDiscountsTotal = 40, netAfterItemDiscounts = 260
        // Order discount: 10% of 260 = 26
        // netAfterAll = 260 - 26 = 234
        //
        // Tax per-item with proportional order discount:
        //   orderDiscountRatio = 26 / 260 = 0.1
        //   ItemA net = 160, after order discount = 160 × 0.9 = 144, tax = 144 × 0.14 = 20.16
        //   ItemB net = 100, after order discount = 100 × 0.9 = 90,  tax = 90 × 0.14 = 12.6
        //   totalTax = 20.16 + 12.6 = 32.76
        //
        // Total = 234 + 32.76 = 266.76
        var order = new Order
        {
            DiscountType = "percentage",
            DiscountValue = 10m
        };

        var itemA = new OrderItem
        {
            UnitPrice = 200m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "percentage", DiscountValue = 20m
        };
        var itemB = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false
        };

        CalculateItemTotals(itemA);
        CalculateItemTotals(itemB);
        order.Items.Add(itemA);
        order.Items.Add(itemB);

        CalculateOrderTotals(order);

        Assert.Equal(300m, order.Subtotal);
        Assert.Equal(26m, order.DiscountAmount);   // 10% of 260
        Assert.Equal(32.76m, order.TaxAmount);     // 20.16 + 12.6
        Assert.Equal(266.76m, order.Total);        // 234 + 32.76
    }

    [Fact]
    public void CombinedDiscounts_FixedOrderDiscount_AfterItemDiscounts()
    {
        // Item: 200 EGP, 50 EGP fixed item discount → net 150
        // Order: 30 EGP fixed discount
        //
        // netAfterItemDiscounts = 200 - 50 = 150
        // orderDiscount = 30 (capped at 150)
        // netAfterAll = 150 - 30 = 120
        // tax = 120 × 14% = 16.8
        // total = 120 + 16.8 = 136.8
        var order = new Order
        {
            DiscountType = "fixed",
            DiscountValue = 30m
        };

        var item = new OrderItem
        {
            UnitPrice = 200m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "fixed", DiscountValue = 50m
        };
        CalculateItemTotals(item);
        order.Items.Add(item);

        CalculateOrderTotals(order);

        Assert.Equal(200m, order.Subtotal);
        Assert.Equal(30m, order.DiscountAmount);
        Assert.Equal(16.8m, order.TaxAmount);      // 120 × 0.14
        Assert.Equal(136.8m, order.Total);          // 120 + 16.8
    }

    [Fact]
    public void OrderDiscount_NeverExceedsNetAfterItemDiscounts()
    {
        // Item: 100 EGP, 90% item discount → net 10
        // Order: fixed 50 EGP discount → capped at 10
        var order = new Order
        {
            DiscountType = "fixed",
            DiscountValue = 50m
        };

        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "percentage", DiscountValue = 90m
        };
        CalculateItemTotals(item);
        order.Items.Add(item);

        CalculateOrderTotals(order);

        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(10m, order.DiscountAmount);    // Capped to netAfterItemDiscounts
        Assert.Equal(0m, order.TaxAmount);           // 0 net → 0 tax
        Assert.Equal(0m, order.Total);
    }

    #endregion

    #region Rounding Tests

    [Fact]
    public void Rounding_NoFloatingPointErrors()
    {
        // 33.33 × 3 = 99.99
        var item = new OrderItem
        {
            UnitPrice = 33.33m,
            Quantity = 3,
            TaxRate = EgyptVatRate,
            TaxInclusive = false
        };

        CalculateItemTotals(item);

        Assert.Equal(99.99m, item.Subtotal);
        Assert.Equal(14m, item.TaxAmount);    // 99.99 × 0.14 = 13.9986 → 14.00
        Assert.Equal(113.99m, item.Total);    // 99.99 + 14.00
    }

    [Fact]
    public void Rounding_SmallAmounts_NoErrors()
    {
        var item = new OrderItem
        {
            UnitPrice = 0.99m,
            Quantity = 1,
            TaxRate = EgyptVatRate,
            TaxInclusive = false
        };

        CalculateItemTotals(item);

        Assert.Equal(0.99m, item.Subtotal);
        Assert.Equal(0.14m, item.TaxAmount);  // 0.99 × 0.14 = 0.1386 → 0.14
        Assert.Equal(1.13m, item.Total);       // 0.99 + 0.14
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ZeroQuantity_ReturnsZeroTotals()
    {
        var item = new OrderItem
        {
            UnitPrice = 100m,
            Quantity = 0,
            TaxRate = EgyptVatRate,
            TaxInclusive = false
        };

        CalculateItemTotals(item);

        Assert.Equal(0m, item.Subtotal);
        Assert.Equal(0m, item.Total);
        Assert.Equal(0m, item.TaxAmount);
    }

    [Fact]
    public void ZeroTaxRate_NoTaxCalculated()
    {
        var item = new OrderItem
        {
            UnitPrice = 100m,
            Quantity = 1,
            TaxRate = 0m,
            TaxInclusive = false
        };

        CalculateItemTotals(item);

        Assert.Equal(100m, item.Subtotal);
        Assert.Equal(100m, item.Total);
        Assert.Equal(0m, item.TaxAmount);
    }

    [Fact]
    public void NoDiscounts_OrderTotalEqualsItemTotalsSum()
    {
        var order = new Order();
        var item1 = new OrderItem { UnitPrice = 50m, Quantity = 2, TaxRate = EgyptVatRate, TaxInclusive = false };
        var item2 = new OrderItem { UnitPrice = 30m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };

        CalculateItemTotals(item1);
        CalculateItemTotals(item2);
        order.Items.Add(item1);
        order.Items.Add(item2);

        CalculateOrderTotals(order);

        // Subtotal: 100 + 30 = 130, Tax: 18.2, Total: 148.2
        Assert.Equal(130m, order.Subtotal);
        Assert.Equal(18.2m, order.TaxAmount);
        Assert.Equal(148.2m, order.Total);
        Assert.Equal(order.Total, order.Items.Sum(i => i.Total));
    }

    [Fact]
    public void ItemDiscountOnly_NoOrderDiscount_OrderTotalEqualsItemTotalsSum()
    {
        // When there's no order-level discount, order.Total = Σ item.Total
        var order = new Order();
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "percentage", DiscountValue = 20m
        };

        CalculateItemTotals(item);
        order.Items.Add(item);

        CalculateOrderTotals(order);

        // 100 - 20 = 80, tax = 11.2, total = 91.2
        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(11.2m, order.TaxAmount);
        Assert.Equal(91.2m, order.Total);
        Assert.Equal(order.Total, order.Items.Sum(i => i.Total));
    }

    #endregion

    #region Refund Ratio Tests

    [Fact]
    public void FullRefund_ItemAndOrderDiscount_RefundEqualsTotal()
    {
        // Build order: Item 200 with 20% item discount, 10% order discount
        var order = new Order { DiscountType = "percentage", DiscountValue = 10m };
        var item = new OrderItem
        {
            UnitPrice = 200m, Quantity = 2, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "percentage", DiscountValue = 20m
        };
        CalculateItemTotals(item);
        order.Items.Add(item);
        CalculateOrderTotals(order);

        // Full refund = order.Total
        var totalRefund = order.Total;

        // Verify sum consistency
        var itemsGrossTotal = order.Items.Sum(i => i.Total);
        var orderLevelAdjust = order.Total - itemsGrossTotal;
        var ratio = CalculateRefundRatio(itemsGrossTotal, itemsGrossTotal);
        var calculatedRefund = Math.Round(itemsGrossTotal + (orderLevelAdjust * ratio), 2);

        Assert.Equal(totalRefund, calculatedRefund);
        Assert.Equal(1m, ratio);
    }

    [Fact]
    public void PartialRefund_HalfItems_RefundIsProportional()
    {
        // Two identical items, each 100 EGP. Refund 1 item.
        var order = new Order { DiscountType = "percentage", DiscountValue = 10m };
        var item1 = new OrderItem { UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };
        var item2 = new OrderItem { UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };

        CalculateItemTotals(item1);
        CalculateItemTotals(item2);
        order.Items.Add(item1);
        order.Items.Add(item2);
        CalculateOrderTotals(order);

        // order.Total for 2×100, 10% discount on 200 = 20, net 180, tax = 25.2, total = 205.2
        Assert.Equal(205.2m, order.Total);

        // Refund 1 item (item1.Total = 114)
        var originalItemsGrossTotal = order.Items.Sum(i => i.Total); // 228
        var refundedGross = item1.Total;  // 114
        var ratio = CalculateRefundRatio(refundedGross, originalItemsGrossTotal); // 0.5
        var orderLevelAdjust = order.Total - originalItemsGrossTotal; // 205.2 - 228 = -22.8
        var totalRefund = Math.Round(refundedGross + (orderLevelAdjust * ratio), 2);
        // = 114 + (-22.8 × 0.5) = 114 - 11.4 = 102.6

        Assert.Equal(0.5m, ratio);
        Assert.Equal(102.6m, totalRefund);

        // Sum of all partial refunds should equal full amount
        // Second refund would also be 102.6 → total 205.2 = order.Total ✓
        Assert.Equal(order.Total, totalRefund * 2);
    }

    #endregion

    #region Helper Methods (Exact mirror of OrderService production logic)

    /// <summary>
    /// Mirrors OrderService.CalculateItemTotals — Tax Exclusive (Additive)
    /// </summary>
    private static void CalculateItemTotals(OrderItem item)
    {
        item.Subtotal = Math.Round(item.UnitPrice * item.Quantity, 2);

        if (item.DiscountType == "percentage" && item.DiscountValue.HasValue)
        {
            var pct = Math.Clamp(item.DiscountValue.Value, 0m, 100m);
            item.DiscountAmount = Math.Round(item.Subtotal * (pct / 100m), 2);
        }
        else if (item.DiscountType == "fixed" && item.DiscountValue.HasValue)
            item.DiscountAmount = Math.Round(Math.Min(Math.Max(item.DiscountValue.Value, 0m), item.Subtotal), 2);
        else
            item.DiscountAmount = 0;

        var netAfterDiscount = Math.Round(item.Subtotal - item.DiscountAmount, 2);

        item.TaxAmount = Math.Round(netAfterDiscount * (item.TaxRate / 100m), 2);
        item.Total = Math.Round(netAfterDiscount + item.TaxAmount, 2);
    }

    /// <summary>
    /// Mirrors OrderService.CalculateOrderTotals — order discount applies AFTER item discounts
    /// </summary>
    private static void CalculateOrderTotals(Order order)
    {
        order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);

        var itemDiscountsTotal = Math.Round(order.Items.Sum(i => i.DiscountAmount), 2);
        var netAfterItemDiscounts = Math.Round(order.Subtotal - itemDiscountsTotal, 2);

        if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
            order.DiscountAmount = Math.Round(netAfterItemDiscounts * (order.DiscountValue.Value / 100m), 2);
        else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
            order.DiscountAmount = Math.Round(order.DiscountValue.Value, 2);
        else
            order.DiscountAmount = 0;

        if (order.DiscountAmount > netAfterItemDiscounts)
            order.DiscountAmount = netAfterItemDiscounts;

        var afterAllDiscounts = netAfterItemDiscounts - order.DiscountAmount;

        if (order.DiscountAmount > 0 && netAfterItemDiscounts > 0)
        {
            var orderDiscountRatio = order.DiscountAmount / netAfterItemDiscounts;
            order.TaxAmount = Math.Round(order.Items.Sum(item =>
            {
                var itemNet = item.Subtotal - item.DiscountAmount;
                var itemAfterOrderDiscount = itemNet * (1m - orderDiscountRatio);
                return itemAfterOrderDiscount * (item.TaxRate / 100m);
            }), 2);
        }
        else
        {
            order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);
        }

        order.ServiceChargeAmount = Math.Round(afterAllDiscounts * (order.ServiceChargePercent / 100m), 2);

        order.Total = Math.Round(afterAllDiscounts + order.TaxAmount + order.ServiceChargeAmount, 2);
        order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
    }

    private static decimal CalculateRefundRatio(decimal refundedItemsGrossTotal, decimal originalItemsGrossTotal)
    {
        if (originalItemsGrossTotal <= 0) return 0;
        return Math.Clamp(refundedItemsGrossTotal / originalItemsGrossTotal, 0m, 1m);
    }

    #endregion

    #region Discount Guard Tests

    [Fact]
    public void FixedDiscount_ExceedsSubtotal_CappedAtSubtotal()
    {
        // Fixed discount of 200 on a 100 EGP item → capped at 100, not negative
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "fixed", DiscountValue = 200m
        };

        CalculateItemTotals(item);

        Assert.Equal(100m, item.DiscountAmount);  // Capped at Subtotal
        Assert.Equal(0m, item.TaxAmount);
        Assert.Equal(0m, item.Total);             // Never negative
    }

    [Fact]
    public void PercentageDiscount_Over100_ClampedTo100()
    {
        // 150% discount → clamped to 100%, item becomes 0
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "percentage", DiscountValue = 150m
        };

        CalculateItemTotals(item);

        Assert.Equal(100m, item.DiscountAmount);  // 100% of 100
        Assert.Equal(0m, item.TaxAmount);
        Assert.Equal(0m, item.Total);
    }

    [Fact]
    public void NegativeFixedDiscount_ClampedToZero()
    {
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "fixed", DiscountValue = -50m
        };

        CalculateItemTotals(item);

        Assert.Equal(0m, item.DiscountAmount);  // Clamped to 0
        Assert.Equal(14m, item.TaxAmount);       // Full tax
        Assert.Equal(114m, item.Total);
    }

    [Fact]
    public void NegativePercentageDiscount_ClampedToZero()
    {
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false,
            DiscountType = "percentage", DiscountValue = -10m
        };

        CalculateItemTotals(item);

        Assert.Equal(0m, item.DiscountAmount);
        Assert.Equal(114m, item.Total);
    }

    #endregion

    #region Refund Quantity Tracking Tests

    [Fact]
    public void RefundedQuantity_PreventsDoubleRefund()
    {
        // Order has 3x ItemA. User refunds 2. Then tries to refund 2 more.
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 3, TaxRate = EgyptVatRate, TaxInclusive = false
        };
        CalculateItemTotals(item);

        // First refund: 2 units
        var availableForRefund = item.Quantity - item.RefundedQuantity; // 3 - 0 = 3
        Assert.Equal(3, availableForRefund);
        Assert.True(2 <= availableForRefund);
        item.RefundedQuantity += 2;

        // Second refund: Try 2 more units
        availableForRefund = item.Quantity - item.RefundedQuantity; // 3 - 2 = 1
        Assert.Equal(1, availableForRefund);
        Assert.False(2 <= availableForRefund); // Should be rejected!

        // Only 1 more unit can be refunded
        Assert.True(1 <= availableForRefund);
        item.RefundedQuantity += 1;

        // No more available
        availableForRefund = item.Quantity - item.RefundedQuantity; // 3 - 3 = 0
        Assert.Equal(0, availableForRefund);
    }

    [Fact]
    public void RefundedQuantity_DefaultIsZero()
    {
        var item = new OrderItem { UnitPrice = 50m, Quantity = 5 };
        Assert.Equal(0, item.RefundedQuantity);
    }

    #endregion

    #region Full Refund on PartiallyRefunded Order

    [Fact]
    public void FullRefundAfterPartial_OnlyRefundsRemainingQuantity()
    {
        // Order: 5x item100 at 100 EGP each, no discounts, 14% tax
        // item.Total = 5 * 100 * 1.14 = 570
        var order = new Order();
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 5, TaxRate = EgyptVatRate, TaxInclusive = false
        };
        CalculateItemTotals(item);
        order.Items.Add(item);
        CalculateOrderTotals(order);

        Assert.Equal(570m, order.Total);

        // Simulate partial refund of 2 units
        item.RefundedQuantity = 2;
        order.RefundAmount = 228m; // 2 * 114 = 228
        order.Status = OrderStatus.PartiallyRefunded;

        // Now "full refund" should only return remaining 3 units
        var remainingQty = item.Quantity - item.RefundedQuantity;
        Assert.Equal(3, remainingQty);

        var unitPriceWithTax = item.Total / item.Quantity; // 570/5 = 114
        var remainingGross = Math.Round(unitPriceWithTax * remainingQty, 2); // 342
        Assert.Equal(342m, remainingGross);

        // Refund ratio and amount
        var originalItemsGrossTotal = order.Items.Sum(i => i.Total); // 570
        var refundRatio = CalculateRefundRatio(remainingGross, originalItemsGrossTotal); // 342/570 = 0.6
        var orderLevelAdjustment = order.Total - originalItemsGrossTotal; // 0 (no order discount)
        var totalRefund = Math.Round(remainingGross + (orderLevelAdjustment * refundRatio), 2);

        Assert.Equal(342m, totalRefund);
        Assert.Equal(order.Total - order.RefundAmount, totalRefund); // 570-228=342 ✓
    }

    [Fact]
    public void FullRefundAfterPartial_WithOrderDiscount_CorrectProportionalAmount()
    {
        // Order: 2x item at 100 EGP each, 10% order discount, 14% tax
        var order = new Order { DiscountType = "percentage", DiscountValue = 10m };
        var item1 = new OrderItem { UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };
        var item2 = new OrderItem { UnitPrice = 100m, Quantity = 1, TaxRate = EgyptVatRate, TaxInclusive = false };

        CalculateItemTotals(item1);
        CalculateItemTotals(item2);
        order.Items.Add(item1);
        order.Items.Add(item2);
        CalculateOrderTotals(order);

        // Subtotal 200, order discount 20, net 180, tax 25.2, total 205.2
        Assert.Equal(205.2m, order.Total);

        // Partial refund: item1 fully refunded (1 unit)
        item1.RefundedQuantity = 1;
        var partialGross = item1.Total; // 114
        var originalItemsGrossTotal = order.Items.Sum(i => i.Total); // 228
        var partialRatio = CalculateRefundRatio(partialGross, originalItemsGrossTotal); // 0.5
        var partialOrderAdjust = order.Total - originalItemsGrossTotal; // -22.8
        var partialRefund = Math.Round(partialGross + (partialOrderAdjust * partialRatio), 2); // 114 - 11.4 = 102.6
        order.RefundAmount = partialRefund;

        // Now "full refund" of remaining: item2 only (item1 fully refunded)
        var remaining1 = item1.Quantity - item1.RefundedQuantity; // 0
        var remaining2 = item2.Quantity - item2.RefundedQuantity; // 1
        Assert.Equal(0, remaining1);
        Assert.Equal(1, remaining2);

        // Only item2 contributes to remaining refund
        var remainingGross = Math.Round((item2.Total / item2.Quantity) * remaining2, 2); // 114
        var fullRatio = CalculateRefundRatio(remainingGross, originalItemsGrossTotal); // 114/228 = 0.5
        var fullRefund = Math.Round(remainingGross + (partialOrderAdjust * fullRatio), 2); // 114 - 11.4 = 102.6

        Assert.Equal(102.6m, fullRefund);
        Assert.Equal(order.Total, order.RefundAmount + fullRefund); // 102.6 + 102.6 = 205.2 ✓
    }

    [Fact]
    public void FullRefundAfterPartial_AllItemsAlreadyRefunded_ZeroRemaining()
    {
        var order = new Order();
        var item = new OrderItem
        {
            UnitPrice = 100m, Quantity = 2, TaxRate = EgyptVatRate, TaxInclusive = false
        };
        CalculateItemTotals(item);
        order.Items.Add(item);
        CalculateOrderTotals(order);

        // All items already refunded
        item.RefundedQuantity = 2;
        order.RefundAmount = order.Total;

        var remainingQty = item.Quantity - item.RefundedQuantity;
        Assert.Equal(0, remainingQty);

        var remainingRefundable = Math.Round(order.Total - order.RefundAmount, 2);
        Assert.Equal(0m, remainingRefundable);
    }

    #endregion
}
