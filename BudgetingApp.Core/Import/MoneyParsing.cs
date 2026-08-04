using System.Globalization;

namespace BudgetingApp.Core.Import;

internal static class MoneyParsing
{
    /// <summary>Parses bank-export money formats: "$1,234.56", "(45.00)" (=-45.00), "-45.00", "45.00".</summary>
    public static bool TryParse(string? raw, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Trim();
        var negative = false;

        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            negative = true;
            text = text[1..^1];
        }

        text = text.Replace("$", "").Replace(",", "").Trim();

        if (!decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        amount = negative ? -Math.Abs(value) : value;
        return true;
    }
}
