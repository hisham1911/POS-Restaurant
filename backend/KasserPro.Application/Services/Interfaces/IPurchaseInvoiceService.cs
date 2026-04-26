namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.PurchaseInvoices;
using KasserPro.Domain.Enums;

public interface IPurchaseInvoiceService
{
    // CRUD Operations
    Task<ApiResponse<PagedResult<PurchaseInvoiceDto>>> GetAllAsync(
        int? supplierId = null,
        PurchaseInvoiceStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 20);

    Task<ApiResponse<PurchaseInvoicePreviewDto>> PrepareAsync(CreatePurchaseInvoiceRequest request);
    
    Task<ApiResponse<PurchaseInvoiceDto>> GetByIdAsync(int id);
    
    Task<ApiResponse<PurchaseInvoiceDto>> CreateAsync(CreatePurchaseInvoiceRequest request);
    
    Task<ApiResponse<PurchaseInvoiceDto>> UpdateAsync(int id, UpdatePurchaseInvoiceRequest request);
    
    Task<ApiResponse<bool>> DeleteAsync(int id);
    
    // State Transitions
    Task<ApiResponse<PurchaseInvoiceDto>> ConfirmAsync(int id);
    
    Task<ApiResponse<PurchaseInvoiceDto>> CancelAsync(int id, CancelInvoiceRequest request);
    
    // Payments
    Task<ApiResponse<PurchaseInvoicePaymentDto>> AddPaymentAsync(int invoiceId, AddPaymentRequest request);
    
    Task<ApiResponse<bool>> DeletePaymentAsync(int invoiceId, int paymentId);
}
