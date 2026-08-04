namespace BudgetingApp.Core.Models;

public record Category(
    long Id,
    string Name,
    bool IsIncome,
    bool IsTransfer
);
