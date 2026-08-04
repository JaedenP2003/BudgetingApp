using System.Text.Json;
using BudgetingApp.Core.Models;
using BudgetingApp.Core.Storage;
using Microsoft.Data.Sqlite;

// One-off (re-runnable) export of the desktop SQLite database into a single JSON file
// shaped to match BudgetingApp.Web's WebBudgetStore — each top-level property here
// corresponds 1:1 to one of its browser-localStorage keys, so the web app's Restore
// Backup page can load this file directly without any reshaping.
//
// Usage: dotnet run --project BudgetingApp.ExportTool [dbPath] [outputPath]
//   dbPath defaults to the desktop app's real database location.
//   outputPath defaults to a file on the Desktop, timestamped, easy to find and to hand
//   off (email, iCloud Drive, etc.) — deliberately NOT a relative "./budgetapp-export.json"
//   default: this tool is naturally run from inside this git repo, and a relative default
//   silently drops years of real transaction data into the working tree, one `git add -A`
//   away from being committed and pushed to a public repo.

var dbPath = args.Length > 0
    ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BudgetingApp", "budget.db");
var outputPath = args.Length > 1
    ? args[1]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"budgetapp-export-{DateTime.Now:yyyy-MM-dd}.json");

if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"No database found at {dbPath}");
    return 1;
}

var database = new BudgetDatabase(dbPath);

var categories = new CategoryRepository(database).GetAll();
var accounts = new AccountRepository(database).GetAll();
var categoryRules = new CategoryRuleRepository(database).GetAll();
var recurringExpenses = new RecurringExpenseRepository(database).GetAll();
var transactions = new TransactionRepository(database).GetAll();
var budgets = GetAllBudgets(database);

var backup = new BackupData(categories, accounts, categoryRules, recurringExpenses, budgets, transactions);

var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(outputPath, json);

Console.WriteLine($"Exported from {dbPath}:");
Console.WriteLine($"  {categories.Count} categories, {accounts.Count} accounts, {categoryRules.Count} category rules,");
Console.WriteLine($"  {recurringExpenses.Count} recurring expenses, {budgets.Count} budgets, {transactions.Count} transactions");
Console.WriteLine($"Wrote {outputPath}");
return 0;

// BudgetRepository only exposes GetForMonth (the desktop app has never needed "all
// budgets" — there's no budget-setting screen yet on either frontend), so this reads
// the table directly rather than adding an app-wide GetAll for a tool-only need.
static List<Budget> GetAllBudgets(BudgetDatabase database)
{
    using var connection = new SqliteConnection(database.ConnectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT id, category_id, month, expected_amount_cents FROM budgets;";

    var results = new List<Budget>();
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        results.Add(new Budget(
            reader.GetInt64(0),
            reader.GetInt64(1),
            YearMonth.Parse(reader.GetString(2)),
            reader.GetInt64(3) / 100m
        ));
    }
    return results;
}

record BackupData(
    List<Category> Categories,
    List<Account> Accounts,
    List<CategoryRule> CategoryRules,
    List<RecurringExpense> RecurringExpenses,
    List<Budget> Budgets,
    List<Transaction> Transactions
);
