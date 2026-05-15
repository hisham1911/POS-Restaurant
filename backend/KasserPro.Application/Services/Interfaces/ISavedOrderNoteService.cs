namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.SavedOrderNotes;

public interface ISavedOrderNoteService
{
    Task<ApiResponse<List<SavedOrderNoteDto>>> GetAllAsync(int branchId, CancellationToken ct = default);
    Task<ApiResponse<SavedOrderNoteDto>> CreateAsync(CreateSavedOrderNoteRequest request, CancellationToken ct = default);
    Task<ApiResponse<SavedOrderNoteDto>> UpdateAsync(int id, UpdateSavedOrderNoteRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default);
}
