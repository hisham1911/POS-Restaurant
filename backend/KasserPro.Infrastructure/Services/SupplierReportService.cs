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

public class SupplierReportService : ISupplierReportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SupplierReportService> _logger;

    public SupplierReportService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<SupplierReportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<SupplierPurchasesReportDto>> GetSupplierPurchasesReportAsync(
        DateTime fromDate, DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var invoices = await _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.Items)
                .Where(pi => pi.TenantId == tenantId
                          && pi.BranchId == branchId
                          && pi.Status != PurchaseInvoiceStatus.Cancelled
                          && pi.InvoiceDate >= utcFrom
                          && pi.InvoiceDate < utcTo)
                .ToListAsync();

            var supplierDetails = invoices
                .GroupBy(pi => new
                {
                    pi.SupplierId,
                    pi.Supplier.Name,
                    pi.Supplier.Phone
                })
                .Select(g =>
                {
                    var totalPurchases = g.Sum(pi => pi.Total);
                    var totalPaid = g.Sum(pi => pi.AmountPaid);
                    var productIds = g.SelectMany(pi => pi.Items).Select(i => i.ProductId).Distinct().Count();

                    return new SupplierPurchaseDetailDto
                    {
                        SupplierId = g.Key.SupplierId,
                        SupplierName = g.Key.Name,
                        Phone = g.Key.Phone,
                        InvoiceCount = g.Count(),
                        TotalPurchases = totalPurchases,
                        TotalPaid = totalPaid,
                        Outstanding = totalPurchases - totalPaid,
                        LastPurchaseDate = g.Max(pi => pi.InvoiceDate),
                        ProductCount = productIds
                    };
                })
                .OrderByDescending(s => s.TotalPurchases)
                .ToList();

            var activeSupplierIds = invoices.Select(pi => pi.SupplierId).Distinct().Count();

            var report = new SupplierPurchasesReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalSuppliers = supplierDetails.Count,
                ActiveSuppliers = activeSupplierIds,
                TotalPurchases = supplierDetails.Sum(s => s.TotalPurchases),
                TotalPaid = supplierDetails.Sum(s => s.TotalPaid),
                TotalOutstanding = supplierDetails.Sum(s => s.Outstanding),
                TotalInvoices = invoices.Count,
                SupplierDetails = supplierDetails
            };

            return ApiResponse<SupplierPurchasesReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supplier purchases report");
            return ApiResponse<SupplierPurchasesReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير مشتريات الموردين");
        }
    }

    public async Task<ApiResponse<SupplierDebtsReportDto>> GetSupplierDebtsReportAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);

            var suppliersWithDebt = await _context.Suppliers
                .Where(s => s.TenantId == tenantId
                          && s.BranchId == branchId
                          && s.IsActive
                          && s.TotalDue > 0)
                .ToListAsync();

            var supplierDebts = new List<SupplierDebtDetailDto>();

            foreach (var supplier in suppliersWithDebt)
            {
                var unpaidInvoices = await _context.PurchaseInvoices
                    .Where(pi => pi.TenantId == tenantId
                              && pi.SupplierId == supplier.Id
                              && pi.Status != PurchaseInvoiceStatus.Cancelled
                              && pi.AmountDue > 0)
                    .OrderBy(pi => pi.InvoiceDate)
                    .ToListAsync();

                var lastPayment = await _context.PurchaseInvoices
                    .Include(pi => pi.Payments)
                    .Where(pi => pi.TenantId == tenantId && pi.SupplierId == supplier.Id)
                    .SelectMany(pi => pi.Payments)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                var oldestUnpaid = unpaidInvoices.FirstOrDefault();
                var daysSinceOldest = oldestUnpaid != null
                    ? (int)(DateTime.UtcNow - oldestUnpaid.InvoiceDate).TotalDays
                    : 0;

                supplierDebts.Add(new SupplierDebtDetailDto
                {
                    SupplierId = supplier.Id,
                    SupplierName = supplier.Name,
                    Phone = supplier.Phone,
                    TotalDue = supplier.TotalDue,
                    UnpaidInvoicesCount = unpaidInvoices.Count,
                    OldestUnpaidInvoiceDate = oldestUnpaid?.InvoiceDate,
                    DaysSinceOldestInvoice = daysSinceOldest,
                    LastPaymentDate = lastPayment?.CreatedAt
                });
            }

            var totalOutstanding = supplierDebts.Sum(s => s.TotalDue);
            var overdueInvoices = supplierDebts.Where(s => s.DaysSinceOldestInvoice > 30).ToList();

            var report = new SupplierDebtsReportDto
            {
                ReportDate = DateTime.UtcNow,
                BranchId = branchId,
                BranchName = branch?.Name,
                TotalSuppliersWithDebt = supplierDebts.Count,
                TotalOutstandingAmount = totalOutstanding,
                TotalOverdueAmount = overdueInvoices.Sum(s => s.TotalDue),
                OverdueInvoicesCount = overdueInvoices.Sum(s => s.UnpaidInvoicesCount),
                SupplierDebts = supplierDebts.OrderByDescending(s => s.TotalDue).ToList()
            };

            return ApiResponse<SupplierDebtsReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supplier debts report");
            return ApiResponse<SupplierDebtsReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير ديون الموردين");
        }
    }

    public async Task<ApiResponse<SupplierPerformanceReportDto>> GetSupplierPerformanceReportAsync(
        DateTime fromDate, DateTime toDate)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var branchId = _currentUserService.BranchId;
            var branch = await _context.Branches.FindAsync(branchId);
            var (utcFrom, utcTo) = ToUtcRange(fromDate, toDate);

            var invoices = await _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.Items)
                .Include(pi => pi.Payments)
                .Where(pi => pi.TenantId == tenantId
                          && pi.BranchId == branchId
                          && pi.Status != PurchaseInvoiceStatus.Cancelled
                          && pi.InvoiceDate >= utcFrom
                          && pi.InvoiceDate < utcTo)
                .ToListAsync();

            var supplierPerformance = invoices
                .GroupBy(pi => new { pi.SupplierId, pi.Supplier.Name })
                .Select(g =>
                {
                    var totalInvoices = g.Count();
                    var totalValue = g.Sum(pi => pi.Total);
                    var avgInvoiceValue = totalInvoices > 0 ? Math.Round(totalValue / totalInvoices, 2) : 0;
                    var uniqueProducts = g.SelectMany(pi => pi.Items).Select(i => i.ProductId).Distinct().Count();

                    // Calculate payment timeliness
                    var paidInvoices = g.Where(pi => pi.AmountDue <= 0).Count();
                    var onTimeRate = totalInvoices > 0 ? Math.Round((decimal)paidInvoices / totalInvoices * 100, 2) : 0;

                    // Average payment delay
                    var avgPaymentDelay = 0;
                    var paidInvoicesList = g.Where(pi => pi.Payments.Any()).ToList();
                    if (paidInvoicesList.Any())
                    {
                        avgPaymentDelay = (int)paidInvoicesList.Average(pi =>
                        {
                            var lastPay = pi.Payments.Max(p => p.CreatedAt);
                            return (lastPay - pi.InvoiceDate).TotalDays;
                        });
                    }

                    var score = onTimeRate >= 90 ? "Excellent" :
                                onTimeRate >= 70 ? "Good" :
                                onTimeRate >= 50 ? "Fair" : "Poor";

                    return new SupplierPerformanceDetailDto
                    {
                        SupplierId = g.Key.SupplierId,
                        SupplierName = g.Key.Name,
                        TotalInvoices = totalInvoices,
                        TotalPurchaseValue = totalValue,
                        AverageInvoiceValue = avgInvoiceValue,
                        UniqueProductsSupplied = uniqueProducts,
                        OnTimePaymentRate = onTimeRate,
                        DaysAveragePaymentDelay = avgPaymentDelay,
                        ReliabilityScore = score
                    };
                })
                .OrderByDescending(s => s.TotalPurchaseValue)
                .ToList();

            var report = new SupplierPerformanceReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                BranchName = branch?.Name,
                SupplierPerformance = supplierPerformance
            };

            return ApiResponse<SupplierPerformanceReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supplier performance report");
            return ApiResponse<SupplierPerformanceReportDto>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ في إنشاء تقرير أداء الموردين");
        }
    }
}
