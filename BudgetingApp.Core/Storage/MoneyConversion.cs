namespace BudgetingApp.Core.Storage;

/// <summary>
/// All money is stored as integer cents (SQLite INTEGER), never REAL/float, so summing
/// many rows can't drift off the true value by fractions of a cent.
/// </summary>
internal static class MoneyConversion
{
    public static long ToCents(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    public static decimal FromCents(long cents) => cents / 100m;
}
