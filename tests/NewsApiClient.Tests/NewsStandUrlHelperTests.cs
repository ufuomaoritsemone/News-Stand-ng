using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApiClient.Tests;

public class NewsStandUrlHelperTests
{
    [Fact]
    public void GetArticleWebUrl_WithSlug_ReturnsCanonicalNewsstandUrl()
    {
        var slug = "fg-unveils-digital-economy-roadmap-seed01";
        var result = NewsStandUrlHelper.GetArticleWebUrl(slug: slug, id: "art-123", title: "FG Unveils Roadmap", fallbackUrl: "https://punchng.com/news");

        Assert.Equal("https://newsstand.ng/news/fg-unveils-digital-economy-roadmap-seed01", result);
    }

    [Fact]
    public void GetArticleWebUrl_WithSlugWithSurroundingSlashes_TrimsCleanly()
    {
        var slug = "/fg-unveils-digital-economy-roadmap/";
        var result = NewsStandUrlHelper.GetArticleWebUrl(slug: slug);

        Assert.Equal("https://newsstand.ng/news/fg-unveils-digital-economy-roadmap", result);
    }

    [Fact]
    public void GetArticleWebUrl_WithoutSlug_WithId_ReturnsIdUrl()
    {
        var id = "art-abc-123";
        var result = NewsStandUrlHelper.GetArticleWebUrl(slug: null, id: id, title: null, fallbackUrl: null);

        Assert.Equal("https://newsstand.ng/news/art-abc-123", result);
    }

    [Fact]
    public void GetArticleWebUrl_WithoutSlugOrId_WithTitle_ReturnsSlugifiedUrl()
    {
        var title = "Super Eagles Defeat Ghana 2-1 in Friendly!";
        var result = NewsStandUrlHelper.GetArticleWebUrl(slug: null, id: null, title: title, fallbackUrl: null);

        Assert.Equal("https://newsstand.ng/news/super-eagles-defeat-ghana-2-1-in-friendly", result);
    }

    [Fact]
    public void GetArticleWebUrl_WithoutSlugIdOrTitle_ReturnsFallbackUrl()
    {
        var fallback = "https://guardian.ng/article/123";
        var result = NewsStandUrlHelper.GetArticleWebUrl(slug: null, id: null, title: null, fallbackUrl: fallback);

        Assert.Equal("https://guardian.ng/article/123", result);
    }

    [Fact]
    public void GetArticleWebUrl_WithAllNull_ReturnsProductionDomain()
    {
        var result = NewsStandUrlHelper.GetArticleWebUrl(slug: null, id: null, title: null, fallbackUrl: null);

        Assert.Equal("https://newsstand.ng", result);
    }

    [Fact]
    public void GetArticleWebUrl_FromBriefingItem_WithSlug_ReturnsCanonicalUrl()
    {
        var item = new BriefingItem
        {
            Id = "guid-1234",
            Title = "Central Bank Holds Policy Rates Steady",
            Slug = "cbn-holds-policy-rates-steady-2026",
            Url = "https://premiumtimesng.com/news/cbn-rate"
        };

        var result = NewsStandUrlHelper.GetArticleWebUrl(item);

        Assert.Equal("https://newsstand.ng/news/cbn-holds-policy-rates-steady-2026", result);
    }

    [Fact]
    public void GetArticleWebUrl_FromBriefingItem_WithoutSlug_UsesIdFallback()
    {
        var item = new BriefingItem
        {
            Id = "guid-5678",
            Title = "Tech Hub Ecosystem Booms Across Lagos",
            Url = "https://punchng.com/tech-hub"
        };

        var result = NewsStandUrlHelper.GetArticleWebUrl(item);

        Assert.Equal("https://newsstand.ng/news/guid-5678", result);
    }

    [Fact]
    public void Slugify_FormatsCleanly()
    {
        var text = "  Lagos & Abuja: New Metro Rail Line Opens — 2026!  ";
        var slug = NewsStandUrlHelper.Slugify(text);

        Assert.Equal("lagos-abuja-new-metro-rail-line-opens-2026", slug);
    }
}
