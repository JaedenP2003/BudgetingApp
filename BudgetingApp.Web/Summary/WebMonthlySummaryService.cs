using BudgetingApp.Core.Models;
using BudgetingApp.Core.Summary;
using BudgetingApp.Web.Storage;

namespace BudgetingApp.Web.Summary;

/// <summary>
/// Web-side port of Core's MonthlySummaryService, same expected-vs-actual logic against
/// WebBudgetStore instead of the SQLite repositories. Reuses Core's internal
/// RecurringExpenseOccurrences (exposed to this assembly via InternalsVisibleTo) and the
/// public MonthlySummary/MonthlySummaryRow record types so the desktop and web summaries
/// stay structurally identical.
/// </summary>
public class WebMonthlySummaryService(WebBudgetStore store)
{
    public MonthlySummary GetSummary(YearMonth month)
    {
        var transactions = store.GetTransactionsForMonth(month);
        if (transactions.Count == 0)
        {
            return new MonthlySummary(month, HasData: false, Rows: [], TotalIncome: 0, TotalExpenses: 0, TotalTransfers: 0, Savings: 0);
        }

        var categories = store.GetCategories();
        var transferCategoryIds = categories.Where(c => c.IsTransfer).Select(c => c.Id).ToHashSet();
        var budgetsByCategory = store.GetBudgetsForMonth(month).ToDictionary(b => b.CategoryId, b => b.ExpectedAmount);

        var recurringExpectedByCategory = store.GetRecurringExpenses()
            .GroupBy(e => e.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(e => RecurringExpenseOccurrences.ExpectedAmountFor(e, month)));

        var actualByCategory = transactions
            .Where(t => t.CategoryId is not null)
            .GroupBy(t => t.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var transactionsByCategory = transactions
            .Where(t => t.CategoryId is not null)
            .GroupBy(t => t.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Transaction>)g.OrderByDescending(t => t.PostingDate).ToList());

        var rows = new List<MonthlySummaryRow>();
        foreach (var category in categories)
        {
            var expected = budgetsByCategory.GetValueOrDefault(category.Id, 0m)
                           + recurringExpectedByCategory.GetValueOrDefault(category.Id, 0m);
            var rawActual = actualByCategory.GetValueOrDefault(category.Id, 0m);
            var actual = category.IsTransfer ? rawActual : category.IsIncome ? rawActual : -rawActual;
            var categoryTransactions = transactionsByCategory.GetValueOrDefault(category.Id, []);

            rows.Add(new MonthlySummaryRow(category.Id, category.Name, category.IsIncome, category.IsTransfer, expected, actual, categoryTransactions));
        }

        var uncategorizedTransactions = transactions.Where(t => t.CategoryId is null).OrderByDescending(t => t.PostingDate).ToList();
        var uncategorizedAmount = uncategorizedTransactions.Sum(t => t.Amount);
        if (uncategorizedAmount != 0m)
        {
            rows.Add(new MonthlySummaryRow(null, "Uncategorized", IsIncome: uncategorizedAmount > 0, IsTransfer: false, 0m, Math.Abs(uncategorizedAmount), uncategorizedTransactions));
        }

        bool IsTransfer(Transaction t) => t.CategoryId is { } id && transferCategoryIds.Contains(id);

        var totalIncome = transactions.Where(t => t.Amount > 0 && !IsTransfer(t)).Sum(t => t.Amount);
        var totalExpenses = -transactions.Where(t => t.Amount < 0 && !IsTransfer(t)).Sum(t => t.Amount);
        var totalTransfers = transactions.Where(IsTransfer).Sum(t => t.Amount);
        var savings = totalIncome - totalExpenses + totalTransfers;

        return new MonthlySummary(month, HasData: true, rows, totalIncome, totalExpenses, totalTransfers, savings);
    }

    public List<YearMonth> GetAvailableMonths() => store.GetImportedMonths();
}
