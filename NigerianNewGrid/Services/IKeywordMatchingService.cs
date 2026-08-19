using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public sealed record KeywordMatchResult(BriefingItem Article, string MatchedKeyword);

public interface IKeywordMatchingService
{
    /// <summary>
    /// Evaluates incoming news articles against active monitored keywords.
    /// Strictly enforces freshness rules so that only newly updated stories within the recency window trigger alerts.
    /// </summary>
    List<KeywordMatchResult> EvaluateFreshArticles(
        IEnumerable<BriefingItem> articles,
        IReadOnlyList<string> keywords,
        DateTime? publishedAfterUtc = null,
        IReadOnlySet<string>? excludedArticleIds = null);
}

public sealed class KeywordMatchingService : IKeywordMatchingService
{
    /// <summary>
    /// Maximum lookback window for breaking news alerts (2 hours).
    /// </summary>
    public static readonly TimeSpan MaxFreshnessWindow = TimeSpan.FromHours(2);

    public List<KeywordMatchResult> EvaluateFreshArticles(
        IEnumerable<BriefingItem> articles,
        IReadOnlyList<string> keywords,
        DateTime? publishedAfterUtc = null,
        IReadOnlySet<string>? excludedArticleIds = null)
    {
        var results = new List<KeywordMatchResult>();
        if (keywords == null || keywords.Count == 0 || articles == null)
            return results;

        var validKeywords = keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validKeywords.Count == 0)
            return results;

        var notifiedIds = excludedArticleIds ?? new HashSet<string>();

        // Determine effective freshness cutoff timestamp
        var baseline = publishedAfterUtc ?? DateTime.UtcNow.Subtract(MaxFreshnessWindow);
        var minAllowedTime = DateTime.UtcNow.Subtract(MaxFreshnessWindow);
        var effectiveCutoff = baseline < minAllowedTime ? minAllowedTime : baseline;

        // Build compiled word-boundary regex matching whole words/phrases
        // e.g. \b(Naira|Tinubu|EFCC|Fuel\ Price|Super\ Eagles)\b
        var escapedTokens = validKeywords.Select(Regex.Escape);
        var pattern = $@"\b({string.Join("|", escapedTokens)})\b";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var article in articles)
        {
            if (string.IsNullOrWhiteSpace(article.Id) || notifiedIds.Contains(article.Id))
                continue;

            // Strict Freshness Check:
            // Article must have a published timestamp and must be published at or after the effective cutoff
            if (article.PublishedAt.HasValue && article.PublishedAt.Value > DateTime.MinValue)
            {
                var articleUtc = article.PublishedAt.Value.ToUniversalTime();
                if (articleUtc < effectiveCutoff)
                {
                    // Story is too old for a fresh breaking notification
                    continue;
                }
            }

            var textToSearch = $"{article.Title} {article.Summary}";
            var match = regex.Match(textToSearch);
            if (match.Success)
            {
                results.Add(new KeywordMatchResult(article, match.Value));
            }
        }

        return results;
    }
}
