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
                .AsNoTracking()
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
                .AsNoTracking()
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
            var grossSales = Math.Round(orders.Sum(o => o.Subtotal), 2);
            // Include BOTH item-level and order-level discounts for complete discount reporting
            var totalItemDiscounts = Math.Round(orders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount), 2);
            var totalOrderDiscounts = Math.Round(orders.Sum(o => o.DiscountAmount), 2);
            var totalDiscount = Math.Round(totalItemDiscounts + totalOrderDiscounts, 2);
            var netSales = Math.Round(grossSales - totalDiscount, 2);
            var totalTax = Math.Round(orders.Sum(o => o.TaxAmount), 2);
            var totalRevenue = Math.Round(orders.Sum(o => o.Total), 2);

            var returnGrossSales = Math.Round(Math.Abs(returnOrders.Sum(o => o.Subtotal)), 2);
            var returnItemDiscounts = Math.Round(Math.Abs(returnOrders.SelectMany(o => o.Items).Sum(i => i.DiscountAmount)), 2);
            var returnOrderDiscounts = Math.Round(Math.Abs(returnOrders.Sum(o => o.DiscountAmount)), 2);
            var returnNetSales = Math.Round(Math.Max(0m, returnGrossSales - returnItemDiscounts - returnOrderDiscounts), 2);
            var returnRevenue = Math.Round(Math.Abs(returnOrders.Sum(o => o.Total)), 2);
            var returnTax = Math.Round(Math.Abs(returnOrders.Sum(o => o.TaxAmount)), 2);

            var actualNetSales = Math.Round(netSales - returnNetSales, 2);
            var actualTotalRevenue = Math.Round(totalRevenue - returnRevenue, 2);
            var actualTotalTax = Math.Round(totalTax - returnTax, 2);

            // Calculate COGS (Cost of Goods Sold)
            var totalCost = Math.Round(orders
                .SelectMany(o => o.Items)
                .Sum(i => (i.UnitCost ?? 0) * i.Quantity), 2);

            // FIX: Subtract returned items COGS
            var returnedCost = Math.Round(returnOrders
                .SelectMany(o => o.Items)
                .Sum(i => (i.UnitCost ?? 0) * Math.Abs(i.Quantity)), 2);
            var netCost = Math.Round(totalCost - returnedCost, 2);

            var grossProfit = Math.Round(actualNetSales - netCost, 2);
            var grossProfitMargin = actualNetSales > 0
                ? Math.Round((grossProfit / actualNetSales) * 100, 2)
                : 0m;

            // Get expenses
            var expenses = await _context.Expenses
                .AsNoTracking()
                .Include(e => e.Category)
                .Where(e => e.TenantId == tenantId
                         && e.BranchId == branchId
                         && e.Status == ExpenseStatus.Paid
                         && e.ExpenseDate >= fromDate.Date
                         && e.ExpenseDate < toDate.Date.AddDays(1))
                .ToListAsync();

            var totalExpenses = Math.Round(expenses.Sum(e => e.Amount), 2);

            // Expenses by category
            var expensesByCategory = expenses
                .GroupBy(e => new { e.CategoryId, e.Category.Name })
                .Select(g => new ExpenseCategoryBreakdownDto
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = Math.Round(g.Sum(e => e.Amount), 2),
                    ExpenseCount = g.Count(),
                    Percentage = totalExpenses > 0
                        ? Math.Round((g.Sum(e => e.Amount) / totalExpenses) * 100, 2)
                        : 0
                })
                .OrderByDescending(e => e.TotalAmount)
                .ToList();

            // Calculate net profit
            var netProfit = Math.Round(grossProfit - totalExpenses, 2);
            var netProfitMargin = actualNetSales > 0
                ? Math.Round((netProfit / actualNetSales) * 100, 2)
                : 0m;

            // Wallet breakdown - use already-loaded orders for consistency
            var walletBreakdown = orders
                .SelectMany(o => o.Payments ?? new List<Domain.Entities.Payment>())
                .Where(p => p.Method == PaymentMethod.Wallet && p.WalletId.HasValue)
                .GroupBy(p => new { p.WalletId, Name = p.Wallet?.Name ?? "غير معروف", Type = p.Wallet?.Type ?? "غير معروف" })
                .Select(g => new WalletPaymentBreakdownDto
                {
                    WalletId = g.Key.WalletId!.Value,
                    WalletName = g.Key.Name,
                    WalletType = g.Key.Type,
                    Total = Math.Round(g.Sum(p => p.Amount), 2),
                    TransactionCount = g.Count()
                })
                .ToList();

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
                TotalTax = actualTotalTax,
                TotalRevenue = actualTotalRevenue,

                // COGS
                TotalCost = netCost,
                GrossProfit = grossProfit,
                GrossProfitMargin = grossProfitMargin,

                // Expenses
                TotalExpenses = totalExpenses,
                ExpensesByCategory = expensesByCategory,

                // Net Profit
                NetProfit = netProfit,
                NetProfitMargin = netProfitMargin,

                // Additional Metrics (exclude fully refunded orders from AOV denominator)
                TotalOrders = orders.Count,
                AverageOrderValue = orders.Count(o => o.Status != OrderStatus.Refunded) > 0
                    ? Math.Round(
                        actualTotalRevenue / orders.Count(o => o.Status != OrderStatus.Refunded),
                        2)
                    : 0,
                RefundsAmount = returnRevenue,

                // Wallet Breakdown
                WalletBreakdown = walletBreakdown
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
            var bankAccountExpenses = expenses
                .Where(e => e.PaymentMethod == PaymentMethod.BankAccount)
                .Sum(e => e.Amount);
            var walletExpenses = expenses
                .Where(e => e.PaymentMethod == PaymentMethod.Wallet)
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
                BankAccountExpenses = bankAccountExpenses,
                WalletExpenses = walletExpenses,

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
