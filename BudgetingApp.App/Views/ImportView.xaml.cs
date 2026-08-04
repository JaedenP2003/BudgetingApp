using System.Windows;
using System.Windows.Controls;
using BudgetingApp.Core.Import;
using Microsoft.Win32;

namespace BudgetingApp.App.Views;

public partial class ImportView : UserControl
{
    private readonly AppServices _services;

    public ImportView(AppServices services)
    {
        _services = services;
        InitializeComponent();
        AccountBox.ItemsSource = _services.Accounts.GetAll();
    }

    private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (AccountBox.SelectedValue is not long accountId)
        {
            ResultPanel.Visibility = Visibility.Visible;
            ResultSummaryText.Text = "Choose an account before importing a file.";
            ResultErrorHeaderText.Text = string.Empty;
            RowErrorsGrid.ItemsSource = null;
            return;
        }

        var dialog = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var flipSign = FlipSignCheckBox.IsChecked == true;
            var result = _services.ImportService.ImportAndCategorize(dialog.FileName, accountId, flipSign);
            ShowResult(result);
        }
        catch (UnrecognizedCsvFormatException ex)
        {
            ResultPanel.Visibility = Visibility.Visible;
            ResultSummaryText.Text = $"Could not import this file: {ex.Message}";
            ResultErrorHeaderText.Text = string.Empty;
            RowErrorsGrid.ItemsSource = null;
        }
    }

    private void ShowResult(ImportResult result)
    {
        ResultPanel.Visibility = Visibility.Visible;
        var categorizedCount = result.ImportedCount - result.UncategorizedCount;
        ResultSummaryText.Text =
            $"Imported {result.ImportedCount} transaction(s) — {categorizedCount} auto-categorized, {result.UncategorizedCount} left uncategorized for manual review.";

        if (result.HasErrors)
        {
            ResultErrorHeaderText.Text = $"{result.RowErrors.Count} row(s) could not be parsed and were skipped:";
            RowErrorsGrid.ItemsSource = result.RowErrors;
        }
        else
        {
            ResultErrorHeaderText.Text = string.Empty;
            RowErrorsGrid.ItemsSource = null;
        }
    }
}
