using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class SearchConsoleTests
{
    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    private SearchConsoleService CreateService(NewsDbContext db)
    {
        var configMock = new Mock<IConfiguration>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var indexNowMock = new Mock<IIndexNowService>();
        indexNowMock.Setup(i => i.GetConfiguration())
            .Returns(("test-key-12345", "newsstand.ng", "https://newsstand.ng/test-key-12345.txt"));

        return new SearchConsoleService(
            db,
            configMock.Object,
            httpClientFactoryMock.Object,
            indexNowMock.Object,
            NullLogger<SearchConsoleService>.Instance);
    }

    [Fact]
    public async Task GetSearchAnalyticsAsync_ReturnsValidSummary_AndDailyTrends()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        db.Articles.Add(new Article
        {
            Id = "art-1",
            Title = "Central Bank of Nigeria Adjusts Monetary Policy Rate",
            Slug = "cbn-adjusts-interest-rate-8f2a1b",
            Url = "https://example.com/cbn-rate",
            Category = "business",
            PublishedAt = DateTime.UtcNow.AddHours(-10)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetSearchAnalyticsAsync(days: 7, dimension: "query", limit: 20);

        Assert.NotNull(result);
        Assert.Equal(7, result.Days);
        Assert.True(result.TotalClicks > 0);
        Assert.True(result.TotalImpressions > result.TotalClicks);
        Assert.True(result.AverageCtr > 0);
        Assert.NotEmpty(result.DailyTrends);
        Assert.Equal(7, result.DailyTrends.Count);
        Assert.NotEmpty(result.TopQueries);
        Assert.NotEmpty(result.TopPages);
        Assert.NotEmpty(result.DeviceBreakdown);

        // Mobile dominance verification (Nigerian traffic profile)
        var mobile = result.DeviceBreakdown.FirstOrDefault(d => d.Device == "MOBILE");
        Assert.NotNull(mobile);
        Assert.True(mobile.PercentageShare > 70.0);
    }

    [Fact]
    public async Task GetCrawlDiagnosticsAsync_EvaluatesTotalUrls_AndReturnsSitemaps()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        db.Articles.Add(new Article
        {
            Id = "art-2",
            Title = "Nigeria Exchange Group Posts Record Gains",
            Slug = "ngx-posts-record-gains-91b2c3",
            Url = "https://example.com/ngx",
            Category = "business",
            PublishedAt = DateTime.UtcNow.AddHours(-5)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var diag = await service.GetCrawlDiagnosticsAsync();

        Assert.NotNull(diag);
        Assert.True(diag.TotalDiscoveredUrls >= 37); // At least all 37 Nigerian states
        Assert.True(diag.TotalIndexedUrls > 0);
        Assert.True(diag.IndexationRate >= 90.0);
        Assert.NotEmpty(diag.IndexationBreakdown);
        Assert.NotEmpty(diag.Sitemaps);
        Assert.Equal(6, diag.Sitemaps.Count);
        Assert.Equal("newsstand.ng", diag.IndexNow.Host);
        Assert.Equal("test-key-12345", diag.IndexNow.Key);
    }

    [Fact]
    public async Task InspectUrlAsync_ValidArticle_ReturnsPass_AndRichResults()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var article = new Article
        {
            Id = "art-3",
            Title = "Lagos Blue Line Rail Launches Phase Two",
            Slug = "lagos-blue-line-rail-phase-two",
            Url = "https://example.com/blue-line",
            Category = "metro",
            PublishedAt = DateTime.UtcNow.AddHours(-3)
        };
        var enrichment = new ArticleEnrichment
        {
            ArticleId = "art-3",
            KeyTakeawaysJson = "[\"Phase 2 opens to commuters.\"]",
            FaqsJson = "[{\"question\":\"When does it run?\",\"answer\":\"Daily from 6am to 10pm.\"}]"
        };
        db.Articles.Add(article);
        db.ArticleEnrichments.Add(enrichment);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.InspectUrlAsync("/news/lagos-blue-line-rail-phase-two");

        Assert.NotNull(result);
        Assert.Equal("PASS", result.Verdict);
        Assert.True(result.IsIndexed);
        Assert.Equal("URL is on Google", result.IndexingState);
        Assert.Equal("https://newsstand.ng/news/lagos-blue-line-rail-phase-two", result.UserDeclaredCanonical);
        Assert.Contains("NewsArticle", result.RichResults);
        Assert.Contains("FAQPage", result.RichResults);
        Assert.Contains("BreadcrumbList", result.RichResults);
    }

    [Fact]
    public async Task InspectUrlAsync_EvergreenTracker_ReturnsPass_AndDatasetSchema()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var service = CreateService(db);

        // English tracker
        var resultEn = await service.InspectUrlAsync("/naira-exchange-rate");
        Assert.Equal("PASS", resultEn.Verdict);
        Assert.True(resultEn.IsIndexed);
        Assert.Contains("Dataset", resultEn.RichResults);
        Assert.Contains("FAQPage", resultEn.RichResults);

        // Pidgin tracker
        var resultPcm = await service.InspectUrlAsync("/pcm/fuel-price-nigeria");
        Assert.Equal("PASS", resultPcm.Verdict);
        Assert.True(resultPcm.IsIndexed);
        Assert.Contains("Dataset", resultPcm.RichResults);
    }

    [Fact]
    public async Task InspectUrlAsync_StateHub_ReturnsPass_AndPlaceSchema()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var service = CreateService(db);

        var result = await service.InspectUrlAsync("/state/lagos");
        Assert.Equal("PASS", result.Verdict);
        Assert.True(result.IsIndexed);
        Assert.Contains("Place", result.RichResults);
        Assert.Contains("AdministrativeArea", result.RichResults);
    }

    [Fact]
    public async Task InspectUrlAsync_NonExistentUrl_ReturnsFail_And404State()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var service = CreateService(db);

        var result = await service.InspectUrlAsync("/news/non-existent-story-12345");
        Assert.Equal("FAIL", result.Verdict);
        Assert.False(result.IsIndexed);
        Assert.Contains("404", result.IndexingState);
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task InspectUrlAsync_RobotsDisallowed_ReturnsFail()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var service = CreateService(db);

        var result = await service.InspectUrlAsync("/api/v1/articles");
        Assert.Equal("FAIL", result.Verdict);
        Assert.Equal("Disallowed", result.RobotsTxtState);
        Assert.False(result.IsIndexed);
    }

    [Fact]
    public async Task SeoController_SearchConsoleEndpoints_ReturnOk()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var service = CreateService(db);
        var controller = new SeoController(db, NullLogger<SeoController>.Instance, service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // 1. Search Analytics
        var analyticsResult = await controller.GetSearchAnalytics(days: 28, dimension: "query", limit: 30);
        var okAnalytics = Assert.IsType<OkObjectResult>(analyticsResult);
        var summary = Assert.IsType<SearchPerformanceSummaryDto>(okAnalytics.Value);
        Assert.Equal(28, summary.Days);

        // 2. Crawl Diagnostics
        var diagResult = await controller.GetCrawlDiagnostics();
        var okDiag = Assert.IsType<OkObjectResult>(diagResult);
        var diag = Assert.IsType<CrawlDiagnosticsDto>(okDiag.Value);
        Assert.True(diag.TotalDiscoveredUrls > 0);

        // 3. Inspect URL
        var inspectResult = await controller.InspectUrl(new UrlInspectionRequestDto { Url = "/naira-exchange-rate" });
        var okInspect = Assert.IsType<OkObjectResult>(inspectResult);
        var inspect = Assert.IsType<UrlInspectionResultDto>(okInspect.Value);
        Assert.Equal("PASS", inspect.Verdict);

        // 4. Sitemaps Status
        var sitemapsResult = await controller.GetSitemapsStatus();
        var okSitemaps = Assert.IsType<OkObjectResult>(sitemapsResult);
        var sitemaps = Assert.IsType<List<SitemapDiagnosticDto>>(okSitemaps.Value);
        Assert.Equal(6, sitemaps.Count);
    }
}
