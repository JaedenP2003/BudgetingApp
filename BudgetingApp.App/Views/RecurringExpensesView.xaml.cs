using System.Windows;
using System.Windows.Controls;
using BudgetingApp.Core.Models;

namespace BudgetingApp.App.Views;

public partial class RecurringExpensesView : UserControl
{
    private readonly AppServices _services;

    public RecurringExpensesView(AppServices services)
    {
        _services = services;
        InitializeComponent();

        var categories = _services.Categories.GetAll();
        CategoryColumn.ItemsSource = categories;
        CategoryBox.ItemsSource = categories;

        var cadences = Enum.GetValues<Cadence>();
        CadenceColumn.ItemsSource = cadences;
        CadenceBox.ItemsSource = cadences;

        LoadExpenses();
    }

    private void LoadExpenses()
    {
        ExpensesGrid.ItemsSource = _services.RecurringExpenses.GetAll()
            .Select(e => new RecurringExpenseRow(e, _services.RecurringExpenses))
            .ToList();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ValidationText.Text = "Name is required.";
            return;
        }
        if (CategoryBox.SelectedValue is not long categoryId)
        {
            ValidationText.Text = "Choose a category.";
            return;
        }
        if (!decimal.TryParse(AmountBox.Text, out var amount))
        {
            ValidationText.Text = "Amount must be a number.";
            return;
        }
        if (CadenceBox.SelectedItem is not Cadence cadence)
        {
            ValidationText.Text = "Choose a cadence.";
            return;
        }
        if (!DateOnly.TryParse(StartDateBox.Text, out var startDate))
        {
            ValidationText.Text = "Start date must be a valid date (e.g. 2026-08-01).";
            return;
        }

        var note = string.IsNullOrWhiteSpace(NoteBox.Text) ? null : NoteBox.Text.Trim();
        _services.RecurringExpenses.Add(new RecurringExpense(0, NameBox.Text.Trim(), categoryId, amount, cadence, startDate, null, note));

        NameBox.Clear();
        AmountBox.Clear();
        StartDateBox.Clear();
        NoteBox.Clear();
        CategoryBox.SelectedIndex = -1;
        CadenceBox.SelectedIndex = -1;

        LoadExpenses();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not RecurringExpenseRow row) return;
        _services.RecurringExpenses.Delete(row.Id);
        LoadExpenses();
    }
}
