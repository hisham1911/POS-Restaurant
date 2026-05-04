namespace KasserPro.Domain.Enums;

/// <summary>
/// Payment methods supported by the system
/// </summary>
public enum PaymentMethod
{
    /// <summary>
    /// Cash payment - affects cash register
    /// </summary>
    Cash = 0,

    /// <summary>
    /// Bank account payment - does not affect cash register
    /// </summary>
    BankAccount = 1,

    /// <summary>
    /// Wallet payment - does not affect cash register
    /// </summary>
    Wallet = 2
}
