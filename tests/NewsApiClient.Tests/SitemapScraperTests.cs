using System;
using System.Net.Http;
using System.Xml.Linq;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging.Abstractions;
using NewsScraperService.Models;
using NewsScraperService.Scrapers;
using NewsScraperService.Services;
using Xunit;

namespace NewsApiClient.Tests;

public class SitemapScraperTests
{
    private class TestSitemapScraper : SitemapScraperBase
    {
        public TestSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null)
            : base(http, extractor, frontier) { }

        public override string SourceId => "test-sitemap";
        public override string SourceName => "Test Publisher";
        public override string SitemapUrl => "https://example.com/news-sitemap.xml";
    }

    [Fact]
    public void GoogleNewsSitemap_ParsesLocationsAndPublicationDatesAndTitlesCorrectly()
    {
        // Arrange
        var xml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
                xmlns:news="http://www.google.com/schemas/sitemap-news/0.9"
                xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">
            <url>
                <loc>https://punchng.com/tinubu-approves-infrastructure-fund/</loc>
                <news:news>
                    <news:publication>
                        <news:name>The Punch</news:name>
                        <news:language>en</news:language>
                    </news:publication>
                    <news:publication_date>2026-08-16T12:30:00Z</news:publication_date>
                    <news:title>Tinubu approves multi-billion naira infrastructure fund for healthcare</news:title>
                </news:news>
                <image:image>
                    <image:loc>https://punchng.com/wp-content/uploads/2026/08/tinubu-presidency.jpg</image:loc>
                </image:image>
            </url>
            <url>
                <loc>https://punchng.com/super-eagles-qualify-afcon/</loc>
                <news:news>
                    <news:publication_date>2026-08-16T14:15:00+01:00</news:publication_date>
                    <news:title>Super Eagles secure qualification for AFCON knockout rounds</news:title>
                </news:news>
            </url>
        </urlset>
        """;

        using var client = new HttpClient();
        var scraper = new TestSitemapScraper(client);
        var doc = XDocument.Parse(xml);

        // Act
        var articles = scraper.ParseSitemapDocument(doc);

        // Assert
        Assert.Equal(2, articles.Count);

        var first = articles[0];
        Assert.Equal("https://punchng.com/tinubu-approves-infrastructure-fund/", first.Url);
        Assert.Equal("Tinubu approves multi-billion naira infrastructure fund for healthcare", first.Title);
        Assert.Equal(new DateTime(2026, 8, 16, 12, 30, 0, DateTimeKind.Utc), first.PublishedAt);
        Assert.Equal("https://punchng.com/wp-content/uploads/2026/08/tinubu-presidency.jpg", first.ImageUrl);
        Assert.Equal("Test Publisher", first.Source);

        var second = articles[1];
        Assert.Equal("https://punchng.com/super-eagles-qualify-afcon/", second.Url);
        Assert.Equal("Super Eagles secure qualification for AFCON knockout rounds", second.Title);
        Assert.Equal(new DateTime(2026, 8, 16, 13, 15, 0, DateTimeKind.Utc), second.PublishedAt);
    }

    [Fact]
    public void StandardXmlSitemap_ParsesLocationsAndLastModCorrectly()
    {
        // Arrange
        var xml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            <url>
                <loc>https://vanguardngr.com/business-cbn-interest-rate/</loc>
                <lastmod>2026-08-16T10:00:00Z</lastmod>
            </url>
            <url>
                <loc>https://vanguardngr.com/tech-startup-funding-record/</loc>
                <lastmod>2026-08-16T11:45:00Z</lastmod>
            </url>
        </urlset>
        """;

        using var client = new HttpClient();
        var scraper = new TestSitemapScraper(client);
        var doc = XDocument.Parse(xml);

        // Act
        var articles = scraper.ParseSitemapDocument(doc);

        // Assert
        Assert.Equal(2, articles.Count);
        Assert.Equal("https://vanguardngr.com/business-cbn-interest-rate/", articles[0].Url);
        Assert.Equal(new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc), articles[0].PublishedAt);
    }

    [Fact]
    public void UrlFrontierManager_NormalizesTrackingParametersAndAnchors()
    {
        // Arrange & Act
        var url1 = "https://punchng.com/article-123/?utm_source=rss&utm_medium=feed&utm_campaign=daily#comments";
        var normalized1 = UrlFrontierManager.NormalizeUrl(url1);

        var url2 = "https://dailypost.ng/news/cbn-update?fbclid=IwAR3Xyz&category=economy&ref=homepage";
        var normalized2 = UrlFrontierManager.NormalizeUrl(url2);

        // Assert
        Assert.Equal("https://punchng.com/article-123", normalized1);
        Assert.Equal("https://dailypost.ng/news/cbn-update?category=economy", normalized2);
    }

    [Fact]
    public void UrlFrontierManager_TracksAndDeduplicatesSeenUrls()
    {
        // Arrange
        var frontier = new UrlFrontierManager(TimeSpan.FromHours(1));
        var url = "https://thecable.ng/breaking-news-report?utm_source=newsletter";

        // Act & Assert
        Assert.False(frontier.IsUrlSeen(url));
        Assert.True(frontier.MarkUrlSeen(url)); // First time seen -> returns true
        Assert.True(frontier.IsUrlSeen(url)); // Now seen -> returns true
        Assert.False(frontier.MarkUrlSeen(url)); // Second attempt -> returns false
    }

    [Fact]
    public void UrlFrontierManager_FingerprintsSyndicatedTitles()
    {
        // Arrange
        var title1 = "CBN Raises Monetary Policy Rate by 50bps to 27%!";
        var title2 = "cbn raises monetary policy rate by 50bps to 27";

        // Act
        var fp1 = UrlFrontierManager.GenerateTitleFingerprint(title1);
        var fp2 = UrlFrontierManager.GenerateTitleFingerprint(title2);

        // Assert
        Assert.Equal(fp1, fp2);
        Assert.Equal("cbn raises monetary policy rate by 50bps to 27", fp1);
    }

    [Fact]
    public void ArticleContentExtractor_ExtractsJsonLdNewsArticle()
    {
        // Arrange
        var html = """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Fallback Title</title>
            <script type="application/ld+json">
            {
                "@context": "https://schema.org",
                "@type": "NewsArticle",
                "headline": "Dangote Refinery Begins Fuel Distribution to Marketers",
                "description": "The 650,000 bpd refinery has commenced loading petroleum trucks across Nigeria.",
                "articleBody": "Lekki, Lagos - The management of Dangote Petroleum Refinery on Sunday officially commenced the direct distribution of Premium Motor Spirit (PMS) and diesel to oil marketers across Nigeria.",
                "datePublished": "2026-08-16T09:00:00Z",
                "image": "https://example.com/images/dangote-trucks.jpg",
                "articleSection": "Business"
            }
            </script>
        </head>
        <body>
            <article>
                <p>Boilerplate body that should be superseded by structured JSON-LD.</p>
            </article>
        </body>
        </html>
        """;

        var extractor = new ArticleContentExtractor(NullLogger<ArticleContentExtractor>.Instance);

        // Act
        var article = extractor.ExtractFromHtml(html, "https://example.com/dangote-fuel", "Premium Times");

        // Assert
        Assert.NotNull(article);
        Assert.Equal("Dangote Refinery Begins Fuel Distribution to Marketers", article.Title);
        Assert.Equal("The 650,000 bpd refinery has commenced loading petroleum trucks across Nigeria.", article.Summary);
        Assert.Contains("Lekki, Lagos - The management of Dangote Petroleum Refinery", article.Content);
        Assert.Equal("https://example.com/images/dangote-trucks.jpg", article.ImageUrl);
        Assert.Equal(new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc), article.PublishedAt);
        Assert.Equal("Business", article.Category);
    }

    [Fact]
    public void ArticleContentExtractor_ExtractsOpenGraphAndTwitterMeta()
    {
        // Arrange
        var html = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta property="og:title" content="Nigeria Wins 4 Gold Medals at World Athletics" />
            <meta property="og:description" content="Nigerian athletes shone brightly in the 4x100m relays and hurdles events." />
            <meta property="og:image" content="https://example.com/athletes-celebrate.jpg" />
            <meta property="article:published_time" content="2026-08-16T15:00:00Z" />
        </head>
        <body>
            <div>
                <h1>Heading inside body</h1>
                <p>Athletes celebrated memorable victories in front of thousands of fans in Tokyo.</p>
            </div>
        </body>
        </html>
        """;

        var extractor = new ArticleContentExtractor(NullLogger<ArticleContentExtractor>.Instance);

        // Act
        var article = extractor.ExtractFromHtml(html, "https://example.com/athletics-win", "Vanguard");

        // Assert
        Assert.NotNull(article);
        Assert.Equal("Nigeria Wins 4 Gold Medals at World Athletics", article.Title);
        Assert.Equal("Nigerian athletes shone brightly in the 4x100m relays and hurdles events.", article.Summary);
        Assert.Equal("https://example.com/athletes-celebrate.jpg", article.ImageUrl);
        Assert.Equal(new DateTime(2026, 8, 16, 15, 0, 0, DateTimeKind.Utc), article.PublishedAt);
    }

    [Fact]
    public void ArticleContentExtractor_SanitizesBoilerplateAndUnwantedLines()
    {
        // Arrange
        var rawContent = """
        The Federal Government has launched the national youth empowerment grant scheme.
        READ ALSO: Previous Youth Grants Disbursed in 2025
        Beneficiaries will receive startup tools and advisory support.
        Join our WhatsApp Channel for instant job alerts.
        All rights reserved. This material, and other digital content on this platform, may not be reproduced.
        """;

        // Act
        var sanitized = ArticleContentExtractor.SanitizeContentBody(rawContent);

        // Assert
        Assert.NotNull(sanitized);
        Assert.Contains("The Federal Government has launched the national youth empowerment grant scheme.", sanitized);
        Assert.Contains("Beneficiaries will receive startup tools and advisory support.", sanitized);
        Assert.DoesNotContain("READ ALSO:", sanitized);
        Assert.DoesNotContain("Join our WhatsApp Channel", sanitized);
        Assert.DoesNotContain("All rights reserved.", sanitized);
    }
}
