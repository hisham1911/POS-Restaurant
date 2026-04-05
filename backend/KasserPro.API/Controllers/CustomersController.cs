namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Customers;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;
using KasserPro.API.Hubs;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ITenantService _tenantService;
    private readonly IHubContext<DeviceHub> _hubContext;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerService customerService,
        ITenantService tenantService,
        IHubContext<DeviceHub> hubContext,
        ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _tenantService = tenantService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var result = await _customerService.GetAllAsync(page, pageSize, search);
        return Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return result != null
            ? Ok(ApiResponse<CustomerDto>.Ok(result))
            : NotFound(ApiResponse<object>.Fail(ErrorCodes.CUSTOMER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CUSTOMER_NOT_FOUND)));
    }

    [HttpGet("by-phone/{phone}")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetByPhone(string phone)
    {
        var result = await _customerService.GetByPhoneAsync(phone);
        return result != null
            ? Ok(ApiResponse<CustomerDto>.Ok(result))
            : NotFound(ApiResponse<object>.Fail(ErrorCodes.CUSTOMER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CUSTOMER_NOT_FOUND)));
    }

    [HttpPost]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "رقم الهاتف مطلوب"));

        try
        {
            var result = await _customerService.CreateAsync(request);
            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                ApiResponse<CustomerDto>.Ok(result, "تم إنشاء العميل بنجاح"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Customer creation conflict for phone {Phone}", request.Phone);
            return Conflict(ApiResponse<object>.Fail(ErrorCodes.CONFLICT, ErrorMessages.Get(ErrorCodes.CONFLICT)));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Customer creation validation failed for phone {Phone}", request.Phone);
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR)));
        }
    }

    [HttpPut("{id}")]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        try
        {
            var result = await _customerService.UpdateAsync(id, request);
            return result != null
                ? Ok(ApiResponse<CustomerDto>.Ok(result, "تم تحديث بيانات العميل بنجاح"))
                : NotFound(ApiResponse<object>.Fail(ErrorCodes.CUSTOMER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CUSTOMER_NOT_FOUND)));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Customer update validation failed for customer {CustomerId}", id);
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR)));
        }
    }

    [HttpPost("get-or-create")]
    public async Task<IActionResult> GetOrCreate([FromBody] GetOrCreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "رقم الهاتف مطلوب"));

        try
        {
            var (customer, wasCreated) = await _customerService.GetOrCreateByPhoneAsync(request.Phone, request.Name);
            var payload = new GetOrCreateCustomerResult
            {
                Customer = customer,
                WasCreated = wasCreated
            };
            var message = wasCreated ? "تم إنشاء عميل جديد بنجاح" : "تم العثور على العميل الحالي";
            return Ok(ApiResponse<GetOrCreateCustomerResult>.Ok(payload, message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "GetOrCreate customer validation failed for phone {Phone}", request.Phone);
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR)));
        }
    }

    [HttpPost("{id}/loyalty/add")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddLoyaltyPoints(int id, [FromBody] LoyaltyPointsRequest request)
    {
        if (request.Points <= 0)
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "عدد النقاط يجب أن يكون أكبر من صفر"));

        await _customerService.AddLoyaltyPointsAsync(id, request.Points);
        return Ok(ApiResponse<bool>.Ok(true, $"تمت إضافة {request.Points} نقطة ولاء بنجاح"));
    }

    [HttpPost("{id}/loyalty/redeem")]
    public async Task<IActionResult> RedeemLoyaltyPoints(int id, [FromBody] LoyaltyPointsRequest request)
    {
        if (request.Points <= 0)
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "عدد النقاط يجب أن يكون أكبر من صفر"));

        var success = await _customerService.RedeemLoyaltyPointsAsync(id, request.Points);
        return success
            ? Ok(ApiResponse<bool>.Ok(true, $"تم استبدال {request.Points} نقطة ولاء بنجاح"))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.VALIDATION_ERROR, "رصيد نقاط الولاء غير كافٍ"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _customerService.DeleteAsync(id);
        return success
            ? Ok(ApiResponse<bool>.Ok(true, "تم حذف العميل بنجاح"))
            : NotFound(ApiResponse<object>.Fail(ErrorCodes.CUSTOMER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CUSTOMER_NOT_FOUND)));
    }

    [HttpPost("{id}/pay-debt")]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> PayDebt(int id, [FromBody] PayDebtRequest request)
    {
        var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var result = await _customerService.PayDebtAsync(id, request, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        var paymentId = result.Data?.PaymentId ?? 0;
        if (paymentId > 0)
        {
            try
            {
                await TrySendDebtPaymentReceiptAsync(paymentId, isAutomatic: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Debt payment recorded but automatic print failed for payment {PaymentId}", paymentId);
            }
        }

        return Ok(result);
    }

    [HttpGet("{id}/debt-history")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetDebtHistory(int id)
    {
        var history = await _customerService.GetDebtPaymentHistoryAsync(id);
        return Ok(ApiResponse<List<DebtPaymentDto>>.Ok(history));
    }

    [HttpGet("with-debt")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetCustomersWithDebt()
    {
        var customers = await _customerService.GetCustomersWithDebtAsync();
        return Ok(ApiResponse<List<CustomerDto>>.Ok(customers));
    }

    [HttpPost("debt-payments/{paymentId}/print")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> PrintDebtPaymentReceipt(int paymentId)
    {
        try
        {
            var (sent, paymentFound) = await TrySendDebtPaymentReceiptAsync(paymentId, isAutomatic: false);
            if (!paymentFound)
                return NotFound(ApiResponse<object>.Fail(ErrorCodes.PAYMENT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PAYMENT_NOT_FOUND)));

            if (!sent)
            {
                return StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
            }

            return Ok(ApiResponse<bool>.Ok(true, "تم إرسال أمر الطباعة بنجاح"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send print command for debt payment {PaymentId}", paymentId);
            return StatusCode(500, ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
        }
    }

    private async Task<(bool Sent, bool PaymentFound)> TrySendDebtPaymentReceiptAsync(int paymentId, bool isAutomatic)
    {
        var tenantId = int.Parse(User.FindFirst("tenantId")?.Value ?? "0");
        var payment = await _customerService.GetDebtPaymentByIdAsync(paymentId, tenantId);
        if (payment == null)
        {
            _logger.LogWarning("Debt payment {PaymentId} not found for tenant {TenantId}", paymentId, tenantId);
            return (false, false);
        }

        var userName = User.FindFirst("name")?.Value ?? "Cashier";
        var branchId = User.FindFirst("branchId")?.Value ?? "default";

        var tenantResult = await _tenantService.GetCurrentTenantAsync();
        var tenant = tenantResult.Data;
        var printRoutingMode = tenant?.PrintRoutingMode ?? "BranchWithFallback";

        if (isAutomatic && (tenant?.AutoPrintOnDebtPayment == false || printRoutingMode == "Disabled"))
        {
            _logger.LogInformation(
                "Automatic debt-payment print skipped for payment {PaymentId}. AutoPrintOnDebtPayment={AutoPrintOnDebtPayment}, RoutingMode={RoutingMode}",
                paymentId,
                tenant?.AutoPrintOnDebtPayment,
                printRoutingMode);
            return (false, true);
        }

        var printCommand = new PrintCommandDto
        {
            CommandId = Guid.NewGuid().ToString(),
            Receipt = new ReceiptDto
            {
                ReceiptNumber = $"PAY-{payment.Id}",
                BranchName = tenant?.Name ?? "KasserPro Store",
                Date = DateTime.Now,
                CustomerName = payment.CustomerName ?? string.Empty,
                PaymentMethod = payment.PaymentMethod.ToString(),
                CashierName = payment.RecordedByUserName ?? userName,
                NetTotal = payment.BalanceBefore,
                TaxAmount = 0,
                TotalAmount = payment.BalanceBefore,
                AmountPaid = payment.Amount,
                ChangeAmount = 0,
                AmountDue = payment.BalanceAfter,
                Items = new List<ReceiptItemDto>()
            },
            Settings = new ReceiptPrintSettings
            {
                PaperSize = tenant?.ReceiptPaperSize ?? "80mm",
                CustomWidth = tenant?.ReceiptCustomWidth,
                HeaderFontSize = tenant?.ReceiptHeaderFontSize ?? 12,
                BodyFontSize = tenant?.ReceiptBodyFontSize ?? 9,
                TotalFontSize = tenant?.ReceiptTotalFontSize ?? 11,
                ShowBranchName = tenant?.ReceiptShowBranchName ?? true,
                ShowCashier = tenant?.ReceiptShowCashier ?? true,
                ShowThankYou = tenant?.ReceiptShowThankYou ?? true,
                ShowCustomerName = tenant?.ReceiptShowCustomerName ?? true,
                ShowLogo = tenant?.ReceiptShowLogo ?? true,
                FooterMessage = tenant?.ReceiptFooterMessage,
                PhoneNumber = tenant?.ReceiptPhoneNumber,
                LogoUrl = tenant?.LogoUrl
            }
        };

        var branchGroup = $"branch-{branchId}";
        await SendPrintCommandByRoutingAsync(
            printCommand,
            branchGroup,
            printRoutingMode,
            isAutomatic,
            "PrintDebtPaymentReceipt");

        _logger.LogInformation("Print command sent for debt payment {PaymentId} to branch group {BranchId}", paymentId, branchId);
        return (true, true);
    }

    private async Task SendPrintCommandByRoutingAsync(
        object printCommand,
        string branchGroup,
        string? routingMode,
        bool isAutomatic,
        string hubMethod)
    {
        var mode = string.IsNullOrWhiteSpace(routingMode) ? "BranchWithFallback" : routingMode;

        // Manual print endpoint should keep working even if auto-routing is disabled.
        if (!isAutomatic && string.Equals(mode, "Disabled", StringComparison.Ordinal))
        {
            mode = "BranchWithFallback";
        }

        if (string.Equals(mode, "BranchOnly", StringComparison.Ordinal))
        {
            await _hubContext.Clients.Group(branchGroup).SendAsync(hubMethod, printCommand);
            return;
        }

        if (string.Equals(mode, "AllDevices", StringComparison.Ordinal))
        {
            await _hubContext.Clients.All.SendAsync(hubMethod, printCommand);
            return;
        }

        if (string.Equals(mode, "Disabled", StringComparison.Ordinal))
        {
            _logger.LogInformation("Skipping automatic print command because routing mode is Disabled");
            return;
        }

        await _hubContext.Clients.Group(branchGroup).SendAsync(hubMethod, printCommand);
        if (branchGroup != "branch-default")
        {
            await _hubContext.Clients.Group("branch-default").SendAsync(hubMethod, printCommand);
        }
    }
}

public class GetOrCreateCustomerRequest
{
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public class LoyaltyPointsRequest
{
    public int Points { get; set; }
}
