using System.Windows;
using System.Windows.Controls;
using BudgetingApp.Core.Models;

namespace BudgetingApp.App.Views;

public partial class CategoryRulesView : UserControl
{
    private readonly AppServices _services;

    public CategoryRulesView(AppServices services)
    {
        _services = services;
        InitializeComponent();

        CategoryBox.ItemsSource = _services.Categories.GetAll();
        MatchTypeBox.ItemsSource = Enum.GetValues<MatchType>();

        LoadRules();
    }

    private void LoadRules()
    {
        var categoryNames = _services.Categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        RulesGrid.ItemsSource = _services.CategoryRules.GetAll()
            .Select(r => CategoryRuleRow.From(r, categoryNames))
            .ToList();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;

        if (CategoryBox.SelectedValue is not long categoryId)
        {
            ValidationText.Text = "Choose a category.";
            return;
        }
        if (MatchTypeBox.SelectedItem is not MatchType matchType)
        {
            ValidationText.Text = "Choose a match type.";
            return;
        }
        if (string.IsNullOrWhiteSpace(PatternBox.Text))
        {
            ValidationText.Text = "Pattern is required.";
            return;
        }
        if (!int.TryParse(PriorityBox.Text, out var priority))
        {
            ValidationText.Text = "Priority must be a whole number.";
            return;
        }

        _services.CategoryRules.Add(categoryId, matchType, PatternBox.Text.Trim(), priority);

        PatternBox.Clear();
        CategoryBox.SelectedIndex = -1;
        MatchTypeBox.SelectedIndex = -1;
        PriorityBox.Text = "100";

        LoadRules();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not CategoryRuleRow row) return;
        _services.CategoryRules.Delete(row.Id);
        LoadRules();
    }
}
