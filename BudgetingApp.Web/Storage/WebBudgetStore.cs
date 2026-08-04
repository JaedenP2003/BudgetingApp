using Blazored.LocalStorage;
using BudgetingApp.Core.Categorization;
using BudgetingApp.Core.Models;
using MatchType = BudgetingApp.Core.Models.MatchType;

namespace BudgetingApp.Web.Storage;

public record CategorizationResult(int CategorizedCount, int UncategorizedCount);

/// <summary>Shape produced by BudgetingApp.ExportTool and consumed by the web app's Restore
/// Backup page — one JSON file mapping 1:1 onto WebBudgetStore's six localStorage keys.</summary>
public record BackupData(
    List<Category> Categories,
    List<Account> Accounts,
    List<CategoryRule> CategoryRules,
    List<RecurringExpense> RecurringExpenses,
    List<Budget> Budgets,
    List<Transaction> Transactions
);

/// <summary>
/// The iPad/web frontend's independent data store — everything lives in memory and is
/// mirrored to browser local storage as JSON, per phase 2 of the iPad proposal ("simplest
/// option: treat the web app as its own independent data store"). Deliberately not backed
/// by BudgetingApp.Core's SQLite repositories: Microsoft.Data.Sqlite has no durable
/// persistence story in Blazor WebAssembly without extra OPFS/IndexedDB VFS plumbing, so
/// this reimplements the same repository shape against browser storage instead. Reuses
/// Core's Models and pure helpers (CategorizationEngine.MatchCategory) directly.
/// </summary>
public class WebBudgetStore(ILocalStorageService localStorage)
{
    private const string CategoriesKey = "budgetapp.categories";
    private const string AccountsKey = "budgetapp.accounts";
    private const string CategoryRulesKey = "budgetapp.categoryRules";
    private const string RecurringExpensesKey = "budgetapp.recurringExpenses";
    private const string BudgetsKey = "budgetapp.budgets";
    private const string TransactionsKey = "budgetapp.transactions";

    private List<Category> _categories = [];
    private List<Account> _accounts = [];
    private List<CategoryRule> _categoryRules = [];
    private List<RecurringExpense> _recurringExpenses = [];
    private List<Budget> _budgets = [];
    private List<Transaction> _transactions = [];

    private long _nextCategoryId;
    private long _nextAccountId;
    private long _nextCategoryRuleId;
    private long _nextRecurringExpenseId;
    private long _nextBudgetId;
    private long _nextTransactionId;

    public bool IsInitialized { get; private set; }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        _categories = await localStorage.GetItemAsync<List<Category>>(CategoriesKey) ?? [];
        _accounts = await localStorage.GetItemAsync<List<Account>>(AccountsKey) ?? [];
        _categoryRules = await localStorage.GetItemAsync<List<CategoryRule>>(CategoryRulesKey) ?? [];
        _recurringExpenses = await localStorage.GetItemAsync<List<RecurringExpense>>(RecurringExpensesKey) ?? [];
        _budgets = await localStorage.GetItemAsync<List<Budget>>(BudgetsKey) ?? [];
        _transactions = await localStorage.GetItemAsync<List<Transaction>>(TransactionsKey) ?? [];

        if (_categories.Count == 0)
        {
            SeedCategories();
            await SaveCategoriesAsync();
        }
        if (_accounts.Count == 0)
        {
            _accounts.Add(new Account(1, "Checking", AccountType.Checking));
            await SaveAccountsAsync();
        }

        _nextCategoryId = NextId(_categories.Select(c => c.Id));
        _nextAccountId = NextId(_accounts.Select(a => a.Id));
        _nextCategoryRuleId = NextId(_categoryRules.Select(r => r.Id));
        _nextRecurringExpenseId = NextId(_recurringExpenses.Select(e => e.Id));
        _nextBudgetId = NextId(_budgets.Select(b => b.Id));
        _nextTransactionId = NextId(_transactions.Select(t => t.Id));

