namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;

public interface IPaymentQueryService
{
    Task<ApiResponse<List<PaymentDto>>> GetByOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
