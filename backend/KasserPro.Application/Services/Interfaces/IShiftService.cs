namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Shifts;

public interface IShiftService
{
    Task<ApiResponse<ShiftDto>> GetCurrentAsync(int userId);
    Task<ApiResponse<ShiftDto>> GetByIdAsync(int shiftId);
    Task<ApiResponse<ShiftDto>> OpenAsync(OpenShiftRequest request, int userId);
    Task<ApiResponse<ShiftDto>> CloseAsync(CloseShiftRequest request, int userId);
    Task<ApiResponse<List<ShiftDto>>> GetUserShiftsAsync(int userId);
    
    // Enhanced shift management
    Task<ApiResponse<ShiftDto>> ForceCloseAsync(int shiftId, ForceCloseShiftRequest request);
    Task<ApiResponse<ShiftDto>> HandoverAsync(int shiftId, HandoverShiftRequest request);
    Task<ApiResponse<bool>> UpdateActivityAsync(int shiftId);
    Task<ApiResponse<List<ShiftDto>>> GetActiveShiftsAsync();
    Task<ApiResponse<ShiftWarningDto>> GetShiftWarningsAsync(int userId);
    Task<ApiResponse<List<ShiftOrderDto>>> GetShiftOrdersAsync(int shiftId);
    Task<ApiResponse<List<ShiftProductSummaryDto>>> GetProductsSummaryAsync(int shiftId);
    
    /// <summary>
    /// Shift deletion is NOT supported for audit/financial integrity.
    /// This method always throws NotSupportedException.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown - shifts cannot be deleted.</exception>
    Task<ApiResponse<bool>> DeleteAsync(int id);
}
