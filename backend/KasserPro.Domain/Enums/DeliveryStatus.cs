namespace KasserPro.Domain.Enums;

public enum DeliveryStatus
{
    /// <summary>Pending assignment to a delivery person (في انتظار التعيين)</summary>
    PendingAssignment = 0,

    /// <summary>Assigned to a delivery person (تم التعيين)</summary>
    Assigned = 1,

    /// <summary>Delivery person is out for delivery (في الطريق)</summary>
    OutForDelivery = 2,

    /// <summary>Delivered to the customer (تم التوصيل)</summary>
    Delivered = 3,

    /// <summary>Delivery was cancelled (تم الإلغاء)</summary>
    Cancelled = 4
}
