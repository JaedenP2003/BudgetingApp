using System.ComponentModel;
using System.Runtime.CompilerServices;
using BudgetingApp.Core.Models;
using BudgetingApp.Core.Storage;

namespace BudgetingApp.App.Views;

/// <summary>Binds a Transaction to the grid; setting CategoryId writes straight through to
/// the database so a manual override takes effect immediately, no separate "save" step.</summary>
public class TransactionRow(Transaction transaction, TransactionRepository repository, string accountName) : INotifyPropertyChanged
{
    public long Id { get; } = transaction.Id;
    public DateOnly PostingDate { get; } = transaction.PostingDate;
    public string Description { get; } = transaction.Description;
    public decimal Amount { get; } = transaction.Amount;
    public string AccountName { get; } = accountName;
    public string SourceFile { get; } = transaction.SourceFile;

    private long? _categoryId = transaction.CategoryId;
    public long? CategoryId
    {
        get => _categoryId;
        set
        {
            if (_categoryId == value) return;
            _categoryId = value;
            repository.SetCategory(Id, value);
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
