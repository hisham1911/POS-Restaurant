namespace KasserPro.Application.DTOs.Inventory;

public class InventoryTransferDto
{
    public int Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public int FromBranchId { get; set; }
    public string FromBranchName { get; set; } = string.Empty;
    public int ToBranchId { get; set; }
    public string ToBranchName { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public string? ApprovedByUserName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    public string? ReceivedByUserName { get; set; }
    public DateTime? ReceivedAt { get; set; }
    
    public string? CancelledByUserName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class CreateTransferRequest
{
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CancelTransferRequest
{
    public string Reason { get; set; } = string.Empty;
}
