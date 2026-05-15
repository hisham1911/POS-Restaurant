namespace KasserPro.Application.Services.Implementations;

using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Wallets;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class WalletService : IWalletService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<WalletService> _logger;

    public WalletService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILogger<WalletService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ApiResponse<List<WalletDto>>> GetAllAsync(CancellationToken ct)
    {
        var wallets = await _unitOfWork.Wallets.Query().AsNoTracking()
            .Where(w => w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId)
            .OrderBy(w => w.Name).Select(w => MapToDto(w)).ToListAsync(ct);
        return ApiResponse<List<WalletDto>>.Ok(wallets);
    }

    public async Task<ApiResponse<List<WalletDto>>> GetActiveAsync(CancellationToken ct)
    {
        var wallets = await _unitOfWork.Wallets.Query().AsNoTracking()
            .Where(w => w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId && w.IsActive)
            .OrderBy(w => w.Type).ThenBy(w => w.Name).Select(w => MapToDto(w)).ToListAsync(ct);
        return ApiResponse<List<WalletDto>>.Ok(wallets);
    }

    public async Task<ApiResponse<WalletDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var wallet = await _unitOfWork.Wallets.Query().AsNoTracking()
            .Where(w => w.Id == id && w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId)
            .Select(w => MapToDto(w)).FirstOrDefaultAsync(ct);
        if (wallet == null)
            return ApiResponse<WalletDto>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));
        return ApiResponse<WalletDto>.Ok(wallet);
    }

    public async Task<ApiResponse<WalletDto>> CreateAsync(CreateWalletRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<WalletDto>.Fail(ErrorCodes.WALLET_NAME_REQUIRED, ErrorMessages.Get(ErrorCodes.WALLET_NAME_REQUIRED));
        var validTypes = new[] { "BankAccount", "Wallet" };
        if (!validTypes.Contains(request.Type))
            return ApiResponse<WalletDto>.Fail(ErrorCodes.WALLET_TYPE_INVALID, ErrorMessages.Get(ErrorCodes.WALLET_TYPE_INVALID));
        if (request.InitialBalance < 0)
            return ApiResponse<WalletDto>.Fail(ErrorCodes.WALLET_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.WALLET_INVALID_AMOUNT));

        var wallet = new Wallet
        {
            TenantId = _currentUser.TenantId, BranchId = _currentUser.BranchId,
            Name = request.Name.Trim(), AccountNumber = request.AccountNumber?.Trim(),
            Type = request.Type, CurrentBalance = request.InitialBalance,
            IsActive = true, Notes = request.Notes?.Trim()
        };
        await _unitOfWork.Wallets.AddAsync(wallet);
        if (request.InitialBalance > 0)
        {
            await _unitOfWork.WalletTransactions.AddAsync(new WalletTransaction
            {
                WalletId = wallet.Id, TenantId = _currentUser.TenantId, BranchId = _currentUser.BranchId,
                Type = "Deposit", Amount = request.InitialBalance, BalanceBefore = 0,
                BalanceAfter = request.InitialBalance, ReferenceType = "Manual",
                Description = "رصيد افتتاحي", UserId = _currentUser.UserId,
                UserName = _currentUser.Email ?? "Unknown", CreatedAt = DateTime.UtcNow
            });
        }
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<WalletDto>.Ok(MapToDto(wallet), "تم إنشاء المحفظة بنجاح");
    }

    public async Task<ApiResponse<WalletDto>> UpdateAsync(int id, UpdateWalletRequest request, CancellationToken ct)
    {
        var wallet = await _unitOfWork.Wallets.Query()
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId, ct);
        if (wallet == null)
            return ApiResponse<WalletDto>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<WalletDto>.Fail(ErrorCodes.WALLET_NAME_REQUIRED, ErrorMessages.Get(ErrorCodes.WALLET_NAME_REQUIRED));
        if (!request.IsActive && wallet.CurrentBalance > 0)
            return ApiResponse<WalletDto>.Fail(ErrorCodes.WALLET_HAS_BALANCE, ErrorMessages.Get(ErrorCodes.WALLET_HAS_BALANCE));

        wallet.Name = request.Name.Trim();
        wallet.AccountNumber = request.AccountNumber?.Trim();
        wallet.IsActive = request.IsActive;
        wallet.Notes = request.Notes?.Trim();
        wallet.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Wallets.Update(wallet);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<WalletDto>.Ok(MapToDto(wallet), "تم تحديث المحفظة بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var wallet = await _unitOfWork.Wallets.Query()
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId, ct);
        if (wallet == null)
            return ApiResponse<bool>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));
        if (wallet.CurrentBalance != 0)
            return ApiResponse<bool>.Fail(ErrorCodes.WALLET_HAS_BALANCE, ErrorMessages.Get(ErrorCodes.WALLET_HAS_BALANCE));
        if (await _unitOfWork.WalletTransactions.Query().AnyAsync(t => t.WalletId == id, ct))
            return ApiResponse<bool>.Fail(ErrorCodes.WALLET_HAS_TRANSACTIONS, ErrorMessages.Get(ErrorCodes.WALLET_HAS_TRANSACTIONS));

        wallet.IsDeleted = true;
        wallet.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Wallets.Update(wallet);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "تم حذف المحفظة بنجاح");
    }

    public async Task<ApiResponse<WalletTransactionDto>> DepositAsync(int walletId, WalletDepositWithdrawRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
            return ApiResponse<WalletTransactionDto>.Fail(ErrorCodes.WALLET_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.WALLET_INVALID_AMOUNT));
        var wallet = await _unitOfWork.Wallets.Query()
            .FirstOrDefaultAsync(w => w.Id == walletId && w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId && w.IsActive, ct);
        if (wallet == null)
            return ApiResponse<WalletTransactionDto>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var balanceBefore = wallet.CurrentBalance;
            wallet.CurrentBalance = Math.Round(wallet.CurrentBalance + request.Amount, 2);
            wallet.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Wallets.Update(wallet);

            var tx = new WalletTransaction
            {
                WalletId = walletId, TenantId = _currentUser.TenantId, BranchId = _currentUser.BranchId,
                Type = "Deposit", Amount = request.Amount, BalanceBefore = balanceBefore,
                BalanceAfter = wallet.CurrentBalance, ReferenceType = "Manual",
                Description = request.Description ?? "إيداع يدوي",
                UserId = _currentUser.UserId, UserName = _currentUser.Email ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.WalletTransactions.AddAsync(tx);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return ApiResponse<WalletTransactionDto>.Ok(MapTransactionToDto(tx, wallet.Name), "تم الإيداع بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error depositing to wallet {WalletId}", walletId);
            throw;
        }
    }

    public async Task<ApiResponse<WalletTransactionDto>> WithdrawAsync(int walletId, WalletDepositWithdrawRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
            return ApiResponse<WalletTransactionDto>.Fail(ErrorCodes.WALLET_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.WALLET_INVALID_AMOUNT));
        var wallet = await _unitOfWork.Wallets.Query()
            .FirstOrDefaultAsync(w => w.Id == walletId && w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId && w.IsActive, ct);
        if (wallet == null)
            return ApiResponse<WalletTransactionDto>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));
        if (request.Amount > wallet.CurrentBalance)
            return ApiResponse<WalletTransactionDto>.Fail(ErrorCodes.WALLET_INSUFFICIENT_BALANCE, ErrorMessages.Get(ErrorCodes.WALLET_INSUFFICIENT_BALANCE));

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var balanceBefore = wallet.CurrentBalance;
            wallet.CurrentBalance = Math.Round(wallet.CurrentBalance - request.Amount, 2);
            wallet.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Wallets.Update(wallet);

            var tx = new WalletTransaction
            {
                WalletId = walletId, TenantId = _currentUser.TenantId, BranchId = _currentUser.BranchId,
                Type = "Withdrawal", Amount = request.Amount, BalanceBefore = balanceBefore,
                BalanceAfter = wallet.CurrentBalance, ReferenceType = "Manual",
                Description = request.Description ?? "سحب يدوي",
                UserId = _currentUser.UserId, UserName = _currentUser.Email ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.WalletTransactions.AddAsync(tx);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return ApiResponse<WalletTransactionDto>.Ok(MapTransactionToDto(tx, wallet.Name), "تم السحب بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error withdrawing from wallet {WalletId}", walletId);
            throw;
        }
    }

    public async Task<ApiResponse<PagedWalletTransactions>> GetTransactionsAsync(int walletId, WalletTransactionFilters filters, CancellationToken ct)
    {
        var walletExists = await _unitOfWork.Wallets.Query()
            .AnyAsync(w => w.Id == walletId && w.TenantId == _currentUser.TenantId && w.BranchId == _currentUser.BranchId, ct);
        if (!walletExists)
            return ApiResponse<PagedWalletTransactions>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));

        var query = _unitOfWork.WalletTransactions.Query().AsNoTracking()
            .Where(t => t.WalletId == walletId);

        if (!string.IsNullOrWhiteSpace(filters.Type))
            query = query.Where(t => t.Type == filters.Type);
        if (filters.DateFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= filters.DateFrom.Value);
        if (filters.DateTo.HasValue)
            query = query.Where(t => t.CreatedAt < filters.DateTo.Value.AddDays(1));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.CreatedAt)
            .Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize)
            .Select(t => MapTransactionToDto(t, t.Wallet.Name))
            .ToListAsync(ct);

        return ApiResponse<PagedWalletTransactions>.Ok(new PagedWalletTransactions
        {
            Items = items, TotalCount = total, Page = filters.Page,
            PageSize = filters.PageSize, TotalPages = (int)Math.Ceiling((double)total / filters.PageSize)
        });
    }

    public async Task RecordOrderPaymentAsync(int walletId, decimal amount, int orderId, string orderNumber, string? referenceNumber, int userId, string userName, CancellationToken ct)
    {
        if (amount <= 0)
            return;

        var wallet = await _unitOfWork.Wallets.Query()
            .FirstOrDefaultAsync(w => w.Id == walletId
                                   && w.TenantId == _currentUser.TenantId
                                   && w.BranchId == _currentUser.BranchId
                                   && w.IsActive
                                   && !w.IsDeleted, ct);
        if (wallet == null) return;

        var balanceBefore = wallet.CurrentBalance;
        wallet.CurrentBalance = Math.Round(wallet.CurrentBalance + amount, 2);
        wallet.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Wallets.Update(wallet);

        await _unitOfWork.WalletTransactions.AddAsync(new WalletTransaction
        {
            WalletId = walletId, TenantId = wallet.TenantId, BranchId = wallet.BranchId,
            Type = "OrderPayment", Amount = amount, BalanceBefore = balanceBefore,
            BalanceAfter = wallet.CurrentBalance, ReferenceType = "Order",
            ReferenceId = orderId, ReferenceNumber = referenceNumber,
            Description = $"دفع طلب رقم {orderNumber}",
            UserId = userId, UserName = userName, CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<ApiResponse<bool>> RecordOrderRefundAsync(int walletId, decimal amount, int returnOrderId, string originalOrderNumber, string? referenceNumber, int userId, string userName, CancellationToken ct)
    {
        if (amount <= 0)
            return ApiResponse<bool>.Fail(ErrorCodes.WALLET_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.WALLET_INVALID_AMOUNT));

        var wallet = await _unitOfWork.Wallets.Query()
            .FirstOrDefaultAsync(w => w.Id == walletId
                                   && w.TenantId == _currentUser.TenantId
                                   && w.BranchId == _currentUser.BranchId
                                   && w.IsActive
                                   && !w.IsDeleted, ct);
        if (wallet == null)
            return ApiResponse<bool>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));

        if (wallet.CurrentBalance < amount)
            return ApiResponse<bool>.Fail(ErrorCodes.WALLET_INSUFFICIENT_BALANCE, ErrorMessages.Get(ErrorCodes.WALLET_INSUFFICIENT_BALANCE));

        var balanceBefore = wallet.CurrentBalance;
        wallet.CurrentBalance = Math.Round(wallet.CurrentBalance - amount, 2);
        wallet.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Wallets.Update(wallet);

        await _unitOfWork.WalletTransactions.AddAsync(new WalletTransaction
        {
            WalletId = walletId, TenantId = wallet.TenantId, BranchId = wallet.BranchId,
            Type = "OrderRefund", Amount = amount, BalanceBefore = balanceBefore,
            BalanceAfter = wallet.CurrentBalance, ReferenceType = "OrderRefund",
            ReferenceId = returnOrderId, ReferenceNumber = referenceNumber,
            Description = $"Ø§Ø³ØªØ±Ø¬Ø§Ø¹ Ø·Ù„Ø¨ Ø±Ù‚Ù… {originalOrderNumber}",
            UserId = userId, UserName = userName, CreatedAt = DateTime.UtcNow
        });

        return ApiResponse<bool>.Ok(true);
    }

    private static WalletDto MapToDto(Wallet w) => new()
    {
        Id = w.Id, Name = w.Name, AccountNumber = w.AccountNumber, Type = w.Type,
        CurrentBalance = w.CurrentBalance, IsActive = w.IsActive, Notes = w.Notes, CreatedAt = w.CreatedAt
    };

    private static WalletTransactionDto MapTransactionToDto(WalletTransaction t, string walletName) => new()
    {
        Id = t.Id, WalletId = t.WalletId, WalletName = walletName, Type = t.Type,
        Amount = t.Amount, BalanceBefore = t.BalanceBefore, BalanceAfter = t.BalanceAfter,
        ReferenceType = t.ReferenceType, ReferenceId = t.ReferenceId, ReferenceNumber = t.ReferenceNumber,
        Description = t.Description, UserName = t.UserName, CreatedAt = t.CreatedAt
    };
}
