namespace KasserPro.Application.Services.Implementations;

using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

public class PaymentQueryService : IPaymentQueryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public PaymentQueryService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<PaymentDto>>> GetByOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var query = _unitOfWork.Payments.Query()
            .AsNoTracking()
            .Where(p => p.OrderId == orderId && p.TenantId == tenantId);

        if (branchId > 0)
        {
            query = query.Where(p => p.BranchId == branchId);
        }

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                Method = p.Method.ToString(),
                Amount = p.Amount,
                Reference = p.Reference,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<PaymentDto>>.Ok(payments);
    }
}
