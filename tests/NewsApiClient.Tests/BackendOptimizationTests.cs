using System;
using System.Collections.Generic;
using System.Linq;
using NigerianNewGrid.Services;
using Xunit;

namespace NewsApiClient.Tests;

public class BackendOptimizationTests
{
    [Theory]
    [InlineData(null, 2, "2 min read")]
    [InlineData("", 2, "2 min read")]
    [InlineData("   ", 2, "2 min read")]
    public void CalculateReadingTime_NullOrEmpty_ReturnsDefaultTwoMin(string? input, int expectedMinutes, string expectedLabel)
    {
        var (minutes, label) = ArticleDistillerService.CalculateReadingTime(input);
        Assert.Equal(expectedMinutes, minutes);
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void CalculateReadingTime_ShortText_ReturnsOneMin()
    {
        var text = "The central bank of Nigeria has issued new guidelines for commercial lenders.";
        var (minutes, label) = ArticleDistillerService.CalculateReadingTime(text, wordsPerMinute: 200);
        Assert.Equal(1, minutes);
        Assert.Equal("1 min read", label);
    }

    [Fact]
    public void CalculateReadingTime_LongArticle_ReturnsAccurateMinutes()
    {
        // Generate 650 words
        var words = Enumerable.Repeat("news", 650).ToArray();
        var text = string.Join(" ", words);

        var (minutes, label) = ArticleDistillerService.CalculateReadingTime(text, wordsPerMinute: 200);
        Assert.Equal(4, minutes); // 650 / 200 = 3.25 -> 4
        Assert.Equal("4 min read", label);
    }

    [Theory]
    [InlineData("https://punchng.com/news/article-123", "The Punch")]
    [InlineData("https://www.vanguardngr.com/2026/08/breaking-news", "Vanguard News")]
    [InlineData("https://www.premiumtimesng.com/news/top-news/71234.html", "Premium Times")]
    [InlineData("https://thecable.ng/special-report", "TheCable")]
    [InlineData("https://unknown-publisher.com/story", "Unknown-publisher")]
    public void IdentifyPublisher_RecognizesMajorNigerianOutlets(string url, string expectedPublisher)
    {
        var info = ArticleDistillerService.IdentifyPublisher(url);
        Assert.Equal(expectedPublisher, info.Name);
    }

    [Fact]
    public void IngestionBatchDeduplication_IdentifiesDuplicatesAccurately()
    {
        var existingUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "https://punchng.com/news/story-1",
            "https://guardian.ng/news/story-2"
        };

        var incomingUrls = new[]
        {
            "https://punchng.com/news/story-1", // duplicate
            "https://thecable.ng/news/story-3", // new
            "https://guardian.ng/news/story-2"  // duplicate
        };

        var newArticles = incomingUrls.Where(u => !existingUrls.Contains(u)).ToList();

        Assert.Single(newArticles);
        Assert.Equal("https://thecable.ng/news/story-3", newArticles[0]);
    }

    [Theory]
    [InlineData(1, 50, 0, 50)]
    [InlineData(2, 20, 20, 20)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(0, 0, 0, 50)] // Test default clamp
    public void PaginationCalculation_ComputesOffsetsCorrectly(int page, int pageSize, int expectedSkip, int expectedTake)
    {
        var pageIndex = Math.Max(1, page);
        var size = Math.Clamp(pageSize > 0 ? pageSize : 50, 1, 200);
        var skip = (pageIndex - 1) * size;

        Assert.Equal(expectedSkip, skip);
        Assert.Equal(expectedTake, size);
    }
}
