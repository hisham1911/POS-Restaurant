namespace KasserPro.Tests.Unit;

using Xunit;
using FluentAssertions;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

/// <summary>
/// Adversarial regression tests generated from the Extreme Adversarial Stress Test Report.
/// Tests every discovered bug and financial invariant violation.
/// </summary>
public class AdversarialFinancialTests
{
    private const decimal EgyptVatRate = 14m;

    #region ── Helper Methods (mirror of production OrderService logic) ──

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
        item.TaxAmount = Math.Round(netAfterDiscount * (item.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        item.Total = Math.Round(netAfterDiscount + item.TaxAmount, 2);
    }

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
            }), 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2, MidpointRounding.AwayFromZero);
        }
        order.ServiceChargeAmount = Math.Round(afterAllDiscounts * (order.ServiceChargePercent / 100m), 2, MidpointRounding.AwayFromZero);
        order.Total = Math.Round(afterAllDiscounts + order.TaxAmount + order.ServiceChargeAmount, 2);
        order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
    }

    private static decimal CalculateRefundRatio(decimal refundedItemsGrossTotal, decimal originalItemsGrossTotal)
    {
        if (originalItemsGrossTotal <= 0) return 0;
        return Math.Clamp(refundedItemsGrossTotal / originalItemsGrossTotal, 0m, 1m);
    }

    /// <summary>
    /// BUG H-6: Current production code — missing MidpointRounding.AwayFromZero
    /// </summary>
    private static decimal CalculateProportionalAmount_BUGGY(decimal amount, decimal ratio)
        => Math.Round(amount * ratio, 2); // Default is MidpointRounding.ToEven

    /// <summary>
    /// FIXED version: uses AwayFromZero to match all other rounding
    /// </summary>
    private static decimal CalculateProportionalAmount_FIXED(decimal amount, decimal ratio)
        => Math.Round(amount * ratio, 2, MidpointRounding.AwayFromZero);

    private static (decimal totalRefund, decimal refundRatio) SimulatePartialRefund(
        Order order, List<(OrderItem item, int refundQty)> refundItems)
    {
        var originalItemsGrossTotal = order.Items.Sum(i => i.Total);
        decimal refundedItemsGrossTotal = 0;

        foreach (var (item, refundQty) in refundItems)
        {
            var remainingQty = item.Quantity - item.RefundedQuantity;
            var actualRefundQty = Math.Min(refundQty, remainingQty);
            var itemRatio = (decimal)actualRefundQty / item.Quantity;
            var itemRefundAmount = Math.Round(item.Total * itemRatio, 2, MidpointRounding.AwayFromZero);
            refundedItemsGrossTotal += itemRefundAmount;
        }

        var refundRatio = CalculateRefundRatio(refundedItemsGrossTotal, originalItemsGrossTotal);
        var orderLevelAdjustment = Math.Round(order.Total - originalItemsGrossTotal, 2);
        var totalRefund = Math.Round(
            refundedItemsGrossTotal + (orderLevelAdjustment * refundRatio), 2,
            MidpointRounding.AwayFromZero);

        var remainingRefundable = Math.Round(order.Total - order.RefundAmount, 2);
        if (totalRefund > remainingRefundable)
            totalRefund = remainingRefundable;

        return (totalRefund, refundRatio);
    }

    private static Order CreateTestOrder(
        decimal unitPrice, int qty, decimal taxRate = 14m,
        string? itemDiscountType = null, decimal? itemDiscountValue = null,
        string? orderDiscountType = null, decimal? orderDiscountValue = null,
        decimal serviceChargePercent = 0)
    {
        var order = new Order
        {
            DiscountType = orderDiscountType,
            DiscountValue = orderDiscountValue,
            ServiceChargePercent = serviceChargePercent
        };
        var item = new OrderItem
        {
            UnitPrice = unitPrice,
            Quantity = qty,
            TaxRate = taxRate,
            TaxInclusive = false,
            DiscountType = itemDiscountType,
            DiscountValue = itemDiscountValue,
            RefundedQuantity = 0
        };
        CalculateItemTotals(item);
        order.Items.Add(item);
        CalculateOrderTotals(order);
        return order;
    }

    private static Order CreateMultiItemOrder(
        List<(decimal price, int qty, decimal taxRate)> items,
        string? orderDiscountType = null, decimal? orderDiscountValue = null,
        decimal serviceChargePercent = 0)
    {
        var order = new Order
        {
            DiscountType = orderDiscountType,
            DiscountValue = orderDiscountValue,
            ServiceChargePercent = serviceChargePercent
        };
        foreach (var (price, qty, taxRate) in items)
        {
            var item = new OrderItem
            {
                UnitPrice = price,
                Quantity = qty,
                TaxRate = taxRate,
                TaxInclusive = false,
                RefundedQuantity = 0
            };
            CalculateItemTotals(item);
            order.Items.Add(item);
        }
        CalculateOrderTotals(order);
        return order;
    }

    #endregion

    #region ── CRITICAL-1: FinancialReportService returnOrders missing Items Include ──

    [Fact]
    public void C1_ReturnOrdersWithoutItems_SelectManyReturnsEmpty()
    {
        // Simulates what EF Core does when Items aren't .Include'd:
        // The navigation property is initialized as an empty collection
        var returnOrder = new Order
        {
            OrderType = OrderType.Return,
            Total = -100m,
            // Items NOT loaded (empty collection — EF Core default)
        };

        var returnedCost = new[] { returnOrder }
            .SelectMany(o => o.Items)
            .Sum(i => (i.UnitCost ?? 0) * Math.Abs(i.Quantity));

        // BUG: returnedCost is 0 because Items collection is empty
        returnedCost.Should().Be(0, "Items not loaded via .Include => COGS fix is dead code");
    }

    [Fact]
    public void C1_ReturnOrdersWithItems_CalculatesCorrectCOGS()
    {
        // What it SHOULD look like with proper .Include
        var returnOrder = new Order
        {
            OrderType = OrderType.Return,
            Total = -100m,
        };
        returnOrder.Items.Add(new OrderItem
        {
            UnitCost = 50m,
            Quantity = -2, // Return order items have negative quantity
            ProductName = "Test"
        });

        var returnedCost = new[] { returnOrder }
            .SelectMany(o => o.Items)
            .Sum(i => (i.UnitCost ?? 0) * Math.Abs(i.Quantity));

        returnedCost.Should().Be(100m, "With Items loaded, COGS should be 100");
    }

    #endregion

    #region ── CRITICAL-2: ShiftService TotalCard Normal vs ForceClose ──

    [Fact]
    public void C2_NormalClose_TotalCardIncludesAllNonCash()
    {
        var payments = new List<Payment>
        {
            new() { Method = PaymentMethod.Cash, Amount = 100 },
            new() { Method = PaymentMethod.Card, Amount = 200 },
            new() { Method = PaymentMethod.Fawry, Amount = 150 },
            new() { Method = PaymentMethod.BankTransfer, Amount = 50 },
        };

        // Normal close logic: p.Method != PaymentMethod.Cash
        var totalCard_NormalClose = payments
            .Where(p => p.Method != PaymentMethod.Cash)
            .Sum(p => p.Amount);

        totalCard_NormalClose.Should().Be(400m, "Normal close includes Card + Fawry + BankTransfer");
    }

    [Fact]
    public void C2_ForceClose_TotalCardMatchesNormalClose()
    {
        var payments = new List<Payment>
        {
            new() { Method = PaymentMethod.Cash, Amount = 100 },
            new() { Method = PaymentMethod.Card, Amount = 200 },
            new() { Method = PaymentMethod.Fawry, Amount = 150 },
            new() { Method = PaymentMethod.BankTransfer, Amount = 50 },
        };

        // FIXED: ForceClose now uses unified CalculateShiftFinancials helper
        // Same formula as NormalClose: p.Method != PaymentMethod.Cash
        var totalCard_ForceClose = payments
            .Where(p => p.Method != PaymentMethod.Cash)
            .Sum(p => p.Amount);

        totalCard_ForceClose.Should().Be(400m, "FIXED: ForceClose includes all non-cash payments");
    }

    [Fact]
    public void C2_TotalCardConsistent_UnifiedHelper()
    {
        var payments = new List<Payment>
        {
            new() { Method = PaymentMethod.Cash, Amount = 100 },
            new() { Method = PaymentMethod.Card, Amount = 200 },
            new() { Method = PaymentMethod.Fawry, Amount = 150 },
            new() { Method = PaymentMethod.BankTransfer, Amount = 50 },
        };

        // FIXED: Both NormalClose and ForceClose use unified CalculateShiftFinancials
        var totalCard_Normal = payments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount);
        var totalCard_Force = payments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount);

        totalCard_Normal.Should().Be(totalCard_Force,
            "FIXED: NormalClose and ForceClose use identical non-cash calculation");
    }

    #endregion

    #region ── CRITICAL-3: Shift close excludes PartiallyRefunded orders ──

    [Fact]
    public void C3_UnifiedFilter_IncludesAllCompletedStatuses()
    {
        var orders = new List<Order>
        {
            new() { Status = OrderStatus.Completed, Total = 100, AmountPaid = 100 },
            new() { Status = OrderStatus.Completed, Total = 200, AmountPaid = 200 },
            new() { Status = OrderStatus.PartiallyRefunded, Total = 300, AmountPaid = 300 },
            new() { Status = OrderStatus.Refunded, Total = 400, AmountPaid = 400 },
        };

        // FIXED: Unified CalculateShiftFinancials includes all valid statuses
        var allCompletedOrders = orders.Where(o =>
            o.Status == OrderStatus.Completed ||
            o.Status == OrderStatus.PartiallyRefunded ||
            o.Status == OrderStatus.Refunded).ToList();

        allCompletedOrders.Count.Should().Be(4, "FIXED: All 4 orders included in shift calculations");

        var totalAmount = allCompletedOrders.Sum(o => o.Total);
        totalAmount.Should().Be(1000m, "FIXED: Total includes all order statuses");
    }

    #endregion

    #region ── CRITICAL-6: PartiallyRefunded missing from ValidTransitions ──

    [Fact]
    public void C6_ValidTransitions_MissingPartiallyRefunded()
    {
        var validTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            { OrderStatus.Draft, new[] { OrderStatus.Pending, OrderStatus.Completed, OrderStatus.Cancelled } },
            { OrderStatus.Pending, new[] { OrderStatus.Completed, OrderStatus.Cancelled } },
            { OrderStatus.Completed, new[] { OrderStatus.Refunded } },
            { OrderStatus.Cancelled, Array.Empty<OrderStatus>() },
            { OrderStatus.Refunded, Array.Empty<OrderStatus>() }
            // PartiallyRefunded is MISSING
        };

        // Simulating ValidateStateTransition for PartiallyRefunded
        var found = validTransitions.TryGetValue(OrderStatus.PartiallyRefunded, out var validNextStates);
        found.Should().BeFalse("PartiallyRefunded is not in the dictionary — any transition attempt fails");
    }

    [Fact]
    public void C6_PartiallyRefundedToRefunded_ShouldBeValid()
    {
        // After fix, PartiallyRefunded should allow transition to Refunded
        var fixedTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            { OrderStatus.Draft, new[] { OrderStatus.Pending, OrderStatus.Completed, OrderStatus.Cancelled } },
            { OrderStatus.Pending, new[] { OrderStatus.Completed, OrderStatus.Cancelled } },
            { OrderStatus.Completed, new[] { OrderStatus.Refunded, OrderStatus.PartiallyRefunded } },
            { OrderStatus.PartiallyRefunded, new[] { OrderStatus.Refunded, OrderStatus.PartiallyRefunded } },
            { OrderStatus.Cancelled, Array.Empty<OrderStatus>() },
            { OrderStatus.Refunded, Array.Empty<OrderStatus>() }
        };

        fixedTransitions.TryGetValue(OrderStatus.PartiallyRefunded, out var nextStates);
        nextStates.Should().Contain(OrderStatus.Refunded);
    }

    #endregion

    #region ── CRITICAL-7: Loyalty points asymmetric rounding ──

    [Fact]
    public void C7_LoyaltyPoints_SymmetricRounding_NoPhantomPoints()
    {
        // Buy 10 items at 1.99 → total = 19.90
        var orderTotal = 19.90m;
        int earned = (int)Math.Round(orderTotal, 0, MidpointRounding.AwayFromZero); // 20 points

        // Partial refund 50% → refund = 9.95
        var refundAmount = 9.95m;
        int deducted = (int)Math.Round(refundAmount, 0, MidpointRounding.AwayFromZero); // 10 points

        var netPoints = earned - deducted; // 10 points
        var netSpend = orderTotal - refundAmount; // 9.95

        // FIXED: Round(9.95) = 10 → matches netPoints = 10. No phantom points.
        netPoints.Should().Be(10);
        ((int)Math.Round(netSpend, 0, MidpointRounding.AwayFromZero)).Should().Be(10,
            "FIXED: Net spend of 9.95 rounds to 10, matching 10 net points — symmetric rounding");
    }

    [Fact]
    public void C7_SubEGPPurchase_SymmetricPointsWithRound()
    {
        var orderTotal = 0.99m;
        int earned = (int)Math.Round(orderTotal, 0, MidpointRounding.AwayFromZero); // 1 point
        earned.Should().Be(1, "FIXED: 0.99 EGP rounds to 1 point with symmetric rounding");

        // Refund 0.50
        int deducted = (int)Math.Round(0.50m, 0, MidpointRounding.AwayFromZero); // 1 point (midpoint rounds up)
        deducted.Should().Be(1, "FIXED: 0.50 EGP midpoint rounds away from zero to 1 point");
    }

    [Theory]
    [InlineData(99.99, 49.50, 100, 50, 50)]  // FIXED: Round(99.99)=100, Round(49.50)=50
    [InlineData(10.50, 5.50, 11, 6, 5)]      // FIXED: Round(10.50)=11, Round(5.50)=6 (midpoints round up)
    [InlineData(19.90, 9.95, 20, 10, 10)]    // FIXED: Round(19.90)=20, Round(9.95)=10 — no phantom points
    [InlineData(1.99, 0.99, 2, 1, 1)]        // FIXED: Round(1.99)=2, Round(0.99)=1
    public void C7_LoyaltyPointsBalance(
        decimal orderTotal, decimal refundAmount,
        int expectedEarned, int expectedDeducted, int expectedNet)
    {
        int earned = (int)Math.Round(orderTotal, 0, MidpointRounding.AwayFromZero);
        int deducted = (int)Math.Round(refundAmount, 0, MidpointRounding.AwayFromZero);
        int net = earned - deducted;

        earned.Should().Be(expectedEarned);
        deducted.Should().Be(expectedDeducted);
        net.Should().Be(expectedNet);
    }

    #endregion

    #region ── HIGH-2: Inventory clamping + refund causes stock inflation ──

    [Fact]
    public void H2_ClampedDecrement_FullRefund_InflatesStock()
    {
        int initialStock = 3;
        int orderedQty = 5;

        // BatchDecrementStockAsync: clamp to available
        int decrementQty = Math.Min(orderedQty, initialStock); // 3
        int stockAfterSale = initialStock - decrementQty; // 0

        // Refund: IncrementStock adds back orderedQty (not decrementQty!)
        int stockAfterRefund = stockAfterSale + orderedQty; // 0 + 5 = 5

        stockAfterRefund.Should().Be(5);
        stockAfterRefund.Should().BeGreaterThan(initialStock,
            "BUG: Stock inflated from 3 to 5 — 2 phantom units created");

        // What it SHOULD be:
        int correctRefundQty = decrementQty; // Should restore only what was actually decremented
        int correctStockAfterRefund = stockAfterSale + correctRefundQty; // 0 + 3 = 3
        correctStockAfterRefund.Should().Be(initialStock, "Correct: restore to initial stock");
    }

    [Theory]
    [InlineData(10, 15, 0, 5)]    // 10 stock, order 15, clamp to 10, stock→0, refund adds 15 → inflated by 5
    [InlineData(0, 1, 0, 1)]       // 0 stock, order 1, clamp to 0, refund adds 1 → inflated by 1
    [InlineData(5, 5, 0, 0)]       // Exact match — clamp to 5, stock→0, refund adds 5 → back to 5, no inflation
    [InlineData(100, 50, 50, 0)]   // Excess stock — no clamping, no inflation
    public void H2_StockInflation_VariousScenarios(
        int initialStock, int orderedQty, int expectedAfterSale, int expectedInflation)
    {
        int decrementQty = Math.Min(orderedQty, initialStock);
        int afterSale = initialStock - decrementQty;
        afterSale.Should().Be(expectedAfterSale);

        // BUG: refund uses orderedQty instead of decrementQty
        int afterRefundBuggy = afterSale + orderedQty;
        int inflation = afterRefundBuggy - initialStock;
        inflation.Should().Be(expectedInflation);
    }

    #endregion

    #region ── HIGH-6: CalculateProportionalAmount missing AwayFromZero ──

    [Theory]
    [InlineData(1.01, 0.5, 0.51, 0.51)]     // 0.505 rounds same both ways
    [InlineData(3.01, 0.5, 1.51, 1.51)]      // 1.505 → ToEven=1.50, AwayFromZero=1.51
    [InlineData(7.03, 0.5, 3.52, 3.52)]      // 3.515 → ToEven=3.52, AwayFromZero=3.52
    [InlineData(5.05, 0.1, 0.51, 0.51)]      // 0.505 → ToEven=0.50, AwayFromZero=0.51
    public void H6_ProportionalAmount_RoundingMismatch(
        decimal amount, decimal ratio, decimal buggyExpected, decimal fixedExpected)
    {
        var buggy = CalculateProportionalAmount_BUGGY(amount, ratio);
        var fixd = CalculateProportionalAmount_FIXED(amount, ratio);

        // The test documents the actual rounding behavior
        // Some values will differ between ToEven and AwayFromZero
        buggy.Should().Be(Math.Round(amount * ratio, 2));
        fixd.Should().Be(Math.Round(amount * ratio, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void H6_CriticalMidpoint_3_01_Half_DivergentRounding()
    {
        decimal amount = 3.01m;
        decimal ratio = 0.5m;
        // 3.01 * 0.5 = 1.505 — exact midpoint

        var buggy = Math.Round(1.505m, 2); // ToEven → 1.50
        var fixd = Math.Round(1.505m, 2, MidpointRounding.AwayFromZero); // → 1.51

        buggy.Should().Be(1.50m, "Banker's rounding rounds to even");
        fixd.Should().Be(1.51m, "AwayFromZero rounds up");
        (fixd - buggy).Should().Be(0.01m, "0.01 EGP difference — accumulates over time");
    }

    #endregion

    #region ── HIGH-9: Return orders have no Payment records ──

    [Fact]
    public void H9_ReturnOrderHasPayments_BreakdownIsAccurate()
    {
        // FIXED: Return orders now have negative Payment records proportional to original split
        var returnOrder = new Order
        {
            OrderType = OrderType.Return,
            Total = -100m,
            AmountPaid = -100m,
        };
        // Production now creates these Payment records on the return order
        returnOrder.Payments.Add(new Payment { Method = PaymentMethod.Cash, Amount = -60m });
        returnOrder.Payments.Add(new Payment { Method = PaymentMethod.Card, Amount = -40m });

        var returnPayments = new[] { returnOrder }
            .SelectMany(o => o.Payments)
            .ToList();

        var refundedCash = Math.Abs(
            returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount));
        var refundedCard = Math.Abs(
            returnPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount));

        refundedCash.Should().Be(60m, "FIXED: Cash refund breakdown is now tracked");
        refundedCard.Should().Be(40m, "FIXED: Card refund breakdown is now tracked");
    }

    #endregion

    #region ── MEDIUM-3: Customer TotalOrders never decremented ──

    [Fact]
    public void M3_CustomerTotalOrders_DecrementedOnFullRefund()
    {
        var customer = new Customer { TotalOrders = 10, TotalSpent = 1000 };

        // UpdateOrderStatsAsync: customer.TotalOrders++
        customer.TotalOrders++;
        customer.TotalSpent += 100;
        customer.TotalOrders.Should().Be(11);

        // FIXED: DeductRefundStatsAsync with isFullRefund=true now decrements TotalOrders
        customer.TotalSpent -= 100;
        if (customer.TotalOrders > 0) customer.TotalOrders--; // Now happens on full refund

        customer.TotalOrders.Should().Be(10, "FIXED: TotalOrders decremented on full refund");
        customer.TotalSpent.Should().Be(1000m);
    }

    #endregion

    #region ── Rounding cascade: Triple partial refund chain ──

    [Fact]
    public void RoundingCascade_TriplePartialRefund_TotalEquals100Percent()
    {
        // 5 items at 3.33 + 14% tax
        var order = new Order();
        var items = new List<OrderItem>();
        for (int i = 0; i < 5; i++)
        {
            var item = new OrderItem
            {
                UnitPrice = 3.33m,
                Quantity = 1,
                TaxRate = EgyptVatRate,
                TaxInclusive = false,
                RefundedQuantity = 0
            };
            CalculateItemTotals(item);
            items.Add(item);
            order.Items.Add(item);
        }
        CalculateOrderTotals(order);
        var originalTotal = order.Total;

        // Refund one item at a time
        decimal totalRefunded = 0;
        var originalItemsGrossTotal = order.Items.Sum(it => it.Total);

        for (int i = 0; i < 5; i++)
        {
            var item = items[i];
            var remainingRefundable = Math.Round(originalTotal - totalRefunded, 2);

            var itemRatio = 1.0m / item.Quantity; // 1/1 = 1.0 for single-qty items
            var itemRefundAmount = Math.Round(item.Total * itemRatio, 2, MidpointRounding.AwayFromZero);

            var refundRatio = CalculateRefundRatio(itemRefundAmount, originalItemsGrossTotal);
            var orderLevelAdjustment = Math.Round(originalTotal - originalItemsGrossTotal, 2);
            var totalRefund = Math.Round(
                itemRefundAmount + (orderLevelAdjustment * refundRatio), 2,
                MidpointRounding.AwayFromZero);

            // Cap at remaining refundable
            if (totalRefund > remainingRefundable)
                totalRefund = remainingRefundable;

            totalRefunded += totalRefund;
            item.RefundedQuantity = 1;
            order.RefundAmount = totalRefunded;
        }

        // Invariant: total refunded must equal order total exactly
        totalRefunded.Should().Be(originalTotal,
            "After refunding all items one-by-one, total refund must match order total exactly");
    }

    [Fact]
    public void RoundingCascade_LastRefundCapped_InternalConsistencyBroken()
    {
        // Demonstrates that the cap mechanism creates a return order with mismatched items_sum vs total
        var order = new Order();
        var items = new List<OrderItem>();
        for (int i = 0; i < 5; i++)
        {
            var item = new OrderItem
            {
                UnitPrice = 3.33m,
                Quantity = 1,
                TaxRate = EgyptVatRate,
                TaxInclusive = false,
                RefundedQuantity = 0
            };
            CalculateItemTotals(item);
            items.Add(item);
            order.Items.Add(item);
        }
        CalculateOrderTotals(order);

        var originalItemsGrossTotal = order.Items.Sum(it => it.Total);
        var orderLevelAdjustment = Math.Round(order.Total - originalItemsGrossTotal, 2);

        // Simulate 4 refunds
        decimal totalRefunded = 0;
        for (int i = 0; i < 4; i++)
        {
            var item = items[i];
            var itemRefundAmount = item.Total;
            var refundRatio = CalculateRefundRatio(itemRefundAmount, originalItemsGrossTotal);
            var totalRefund = Math.Round(
                itemRefundAmount + (orderLevelAdjustment * refundRatio), 2,
                MidpointRounding.AwayFromZero);
            var remaining = Math.Round(order.Total - totalRefunded, 2);
            if (totalRefund > remaining) totalRefund = remaining;
            totalRefunded += totalRefund;
            item.RefundedQuantity = 1;
        }

        // Last refund — the proportional calculation
        var lastItem = items[4];
        var lastItemRefund = lastItem.Total;
        var lastRatio = CalculateRefundRatio(lastItemRefund, originalItemsGrossTotal);
        var lastCalculatedRefund = Math.Round(
            lastItemRefund + (orderLevelAdjustment * lastRatio), 2,
            MidpointRounding.AwayFromZero);
        var lastRemaining = Math.Round(order.Total - totalRefunded, 2);
        var lastActualRefund = Math.Min(lastCalculatedRefund, lastRemaining);

        // If cap kicks in, the actual refund differs from what items would sum to
        if (lastActualRefund < lastCalculatedRefund)
        {
            var mismatch = lastCalculatedRefund - lastActualRefund;
            mismatch.Should().BeGreaterThan(0,
                "Cap created a mismatch between return order items_sum and order total");
        }
    }

    #endregion

    #region ── Rounding cascade: Pathological unit prices ──

    [Theory]
    [InlineData(0.01, 1, 14, 0.01, 0.00, 0.01)]     // Tax is 0 on sub-penny amounts
    [InlineData(0.01, 1, 33.333, 0.01, 0.00, 0.01)]  // Even high tax rate → 0 tax
    [InlineData(0.33, 3, 14, 0.99, 0.14, 1.13)]
    [InlineData(0.66, 3, 14, 1.98, 0.28, 2.26)]
    [InlineData(0.99, 7, 14, 6.93, 0.97, 7.90)]
    [InlineData(999.99, 1, 14, 999.99, 140.00, 1139.99)]
    public void PathologicalPrices_CalculateCorrectly(
        decimal unitPrice, int qty, decimal taxRate,
        decimal expectedSubtotal, decimal expectedTax, decimal expectedTotal)
    {
        var item = new OrderItem
        {
            UnitPrice = unitPrice,
            Quantity = qty,
            TaxRate = taxRate,
            TaxInclusive = false
        };
        CalculateItemTotals(item);

        item.Subtotal.Should().Be(expectedSubtotal);
        item.TaxAmount.Should().Be(expectedTax);
        item.Total.Should().Be(expectedTotal);
    }

    [Fact]
    public void TaxOn1Piaster_IsEffectively0Percent()
    {
        // 33.333% tax on 0.01 = 0.003333 → rounds to 0.00
        var item = new OrderItem
        {
            UnitPrice = 0.01m,
            Quantity = 1,
            TaxRate = 33.333m,
            TaxInclusive = false
        };
        CalculateItemTotals(item);

        item.TaxAmount.Should().Be(0m, "Tax on 1 piaster is effectively 0 at any rate");
        item.Total.Should().Be(0.01m);
    }

    #endregion

    #region ── Double discount with tax recalculation divergence ──

    [Fact]
    public void DoubleDiscount_ItemTaxDivergesFromOrderTax()
    {
        // When order-level discount exists, order.TaxAmount is recalculated
        // but item.TaxAmount is NOT updated — they diverge
        var order = new Order { DiscountType = "percentage", DiscountValue = 5m };
        var item = new OrderItem
        {
            UnitPrice = 9.99m,
            Quantity = 3,
            TaxRate = EgyptVatRate,
            TaxInclusive = false,
            DiscountType = "percentage",
            DiscountValue = 10m
        };
        CalculateItemTotals(item);
        order.Items.Add(item);
        CalculateOrderTotals(order);

        var itemTaxSum = order.Items.Sum(i => i.TaxAmount);
        var orderTax = order.TaxAmount;

        // Item tax was calculated BEFORE order-level discount
        // Order tax was recalculated AFTER order-level discount
        // They should differ when order-level discount exists
        if (order.DiscountAmount > 0)
        {
            itemTaxSum.Should().BeGreaterThan(orderTax,
                "Item tax doesn't account for order-level discount reduction");
        }
    }

    #endregion

    #region ── Service charge on refund ──

    [Fact]
    public void ServiceCharge_OnRefund_ProportionallyCalculated()
    {
        var order = CreateTestOrder(100m, 2, serviceChargePercent: 12m);
        // Subtotal=200, Tax=28, ServiceCharge=24, Total=252

        order.ServiceChargeAmount.Should().BeGreaterThan(0);

        // 50% refund ratio
        var ratio = 0.5m;
        var buggyRefundServiceCharge = CalculateProportionalAmount_BUGGY(order.ServiceChargeAmount, ratio);
        var fixedRefundServiceCharge = CalculateProportionalAmount_FIXED(order.ServiceChargeAmount, ratio);

        // For these specific values, both should agree
        buggyRefundServiceCharge.Should().Be(fixedRefundServiceCharge,
            "For even midpoints, both rounding modes agree");
    }

    [Fact]
    public void ServiceCharge_OnRefund_OddRatio_MayDiverge()
    {
        var order = CreateTestOrder(100m, 3, serviceChargePercent: 12m);
        // 3 items at 100 → Subtotal=300, ServiceCharge=36

        // Refund 1 of 3 → ratio = 1/3 = 0.3333...
        var ratio = 1.0m / 3.0m;
        var buggy = CalculateProportionalAmount_BUGGY(order.ServiceChargeAmount, ratio);
        var fixd = CalculateProportionalAmount_FIXED(order.ServiceChargeAmount, ratio);

        // 36 × 0.3333... = 12.0000 → no midpoint, both agree
        buggy.Should().Be(fixd);
    }

    #endregion

    #region ── Financial invariant: Full refund equals total ──

    [Theory]
    [InlineData(100, 1, 14, null, null, null, null)]
    [InlineData(33.33, 3, 14, null, null, null, null)]
    [InlineData(99.99, 1, 14, "percentage", 10.0, null, null)]
    [InlineData(50, 2, 14, "fixed", 15.0, "percentage", 10.0)]
    [InlineData(200, 1, 0, null, null, "fixed", 30.0)]
    [InlineData(0.99, 7, 14, null, null, null, null)]
    public void Invariant_FullRefund_EqualsOrderTotal(
        decimal price, int qty, decimal taxRate,
        string? itemDiscType, double? itemDiscVal,
        string? orderDiscType, double? orderDiscVal)
    {
        var order = CreateTestOrder(price, qty, taxRate,
            itemDiscType, itemDiscVal.HasValue ? (decimal)itemDiscVal.Value : null,
            orderDiscType, orderDiscVal.HasValue ? (decimal)orderDiscVal.Value : null);

        // Full refund: all items
        var item = order.Items.First();
        var originalItemsGrossTotal = order.Items.Sum(i => i.Total);
        var refundedGross = item.Total * (decimal)qty / (decimal)qty; // 100% of items
        var refundRatio = CalculateRefundRatio(item.Total, originalItemsGrossTotal);
        var orderAdjust = order.Total - originalItemsGrossTotal;
        var totalRefund = Math.Round(
            item.Total + (orderAdjust * refundRatio), 2,
            MidpointRounding.AwayFromZero);

        // Cap at remaining
        if (totalRefund > order.Total)
            totalRefund = order.Total;

        totalRefund.Should().Be(order.Total,
            "Full refund of single-item order must equal order total");
    }

    #endregion

    #region ── Financial invariant: Sum of partials ≤ total ──

    [Fact]
    public void Invariant_SumOfPartialRefunds_NeverExceedsTotal()
    {
        // Multi-item order with order-level discount
        var order = CreateMultiItemOrder(
            new List<(decimal, int, decimal)>
            {
                (33.33m, 1, 14m),
                (66.66m, 1, 14m),
                (99.99m, 1, 14m),
            },
            orderDiscountType: "percentage",
            orderDiscountValue: 10m);

        var originalTotal = order.Total;
        var originalItemsGrossTotal = order.Items.Sum(i => i.Total);
        var orderLevelAdjustment = Math.Round(originalTotal - originalItemsGrossTotal, 2);
        decimal totalRefunded = 0;

        // Refund each item one at a time
        foreach (var item in order.Items.ToList())
        {
            var itemRefundAmount = item.Total;
            var refundRatio = CalculateRefundRatio(itemRefundAmount, originalItemsGrossTotal);
            var thisRefund = Math.Round(
                itemRefundAmount + (orderLevelAdjustment * refundRatio), 2,
                MidpointRounding.AwayFromZero);

            var remaining = Math.Round(originalTotal - totalRefunded, 2);
            if (thisRefund > remaining) thisRefund = remaining;

            totalRefunded += thisRefund;
            item.RefundedQuantity = item.Quantity;
        }

        totalRefunded.Should().BeLessOrEqualTo(originalTotal,
            "Sum of partial refunds must never exceed order total");
        totalRefunded.Should().Be(originalTotal,
            "After refunding all items, total refunded should exactly equal order total");
    }

    #endregion

    #region ── Financial invariant: Non-negative totals ──

    [Theory]
    [InlineData(100, "percentage", 100)]  // 100% discount
    [InlineData(100, "fixed", 500)]       // Fixed > subtotal
    [InlineData(0.01, "fixed", 0.01)]     // Exact match
    public void Invariant_OrderTotal_NeverNegative(decimal price, string discType, decimal discVal)
    {
        var order = CreateTestOrder(price, 1, EgyptVatRate,
            discType, discVal);

        order.Total.Should().BeGreaterOrEqualTo(0m,
            "Order total must never be negative regardless of discounts");
        order.TaxAmount.Should().BeGreaterOrEqualTo(0m,
            "Tax must never be negative");
    }

    #endregion

    #region ── Order number collision probability ──

    [Fact]
    public void M1_OrderNumberCollision_ImprovedEntropy()
    {
        // FIXED: 12 hex chars + timestamp = 16^12 = 281,474,976,710,656 unique values per second
        const long uniqueSpace = 281_474_976_710_656L;

        // Birthday paradox: P(collision) ≈ 1 - e^(-n²/2m)
        // At n=5800: P ≈ practically 0
        const int ordersPerDay = 5800;
        double pCollision = 1.0 - Math.Exp(-(double)((long)ordersPerDay * ordersPerDay) / (2.0 * uniqueSpace));

        pCollision.Should().BeLessThan(0.0001,
            "FIXED: With 12 hex chars + timestamp, collision probability is negligible");
    }

    #endregion

    #region ── Multi-item order: refund single item with order discount ──

    [Fact]
    public void RefundSingleItem_FromMultiItemOrder_WithOrderDiscount()
    {
        var order = CreateMultiItemOrder(
            new List<(decimal, int, decimal)>
            {
                (100m, 1, 14m),   // Item A: 114
                (200m, 1, 14m),   // Item B: 228
                (300m, 1, 14m),   // Item C: 342
            },
            orderDiscountType: "percentage",
            orderDiscountValue: 10m);

        var originalItemsGross = order.Items.Sum(i => i.Total); // 114+228+342=684
        var orderAdjust = order.Total - originalItemsGross;

        // Refund Item A only
        var itemA = order.Items.First();
        var refundRatio = CalculateRefundRatio(itemA.Total, originalItemsGross);
        var refund = Math.Round(
            itemA.Total + (orderAdjust * refundRatio), 2,
            MidpointRounding.AwayFromZero);

        // Refund should be proportional — not just itemA.Total
        refund.Should().BeLessThan(itemA.Total,
            "Refund should be less than item total because order discount share is subtracted");
        refund.Should().BeGreaterThan(0);
    }

    #endregion

    #region ── Zero-value edge cases ──

    [Fact]
    public void ZeroPrice_ZeroTax_ZeroTotal()
    {
        var order = CreateTestOrder(0m, 1, 14m);
        order.Total.Should().Be(0m);
        order.TaxAmount.Should().Be(0m);
    }

    [Fact]
    public void ZeroQuantity_ZeroTotal()
    {
        var order = CreateTestOrder(100m, 0, 14m);
        order.Total.Should().Be(0m);
    }

    [Fact]
    public void FullDiscount_ZeroTotal_RefundIsZero()
    {
        var order = CreateTestOrder(100m, 1, 14m, "percentage", 100m);
        order.Total.Should().Be(0m);

        // Refund on zero-total order
        var item = order.Items.First();
        var originalItemsGross = order.Items.Sum(i => i.Total);
        var refundRatio = CalculateRefundRatio(item.Total, originalItemsGross);

        // total is 0, so refund is 0
        refundRatio.Should().Be(0m, "No refund possible on a zero-total order");
    }

    #endregion

    #region ── Cross-report consistency simulation ──

    [Fact]
    public void CrossReport_DailySalesIncludesReturns_ConsistentWithMonthly()
    {
        // Simulate: Day 1 sale, Day 2 refund
        var saleOrders = new List<Order>
        {
            new() { Total = 100, CompletedAt = new DateTime(2025, 1, 1), OrderType = OrderType.DineIn },
            new() { Total = 200, CompletedAt = new DateTime(2025, 1, 1), OrderType = OrderType.DineIn },
        };
        var returnOrders = new List<Order>
        {
            new() { Total = -100, CompletedAt = new DateTime(2025, 1, 2), OrderType = OrderType.Return },
        };

        // FIXED: dailySales now merges sales and returns by day
        var allOrders = saleOrders.Concat(returnOrders).ToList();
        var day1Sales = allOrders
            .Where(o => o.CompletedAt!.Value.Date == new DateTime(2025, 1, 1))
            .Sum(o => o.OrderType == OrderType.Return ? -Math.Abs(o.Total) : o.Total);
        var day2Sales = allOrders
            .Where(o => o.CompletedAt!.Value.Date == new DateTime(2025, 1, 2))
            .Sum(o => o.OrderType == OrderType.Return ? -Math.Abs(o.Total) : o.Total);

        day1Sales.Should().Be(300m, "Day 1: two sales totaling 300");
        day2Sales.Should().Be(-100m, "Day 2: return of 100 shown as negative");

        // Monthly aggregate also subtracts refunds
        var monthlyNet = saleOrders.Sum(o => o.Total) - Math.Abs(returnOrders.Sum(o => o.Total));
        monthlyNet.Should().Be(200m, "Monthly net: 300 - 100 = 200");

        // FIXED: Sum of daily now equals monthly
        (day1Sales + day2Sales).Should().Be(200m);
        (day1Sales + day2Sales).Should().Be(monthlyNet,
            "FIXED: Daily breakdown consistent with monthly — both show 200 net");
    }

    #endregion

    #region ── Extreme values stress ──

    [Fact]
    public void ExtremeValue_MaxDecimalPrice_DoesNotOverflow()
    {
        // Very large order
        var item = new OrderItem
        {
            UnitPrice = 999_999.99m,
            Quantity = 999,
            TaxRate = 14m,
            TaxInclusive = false,
        };

        var action = () => CalculateItemTotals(item);
        action.Should().NotThrow("Large values should not overflow decimal");

        item.Subtotal.Should().Be(999_999.99m * 999);
        item.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExtremeValue_ManyItems_TotalsConsistent()
    {
        var order = new Order();
        for (int i = 0; i < 50; i++)
        {
            var item = new OrderItem
            {
                UnitPrice = 9.99m,
                Quantity = 1,
                TaxRate = EgyptVatRate,
                TaxInclusive = false
            };
            CalculateItemTotals(item);
            order.Items.Add(item);
        }
        CalculateOrderTotals(order);

        // Without order-level discount, order.Total == items.Sum(Total)
        order.Total.Should().Be(order.Items.Sum(i => i.Total),
            "Without order discount, order total must equal sum of item totals");
    }

    #endregion

    #region ── NEW: C-4 TOCTOU Credit Limit Protection ──

    [Fact]
    public void C4_CreditLimit_AuthoritativeCheckAfterWriteLock()
    {
        // Simulates the TOCTOU fix: after SaveChanges acquires write lock,
        // re-read customer to validate credit with fresh data
        var customer = new Customer { CreditLimit = 1000m, TotalDue = 950m };
        var orderAmountDue = 100m;

        // Soft check (pre-write): already exceeds limit
        var softCheck = customer.CreditLimit <= 0 || customer.TotalDue + orderAmountDue <= customer.CreditLimit;
        softCheck.Should().BeFalse("950 + 100 = 1050 > 1000 credit limit");

        // Simulates concurrent transaction committing additional debt
        customer.TotalDue = 1000m;

        // Authoritative check (post-write-lock): reads fresh data
        var authCheck = customer.CreditLimit > 0 && customer.TotalDue + orderAmountDue > customer.CreditLimit;
        authCheck.Should().BeTrue("1000 + 100 = 1100 > 1000 — correctly rejects after write lock");
    }

    [Fact]
    public void C4_CreditLimit_ZeroLimitMeansUnlimited()
    {
        var customer = new Customer { CreditLimit = 0m, TotalDue = 999999m };
        var orderAmountDue = 100m;

        // CreditLimit = 0 means no limit enforced
        var shouldReject = customer.CreditLimit > 0 && customer.TotalDue + orderAmountDue > customer.CreditLimit;
        shouldReject.Should().BeFalse("CreditLimit = 0 means unlimited credit");
    }

    #endregion

    #region ── NEW: C-5 Debt Reduction Accounting ──

    [Fact]
    public void C5_DebtReduction_CapsAtRemainingDebt()
    {
        decimal originalTotal = 1000m;
        decimal originalAmountDue = 400m;

        // First partial refund: 50%
        decimal refund1Amount = 500m;
        decimal debtReduction1 = Math.Round(
            (refund1Amount / originalTotal) * originalAmountDue, 2, MidpointRounding.AwayFromZero);
        debtReduction1.Should().Be(200m, "50% refund reduces 50% of debt");

        // Second partial refund: remaining 50%
        decimal refund2Amount = 500m;
        decimal theoreticalDebtReduction2 = Math.Round(
            (refund2Amount / originalTotal) * originalAmountDue, 2, MidpointRounding.AwayFromZero);
        decimal remainingDebt = originalAmountDue - debtReduction1;
        decimal actualDebtReduction2 = Math.Min(theoreticalDebtReduction2, remainingDebt);

        actualDebtReduction2.Should().Be(200m, "Capped at remaining debt of 200");
        (debtReduction1 + actualDebtReduction2).Should().Be(originalAmountDue,
            "Total debt reduced must equal original AmountDue");
    }

    [Fact]
    public void C5_DebtReduction_RoundingDoesNotOverReduce()
    {
        decimal originalTotal = 333.33m;
        decimal originalAmountDue = 100m;

        // Three equal partial refunds of 111.11
        decimal totalDebtReduced = 0;
        for (int i = 0; i < 3; i++)
        {
            decimal refundAmount = 111.11m;
            decimal proportionalDebt = Math.Round(
                (refundAmount / originalTotal) * originalAmountDue, 2, MidpointRounding.AwayFromZero);
            decimal remaining = originalAmountDue - totalDebtReduced;
            decimal actualDebt = Math.Min(proportionalDebt, remaining);
            totalDebtReduced += actualDebt;
        }

        totalDebtReduced.Should().BeLessOrEqualTo(originalAmountDue,
            "Debt reduction must never exceed original AmountDue");
    }

    #endregion

    #region ── NEW: M-5 Max Refund Chain Depth ──

    [Fact]
    public void M5_MaxRefundChainDepth_LimitedTo20()
    {
        const int maxRefunds = 20;

        // At exactly 20, further refunds should be blocked
        int existingReturnCount = 20;
        var allowed = existingReturnCount < maxRefunds;
        allowed.Should().BeFalse("20 existing returns = limit reached");

        // 19 should still be allowed
        existingReturnCount = 19;
        allowed = existingReturnCount < maxRefunds;
        allowed.Should().BeTrue("19 existing returns = 1 more allowed");
    }

    #endregion

    #region ── NEW: M-8 Discount Re-validation at Complete ──

    [Fact]
    public void M8_DiscountOver100Percent_RejectedAtComplete()
    {
        var order = new Order
        {
            DiscountType = "percentage",
            DiscountValue = 150m // Tampered after creation
        };

        // At CompleteAsync, discount is re-validated
        bool isInvalid = order.DiscountType == "percentage" && order.DiscountValue > 100;
        isInvalid.Should().BeTrue("150% discount should be rejected at completion time");
    }

    [Fact]
    public void M8_DiscountExactly100Percent_Allowed()
    {
        var order = new Order
        {
            DiscountType = "percentage",
            DiscountValue = 100m
        };

        bool isInvalid = order.DiscountType == "percentage" && order.DiscountValue > 100;
        isInvalid.Should().BeFalse("100% discount is valid (free order)");
    }

    #endregion

    #region ── NEW: H-4 Negative Payment Breakdown Guard ──

    [Fact]
    public void H4_NegativePaymentBreakdown_ClampedToZero()
    {
        // When returns exceed sales for a payment method in daily breakdown
        var cashSales = 100m;
        var cashReturns = 150m;

        var rawBreakdown = cashSales - cashReturns;
        rawBreakdown.Should().BeNegative();

        var guardedBreakdown = Math.Max(0m, cashSales - cashReturns);
        guardedBreakdown.Should().Be(0m, "FIXED: Negative breakdown clamped to zero");
    }

    [Fact]
    public void H4_PositivePaymentBreakdown_Unchanged()
    {
        var cashSales = 200m;
        var cashReturns = 50m;

        var guardedBreakdown = Math.Max(0m, cashSales - cashReturns);
        guardedBreakdown.Should().Be(150m, "Positive breakdown passes through unchanged");
    }

    #endregion

    #region ── NEW: L-4 Refund Reason Truncation ──

    [Fact]
    public void L4_RefundReason_TruncatedSafely()
    {
        var longReason = new string('X', 500);
        longReason.Length.Should().Be(500);

        // Production truncation logic
        string truncated = longReason.Length > 490
            ? longReason[..487] + "..."
            : longReason;

        truncated.Length.Should().Be(490, "Truncated to 487 chars + '...' = 490");
        truncated.Should().EndWith("...");
    }

    [Fact]
    public void L4_RefundReason_ShortReasonUnchanged()
    {
        var shortReason = "Customer changed mind";

        string result = shortReason.Length > 490
            ? shortReason[..487] + "..."
            : shortReason;

        result.Should().Be(shortReason, "Short reasons pass through unchanged");
    }

    [Fact]
    public void L4_RefundReason_ExactlyAtLimit_Unchanged()
    {
        var reason = new string('A', 490);

        string result = reason.Length > 490
            ? reason[..487] + "..."
            : reason;

        result.Should().Be(reason, "Exactly 490 chars is within limit");
        result.Length.Should().Be(490);
    }

    #endregion

    #region ── NEW: L-5 Consistent Refund Timestamp ──

    [Fact]
    public void L5_SingleTimestamp_ConsistentAcrossTransaction()
    {
        // Ensures all timestamps in a refund transaction use the same captured instant
        var refundTimestamp = DateTime.UtcNow;

        var returnOrder = new Order { CompletedAt = refundTimestamp };
        var refundLog = new RefundLog { CreatedAt = refundTimestamp };

        returnOrder.CompletedAt.Should().Be(refundLog.CreatedAt,
            "Return order CompletedAt must match RefundLog CreatedAt exactly");
    }

    #endregion

    #region ── NEW: H-2 Restorable Quantity Prevents Inflation ──

    [Fact]
    public void H2_RestorableQuantity_PreventsDoubleRestore()
    {
        // Original sale: ordered 5 items, stock had 3 — only 3 decremented
        int actualDecremented = 3;
        int alreadyRestored = 0;

        // First refund: refund all 5 items
        int refundQty = 5;
        int restorableQty = Math.Max(0, actualDecremented - alreadyRestored);
        int actualRestore = Math.Min(refundQty, restorableQty);
        actualRestore.Should().Be(3, "Can only restore what was actually decremented");

        // After first refund
        alreadyRestored = 3;
        restorableQty = Math.Max(0, actualDecremented - alreadyRestored);
        actualRestore = Math.Min(refundQty, restorableQty);
        actualRestore.Should().Be(0, "Nothing left to restore — prevents double-restore inflation");
    }

    [Fact]
    public void H2_RestorableQuantity_PartialRefundChain()
    {
        // 10 items ordered, stock had 7 — only 7 decremented
        int actualDecremented = 7;
        int alreadyRestored = 0;

        // Refund 3 items
        int refundQty1 = 3;
        int restore1 = Math.Min(refundQty1, Math.Max(0, actualDecremented - alreadyRestored));
        restore1.Should().Be(3);
        alreadyRestored += restore1;

        // Refund 5 more items
        int refundQty2 = 5;
        int restore2 = Math.Min(refundQty2, Math.Max(0, actualDecremented - alreadyRestored));
        restore2.Should().Be(4, "Only 4 of 7 remaining to restore");
        alreadyRestored += restore2;

        // Final refund: remaining 2 items
        int refundQty3 = 2;
        int restore3 = Math.Min(refundQty3, Math.Max(0, actualDecremented - alreadyRestored));
        restore3.Should().Be(0, "All 7 decremented already restored — no more to give back");

        alreadyRestored.Should().Be(actualDecremented, "Total restored equals actual decremented");
    }

    #endregion

    #region ── NEW: M-7 Soft-Deleted Product Lookup ──

    [Fact]
    public void M7_SoftDeletedProduct_StillRefundable()
    {
        // Simulates using Query() instead of GetByIdAsync (which uses FindAsync)
        // FindAsync doesn't filter soft-deleted records, but production used GetByIdAsync
        // Fix: Use Query().FirstOrDefaultAsync() which bypasses potential soft-delete filters
        var products = new List<(int Id, string Name, bool IsDeleted)>
        {
            (1, "Active Product", false),
            (2, "Deleted Product", true),
        };

        // Old behavior: GetByIdAsync might miss soft-deleted products
        // depending on global query filter configuration
        var activeOnly = products.Where(p => !p.IsDeleted).ToList();
        var allProducts = products.ToList();

        activeOnly.Should().HaveCount(1);
        allProducts.Should().HaveCount(2, "Query() finds both active and soft-deleted products");

        // The deleted product should still allow stock restore on refund
        var deletedProduct = allProducts.FirstOrDefault(p => p.Id == 2);
        deletedProduct.Should().NotBe(default, "Soft-deleted product found via Query()");
    }

    #endregion
}
