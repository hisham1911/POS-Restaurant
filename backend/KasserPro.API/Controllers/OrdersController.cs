namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.Services.Interfaces;
using KasserPro.API.Hubs;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ITenantService _tenantService;
    private readonly IHubContext<DeviceHub> _hubContext;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        ITenantService tenantService,
        IHubContext<DeviceHub> hubContext,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _tenantService = tenantService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _orderService.GetAllAsync(status, fromDate, toDate, page, pageSize);
        return Ok(result);
    }

    [HttpGet("today")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetTodayOrders()
    {
        var result = await _orderService.GetTodayOrdersAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Get all orders for a specific customer with pagination
    /// </summary>
    [HttpGet("by-customer/{customerId}")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetByCustomer(int customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _orderService.GetByCustomerIdAsync(customerId, page, pageSize);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var result = await _orderService.CreateAsync(request, userId);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result) : BadRequest(result);
    }

    [HttpPost("{id}/items")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> AddItem(int id, [FromBody] AddOrderItemRequest request)
    {
        var result = await _orderService.AddItemAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Add a custom POS item (not from product catalog) to an order
    /// </summary>
    [HttpPost("{id}/items/custom")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> AddCustomItem(int id, [FromBody] AddCustomItemRequest request)
    {
        var result = await _orderService.AddCustomItemAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}/items/{itemId}")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> RemoveItem(int id, int itemId)
    {
        var result = await _orderService.RemoveItemAsync(id, itemId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/complete")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteOrderRequest request)
    {
        var result = await _orderService.CompleteAsync(id, request);

        if (result.Success && result.Data != null)
        {
            // Send print command to all connected devices
            try
            {
                var order = result.Data;
                var userName = User.FindFirst("name")?.Value ?? "Cashier";

                // Get tenant settings for receipt configuration
                var tenantResult = await _tenantService.GetCurrentTenantAsync();
                var tenant = tenantResult.Data;
                var printRoutingMode = tenant?.PrintRoutingMode ?? "BranchWithFallback";

                if (tenant?.AutoPrintOnSale == false || printRoutingMode == "Disabled")
                {
                    _logger.LogInformation(
                        "Auto print skipped for order {OrderId}. AutoPrintOnSale={AutoPrintOnSale}, RoutingMode={RoutingMode}",
                        order.Id,
                        tenant?.AutoPrintOnSale,
                        printRoutingMode);
                    return Ok(result);
                }

                var printCommand = new
                {
                    CommandId = Guid.NewGuid().ToString(),
                    Receipt = new
                    {
                        ReceiptNumber = order.OrderNumber,
                        BranchName = order.BranchName ?? "KasserPro Store",
                        Date = ConvertToLocalTime(order.CompletedAt ?? DateTime.UtcNow, tenant?.Timezone),
                        Items = order.Items.Select(item => new
                        {
                            Name = item.ProductName,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TotalPrice = item.Total,
                            DiscountType = item.DiscountType,
                            DiscountValue = item.DiscountValue,
                            DiscountAmount = item.DiscountAmount,
                            DiscountReason = item.DiscountReason
                        }).ToList(),
                        ItemDiscountsTotal = order.Items.Sum(i => i.DiscountAmount),
                        DiscountType = order.DiscountType,
                        DiscountValue = order.DiscountValue,
                        DiscountAmount = order.DiscountAmount,
                        NetTotal = order.Subtotal,
                        TaxAmount = order.TaxAmount,
                        TotalAmount = order.Total,
                        AmountPaid = order.AmountPaid,
                        ChangeAmount = order.ChangeAmount,
                        AmountDue = order.AmountDue,
                        PaymentMethod = order.Payments.FirstOrDefault()?.Method ?? "Cash",
                        CashierName = order.UserName ?? userName,
                        CustomerName = order.CustomerName ?? "",
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
                        ShowCashier = tenant.ReceiptShowCashier,
                        ShowThankYou = tenant.ReceiptShowThankYou,
                        ShowCustomerName = tenant.ReceiptShowCustomerName,
                        ShowLogo = tenant.ReceiptShowLogo,
                        FooterMessage = tenant.ReceiptFooterMessage,
                        PhoneNumber = tenant.ReceiptPhoneNumber,
                        LogoUrl = tenant.LogoUrl,
                        TaxRate = tenant.TaxRate,
                        IsTaxEnabled = tenant.IsTaxEnabled
                    } : (object?)null
                };

                // Send receipt to branch group AND default group to ensure delivery
                var branchId = User.FindFirst("branchId")?.Value ?? "default";
                var branchGroup = $"branch-{branchId}";

                await SendPrintCommandByRoutingAsync(
                    printCommand,
                    branchGroup,
                    printRoutingMode,
                    isAutomatic: true);

                _logger.LogInformation("Print command sent for order {OrderId} to branch group {BranchId}", order.Id, branchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send print command for order {OrderId}", id);
                // Don't fail the request if printing fails
            }
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/cancel")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelOrderRequest? request)
    {
        var result = await _orderService.CancelAsync(id, request?.Reason);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Process a full or partial refund for a completed order.
    /// Restores stock and updates order status.
    /// </summary>
    [HttpPost("{id}/refund")]
    [Authorize(Roles = "Admin,Manager")]
    [HasPermission(Permission.OrdersRefund)]
    public async Task<IActionResult> Refund(int id, [FromBody] RefundRequest request)
    {
        // For full refund (no items), reason is required
        var isPartialRefund = request?.Items != null && request.Items.Count > 0;
        if (!isPartialRefund && string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "سبب الاسترجاع مطلوب للاسترجاع الكامل"));

        var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");

        // Convert to service model
        var refundItems = request?.Items?.Select(i => new Application.DTOs.RefundItemDto
        {
            ItemId = i.ItemId,
            Quantity = i.Quantity,
            Reason = i.Reason
        }).ToList();

        var result = await _orderService.RefundAsync(id, userId, request?.Reason, refundItems);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Print receipt for an existing order
    /// </summary>
    [HttpPost("{id}/print")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> PrintReceipt(int id)
    {
        var orderResult = await _orderService.GetByIdAsync(id);
        if (!orderResult.Success || orderResult.Data == null)
            return NotFound(orderResult);

        var order = orderResult.Data;

        // Only print completed orders
        if (order.Status != "Completed" && order.Status != "PartiallyRefunded" && order.Status != "Refunded")
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, ErrorMessages.Get(ErrorCodes.ORDER_INVALID_STATE_TRANSITION)));

        try
        {
            var userName = User.FindFirst("name")?.Value ?? "Cashier";

            // Get tenant settings for receipt configuration
            var tenantResult = await _tenantService.GetCurrentTenantAsync();
            var tenant = tenantResult.Data;

            var printCommand = new
            {
                CommandId = Guid.NewGuid().ToString(),
                Receipt = new
                {
                    ReceiptNumber = order.OrderNumber,
                    BranchName = order.BranchName ?? "KasserPro Store",
                    Date = ConvertToLocalTime(order.CompletedAt ?? order.CreatedAt, tenant?.Timezone),
                    Items = order.Items.Select(item => new
                    {
                        Name = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.Total,
                        DiscountType = item.DiscountType,
                        DiscountValue = item.DiscountValue,
                        DiscountAmount = item.DiscountAmount,
                        DiscountReason = item.DiscountReason
                    }).ToList(),
                    ItemDiscountsTotal = order.Items.Sum(i => i.DiscountAmount),
                    DiscountType = order.DiscountType,
                    DiscountValue = order.DiscountValue,
                    DiscountAmount = order.DiscountAmount,
                    NetTotal = order.Subtotal,
                    TaxAmount = order.TaxAmount,
                    TotalAmount = order.Total,
                    AmountPaid = order.AmountPaid,
                    ChangeAmount = order.ChangeAmount,
                    AmountDue = order.AmountDue,
                    PaymentMethod = order.Payments.FirstOrDefault()?.Method ?? "Cash",
                    CashierName = order.UserName ?? userName,
                    CustomerName = order.CustomerName ?? "",
                    IsRefund = order.Status == "Refunded" || order.Status == "PartiallyRefunded" || order.OrderType == "Return",
                    RefundReason = order.RefundReason ?? (order.OrderType == "Return" ? "ارجاع بضاعة" : null)
                },
                Settings = tenant != null ? new
                {
                    PaperSize = tenant.ReceiptPaperSize,
                    CustomWidth = tenant.ReceiptCustomWidth,
                    HeaderFontSize = tenant.ReceiptHeaderFontSize,
                    BodyFontSize = tenant.ReceiptBodyFontSize,
                    TotalFontSize = tenant.ReceiptTotalFontSize,
                    ShowBranchName = tenant.ReceiptShowBranchName,
                    ShowCashier = tenant.ReceiptShowCashier,
                    ShowThankYou = tenant.ReceiptShowThankYou,
                    ShowCustomerName = tenant.ReceiptShowCustomerName,
                    ShowLogo = tenant.ReceiptShowLogo,
                    FooterMessage = tenant.ReceiptFooterMessage,
                    PhoneNumber = tenant.ReceiptPhoneNumber,
                    LogoUrl = tenant.LogoUrl,
                    TaxRate = tenant.TaxRate,
                    IsTaxEnabled = tenant.IsTaxEnabled
                } : (object?)null
            };

            // Send receipt to branch group AND default group to ensure delivery
            var branchId = User.FindFirst("branchId")?.Value ?? "default";
            var branchGroup = $"branch-{branchId}";

            await SendPrintCommandByRoutingAsync(
                printCommand,
                branchGroup,
                tenant?.PrintRoutingMode ?? "BranchWithFallback",
                isAutomatic: false);

            _logger.LogInformation("Print command sent for order {OrderId} to branch group {BranchId}", order.Id, branchId);

            return Ok(ApiResponse<bool>.Ok(true, "تم إرسال أمر الطباعة بنجاح"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send print command for order {OrderId}", id);
            return StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
        }
    }

    /// <summary>
    /// Convert UTC time to local time based on tenant timezone (IANA or Windows ID)
    /// </summary>
    private DateTime ConvertToLocalTime(DateTime utcTime, string? timezone)
    {
        if (string.IsNullOrEmpty(timezone))
            return utcTime.ToLocalTime();

        try
        {
            // Try direct lookup (Windows timezone ID like "Egypt Standard Time")
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            // Map common IANA timezone IDs to Windows IDs
            var ianaToWindows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Africa/Cairo"] = "Egypt Standard Time",
                ["Asia/Riyadh"] = "Arab Standard Time",
                ["Asia/Dubai"] = "Arabian Standard Time",
                ["Asia/Kuwait"] = "Arab Standard Time",
                ["Europe/London"] = "GMT Standard Time",
                ["America/New_York"] = "Eastern Standard Time",
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

            // Fallback to local time
            return utcTime.ToLocalTime();
        }
    }

    private async Task SendPrintCommandByRoutingAsync(
        object printCommand,
        string branchGroup,
        string? routingMode,
        bool isAutomatic)
    {
        var mode = string.IsNullOrWhiteSpace(routingMode) ? "BranchWithFallback" : routingMode;

        // Manual print endpoint should keep working even if auto-routing is disabled.
        if (!isAutomatic && string.Equals(mode, "Disabled", StringComparison.Ordinal))
        {
            mode = "BranchWithFallback";
        }

        if (string.Equals(mode, "BranchOnly", StringComparison.Ordinal))
        {
            await _hubContext.Clients.Group(branchGroup).SendAsync("PrintReceipt", printCommand);
            return;
        }

        if (string.Equals(mode, "AllDevices", StringComparison.Ordinal))
        {
            await _hubContext.Clients.All.SendAsync("PrintReceipt", printCommand);
            return;
        }

        if (string.Equals(mode, "Disabled", StringComparison.Ordinal))
        {
            _logger.LogInformation("Skipping automatic print command because routing mode is Disabled");
            return;
        }

        await _hubContext.Clients.Group(branchGroup).SendAsync("PrintReceipt", printCommand);
        if (branchGroup != "branch-default")
        {
            await _hubContext.Clients.Group("branch-default").SendAsync("PrintReceipt", printCommand);
        }
    }
}

public class CancelOrderRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// Request to process a refund (full or partial)
/// </summary>
public class RefundRequest
{
    /// <summary>
    /// General reason for the refund (required for full refund, optional for partial)
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Items to refund. If null or empty, performs a full refund.
    /// </summary>
    public List<RefundItemRequest>? Items { get; set; }
}

/// <summary>
/// A single item to refund in a partial refund
/// </summary>
public class RefundItemRequest
{
    /// <summary>
    /// The order item ID to refund
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Quantity to refund (must be <= original quantity)
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Reason for refunding this specific item (optional)
    /// </summary>
    public string? Reason { get; set; }
}
