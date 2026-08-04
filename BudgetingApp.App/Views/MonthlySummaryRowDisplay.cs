using System.ComponentModel;
using System.Windows.Media;

namespace BudgetingApp.App.Views;

public class MonthlySummaryRowDisplay(
    string categoryName,
    decimal expected,
    decimal actual,
    decimal difference,
    string differenceText,
    Brush accentBrush,
    Brush differenceBrush,
    IReadOnlyList<TransactionDisplay> transactions) : INotifyPropertyChanged
{
    public string CategoryName { get; init; } = categoryName;
    public decimal Expected { get; init; } = expected;
    public decimal Actual { get; init; } = actual;
    public decimal Difference { get; init; } = difference;
    public string DifferenceText { get; init; } = differenceText;
    public Brush AccentBrush { get; init; } = accentBrush;
    public Brush DifferenceBrush { get; init; } = differenceBrush;
    public IReadOnlyList<TransactionDisplay> Transactions { get; init; } = transactions;

    public bool HasTransactions => Transactions.Count > 0;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleLabel)));
        }
    }

    public string ToggleLabel => IsExpanded
        ? "▲ Hide"
        : $"▼ {Transactions.Count} transaction{(Transactions.Count == 1 ? "" : "s")}";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public record TransactionDisplay(string DateText, string Description, string AmountText, Brush AmountBrush);
