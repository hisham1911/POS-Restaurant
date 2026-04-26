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
}
