namespace BudgetingApp.Core.Models;

public record Account(
    long Id,
    string Name,
    AccountType Type
);
