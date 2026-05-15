namespace KasserPro.Application.Common.Helpers;

/// <summary>
/// حسابات الوردية المشتركة — مصدر الحقيقة الوحيد
/// </summary>
public static class ShiftCalculationHelper
{
    /// <summary>
    /// الرصيد المتوقع = رصيد الافتتاح + مبيعات كاش - مرتجعات كاش - مصروفات + إيداعات - سحوبات
    /// </summary>
    public static decimal CalculateExpectedBalance(
        decimal openingBalance,
        decimal cashSales,
        decimal cashRefunds,
        decimal cashExpenses,
        decimal cashIn,
        decimal cashOut)
    {
        return Math.Round(
            openingBalance + cashSales - cashRefunds - cashExpenses + cashIn - cashOut,
            2);
    }

    /// <summary>
    /// Resolve expected balance from the cash register when available; otherwise
    /// fall back to the shared opening/cash movement formula.
    /// </summary>
    public static decimal ResolveExpectedBalance(
        decimal openingBalance,
        decimal netCash,
        decimal? currentCashRegisterBalance)
    {
        if (currentCashRegisterBalance.HasValue)
            return Math.Round(currentCashRegisterBalance.Value, 2);

        return CalculateExpectedBalance(
            openingBalance,
            netCash,
            cashRefunds: 0m,
            cashExpenses: 0m,
            cashIn: 0m,
            cashOut: 0m);
    }
}
