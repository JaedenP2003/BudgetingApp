using BudgetingApp.Core.Models;

namespace BudgetingApp.Core.Summary;

/// <summary>Computes how many times a recurring expense falls due within a given month.</summary>
internal static class RecurringExpenseOccurrences
{
    public static decimal ExpectedAmountFor(RecurringExpense expense, YearMonth month) =>
        Count(expense, month) * expense.ExpectedAmount;

    private static int Count(RecurringExpense expense, YearMonth month)
    {
        if (expense.StartDate > month.LastDay) return 0;
        if (expense.EndDate is { } end && end < month.FirstDay) return 0;

        return expense.Cadence switch
        {
            Cadence.Monthly => 1,
            Cadence.Yearly => expense.StartDate.Month == month.Month ? 1 : 0,
            Cadence.Weekly => CountStepped(expense, month, intervalDays: 7),
            Cadence.Biweekly => CountStepped(expense, month, intervalDays: 14),
            _ => 0
        };
    }

    private static int CountStepped(RecurringExpense expense, YearMonth month, int intervalDays)
    {
        var count = 0;
        var occurrence = expense.StartDate;
        while (occurrence <= month.LastDay)
        {
            if (occurrence >= month.FirstDay && (expense.EndDate is null || occurrence <= expense.EndDate))
            {
                count++;
            }
            occurrence = occurrence.AddDays(intervalDays);
        }
        return count;
    }
}
