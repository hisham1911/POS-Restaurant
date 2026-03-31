namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Shifts;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class ShiftService : IShiftService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICashRegisterService _cashRegisterService;

    public ShiftService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, ICashRegisterService cashRegisterService)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _cashRegisterService = cashRegisterService;
    }

    public async Task<ApiResponse<ShiftDto>> GetCurrentAsync(int userId)
    {
        // Validate userId
        if (userId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.USER_NOT_FOUND, "معرف المستخدم غير صالح");

        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        // SECURITY: Validate tenant context
        if (tenantId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.TENANT_NOT_FOUND, "سياق المستأجر غير صالح");

        // Query shift for this user in the current branch with Orders and Payments eager loaded
        var shift = await _unitOfWork.Shifts.Query()
            .Include(s => s.User)
            .Include(s => s.Orders.OrderByDescending(o => o.CreatedAt))
                .ThenInclude(o => o.Payments)
            .FirstOrDefaultAsync(s => s.UserId == userId
                                   && s.TenantId == tenantId
                                   && s.BranchId == branchId
                                   && !s.IsClosed);

        // Return success with null data if no shift is open (not an error)
        if (shift == null)
            return ApiResponse<ShiftDto>.Ok(null!, "لا توجد وردية مفتوحة");

        return ApiResponse<ShiftDto>.Ok(MapToDto(shift));
    }

    public async Task<ApiResponse<ShiftDto>> OpenAsync(OpenShiftRequest request, int userId)
    {
        // Validate userId
        if (userId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.USER_NOT_FOUND, "معرف المستخدم غير صالح");

        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        // SECURITY: Validate TenantId and BranchId
        if (tenantId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.TENANT_NOT_FOUND, "معرف المستأجر غير صالح");

        if (branchId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, "معرف الفرع غير صالح");

        // Verify user exists
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

        // Verify branch exists
        var branch = await _unitOfWork.Branches.GetByIdAsync(branchId);
        if (branch == null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, ErrorMessages.Get(ErrorCodes.BRANCH_NOT_FOUND));

        // Check for existing open shift for this user in this branch
        var existingShift = await _unitOfWork.Shifts.Query()
            .FirstOrDefaultAsync(s => s.UserId == userId
                                   && s.TenantId == tenantId
                                   && s.BranchId == branchId
                                   && !s.IsClosed);

        if (existingShift != null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_ALREADY_OPEN, "يوجد وردية مفتوحة بالفعل في هذا الفرع");

        // Use transaction for atomicity - Shift + Cash Register Opening
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            var shift = new Shift
            {
                TenantId = tenantId,
                BranchId = branchId,
                UserId = userId,
                OpeningBalance = Math.Round(request.OpeningBalance, 2),
                OpenedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow, // Initialize activity tracking
                IsClosed = false
            };

            await _unitOfWork.Shifts.AddAsync(shift);
            await _unitOfWork.SaveChangesAsync();

            // INTEGRATION: Create Opening cash register transaction
            await _cashRegisterService.RecordTransactionAsync(
                type: CashRegisterTransactionType.Opening,
                amount: shift.OpeningBalance,
                description: $"فتح وردية - {user.Name}",
                referenceType: "Shift",
                referenceId: shift.Id,
                shiftId: shift.Id
            );

            await _unitOfWork.CommitTransactionAsync();

            shift.User = user;

            return ApiResponse<ShiftDto>.Ok(MapToDto(shift), "تم فتح الوردية بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"حدث خطأ أثناء فتح الوردية: {ex.Message}");
        }
    }

    /// <summary>
    /// Close a shift with optimistic concurrency control.
    /// Uses RowVersion to prevent race conditions when multiple requests try to close the same shift.
    ///
    /// Scenario: If two admins click "Close Shift" at the exact same millisecond:
    /// - First request: Loads shift with RowVersion = 0x00000001
    /// - Second request: Loads shift with RowVersion = 0x00000001
    /// - First request: Saves successfully, RowVersion becomes 0x00000002
    /// - Second request: Fails with DbUpdateConcurrencyException (RowVersion mismatch)
    /// </summary>
    public async Task<ApiResponse<ShiftDto>> CloseAsync(CloseShiftRequest request, int userId)
    {
        // Validate userId
        if (userId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.USER_NOT_FOUND, "معرف المستخدم غير صالح");

        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        // SECURITY: Validate tenant context
        if (tenantId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.TENANT_NOT_FOUND, "سياق المستأجر غير صالح");

        // Use transaction for atomicity
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            var shift = await _unitOfWork.Shifts.Query()
                .Include(s => s.User)
                .Include(s => s.Orders).ThenInclude(o => o.Payments)
                .FirstOrDefaultAsync(s => s.UserId == userId
                                       && s.TenantId == tenantId
                                       && s.BranchId == branchId
                                       && !s.IsClosed);

            if (shift == null)
                return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_NOT_FOUND, "لا توجد وردية مفتوحة");

            // Double-check: Ensure shift is still open (race condition protection)
            if (shift.IsClosed)
                return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_ALREADY_CLOSED, "الوردية مغلقة بالفعل");

            // INTEGRATION: Validate reconciliation before closing
            // Get current cash balance from cash register
            var balanceResponse = await _cashRegisterService.GetCurrentBalanceAsync(branchId);
            if (!balanceResponse.Success)
                return ApiResponse<ShiftDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                    "فشل الحصول على رصيد الخزينة");

            var currentCashBalance = balanceResponse.Data!.CurrentBalance;

            // FIX C-2/C-3/H-8: Use unified helper for 100% parity with ForceCloseAsync
            var (totalOrders, totalCash, totalCard, _, _) = CalculateShiftFinancials(shift.Orders);
            shift.TotalOrders = totalOrders;
            shift.TotalCash = totalCash;
            shift.TotalCard = totalCard;

            shift.ClosingBalance = Math.Round(request.ClosingBalance, 2);
            shift.ExpectedBalance = Math.Round(currentCashBalance, 2); // Use cash register balance
            shift.Difference = Math.Round(shift.ClosingBalance - shift.ExpectedBalance, 2);
            shift.ClosedAt = DateTime.UtcNow;
            shift.IsClosed = true;
            shift.Notes = request.Notes;

            _unitOfWork.Shifts.Update(shift);

            // This will throw DbUpdateConcurrencyException if RowVersion doesn't match
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<ShiftDto>.Ok(MapToDto(shift), "تم إغلاق الوردية بنجاح");
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request already closed this shift
            await _unitOfWork.RollbackTransactionAsync();
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_CONCURRENCY_CONFLICT,
                "تم إغلاق الوردية بواسطة مستخدم آخر. يرجى تحديث الصفحة.");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"حدث خطأ أثناء إغلاق الوردية: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<ShiftDto>>> GetUserShiftsAsync(int userId)
    {
        // Validate userId
        if (userId <= 0)
            return ApiResponse<List<ShiftDto>>.Fail(ErrorCodes.USER_NOT_FOUND, "معرف المستخدم غير صالح");

        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        // SECURITY: Validate tenant context
        if (tenantId <= 0)
            return ApiResponse<List<ShiftDto>>.Fail(ErrorCodes.TENANT_NOT_FOUND, "سياق المستأجر غير صالح");

        var shifts = await _unitOfWork.Shifts.Query()
            .Include(s => s.User)
            .Where(s => s.UserId == userId && s.TenantId == tenantId && s.BranchId == branchId)
            .OrderByDescending(s => s.OpenedAt)
            .Take(30)
            .ToListAsync();

        return ApiResponse<List<ShiftDto>>.Ok(shifts.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Shift deletion is NOT supported for audit/financial integrity.
    /// Shifts contain financial records that must be preserved for accounting and legal compliance.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown - shifts cannot be deleted.</exception>
    public Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        throw new NotSupportedException("حذف الورديات غير مسموح به للحفاظ على سلامة السجلات المالية");
    }

    /// <summary>
    /// Force close a shift (Admin only)
    /// </summary>
    public async Task<ApiResponse<ShiftDto>> ForceCloseAsync(int shiftId, ForceCloseShiftRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var currentUserId = _currentUser.UserId;

        // Get current user info
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId);
        if (currentUser == null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.USER_NOT_FOUND, "المستخدم الحالي غير موجود");

        // Validate request
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_FORCE_CLOSE_REASON_REQUIRED, "سبب الإغلاق مطلوب");

        // Get shift with user info
        var shift = await _unitOfWork.Shifts.Query()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId && s.BranchId == branchId);

        if (shift == null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_NOT_FOUND, "الوردية غير موجودة");

        if (shift.IsClosed)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_ALREADY_CLOSED, "الوردية مغلقة بالفعل");

        if (shift.IsForceClosed)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_ALREADY_FORCE_CLOSED, "تم إغلاق الوردية بالقوة مسبقاً");

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // FIX C-2/C-3/H-8: Use unified helper for 100% parity with CloseAsync
            var completedOrders = await _unitOfWork.Orders.Query()
                .Include(o => o.Payments)
                .Where(o => o.ShiftId == shiftId && (o.Status == OrderStatus.Completed
                    || o.Status == OrderStatus.PartiallyRefunded
                    || o.Status == OrderStatus.Refunded))
                .ToListAsync();

            var (totalOrders, totalCash, totalCard, _, _) = CalculateShiftFinancials(completedOrders);

            // Set closing values
            shift.ClosingBalance = request.ActualBalance ?? (shift.OpeningBalance + totalCash);
            shift.ExpectedBalance = shift.OpeningBalance + totalCash;
            shift.Difference = shift.ClosingBalance - shift.ExpectedBalance;
            shift.TotalCash = totalCash;
            shift.TotalCard = totalCard;
            shift.TotalOrders = totalOrders;
            shift.ClosedAt = DateTime.UtcNow;
            shift.IsClosed = true;
            shift.IsForceClosed = true;
            shift.ForceClosedByUserId = currentUserId;
            shift.ForceClosedByUserName = currentUser.Name;
            shift.ForceClosedAt = DateTime.UtcNow;
            shift.ForceCloseReason = request.Reason;
            shift.Notes = request.Notes ?? shift.Notes;

            _unitOfWork.Shifts.Update(shift);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<ShiftDto>.Ok(MapToDto(shift), "تم إغلاق الوردية بالقوة بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"حدث خطأ أثناء إغلاق الوردية بالقوة: {ex.Message}");
        }
    }

    /// <summary>
    /// Handover shift to another user
    /// </summary>
    public async Task<ApiResponse<ShiftDto>> HandoverAsync(int shiftId, HandoverShiftRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var currentUserId = _currentUser.UserId;

        // Get current user info
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId);
        if (currentUser == null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.USER_NOT_FOUND, "المستخدم الحالي غير موجود");

        // Validate request
        if (request.ToUserId <= 0)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_HANDOVER_USER_REQUIRED, "يجب اختيار المستخدم المستلم");

        if (request.ToUserId == currentUserId)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_HANDOVER_TO_SAME_USER, "لا يمكن تسليم الوردية لنفس المستخدم");

        // Get shift
        var shift = await _unitOfWork.Shifts.Query()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId && s.BranchId == branchId);

        if (shift == null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_NOT_FOUND, "الوردية غير موجودة");

        if (shift.IsClosed)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_CANNOT_HANDOVER_CLOSED, "لا يمكن تسليم وردية مغلقة");

        if (shift.IsHandedOver)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_ALREADY_HANDED_OVER, "تم تسليم الوردية مسبقاً");

        // Verify target user exists
        var targetUser = await _unitOfWork.Users.GetByIdAsync(request.ToUserId);
        if (targetUser == null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.USER_NOT_FOUND, "المستخدم المستلم غير موجود");

        // Check if target user has an open shift in this branch
        var existingShift = await _unitOfWork.Shifts.Query()
            .FirstOrDefaultAsync(s => s.UserId == request.ToUserId
                                   && s.TenantId == tenantId
                                   && s.BranchId == branchId
                                   && !s.IsClosed);

        if (existingShift != null)
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SHIFT_USER_HAS_OPEN_SHIFT,
                "المستخدم المستلم لديه وردية مفتوحة بالفعل");

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Record handover details
            shift.IsHandedOver = true;
            shift.HandedOverFromUserId = currentUserId;
            shift.HandedOverFromUserName = currentUser.Name;
            shift.HandedOverToUserId = request.ToUserId;
            shift.HandedOverToUserName = targetUser.Name;
            shift.HandedOverAt = DateTime.UtcNow;
            shift.HandoverBalance = request.CurrentBalance;
            shift.HandoverNotes = request.Notes;
            shift.LastActivityAt = DateTime.UtcNow;

            // Transfer shift to new user
            shift.UserId = request.ToUserId;

            _unitOfWork.Shifts.Update(shift);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<ShiftDto>.Ok(MapToDto(shift), "تم تسليم الوردية بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return ApiResponse<ShiftDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"حدث خطأ أثناء تسليم الوردية: {ex.Message}");
        }
    }

    /// <summary>
    /// Update last activity timestamp for a shift
    /// </summary>
    public async Task<ApiResponse<bool>> UpdateActivityAsync(int shiftId)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var shift = await _unitOfWork.Shifts.Query()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId && s.BranchId == branchId);

        if (shift == null)
            return ApiResponse<bool>.Fail(ErrorCodes.SHIFT_NOT_FOUND, "الوردية غير موجودة");

        if (shift.IsClosed)
            return ApiResponse<bool>.Ok(true, "الوردية مغلقة");

        shift.LastActivityAt = DateTime.UtcNow;
        _unitOfWork.Shifts.Update(shift);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    /// <summary>
    /// Get all active shifts in the current branch
    /// </summary>
    public async Task<ApiResponse<List<ShiftDto>>> GetActiveShiftsAsync()
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var shifts = await _unitOfWork.Shifts.Query()
            .Include(s => s.User)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Payments)
            .Where(s => s.TenantId == tenantId && s.BranchId == branchId && !s.IsClosed)
            .OrderBy(s => s.OpenedAt)
            .ToListAsync();

        return ApiResponse<List<ShiftDto>>.Ok(shifts.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get shift warnings for current user's shift
    /// </summary>
    public async Task<ApiResponse<ShiftWarningDto>> GetShiftWarningsAsync(int userId)
    {
        // Validate userId
        if (userId <= 0)
            return ApiResponse<ShiftWarningDto>.Fail(ErrorCodes.USER_NOT_FOUND, "معرف المستخدم غير صالح");

        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        // SECURITY: Validate tenant context
        if (tenantId <= 0)
            return ApiResponse<ShiftWarningDto>.Fail(ErrorCodes.TENANT_NOT_FOUND, "سياق المستأجر غير صالح");

        // Get current open shift
        var shift = await _unitOfWork.Shifts.Query()
            .FirstOrDefaultAsync(s => s.UserId == userId
                                   && s.TenantId == tenantId
                                   && s.BranchId == branchId
                                   && !s.IsClosed);

        // No open shift - no warnings
        if (shift == null)
        {
            return ApiResponse<ShiftWarningDto>.Ok(new ShiftWarningDto
            {
                Level = "None",
                Message = string.Empty,
                HoursOpen = 0,
                ShouldWarn = false,
                IsCritical = false,
                ShiftId = null
            });
        }

        // Calculate hours open
        var hoursOpen = (DateTime.UtcNow - shift.OpenedAt).TotalHours;

        // Determine warning level
        if (hoursOpen >= 24)
        {
            // Critical warning (24+ hours)
            return ApiResponse<ShiftWarningDto>.Ok(new ShiftWarningDto
            {
                Level = "Critical",
                Message = ErrorMessages.Get(ErrorCodes.SHIFT_CRITICAL_24_HOURS),
                HoursOpen = hoursOpen,
                ShouldWarn = true,
                IsCritical = true,
                ShiftId = shift.Id
            });
        }
        else if (hoursOpen >= 12)
        {
            // Standard warning (12+ hours)
            return ApiResponse<ShiftWarningDto>.Ok(new ShiftWarningDto
            {
                Level = "Warning",
                Message = ErrorMessages.Get(ErrorCodes.SHIFT_WARNING_12_HOURS),
                HoursOpen = hoursOpen,
                ShouldWarn = true,
                IsCritical = false,
                ShiftId = shift.Id
            });
        }

        // No warning needed
        return ApiResponse<ShiftWarningDto>.Ok(new ShiftWarningDto
        {
            Level = "None",
            Message = string.Empty,
            HoursOpen = hoursOpen,
            ShouldWarn = false,
            IsCritical = false,
            ShiftId = shift.Id
        });
    }

    /// <summary>
    /// FIX C-2/C-3/H-8: Single source of truth for shift financial calculations.
    /// Used by both CloseAsync and ForceCloseAsync to guarantee 100% parity.
    /// Includes Completed, PartiallyRefunded, and Refunded orders.
    /// TotalCard includes Card payments ONLY (excludes Fawry and BankTransfer).
    /// TotalFawry and TotalBankTransfer provide granular breakdown.
    /// </summary>
    private static (int TotalOrders, decimal TotalCash, decimal TotalCard, decimal TotalFawry, decimal TotalBankTransfer) CalculateShiftFinancials(
        IEnumerable<Order> orders)
    {
        var completedOrders = orders.Where(o =>
            o.Status == OrderStatus.Completed
            || o.Status == OrderStatus.PartiallyRefunded
            || o.Status == OrderStatus.Refunded).ToList();

        var salesOrders = completedOrders.Where(o => o.OrderType != OrderType.Return).ToList();
        var returnOrders = completedOrders.Where(o => o.OrderType == OrderType.Return).ToList();

        var salesPayments = salesOrders.SelectMany(o => o.Payments ?? Enumerable.Empty<Payment>()).ToList();
        var returnPayments = returnOrders.SelectMany(o => o.Payments ?? Enumerable.Empty<Payment>()).ToList();

        var totalCash = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)), 2);
        var totalCard = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount)), 2);
        var totalFawry = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.Fawry).Sum(p => p.Amount)), 2);
        var totalBankTransfer = Math.Round(
            salesPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount)
            - Math.Abs(returnPayments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount)), 2);

        return (salesOrders.Count, totalCash, totalCard, totalFawry, totalBankTransfer);
    }

    private static ShiftDto MapToDto(Shift shift)
    {
        // FIX C-2/C-3/H-8: Use unified helper for DTO calculation too
        var (calculatedTotalOrders, calculatedTotalCash, calculatedTotalCard, calculatedTotalFawry, calculatedTotalBankTransfer) =
            CalculateShiftFinancials(shift.Orders ?? new List<Order>());

        // Use calculated values for open shifts, stored values for closed shifts
        var totalCash = shift.IsClosed ? shift.TotalCash : calculatedTotalCash;
        var totalCard = shift.IsClosed ? shift.TotalCard : calculatedTotalCard;
        var totalOrders = shift.IsClosed ? shift.TotalOrders : calculatedTotalOrders;
        // Fawry/BankTransfer are always computed from orders (not stored separately on entity)
        var totalFawry = calculatedTotalFawry;
        var totalBankTransfer = calculatedTotalBankTransfer;

        // Calculate duration
        var endTime = shift.ClosedAt ?? DateTime.UtcNow;
        var duration = endTime - shift.OpenedAt;
        var durationHours = (int)duration.TotalHours;
        var durationMinutes = duration.Minutes;

        // Calculate inactive hours
        var inactiveHours = shift.IsClosed ? 0 : (int)(DateTime.UtcNow - shift.LastActivityAt).TotalHours;

        return new ShiftDto
        {
            Id = shift.Id,
            OpeningBalance = shift.OpeningBalance,
            ClosingBalance = shift.ClosingBalance,
            ExpectedBalance = shift.IsClosed ? shift.ExpectedBalance : Math.Round(shift.OpeningBalance + calculatedTotalCash, 2),
            Difference = shift.Difference,
            OpenedAt = shift.OpenedAt,
            ClosedAt = shift.ClosedAt,
            IsClosed = shift.IsClosed,
            Notes = shift.Notes,
            TotalCash = totalCash,
            TotalCard = totalCard,
            TotalFawry = totalFawry,
            TotalBankTransfer = totalBankTransfer,
            TotalOrders = totalOrders,
            UserName = shift.User?.Name ?? string.Empty,

            // New fields
            LastActivityAt = shift.LastActivityAt,
            InactiveHours = inactiveHours,
            IsForceClosed = shift.IsForceClosed,
            ForceClosedByUserName = shift.ForceClosedByUserName,
            ForceClosedAt = shift.ForceClosedAt,
            ForceCloseReason = shift.ForceCloseReason,
            IsHandedOver = shift.IsHandedOver,
            HandedOverFromUserName = shift.HandedOverFromUserName,
            HandedOverToUserName = shift.HandedOverToUserName,
            HandedOverAt = shift.HandedOverAt,
            HandoverBalance = shift.HandoverBalance,
            HandoverNotes = shift.HandoverNotes,
            DurationHours = durationHours,
            DurationMinutes = durationMinutes,

            Orders = shift.Orders?.Select(o => new ShiftOrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status.ToString(),
                OrderType = o.OrderType.ToString(),
                Total = o.Total,
                CustomerName = o.CustomerName,
                CreatedAt = o.CreatedAt,
                CompletedAt = o.CompletedAt
            }).ToList() ?? new()
        };
    }
}
