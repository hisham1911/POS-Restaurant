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
                    var totalPurchases = Math.Round(g.Sum(pi => pi.Total), 2);
                    var totalPaid = Math.Round(g.Sum(pi => pi.AmountPaid), 2);
                    var productIds = g.SelectMany(pi => pi.Items).Select(i => i.ProductId).Distinct().Count();

                    return new SupplierPurchaseDetailDto
                    {
                        SupplierId = g.Key.SupplierId,
                        SupplierName = g.Key.Name,
                        Phone = g.Key.Phone,
                        InvoiceCount = g.Count(),
                        TotalPurchases = totalPurchases,
                        TotalPaid = totalPaid,
                        Outstanding = Math.Round(Math.Max(0m, totalPurchases - totalPaid), 2),
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
                TotalPurchases = Math.Round(supplierDetails.Sum(s => s.TotalPurchases), 2),
                TotalPaid = Math.Round(supplierDetails.Sum(s => s.TotalPaid), 2),
                TotalOutstanding = Math.Round(supplierDetails.Sum(s => s.Outstanding), 2),
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

            var supplierDebts = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId
                         && s.BranchId == branchId
                         && !s.IsDeleted
                         && s.TotalDue > 0)
                .Select(s => new SupplierDebtDetailDto
                {
                    SupplierId = s.Id,
                    SupplierName = s.Name,
                    Phone = s.Phone,
                    TotalDue = s.TotalDue,
                    UnpaidInvoicesCount = s.PurchaseInvoices
                        .Count(pi => !pi.IsDeleted
                                  && pi.AmountDue > 0
                                  && pi.Status != PurchaseInvoiceStatus.Cancelled),
                    OldestUnpaidInvoiceDate = s.PurchaseInvoices
                        .Where(pi => !pi.IsDeleted
                                  && pi.AmountDue > 0
                                  && pi.Status != PurchaseInvoiceStatus.Cancelled)
                        .OrderBy(pi => pi.InvoiceDate)
                        .Select(pi => (DateTime?)pi.InvoiceDate)
                        .FirstOrDefault(),
                    LastPaymentDate = s.PurchaseInvoices
                        .Where(pi => !pi.IsDeleted
                                  && pi.Status != PurchaseInvoiceStatus.Cancelled)
                        .SelectMany(pi => pi.Payments)
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => (DateTime?)p.CreatedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var totalOutstanding = supplierDebts.Sum(s => s.TotalDue);
            var overdueInvoices = supplierDebts
                .Where(s => s.OldestUnpaidInvoiceDate.HasValue
                         && (DateTime.UtcNow - s.OldestUnpaidInvoiceDate.Value).TotalDays > 30)
                .ToList();

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
