namespace KasserPro.Domain.Enums;

/// <summary>
/// Status of a stock taking (physical inventory count) session
/// </summary>
public enum StockTakingStatus
{
    /// <summary>Counting is in progress</summary>
    InProgress = 1,

    /// <summary>Count completed and differences applied to inventory</summary>
    Completed = 2,

    /// <summary>Stock taking was cancelled without applying changes</summary>
    Cancelled = 3
}
