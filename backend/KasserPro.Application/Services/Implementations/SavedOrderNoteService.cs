namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.SavedOrderNotes;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;

public class SavedOrderNoteService : ISavedOrderNoteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SavedOrderNoteService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<SavedOrderNoteDto>>> GetAllAsync(int branchId, CancellationToken ct = default)
    {
        var targetBranchId = branchId > 0 ? branchId : _currentUser.BranchId;
        var notes = await _unitOfWork.SavedOrderNotes.Query()
            .AsNoTracking()
            .Where(n => n.TenantId == _currentUser.TenantId && n.BranchId == targetBranchId)
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.Text)
            .ToListAsync(ct);

        return ApiResponse<List<SavedOrderNoteDto>>.Ok(notes.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<SavedOrderNoteDto>> CreateAsync(CreateSavedOrderNoteRequest request, CancellationToken ct = default)
    {
        var targetBranchId = request.BranchId > 0 ? request.BranchId : _currentUser.BranchId;
        var text = request.Text.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return ApiResponse<SavedOrderNoteDto>.Fail(ErrorCodes.VALIDATION_ERROR, "نص الملاحظة مطلوب");

        var duplicate = await _unitOfWork.SavedOrderNotes.Query()
            .AnyAsync(n => n.TenantId == _currentUser.TenantId
                        && n.BranchId == targetBranchId
                        && n.Text == text, ct);

        if (duplicate)
            return ApiResponse<SavedOrderNoteDto>.Fail(ErrorCodes.CONFLICT, "الملاحظة محفوظة بالفعل");

        var note = new SavedOrderNote
        {
            TenantId = _currentUser.TenantId,
            BranchId = targetBranchId,
            Text = text,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _unitOfWork.SavedOrderNotes.AddAsync(note);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<SavedOrderNoteDto>.Ok(MapToDto(note), "تم إضافة الملاحظة");
    }

    public async Task<ApiResponse<SavedOrderNoteDto>> UpdateAsync(int id, UpdateSavedOrderNoteRequest request, CancellationToken ct = default)
    {
        var note = await _unitOfWork.SavedOrderNotes.Query()
            .FirstOrDefaultAsync(n => n.Id == id
                                   && n.TenantId == _currentUser.TenantId
                                   && n.BranchId == _currentUser.BranchId, ct);

        if (note == null)
            return ApiResponse<SavedOrderNoteDto>.Fail(ErrorCodes.NOT_FOUND, "الملاحظة غير موجودة");

        var text = request.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return ApiResponse<SavedOrderNoteDto>.Fail(ErrorCodes.VALIDATION_ERROR, "نص الملاحظة مطلوب");

        var duplicate = await _unitOfWork.SavedOrderNotes.Query()
            .AnyAsync(n => n.TenantId == _currentUser.TenantId
                        && n.BranchId == note.BranchId
                        && n.Text == text
                        && n.Id != id, ct);

        if (duplicate)
            return ApiResponse<SavedOrderNoteDto>.Fail(ErrorCodes.CONFLICT, "الملاحظة محفوظة بالفعل");

        note.Text = text;
        note.SortOrder = request.SortOrder;
        note.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<SavedOrderNoteDto>.Ok(MapToDto(note), "تم تحديث الملاحظة");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var note = await _unitOfWork.SavedOrderNotes.Query()
            .FirstOrDefaultAsync(n => n.Id == id
                                   && n.TenantId == _currentUser.TenantId
                                   && n.BranchId == _currentUser.BranchId, ct);

        if (note == null)
            return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "الملاحظة غير موجودة");

        note.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف الملاحظة");
    }

    private static SavedOrderNoteDto MapToDto(SavedOrderNote note) => new()
    {
        Id = note.Id,
        TenantId = note.TenantId,
        BranchId = note.BranchId,
        Text = note.Text,
        SortOrder = note.SortOrder,
        IsActive = note.IsActive,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt
    };
}
