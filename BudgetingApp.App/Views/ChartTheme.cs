using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace BudgetingApp.App.Views;

/// <summary>Sheikah Slate chart palette, matching Theme/AppTheme.xaml's brushes. LiveCharts
/// paints are set in code (not XAML resources), so the hex values are kept here once
/// rather than duplicated in every chart-owning view.</summary>
internal static class ChartTheme
{
    public static readonly SKColor Accent = new(0x4C, 0xE7, 0xEE);
    public static readonly SKColor Positive = new(0x37, 0xE6, 0xA6);
    public static readonly SKColor Negative = new(0xFF, 0x6E, 0x5A);
    public static readonly SKColor Text = new(0xE7, 0xF6, 0xF8);
    public static readonly SKColor MutedText = new(0x79, 0x96, 0xA3);
    public static readonly SKColor Separator = new(0x22, 0x32, 0x3F);

    /// <summary>Cycled per-slice for pie/ring charts, where categories are told apart by
    /// color rather than position. Cyan-adjacent tech palette, distinct at a glance.</summary>
    public static readonly SKColor[] CategoryPalette =
    [
        new(0x4C, 0xE7, 0xEE), // accent cyan
        new(0xFF, 0xB2, 0x38), // amber
        new(0xFF, 0x6E, 0x5A), // coral
        new(0x37, 0xE6, 0xA6), // teal-green
        new(0x8A, 0x7C, 0xF0), // muted violet
        new(0xE6, 0x5C, 0xC9), // magenta
        new(0x5C, 0x9C, 0xE6), // sky blue
        new(0xE6, 0xD4, 0x5C), // gold
        new(0x5C, 0xE6, 0xD4), // aqua
        new(0xE6, 0x8A, 0x5C), // burnt orange
        new(0x9C, 0xE6, 0x5C), // lime
        new(0xC0, 0xC8, 0xD4), // slate gray (Uncategorized, Misc, etc.)
    ];

    public static SolidColorPaint TextPaint() => new(Text);
    public static SolidColorPaint MutedTextPaint() => new(MutedText) { SKTypeface = SKTypeface.Default };
    public static SolidColorPaint SeparatorPaint() => new(Separator) { StrokeThickness = 1 };
}
