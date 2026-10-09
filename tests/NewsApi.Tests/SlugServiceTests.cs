using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class SlugServiceTests
{
    private readonly ISlugService _slugService = new SlugService();

    [Theory]
    [InlineData("Breaking: Federal Government Unveils Digital Economy Roadmap!", "breaking-federal-government-unveils-digital-economy-roadmap")]
    [InlineData("CBN Approves ₦1.5 Trillion FX Stabilization Fund", "cbn-approves-naira-1-5-trillion-fx-stabilization-fund")]
    [InlineData("Café & Crème: Lagos Fashion Week 2026", "cafe-creme-lagos-fashion-week-2026")]
    [InlineData("  Multiple   Spaces   and---Dashes  ", "multiple-spaces-and-dashes")]
    [InlineData("What??? Why!!! (Updated)", "what-why-updated")]
    public void GenerateSlug_NormalizesText_ToCleanKebabCase(string input, string expected)
    {
        var result = _slugService.GenerateSlug(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateSlug_RespectsMaxLength()
    {
        var longTitle = "A very long headline about political developments in Nigeria that exceeds the normal character boundary and needs truncation";
        var result = _slugService.GenerateSlug(longTitle, 40);

        Assert.True(result.Length <= 40);
        Assert.False(result.EndsWith('-'));
    }

    [Fact]
    public void GenerateArticleSlug_AppendsSixCharacterHash()
    {
        var title = "President Signs Landmark Tax Bill";
        var slug = _slugService.GenerateArticleSlug(title, "article_12345");

        Assert.StartsWith("president-signs-landmark-tax-bill-", slug);
        var parts = slug.Split('-');
        Assert.True(parts.Length >= 2);
        Assert.Equal(6, parts[^1].Length);
    }

    [Fact]
    public void GenerateArticleSlug_IsCollisionSafeForSameTitles()
    {
        var title = "Breaking News Story";
        var slug1 = _slugService.GenerateArticleSlug(title);
        var slug2 = _slugService.GenerateArticleSlug(title);

        Assert.NotEqual(slug1, slug2);
    }

    [Fact]
    public void GenerateExcerpt_StripsHtmlAndDecodesEntities()
    {
        var rawHtml = "<p>The <strong>Central Bank of Nigeria</strong> &amp; commercial banks announced new liquidity rules.</p>";
        var excerpt = _slugService.GenerateExcerpt(rawHtml, null, 300);

        Assert.Equal("The Central Bank of Nigeria & commercial banks announced new liquidity rules.", excerpt);
    }

    [Fact]
    public void GenerateExcerpt_CutsCleanlyAtSentenceBoundary()
    {
        var longContent = "The Federal Executive Council met on Wednesday in Abuja. Several infrastructure projects were approved for the South-West and North-Central regions. Ministers also discussed economic reforms. Further updates will follow later this evening.";
        var excerpt = _slugService.GenerateExcerpt(longContent, null, 120);

        Assert.EndsWith(".", excerpt);
        Assert.True(excerpt.Length <= 120);
        Assert.Contains("Council met on Wednesday in Abuja.", excerpt);
    }

    [Fact]
    public void GenerateExcerpt_FallsBackToWordBoundaryWithEllipsis()
    {
        var sentenceWithoutPeriod = "A continuous unbroken statement explaining various economic and monetary policies without standard punctuation across multiple paragraphs";
        var excerpt = _slugService.GenerateExcerpt(sentenceWithoutPeriod, null, 50);

        Assert.EndsWith("...", excerpt);
        Assert.True(excerpt.Length <= 50);
        Assert.DoesNotContain("  ", excerpt);
    }
}
