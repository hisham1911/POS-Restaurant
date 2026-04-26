namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using static KasserPro.Application.Common.DateTimeHelper;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ReportService> _logger;
    private readonly record struct ShiftSummaryFinancials(
        decimal CollectedCash,
        decimal CollectedCard,
        decimal CollectedFawry,
        decimal CollectedOther,
        decimal TotalSales,
        decimal TotalCollected,
        decimal DeferredAmount);

    public ReportService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILogger<ReportService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ApiResponse<DailyReportDto>> GetDailyReportAsync(DateTime? date = null)
    {
        var reportDate = date?.Date ?? DateTime.UtcNow.Date;
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var (utcFrom, utcTo) = ToUtcDayRange(reportDate);

        // Get branch info for report
        var branch = await _unitOfWork.Branches.GetByIdAsync(branchId);

        // Get shifts closed on this date (Egypt local time)
        var shifts = await _unitOfWork.Shifts.Query()
            .Include(s => s.User)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Items)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Payments)
            .Where(s => s.TenantId == tenantId
                     && s.BranchId == branchId
                     && s.IsClosed
                     && s.ClosedAt >= utcFrom
                     && s.ClosedAt < utcTo)
            .ToListAsync();

        // Get all orders from these shifts
        var orders = shifts.SelectMany(s => s.Orders).ToList();

        // Filter completed orders for sales calculations (EXCLUDE Return orders)
        var completedOrders = orders
            .Where(o => (o.Status == OrderStatus.Completed
                      || o.Status == OrderStatus.PartiallyRefunded
                      || o.Status == OrderStatus.Refunded)
                     && o.OrderType != OrderType.Return)
            .ToList();

        // Get return orders separately for refund calculations
        var returnOrders = orders
            .Where(o => (o.Status == OrderStatus.Completed
                      || o.Status == OrderStatus.PartiallyRefunded
                      || o.Status == OrderStatus.Refunded)
                     && o.OrderType == OrderType.Return)
            .ToList();

        _logger.LogDebug("Daily Report (Shift-Based): Date={Date}, Shifts={ShiftCount}, TotalOrders={OrderCount}, CompletedOrders={CompletedCount}, ReturnOrders={ReturnCount}",
            reportDate, shifts.Count, orders.Count, completedOrders.Count, returnOrders.Count);

        // Calculate payment breakdown
        var allPayments = completedOrders.SelectMany(o => o.Payments).ToList();
        // FIX: Include return order payments in breakdown to account for refunded cash
        var returnPayments = returnOrders.SelectMany(o => o.Payments).ToList();
        var refundedCash = Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount));
        var refundedCard = Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount));
        var refundedFawry = Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount));
        var refundedOther = Math.Abs(returnPayments.Where(p => p.Method != PaymentMethod.Cash
                                                              && p.Method != PaymentMethod.Card
                                                              && p.Method != PaymentMethod.Fawry).Sum(p => p.Amount));

        // FIX H-4: Ensure payment breakdown doesn't go negative (display as 0 minimum)
        var totalCash = Math.Round(Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount) - refundedCash), 2);
        var totalCard = Math.Round(Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount) - refundedCard), 2);
        var totalFawry = Math.Round(Math.Max(0, allPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount) - refundedFawry), 2);
        var totalOther = Math.Round(Math.Max(0, allPayments.Where(p => p.Method != PaymentMethod.Cash
                                              && p.Method != PaymentMethod.Card
                                              && p.Method != PaymentMethod.Fawry).Sum(p => p.Amount) - refundedOther), 2);

        // Calculate sales totals (INCLUDING refund adjustments)
        var grossSales = completedOrders.Sum(o => o.Subtotal);
        // Total discount = order-level discounts + item-level discounts
        // FIX: Subtract return order discounts to avoid overstating discounts
        var totalItemDiscounts = completedOrders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
        var totalOrderDiscounts = completedOrders.Sum(o => o.DiscountAmount);
        var returnItemDiscounts = Math.Abs(returnOrders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount));
        var returnOrderDiscounts = Math.Abs(returnOrders.Sum(o => o.DiscountAmount));
        var totalDiscount = Math.Round((totalItemDiscounts + totalOrderDiscounts) - (returnItemDiscounts + returnOrderDiscounts), 2);
        var totalTax = Math.Round(completedOrders.Sum(o => o.TaxAmount), 2);
        var totalSales = Math.Round(completedOrders.Sum(o => o.Total), 2);
        var netSales = Math.Round(grossSales - totalDiscount, 2);

        // Calculate refunds from return orders
        var totalRefunds = Math.Round(Math.Abs(returnOrders.Sum(o => o.Total)), 2); // Make positive for display

        // Adjust sales totals by subtracting refunds for ACTUAL sales
        var actualGrossSales = Math.Round(grossSales - Math.Abs(returnOrders.Sum(o => o.Subtotal)), 2);
        var actualTotalTax = Math.Round(totalTax - Math.Abs(returnOrders.Sum(o => o.TaxAmount)), 2);
        var actualTotalSales = Math.Round(totalSales - totalRefunds, 2);
        var actualNetSales = Math.Round(netSales - Math.Abs(returnOrders.Sum(o => o.Subtotal - o.DiscountAmount)), 2);
        var totalCollected = Math.Round(totalCash + totalCard + totalFawry + totalOther, 2);
        var totalDeferred = Math.Round(actualTotalSales - totalCollected, 2);

        // Top products - Calculate NET quantities (sales - returns)
        var allSalesItems = completedOrders.SelectMany(o => o.Items).ToList();
        var allReturnItems = returnOrders.SelectMany(o => o.Items).ToList();

        _logger.LogDebug("Sales items: {SalesCount}, Return items: {ReturnCount}", allSalesItems.Count, allReturnItems.Count);

        // Group sales items (تجاهل المنتجات المخصصة)
        var salesByProduct = allSalesItems
            .Where(i => i.ProductId.HasValue) // فقط المنتجات من الكتالوج
            .GroupBy(i => new { ProductId = i.ProductId!.Value, i.ProductName })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                QuantitySold = g.Sum(i => i.Quantity),
                TotalSales = g.Sum(i => i.Total)
            })
            .ToList();

        // Group return items
        var returnsByProduct = allReturnItems
            .Where(i => i.ProductId.HasValue) // تجاهل المنتجات المخصصة
            .GroupBy(i => i.ProductId!.Value) // Group by ProductId value directly
            .Select(g => new
            {
                ProductId = g.Key,
                QuantityReturned = Math.Abs(g.Sum(i => i.Quantity)), // Make positive
                TotalReturns = Math.Abs(g.Sum(i => i.Total)) // Make positive
            })
            .ToDictionary(x => x.ProductId);

        // Calculate NET sales (sales - returns) for each product
        var topProducts = salesByProduct
            .Select(s => new TopProductDto
            {
                ProductId = s.ProductId,
                ProductName = s.ProductName,
                QuantitySold = s.QuantitySold - (returnsByProduct.ContainsKey(s.ProductId) ? returnsByProduct[s.ProductId].QuantityReturned : 0),
                TotalSales = s.TotalSales - (returnsByProduct.ContainsKey(s.ProductId) ? returnsByProduct[s.ProductId].TotalReturns : 0)
            })
            .Where(p => p.QuantitySold > 0) // Only show products with net positive sales
            .OrderByDescending(p => p.QuantitySold)
            .Take(10)
            .ToList();

        _logger.LogDebug("Top products (after returns): {Count}", topProducts.Count);

        // Hourly breakdown
        var hourlySales = completedOrders
            .GroupBy(o => o.CompletedAt?.Hour ?? o.CreatedAt.Hour)
            .Select(g => new HourlySalesDto
            {
                Hour = g.Key,
                OrderCount = g.Count(),
                Sales = g.Sum(o => o.Total)
            })
            .OrderBy(h => h.Hour)
            .ToList();

        // Shift summaries - compute payment breakdown from actual Payment records for accuracy
        var shiftSummaries = shifts.Select(s =>
        {
            var financials = CalculateShiftSummaryFinancials(s.Orders ?? new List<Domain.Entities.Order>());

            return new ShiftSummaryDto
            {
                ShiftId = s.Id,
                UserName = s.User?.Name ?? "غير معروف",
                OpenedAt = s.OpenedAt,
                ClosedAt = s.ClosedAt!.Value,
                TotalOrders = s.TotalOrders,
                TotalCash = financials.CollectedCash,
                TotalCard = financials.CollectedCard,
                TotalFawry = financials.CollectedFawry,
                TotalOther = financials.CollectedOther,
                TotalSales = financials.TotalSales,
                TotalCollected = financials.TotalCollected,
                DeferredAmount = financials.DeferredAmount,
                CollectedCash = financials.CollectedCash,
                CollectedCard = financials.CollectedCard,
                CollectedFawry = financials.CollectedFawry,
                CollectedOther = financials.CollectedOther,
                IsForceClosed = s.IsForceClosed,
                ForceCloseReason = s.ForceCloseReason
            };
        }).ToList();

        var report = new DailyReportDto
        {
            Date = reportDate,
            BranchId = branchId,
            BranchName = branch?.Name,

            // Shift Information
            TotalShifts = shifts.Count,
            Shifts = shiftSummaries,

            // Order Counts
            TotalOrders = orders.Count,
            CompletedOrders = completedOrders.Count,
            CancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
            PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Draft),

            // Sales Totals (ACTUAL - after subtracting refunds)
            GrossSales = actualGrossSales,
            TotalDiscount = totalDiscount, // Discount stays the same (from original orders)
            NetSales = actualNetSales,
            TotalTax = actualTotalTax,
            TotalSales = actualTotalSales,
            ActualTotalSales = actualTotalSales,
            TotalRefunds = totalRefunds,

            // Payment Breakdown
            TotalCash = totalCash,
            TotalCard = totalCard,
            TotalFawry = totalFawry,
            TotalOther = totalOther,
            TotalCollected = totalCollected,
            TotalDeferred = totalDeferred,

            // Details
            TopProducts = topProducts,
            HourlySales = hourlySales
        };

        return ApiResponse<DailyReportDto>.Ok(report);
    }

    private static ShiftSummaryFinancials CalculateShiftSummaryFinancials(
        IEnumerable<Domain.Entities.Order> orders)
    {
        var completedOrders = orders
            .Where(o => o.Status == OrderStatus.Completed
                     || o.Status == OrderStatus.PartiallyRefunded
                     || o.Status == OrderStatus.Refunded)
            .ToList();

        var salesOrders = completedOrders
            .Where(o => o.OrderType != OrderType.Return)
            .ToList();
        var returnOrders = completedOrders
            .Where(o => o.OrderType == OrderType.Return)
            .ToList();

        var salesPayments = salesOrders
            .SelectMany(o => o.Payments ?? new List<Domain.Entities.Payment>())
            .ToList();
        var returnPayments = returnOrders
            .SelectMany(o => o.Payments ?? new List<Domain.Entities.Payment>())
            .ToList();

        var collectedCash = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)), 2);
        var collectedCard = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)), 2);
        var collectedFawry = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)), 2);
        var collectedOther = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount)), 2);
        var totalSales = Math.Round(
            salesOrders.Sum(o => o.Total)
            - Math.Abs(returnOrders.Sum(o => o.Total)), 2);
        var totalCollected = Math.Round(collectedCash + collectedCard + collectedFawry + collectedOther, 2);
        var deferredAmount = Math.Round(totalSales - totalCollected, 2);

        return new ShiftSummaryFinancials(
            collectedCash,
            collectedCard,
            collectedFawry,
            collectedOther,
            totalSales,
            totalCollected,
            deferredAmount);
    }

    public async Task<ApiResponse<SalesReportDto>> GetSalesReportAsync(DateTime fromDate, DateTime toDate)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

        // Include Completed, PartiallyRefunded, and Refunded orders (exclude Return type)
        var allOrders = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenantId
                     && o.BranchId == branchId
                     && (o.Status == OrderStatus.Completed
                         || o.Status == OrderStatus.PartiallyRefunded
                         || o.Status == OrderStatus.Refunded)
                     && o.CompletedAt >= utcFrom
                     && o.CompletedAt < utcTo)
            .ToListAsync();

        // Separate sales orders from return orders
        var salesOrders = allOrders.Where(o => o.OrderType != OrderType.Return).ToList();
        var returnOrders = allOrders.Where(o => o.OrderType == OrderType.Return).ToList();

        var grossSales = Math.Round(salesOrders.Sum(o => o.Total), 2);
        var totalRefunds = Math.Round(Math.Abs(returnOrders.Sum(o => o.Total)), 2);
        var totalSales = Math.Round(grossSales - totalRefunds, 2);

        var totalCost = Math.Round(salesOrders.SelectMany(o => o.Items)
            .Sum(i => (i.UnitCost ?? 0) * i.Quantity), 2);
        // Subtract COGS for returned items
        var returnedCost = Math.Round(returnOrders.SelectMany(o => o.Items)
            .Sum(i => (i.UnitCost ?? 0) * Math.Abs(i.Quantity)), 2);
        var netCost = Math.Round(totalCost - returnedCost, 2);

        // FIX C-8: Include return orders in daily breakdown so daily sum matches monthly total.
        // Group sales and returns by day, then compute net per day.
        var salesByDay = salesOrders
            .GroupBy(o => o.CompletedAt!.Value.Date)
            .ToDictionary(g => g.Key, g => (Sales: g.Sum(o => o.Total), Orders: g.Count()));
        var returnsByDay = returnOrders
            .GroupBy(o => o.CompletedAt!.Value.Date)
            .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(o => o.Total)));

        var allDays = salesByDay.Keys.Union(returnsByDay.Keys).OrderBy(d => d);
        var dailySales = allDays.Select(day =>
        {
            var daySales = salesByDay.ContainsKey(day) ? salesByDay[day].Sales : 0m;
            var dayReturns = returnsByDay.ContainsKey(day) ? returnsByDay[day] : 0m;
            var dayOrders = salesByDay.ContainsKey(day) ? salesByDay[day].Orders : 0;
            return new DailySalesDto
            {
                Date = day,
                Sales = Math.Round(daySales - dayReturns, 2),
                Orders = dayOrders
            };
        }).ToList();

        var report = new SalesReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalSales = totalSales,
            TotalCost = netCost,
            GrossProfit = Math.Round(totalSales - netCost, 2),
            TotalOrders = salesOrders.Count,
            AverageOrderValue = salesOrders.Count > 0
                ? Math.Round(totalSales / salesOrders.Count, 2)
                : 0m,
            DailySales = dailySales
        };

        return ApiResponse<SalesReportDto>.Ok(report);
    }
}
