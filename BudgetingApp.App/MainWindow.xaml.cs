using System.Windows;
using System.Windows.Controls;
using BudgetingApp.App.Views;

namespace BudgetingApp.App;

public partial class MainWindow : Window
{
    private readonly AppServices _services;

    public MainWindow(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ShowImport();
    }

    private void ImportNavButton_Click(object sender, RoutedEventArgs e) => ShowImport();
    private void AccountsNavButton_Click(object sender, RoutedEventArgs e) => ShowAccounts();
    private void TransactionsNavButton_Click(object sender, RoutedEventArgs e) => ShowTransactions();
    private void RecurringNavButton_Click(object sender, RoutedEventArgs e) => ShowRecurringExpenses();
    private void RulesNavButton_Click(object sender, RoutedEventArgs e) => ShowRules();
    private void SummaryNavButton_Click(object sender, RoutedEventArgs e) => ShowSummary();
    private void TrendsNavButton_Click(object sender, RoutedEventArgs e) => ShowTrends();

    private void ShowImport()
    {
        SetActive(ImportNavButton);
        ContentArea.Content = new ImportView(_services);
    }

    private void ShowAccounts()
    {
        SetActive(AccountsNavButton);
        ContentArea.Content = new AccountsView(_services);
    }

    private void ShowTransactions()
    {
        SetActive(TransactionsNavButton);
        ContentArea.Content = new TransactionsView(_services);
    }

    private void ShowRecurringExpenses()
    {
        SetActive(RecurringNavButton);
        ContentArea.Content = new RecurringExpensesView(_services);
    }

    private void ShowRules()
    {
        SetActive(RulesNavButton);
        ContentArea.Content = new CategoryRulesView(_services);
    }

    private void ShowSummary()
    {
        SetActive(SummaryNavButton);
        ContentArea.Content = new MonthlySummaryView(_services);
    }

    private void ShowTrends()
    {
        SetActive(TrendsNavButton);
        ContentArea.Content = new TrendsView(_services);
    }

    private void SetActive(Button active)
    {
        foreach (var button in new[] { ImportNavButton, AccountsNavButton, TransactionsNavButton, RecurringNavButton, RulesNavButton, SummaryNavButton, TrendsNavButton })
        {
            button.Style = (Style)FindResource(button == active ? "ActiveNavButtonStyle" : "NavButtonStyle");
        }
    }
}
