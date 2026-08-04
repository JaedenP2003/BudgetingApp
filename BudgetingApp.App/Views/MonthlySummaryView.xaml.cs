using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BudgetingApp.Core.Models;
using BudgetingApp.Core.Summary;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

namespace BudgetingApp.App.Views;

public partial class MonthlySummaryView : UserControl
{
    private enum ChartDisplayMode { Bar, Pie, Ring }

    private record MonthOption(int Value, string Name);
    private record LegendItem(string Name, Brush Color);

    private readonly AppServices _services;
    private bool _initializing = true;
    private ChartDisplayMode _chartMode = ChartDisplayMode.Bar;
    private MonthlySummary? _currentSummary;

    public MonthlySummaryView(AppServices services)
    {
        _services = services;
        InitializeComponent();

        MonthBox.ItemsSource = Enumerable.Range(1, 12)
            .Select(m => new MonthOption(m, CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m)))
            .ToList();
        MonthBox.DisplayMemberPath = "Name";
        MonthBox.SelectedValuePath = "Value";

        var currentYear = DateTime.Today.Year;
        YearBox.ItemsSource = Enumerable.Range(currentYear - 4, 6).ToList();

        var availableMonths = _services.MonthlySummary.GetAvailableMonths();
        var defaultMonth = availableMonths.Count > 0 ? availableMonths.Max() : YearMonth.FromDate(DateOnly.FromDateTime(DateTime.Today));

        MonthBox.SelectedValue = defaultMonth.Month;
        YearBox.SelectedItem = defaultMonth.Year;

        UpdateChartModeButtons();

