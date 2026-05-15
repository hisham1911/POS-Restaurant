namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.DTOs.Tenants;
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

    private int GetUserId()
    {
        var claim = User.FindFirst("userId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    [HttpGet]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] string? status = null,
        [FromQuery] string? orderType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _orderService.GetAllAsync(status, orderType, fromDate, toDate, page, pageSize);
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
        var userId = GetUserId();
        if (userId == 0) return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));
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
    [HasPermission(Permission.PosDeleteItem)]
    public async Task<IActionResult> RemoveItem(int id, int itemId)
    {
        var result = await _orderService.RemoveItemAsync(id, itemId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/send-to-kitchen")]
    [HasPermission(Permission.PosSell)]
    public async Task<IActionResult> SendToKitchen(int id)
    {
        var result = await _orderService.SendToKitchenAsync(id);
        if (!result.Success || result.Data == null)
            return BadRequest(result);

        try
        {
            var tenantResult = await _tenantService.GetCurrentTenantAsync();
            var tenant = tenantResult.Data;
            var ticket = result.Data;
            var commandId = Guid.NewGuid().ToString();
            var printCompletionTask = DeviceHub.WaitForPrintCompletionAsync(
                commandId,
                TimeSpan.FromSeconds(15),
                HttpContext.RequestAborted);

            var printCommand = new
            {
                CommandId = commandId,
                Receipt = new
                {
                    ReceiptNumber = $"KITCHEN-{ticket.OrderNumber}-{ticket.KitchenPrintCount}",
                    BranchName = tenant?.Name ?? "KasserPro Store",
                    Date = ConvertToLocalTime(ticket.PrintedAt, tenant?.Timezone),
                    Items = ticket.Items.Select(item => new
                    {
                        Name = item.Name,
                        Quantity = item.Quantity,
                        UnitPrice = 0m,
                        TotalPrice = 0m,
                        Notes = item.Notes,
                        IsAddOn = item.IsAddOn
                    }).ToList(),
                    NetTotal = 0m,
                    TaxAmount = 0m,
                    TotalAmount = 0m,
                    AmountPaid = 0m,
                    ChangeAmount = 0m,
                    AmountDue = 0m,
                    PaymentMethod = "",
                    CashierName = User.FindFirst("name")?.Value ?? "Cashier",
                    CustomerName = "",
                    IsRefund = false,
                    RefundReason = (string?)null,
                    IsKitchenTicket = true,
                    KitchenTitle = ticket.Header,
                    OrderNotes = ticket.Notes,
                    IsAdditionTicket = ticket.TicketType == "Additions"
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
                    ShowThankYou = false,
                    ShowCustomerName = false,
                    ShowLogo = tenant.ReceiptShowLogo,
                    FooterMessage = tenant.ReceiptFooterMessage,
                    PhoneNumber = tenant.ReceiptPhoneNumber,
                    LogoUrl = tenant.LogoUrl,
                    TaxRate = tenant.TaxRate,
                    IsTaxEnabled = tenant.IsTaxEnabled
                } : (object?)null
            };

            var branchId = User.FindFirst("branchId")?.Value ?? "default";
            var branchGroup = $"branch-{branchId}";
            var printSent = await SendPrintCommandByRoutingAsync(
                printCommand,
                branchGroup,
                tenant?.PrintRoutingMode ?? "BranchWithFallback",
                Request.Headers["X-Target-Device-Id"].ToString(),
                isAutomatic: false);

            if (!printSent)
            {
                _logger.LogWarning("Kitchen ticket for order {OrderId} was not marked printed because no printer device received it", id);
                return StatusCode(503, ApiResponse<object>.Fail(
                    ErrorCodes.INTERNAL_ERROR,
                    "تعذر إرسال تذكرة المطبخ للطابعة. تأكد من اتصال برنامج الطابعة ثم حاول مرة أخرى."));
            }

            var printCompleted = await printCompletionTask;
            if (!printCompleted)
            {
                _logger.LogWarning("Kitchen ticket print command {CommandId} for order {OrderId} was delivered but not confirmed by the printer bridge", commandId, id);
                return StatusCode(503, ApiResponse<object>.Fail(
                    ErrorCodes.INTERNAL_ERROR,
                    "تم إرسال تذكرة المطبخ للبرنامج لكن لم يتم تأكيد الطباعة. راجع الطابعة أو برنامج Bridge ثم حاول مرة أخرى."));
            }

            var markPrintedResult = await _orderService.MarkKitchenTicketPrintedAsync(id, ticket);
            if (!markPrintedResult.Success)
            {
                _logger.LogWarning(
                    "Kitchen ticket for order {OrderId} was delivered to printer but failed to mark as printed: {ErrorCode}",
                    id,
                    markPrintedResult.ErrorCode);
                return BadRequest(markPrintedResult);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send kitchen ticket print command for order {OrderId}", id);
            return StatusCode(500, ApiResponse<object>.Fail(
                ErrorCodes.INTERNAL_ERROR,
                "حدث خطأ أثناء إرسال تذكرة المطبخ للطابعة."));
        }

        return Ok(result);
    }

    [HttpPost("{id}/complete")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteOrderRequest request)
    {
        var result = await _orderService.CompleteAsync(id, request);
        var printAttempted = false;
        var printDelivered = false;
        var kitchenPrintAttempted = false;
        var kitchenPrintDelivered = false;
        var clientPrintPreference = Request.Headers["X-Print-Preference"].ToString();
        var targetDeviceId = Request.Headers["X-Target-Device-Id"].ToString();
        var browserOnlyPrintRequested =
            string.Equals(clientPrintPreference, "BrowserOnly", StringComparison.OrdinalIgnoreCase);
        var kitchenPrintRequested = IsKitchenPrintRequested();

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

                if (tenant?.AutoPrintOnSale == false || printRoutingMode == "Disabled" || browserOnlyPrintRequested)
                {
                    _logger.LogInformation(
                        "Auto print skipped for order {OrderId}. AutoPrintOnSale={AutoPrintOnSale}, RoutingMode={RoutingMode}, ClientPrintPreference={ClientPrintPreference}",
                        order.Id,
                        tenant?.AutoPrintOnSale,
                        printRoutingMode,
                        string.IsNullOrWhiteSpace(clientPrintPreference) ? "Auto" : clientPrintPreference);
                }
                else
                {
                    var branchId = User.FindFirst("branchId")?.Value ?? "default";
                    var branchGroup = $"branch-{branchId}";

                    if (kitchenPrintRequested)
                    {
                        kitchenPrintAttempted = true;
                        kitchenPrintDelivered = await SendPrintCommandByRoutingAsync(
                            BuildKitchenReceiptPrintCommand(order, tenant, userName),
                            branchGroup,
                            printRoutingMode,
                            targetDeviceId,
                            isAutomatic: true);

                        if (!kitchenPrintDelivered)
                        {
                            _logger.LogWarning(
                                "Kitchen receipt auto print could not be delivered for order {OrderId}; no connected printer device",
                                order.Id);
                        }
                    }

                    printAttempted = true;
                    printDelivered = await SendPrintCommandByRoutingAsync(
                        BuildSalesReceiptPrintCommand(order, tenant, userName),
                        branchGroup,
                        printRoutingMode,
                        targetDeviceId,
                        isAutomatic: true);

                    if (!printDelivered)
                    {
                        _logger.LogWarning(
                            "Auto print could not be delivered for order {OrderId}; no connected printer device",
                            order.Id);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Auto print delivered for order {OrderId} to branch group {BranchId}",
                            order.Id,
                            branchId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send print command for order {OrderId}", id);
                // Don't fail the request if printing fails
            }
        }

        if (result.Success)
        {
            Response.Headers["X-Print-Attempted"] = printAttempted ? "1" : "0";
            Response.Headers["X-Print-Delivered"] = printDelivered ? "1" : "0";
            Response.Headers["X-Kitchen-Print-Attempted"] = kitchenPrintAttempted ? "1" : "0";
            Response.Headers["X-Kitchen-Print-Delivered"] = kitchenPrintDelivered ? "1" : "0";
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPatch("{id}/status")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var result = await _orderService.UpdateStatusAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/cancel")]
    [HasPermission(Permission.PosCancelOrder)]
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
    [HasPermission(Permission.OrdersRefund)]
    public async Task<IActionResult> Refund(int id, [FromBody] RefundRequest request)
    {
        // For full refund (no items), reason is required
        var isPartialRefund = request?.Items != null && request.Items.Count > 0;
        if (!isPartialRefund && string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "سبب الاسترجاع مطلوب للاسترجاع الكامل"));

        var userId = GetUserId();
        if (userId == 0) return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.UNAUTHORIZED, ErrorMessages.Get(ErrorCodes.UNAUTHORIZED)));

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
                    RefundReason = order.RefundReason ?? (order.OrderType == "Return" ? "ارجاع بضاعة" : null),
                    OrderType = order.OrderType,
                    DeliveryAddress = order.DeliveryAddress,
                    DeliveryFee = order.DeliveryFee,
                    DeliveryNotes = order.DeliveryNotes,
                    DeliveryStatus = order.DeliveryStatus
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

            var printSent = await SendPrintCommandByRoutingAsync(
                printCommand,
                branchGroup,
                tenant?.PrintRoutingMode ?? "BranchWithFallback",
                Request.Headers["X-Target-Device-Id"].ToString(),
                isAutomatic: false);

            if (!printSent)
            {
                _logger.LogWarning(
                    "Manual print request for order {OrderId} failed because no printer device is connected",
                    order.Id);
                return StatusCode(503, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
            }

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

    private bool IsKitchenPrintRequested()
    {
        var value = Request.Headers["X-Print-Kitchen-Ticket"].ToString();
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private PrintCommandDto BuildKitchenReceiptPrintCommand(OrderDto order, TenantDto? tenant, string userName)
    {
        return new PrintCommandDto
        {
            CommandId = Guid.NewGuid().ToString(),
            Receipt = new ReceiptDto
            {
                ReceiptNumber = $"KITCHEN-{order.OrderNumber}",
                BranchName = order.BranchName ?? tenant?.Name ?? "KasserPro Store",
                Date = ConvertToLocalTime(order.CompletedAt ?? DateTime.UtcNow, tenant?.Timezone),
                Items = order.Items.Select(item => new ReceiptItemDto
                {
                    Name = item.ParentOrderItemId.HasValue ? $"+ {item.ProductName}" : item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = 0m,
                    TotalPrice = 0m,
                    Notes = item.Notes,
                    IsAddOn = item.ParentOrderItemId.HasValue || item.IsCustomItem
                }).ToList(),
                NetTotal = 0m,
                TaxAmount = 0m,
                TotalAmount = 0m,
                AmountPaid = 0m,
                ChangeAmount = 0m,
                AmountDue = 0m,
                PaymentMethod = string.Empty,
                CashierName = order.UserName ?? userName,
                CustomerName = string.Empty,
                IsRefund = false,
                RefundReason = null,
                OrderType = order.OrderType,
                IsKitchenTicket = true,
                KitchenTitle = GetKitchenTitle(order),
                OrderNotes = order.Notes,
                IsAdditionTicket = false
            },
            Settings = BuildReceiptSettings(tenant, showThankYou: false, showCustomerName: false)
        };
    }

    private PrintCommandDto BuildSalesReceiptPrintCommand(OrderDto order, TenantDto? tenant, string userName)
    {
        return new PrintCommandDto
        {
            CommandId = Guid.NewGuid().ToString(),
            Receipt = new ReceiptDto
            {
                ReceiptNumber = order.OrderNumber,
                BranchName = order.BranchName ?? tenant?.Name ?? "KasserPro Store",
                Date = ConvertToLocalTime(order.CompletedAt ?? DateTime.UtcNow, tenant?.Timezone),
                Items = order.Items.Select(item => new ReceiptItemDto
                {
                    Name = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.Total,
                    DiscountType = item.DiscountType,
                    DiscountValue = item.DiscountValue,
                    DiscountAmount = item.DiscountAmount,
                    DiscountReason = item.DiscountReason,
                    Notes = item.Notes,
                    IsAddOn = item.ParentOrderItemId.HasValue || item.IsCustomItem
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
                CustomerName = order.CustomerName ?? string.Empty,
                IsRefund = false,
                RefundReason = null,
                OrderType = order.OrderType,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryFee = order.DeliveryFee,
                DeliveryNotes = order.DeliveryNotes,
                DeliveryStatus = order.DeliveryStatus,
                OrderNotes = order.Notes
            },
            Settings = BuildReceiptSettings(tenant, tenant?.ReceiptShowThankYou ?? true, tenant?.ReceiptShowCustomerName ?? true)
        };
    }

    private static ReceiptPrintSettings BuildReceiptSettings(
        TenantDto? tenant,
        bool showThankYou,
        bool showCustomerName)
    {
        return new ReceiptPrintSettings
        {
            PaperSize = tenant?.ReceiptPaperSize ?? "80mm",
            CustomWidth = tenant?.ReceiptCustomWidth,
            HeaderFontSize = tenant?.ReceiptHeaderFontSize ?? 12,
            BodyFontSize = tenant?.ReceiptBodyFontSize ?? 9,
            TotalFontSize = tenant?.ReceiptTotalFontSize ?? 11,
            ShowBranchName = tenant?.ReceiptShowBranchName ?? true,
            ShowCashier = tenant?.ReceiptShowCashier ?? true,
            ShowThankYou = showThankYou,
            ShowCustomerName = showCustomerName,
            ShowLogo = tenant?.ReceiptShowLogo ?? true,
            FooterMessage = tenant?.ReceiptFooterMessage,
            PhoneNumber = tenant?.ReceiptPhoneNumber,
            LogoUrl = tenant?.LogoUrl,
            TaxRate = tenant?.TaxRate ?? 14m,
            IsTaxEnabled = tenant?.IsTaxEnabled ?? true
        };
    }

    private static string GetKitchenTitle(OrderDto order)
    {
        if (string.Equals(order.OrderType, "DineIn", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(order.TableNumberSnapshot)
                ? "طاولة"
                : $"طاولة {order.TableNumberSnapshot}";
        }

        if (string.Equals(order.OrderType, "Delivery", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(order.CustomerName)
                ? "دليفري"
                : $"دليفري - {order.CustomerName}";
        }

        return "تيك أواي";
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

                if (isAutomatic || string.Equals(mode, "BranchOnly", StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else
            {
                await _hubContext.Clients.Client(targetConnectionId).SendAsync("PrintReceipt", printCommand);
                _logger.LogInformation(
                    "Print command routed to target device {DeviceId} using connection {ConnectionId}",
                    normalizedTargetDeviceId,
                    targetConnectionId);
                return true;
            }
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

    /// <summary>
    /// Mark a delivery order as delivered and complete it
    /// </summary>
    [HttpPost("{id}/mark-delivered")]
    [HasPermission(Permission.OrdersCreate)]
    public async Task<IActionResult> MarkAsDelivered(int id)
    {
        var result = await _orderService.MarkAsDeliveredAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
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
