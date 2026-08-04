using System.Globalization;

namespace BudgetingApp.Core.Models;

/// <summary>A calendar month, independent of any specific day. Sorts and compares naturally.</summary>
public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public static YearMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    public static YearMonth Parse(string value)
    {
        var parts = value.Split('-');
        return new YearMonth(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    public override string ToString() => $"{Year:D4}-{Month:D2}";

    public int CompareTo(YearMonth other)
    {
        var yearCompare = Year.CompareTo(other.Year);
        return yearCompare != 0 ? yearCompare : Month.CompareTo(other.Month);
    }
}
