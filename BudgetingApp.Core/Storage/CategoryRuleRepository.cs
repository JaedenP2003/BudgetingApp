using Microsoft.Data.Sqlite;
using BudgetingApp.Core.Models;
using MatchType = BudgetingApp.Core.Models.MatchType;

namespace BudgetingApp.Core.Storage;

public class CategoryRuleRepository(BudgetDatabase database)
{
    public List<CategoryRule> GetAll()
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, category_id, match_type, pattern, priority FROM category_rules ORDER BY priority, id;";

        var results = new List<CategoryRule>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CategoryRule(
                reader.GetInt64(0),
                reader.GetInt64(1),
                Enum.Parse<MatchType>(reader.GetString(2), ignoreCase: true),
                reader.GetString(3),
                reader.GetInt32(4)
            ));
        }
        return results;
    }

    public long Add(long categoryId, MatchType matchType, string pattern, int priority)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO category_rules (category_id, match_type, pattern, priority)
            VALUES ($categoryId, $matchType, $pattern, $priority);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$categoryId", categoryId);
        command.Parameters.AddWithValue("$matchType", matchType.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$pattern", pattern);
        command.Parameters.AddWithValue("$priority", priority);
        return (long)command.ExecuteScalar()!;
    }

    public void Delete(long ruleId)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM category_rules WHERE id = $id;";
        command.Parameters.AddWithValue("$id", ruleId);
        command.ExecuteNonQuery();
    }
}
