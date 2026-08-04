using System.Windows;
using System.Windows.Controls;
using BudgetingApp.Core.Models;

namespace BudgetingApp.App.Views;

public partial class AccountsView : UserControl
{
    private readonly AppServices _services;

    public AccountsView(AppServices services)
    {
        _services = services;
        InitializeComponent();

        TypeBox.ItemsSource = Enum.GetValues<AccountType>();

        LoadAccounts();
    }

    private void LoadAccounts()
    {
        AccountsGrid.ItemsSource = _services.Accounts.GetAll()
            .Select(a => new AccountRow(a.Id, a.Name, a.Type))
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
        if (TypeBox.SelectedItem is not AccountType type)
        {
            ValidationText.Text = "Choose a type.";
            return;
        }

        _services.Accounts.Add(NameBox.Text.Trim(), type);

        NameBox.Clear();
        TypeBox.SelectedIndex = -1;

        LoadAccounts();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not AccountRow row) return;

        if (_services.Transactions.AnyForAccount(row.Id))
        {
            ValidationText.Text = $"Can't delete \"{row.Name}\" — it still has imported transactions.";
            return;
        }

        _services.Accounts.Delete(row.Id);
        LoadAccounts();
    }
}
