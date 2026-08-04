using System.Windows;
using System.Windows.Controls;

namespace BudgetingApp.App.Views;

public partial class TransactionsView : UserControl
{
    private record AccountFilterOption(long? Id, string Name);

    private readonly AppServices _services;

    public TransactionsView(AppServices services)
    {
        _services = services;
        InitializeComponent();
        CategoryColumn.ItemsSource = _services.Categories.GetAll();

        var accounts = _services.Accounts.GetAll();
        var accountOptions = new List<AccountFilterOption> { new(null, "All accounts") };
        accountOptions.AddRange(accounts.Select(a => new AccountFilterOption(a.Id, a.Name)));
        AccountFilterBox.ItemsSource = accountOptions;
        AccountFilterBox.SelectedIndex = 0;

        LoadTransactions();
    }

    private void FilterChanged(object sender, RoutedEventArgs e) => LoadTransactions();
    private void FilterChanged(object sender, SelectionChangedEventArgs e) => LoadTransactions();

    private void RerunCategorizationButton_Click(object sender, RoutedEventArgs e)
    {
        _services.CategorizationEngine.CategorizeUncategorized();
        LoadTransactions();
    }

    private void LoadTransactions()
    {
        var accountNames = _services.Accounts.GetAll().ToDictionary(a => a.Id, a => a.Name);

        var transactions = UncategorizedOnlyCheckBox.IsChecked == true
            ? _services.Transactions.GetUncategorized()
            : _services.Transactions.GetAll();

        if (AccountFilterBox.SelectedValue is long accountId)
        {
            transactions = transactions.Where(t => t.AccountId == accountId).ToList();
        }

        TransactionsGrid.ItemsSource = transactions
            .Select(t => new TransactionRow(t, _services.Transactions, accountNames.GetValueOrDefault(t.AccountId, "?")))
            .ToList();
    }
}
