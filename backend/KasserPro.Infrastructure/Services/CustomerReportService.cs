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

public class CustomerReportService : ICustomerReportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CustomerReportService> _logger;

    public CustomerReportService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<CustomerReportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<TopCustomersReportDto>> GetTopCustomersReportAsync(
        DateTime fromDate,
        DateTime toDate,
        int topCount = 20)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var branch = await _context.Branches.FindAsync(branchId);

            // Get orders with customers in date range (all completed states, exclude returns)
            var ordersWithCustomers = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && o.CustomerId != null
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .ToListAsync();

            // Get return orders for refund netting
            var returnOrdersCustomer = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && o.CustomerId != null
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType == OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .ToListAsync();

            // Group returns by customer for netting
            var returnsByCustomer = returnOrdersCustomer
                .Where(o => o.CustomerId.HasValue)
                .GroupBy(o => o.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(o => o.Total)));

            // Get all customers who ordered
            var customerIds = ordersWithCustomers
                .Select(o => o.CustomerId!.Value)
                .Distinct()
                .ToList();

            var totalCustomers = customerIds.Count;
            var totalRefundsForCustomers = returnsByCustomer.Values.Sum();
            var totalRevenue = ordersWithCustomers.Sum(o => o.Total) - totalRefundsForCustomers;
            var averageCustomerValue = totalCustomers > 0 ? totalRevenue / totalCustomers : 0;

            // Count new customers (first order in this period) — single batch query
            var firstOrderDates = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.CustomerId != null
                         && customerIds.Contains(o.CustomerId!.Value)
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return)
                .GroupBy(o => o.CustomerId!.Value)
                .Select(g => new { CustomerId = g.Key, FirstDate = g.Min(o => o.CompletedAt) })
                .ToListAsync();

            var newCustomers = firstOrderDates
                .Count(f => f.FirstDate >= utcFrom && f.FirstDate < utcTo);

            // Group by customer and calculate metrics
            var topCustomers = ordersWithCustomers
                .GroupBy(o => new
                {
                    CustomerId = o.CustomerId!.Value,
                    o.Customer!.Name,
                    o.Customer.Phone
                })
                .Select(g =>
                {
                    var spent = g.Sum(o => o.Total);
                    // Net out returns for this customer
                    if (returnsByCustomer.TryGetValue(g.Key.CustomerId, out var custRefunds))
                        spent -= custRefunds;
                    return new TopCustomerDto
                    {
                        CustomerId = g.Key.CustomerId,
                        CustomerName = g.Key.Name ?? "عميل",
                        Phone = g.Key.Phone,
                        TotalOrders = g.Count(),
                        TotalSpent = spent,
                        AverageOrderValue = g.Count() > 0 ? spent / g.Count() : 0,
                        LastOrderDate = g.Max(o => o.CompletedAt),
                        OutstandingBalance = g.First().Customer!.TotalDue
                    };
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(topCount)
                .ToList();

            var report = new TopCustomersReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,

                TotalCustomers = totalCustomers,
                ActiveCustomers = totalCustomers,
                NewCustomers = newCustomers,
                TotalRevenue = totalRevenue,
                AverageCustomerValue = Math.Round(averageCustomerValue, 2),

                TopCustomers = topCustomers
            };

            return ApiResponse<TopCustomersReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating top customers report");
            return ApiResponse<TopCustomersReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير أفضل العملاء");
        }
    }

    public async Task<ApiResponse<CustomerDebtsReportDto>> GetCustomerDebtsReportAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;

            var branch = await _context.Branches.FindAsync(branchId);

            // Get customers with outstanding balance
            var customersWithDebt = await _context.Customers
                .Where(c => c.TenantId == tenantId
                         && c.IsActive
                         && c.TotalDue > 0)
                .ToListAsync();

            var totalOutstandingAmount = customersWithDebt.Sum(c => c.TotalDue);

            // Batch-fetch order data for all customers with debt (eliminates N+1)
            var debtCustomerIds = customersWithDebt.Select(c => c.Id).ToList();

            var lastOrderByCustomer = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.CustomerId != null
                         && debtCustomerIds.Contains(o.CustomerId!.Value)
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return)
                .GroupBy(o => o.CustomerId!.Value)
                .Select(g => new { CustomerId = g.Key, LastDate = g.Max(o => o.CompletedAt) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.LastDate);

            var oldestUnpaidByCustomer = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.CustomerId != null
                         && debtCustomerIds.Contains(o.CustomerId!.Value)
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.AmountDue > 0)
                .GroupBy(o => o.CustomerId!.Value)
                .Select(g => new { CustomerId = g.Key, OldestDate = g.Min(o => o.CompletedAt) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.OldestDate);

            var unpaidCountByCustomer = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.CustomerId != null
                         && debtCustomerIds.Contains(o.CustomerId!.Value)
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.AmountDue > 0)
                .GroupBy(o => o.CustomerId!.Value)
                .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Count);

            var customerDebts = customersWithDebt.Select(customer =>
            {
                lastOrderByCustomer.TryGetValue(customer.Id, out var lastDate);
                oldestUnpaidByCustomer.TryGetValue(customer.Id, out var oldestDate);
                unpaidCountByCustomer.TryGetValue(customer.Id, out var unpaidCount);

                var daysSinceLastOrder = lastDate != null
                    ? (DateTime.UtcNow - lastDate.Value).Days
                    : 0;

                return new CustomerDebtDetailDto
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name ?? "عميل",
                    Phone = customer.Phone,
                    TotalDue = customer.TotalDue,
                    CreditLimit = customer.CreditLimit,
                    DaysSinceLastOrder = daysSinceLastOrder,
                    LastOrderDate = lastDate,
                    OldestUnpaidOrderDate = oldestDate,
                    UnpaidOrdersCount = unpaidCount,
                    IsOverLimit = customer.CreditLimit > 0 && customer.TotalDue > customer.CreditLimit
                };
            }).ToList();

            // Aging analysis
            var agingAnalysis = new List<AgingBracketDto>
            {
                new AgingBracketDto { Bracket = "0-30 يوم" },
                new AgingBracketDto { Bracket = "31-60 يوم" },
                new AgingBracketDto { Bracket = "61-90 يوم" },
                new AgingBracketDto { Bracket = "أكثر من 90 يوم" }
            };

            foreach (var debt in customerDebts)
            {
                var daysSinceOldest = debt.OldestUnpaidOrderDate.HasValue
                    ? (DateTime.UtcNow - debt.OldestUnpaidOrderDate.Value).Days
                    : 0;

                if (daysSinceOldest <= 30)
                {
                    agingAnalysis[0].CustomerCount++;
                    agingAnalysis[0].TotalAmount += debt.TotalDue;
                }
                else if (daysSinceOldest <= 60)
                {
                    agingAnalysis[1].CustomerCount++;
                    agingAnalysis[1].TotalAmount += debt.TotalDue;
                }
                else if (daysSinceOldest <= 90)
                {
                    agingAnalysis[2].CustomerCount++;
                    agingAnalysis[2].TotalAmount += debt.TotalDue;
                }
                else
                {
                    agingAnalysis[3].CustomerCount++;
                    agingAnalysis[3].TotalAmount += debt.TotalDue;
                }
            }

            // Calculate percentages
            foreach (var bracket in agingAnalysis)
            {
                bracket.Percentage = totalOutstandingAmount > 0
                    ? Math.Round((bracket.TotalAmount / totalOutstandingAmount) * 100, 2)
                    : 0;
            }

            var overdueCustomers = customerDebts
                .Where(c => c.OldestUnpaidOrderDate.HasValue
                         && (DateTime.UtcNow - c.OldestUnpaidOrderDate.Value).Days > 30)
                .ToList();

            var report = new CustomerDebtsReportDto
            {
                ReportDate = DateTime.UtcNow,
                BranchId = branchId,
                BranchName = branch?.Name,

                TotalCustomersWithDebt = customersWithDebt.Count,
                TotalOutstandingAmount = totalOutstandingAmount,
                TotalOverdueAmount = overdueCustomers.Sum(c => c.TotalDue),
                OverdueCustomersCount = overdueCustomers.Count,

                CustomerDebts = customerDebts.OrderByDescending(c => c.TotalDue).ToList(),
                AgingAnalysis = agingAnalysis
            };

            return ApiResponse<CustomerDebtsReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating customer debts report");
            return ApiResponse<CustomerDebtsReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير ديون العملاء");
        }
    }

    public async Task<ApiResponse<CustomerActivityReportDto>> GetCustomerActivityReportAsync(
        DateTime fromDate,
        DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var branch = await _context.Branches.FindAsync(branchId);

            // Get all orders with customers in period (all completed states, exclude returns)
            var ordersInPeriod = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && o.CustomerId != null
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return
                         && o.CompletedAt >= utcFrom
                         && o.CompletedAt < utcTo)
                .ToListAsync();

            var customerIds = ordersInPeriod.Select(o => o.CustomerId!.Value).Distinct().ToList();

            // Batch-fetch first order dates for all customers (eliminates N+1)
            var firstOrderDates = await _context.Orders
                .Where(o => o.TenantId == tenantId
                         && o.CustomerId != null
                         && customerIds.Contains(o.CustomerId!.Value)
                         && (o.Status == OrderStatus.Completed
                             || o.Status == OrderStatus.PartiallyRefunded
                             || o.Status == OrderStatus.Refunded)
                         && o.OrderType != OrderType.Return)
                .GroupBy(o => o.CustomerId!.Value)
                .Select(g => new { CustomerId = g.Key, FirstDate = g.Min(o => o.CompletedAt) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.FirstDate);

            // Categorize customers
            var newCustomers = 0;
            var returningCustomers = 0;
            decimal newCustomerRevenue = 0;
            decimal returningCustomerRevenue = 0;
            var newCustomerIds = new HashSet<int>();
            var returningCustomerIds = new HashSet<int>();

            foreach (var customerId in customerIds)
            {
                var customerOrders = ordersInPeriod.Where(o => o.CustomerId == customerId).ToList();
                var customerRevenue = customerOrders.Sum(o => o.Total);

                firstOrderDates.TryGetValue(customerId, out var firstDate);
                if (firstDate != null && firstDate >= utcFrom && firstDate < utcTo)
                {
                    newCustomers++;
                    newCustomerRevenue += customerRevenue;
                    newCustomerIds.Add(customerId);
                }
                else
                {
                    returningCustomers++;
                    returningCustomerRevenue += customerRevenue;
                    returningCustomerIds.Add(customerId);
                }
            }

            var averageNewCustomerValue = newCustomers > 0 ? newCustomerRevenue / newCustomers : 0;
            var averageReturningCustomerValue = returningCustomers > 0 ? returningCustomerRevenue / returningCustomers : 0;

            // Simple retention calculation
            var totalCustomers = newCustomers + returningCustomers;
            var retentionRate = totalCustomers > 0 ? (decimal)returningCustomers / totalCustomers * 100 : 0;
            var churnRate = 100 - retentionRate;

            var report = new CustomerActivityReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,

                NewCustomers = newCustomers,
                ReturningCustomers = returningCustomers,
                InactiveCustomers = 0, // Would need more complex logic

                NewCustomerRevenue = newCustomerRevenue,
                ReturningCustomerRevenue = returningCustomerRevenue,
                AverageNewCustomerValue = Math.Round(averageNewCustomerValue, 2),
                AverageReturningCustomerValue = Math.Round(averageReturningCustomerValue, 2),

                RetentionRate = Math.Round(retentionRate, 2),
                ChurnRate = Math.Round(churnRate, 2),

                CustomerSegments = new List<CustomerSegmentDto>
                {
                    new CustomerSegmentDto
                    {
                        SegmentName = "عملاء جدد",
                        CustomerCount = newCustomers,
                        TotalRevenue = newCustomerRevenue,
                        AverageOrderValue = averageNewCustomerValue,
                        TotalOrders = ordersInPeriod.Count(o =>
                            newCustomerIds.Contains(o.CustomerId!.Value))
                    },
                    new CustomerSegmentDto
                    {
                        SegmentName = "عملاء عائدون",
                        CustomerCount = returningCustomers,
                        TotalRevenue = returningCustomerRevenue,
                        AverageOrderValue = averageReturningCustomerValue,
                        TotalOrders = ordersInPeriod.Count(o =>
                            returningCustomerIds.Contains(o.CustomerId!.Value))
                    }
                }
            };

            return ApiResponse<CustomerActivityReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating customer activity report");
            return ApiResponse<CustomerActivityReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير نشاط العملاء");
        }
    }
}
