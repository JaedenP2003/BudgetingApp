namespace BudgetingApp.Core.Models;

/// <summary>
/// A single match pattern resolving to exactly one category. Never a join table —
/// a rule cannot point at two categories, so a matching transaction can never be
/// double-counted. When more than one rule matches the same transaction, the rule
/// with the lower Priority value wins; ties are broken by rule Id.
/// </summary>
public record CategoryRule(
    long Id,
    long CategoryId,
    MatchType MatchType,
    string Pattern,
    int Priority
);
