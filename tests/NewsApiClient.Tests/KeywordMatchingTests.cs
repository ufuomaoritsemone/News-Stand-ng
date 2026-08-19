using System;
using System.Collections.Generic;
using NigerianNewGrid.Services;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApiClient.Tests;

public class KeywordMatchingTests
{
    private readonly KeywordMatchingService _matcher = new();

    [Fact]
    public void WordBoundary_MatchesWholeWord_DoesNotMatchSubstrings()
    {
        var articles = new List<BriefingItem>
        {
            new()
            {
                Id = "art-1",
                Title = "Naira appreciates against US dollar at official window",
                Summary = "The Nigerian currency gained momentum today.",
                PublishedAt = DateTime.UtcNow.AddMinutes(-15)
            },
            new()
            {
                Id = "art-2",
                Title = "Nairaland tech thread gains 500 new replies",
                Summary = "Popular forum community discusses AI development.",
                PublishedAt = DateTime.UtcNow.AddMinutes(-10)
            }
        };

        var keywords = new List<string> { "Naira" };
        var matches = _matcher.EvaluateFreshArticles(
            articles, 
            keywords, 
            publishedAfterUtc: DateTime.UtcNow.AddHours(-1),
            excludedArticleIds: new HashSet<string>());

        Assert.Single(matches);
        Assert.Equal("art-1", matches[0].Article.Id);
        Assert.Equal("Naira", matches[0].MatchedKeyword);
    }

    [Fact]
    public void MultiWordPhrases_MatchesCorrectly()
    {
        var articles = new List<BriefingItem>
        {
            new()
            {
                Id = "art-sports",
                Title = "Super Eagles secure 3-0 victory in friendly match",
                Summary = "Nigeria's national team delivered a dominant performance.",
                PublishedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new()
            {
                Id = "art-economy",
                Title = "NNPCL clarifies new Fuel Price regime across retail outlets",
                Summary = "Petrol pricing adjustments explained to motorists.",
                PublishedAt = DateTime.UtcNow.AddMinutes(-20)
            }
        };

        var keywords = new List<string> { "Super Eagles", "Fuel Price" };
        var matches = _matcher.EvaluateFreshArticles(
            articles, 
            keywords, 
            publishedAfterUtc: DateTime.UtcNow.AddHours(-1),
            excludedArticleIds: new HashSet<string>());

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.Article.Id == "art-sports" && m.MatchedKeyword.Equals("Super Eagles", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(matches, m => m.Article.Id == "art-economy" && m.MatchedKeyword.Equals("Fuel Price", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CaseInsensitive_MatchesRegardlessOfCasing()
    {
        var articles = new List<BriefingItem>
        {
            new()
            {
                Id = "art-tinubu",
                Title = "President Tinubu chairs Federal Executive Council meeting in Abuja",
                Summary = "Cabinet ministers briefed the president on national infrastructure projects.",
                PublishedAt = DateTime.UtcNow.AddMinutes(-30)
            }
        };

        var keywords = new List<string> { "tinubu" };
        var matches = _matcher.EvaluateFreshArticles(
            articles, 
            keywords, 
            publishedAfterUtc: DateTime.UtcNow.AddHours(-1),
            excludedArticleIds: new HashSet<string>());

        Assert.Single(matches);
        Assert.Equal("art-tinubu", matches[0].Article.Id);
    }

    [Fact]
    public void StrictFreshnessGate_ExcludesOldOrHistoricalStories()
    {
        var now = DateTime.UtcNow;
        var articles = new List<BriefingItem>
        {
            new()
            {
                Id = "fresh-story",
                Title = "EFCC arrests suspect in high-profile financial fraud investigation",
                Summary = "Operations conducted in Lagos early this morning.",
                PublishedAt = now.AddMinutes(-15) // Fresh
            },
            new()
            {
                Id = "old-story",
                Title = "EFCC secures conviction in previous court proceedings",
                Summary = "Historical case finalized last week.",
                PublishedAt = now.AddHours(-6) // Old - outside 2 hour freshness cutoff
            }
        };

        var keywords = new List<string> { "EFCC" };
        var cutoff = now.AddMinutes(-30);

        var matches = _matcher.EvaluateFreshArticles(
            articles, 
            keywords, 
            publishedAfterUtc: cutoff,
            excludedArticleIds: new HashSet<string>());

        Assert.Single(matches);
        Assert.Equal("fresh-story", matches[0].Article.Id);
    }

    [Fact]
    public void Deduplication_IgnoresAlreadyNotifiedArticleIds()
    {
        var articles = new List<BriefingItem>
        {
            new()
            {
                Id = "already-notified-1",
                Title = "Naira stabilizes at 1450 per dollar in foreign exchange market",
                Summary = "Market analysis shows steady liquidity.",
                PublishedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new()
            {
                Id = "brand-new-2",
                Title = "Naira liquidity surges as CBN clears foreign exchange backlog",
                Summary = "Central bank releases funds to commercial banks.",
                PublishedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        var notifiedIds = new HashSet<string> { "already-notified-1" };
        var keywords = new List<string> { "Naira" };

        var matches = _matcher.EvaluateFreshArticles(
            articles, 
            keywords, 
            publishedAfterUtc: DateTime.UtcNow.AddHours(-1),
            excludedArticleIds: notifiedIds);

        Assert.Single(matches);
        Assert.Equal("brand-new-2", matches[0].Article.Id);
    }

    [Fact]
    public void EmptyAndEdgeCases_HandledGracefully()
    {
        var articles = new List<BriefingItem>
        {
            new()
            {
                Id = "art-1",
                Title = "Some standard headline",
                Summary = "Standard news summary.",
                PublishedAt = DateTime.UtcNow
            }
        };

        // Empty keywords
        var emptyKeywords = _matcher.EvaluateFreshArticles(articles, new List<string>(), DateTime.UtcNow.AddHours(-1), new HashSet<string>());
        Assert.Empty(emptyKeywords);

        // Whitespace only keywords
        var whitespaceKeywords = _matcher.EvaluateFreshArticles(articles, new List<string> { "   ", "" }, DateTime.UtcNow.AddHours(-1), new HashSet<string>());
        Assert.Empty(whitespaceKeywords);

        // Empty articles
        var emptyArticles = _matcher.EvaluateFreshArticles(new List<BriefingItem>(), new List<string> { "Naira" }, DateTime.UtcNow.AddHours(-1), new HashSet<string>());
        Assert.Empty(emptyArticles);
    }
}
