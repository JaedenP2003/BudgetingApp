using BudgetingApp.Core.Categorization;

namespace BudgetingApp.Core.Import;

/// <summary>
/// Orchestrates a CSV upload end to end: parse + insert, then auto-categorize.
/// flipSign is an explicit per-import choice, not inferred from account type — bank
/// export tools vary on whether a credit card charge is shown as positive or negative,
/// and guessing wrong silently corrupts every amount in the file.
/// </summary>
public class ImportService(CsvImporter csvImporter, CategorizationEngine categorizationEngine)
{
    public ImportResult ImportAndCategorize(string filePath, long accountId, bool flipSign = false)
    {
        var csvResult = csvImporter.Import(filePath, accountId, flipSign);
        var categorization = categorizationEngine.CategorizeTransactions(csvResult.InsertedTransactionIds);

        return new ImportResult(csvResult.ImportedCount, categorization.UncategorizedCount, csvResult.RowErrors);
    }
}
