using BudgetingApp.Core.Models;

namespace BudgetingApp.App.Views;

public record CategoryRuleRow(long Id, string CategoryName, MatchType MatchType, string Pattern, int Priority)
{
    public static CategoryRuleRow From(CategoryRule rule, IReadOnlyDictionary<long, string> categoryNames) =>
        new(rule.Id, categoryNames.GetValueOrDefault(rule.CategoryId, "?"), rule.MatchType, rule.Pattern, rule.Priority);
}
