namespace KasserPro.Application.DTOs.RestaurantTables;

using KasserPro.Domain.Enums;

public class RestaurantTableDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public RestaurantTableStatus Status { get; set; }
    public bool IsActive { get; set; }
    public int? OpenOrderId { get; set; }
    public string? OpenOrderNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateRestaurantTableRequest
{
    public int BranchId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class UpdateRestaurantTableRequest
{
    public string Number { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SetRestaurantTableStatusRequest
{
    public RestaurantTableStatus Status { get; set; }
}
