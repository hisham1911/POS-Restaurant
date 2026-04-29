namespace KasserPro.Domain.Enums;

/// <summary>
/// Type of stock taking session scope
/// </summary>
public enum StockTakingType
{
    /// <summary>Full branch inventory count</summary>
    Full = 1,

    /// <summary>Partial count — specific category or area</summary>
    Partial = 2
}
