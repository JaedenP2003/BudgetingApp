namespace BudgetingApp.Core.Import;

/// <summary>Raw result of parsing and inserting rows, before categorization runs.</summary>
public record CsvImportResult(IReadOnlyList<long> InsertedTransactionIds, IReadOnlyList<RowError> RowErrors)
{
    public int ImportedCount => InsertedTransactionIds.Count;
}
