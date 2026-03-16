namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;
using KasserPro.API.Hubs;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ITenantService _tenantService;
    private readonly IHubContext<DeviceHub> _hubContext;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportService reportService,
        ITenantService tenantService,
        IHubContext<DeviceHub> hubContext,
        ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _tenantService = tenantService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet("daily")]
    [HasPermission(Permission.ReportsView)]
    public async Task<IActionResult> GetDailyReport([FromQuery] DateTime? date)
    {
        var result = await _reportService.GetDailyReportAsync(date);
        return Ok(result);
    }

    [HttpGet("sales")]
    [HasPermission(Permission.ReportsView)]
    public async Task<IActionResult> GetSalesReport([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetSalesReportAsync(fromDate, toDate);
        return Ok(result);
    }

    /// <summary>
    /// Print daily report as a receipt (sent to thermal printer via SignalR)
    /// </summary>
    [HttpPost("daily/print")]
    [HasPermission(Permission.ReportsView)]
    public async Task<IActionResult> PrintDailyReport([FromQuery] DateTime? date)
    {
        var result = await _reportService.GetDailyReportAsync(date);
        if (!result.Success || result.Data == null)
            return BadRequest(new { Success = false, Message = "فشل في تحميل التقرير اليومي" });

        var report = result.Data;

        try
        {
            var tenantResult = await _tenantService.GetCurrentTenantAsync();
            var tenant = tenantResult.Data;

            var reportDate = report.Date;
            if (tenant?.Timezone != null)
            {
                reportDate = ConvertToLocalTime(report.Date, tenant.Timezone);
            }

            // ===== Format daily report data as a Receipt =====
            // The BridgeApp expects PrintCommandDto { CommandId, Receipt, Settings }
            // We map report summary lines as "items" so the printer renders them.

            var receiptItems = new List<object>();
            // Quantity encoding:
            //  -1 = section header (bold centered divider)
            //   0 = value-only row if TotalPrice > 0, or count-only if Quantity-field carries the number (see count rows below)
            // The BridgeApp PrinterServiceNew uses this convention.

            // --- Section: Orders Summary ---
            receiptItems.Add(new { Name = "الطلبات", Quantity = -1, UnitPrice = 0m, TotalPrice = 0m });
            receiptItems.Add(new { Name = "إجمالي الطلبات",   Quantity = report.TotalOrders,     UnitPrice = 0m, TotalPrice = 0m });
            receiptItems.Add(new { Name = "الطلبات المكتملة", Quantity = report.CompletedOrders,  UnitPrice = 0m, TotalPrice = 0m });
            if (report.CancelledOrders > 0)
                receiptItems.Add(new { Name = "الطلبات الملغاة", Quantity = report.CancelledOrders, UnitPrice = 0m, TotalPrice = 0m });

            // --- Deductions (shown as NEGATIVE so renderer prefixes with "-") ---
            if (report.TotalDiscount > 0)
                receiptItems.Add(new { Name = "خصم", Quantity = 0, UnitPrice = 0m, TotalPrice = -report.TotalDiscount });
            if (report.TotalRefunds > 0)
                receiptItems.Add(new { Name = "مرتجعات", Quantity = 0, UnitPrice = 0m, TotalPrice = -report.TotalRefunds });

            // --- Section: Payment Methods ---
            receiptItems.Add(new { Name = "طرق الدفع", Quantity = -1, UnitPrice = 0m, TotalPrice = 0m });
            if (report.TotalCash > 0)
                receiptItems.Add(new { Name = "نقدي",  Quantity = 0, UnitPrice = 0m, TotalPrice = report.TotalCash });
            if (report.TotalCard > 0)
                receiptItems.Add(new { Name = "بطاقة", Quantity = 0, UnitPrice = 0m, TotalPrice = report.TotalCard });
            if (report.TotalFawry > 0)
                receiptItems.Add(new { Name = "فوري",  Quantity = 0, UnitPrice = 0m, TotalPrice = report.TotalFawry });
            if (report.TotalOther > 0)
                receiptItems.Add(new { Name = "أخرى",  Quantity = 0, UnitPrice = 0m, TotalPrice = report.TotalOther });
            // Deferred / credit: whatever wasn't covered by the above methods
            var totalPaid = report.TotalCash + report.TotalCard + report.TotalFawry + report.TotalOther;
            var totalDeferred = report.TotalSales - totalPaid;
            if (totalDeferred > 0.01m)
                receiptItems.Add(new { Name = "آجل (دين)", Quantity = 0, UnitPrice = 0m, TotalPrice = totalDeferred });

            // --- Section: Shifts ---
            var shifts = report.Shifts ?? new();
            if (shifts.Count > 0)
            {
                receiptItems.Add(new { Name = "الورديات", Quantity = -1, UnitPrice = 0m, TotalPrice = 0m });
                receiptItems.Add(new { Name = "عدد الورديات", Quantity = shifts.Count, UnitPrice = 0m, TotalPrice = 0m });
                foreach (var s in shifts)
                {
                    var flag = s.IsForceClosed ? " ⚠" : "";
                    // Always value row — shows cashier + order count as name, sales as price (even if 0)
                    var shiftLabel = $"{s.UserName}{flag} ({s.TotalOrders})";
                    receiptItems.Add(new { Name = shiftLabel, Quantity = 0, UnitPrice = 0m, TotalPrice = s.TotalSales });
                }
            }

            // --- Section: Top Products ---
            var topProducts = (report.TopProducts ?? new()).Take(10).ToList();
            if (topProducts.Count > 0)
            {
                receiptItems.Add(new { Name = "أعلى المنتجات", Quantity = -1, UnitPrice = 0m, TotalPrice = 0m });
                foreach (var p in topProducts)
                {
                    // Normal product row: Name × Qty | Price
                    receiptItems.Add(new { Name = p.ProductName, Quantity = p.QuantitySold, UnitPrice = p.TotalSales / Math.Max(p.QuantitySold, 1), TotalPrice = p.TotalSales });
                }
            }

            var formattedDate = reportDate.ToString("dd/MM/yyyy");

            var printCommand = new
            {
                CommandId = Guid.NewGuid().ToString(),
                Receipt = new
                {
                    ReceiptNumber = $"تقرير يومي - {formattedDate}",
                    BranchName = report.BranchName ?? "KasserPro Store",
                    Date = reportDate,
                    Items = receiptItems,
                    NetTotal = report.NetSales,
                    TaxAmount = report.TotalTax,
                    TotalAmount = report.TotalSales,
                    AmountPaid = 0m,
                    ChangeAmount = 0m,
                    AmountDue = 0m,
                    PaymentMethod = "",
                    CashierName = "",
                    CustomerName = "",
                    IsRefund = false,
                    RefundReason = (string?)null
                },
                Settings = tenant != null ? new
                {
                    PaperSize = tenant.ReceiptPaperSize,
                    CustomWidth = tenant.ReceiptCustomWidth,
                    HeaderFontSize = tenant.ReceiptHeaderFontSize,
                    BodyFontSize = tenant.ReceiptBodyFontSize,
                    TotalFontSize = tenant.ReceiptTotalFontSize,
                    ShowBranchName = tenant.ReceiptShowBranchName,
                    ShowCashier = false,        // daily report has no cashier line
                    ShowThankYou = false,        // no "شكراً لزيارتكم" for reports
                    ShowCustomerName = false,
                    ShowLogo = tenant.ReceiptShowLogo,
                    FooterMessage = tenant.ReceiptFooterMessage,
                    PhoneNumber = tenant.ReceiptPhoneNumber,
                    LogoUrl = tenant.LogoUrl,
                    TaxRate = tenant.TaxRate,
                    IsTaxEnabled = false         // tax already shown inside items section
                } : (object?)null
            };

            var branchId = User.FindFirst("branchId")?.Value ?? "default";
            var branchGroup = $"branch-{branchId}";

            // Send via the existing PrintReceipt handler so BridgeApp processes it correctly
            await _hubContext.Clients.Group(branchGroup)
                .SendAsync("PrintReceipt", printCommand);

            if (branchGroup != "branch-default")
            {
                await _hubContext.Clients.Group("branch-default")
                    .SendAsync("PrintReceipt", printCommand);
            }

            _logger.LogInformation("Daily report print command sent for date {Date} to branch {BranchId}", date, branchId);

            return Ok(new { Success = true, Message = "تم إرسال أمر طباعة التقرير اليومي بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send daily report print command");
            return StatusCode(500, new { Success = false, Message = "فشل إرسال أمر الطباعة" });
        }
    }

    /// <summary>
    /// Convert UTC time to local time based on tenant timezone
    /// </summary>
    private static DateTime ConvertToLocalTime(DateTime utcTime, string? timezone)
    {
        if (string.IsNullOrEmpty(timezone))
            return utcTime.ToLocalTime();

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            var ianaToWindows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Africa/Cairo"] = "Egypt Standard Time",
                ["Asia/Riyadh"] = "Arab Standard Time",
                ["Asia/Dubai"] = "Arabian Standard Time",
                ["UTC"] = "UTC",
            };

            if (ianaToWindows.TryGetValue(timezone, out var windowsId))
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                    return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
                }
                catch { }
            }

            return utcTime.ToLocalTime();
        }
    }
}
