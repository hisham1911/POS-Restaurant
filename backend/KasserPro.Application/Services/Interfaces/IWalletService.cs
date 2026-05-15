namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Wallets;

public interface IWalletService
{
    Task<ApiResponse<List<WalletDto>>> GetAllAsync(CancellationToken ct);
    Task<ApiResponse<List<WalletDto>>> GetActiveAsync(CancellationToken ct);
    Task<ApiResponse<WalletDto>> GetByIdAsync(int id, CancellationToken ct);
    Task<ApiResponse<WalletDto>> CreateAsync(CreateWalletRequest request, CancellationToken ct);
    Task<ApiResponse<WalletDto>> UpdateAsync(int id, UpdateWalletRequest request, CancellationToken ct);
    Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct);
    Task<ApiResponse<WalletTransactionDto>> DepositAsync(int walletId, WalletDepositWithdrawRequest request, CancellationToken ct);
    Task<ApiResponse<WalletTransactionDto>> WithdrawAsync(int walletId, WalletDepositWithdrawRequest request, CancellationToken ct);
    Task<ApiResponse<PagedWalletTransactions>> GetTransactionsAsync(int walletId, WalletTransactionFilters filters, CancellationToken ct);

    Task RecordOrderPaymentAsync(int walletId, decimal amount, int orderId, string orderNumber, string? referenceNumber, int userId, string userName, CancellationToken ct);
    Task<ApiResponse<bool>> RecordOrderRefundAsync(int walletId, decimal amount, int returnOrderId, string originalOrderNumber, string? referenceNumber, int userId, string userName, CancellationToken ct);
}
