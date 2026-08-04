using Microsoft.Data.Sqlite;
using BudgetingApp.Core.Models;

namespace BudgetingApp.Core.Storage;

public class TransactionRepository(BudgetDatabase database)
{
    private const string Columns = "id, posting_date, description, amount_cents, category_id, account_id, source_file, raw_row";

    /// <summary>Inserts a transaction with no category (categorization is a separate pass) and returns its new id.</summary>
    public long Insert(SqliteConnection connection, DateOnly postingDate, string description, decimal amount, long accountId, string sourceFile, string rawRowJson)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO transactions (posting_date, description, amount_cents, category_id, account_id, source_file, raw_row)
            VALUES ($postingDate, $description, $amountCents, NULL, $accountId, $sourceFile, $rawRow);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$postingDate", postingDate.ToString("O"));
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$amountCents", MoneyConversion.ToCents(amount));
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$sourceFile", sourceFile);
        command.Parameters.AddWithValue("$rawRow", rawRowJson);
        return (long)command.ExecuteScalar()!;
    }

    public void SetCategory(long transactionId, long? categoryId)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE transactions SET category_id = $categoryId WHERE id = $id;";
        command.Parameters.AddWithValue("$categoryId", (object?)categoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", transactionId);
        command.ExecuteNonQuery();
    }

    public List<Transaction> GetUncategorized()
    {
        return Query("WHERE category_id IS NULL ORDER BY posting_date DESC;");
    }

    public List<Transaction> GetByIds(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0) return [];

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        var placeholders = string.Join(",", ids.Select((_, i) => $"$id{i}"));
        command.CommandText = $"SELECT {Columns} FROM transactions WHERE id IN ({placeholders});";
        var i2 = 0;
        foreach (var id in ids)
        {
            command.Parameters.AddWithValue($"$id{i2}", id);
            i2++;
        }

        var results = new List<Transaction>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) results.Add(Read(reader));
        return results;
    }

    public List<Transaction> GetForMonth(YearMonth month)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {Columns} " +
            "FROM transactions WHERE posting_date >= $start AND posting_date <= $end ORDER BY posting_date;";
        command.Parameters.AddWithValue("$start", month.FirstDay.ToString("O"));
        command.Parameters.AddWithValue("$end", month.LastDay.ToString("O"));

        var results = new List<Transaction>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) results.Add(Read(reader));
        return results;
    }

    public List<Transaction> GetAll()
    {
        return Query("ORDER BY posting_date DESC;");
    }

    public bool AnyForAccount(long accountId)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM transactions WHERE account_id = $accountId);";
        command.Parameters.AddWithValue("$accountId", accountId);
        return (long)command.ExecuteScalar()! == 1;
    }

    /// <summary>Distinct months that have at least one imported transaction, newest first.</summary>
    public List<YearMonth> GetImportedMonths()
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT substr(posting_date, 1, 7) FROM transactions ORDER BY 1 DESC;";

        var results = new List<YearMonth>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) results.Add(YearMonth.Parse(reader.GetString(0)));
        return results;
    }

    private List<Transaction> Query(string whereAndOrder)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM transactions {whereAndOrder}";

        var results = new List<Transaction>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) results.Add(Read(reader));
        return results;
    }

    private static Transaction Read(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        DateOnly.Parse(reader.GetString(1)),
        reader.GetString(2),
        MoneyConversion.FromCents(reader.GetInt64(3)),
        reader.IsDBNull(4) ? null : reader.GetInt64(4),
        reader.GetInt64(5),
        reader.GetString(6),
        reader.GetString(7)
    );
}
