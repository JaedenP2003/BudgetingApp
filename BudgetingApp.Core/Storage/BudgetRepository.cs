using Microsoft.Data.Sqlite;
using BudgetingApp.Core.Models;

namespace BudgetingApp.Core.Storage;

public class BudgetRepository(BudgetDatabase database)
{
    public List<Budget> GetForMonth(YearMonth month)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, category_id, month, expected_amount_cents FROM budgets WHERE month = $month;";
        command.Parameters.AddWithValue("$month", month.ToString());

        var results = new List<Budget>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new Budget(
                reader.GetInt64(0),
                reader.GetInt64(1),
                YearMonth.Parse(reader.GetString(2)),
                MoneyConversion.FromCents(reader.GetInt64(3))
            ));
        }
        return results;
    }

    public void Upsert(long categoryId, YearMonth month, decimal expectedAmount)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO budgets (category_id, month, expected_amount_cents)
            VALUES ($categoryId, $month, $amountCents)
            ON CONFLICT(category_id, month) DO UPDATE SET expected_amount_cents = excluded.expected_amount_cents;
            """;
        command.Parameters.AddWithValue("$categoryId", categoryId);
        command.Parameters.AddWithValue("$month", month.ToString());
        command.Parameters.AddWithValue("$amountCents", MoneyConversion.ToCents(expectedAmount));
        command.ExecuteNonQuery();
    }
}