        IsInitialized = true;
    }

    /// <summary>Wholesale-replaces every entity with the contents of a BackupData export
    /// (see BudgetingApp.ExportTool) and persists it — the "carry my desktop data over"
    /// path. Not a merge: existing browser data is discarded, since the export is meant to
    /// be the new source of truth, not layered on top of the seeded defaults.</summary>
    public async Task RestoreAsync(BackupData backup)
    {
        _categories = backup.Categories;
        _accounts = backup.Accounts;
        _categoryRules = backup.CategoryRules;
        _recurringExpenses = backup.RecurringExpenses;
        _budgets = backup.Budgets;
        _transactions = backup.Transactions;

        _nextCategoryId = NextId(_categories.Select(c => c.Id));
        _nextAccountId = NextId(_accounts.Select(a => a.Id));
        _nextCategoryRuleId = NextId(_categoryRules.Select(r => r.Id));
        _nextRecurringExpenseId = NextId(_recurringExpenses.Select(e => e.Id));
        _nextBudgetId = NextId(_budgets.Select(b => b.Id));
        _nextTransactionId = NextId(_transactions.Select(t => t.Id));

        await SaveCategoriesAsync();
        await SaveAccountsAsync();
        await SaveCategoryRulesAsync();
        await SaveRecurringExpensesAsync();
        await localStorage.SetItemAsync(BudgetsKey, _budgets);
        await SaveTransactionsAsync();
    }

    private static long NextId(IEnumerable<long> ids)
    {
        var max = 0L;
        foreach (var id in ids)
        {
            if (id > max) max = id;
        }
        return max + 1;
    }

    // ----- Categories -----

    public List<Category> GetCategories() => [.. _categories.OrderByDescending(c => c.IsIncome).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)];

    private void SeedCategories()
    {
        // Mirrors BudgetingApp.Core.Storage.BudgetDatabase.SeedCategories so a fresh
        // web install starts with the same default category set as a fresh desktop install.
        (string Name, bool IsIncome, bool IsTransfer)[] seed =
        [
            ("Paycheck", true, false),
            ("Other Income", true, false),
            ("Tithing", false, false),
            ("Rent", false, false),
            ("Utilities", false, false),
            ("Groceries", false, false),
            ("Dining Out", false, false),
            ("Gas", false, false),
            ("Entertainment", false, false),
            ("Subscriptions", false, false),
            ("Shopping", false, false),
            ("Misc", false, false),
            ("Investments", false, false),
            ("Transfer", false, true)
        ];

        var id = 1L;
        _categories = seed.Select(s => new Category(id++, s.Name, s.IsIncome, s.IsTransfer)).ToList();
    }

    private Task SaveCategoriesAsync() => localStorage.SetItemAsync(CategoriesKey, _categories).AsTask();

    // ----- Accounts -----

    public List<Account> GetAccounts() => [.. _accounts.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)];

    public async Task<Account> AddAccountAsync(string name, AccountType type)
    {
        var account = new Account(_nextAccountId++, name, type);
        _accounts.Add(account);
        await SaveAccountsAsync();
        return account;
    }

    public async Task DeleteAccountAsync(long id)
    {
        _accounts.RemoveAll(a => a.Id == id);
        await SaveAccountsAsync();
    }

    private Task SaveAccountsAsync() => localStorage.SetItemAsync(AccountsKey, _accounts).AsTask();

    // ----- Category rules -----

    public List<CategoryRule> GetCategoryRules() => [.. _categoryRules.OrderBy(r => r.Priority).ThenBy(r => r.Id)];

    public async Task<CategoryRule> AddCategoryRuleAsync(long categoryId, MatchType matchType, string pattern, int priority)
    {
        var rule = new CategoryRule(_nextCategoryRuleId++, categoryId, matchType, pattern, priority);
        _categoryRules.Add(rule);
        await SaveCategoryRulesAsync();
        return rule;
    }

    public async Task DeleteCategoryRuleAsync(long ruleId)
    {
        _categoryRules.RemoveAll(r => r.Id == ruleId);
        await SaveCategoryRulesAsync();
    }

    private Task SaveCategoryRulesAsync() => localStorage.SetItemAsync(CategoryRulesKey, _categoryRules).AsTask();

    // ----- Recurring expenses -----

    public List<RecurringExpense> GetRecurringExpenses() => [.. _recurringExpenses.OrderByDescending(e => e.StartDate)];

    public async Task<RecurringExpense> AddRecurringExpenseAsync(RecurringExpense expense)
    {
        var created = expense with { Id = _nextRecurringExpenseId++ };
        _recurringExpenses.Add(created);
        await SaveRecurringExpensesAsync();
        return created;
    }

    /// <summary>Every field on the recurring-expenses grid writes through immediately (see
    /// RecurringExpensesView.xaml.cs's RecurringExpenseRow) — this is that same "no separate
    /// save step" behavior for the web grid.</summary>
    public async Task UpdateRecurringExpenseAsync(RecurringExpense expense)
    {
        var index = _recurringExpenses.FindIndex(e => e.Id == expense.Id);
        if (index < 0) return;
        _recurringExpenses[index] = expense;
        await SaveRecurringExpensesAsync();
    }

    public async Task DeleteRecurringExpenseAsync(long id)
    {
        _recurringExpenses.RemoveAll(e => e.Id == id);
        await SaveRecurringExpensesAsync();
    }

    private Task SaveRecurringExpensesAsync() => localStorage.SetItemAsync(RecurringExpensesKey, _recurringExpenses).AsTask();

    // ----- Budgets -----

    public List<Budget> GetBudgetsForMonth(YearMonth month) => [.. _budgets.Where(b => b.Month == month)];

    public async Task UpsertBudgetAsync(long categoryId, YearMonth month, decimal expectedAmount)
    {
        var index = _budgets.FindIndex(b => b.CategoryId == categoryId && b.Month == month);
        if (index >= 0)
        {
            _budgets[index] = _budgets[index] with { ExpectedAmount = expectedAmount };
        }
        else
        {
            _budgets.Add(new Budget(_nextBudgetId++, categoryId, month, expectedAmount));
        }
        await localStorage.SetItemAsync(BudgetsKey, _budgets);
    }

    // ----- Transactions -----

    public List<Transaction> GetAllTransactions() => [.. _transactions.OrderByDescending(t => t.PostingDate)];

    public List<Transaction> GetUncategorizedTransactions() =>
        [.. _transactions.Where(t => t.CategoryId is null).OrderByDescending(t => t.PostingDate)];

    public List<Transaction> GetTransactionsForMonth(YearMonth month) =>
        [.. _transactions.Where(t => t.PostingDate >= month.FirstDay && t.PostingDate <= month.LastDay).OrderBy(t => t.PostingDate)];

    public List<YearMonth> GetImportedMonths() =>
        [.. _transactions.Select(t => YearMonth.FromDate(t.PostingDate)).Distinct().OrderByDescending(m => m)];

    /// <summary>Inserts a batch of parsed rows and persists once, rather than round-tripping to
    /// local storage per row — matters once a CSV import runs into the hundreds of rows.</summary>
    public async Task<List<long>> InsertTransactionsAsync(IEnumerable<(DateOnly PostingDate, string Description, decimal Amount, long AccountId, string SourceFile, string RawRow)> rows)
    {
        var insertedIds = new List<long>();
        foreach (var row in rows)
        {
            var transaction = new Transaction(_nextTransactionId++, row.PostingDate, row.Description, row.Amount, null, row.AccountId, row.SourceFile, row.RawRow);
            _transactions.Add(transaction);
            insertedIds.Add(transaction.Id);
        }
        await SaveTransactionsAsync();
        return insertedIds;
    }

    public async Task SetCategoryAsync(long transactionId, long? categoryId)
    {
        var index = _transactions.FindIndex(t => t.Id == transactionId);
        if (index < 0) return;
        _transactions[index] = _transactions[index] with { CategoryId = categoryId };
        await SaveTransactionsAsync();
    }

    /// <summary>Runs category_rules against the given (already-uncategorized) transactions and
    /// assigns the first matching rule's category, exactly like Core's CategorizationEngine —
    /// reuses its static matching logic directly rather than re-deriving the match semantics.</summary>
    public async Task<CategorizationResult> CategorizeTransactionsAsync(IReadOnlyCollection<long> transactionIds)
    {
        var rules = GetCategoryRules();
        var categorizedCount = 0;

        foreach (var id in transactionIds)
        {
            var index = _transactions.FindIndex(t => t.Id == id);
            if (index < 0) continue;

            var categoryId = CategorizationEngine.MatchCategory(_transactions[index].Description, rules);
            if (categoryId is null) continue;

            _transactions[index] = _transactions[index] with { CategoryId = categoryId };
            categorizedCount++;
        }

        await SaveTransactionsAsync();
        return new CategorizationResult(categorizedCount, transactionIds.Count - categorizedCount);
    }

    private Task SaveTransactionsAsync() => localStorage.SetItemAsync(TransactionsKey, _transactions).AsTask();
}
