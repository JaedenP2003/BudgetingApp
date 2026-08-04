using System.Windows.Controls;
using BudgetingApp.Core.Models;
using BudgetingApp.Core.Summary;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace BudgetingApp.App.Views;

public partial class TrendsView : UserControl
{
    private readonly AppServices _services;
    private List<YearMonth> _months = [];
    private List<MonthlySummary> _summaries = [];
    private bool _initializing = true;

    public TrendsView(AppServices services)
    {
        _services = services;
        InitializeComponent();

        _months = _services.MonthlySummary.GetAvailableMonths().OrderBy(m => m).ToList();
        if (_months.Count == 0)
        {
            NoDataPanel.Visibility = System.Windows.Visibility.Visible;
            ContentGrid.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        _summaries = _months.Select(m => _services.MonthlySummary.GetSummary(m)).ToList();

        CategoryBox.ItemsSource = _services.Categories.GetAll().Where(c => !c.IsTransfer).ToList();

        BuildOverviewChart();

        _initializing = false;
        CategoryBox.SelectedIndex = 0;
    }

    private string[] MonthLabels => _months.Select(m => $"{System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m.Month)} {m.Year}").ToArray();

    private void BuildOverviewChart()
    {
        OverviewChart.Series =
        [
            new ColumnSeries<double>
            {
                Name = "Income",
                Values = _summaries.Select(s => (double)s.TotalIncome).ToArray(),
                Fill = new SolidColorPaint(ChartTheme.Positive)
            },
            new ColumnSeries<double>
            {
                Name = "Expenses",
                Values = _summaries.Select(s => (double)s.TotalExpenses).ToArray(),
                Fill = new SolidColorPaint(ChartTheme.Negative)
            },
            new LineSeries<double>
            {
                Name = "Savings",
                Values = _summaries.Select(s => (double)s.Savings).ToArray(),
                Stroke = new SolidColorPaint(ChartTheme.Accent) { StrokeThickness = 3 },
                Fill = null,
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(ChartTheme.Accent),
                GeometryStroke = new SolidColorPaint(ChartTheme.Accent)
            }
        ];
        OverviewChart.XAxes = [new Axis { Labels = MonthLabels, LabelsPaint = ChartTheme.MutedTextPaint(), SeparatorsPaint = null }];
        OverviewChart.YAxes = [new Axis { Labeler = value => value.ToString("C0"), LabelsPaint = ChartTheme.MutedTextPaint(), SeparatorsPaint = ChartTheme.SeparatorPaint() }];
    }

    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (CategoryBox.SelectedItem is not Category category) return;

        var values = _summaries
            .Select(s => (double)(s.Rows.FirstOrDefault(r => r.CategoryId == category.Id)?.ActualAmount ?? 0m))
            .ToArray();

        CategoryTrendChart.Series =
        [
            new LineSeries<double>
            {
                Name = category.Name,
                Values = values,
                Stroke = new SolidColorPaint(ChartTheme.Accent) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(ChartTheme.Accent.WithAlpha(0x30)),
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(ChartTheme.Accent),
                GeometryStroke = new SolidColorPaint(ChartTheme.Accent)
            }
        ];
        CategoryTrendChart.XAxes = [new Axis { Labels = MonthLabels, LabelsPaint = ChartTheme.MutedTextPaint(), SeparatorsPaint = null }];
        CategoryTrendChart.YAxes = [new Axis { Labeler = value => value.ToString("C0"), LabelsPaint = ChartTheme.MutedTextPaint(), SeparatorsPaint = ChartTheme.SeparatorPaint() }];
    }
}
