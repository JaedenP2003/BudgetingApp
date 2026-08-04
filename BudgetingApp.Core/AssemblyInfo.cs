using System.Runtime.CompilerServices;

// Lets BudgetingApp.Web reuse the internal CSV column-detection/money-parsing/recurring-expense
// helpers below Core's public repository API, without exposing them to arbitrary consumers.
[assembly: InternalsVisibleTo("BudgetingApp.Web")]
