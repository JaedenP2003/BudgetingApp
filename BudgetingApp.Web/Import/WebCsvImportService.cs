using System.Globalization;
using System.Text.Json;
using BudgetingApp.Core.Import;
using BudgetingApp.Web.Storage;
using CsvHelper;

namespace BudgetingApp.Web.Import;

/// <summary>
/// Browser-side counterpart to Core's CsvImporter + ImportService, reading from an uploaded
/// file's stream instead of a file path and writing into WebBudgetStore instead of SQLite.
/// Reuses Core's internal ColumnMapping/MoneyParsing (exposed to this assembly via
/// InternalsVisibleTo) so the header-detection and money-format parsing rules can't drift
/// between the desktop and web importers.
/// </summary>
public class WebCsvImportService(WebBudgetStore store)
{
    public async Task<ImportResult> ImportAsync(Stream csvStream, string sourceFileName, long accountId, bool flipSign)
    {
        using var textReader = new StreamReader(csvStream);
        using var csv = new CsvReader(textReader, CultureInfo.InvariantCulture);

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            throw new UnrecognizedCsvFormatException("The file has no header row.");
        }

        var headers = csv.HeaderRecord;
        var mapping = ColumnMapping.Detect(headers);

        var errors = new List<RowError>();
        var rows = new List<(DateOnly PostingDate, string Description, decimal Amount, long AccountId, string SourceFile, string RawRow)>();
        var rowNumber = 1;

        while (csv.Read())
        {
            rowNumber++;
            var rawLine = (csv.Context.Parser?.RawRecord ?? string.Empty).TrimEnd('\r', '\n');

            var rawFields = new Dictionary<string, string?>();
            foreach (var header in headers)
            {
                rawFields[header] = csv.GetField(header);
            }

            if (!TryParseRow(csv, mapping, out var postingDate, out var description, out var amount, out var reason))
            {
                errors.Add(new RowError(rowNumber, rawLine, reason!));
                continue;
            }

            var rawRowJson = JsonSerializer.Serialize(rawFields);
            var signedAmount = flipSign ? -amount : amount;
            rows.Add((postingDate, description, signedAmount, accountId, sourceFileName, rawRowJson));
        }

        var insertedIds = await store.InsertTransactionsAsync(rows);
        var categorization = await store.CategorizeTransactionsAsync(insertedIds);

        return new ImportResult(insertedIds.Count, categorization.UncategorizedCount, errors);
    }

    // Mirrors CsvImporter.TryParseRow in BudgetingApp.Core exactly, since that method is
    // private on a SQLite-coupled class and can't be called directly from here.
    private static bool TryParseRow(
        CsvReader csv,
        ColumnMapping mapping,
        out DateOnly postingDate,
        out string description,
        out decimal amount,
        out string? reason)
    {
        postingDate = default;
        description = string.Empty;
        amount = default;
        reason = null;

        var dateField = csv.GetField(mapping.DateIndex);
        if (!DateOnly.TryParse(dateField, CultureInfo.InvariantCulture, DateTimeStyles.None, out postingDate))
        {
            reason = $"Could not parse date \"{dateField}\".";
            return false;
        }

        description = csv.GetField(mapping.DescriptionIndex)?.Trim() ?? string.Empty;
        if (description.Length == 0)
        {
            reason = "Description is empty.";
            return false;
        }

        if (mapping.AmountIndex is { } amountIndex)
        {
            var amountField = csv.GetField(amountIndex);
            if (!MoneyParsing.TryParse(amountField, out amount))
            {
                reason = $"Could not parse amount \"{amountField}\".";
                return false;
            }
            return true;
        }

        var debitField = mapping.DebitIndex is { } debitIndex ? csv.GetField(debitIndex) : null;
        var creditField = mapping.CreditIndex is { } creditIndex ? csv.GetField(creditIndex) : null;

        var hasDebit = MoneyParsing.TryParse(debitField, out var debit);
        var hasCredit = MoneyParsing.TryParse(creditField, out var credit);

        if (!hasDebit && !hasCredit)
        {
            reason = "Both debit and credit fields are empty or unparseable.";
            return false;
        }

        amount = (hasCredit ? Math.Abs(credit) : 0m) - (hasDebit ? Math.Abs(debit) : 0m);
        return true;
    }
}
