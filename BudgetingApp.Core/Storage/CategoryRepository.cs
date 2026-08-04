using Microsoft.Data.Sqlite;
using BudgetingApp.Core.Models;

namespace BudgetingApp.Core.Storage;

public class CategoryRepository(BudgetDatabase database)
{
    public List<Category> GetAll()
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, is_income, is_transfer FROM categories ORDER BY is_income DESC, name;";

        var results = new List<Category>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new Category(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2) == 1, reader.GetInt32(3) == 1));
        }
        return results;
    }
}
