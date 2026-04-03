using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Expenses;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasserPro.Application.Services.Implementations;

/// <summary>
/// Service implementation for Expense management
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashRegisterService _cashRegisterService;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICashRegisterService cashRegisterService,
        ILogger<ExpenseService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cashRegisterService = cashRegisterService;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedResult<ExpenseDto>>> GetAllAsync(
        int? categoryId = null,
        ExpenseStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? shiftId = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            var query = _unitOfWork.Expenses.Query()
                .Where(e => e.TenantId == _currentUserService.TenantId &&
                           e.BranchId == _currentUserService.BranchId)
                .Include(e => e.Category)
                .Include(e => e.Shift)
                .Include(e => e.Attachments)
                .AsQueryable();

            // Apply filters
            if (categoryId.HasValue)
                query = query.Where(e => e.CategoryId == categoryId.Value);

            if (status.HasValue)
                query = query.Where(e => e.Status == status.Value);

            if (fromDate.HasValue)
                query = query.Where(e => e.ExpenseDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.ExpenseDate <= toDate.Value);

            if (shiftId.HasValue)
                query = query.Where(e => e.ShiftId == shiftId.Value);

            // Order by date descending
            query = query.OrderByDescending(e => e.ExpenseDate);

            // Pagination
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = items.Select(MapToDto).ToList();

            var pagedResult = new PagedResult<ExpenseDto>(dtos, totalCount, pageNumber, pageSize);

            return ApiResponse<PagedResult<ExpenseDto>>.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            return ApiResponse<PagedResult<ExpenseDto>>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseDto>> GetByIdAsync(int id)
    {
        try
        {
            var expense = await _unitOfWork.Expenses.Query()
                .Where(e => e.Id == id &&
                           e.TenantId == _currentUserService.TenantId &&
                           e.BranchId == _currentUserService.BranchId)
                .Include(e => e.Category)
                .Include(e => e.Shift)
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync();

            if (expense == null)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_NOT_FOUND);

            return ApiResponse<ExpenseDto>.Ok(MapToDto(expense));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {Id}", id);
            return ApiResponse<ExpenseDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseDto>> CreateAsync(CreateExpenseRequest request)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Validate category exists
            var category = await _unitOfWork.ExpenseCategories.Query()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId &&
                                         c.TenantId == _currentUserService.TenantId &&
                                         c.IsActive);

            if (category == null)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_CATEGORY_NOT_FOUND);

            // Get user name
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            // Generate expense number
            var expenseNumber = await GenerateExpenseNumberAsync();

            // Get active shift
            var activeShift = await _unitOfWork.Shifts.Query()
                .FirstOrDefaultAsync(s => s.BranchId == _currentUserService.BranchId &&
                                         !s.IsClosed);

            // Create expense
            var expense = new Expense
            {
                TenantId = _currentUserService.TenantId,
                BranchId = _currentUserService.BranchId,
                ExpenseNumber = expenseNumber,
                CategoryId = request.CategoryId,
                Amount = request.Amount,
                ExpenseDate = request.ExpenseDate,
                Description = request.Description,
                Beneficiary = request.Beneficiary,
                ReferenceNumber = request.ReferenceNumber,
                Notes = request.Notes,
                Status = ExpenseStatus.Draft,
                ShiftId = activeShift?.Id,
                CreatedByUserId = _currentUserService.UserId,
                CreatedByUserName = user?.Name ?? _currentUserService.Email ?? "Unknown"
            };

            await _unitOfWork.Expenses.AddAsync(expense);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Reload with includes
            var createdExpense = await _unitOfWork.Expenses.Query()
                .Where(e => e.Id == expense.Id)
                .Include(e => e.Category)
                .Include(e => e.Shift)
                .Include(e => e.Attachments)
                .FirstAsync();

            return ApiResponse<ExpenseDto>.Ok(MapToDto(createdExpense));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error creating expense");
            return ApiResponse<ExpenseDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseDto>> UpdateAsync(int id, UpdateExpenseRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var expense = await _unitOfWork.Expenses.Query()
                .FirstOrDefaultAsync(e => e.Id == id &&
                                         e.TenantId == _currentUserService.TenantId &&
                                         e.BranchId == _currentUserService.BranchId);

            if (expense == null)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_NOT_FOUND);

            // Can only edit Draft expenses
            if (expense.Status != ExpenseStatus.Draft)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_NOT_EDITABLE);

            // Validate category
            var category = await _unitOfWork.ExpenseCategories.Query()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId &&
                                         c.TenantId == _currentUserService.TenantId &&
                                         c.IsActive);

            if (category == null)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_CATEGORY_NOT_FOUND);

            // Update expense
            expense.CategoryId = request.CategoryId;
            expense.Amount = request.Amount;
            expense.ExpenseDate = request.ExpenseDate;
            expense.Description = request.Description;
            expense.Beneficiary = request.Beneficiary;
            expense.ReferenceNumber = request.ReferenceNumber;
            expense.Notes = request.Notes;

            _unitOfWork.Expenses.Update(expense);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Reload with includes
            var updatedExpense = await _unitOfWork.Expenses.Query()
                .Where(e => e.Id == expense.Id)
                .Include(e => e.Category)
                .Include(e => e.Shift)
                .Include(e => e.Attachments)
                .FirstAsync();

            return ApiResponse<ExpenseDto>.Ok(MapToDto(updatedExpense));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error updating expense {Id}", id);
            return ApiResponse<ExpenseDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var expense = await _unitOfWork.Expenses.Query()
                .FirstOrDefaultAsync(e => e.Id == id &&
                                         e.TenantId == _currentUserService.TenantId &&
                                         e.BranchId == _currentUserService.BranchId);

            if (expense == null)
                return ApiResponse<bool>.Fail(ErrorCodes.EXPENSE_NOT_FOUND);

            // Can only delete Draft expenses
            if (expense.Status != ExpenseStatus.Draft)
                return ApiResponse<bool>.Fail(ErrorCodes.EXPENSE_NOT_DELETABLE);

            _unitOfWork.Expenses.Delete(expense);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error deleting expense {Id}", id);
            return ApiResponse<bool>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseDto>> ApproveAsync(int id, ApproveExpenseRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var expense = await _unitOfWork.Expenses.Query()
                .FirstOrDefaultAsync(e => e.Id == id &&
                                         e.TenantId == _currentUserService.TenantId &&
                                         e.BranchId == _currentUserService.BranchId);

            if (expense == null)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_NOT_FOUND);

            // Can only approve Draft expenses
            if (expense.Status != ExpenseStatus.Draft)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_ALREADY_APPROVED);

            // Get user name
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            // Update expense
            expense.Status = ExpenseStatus.Approved;
            expense.ApprovedByUserId = _currentUserService.UserId;
            expense.ApprovedByUserName = user?.Name ?? _currentUserService.Email ?? "Unknown";
            expense.ApprovedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.Notes))
                expense.Notes = string.IsNullOrEmpty(expense.Notes) ? request.Notes : $"{expense.Notes}\n{request.Notes}";

            _unitOfWork.Expenses.Update(expense);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Reload with includes
            var approvedExpense = await _unitOfWork.Expenses.Query()
                .Where(e => e.Id == expense.Id)
                .Include(e => e.Category)
                .Include(e => e.Shift)
                .Include(e => e.Attachments)
                .FirstAsync();

            return ApiResponse<ExpenseDto>.Ok(MapToDto(approvedExpense));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error approving expense {Id}", id);
            return ApiResponse<ExpenseDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseDto>> RejectAsync(int id, RejectExpenseRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var expense = await _unitOfWork.Expenses.Query()
                .FirstOrDefaultAsync(e => e.Id == id &&
                                         e.TenantId == _currentUserService.TenantId &&
                                         e.BranchId == _currentUserService.BranchId);

            if (expense == null)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_NOT_FOUND);

            // Can only reject Draft expenses
            if (expense.Status != ExpenseStatus.Draft)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_ALREADY_PROCESSED);

            // Get user name
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            // Update expense
            expense.Status = ExpenseStatus.Rejected;
            expense.RejectedByUserId = _currentUserService.UserId;
            expense.RejectedByUserName = user?.Name ?? _currentUserService.Email ?? "Unknown";
            expense.RejectedAt = DateTime.UtcNow;
            expense.RejectionReason = request.RejectionReason;

            _unitOfWork.Expenses.Update(expense);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Reload with includes
            var rejectedExpense = await _unitOfWork.Expenses.Query()
                .Where(e => e.Id == expense.Id)
                .Include(e => e.Category)
                .Include(e => e.Shift)
                .Include(e => e.Attachments)
                .FirstAsync();

            return ApiResponse<ExpenseDto>.Ok(MapToDto(rejectedExpense));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error rejecting expense {Id}", id);
            return ApiResponse<ExpenseDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseDto>> PayAsync(int id, PayExpenseRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var expense = await _unitOfWork.Expenses.Query()
                .FirstOrDefaultAsync(e => e.Id == id &&
                                         e.TenantId == _currentUserService.TenantId &&
                                         e.BranchId == _currentUserService.BranchId);

            if (expense == null)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_NOT_FOUND);

            // Can only pay Approved expenses
            if (expense.Status != ExpenseStatus.Approved)
                return ApiResponse<ExpenseDto>.Fail(ErrorCodes.EXPENSE_NOT_APPROVED);

            // Get user name
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

            // Update expense
            expense.Status = ExpenseStatus.Paid;
            expense.PaymentMethod = request.PaymentMethod;
            expense.PaymentDate = request.PaymentDate;
            expense.PaymentReferenceNumber = request.PaymentReferenceNumber;
            expense.PaidByUserId = _currentUserService.UserId;
            expense.PaidByUserName = user?.Name ?? _currentUserService.Email ?? "Unknown";
            expense.PaidAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.Notes))
                expense.Notes = string.IsNullOrEmpty(expense.Notes) ? request.Notes : $"{expense.Notes}\n{request.Notes}";

            _unitOfWork.Expenses.Update(expense);

            // If payment method is Cash, update cash register
            if (request.PaymentMethod == PaymentMethod.Cash)
            {
                var cashBalanceResponse = await _cashRegisterService.GetCurrentBalanceAsync(_currentUserService.BranchId);
                if (!cashBalanceResponse.Success)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return ApiResponse<ExpenseDto>.Fail(ErrorCodes.INTERNAL_ERROR);
                }

                if (cashBalanceResponse.Data!.CurrentBalance < expense.Amount)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return ApiResponse<ExpenseDto>.Fail(ErrorCodes.CASH_REGISTER_INSUFFICIENT_BALANCE);
                }

                await _cashRegisterService.RecordTransactionAsync(
                    CashRegisterTransactionType.Expense,
                    expense.Amount,
                    $"Expense: {expense.Description}",
                    "Expense",
                    expense.Id,
                    expense.ShiftId);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            // Reload with includes
            var paidExpense = await _unitOfWork.Expenses.Query()
                .Where(e => e.Id == expense.Id)
                .Include(e => e.Category)
                .Include(e => e.Shift)
                .Include(e => e.Attachments)
                .FirstAsync();

            return ApiResponse<ExpenseDto>.Ok(MapToDto(paidExpense));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error paying expense {Id}", id);
            return ApiResponse<ExpenseDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    private async Task<string> GenerateExpenseNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var lastExpense = await _unitOfWork.Expenses.Query()
            .Where(e => e.TenantId == _currentUserService.TenantId &&
                       e.ExpenseDate.Year == year)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastExpense != null)
        {
            // Extract number from last expense (EXP-2026-0001 -> 0001)
            var parts = lastExpense.ExpenseNumber.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"EXP-{year}-{nextNumber:D4}";
    }

    private ExpenseDto MapToDto(Expense expense)
    {
        return new ExpenseDto
        {
            Id = expense.Id,
            ExpenseNumber = expense.ExpenseNumber,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category?.Name ?? string.Empty,
            CategoryIcon = expense.Category?.Icon,
            CategoryColor = expense.Category?.Color,
            Amount = expense.Amount,
            ExpenseDate = expense.ExpenseDate,
            Description = expense.Description,
            Beneficiary = expense.Beneficiary,
            ReferenceNumber = expense.ReferenceNumber,
            Notes = expense.Notes,
            Status = expense.Status.ToString(),
            ShiftId = expense.ShiftId,
            ShiftNumber = expense.Shift?.Id.ToString(),
            PaymentMethod = expense.PaymentMethod?.ToString(),
            PaymentDate = expense.PaymentDate,
            PaymentReferenceNumber = expense.PaymentReferenceNumber,
            CreatedByUserName = expense.CreatedByUserName,
            ApprovedByUserName = expense.ApprovedByUserName,
            ApprovedAt = expense.ApprovedAt,
            PaidByUserName = expense.PaidByUserName,
            PaidAt = expense.PaidAt,
            RejectedByUserName = expense.RejectedByUserName,
            RejectedAt = expense.RejectedAt,
            RejectionReason = expense.RejectionReason,
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt,
            Attachments = expense.Attachments.Select(a => new ExpenseAttachmentDto
            {
                Id = a.Id,
                ExpenseId = a.ExpenseId,
                FileName = a.FileName,
                FilePath = a.FilePath,
                FileSize = a.FileSize,
                FileType = a.FileType,
                UploadedByUserName = a.UploadedByUserName,
                CreatedAt = a.CreatedAt
            }).ToList()
        };
    }
}
