using BudgetingApp.Core.Models;
using BudgetingApp.Core.Storage;

namespace BudgetingApp.Core.Summary;

public class MonthlySummaryService(
    TransactionRepository transactionRepository,
    CategoryRepository categoryRepository,
    RecurringExpenseRepository recurringExpenseRepository,
    BudgetRepository budgetRepository)
{
    public MonthlySummary GetSummary(YearMonth month)
    {
        var transactions = transactionRepository.GetForMonth(month);
        if (transactions.Count == 0)
        {
            return new MonthlySummary(month, HasData: false, Rows: [], TotalIncome: 0, TotalExpenses: 0, TotalTransfers: 0, Savings: 0);
        }

        var categories = categoryRepository.GetAll();
        var transferCategoryIds = categories.Where(c => c.IsTransfer).Select(c => c.Id).ToHashSet();
        var budgetsByCategory = budgetRepository.GetForMonth(month).ToDictionary(b => b.CategoryId, b => b.ExpectedAmount);

        var recurringExpectedByCategory = recurringExpenseRepository.GetAll()
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
            // Transfer categories show the raw signed net (matching the Transfers tile) rather
            // than the income/expense sign flip, which would misleadingly frame money moved
            // between the household's own accounts as if it were spending.
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

    public List<YearMonth> GetAvailableMonths() => transactionRepository.GetImportedMonths();
}
