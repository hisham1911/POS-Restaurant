namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
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
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, "فشل في تحميل التقرير اليومي"));

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
            if (report.TotalBankAccount > 0)
                receiptItems.Add(new { Name = "بنك", Quantity = 0, UnitPrice = 0m, TotalPrice = report.TotalBankAccount });
            if (report.TotalWallet > 0)
                receiptItems.Add(new { Name = "محفظة",  Quantity = 0, UnitPrice = 0m, TotalPrice = report.TotalWallet });
            // Deferred / credit: whatever wasn't covered by the above methods
            var totalDeferred = report.TotalDeferred;
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
            var topProducts = (report.TopProducts ?? new()).ToList();
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
            var printRoutingMode = tenant?.PrintRoutingMode ?? "BranchWithFallback";
            var targetDeviceId = Request.Headers["X-Target-Device-Id"].ToString();

            // Send via the existing PrintReceipt handler so BridgeApp processes it correctly
            var printSent = await SendPrintCommandByRoutingAsync(
                printCommand,
                branchGroup,
                printRoutingMode,
                targetDeviceId,
                isAutomatic: false);

            if (!printSent)
            {
                _logger.LogWarning(
                    "Daily report print for branch {BranchId} could not be delivered; no connected printer device",
                    branchId);
                return StatusCode(503, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
            }

            _logger.LogInformation("Daily report print command sent for date {Date} to branch {BranchId}", date, branchId);

            return Ok(ApiResponse<bool>.Ok(true, "تم إرسال أمر طباعة التقرير اليومي بنجاح"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send daily report print command");
            return StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
        }
    }

    /// <summary>
    /// Convert UTC time to local time based on tenant timezone
    /// </summary>
    private DateTime ConvertToLocalTime(DateTime utcTime, string? timezone)
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
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to convert timezone using {WindowsId}", windowsId);
                }
            }

            return utcTime.ToLocalTime();
        }
    }

    private async Task<bool> SendPrintCommandByRoutingAsync(
        object printCommand,
        string branchGroup,
        string? routingMode,
        string? targetDeviceId,
        bool isAutomatic)
    {
        var mode = string.IsNullOrWhiteSpace(routingMode) ? "BranchWithFallback" : routingMode;

        // Manual print endpoint should keep working even if auto-routing is disabled.
        if (!isAutomatic && string.Equals(mode, "Disabled", StringComparison.Ordinal))
        {
            mode = "BranchWithFallback";
        }

        if (string.Equals(mode, "Disabled", StringComparison.Ordinal))
        {
            _logger.LogInformation("Skipping automatic print command because routing mode is Disabled");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(targetDeviceId))
        {
            var normalizedTargetDeviceId = targetDeviceId.Trim();
            var targetConnectionId = string.Equals(mode, "BranchOnly", StringComparison.Ordinal)
                ? DeviceHub.GetConnectionIdForDevice(normalizedTargetDeviceId, branchGroup)
                : DeviceHub.GetConnectionIdForDevice(normalizedTargetDeviceId, branchGroup, "branch-default");

            if (string.IsNullOrWhiteSpace(targetConnectionId))
            {
                _logger.LogWarning(
                    "Target printer device {DeviceId} is not connected in branch scope {BranchGroup}",
                    normalizedTargetDeviceId,
                    branchGroup);
                return false;
            }

            await _hubContext.Clients.Client(targetConnectionId).SendAsync("PrintReceipt", printCommand);
            _logger.LogInformation(
                "Print command routed to target device {DeviceId} using connection {ConnectionId}",
                normalizedTargetDeviceId,
                targetConnectionId);
            return true;
        }

        if (string.Equals(mode, "BranchOnly", StringComparison.Ordinal))
        {
            var sentToBranchOnly = await SendToPreferredDeviceAsync(branchGroup, "PrintReceipt", printCommand);
            if (!sentToBranchOnly)
            {
                _logger.LogWarning("No connected printer device found for group {BranchGroup}", branchGroup);
            }

            return sentToBranchOnly;
        }

        if (string.Equals(mode, "AllDevices", StringComparison.Ordinal))
        {
            await _hubContext.Clients.All.SendAsync("PrintReceipt", printCommand);
            return DeviceHub.GetConnectedDeviceCount() > 0;
        }

        if (await SendToPreferredDeviceAsync(branchGroup, "PrintReceipt", printCommand))
        {
            return true;
        }

        if (branchGroup != "branch-default"
            && await SendToPreferredDeviceAsync("branch-default", "PrintReceipt", printCommand))
        {
            return true;
        }

        _logger.LogWarning(
            "No connected printer device found for primary group {BranchGroup} or fallback group branch-default",
            branchGroup);

        return false;
    }

    private async Task<bool> SendToPreferredDeviceAsync(string groupName, string hubMethod, object printCommand)
    {
        var connectionId = DeviceHub.GetPreferredConnectionId(groupName);
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return false;
        }

        await _hubContext.Clients.Client(connectionId).SendAsync(hubMethod, printCommand);
        _logger.LogInformation(
            "Print command routed to device connection {ConnectionId} in group {GroupName}",
            connectionId,
            groupName);

        return true;
    }
}
