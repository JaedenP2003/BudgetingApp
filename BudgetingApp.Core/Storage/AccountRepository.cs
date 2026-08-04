using Microsoft.Data.Sqlite;
using BudgetingApp.Core.Models;

namespace BudgetingApp.Core.Storage;

public class AccountRepository(BudgetDatabase database)
{
    public List<Account> GetAll()
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, type FROM accounts ORDER BY name;";

        var results = new List<Account>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new Account(
                reader.GetInt64(0),
                reader.GetString(1),
                ParseType(reader.GetString(2))
            ));
        }
        return results;
    }

    public Account GetById(long id)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, type FROM accounts WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException($"No account with id {id}.");
        return new Account(reader.GetInt64(0), reader.GetString(1), ParseType(reader.GetString(2)));
    }

    public long Add(string name, AccountType type)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO accounts (name, type) VALUES ($name, $type);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$type", ToDbValue(type));
        return (long)command.ExecuteScalar()!;
    }

    public void Delete(long id)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM accounts WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static AccountType ParseType(string value) => value switch
    {
        "checking" => AccountType.Checking,
        "savings" => AccountType.Savings,
        "credit_card" => AccountType.CreditCard,
        _ => throw new InvalidOperationException($"Unknown account type \"{value}\".")
    };

    private static string ToDbValue(AccountType type) => type switch
    {
        AccountType.Checking => "checking",
        AccountType.Savings => "savings",
        AccountType.CreditCard => "credit_card",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
