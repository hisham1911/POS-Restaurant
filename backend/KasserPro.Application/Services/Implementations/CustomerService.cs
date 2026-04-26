namespace KasserPro.Application.Services.Implementations;

using KasserPro.Application.Common;
using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Customers;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

/// <summary>
/// Service for customer management operations
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICashRegisterService _cashRegisterService;

    public CustomerService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, ICashRegisterService cashRegisterService)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _cashRegisterService = cashRegisterService;
    }

    public async Task<PagedResult<CustomerDto>> GetAllAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var tenantId = _currentUser.TenantId;

        var query = _unitOfWork.Customers.Query()
            .Where(c => c.TenantId == tenantId && c.IsActive);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                c.Phone.Contains(search) ||
                (c.Name != null && c.Name.ToLower().Contains(searchLower)) ||
                (c.Email != null && c.Email.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync();
        // SQLite doesn't support Sum on decimal - cast to double first
        var totalSpentAmount = Math.Round(
            (decimal)(await query.SumAsync(c => (double?)c.TotalSpent) ?? 0.0),
            2);
        var totalDueAmount = Math.Round(
            (decimal)(await query.SumAsync(c => (double?)c.TotalDue) ?? 0.0),
            2);

        var customers = await query
            .OrderByDescending(c => c.LastOrderAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync();

        return new PagedResult<CustomerDto>(
            customers,
            totalCount,
            page,
            pageSize,
            totalSpentAmount: totalSpentAmount,
            totalDueAmount: totalDueAmount);
    }

    public async Task<CustomerDto?> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        return customer == null ? null : MapToDto(customer);
    }

    public async Task<CustomerDto?> GetByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var tenantId = _currentUser.TenantId;
        var normalizedPhone = NormalizePhone(phone);

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Phone == normalizedPhone && c.TenantId == tenantId);

        return customer == null ? null : MapToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            throw new ArgumentException("Phone number is required", nameof(request.Phone));

        var tenantId = _currentUser.TenantId;
        var normalizedPhone = NormalizePhone(request.Phone);

        // Check for duplicate phone
        var existingCustomer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Phone == normalizedPhone && c.TenantId == tenantId);

        if (existingCustomer != null)
            throw new InvalidOperationException($"Customer with phone {request.Phone} already exists");

        var customer = new Customer
        {
            TenantId = tenantId,
            Phone = normalizedPhone,
            Name = request.Name,
            Email = request.Email,
            Address = request.Address,
            Notes = request.Notes,
            IsActive = true,
            LoyaltyPoints = 0,
            TotalOrders = 0,
            TotalSpent = 0
        };

        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(customer);
    }

    public async Task<CustomerDto?> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        if (customer == null)
            return null;

        // Update only provided fields
        if (request.Name != null)
            customer.Name = request.Name;
        if (request.Email != null)
            customer.Email = request.Email;
        if (request.Address != null)
            customer.Address = request.Address;
        if (request.Notes != null)
            customer.Notes = request.Notes;
        if (request.IsActive.HasValue)
        {
            if (!request.IsActive.Value && customer.TotalDue > 0)
                throw new ArgumentException("لا يمكن تعطيل عميل لديه ديون مستحقة");

            customer.IsActive = request.IsActive.Value;
        }
        if (request.CreditLimit.HasValue)
            customer.CreditLimit = request.CreditLimit.Value;

        await _unitOfWork.SaveChangesAsync();

        return MapToDto(customer);
    }

    public async Task<(CustomerDto Customer, bool WasCreated)> GetOrCreateByPhoneAsync(string phone, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone number is required", nameof(phone));

        var existingCustomer = await GetByPhoneAsync(phone);
        if (existingCustomer != null)
            return (existingCustomer, false);

        // Auto-create customer
        var newCustomer = await CreateAsync(new CreateCustomerRequest
        {
            Phone = phone,
            Name = name
        });

        return (newCustomer, true);
    }

    /// <summary>
    /// Update customer order statistics.
    /// CRITICAL: NO transaction management - participates in parent transaction.
    /// Parent service (OrderService) is responsible for SaveChanges and Commit.
    /// </summary>
    public async Task UpdateOrderStatsAsync(int customerId, decimal orderTotal, int loyaltyPoints = 0)
    {
        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

        if (customer == null)
            return;

        // Update order statistics
        customer.TotalOrders++;
        customer.TotalSpent += orderTotal;
        customer.LastOrderAt = DateTime.UtcNow;

        // Update loyalty points (can be positive or negative)
        if (loyaltyPoints != 0)
        {
            customer.LoyaltyPoints += loyaltyPoints;
            // Ensure points don't go below zero
            if (customer.LoyaltyPoints < 0)
                customer.LoyaltyPoints = 0;
        }

        // NO SaveChangesAsync - parent will save
        // NO Commit - parent will commit
    }

    /// <summary>
    /// Deduct customer stats on refund.
    /// CRITICAL: NO transaction management - participates in parent transaction (OrderService.RefundAsync).
    /// Parent service is responsible for SaveChanges and Commit.
    /// </summary>
    public async Task DeductRefundStatsAsync(int customerId, decimal refundAmount, int pointsToDeduct, bool isFullRefund = false)
    {
        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

        if (customer == null)
            return;

        // Deduct from TotalSpent (don't go below zero)
        customer.TotalSpent -= refundAmount;
        if (customer.TotalSpent < 0)
            customer.TotalSpent = 0;

        // Deduct loyalty points (don't go below zero)
        customer.LoyaltyPoints -= pointsToDeduct;
        if (customer.LoyaltyPoints < 0)
            customer.LoyaltyPoints = 0;

        // FIX M-3: Decrement TotalOrders on full refund
        if (isFullRefund && customer.TotalOrders > 0)
            customer.TotalOrders--;

        // NO SaveChangesAsync - parent will save
        // NO Commit - parent will commit
    }

    /// <summary>
    /// Update customer credit balance (add to TotalDue).
    /// CRITICAL: NO transaction management - participates in parent transaction.
    /// Parent service (OrderService) is responsible for SaveChanges and Commit.
    /// </summary>
    public async Task UpdateCreditBalanceAsync(int customerId, decimal amountDue)
    {
        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

        if (customer == null)
            return;

        // Add to TotalDue (customer owes more money)
        customer.TotalDue += amountDue;
        await UpsertBranchBalanceAsync(customerId, amountDue);

        // NO SaveChangesAsync - parent will save
        // NO Commit - parent will commit
    }

    public async Task<bool> ValidateCreditLimitAsync(int customerId, decimal additionalAmount)
    {
        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

        if (customer == null)
            return false;

        // If credit limit is 0, no limit is enforced
        if (customer.CreditLimit == 0)
            return true;

        var branchBalance = await _unitOfWork.CustomerBranchBalances.Query()
            .Where(b => b.CustomerId == customerId
                     && b.BranchId == _currentUser.BranchId
                     && b.TenantId == tenantId)
            .AsNoTracking()
            .Select(b => (decimal?)b.AmountDue)
            .FirstOrDefaultAsync() ?? 0m;

        // Check if adding this amount would exceed the credit limit for the current branch
        var newTotalDue = branchBalance + additionalAmount;
        return newTotalDue <= customer.CreditLimit;
    }

    public async Task AddLoyaltyPointsAsync(int customerId, int points)
    {
        if (points <= 0) return;

        var tenantId = _currentUser.TenantId;

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var customer = await _unitOfWork.Customers.Query()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

            if (customer == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return;
            }

            customer.LoyaltyPoints += points;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> RedeemLoyaltyPointsAsync(int customerId, int points)
    {
        if (points <= 0) return false;

        var tenantId = _currentUser.TenantId;

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var customer = await _unitOfWork.Customers.Query()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

            if (customer == null || customer.LoyaltyPoints < points)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return false;
            }

            customer.LoyaltyPoints -= points;
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse<PayDebtResponse>> PayDebtAsync(int customerId, PayDebtRequest request, int recordedByUserId)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var actingUserId = _currentUser.UserId;

        var currentShift = await _unitOfWork.Shifts.Query()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                                   && s.BranchId == branchId
                                   && s.UserId == actingUserId
                                   && !s.IsClosed);

        if (currentShift == null)
        {
            return ApiResponse<PayDebtResponse>.Fail(
                ErrorCodes.NO_OPEN_SHIFT,
                "لا يمكن تسجيل سداد دين بدون وردية مفتوحة");
        }

        // Validate amount
        if (request.Amount <= 0)
            return ApiResponse<PayDebtResponse>.Fail(
                ErrorCodes.PAYMENT_INVALID_AMOUNT,
                ErrorMessages.Get(ErrorCodes.PAYMENT_INVALID_AMOUNT));

        var referenceValidation = ValidateReferenceForNonCashPayment(
            request.PaymentMethod,
            request.ReferenceNumber);
        if (!referenceValidation.Success)
        {
            return ApiResponse<PayDebtResponse>.Fail(
                ErrorCodes.PAYMENT_REFERENCE_REQUIRED,
                referenceValidation.Message!);
        }

        // Get user for snapshot (filter by TenantId for multi-tenancy)
        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == recordedByUserId && u.TenantId == tenantId);
        if (user == null)
            return ApiResponse<PayDebtResponse>.Fail(
                ErrorCodes.USER_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

        // BEGIN TRANSACTION - SQLite will acquire EXCLUSIVE lock on first write
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // FIX: Read customer INSIDE transaction with fresh data protected by lock
            var customer = await _unitOfWork.Customers.Query()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

            if (customer == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return ApiResponse<PayDebtResponse>.Fail(
                    ErrorCodes.CUSTOMER_NOT_FOUND,
                    ErrorMessages.Get(ErrorCodes.CUSTOMER_NOT_FOUND));
            }

            // FIX: Validate with fresh data inside transaction
            if (request.Amount > customer.TotalDue)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return ApiResponse<PayDebtResponse>.Fail(
                    ErrorCodes.PAYMENT_EXCEEDS_DUE,
                    ErrorMessages.Get(ErrorCodes.PAYMENT_EXCEEDS_DUE));
            }

            // Calculate balance with fresh data
            var balanceBefore = customer.TotalDue;
            var balanceAfter = balanceBefore - request.Amount;

            // Create debt payment record
            var debtPayment = new DebtPayment
            {
                TenantId = tenantId,
                BranchId = branchId,
                CustomerId = customerId,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                ReferenceNumber = request.ReferenceNumber,
                Notes = request.Notes,
                RecordedByUserId = recordedByUserId,
                RecordedByUserName = user.Name,
                ShiftId = currentShift.Id,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter
            };

            await _unitOfWork.DebtPayments.AddAsync(debtPayment);

            // Reduce customer's TotalDue
            customer.TotalDue = balanceAfter;
            await UpsertBranchBalanceAsync(customerId, -request.Amount);

            await _unitOfWork.SaveChangesAsync();

            // INTEGRATION: Record in cash register if payment method is Cash
            if (request.PaymentMethod == Domain.Enums.PaymentMethod.Cash)
            {
                await _cashRegisterService.RecordTransactionAsync(
                    type: Domain.Enums.CashRegisterTransactionType.Sale, // Debt payment is income
                    amount: request.Amount,
                    description: $"تسديد دين - عميل: {customer.Name ?? customer.Phone}",
                    referenceType: "DebtPayment",
                    referenceId: debtPayment.Id,
                    shiftId: currentShift.Id
                );
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            var response = new PayDebtResponse
            {
                PaymentId = debtPayment.Id,
                AmountPaid = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                RemainingDebt = balanceAfter,
                Message = balanceAfter == 0
                    ? "تم تسديد الدين بالكامل"
                    : $"تم تسديد {request.Amount:F2} ج.م - المتبقي: {balanceAfter:F2} ج.م"
            };

            return ApiResponse<PayDebtResponse>.Ok(response, response.Message);
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return ApiResponse<PayDebtResponse>.Fail(
                ErrorCodes.SYSTEM_INTERNAL_ERROR,
                ErrorMessages.Get(ErrorCodes.SYSTEM_INTERNAL_ERROR));
        }
    }

    public async Task<DebtPaymentDto?> GetDebtPaymentByIdAsync(int paymentId, int tenantId)
    {
        var payment = await _unitOfWork.DebtPayments.Query()
            .Include(dp => dp.Customer)
            .FirstOrDefaultAsync(dp => dp.Id == paymentId && dp.TenantId == tenantId);

        if (payment == null)
            return null;

        return new DebtPaymentDto
        {
            Id = payment.Id,
            CustomerId = payment.CustomerId,
            CustomerName = payment.Customer?.Name,
            CustomerPhone = payment.Customer?.Phone,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            ReferenceNumber = payment.ReferenceNumber,
            Notes = payment.Notes,
            RecordedByUserId = payment.RecordedByUserId,
            RecordedByUserName = payment.RecordedByUserName,
            ShiftId = payment.ShiftId,
            BalanceBefore = payment.BalanceBefore,
            BalanceAfter = payment.BalanceAfter,
            CreatedAt = payment.CreatedAt
        };
    }

    private static (bool Success, string? Message) ValidateReferenceForNonCashPayment(
        PaymentMethod method,
        string? reference)
    {
        if (method == PaymentMethod.Cash)
        {
            return (true, null);
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return (false, "رقم المعاملة مطلوب لطرق الدفع غير النقدية");
        }

        return (true, null);
    }

    public async Task<List<DebtPaymentDto>> GetDebtPaymentHistoryAsync(int customerId)
    {
        var tenantId = _currentUser.TenantId;

        var payments = await _unitOfWork.DebtPayments.Query()
            .Where(dp => dp.CustomerId == customerId && dp.TenantId == tenantId)
            .OrderByDescending(dp => dp.CreatedAt)
            .Select(dp => new DebtPaymentDto
            {
                Id = dp.Id,
                CustomerId = dp.CustomerId,
                Amount = dp.Amount,
                PaymentMethod = dp.PaymentMethod,
                ReferenceNumber = dp.ReferenceNumber,
                Notes = dp.Notes,
                RecordedByUserId = dp.RecordedByUserId,
                RecordedByUserName = dp.RecordedByUserName,
                BalanceBefore = dp.BalanceBefore,
                BalanceAfter = dp.BalanceAfter,
                CreatedAt = dp.CreatedAt
            })
            .ToListAsync();

        return payments;
    }

    public async Task<List<CustomerDto>> GetCustomersWithDebtAsync()
    {
        var tenantId = _currentUser.TenantId;

        var customers = await _unitOfWork.Customers.Query()
            .Where(c => c.TenantId == tenantId && c.IsActive && c.TotalDue > 0)
            .OrderByDescending(c => c.TotalDue)
            .Select(c => MapToDto(c))
            .ToListAsync();

        return customers;
    }

    /// <summary>
    /// Reduce customer credit balance (subtract from TotalDue).
    /// CRITICAL: NO transaction management - participates in parent transaction (OrderService.RefundAsync / CancelAsync).
    /// Parent service is responsible for SaveChanges and Commit.
    /// </summary>
    public async Task ReduceCreditBalanceAsync(int customerId, decimal amountToReduce)
    {
        if (amountToReduce <= 0) return;

        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);

        if (customer == null)
            return;

        // Reduce TotalDue (don't go below zero)
        customer.TotalDue -= amountToReduce;
        if (customer.TotalDue < 0)
            customer.TotalDue = 0;
        await UpsertBranchBalanceAsync(customerId, -amountToReduce);

        // NO SaveChangesAsync - parent will save
        // NO Commit - parent will commit
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;

        var customer = await _unitOfWork.Customers.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        if (customer == null)
            return ApiResponse<bool>.Fail(ErrorCodes.CUSTOMER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CUSTOMER_NOT_FOUND));

        var hasOpenOrders = await _unitOfWork.Orders.Query()
            .AnyAsync(o => o.TenantId == tenantId
                        && !o.IsDeleted
                        && o.CustomerId == customer.Id
                        && o.Status != OrderStatus.Completed
                        && o.Status != OrderStatus.Cancelled);

        if (customer.TotalDue > 0 || hasOpenOrders)
            return ApiResponse<bool>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "لا يمكن حذف هذا العميل. لديه رصيد مديون أو طلبات غير مكتملة. يرجى تسوية الديون وإغلاق جميع الطلبات أولاً.");

        // Soft delete (using both IsActive and IsDeleted for compatibility)
        customer.IsActive = false;
        customer.IsDeleted = true;

        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "تم حذف العميل بنجاح");
    }

    #region Private Methods

    private async Task UpsertBranchBalanceAsync(int customerId, decimal delta)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var balance = await _unitOfWork.CustomerBranchBalances.Query()
            .FirstOrDefaultAsync(b => b.CustomerId == customerId
                                   && b.BranchId == branchId
                                   && b.TenantId == tenantId);

        if (balance == null)
        {
            balance = new CustomerBranchBalance
            {
                CustomerId = customerId,
                BranchId = branchId,
                TenantId = tenantId,
                AmountDue = 0m
            };

            await _unitOfWork.CustomerBranchBalances.AddAsync(balance);
        }

        balance.AmountDue = Math.Round(balance.AmountDue + delta, 2);
        if (balance.AmountDue < 0)
        {
            balance.AmountDue = 0m;
        }
    }

    private static string NormalizePhone(string phone)
    {
        // Remove spaces, dashes, and other common separators
        return phone.Replace(" ", "")
                   .Replace("-", "")
                   .Replace("(", "")
                   .Replace(")", "")
                   .Trim();
    }

    private static CustomerDto MapToDto(Customer c) => new()
    {
        Id = c.Id,
        Phone = c.Phone,
        Name = c.Name,
        Email = c.Email,
        Address = c.Address,
        Notes = c.Notes,
        LoyaltyPoints = c.LoyaltyPoints,
        TotalOrders = c.TotalOrders,
        TotalSpent = c.TotalSpent,
        LastOrderAt = c.LastOrderAt,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        TotalDue = c.TotalDue,
        CreditLimit = c.CreditLimit
    };

    #endregion
}
