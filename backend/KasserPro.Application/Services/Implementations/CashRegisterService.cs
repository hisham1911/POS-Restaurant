using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.CashRegister;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace KasserPro.Application.Services.Implementations;

/// <summary>
/// Service implementation for Cash Register management
/// </summary>
public class CashRegisterService : ICashRegisterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CashRegisterService> _logger;

    public CashRegisterService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CashRegisterService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<CashRegisterBalanceDto>> GetCurrentBalanceAsync(int branchId)
    {
        try
        {
            // Get last transaction for this branch
            var lastTransaction = await _unitOfWork.CashRegisterTransactions.Query()
                .Where(t => t.TenantId == _currentUserService.TenantId && t.BranchId == branchId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            // Get branch info
            var branch = await _unitOfWork.Branches.Query()
                .FirstOrDefaultAsync(b => b.Id == branchId && b.TenantId == _currentUserService.TenantId);

            if (branch == null)
                return ApiResponse<CashRegisterBalanceDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, ErrorMessages.Get(ErrorCodes.BRANCH_NOT_FOUND));

            // Get active shift
            var activeShift = await _unitOfWork.Shifts.Query()
                .FirstOrDefaultAsync(s => s.BranchId == branchId && !s.IsClosed);

            var balance = new CashRegisterBalanceDto
            {
                BranchId = branchId,
                BranchName = branch.Name,
                CurrentBalance = lastTransaction?.BalanceAfter ?? 0,
                LastTransactionDate = lastTransaction?.TransactionDate ?? DateTime.UtcNow,
                ActiveShiftId = activeShift?.Id,
                ActiveShiftNumber = activeShift?.Id.ToString()
            };

            return ApiResponse<CashRegisterBalanceDto>.Ok(balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash register balance for branch {BranchId}", branchId);
            return ApiResponse<CashRegisterBalanceDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<PagedResult<CashRegisterTransactionDto>>> GetTransactionsAsync(
        int? branchId = null,
        CashRegisterTransactionType? type = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? shiftId = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            // Use current user's branch if not specified
            var targetBranchId = branchId ?? _currentUserService.BranchId;

            var query = _unitOfWork.CashRegisterTransactions.Query()
                .Where(t => t.TenantId == _currentUserService.TenantId &&
                           t.BranchId == targetBranchId)
                .Include(t => t.Shift)
                .AsQueryable();

            // Apply filters
            if (type.HasValue)
                query = query.Where(t => t.Type == type.Value);

            if (fromDate.HasValue)
                query = query.Where(t => t.TransactionDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(t => t.TransactionDate <= toDate.Value);

            if (shiftId.HasValue)
                query = query.Where(t => t.ShiftId == shiftId.Value);

            // Order by date descending
            query = query.OrderByDescending(t => t.TransactionDate);

            // Pagination
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = items.Select(MapToDto).ToList();

            var pagedResult = new PagedResult<CashRegisterTransactionDto>(dtos, totalCount, pageNumber, pageSize);

            return ApiResponse<PagedResult<CashRegisterTransactionDto>>.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash register transactions");
            return ApiResponse<PagedResult<CashRegisterTransactionDto>>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<CashRegisterTransactionDto>> CreateTransactionAsync(CreateCashRegisterTransactionRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Validate transaction type (only Deposit or Withdrawal allowed for manual transactions)
            if (request.Type != CashRegisterTransactionType.Deposit &&
                request.Type != CashRegisterTransactionType.Withdrawal)
            {
                return ApiResponse<CashRegisterTransactionDto>.Fail(ErrorCodes.CASH_REGISTER_INVALID_TYPE);
            }

            // Get current balance
            var currentBalance = await GetCurrentBalanceForBranchAsync(_currentUserService.BranchId);

            // Check if withdrawal would result in negative balance
            if (request.Type == CashRegisterTransactionType.Withdrawal)
            {
                var tenant = await _unitOfWork.Tenants.Query()
                    .FirstOrDefaultAsync(t => t.Id == _currentUserService.TenantId);

                if (tenant != null && !tenant.AllowNegativeStock && currentBalance < request.Amount)
                {
                    return ApiResponse<CashRegisterTransactionDto>.Fail(ErrorCodes.CASH_REGISTER_INSUFFICIENT_BALANCE);
                }
            }

            // Get active shift
            var activeShift = await _unitOfWork.Shifts.Query()
                .FirstOrDefaultAsync(s => s.BranchId == _currentUserService.BranchId && !s.IsClosed);

            // Get user name
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            // Generate transaction number
            var transactionNumber = await GenerateTransactionNumberAsync();

            // Calculate new balance
            var balanceBefore = currentBalance;
            var balanceAfter = request.Type == CashRegisterTransactionType.Deposit
                ? balanceBefore + request.Amount
                : balanceBefore - request.Amount;

            // Create transaction
            var cashTransaction = new CashRegisterTransaction
            {
                TenantId = _currentUserService.TenantId,
                BranchId = _currentUserService.BranchId,
                TransactionNumber = transactionNumber,
                Type = request.Type,
                Amount = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                TransactionDate = request.TransactionDate,
                Description = request.Description,
                ShiftId = activeShift?.Id,
                UserId = _currentUserService.UserId,
                UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
            };

            await _unitOfWork.CashRegisterTransactions.AddAsync(cashTransaction);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Reload with includes
            var createdTransaction = await _unitOfWork.CashRegisterTransactions.Query()
                .Where(t => t.Id == cashTransaction.Id)
                .Include(t => t.Shift)
                .FirstAsync();

            return ApiResponse<CashRegisterTransactionDto>.Ok(MapToDto(createdTransaction));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error creating cash register transaction");
            return ApiResponse<CashRegisterTransactionDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<bool>> ReconcileAsync(int shiftId, ReconcileCashRegisterRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var shift = await _unitOfWork.Shifts.Query()
                .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == _currentUserService.TenantId);

            if (shift == null)
                return ApiResponse<bool>.Fail(ErrorCodes.SHIFT_NOT_FOUND);

            // Can only reconcile open shifts
            if (shift.IsClosed)
                return ApiResponse<bool>.Fail(ErrorCodes.SHIFT_NOT_OPEN);

            // Get user name
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            // Calculate expected balance from cash register transactions
            var expectedBalance = await CalculateExpectedBalanceAsync(shift.BranchId, shift.OpenedAt);

            // Calculate variance
            var variance = request.ActualBalance - expectedBalance;

            // Update shift
            shift.ClosingBalance = request.ActualBalance;
            shift.ExpectedBalance = expectedBalance;
            shift.Difference = variance;
            shift.IsReconciled = true;
            shift.ReconciledByUserId = _currentUserService.UserId;
            shift.ReconciledByUserName = user?.Name ?? _currentUserService.Email ?? "Unknown";
            shift.ReconciledAt = DateTime.UtcNow;
            shift.VarianceReason = request.VarianceReason;

            _unitOfWork.Shifts.Update(shift);

            // If there's a variance, create an adjustment transaction
            if (variance != 0)
            {
                var transactionNumber = await GenerateTransactionNumberAsync();
                var currentBalance = await GetCurrentBalanceForBranchAsync(shift.BranchId);

                var adjustmentTransaction = new CashRegisterTransaction
                {
                    TenantId = _currentUserService.TenantId,
                    BranchId = shift.BranchId,
                    TransactionNumber = transactionNumber,
                    Type = CashRegisterTransactionType.Adjustment,
                    Amount = Math.Abs(variance),
                    BalanceBefore = currentBalance,
                    BalanceAfter = request.ActualBalance,
                    TransactionDate = DateTime.UtcNow,
                    Description = $"Reconciliation adjustment: {request.VarianceReason ?? "No reason provided"}",
                    ReferenceType = "Shift",
                    ReferenceId = shiftId,
                    ShiftId = shiftId,
                    UserId = _currentUserService.UserId,
                    UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
                };

                await _unitOfWork.CashRegisterTransactions.AddAsync(adjustmentTransaction);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error reconciling cash register for shift {ShiftId}", shiftId);
            return ApiResponse<bool>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<bool>> TransferCashAsync(TransferCashRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Validate branches
            if (request.SourceBranchId == request.TargetBranchId)
                return ApiResponse<bool>.Fail(ErrorCodes.CASH_REGISTER_SAME_BRANCH);

            var sourceBranch = await _unitOfWork.Branches.Query()
                .FirstOrDefaultAsync(b => b.Id == request.SourceBranchId && b.TenantId == _currentUserService.TenantId);

            var targetBranch = await _unitOfWork.Branches.Query()
                .FirstOrDefaultAsync(b => b.Id == request.TargetBranchId && b.TenantId == _currentUserService.TenantId);

            if (sourceBranch == null || targetBranch == null)
                return ApiResponse<bool>.Fail(ErrorCodes.BRANCH_NOT_FOUND, ErrorMessages.Get(ErrorCodes.BRANCH_NOT_FOUND));

            // Check source balance
            var sourceBalance = await GetCurrentBalanceForBranchAsync(request.SourceBranchId);
            if (sourceBalance < request.Amount)
                return ApiResponse<bool>.Fail(ErrorCodes.CASH_REGISTER_INSUFFICIENT_BALANCE);

            // Get user name
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            // Get active shifts
            var sourceShift = await _unitOfWork.Shifts.Query()
                .FirstOrDefaultAsync(s => s.BranchId == request.SourceBranchId && !s.IsClosed);

            var targetShift = await _unitOfWork.Shifts.Query()
                .FirstOrDefaultAsync(s => s.BranchId == request.TargetBranchId && !s.IsClosed);

            // Create withdrawal transaction (source)
            var withdrawalNumber = await GenerateTransactionNumberAsync();
            var withdrawalTransaction = new CashRegisterTransaction
            {
                TenantId = _currentUserService.TenantId,
                BranchId = request.SourceBranchId,
                TransactionNumber = withdrawalNumber,
                Type = CashRegisterTransactionType.Transfer,
                Amount = request.Amount,
                BalanceBefore = sourceBalance,
                BalanceAfter = sourceBalance - request.Amount,
                TransactionDate = request.TransactionDate,
                Description = $"Transfer to {targetBranch.Name}: {request.Description}",
                ShiftId = sourceShift?.Id,
                UserId = _currentUserService.UserId,
                UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
            };

            await _unitOfWork.CashRegisterTransactions.AddAsync(withdrawalTransaction);
            await _unitOfWork.SaveChangesAsync(); // Flush to get withdrawal ID for FK reference

            // Create deposit transaction (target)
            var targetBalance = await GetCurrentBalanceForBranchAsync(request.TargetBranchId);
            var depositNumber = await GenerateTransactionNumberAsync();
            var depositTransaction = new CashRegisterTransaction
            {
                TenantId = _currentUserService.TenantId,
                BranchId = request.TargetBranchId,
                TransactionNumber = depositNumber,
                Type = CashRegisterTransactionType.Transfer,
                Amount = request.Amount,
                BalanceBefore = targetBalance,
                BalanceAfter = targetBalance + request.Amount,
                TransactionDate = request.TransactionDate,
                Description = $"Transfer from {sourceBranch.Name}: {request.Description}",
                ShiftId = targetShift?.Id,
                TransferReferenceId = withdrawalTransaction.Id,
                UserId = _currentUserService.UserId,
                UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
            };

            await _unitOfWork.CashRegisterTransactions.AddAsync(depositTransaction);

            // Link withdrawal → deposit (bidirectional reference)
            withdrawalTransaction.TransferReferenceId = depositTransaction.Id;
            _unitOfWork.CashRegisterTransactions.Update(withdrawalTransaction);

            // Single final flush before commit
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error transferring cash");
            return ApiResponse<bool>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<CashRegisterSummaryDto>> GetSummaryAsync(
        int branchId,
        DateTime fromDate,
        DateTime toDate)
    {
        try
        {
            var transactions = await _unitOfWork.CashRegisterTransactions.Query()
                .Where(t => t.TenantId == _currentUserService.TenantId &&
                           t.BranchId == branchId &&
                           t.TransactionDate >= fromDate &&
                           t.TransactionDate <= toDate)
                .ToListAsync();

            // Get opening balance (balance before first transaction in period)
            var firstTransaction = transactions.OrderBy(t => t.TransactionDate).FirstOrDefault();
            var openingBalance = firstTransaction?.BalanceBefore ?? 0;

            // Get closing balance (balance after last transaction in period)
            var lastTransaction = transactions.OrderByDescending(t => t.TransactionDate).FirstOrDefault();
            var closingBalance = lastTransaction?.BalanceAfter ?? openingBalance;

            var summary = new CashRegisterSummaryDto
            {
                OpeningBalance = openingBalance,
                ClosingBalance = closingBalance,
                TotalDeposits = transactions.Where(t => t.Type == CashRegisterTransactionType.Deposit).Sum(t => t.Amount),
                TotalWithdrawals = transactions.Where(t => t.Type == CashRegisterTransactionType.Withdrawal).Sum(t => t.Amount),
                TotalSales = transactions.Where(t => t.Type == CashRegisterTransactionType.Sale).Sum(t => t.Amount),
                TotalRefunds = transactions.Where(t => t.Type == CashRegisterTransactionType.Refund).Sum(t => t.Amount),
                TotalExpenses = transactions.Where(t => t.Type == CashRegisterTransactionType.Expense).Sum(t => t.Amount),
                TotalSupplierPayments = transactions.Where(t => t.Type == CashRegisterTransactionType.SupplierPayment).Sum(t => t.Amount),
                TotalAdjustments = transactions.Where(t => t.Type == CashRegisterTransactionType.Adjustment).Sum(t => t.Amount),
                TotalTransfersIn = transactions.Where(t => t.Type == CashRegisterTransactionType.Transfer && t.Amount > 0).Sum(t => t.Amount),
                TotalTransfersOut = transactions.Where(t => t.Type == CashRegisterTransactionType.Transfer && t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                TransactionCount = transactions.Count,
                FromDate = fromDate,
                ToDate = toDate
            };

            return ApiResponse<CashRegisterSummaryDto>.Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash register summary");
            return ApiResponse<CashRegisterSummaryDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task RecordTransactionAsync(
        CashRegisterTransactionType type,
        decimal amount,
        string description,
        string? referenceType = null,
        int? referenceId = null,
        int? shiftId = null)
    {
        // P0-8: If we're already inside a caller's transaction (e.g., CompleteAsync),
        // piggyback on it. If not, create our own to ensure read+write atomicity.
        var hasExistingTransaction = _unitOfWork.HasActiveTransaction;
        var ownsTransaction = !hasExistingTransaction;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
        {
            transaction = await _unitOfWork.BeginTransactionAsync();
        }

        try
        {
            // Read current balance — inside transaction, so SQLite write lock protects us
            var currentBalance = await GetCurrentBalanceForBranchAsync(_currentUserService.BranchId);

            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            var transactionNumber = await GenerateTransactionNumberAsync();

            var balanceAfter = type switch
            {
                CashRegisterTransactionType.Sale => currentBalance + amount,
                CashRegisterTransactionType.Deposit => currentBalance + amount,
                CashRegisterTransactionType.Opening => amount,
                CashRegisterTransactionType.Refund => currentBalance - amount,
                CashRegisterTransactionType.Withdrawal => currentBalance - amount,
                CashRegisterTransactionType.Expense => currentBalance - amount,
                CashRegisterTransactionType.SupplierPayment => currentBalance - amount,
                CashRegisterTransactionType.Adjustment => currentBalance + amount,
                CashRegisterTransactionType.ShiftClose => amount, // P3: ShiftClose sets final balance
                _ => currentBalance
            };

            var cashTransaction = new CashRegisterTransaction
            {
                TenantId = _currentUserService.TenantId,
                BranchId = _currentUserService.BranchId,
                TransactionNumber = transactionNumber,
                Type = type,
                Amount = amount,
                BalanceBefore = currentBalance,
                BalanceAfter = balanceAfter,
                TransactionDate = DateTime.UtcNow,
                Description = description,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                ShiftId = shiftId,
                UserId = _currentUserService.UserId,
                UserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
            };

            await _unitOfWork.CashRegisterTransactions.AddAsync(cashTransaction);

            // CRITICAL: Only save and commit if we own the transaction
            // When called as sub-service, parent will SaveChanges+Commit for all changes
            if (ownsTransaction)
            {
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
            }
            // else: NO SaveChangesAsync - parent will save

            _logger.LogInformation("Cash register transaction recorded: {Type} - {Amount}", type, amount);
        }
        catch (Exception ex)
        {
            // CRITICAL: Only rollback if we own the transaction
            if (ownsTransaction)
            {
                try { await _unitOfWork.RollbackTransactionAsync(); } catch { /* Already rolled back */ }
            }
            _logger.LogError(ex, "Error recording cash register transaction");
            throw;
        }
    }

    private async Task<decimal> GetCurrentBalanceForBranchAsync(int branchId)
    {
        var lastTransaction = await _unitOfWork.CashRegisterTransactions.Query()
            .Where(t => t.BranchId == branchId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        return lastTransaction?.BalanceAfter ?? 0;
    }

    private async Task<decimal> CalculateExpectedBalanceAsync(int branchId, DateTime fromDate)
    {
        var transactions = await _unitOfWork.CashRegisterTransactions.Query()
            .Where(t => t.BranchId == branchId && t.TransactionDate >= fromDate)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();

        if (!transactions.Any())
            return 0;

        // Return the balance after the last transaction
        return transactions.Last().BalanceAfter;
    }

    private async Task<string> GenerateTransactionNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var lastTransaction = await _unitOfWork.CashRegisterTransactions.Query()
            .Where(t => t.TenantId == _currentUserService.TenantId &&
                       t.TransactionDate.Year == year)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastTransaction != null)
        {
            // Extract number from last transaction (CR-2026-0001 -> 0001)
            var parts = lastTransaction.TransactionNumber.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"CR-{year}-{nextNumber:D4}";
    }

    private CashRegisterTransactionDto MapToDto(CashRegisterTransaction transaction)
    {
        return new CashRegisterTransactionDto
        {
            Id = transaction.Id,
            TransactionNumber = transaction.TransactionNumber,
            Type = transaction.Type.ToString(),
            Amount = transaction.Amount,
            BalanceBefore = transaction.BalanceBefore,
            BalanceAfter = transaction.BalanceAfter,
            TransactionDate = transaction.TransactionDate,
            Description = transaction.Description,
            ReferenceType = transaction.ReferenceType,
            ReferenceId = transaction.ReferenceId,
            ShiftId = transaction.ShiftId,
            ShiftNumber = transaction.Shift?.Id.ToString(),
            TransferReferenceId = transaction.TransferReferenceId,
            UserName = transaction.UserName,
            CreatedAt = transaction.CreatedAt
        };
    }
}
