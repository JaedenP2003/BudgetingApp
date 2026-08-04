namespace BudgetingApp.Core.Import;

public record RowError(int RowNumber, string RawLine, string Reason);

public record ImportResult(
    int ImportedCount,
    int UncategorizedCount,
    IReadOnlyList<RowError> RowErrors
)
{
    public bool HasErrors => RowErrors.Count > 0;
}
