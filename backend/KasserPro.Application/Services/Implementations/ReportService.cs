namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using static KasserPro.Application.Common.DateTimeHelper;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ReportService> _logger;
    private readonly record struct ShiftSummaryFinancials(
        decimal CollectedCash,
        decimal CollectedBankAccount,
        decimal CollectedWallet,
        decimal TotalSales,
        decimal TotalCollected,
        decimal DeferredAmount);

    public ReportService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILogger<ReportService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    private static decimal SumAppliedPayments(IEnumerable<Order> orders, PaymentMethod method)
        => Math.Round(orders.Sum(order => GetAppliedPaymentAmount(order, method)), 2);

    private static decimal GetAppliedPaymentAmount(Order order, PaymentMethod method)
    {
        var remaining = Math.Max(0, Math.Round(order.Total, 2));
        var total = 0m;

        foreach (var payment in (order.Payments ?? Enumerable.Empty<Payment>()).OrderBy(p => p.Id))
        {
            if (remaining <= 0)
                break;

            var amount = Math.Max(0, Math.Round(payment.Amount, 2));
            var applied = Math.Min(amount, remaining);
            if (payment.Method == method)
                total += applied;

            remaining -= applied;
        }

        return Math.Round(total, 2);
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
                    .ThenInclude(p => p.Wallet)
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
        // FIX: Include return order payments in breakdown to account for refunded cash
        var returnPayments = returnOrders.SelectMany(o => o.Payments).ToList();
        var refundedCash = Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount));
        var refundedBankAccount = Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.BankAccount).Sum(p => p.Amount));
        var refundedWallet = Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Wallet).Sum(p => p.Amount));

        // FIX H-4: Ensure payment breakdown doesn't go negative (display as 0 minimum)
        // FIX: Use raw amounts for consistency with walletBreakdown
        var totalCash = Math.Round(Math.Max(0, SumAppliedPayments(completedOrders, PaymentMethod.Cash) - refundedCash), 2);
        var totalBankAccount = Math.Round(Math.Max(0, SumAppliedPayments(completedOrders, PaymentMethod.BankAccount) - refundedBankAccount), 2);
        var totalWallet = Math.Round(Math.Max(0,
            completedOrders.SelectMany(o => o.Payments ?? Enumerable.Empty<Payment>())
                .Where(p => p.Method == PaymentMethod.Wallet)
                .Sum(p => p.Amount) - refundedWallet), 2);

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
        var totalCollected = Math.Round(totalCash + totalBankAccount + totalWallet, 2);
        var totalDeferred = Math.Round(actualTotalSales - totalCollected, 2);

        // Wallet breakdown: use already-loaded completedOrders to guarantee
        // exact parity with totalWallet (same data source, same filter).
        var refundedWalletById = returnPayments
            .Where(p => p.Method == PaymentMethod.Wallet && p.WalletId.HasValue)
            .GroupBy(p => p.WalletId!.Value)
            .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(p => p.Amount)));

        var walletBreakdown = completedOrders
            .Where(o => o.Status == OrderStatus.Completed
                     || o.Status == OrderStatus.PartiallyRefunded
                     || o.Status == OrderStatus.Refunded)
            .SelectMany(o => o.Payments ?? Enumerable.Empty<Payment>())
            .Where(p => p.Method == PaymentMethod.Wallet && p.WalletId.HasValue)
            .GroupBy(p => new { p.WalletId, Name = p.Wallet?.Name ?? "غير معروف", Type = p.Wallet?.Type ?? "غير معروف" })
            .Select(g => new WalletPaymentBreakdownDto
            {
                WalletId = g.Key.WalletId!.Value,
                WalletName = g.Key.Name,
                WalletType = g.Key.Type,
                Total = Math.Round(Math.Max(0, g.Sum(p => p.Amount) - (refundedWalletById.GetValueOrDefault(g.Key.WalletId!.Value))), 2),
                TransactionCount = g.Count()
            })
            .ToList();

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
            .ToList();

        _logger.LogDebug($"Top products (after returns): {topProducts.Count}");

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
                TotalBankAccount = financials.CollectedBankAccount,
                TotalWallet = financials.CollectedWallet,
                TotalSales = financials.TotalSales,
                TotalCollected = financials.TotalCollected,
                DeferredAmount = financials.DeferredAmount,
                CollectedCash = financials.CollectedCash,
                CollectedBankAccount = financials.CollectedBankAccount,
                CollectedWallet = financials.CollectedWallet,
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
            TotalBankAccount = totalBankAccount,
            TotalWallet = totalWallet,
            TotalCollected = totalCollected,
            TotalDeferred = totalDeferred,

            // Wallet Breakdown
            WalletBreakdown = walletBreakdown,

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

        var returnPayments = returnOrders
            .SelectMany(o => o.Payments ?? new List<Domain.Entities.Payment>())
            .ToList();

        var collectedCash = Math.Round(
            SumAppliedPayments(salesOrders, PaymentMethod.Cash)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)), 2);
        var collectedBankAccount = Math.Round(
            SumAppliedPayments(salesOrders, PaymentMethod.BankAccount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.BankAccount).Sum(p => p.Amount)), 2);
        var collectedWallet = Math.Round(
            SumAppliedPayments(salesOrders, PaymentMethod.Wallet)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Wallet).Sum(p => p.Amount)), 2);
        var totalSales = Math.Round(
            salesOrders.Sum(o => o.Total)
            - Math.Abs(returnOrders.Sum(o => o.Total)), 2);
        var totalCollected = Math.Round(collectedCash + collectedBankAccount + collectedWallet, 2);
        var deferredAmount = Math.Max(0, Math.Round(totalSales - totalCollected, 2));

        return new ShiftSummaryFinancials(
            collectedCash,
            collectedBankAccount,
            collectedWallet,
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

        // Wallet breakdown (client-side aggregation because SQLite cannot Sum(decimal) in SQL)
        var walletPayments = await _unitOfWork.Payments.Query()
            .AsNoTracking()
            .Include(p => p.Wallet)
            .Where(p => p.Order != null
                     && p.Order.TenantId == tenantId
                     && p.Order.BranchId == branchId
                     && p.Order.CompletedAt >= utcFrom
                     && p.Order.CompletedAt < utcTo
                     && p.WalletId.HasValue
                     && !p.Order.IsDeleted)
            .ToListAsync();

        var walletBreakdown = walletPayments
            .GroupBy(p => new { p.WalletId, p.Wallet!.Name, p.Wallet!.Type })
            .Select(g => new WalletPaymentBreakdownDto
            {
                WalletId = g.Key.WalletId!.Value,
                WalletName = g.Key.Name,
                WalletType = g.Key.Type,
                Total = Math.Round(g.Sum(p => p.Amount), 2),
                TransactionCount = g.Count()
            })
            .ToList();

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
            DailySales = dailySales,
            WalletBreakdown = walletBreakdown
        };

        return ApiResponse<SalesReportDto>.Ok(report);
    }
}
