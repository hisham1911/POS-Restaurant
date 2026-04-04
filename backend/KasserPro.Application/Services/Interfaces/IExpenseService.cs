using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Expenses;
using KasserPro.Domain.Enums;

namespace KasserPro.Application.Services.Interfaces;

/// <summary>
/// Service interface for Expense management
/// </summary>
public interface IExpenseService
{
    /// <summary>
    /// Get all expenses with filtering and pagination
    /// </summary>
    Task<ApiResponse<PagedResult<ExpenseDto>>> GetAllAsync(
        int? categoryId = null,
        ExpenseStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? shiftId = null,
        int pageNumber = 1,
        int pageSize = 20);

    /// <summary>
    /// Get expense by ID
    /// </summary>
    Task<ApiResponse<ExpenseDto>> GetByIdAsync(int id);

    /// <summary>
    /// Create a new expense (Draft status)
    /// </summary>
    Task<ApiResponse<ExpenseDto>> CreateAsync(CreateExpenseRequest request);

    /// <summary>
    /// Update an expense (Draft only)
    /// </summary>
    Task<ApiResponse<ExpenseDto>> UpdateAsync(int id, UpdateExpenseRequest request);

    /// <summary>
    /// Delete an expense (Draft only)
    /// </summary>
    Task<ApiResponse<bool>> DeleteAsync(int id);

    /// <summary>
    /// Approve an expense (Admin only, Draft → Approved)
    /// </summary>
    Task<ApiResponse<ExpenseDto>> ApproveAsync(int id, ApproveExpenseRequest request);

    /// <summary>
    /// Reject an expense (Admin only, Draft → Rejected)
    /// </summary>
    Task<ApiResponse<ExpenseDto>> RejectAsync(int id, RejectExpenseRequest request);

    /// <summary>
    /// Pay an expense (Admin only, Approved → Paid)
    /// Updates cash register if payment method is Cash
    /// </summary>
    Task<ApiResponse<ExpenseDto>> PayAsync(int id, PayExpenseRequest request);

    /// <summary>
    /// Upload and attach a file to an expense (Draft only)
    /// </summary>
    Task<ApiResponse<ExpenseDto>> UploadAttachmentAsync(
        int id,
        string fileName,
        string filePath,
        long fileSize,
        string fileType);

    /// <summary>
    /// Delete an attachment from an expense (Draft only)
    /// </summary>
    Task<ApiResponse<bool>> DeleteAttachmentAsync(int expenseId, int attachmentId);
}
