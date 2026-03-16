namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using KasserPro.Application.DTOs.Customers;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;
using KasserPro.API.Hubs;

/// <summary>
/// Customer management endpoints
/// </summary>
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

    /// <summary>
    /// Get all customers with pagination and optional search
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20)</param>
    /// <param name="search">Search by phone, name, or email</param>
    [HttpGet]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var result = await _customerService.GetAllAsync(page, pageSize, search);
        return Ok(new { Success = true, Data = result });
    }

    /// <summary>
    /// Get customer by ID
    /// </summary>
    [HttpGet("{id}")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return result != null 
            ? Ok(new { Success = true, Data = result }) 
            : NotFound(new { Success = false, Message = "Customer not found" });
    }

    /// <summary>
    /// Get customer by phone number
    /// </summary>
    [HttpGet("by-phone/{phone}")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetByPhone(string phone)
    {
        var result = await _customerService.GetByPhoneAsync(phone);
        return result != null 
            ? Ok(new { Success = true, Data = result }) 
            : NotFound(new { Success = false, Message = "Customer not found" });
    }

    /// <summary>
    /// Create a new customer
    /// </summary>
    [HttpPost]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { Success = false, Message = "Phone number is required" });

        try
        {
            var result = await _customerService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, new { Success = true, Data = result });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Success = false, Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing customer
    /// </summary>
    [HttpPut("{id}")]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _customerService.UpdateAsync(id, request);
        return result != null 
            ? Ok(new { Success = true, Data = result }) 
            : NotFound(new { Success = false, Message = "Customer not found" });
    }

    /// <summary>
    /// Get or create customer by phone (auto-create if not exists)
    /// Used during POS checkout flow
    /// </summary>
    [HttpPost("get-or-create")]
    public async Task<IActionResult> GetOrCreate([FromBody] GetOrCreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { Success = false, Message = "Phone number is required" });

        try
        {
            var (customer, wasCreated) = await _customerService.GetOrCreateByPhoneAsync(request.Phone, request.Name);
            return Ok(new { 
                Success = true, 
                Data = customer, 
                WasCreated = wasCreated,
                Message = wasCreated ? "New customer created" : "Existing customer found"
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Add loyalty points to customer
    /// </summary>
    [HttpPost("{id}/loyalty/add")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddLoyaltyPoints(int id, [FromBody] LoyaltyPointsRequest request)
    {
        if (request.Points <= 0)
            return BadRequest(new { Success = false, Message = "Points must be positive" });

        await _customerService.AddLoyaltyPointsAsync(id, request.Points);
        return Ok(new { Success = true, Message = $"Added {request.Points} loyalty points" });
    }

    /// <summary>
    /// Redeem loyalty points from customer
    /// </summary>
    [HttpPost("{id}/loyalty/redeem")]
    public async Task<IActionResult> RedeemLoyaltyPoints(int id, [FromBody] LoyaltyPointsRequest request)
    {
        if (request.Points <= 0)
            return BadRequest(new { Success = false, Message = "Points must be positive" });

        var success = await _customerService.RedeemLoyaltyPointsAsync(id, request.Points);
        return success 
            ? Ok(new { Success = true, Message = $"Redeemed {request.Points} loyalty points" })
            : BadRequest(new { Success = false, Message = "Insufficient loyalty points" });
    }

    /// <summary>
    /// Delete (soft) a customer
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _customerService.DeleteAsync(id);
        return success 
            ? Ok(new { Success = true, Message = "Customer deleted" })
            : NotFound(new { Success = false, Message = "Customer not found" });
    }

    /// <summary>
    /// Record a debt payment from customer
    /// </summary>
    [HttpPost("{id}/pay-debt")]
    [HasPermission(Permission.CustomersManage)]
    public async Task<IActionResult> PayDebt(int id, [FromBody] PayDebtRequest request)
    {
        var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var result = await _customerService.PayDebtAsync(id, request, userId);
        
        return result.Success 
            ? Ok(result) 
            : BadRequest(result);
    }

    /// <summary>
    /// Get debt payment history for a customer
    /// </summary>
    [HttpGet("{id}/debt-history")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetDebtHistory(int id)
    {
        var history = await _customerService.GetDebtPaymentHistoryAsync(id);
        return Ok(new { Success = true, Data = history });
    }

    /// <summary>
    /// Get all customers with outstanding debt
    /// </summary>
    [HttpGet("with-debt")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> GetCustomersWithDebt()
    {
        var customers = await _customerService.GetCustomersWithDebtAsync();
        return Ok(new { Success = true, Data = customers });
    }

    /// <summary>
    /// Print debt payment receipt
    /// </summary>
    [HttpPost("debt-payments/{paymentId}/print")]
    [HasPermission(Permission.CustomersView)]
    public async Task<IActionResult> PrintDebtPaymentReceipt(int paymentId)
    {
        try
        {
            // Get debt payment details
            var tenantId = int.Parse(User.FindFirst("tenantId")?.Value ?? "0");
            var payment = await _customerService.GetDebtPaymentByIdAsync(paymentId, tenantId);
            
            if (payment == null)
                return NotFound(new { Success = false, Message = "الدفعة غير موجودة" });

            var userName = User.FindFirst("name")?.Value ?? "Cashier";
            var branchId = User.FindFirst("branchId")?.Value ?? "default";

            // Get tenant settings
            var tenantResult = await _tenantService.GetCurrentTenantAsync();
            var tenant = tenantResult.Data;

            // Create proper PrintCommandDto for debt payment receipt
            var printCommand = new PrintCommandDto
            {
                CommandId = Guid.NewGuid().ToString(),
                Receipt = new ReceiptDto
                {
                    ReceiptNumber = $"PAY-{payment.Id}",
                    BranchName = tenant?.Name ?? "KasserPro Store",
                    Date = DateTime.Now,
                    CustomerName = payment.CustomerName ?? "",
                    PaymentMethod = payment.PaymentMethod.ToString(),
                    CashierName = payment.RecordedByUserName ?? userName,
                    // For debt payment receipt - show only the 3 amounts we need
                    NetTotal = payment.BalanceBefore, // المجموع (الدين الكلي قبل الدفع)
                    TaxAmount = 0, // No tax for debt payment
                    TotalAmount = payment.BalanceBefore, // Same as NetTotal (no tax)
                    AmountPaid = payment.Amount, // المبلغ المدفوع
                    ChangeAmount = 0,
                    AmountDue = payment.BalanceAfter, // المتبقي على العميل
                    Items = new List<ReceiptItemDto>() // Empty - debt payment uses special format
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

            // Send to specific branch group using PrintReceipt (not PrintDebtPaymentReceipt)
            // This ensures it's handled by the standard print handler
            await _hubContext.Clients.Group(branchGroup)
                .SendAsync("PrintReceipt", printCommand);

            // Also send to default group as fallback
            if (branchGroup != "branch-default")
            {
                await _hubContext.Clients.Group("branch-default")
                    .SendAsync("PrintReceipt", printCommand);
            }

            _logger.LogInformation("Print command sent for debt payment {PaymentId} to branch group {BranchId}", paymentId, branchId);

            return Ok(new { Success = true, Message = "تم إرسال أمر الطباعة بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send print command for debt payment {PaymentId}", paymentId);
            return StatusCode(500, new { Success = false, Message = "فشل إرسال أمر الطباعة" });
        }
    }
}

/// <summary>
/// Request to get or create customer by phone
/// </summary>
public class GetOrCreateCustomerRequest
{
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
}

/// <summary>
/// Request for loyalty points operations
/// </summary>
public class LoyaltyPointsRequest
{
    public int Points { get; set; }
}
