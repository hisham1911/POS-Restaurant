using KasserPro.Application.DTOs.CashRegister;
using KasserPro.Application.DTOs.Common;
using KasserPro.Domain.Enums;

namespace KasserPro.Application.Services.Interfaces;

/// <summary>
/// Service interface for Cash Register management
/// </summary>
public interface ICashRegisterService
{
    /// <summary>
    /// Get current cash balance for a branch
    /// </summary>
    Task<ApiResponse<CashRegisterBalanceDto>> GetCurrentBalanceAsync(int branchId);
    
    /// <summary>
    /// Get cash register transactions with filtering and pagination
    /// </summary>
    Task<ApiResponse<PagedResult<CashRegisterTransactionDto>>> GetTransactionsAsync(
        int? branchId = null,
        CashRegisterTransactionType? type = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? shiftId = null,
        int pageNumber = 1,
        int pageSize = 20);
    
    /// <summary>
    /// Create a manual cash register transaction (Deposit/Withdrawal)
    /// </summary>
    Task<ApiResponse<CashRegisterTransactionDto>> CreateTransactionAsync(CreateCashRegisterTransactionRequest request);
    
    /// <summary>
    /// Reconcile cash register at shift close
    /// Creates adjustment transaction if variance exists
    /// </summary>
    Task<ApiResponse<bool>> ReconcileAsync(int shiftId, ReconcileCashRegisterRequest request);
    
    /// <summary>
    /// Transfer cash between branches
    /// Creates two linked transactions (Withdrawal + Deposit)
    /// </summary>
    Task<ApiResponse<bool>> TransferCashAsync(TransferCashRequest request);
    
    /// <summary>
    /// Get cash register summary for a date range
    /// </summary>
    Task<ApiResponse<CashRegisterSummaryDto>> GetSummaryAsync(
        int branchId,
        DateTime fromDate,
        DateTime toDate);
    
    /// <summary>
    /// Internal method: Record a cash transaction (called by other services)
    /// </summary>
    Task RecordTransactionAsync(
        CashRegisterTransactionType type,
        decimal amount,
        string description,
        string? referenceType = null,
        int? referenceId = null,
        int? shiftId = null,
        int? branchId = null);
}
