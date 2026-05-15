namespace KasserPro.Application.DTOs.SavedOrderNotes;

public class SavedOrderNoteDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateSavedOrderNoteRequest
{
    public int BranchId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class UpdateSavedOrderNoteRequest
{
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
