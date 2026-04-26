namespace KasserPro.API.Controllers;

using KasserPro.Application.DTOs.PurchaseInvoices;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class PurchaseInvoicesController : ControllerBase
{
    private readonly IPurchaseInvoiceService _purchaseInvoiceService;
    private readonly ILogger<PurchaseInvoicesController> _logger;

    public PurchaseInvoicesController(
        IPurchaseInvoiceService purchaseInvoiceService,
        ILogger<PurchaseInvoicesController> logger)
    {
        _purchaseInvoiceService = purchaseInvoiceService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? supplierId = null,
        [FromQuery] PurchaseInvoiceStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _purchaseInvoiceService.GetAllAsync(
            supplierId, status, fromDate, toDate, pageNumber, pageSize);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare([FromBody] CreatePurchaseInvoiceRequest request)
    {
        var result = await _purchaseInvoiceService.PrepareAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _purchaseInvoiceService.GetByIdAsync(id);
        
        if (!result.Success)
            return NotFound(result);
        
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseInvoiceRequest request)
    {
        var result = await _purchaseInvoiceService.CreateAsync(request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePurchaseInvoiceRequest request)
    {
        var result = await _purchaseInvoiceService.UpdateAsync(id, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _purchaseInvoiceService.DeleteAsync(id);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(int id)
    {
        var result = await _purchaseInvoiceService.ConfirmAsync(id);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelInvoiceRequest request)
    {
        var result = await _purchaseInvoiceService.CancelAsync(id, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }

    [HttpPost("{invoiceId}/payments")]
    public async Task<IActionResult> AddPayment(int invoiceId, [FromBody] AddPaymentRequest request)
    {
        var result = await _purchaseInvoiceService.AddPaymentAsync(invoiceId, request);
        
        if (!result.Success)
            return BadRequest(result);
        
        return CreatedAtAction(nameof(GetById), new { id = invoiceId }, result);
    }

    [HttpDelete("{invoiceId}/payments/{paymentId}")]
    public async Task<IActionResult> DeletePayment(int invoiceId, int paymentId)
    {
        var result = await _purchaseInvoiceService.DeletePaymentAsync(invoiceId, paymentId);
        
        if (!result.Success)
            return BadRequest(result);
        
        return Ok(result);
    }
}
