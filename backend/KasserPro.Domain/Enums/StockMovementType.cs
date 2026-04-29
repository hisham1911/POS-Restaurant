namespace KasserPro.Domain.Enums;

/// <summary>
/// Types of stock movements for inventory tracking
/// </summary>
public enum StockMovementType
{
    /// <summary>Order completed - stock decreases</summary>
    Sale = 1,
    
    /// <summary>Order refunded - stock increases</summary>
    Refund = 2,
    
    /// <summary>Manual stock adjustment</summary>
    Adjustment = 3,
    
    /// <summary>Stock received from supplier</summary>
    Receiving = 4,
    
    /// <summary>Damaged or expired goods</summary>
    Damage = 5,
    
    /// <summary>Transfer between branches</summary>
    Transfer = 6,

    /// <summary>Stock taking / physical inventory count</summary>
    StockTaking = 7,

    /// <summary>Expired goods write-off</summary>
    Expired = 8
}
