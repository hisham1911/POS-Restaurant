namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Customers;

/// <summary>
/// Service for customer management operations
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Get all customers with pagination
    /// </summary>
    Task<PagedResult<CustomerDto>> GetAllAsync(int page = 1, int pageSize = 20, string? search = null);

    /// <summary>
    /// Get customer by ID
    /// </summary>
    Task<CustomerDto?> GetByIdAsync(int id);

    /// <summary>
    /// Get customer by phone number
    /// </summary>
    Task<CustomerDto?> GetByPhoneAsync(string phone);

    /// <summary>
    /// Create a new customer
    /// </summary>
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request);

    /// <summary>
    /// Update an existing customer
    /// </summary>
    Task<CustomerDto?> UpdateAsync(int id, UpdateCustomerRequest request);

    /// <summary>
    /// Get or create customer by phone (auto-create if not exists)
    /// </summary>
    /// <param name="phone">Phone number</param>
    /// <param name="name">Optional name for new customer</param>
    /// <returns>Customer info and whether it was newly created</returns>
    Task<(CustomerDto Customer, bool WasCreated)> GetOrCreateByPhoneAsync(string phone, string? name = null);

    /// <summary>
    /// Update customer stats after order completion (stats + loyalty points)
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="orderTotal">Order total amount</param>
    /// <param name="loyaltyPoints">Points to add (positive) or deduct (negative)</param>
    Task UpdateOrderStatsAsync(int customerId, decimal orderTotal, int loyaltyPoints = 0);

    /// <summary>
    /// Deduct stats when a refund is processed. Optionally decrements TotalOrders on full refund.
    /// </summary>
    Task DeductRefundStatsAsync(int customerId, decimal refundAmount, int pointsToDeduct, bool isFullRefund = false);

    /// <summary>
    /// Update customer credit balance (TotalDue) when order has unpaid amount
    /// </summary>
    Task UpdateCreditBalanceAsync(int customerId, decimal amountDue);

    /// <summary>
    /// Validate if customer can take additional credit without exceeding limit
    /// </summary>
    Task<bool> ValidateCreditLimitAsync(int customerId, decimal additionalAmount);

    /// <summary>
    /// Add loyalty points to customer
    /// </summary>
    Task AddLoyaltyPointsAsync(int customerId, int points);

    /// <summary>
    /// Redeem loyalty points from customer
    /// </summary>
    Task<bool> RedeemLoyaltyPointsAsync(int customerId, int points);

    /// <summary>
    /// Record a debt payment from customer (reduces TotalDue)
    /// </summary>
    Task<ApiResponse<PayDebtResponse>> PayDebtAsync(int customerId, PayDebtRequest request, int recordedByUserId);

    /// <summary>
    /// Get debt payment by ID
    /// </summary>
    Task<DebtPaymentDto?> GetDebtPaymentByIdAsync(int paymentId, int tenantId);

    /// <summary>
    /// Get debt payment history for a customer
    /// </summary>
    Task<List<DebtPaymentDto>> GetDebtPaymentHistoryAsync(int customerId);

    /// <summary>
    /// Get all customers with outstanding debt
    /// </summary>
    Task<List<CustomerDto>> GetCustomersWithDebtAsync();

    /// <summary>
    /// Reduce customer credit balance (used for refunds/cancellations)
    /// </summary>
    Task ReduceCreditBalanceAsync(int customerId, decimal amountToReduce);

    /// <summary>
    /// Soft delete a customer
    /// </summary>
    Task<bool> DeleteAsync(int id);
}
