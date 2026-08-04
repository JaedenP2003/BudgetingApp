namespace BudgetingApp.Core.Models;

/// <summary>A manually-set monthly target for a category, distinct from RecurringExpense
/// (which represents a specific recurring bill). Use this for discretionary categories
/// like Groceries or Dining Out that have a target but no fixed recurring line items.</summary>
public record Budget(
    long Id,
    long CategoryId,
    YearMonth Month,
    decimal ExpectedAmount
);
