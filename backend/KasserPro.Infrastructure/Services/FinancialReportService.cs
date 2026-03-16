namespace KasserPro.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using static KasserPro.Application.Common.DateTimeHelper;

public class FinancialReportService : IFinancialReportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FinancialReportService> _logger;

    public FinancialReportService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<FinancialReportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<ProfitLossReportDto>> GetProfitLossReportAsync(
        DateTime fromDate,
        DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var branch = await _context.Branches.FindAsync(branchId);

            // Get completed orders (excluding returns)
            var orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .ToListAsync();

            // Get return orders for refunds calculation
            var returnOrders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType == OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .ToListAsync();

            // Calculate revenue
            var grossSales = orders.Sum(o => o.Subtotal);
            // Include BOTH item-level and order-level discounts for complete discount reporting
            var totalItemDiscounts = orders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount);
            var totalOrderDiscounts = orders.Sum(o => o.DiscountAmount);
            var totalDiscount = totalItemDiscounts + totalOrderDiscounts;
            var netSales = grossSales - totalDiscount;
            var totalTax = orders.Sum(o => o.TaxAmount);
            var totalRevenue = orders.Sum(o => o.Total);
            var refundsAmount = Math.Abs(returnOrders.Sum(o => o.Total));

            // FIX: Subtract refunds from revenue to get ACTUAL net figures
            var actualNetSales = netSales - refundsAmount;
            var actualTotalRevenue = totalRevenue - refundsAmount;

            // Calculate COGS (Cost of Goods Sold)
            var totalCost = orders
                .SelectMany(o => o.Items)
                .Sum(i => (i.UnitCost ?? 0) * i.Quantity);

            // FIX: Subtract returned items COGS
            var returnedCost = returnOrders
                .SelectMany(o => o.Items)
                .Sum(i => (i.UnitCost ?? 0) * Math.Abs(i.Quantity));
            var netCost = totalCost - returnedCost;

            var grossProfit = actualNetSales - netCost;
            var grossProfitMargin = actualNetSales > 0 ? (grossProfit / actualNetSales) * 100 : 0;

            // Get expenses
            var expenses = await _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.TenantId == tenantId
                         && e.BranchId == branchId
                         && e.Status == ExpenseStatus.Paid
                         && e.ExpenseDate >= fromDate.Date
                         && e.ExpenseDate < toDate.Date.AddDays(1))
                .ToListAsync();

            var totalExpenses = expenses.Sum(e => e.Amount);

            // Expenses by category
            var expensesByCategory = expenses
                .GroupBy(e => new { e.CategoryId, e.Category.Name })
                .Select(g => new ExpenseCategoryBreakdownDto
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = g.Sum(e => e.Amount),
                    ExpenseCount = g.Count(),
                    Percentage = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
                })
                .OrderByDescending(e => e.TotalAmount)
                .ToList();

            // Calculate net profit
            var netProfit = grossProfit - totalExpenses;
            var netProfitMargin = actualTotalRevenue > 0 ? (netProfit / actualTotalRevenue) * 100 : 0;

            var report = new ProfitLossReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,

                // Revenue
                GrossSales = grossSales,
                TotalDiscount = totalDiscount,
                NetSales = actualNetSales,
                TotalTax = totalTax,
                TotalRevenue = actualTotalRevenue,

                // COGS
                TotalCost = netCost,
                GrossProfit = grossProfit,
                GrossProfitMargin = Math.Round(grossProfitMargin, 2),

                // Expenses
                TotalExpenses = totalExpenses,
                ExpensesByCategory = expensesByCategory,

                // Net Profit
                NetProfit = netProfit,
                NetProfitMargin = Math.Round(netProfitMargin, 2),

                // Additional Metrics (exclude fully refunded orders from AOV denominator)
                TotalOrders = orders.Count,
                AverageOrderValue = orders.Count(o => o.Status != OrderStatus.Refunded) > 0
                    ? actualTotalRevenue / orders.Count(o => o.Status != OrderStatus.Refunded)
                    : 0,
                RefundsAmount = refundsAmount
            };

            return ApiResponse<ProfitLossReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating profit & loss report");
            return ApiResponse<ProfitLossReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير الأرباح والخسائر");
        }
    }

    public async Task<ApiResponse<ExpensesReportDto>> GetExpensesReportAsync(
        DateTime fromDate,
        DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var branch = await _context.Branches.FindAsync(branchId);

            // Get all expenses in date range
            var expenses = await _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.TenantId == tenantId
                         && e.BranchId == branchId
                         && e.Status == ExpenseStatus.Paid
                         && e.ExpenseDate >= utcFrom
                         && e.ExpenseDate < utcTo)
                .ToListAsync();

            var totalExpenses = expenses.Sum(e => e.Amount);
            var totalExpenseCount = expenses.Count;
            var averageExpenseAmount = totalExpenseCount > 0 ? totalExpenses / totalExpenseCount : 0;

            // Breakdown by Category
            var expensesByCategory = expenses
                .GroupBy(e => new { e.CategoryId, e.Category.Name })
                .Select(g => new ExpenseCategoryBreakdownDto
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = g.Sum(e => e.Amount),
                    ExpenseCount = g.Count(),
                    Percentage = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
                })
                .OrderByDescending(e => e.TotalAmount)
                .ToList();

            // Breakdown by Payment Method
            var cashExpenses = expenses
                .Where(e => e.PaymentMethod == PaymentMethod.Cash)
                .Sum(e => e.Amount);
            var cardExpenses = expenses
                .Where(e => e.PaymentMethod == PaymentMethod.Card)
                .Sum(e => e.Amount);
            var otherExpenses = expenses
                .Where(e => e.PaymentMethod != PaymentMethod.Cash
                         && e.PaymentMethod != PaymentMethod.Card)
                .Sum(e => e.Amount);

            // Daily Breakdown
            var dailyExpenses = expenses
                .GroupBy(e => e.ExpenseDate.Date)
                .Select(g => new DailyExpenseDto
                {
                    Date = g.Key,
                    Amount = g.Sum(e => e.Amount),
                    Count = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            // Top Expenses
            var topExpenses = expenses
                .OrderByDescending(e => e.Amount)
                .Take(10)
                .Select(e => new ExpenseDetailDto
                {
                    Id = e.Id,
                    Date = e.ExpenseDate,
                    CategoryName = e.Category.Name,
                    Description = e.Description,
                    Amount = e.Amount,
                    PaymentMethod = e.PaymentMethod?.ToString() ?? "غير محدد",
                    RecipientName = e.Beneficiary
                })
                .ToList();

            var report = new ExpensesReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,

                TotalExpenses = totalExpenses,
                TotalExpenseCount = totalExpenseCount,
                AverageExpenseAmount = Math.Round(averageExpenseAmount, 2),

                ExpensesByCategory = expensesByCategory,

                CashExpenses = cashExpenses,
                CardExpenses = cardExpenses,
                OtherExpenses = otherExpenses,

                DailyExpenses = dailyExpenses,
                TopExpenses = topExpenses
            };

            return ApiResponse<ExpensesReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating expenses report");
            return ApiResponse<ExpensesReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير المصروفات");
        }
    }
}