        _initializing = false;
        RefreshSummary();
    }

    private void MonthOrYear_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        RefreshSummary();
    }

    private void ChartMode_Click(object sender, RoutedEventArgs e)
    {
        _chartMode = sender switch
        {
            _ when ReferenceEquals(sender, PieChartModeButton) => ChartDisplayMode.Pie,
            _ when ReferenceEquals(sender, RingChartModeButton) => ChartDisplayMode.Ring,
            _ => ChartDisplayMode.Bar
        };
        UpdateChartModeButtons();
        if (_currentSummary is not null) RefreshCategoryChart(_currentSummary);
    }

    private void UpdateChartModeButtons()
    {
        foreach (var (button, mode) in new[] { (BarChartModeButton, ChartDisplayMode.Bar), (PieChartModeButton, ChartDisplayMode.Pie), (RingChartModeButton, ChartDisplayMode.Ring) })
        {
            button.Style = (Style)FindResource(mode == _chartMode ? "AccentButtonStyle" : "SecondaryButtonStyle");
        }
    }

    private void RefreshSummary()
    {
        if (MonthBox.SelectedValue is not int month || YearBox.SelectedItem is not int year) return;

        var summary = _services.MonthlySummary.GetSummary(new YearMonth(year, month));
        _currentSummary = summary;

        if (!summary.HasData)
        {
            NoDataPanel.Visibility = Visibility.Visible;
            TotalsPanel.Visibility = Visibility.Collapsed;
            DetailGrid.Visibility = Visibility.Collapsed;
            return;
        }

        NoDataPanel.Visibility = Visibility.Collapsed;
        TotalsPanel.Visibility = Visibility.Visible;
        DetailGrid.Visibility = Visibility.Visible;

        TotalIncomeText.Text = summary.TotalIncome.ToString("C");
        TotalExpensesText.Text = summary.TotalExpenses.ToString("C");
        TransfersText.Text = summary.TotalTransfers.ToString("C");
        SavingsText.Text = summary.Savings.ToString("C");
        var savingsBrush = (System.Windows.Media.Brush)FindResource(summary.Savings >= 0 ? "PositiveBrush" : "NegativeBrush");
        SavingsText.Foreground = savingsBrush;
        SavingsText.Effect = summary.Savings >= 0 ? (System.Windows.Media.Effects.Effect)FindResource("AccentGlowEffect") : null;
        SavingsCardAccent.Background = savingsBrush;

        SummaryGrid.ItemsSource = summary.Rows.Select(BuildCardDisplay).ToList();

        RefreshCategoryChart(summary);
    }

    private MonthlySummaryRowDisplay BuildCardDisplay(MonthlySummaryRow r)
    {
        var positive = (System.Windows.Media.Brush)FindResource("PositiveBrush");
        var negative = (System.Windows.Media.Brush)FindResource("NegativeBrush");
        var muted = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
        var amber = (System.Windows.Media.Brush)FindResource("AmberBrush");

        var accentBrush = r.IsIncome ? positive
            : r.IsTransfer ? muted
            : r.CategoryId is null ? amber
            : negative;

        var difference = r.ActualAmount - r.ExpectedAmount;
        var differenceText = (difference >= 0 ? "+" : "") + difference.ToString("C");

        Brush differenceBrush;
        if (r.ExpectedAmount == 0)
        {
            differenceBrush = muted; // no budget/recurring amount set — nothing to judge against
        }
        else
        {
            var isGoodDirection = r.IsIncome ? difference >= 0 : difference <= 0;
            differenceBrush = isGoodDirection ? positive : negative;
        }

        // Matches the sign convention used for r.ActualAmount above, so the individual
        // transaction amounts sum to the card's headline total.
        var transactionAmountBrush = r.IsIncome ? positive : r.IsTransfer ? muted : negative;
        var transactions = r.Transactions.Select(t =>
        {
            var displayAmount = r.IsTransfer ? t.Amount : r.IsIncome ? t.Amount : -t.Amount;
            return new TransactionDisplay(t.PostingDate.ToString("MMM d"), t.Description, displayAmount.ToString("C"), transactionAmountBrush);
        }).ToList();

        return new MonthlySummaryRowDisplay(r.CategoryName, r.ExpectedAmount, r.ActualAmount, difference, differenceText, accentBrush, differenceBrush, transactions);
    }

    private void ToggleTransactions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MonthlySummaryRowDisplay row })
        {
            row.IsExpanded = !row.IsExpanded;
        }
    }

    private void RefreshCategoryChart(MonthlySummary summary)
    {
        var spending = summary.Rows
            .Where(r => !r.IsIncome && !r.IsTransfer && r.ActualAmount > 0)
            .OrderByDescending(r => r.ActualAmount)
            .ToList();

        if (_chartMode == ChartDisplayMode.Bar)
        {
            CategoryChart.Visibility = Visibility.Visible;
            CategoryPieChart.Visibility = Visibility.Collapsed;
            CategoryLegend.Visibility = Visibility.Collapsed;

            var ascending = spending.AsEnumerable().Reverse().ToList(); // largest bar at top
            CategoryChart.Series =
            [
                new RowSeries<double>
                {
                    Values = ascending.Select(r => (double)r.ActualAmount).ToArray(),
                    Fill = new SolidColorPaint(ChartTheme.Negative),
                    DataLabelsPaint = ChartTheme.TextPaint(),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString("C0")
                }
            ];
            CategoryChart.YAxes =
            [
                new Axis { Labels = ascending.Select(r => r.CategoryName).ToArray(), LabelsPaint = ChartTheme.MutedTextPaint(), SeparatorsPaint = null }
            ];
            CategoryChart.XAxes =
            [
                new Axis { Labeler = value => value.ToString("C0"), LabelsPaint = ChartTheme.MutedTextPaint(), SeparatorsPaint = ChartTheme.SeparatorPaint() }
            ];
            return;
        }

        CategoryChart.Visibility = Visibility.Collapsed;
        CategoryPieChart.Visibility = Visibility.Visible;

        var isRing = _chartMode == ChartDisplayMode.Ring;
        var total = spending.Sum(r => r.ActualAmount);
        var colors = spending.Select((r, i) => ChartTheme.CategoryPalette[i % ChartTheme.CategoryPalette.Length]).ToList();

        CategoryPieChart.Series = spending.Select((r, i) => (ISeries)new PieSeries<double>
        {
            Values = [(double)r.ActualAmount],
            Name = r.CategoryName,
            Fill = new SolidColorPaint(colors[i]),
            InnerRadius = isRing ? 130 : 0,
            DataLabelsPaint = ChartTheme.TextPaint(),
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
            DataLabelsFormatter = point => total == 0 ? "" : (point.Coordinate.PrimaryValue / (double)total).ToString("P0")
        }).ToList();
        // LiveCharts2 2.0.5's own PieChart Legend has a layout bug with many series
        // (renders the pie as a clipped wedge instead of a full circle) — keep it off
        // and use CategoryLegend, a plain WPF ItemsControl, instead.
        CategoryPieChart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden;

        CategoryLegend.ItemsSource = spending.Select((r, i) => new LegendItem(r.CategoryName, ToBrush(colors[i]))).ToList();
        CategoryLegend.Visibility = Visibility.Visible;
    }

    private static Brush ToBrush(SkiaSharp.SKColor color)
    {
        var brush = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }
}
