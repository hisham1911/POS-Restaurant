namespace KasserPro.Domain.Enums;

public enum OrderStatus
{
    Draft = 0,
    Pending = 1,
    Completed = 2,
    Cancelled = 3,
    Refunded = 4,
    PartiallyRefunded = 5,
    Preparing = 6,
    Prepared = 7,
    Delivered = 8
}
