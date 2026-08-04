using BudgetingApp.Core.Categorization;
using BudgetingApp.Core.Import;
using BudgetingApp.Core.Storage;
using BudgetingApp.Core.Summary;

namespace BudgetingApp.App;

/// <summary>Wires up the local SQLite database and every Core service. Constructed once at startup.</summary>
public class AppServices
{
    public BudgetDatabase Database { get; }
    public CategoryRepository Categories { get; }
    public CategoryRuleRepository CategoryRules { get; }
    public RecurringExpenseRepository RecurringExpenses { get; }
    public TransactionRepository Transactions { get; }
    public BudgetRepository Budgets { get; }
    public AccountRepository Accounts { get; }
    public CategorizationEngine CategorizationEngine { get; }
    public ImportService ImportService { get; }
    public MonthlySummaryService MonthlySummary { get; }

    public AppServices(string dbPath)
    {
        Database = new BudgetDatabase(dbPath);
        Categories = new CategoryRepository(Database);
        CategoryRules = new CategoryRuleRepository(Database);
        RecurringExpenses = new RecurringExpenseRepository(Database);
        Transactions = new TransactionRepository(Database);
        Budgets = new BudgetRepository(Database);
        Accounts = new AccountRepository(Database);
        CategorizationEngine = new CategorizationEngine(CategoryRules, Transactions);
        ImportService = new ImportService(new CsvImporter(Database, Transactions), CategorizationEngine);
        MonthlySummary = new MonthlySummaryService(Transactions, Categories, RecurringExpenses, Budgets);
    }
}
