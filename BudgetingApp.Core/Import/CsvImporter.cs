using System.Globalization;
using System.Text.Json;
using CsvHelper;
using Microsoft.Data.Sqlite;
using BudgetingApp.Core.Storage;

namespace BudgetingApp.Core.Import;

/// <summary>
/// Parses a bank CSV export and inserts every parseable row as an uncategorized
/// transaction. Rows that can't be parsed (bad date, bad amount) are reported in
/// the result, never silently dropped — the file may still partially import.
/// Column layout is auto-detected from the header (see ColumnMapping); v1 only
/// recognizes common layouts, a mapping UI for arbitrary formats is P1.
/// </summary>
public class CsvImporter(BudgetDatabase database, TransactionRepository transactionRepository)
{
    /// <summary>
    /// Imports transactions into the given account. Set <paramref name="flipSign"/> for
    /// credit card accounts: a card statement typically shows a charge as a positive
    /// number (debt increasing) and a payment as negative, the opposite of the
    /// negative-is-money-out convention used for checking/savings — flipping here
    /// normalizes every account onto the same sign convention everywhere else in the app.
    /// </summary>
    public CsvImportResult Import(string filePath, long accountId, bool flipSign = false)
    {
        var sourceFile = Path.GetFileName(filePath);

        using var textReader = new StreamReader(filePath);
        using var csv = new CsvReader(textReader, CultureInfo.InvariantCulture);

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            throw new UnrecognizedCsvFormatException("The file has no header row.");
        }

        var headers = csv.HeaderRecord;
        var mapping = ColumnMapping.Detect(headers);

        var errors = new List<RowError>();
        var insertedIds = new List<long>();
        var rowNumber = 1;

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

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
            var id = transactionRepository.Insert(connection, postingDate, description, signedAmount, accountId, sourceFile, rawRowJson);
            insertedIds.Add(id);
        }

        transaction.Commit();

        return new CsvImportResult(insertedIds, errors);
    }

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

        // Split debit/credit columns: debit is money out (negative), credit is money in (positive).
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
