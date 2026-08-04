using System.Text.RegularExpressions;
using BudgetingApp.Core.Models;
using BudgetingApp.Core.Storage;
using MatchType = BudgetingApp.Core.Models.MatchType;

namespace BudgetingApp.Core.Categorization;

public record CategorizationResult(int CategorizedCount, int UncategorizedCount);

/// <summary>
/// Applies category_rules to transactions by description. Rules are tried in
/// priority order (lowest Priority value first, then rule Id); the first match
/// wins and assigns exactly one category_id — never more than one rule's
/// category is applied to the same transaction. A transaction matching no rule
/// is left uncategorized (category_id stays NULL) for manual review, never
/// defaulted into a bucket.
/// </summary>
public class CategorizationEngine(CategoryRuleRepository ruleRepository, TransactionRepository transactionRepository)
{
    public CategorizationResult CategorizeUncategorized() => Categorize(transactionRepository.GetUncategorized());

    /// <summary>Categorizes only the given (already-uncategorized) transaction ids, e.g. right after an import.</summary>
    public CategorizationResult CategorizeTransactions(IReadOnlyCollection<long> transactionIds) =>
        Categorize(transactionRepository.GetByIds(transactionIds));

    private CategorizationResult Categorize(List<Models.Transaction> transactions)
    {
        var rules = ruleRepository.GetAll(); // already ordered by priority, id

        var categorizedCount = 0;
        foreach (var transaction in transactions)
        {
            var categoryId = MatchCategory(transaction.Description, rules);
            if (categoryId is null) continue;

            transactionRepository.SetCategory(transaction.Id, categoryId);
            categorizedCount++;
        }

        return new CategorizationResult(categorizedCount, transactions.Count - categorizedCount);
    }

    public static long? MatchCategory(string description, IReadOnlyList<CategoryRule> rulesByPriority)
    {
        foreach (var rule in rulesByPriority)
        {
            if (Matches(description, rule)) return rule.CategoryId;
        }
        return null;
    }

    private static bool Matches(string description, CategoryRule rule)
    {
        return rule.MatchType switch
        {
            MatchType.Contains => description.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            MatchType.Exact => string.Equals(description, rule.Pattern, StringComparison.OrdinalIgnoreCase),
            MatchType.Regex => TryRegexMatch(description, rule.Pattern),
            _ => false
        };
    }

    private static bool TryRegexMatch(string description, string pattern)
    {
        try
        {
            return Regex.IsMatch(description, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            // An invalid or runaway regex rule shouldn't crash the whole import — treat as no match.
            return false;
        }
    }
}
