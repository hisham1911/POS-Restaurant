namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentQueryService _paymentQueryService;

    public PaymentsController(IPaymentQueryService paymentQueryService) => _paymentQueryService = paymentQueryService;

    [HttpGet("order/{orderId}")]
    [HasPermission(Permission.OrdersView)]
    public async Task<IActionResult> GetByOrder(int orderId, CancellationToken cancellationToken)
    {
        var result = await _paymentQueryService.GetByOrderAsync(orderId, cancellationToken);
        return Ok(result);
    }
}
