using Microsoft.Data.Sqlite;
using BudgetingApp.Core.Models;

namespace BudgetingApp.Core.Storage;

public class RecurringExpenseRepository(BudgetDatabase database)
{
    public List<RecurringExpense> GetAll()
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, name, category_id, expected_amount_cents, cadence, start_date, end_date, note " +
            "FROM recurring_expenses ORDER BY start_date DESC;";

        var results = new List<RecurringExpense>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(Read(reader));
        }
        return results;
    }

    public long Add(RecurringExpense expense)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO recurring_expenses (name, category_id, expected_amount_cents, cadence, start_date, end_date, note)
            VALUES ($name, $categoryId, $amountCents, $cadence, $startDate, $endDate, $note);
            SELECT last_insert_rowid();
            """;
        BindParameters(command, expense);
        return (long)command.ExecuteScalar()!;
    }

    public void Update(RecurringExpense expense)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE recurring_expenses
            SET name = $name, category_id = $categoryId, expected_amount_cents = $amountCents,
                cadence = $cadence, start_date = $startDate, end_date = $endDate, note = $note
            WHERE id = $id;
            """;
        BindParameters(command, expense);
        command.Parameters.AddWithValue("$id", expense.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM recurring_expenses WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static void BindParameters(SqliteCommand command, RecurringExpense expense)
    {
        command.Parameters.AddWithValue("$name", expense.Name);
        command.Parameters.AddWithValue("$categoryId", expense.CategoryId);
        command.Parameters.AddWithValue("$amountCents", MoneyConversion.ToCents(expense.ExpectedAmount));
        command.Parameters.AddWithValue("$cadence", expense.Cadence.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$startDate", expense.StartDate.ToString("O"));
        command.Parameters.AddWithValue("$endDate", (object?)expense.EndDate?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$note", (object?)expense.Note ?? DBNull.Value);
    }

    private static RecurringExpense Read(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetInt64(2),
        MoneyConversion.FromCents(reader.GetInt64(3)),
        Enum.Parse<Cadence>(reader.GetString(4), ignoreCase: true),
        DateOnly.Parse(reader.GetString(5)),
        reader.IsDBNull(6) ? null : DateOnly.Parse(reader.GetString(6)),
        reader.IsDBNull(7) ? null : reader.GetString(7)
    );
}
