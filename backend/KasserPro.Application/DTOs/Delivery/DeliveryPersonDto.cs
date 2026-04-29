namespace KasserPro.Application.DTOs.Delivery;

public class DeliveryPersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? VehicleInfo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDeliveryPersonRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? VehicleInfo { get; set; }
}

public class UpdateDeliveryPersonRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? VehicleInfo { get; set; }
    public bool IsActive { get; set; }
}

public class AssignDeliveryRequest
{
    public int DeliveryPersonId { get; set; }
    public string? DeliveryNotes { get; set; }
}

public class UpdateDeliveryStatusRequest
{
    public string DeliveryStatus { get; set; } = string.Empty; // PendingAssignment, Assigned, OutForDelivery, Delivered, Cancelled
    public string? DeliveryNotes { get; set; }
}

public class DeliveryOrderFilters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public int? DeliveryPersonId { get; set; }
    public DateTime? Date { get; set; }
}
