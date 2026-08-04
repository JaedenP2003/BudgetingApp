using BudgetingApp.Core.Models;

namespace BudgetingApp.Core.Summary;

/// <summary>CategoryId is null only for the synthetic "Uncategorized" row, which exists
/// so an uncategorized transaction's dollar amount is still visible somewhere rather
/// than silently missing from the category breakdown.</summary>
public record MonthlySummaryRow(long? CategoryId, string CategoryName, bool IsIncome, bool IsTransfer, decimal ExpectedAmount, decimal ActualAmount, IReadOnlyList<Transaction> Transactions);

/// <summary>
/// When HasData is false, no transactions have been imported for this month yet —
/// Rows is empty and totals are zero. The UI must show "no data imported yet",
/// never a deficit computed from recurring expenses alone (see project design principles).
///
/// TotalIncome and TotalExpenses exclude transactions categorized as a transfer between
/// the household's own tracked accounts (see Category.IsTransfer) — otherwise moving money
/// from Checking to Savings would count as both an expense (Checking) and income (Savings)
/// for the same dollar. TotalTransfers is shown separately so nothing is hidden, and
/// Savings = TotalIncome - TotalExpenses + TotalTransfers always equals the true net change
/// across every transaction regardless of how completely transfers have been categorized.
/// </summary>
public record MonthlySummary(
    YearMonth Month,
    bool HasData,
    IReadOnlyList<MonthlySummaryRow> Rows,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal TotalTransfers,
    decimal Savings
);
