using System.ComponentModel;
using System.Runtime.CompilerServices;
using BudgetingApp.Core.Models;
using BudgetingApp.Core.Storage;

namespace BudgetingApp.App.Views;

/// <summary>Binds a RecurringExpense to the grid; every field setter writes straight
/// through to the database, so an edit here — including setting an EndDate to stop
/// a subscription — takes effect immediately, no separate "save" step.</summary>
public class RecurringExpenseRow : INotifyPropertyChanged
{
    private readonly RecurringExpenseRepository _repository;
    private RecurringExpense _current;

    public RecurringExpenseRow(RecurringExpense expense, RecurringExpenseRepository repository)
    {
        _current = expense;
        _repository = repository;
    }

    public long Id => _current.Id;

    public string Name
    {
        get => _current.Name;
        set => Update(_current with { Name = value });
    }

    public long CategoryId
    {
        get => _current.CategoryId;
        set => Update(_current with { CategoryId = value });
    }

    public decimal ExpectedAmount
    {
        get => _current.ExpectedAmount;
        set => Update(_current with { ExpectedAmount = value });
    }

    public Cadence Cadence
    {
        get => _current.Cadence;
        set => Update(_current with { Cadence = value });
    }

    public DateOnly StartDate
    {
        get => _current.StartDate;
        set => Update(_current with { StartDate = value });
    }

    public DateOnly? EndDate
    {
        get => _current.EndDate;
        set => Update(_current with { EndDate = value });
    }

    /// <summary>Text-editable form of StartDate for the grid; invalid input is ignored, keeping the prior value.</summary>
    public string StartDateText
    {
        get => StartDate.ToString("yyyy-MM-dd");
        set
        {
            if (DateOnly.TryParse(value, out var parsed)) StartDate = parsed;
        }
    }

    /// <summary>Text-editable form of EndDate; blank clears it, invalid input is ignored.</summary>
    public string EndDateText
    {
        get => EndDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) { EndDate = null; return; }
            if (DateOnly.TryParse(value, out var parsed)) EndDate = parsed;
        }
    }

    public string? Note
    {
        get => _current.Note;
        set => Update(_current with { Note = value });
    }

    private void Update(RecurringExpense updated)
    {
        _current = updated;
        _repository.Update(updated);
        OnPropertyChanged(string.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
