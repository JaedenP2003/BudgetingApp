namespace BudgetingApp.Core.Models;

/// <summary>
/// A named, recurring dollar amount (rent, a subscription, or a one-time irregular
/// expense entered as a single-occurrence row with a note). Replaces the old
/// spreadsheet's hardcoded numbers buried inside formulas — every expected dollar
/// here has a name and, optionally, a note explaining it.
/// </summary>
public record RecurringExpense(
    long Id,
    string Name,
    long CategoryId,
    decimal ExpectedAmount,
    Cadence Cadence,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Note
);
