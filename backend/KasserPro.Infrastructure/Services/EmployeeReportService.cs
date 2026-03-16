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

public class EmployeeReportService : IEmployeeReportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EmployeeReportService> _logger;

    public EmployeeReportService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<EmployeeReportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<CashierPerformanceReportDto>> GetCashierPerformanceReportAsync(
        DateTime fromDate, DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var shifts = await _context.Shifts
                .Include(s => s.User)
                .Include(s => s.Orders)
                    .ThenInclude(o => o.Payments)
                .Where(s => s.TenantId == tenantId
                         && s.BranchId == branchId
                         && s.OpenedAt >= utcFrom
                         && s.OpenedAt < utcTo)
                .ToListAsync();

            var userIds = shifts.Select(s => s.UserId).Distinct().ToList();

            var cashierPerformance = new List<CashierPerformanceDetailDto>();

            foreach (var userId in userIds)
            {
                var userShifts = shifts.Where(s => s.UserId == userId).ToList();
                var user = userShifts.First().User;
                var userOrders = userShifts.SelectMany(s => s.Orders)
                    .Where(o => o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded
                             || o.Status == OrderStatus.Cancelled)
                    .ToList();

                // Sales orders (all completed states, excluding returns)
                var salesOrders = userOrders
                    .Where(o => (o.Status == OrderStatus.Completed
                              || o.Status == OrderStatus.PartiallyRefunded
                              || o.Status == OrderStatus.Refunded)
                             && o.OrderType != OrderType.Return)
                    .ToList();
                var returnOrdersCashier = userOrders
                    .Where(o => (o.Status == OrderStatus.Completed
                              || o.Status == OrderStatus.PartiallyRefunded
                              || o.Status == OrderStatus.Refunded)
                             && o.OrderType == OrderType.Return)
                    .ToList();

                var cancelledOrders = userOrders.Count(o => o.Status == OrderStatus.Cancelled);
                var refundedOrders = userOrders.Count(o => o.Status == OrderStatus.Refunded || o.Status == OrderStatus.PartiallyRefunded);
                var totalRevenue = salesOrders.Sum(o => o.Total) - Math.Abs(returnOrdersCashier.Sum(o => o.Total));
                var totalOrders = salesOrders.Count;
                var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

                var totalShiftHours = userShifts
                    .Where(s => s.ClosedAt.HasValue)
                    .Sum(s => (s.ClosedAt!.Value - s.OpenedAt).TotalHours);
                var avgShiftDuration = userShifts.Count > 0 ? totalShiftHours / userShifts.Count : 0;
                var ordersPerHour = totalShiftHours > 0 ? totalOrders / (decimal)totalShiftHours : 0;

                // Calculate payment method breakdown from actual Payment records
                var salesPayments = salesOrders.SelectMany(o => o.Payments ?? Enumerable.Empty<Domain.Entities.Payment>()).ToList();
                var returnPayments = returnOrdersCashier.SelectMany(o => o.Payments ?? Enumerable.Empty<Domain.Entities.Payment>()).ToList();
                var cashSales = Math.Max(0,
                    salesPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
                    - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)));
                var cardSales = Math.Max(0,
                    salesPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)
                    - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)));
                var fawrySales = Math.Max(0,
                    salesPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)
                    - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)));

                var completedShifts = userShifts.Count(s => s.IsClosed && !s.IsForceClosed);
                var forceClosedShifts = userShifts.Count(s => s.IsForceClosed);
                var cancellationRate = userOrders.Count > 0 ? (decimal)cancelledOrders / userOrders.Count * 100 : 0;

                // Performance score calculation
                var performanceScore = Math.Min(100, Math.Max(0,
                    (ordersPerHour > 0 ? 30 : 0) +
                    (avgOrderValue > 0 ? 25 : 0) +
                    (cancellationRate < 5 ? 25 : cancellationRate < 10 ? 15 : 5) +
                    (completedShifts > 0 ? 20 : 0)));

                var rating = performanceScore >= 80 ? "Excellent" :
                             performanceScore >= 60 ? "Good" :
                             performanceScore >= 40 ? "Average" : "Poor";

                cashierPerformance.Add(new CashierPerformanceDetailDto
                {
                    UserId = userId,
                    UserName = user.Name,
                    Email = user.Email,
                    TotalShifts = userShifts.Count,
                    CompletedShifts = completedShifts,
                    ForceClosedShifts = forceClosedShifts,
                    AverageShiftDuration = Math.Round((decimal)avgShiftDuration, 2),
                    TotalOrders = totalOrders,
                    TotalRevenue = totalRevenue,
                    AverageOrderValue = Math.Round(avgOrderValue, 2),
                    OrdersPerHour = Math.Round(ordersPerHour, 2),
                    CompletedOrders = salesOrders.Count,
                    CancelledOrders = cancelledOrders,
                    RefundedOrders = refundedOrders,
                    CancellationRate = Math.Round(cancellationRate, 2),
                    CashSales = cashSales,
                    CardSales = cardSales,
                    FawrySales = fawrySales,
                    PerformanceScore = Math.Round((decimal)performanceScore, 2),
                    PerformanceRating = rating
                });
            }

            var report = new CashierPerformanceReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalCashiers = userIds.Count,
                TotalShifts = shifts.Count,
                TotalRevenue = cashierPerformance.Sum(c => c.TotalRevenue),
                TotalOrders = cashierPerformance.Sum(c => c.TotalOrders),
                CashierPerformance = cashierPerformance.OrderByDescending(c => c.TotalRevenue).ToList()
            };

            return ApiResponse<CashierPerformanceReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating cashier performance report");
            return ApiResponse<CashierPerformanceReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير أداء الكاشير");
        }
    }

    public async Task<ApiResponse<DetailedShiftsReportDto>> GetDetailedShiftsReportAsync(
        DateTime fromDate, DateTime toDate, int? userId = null)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var query = _context.Shifts
                .Include(s => s.User)
                .Include(s => s.Orders)
                    .ThenInclude(o => o.Payments)
                .Where(s => s.TenantId == tenantId
                         && s.BranchId == branchId
                         && s.OpenedAt >= utcFrom
                         && s.OpenedAt < utcTo);

            if (userId.HasValue)
                query = query.Where(s => s.UserId == userId.Value);

            var shifts = await query.OrderByDescending(s => s.OpenedAt).ToListAsync();

            var detailedShifts = shifts.Select(s =>
            {
                // Compute payment breakdown from actual Payment records
                var shiftPayments = (s.Orders ?? new List<Domain.Entities.Order>())
                    .Where(o => o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                    .SelectMany(o => o.Payments ?? new List<Domain.Entities.Payment>())
                    .ToList();
                var shiftCash = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount), 2);
                var shiftCard = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount), 2);
                var shiftFawry = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount), 2);
                var shiftBankTransfer = Math.Round(shiftPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount), 2);

                return new DetailedShiftDto
                {
                    ShiftId = s.Id,
                    UserName = s.User.Name,
                    OpenedAt = s.OpenedAt,
                    ClosedAt = s.ClosedAt,
                    Duration = s.ClosedAt.HasValue
                        ? Math.Round((decimal)(s.ClosedAt.Value - s.OpenedAt).TotalHours, 2)
                        : 0,
                    OpeningBalance = s.OpeningBalance,
                    ClosingBalance = s.ClosingBalance,
                    ExpectedBalance = s.ExpectedBalance,
                    Variance = s.Difference,
                    TotalOrders = s.TotalOrders,
                    TotalCash = shiftCash,
                    TotalCard = shiftCard,
                    TotalFawry = shiftFawry,
                    TotalBankTransfer = shiftBankTransfer,
                    TotalSales = shiftCash + shiftCard + shiftFawry + shiftBankTransfer,
                    IsForceClosed = s.IsForceClosed,
                    ForceCloseReason = s.ForceCloseReason,
                    ClosedByUserName = s.IsForceClosed ? s.ForceClosedByUserName : s.User.Name
                };
            }).ToList();

            var completedShifts = shifts.Count(s => s.IsClosed && !s.IsForceClosed);
            var forceClosedShifts = shifts.Count(s => s.IsForceClosed);
            var totalRevenue = detailedShifts.Sum(s => s.TotalSales);

            var report = new DetailedShiftsReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalShifts = shifts.Count,
                CompletedShifts = completedShifts,
                ForceClosedShifts = forceClosedShifts,
                TotalRevenue = totalRevenue,
                AverageShiftRevenue = shifts.Count > 0 ? Math.Round(totalRevenue / shifts.Count, 2) : 0,
                Shifts = detailedShifts
            };

            return ApiResponse<DetailedShiftsReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating detailed shifts report");
            return ApiResponse<DetailedShiftsReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير الورديات");
        }
    }

    public async Task<ApiResponse<SalesByEmployeeReportDto>> GetSalesByEmployeeReportAsync(
        DateTime fromDate, DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            // Include all completed states for accurate revenue reporting
            var allOrders = await _context.Orders
                .Include(o => o.User)
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .ToListAsync();

            // Separate sales from returns
            var orders = allOrders.Where(o => o.OrderType != OrderType.Return).ToList();
            var returnOrdersEmp = allOrders.Where(o => o.OrderType == OrderType.Return).ToList();
            var returnsByUser = returnOrdersEmp
                .GroupBy(o => o.UserId)
                .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(o => o.Total)));

            var totalRefunds = Math.Abs(returnOrdersEmp.Sum(o => o.Total));
            var totalRevenue = orders.Sum(o => o.Total) - totalRefunds;
            var totalOrders = orders.Count;

            var employeeSales = orders
                .GroupBy(o => new { o.UserId, o.User.Name, o.User.Role })
                .Select(g =>
                {
                    var empRevenue = g.Sum(o => o.Total);
                    // Net out returns attributed to this employee
                    if (returnsByUser.TryGetValue(g.Key.UserId, out var empRefunds))
                        empRevenue -= empRefunds;
                    var empOrders = g.Count();
                    // Get returns for this employee to net in daily breakdown
                    var empReturnOrders = returnOrdersEmp.Where(o => o.UserId == g.Key.UserId).ToList();
                    var dailySales = g
                        .GroupBy(o => o.CompletedAt!.Value.Date)
                        .Select(d =>
                        {
                            var dayRevenue = d.Sum(o => o.Total);
                            // Net returns for this employee on this day
                            var dayReturns = empReturnOrders
                                .Where(r => r.CompletedAt!.Value.Date == d.Key)
                                .Sum(r => Math.Abs(r.Total));
                            return new DailyEmployeeSalesDto
                            {
                                Date = d.Key,
                                Orders = d.Count(),
                                Revenue = dayRevenue - dayReturns
                            };
                        })
                        .OrderBy(d => d.Date)
                        .ToList();

                    return new EmployeeSalesDetailDto
                    {
                        UserId = g.Key.UserId,
                        UserName = g.Key.Name,
                        Role = g.Key.Role.ToString(),
                        TotalOrders = empOrders,
                        TotalRevenue = empRevenue,
                        AverageOrderValue = empOrders > 0 ? Math.Round(empRevenue / empOrders, 2) : 0,
                        RevenuePercentage = totalRevenue > 0 ? Math.Round(empRevenue / totalRevenue * 100, 2) : 0,
                        DailySales = dailySales
                    };
                })
                .OrderByDescending(e => e.TotalRevenue)
                .ToList();

            var report = new SalesByEmployeeReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                TotalEmployees = employeeSales.Count,
                EmployeeSales = employeeSales
            };

            return ApiResponse<SalesByEmployeeReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sales by employee report");
            return ApiResponse<SalesByEmployeeReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير المبيعات حسب الموظف");
        }
    }
}
