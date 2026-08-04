namespace BudgetingApp.Core.Import;

internal record ColumnMapping(int DateIndex, int DescriptionIndex, int? AmountIndex, int? DebitIndex, int? CreditIndex)
{
    public static ColumnMapping Detect(string[] headers)
    {
        var normalized = headers.Select(h => h.Trim().ToLowerInvariant()).ToArray();

        var dateIndex = FindFirst(normalized, "posting date", "transaction date", "date")
            ?? throw new UnrecognizedCsvFormatException(
                "No date column found. Expected a header containing \"date\" (e.g. \"Posting Date\", \"Transaction Date\").");

        var descriptionIndex = FindFirst(normalized, "description", "memo", "payee", "merchant", "name")
            ?? throw new UnrecognizedCsvFormatException(
                "No description column found. Expected a header like \"Description\", \"Memo\", or \"Payee\".");

        var amountIndex = FindFirst(normalized, "amount");
        var debitIndex = FindFirst(normalized, "debit", "withdrawal");
        var creditIndex = FindFirst(normalized, "credit", "deposit");

        if (amountIndex is null && debitIndex is null && creditIndex is null)
        {
            throw new UnrecognizedCsvFormatException(
                "No amount column found. Expected either an \"Amount\" column, or \"Debit\"/\"Credit\" columns.");
        }

        return new ColumnMapping(dateIndex, descriptionIndex, amountIndex, debitIndex, creditIndex);
    }

    private static int? FindFirst(string[] normalizedHeaders, params string[] candidatesInPriorityOrder)
    {
        foreach (var candidate in candidatesInPriorityOrder)
        {
            var exact = Array.IndexOf(normalizedHeaders, candidate);
            if (exact >= 0) return exact;
        }
        foreach (var candidate in candidatesInPriorityOrder)
        {
            var partial = Array.FindIndex(normalizedHeaders, h => h.Contains(candidate));
            if (partial >= 0) return partial;
        }
        return null;
    }
}
