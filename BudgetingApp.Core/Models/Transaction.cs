namespace BudgetingApp.Core.Models;

public record Transaction(
    long Id,
    DateOnly PostingDate,
    string Description,
    decimal Amount,
    long? CategoryId,
    long AccountId,
    string SourceFile,
    string RawRow
);
