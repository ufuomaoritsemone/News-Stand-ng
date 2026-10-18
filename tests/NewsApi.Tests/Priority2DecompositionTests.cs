using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services;
using NewsScraperService;
using NewsScraperService.Models;
using NewsScraperService.Scrapers;
using NewsScraperService.Services;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApi.Tests;

public class Priority2DecompositionTests
{
    private static NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task BriefingService_CachesAndInvalidatesBriefingAtomically()
    {
        using var db = CreateInMemoryDb(Guid.NewGuid().ToString("N"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new BriefingService(db, cache, NullLogger<BriefingService>.Instance);

        db.Articles.Add(new Article
        {
            Id = "brief_1",
            Title = "Central Bank Announces New FX Framework",
            Summary = "Monetary policy update from Abuja.",
            Category = "Business",
            Source = "Punch Newspaper",
            PublishedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // 1. First call is a cache MISS
        var first = await service.GetBriefingsAsync(topPerCategory: 5, language: "English", isInternal: false);
        Assert.False(first.CacheHit);
        Assert.Single(first.Categories);
        Assert.Equal("Business", first.Categories[0].Category);

        // 2. Second call is a cache HIT
        var second = await service.GetBriefingsAsync(topPerCategory: 5, language: "English", isInternal: false);
        Assert.True(second.CacheHit);

        // 3. InvalidateBriefingCache evicts the token-bound cache entry
        service.InvalidateBriefingCache();

        var third = await service.GetBriefingsAsync(topPerCategory: 5, language: "English", isInternal: false);
        Assert.False(third.CacheHit);
    }

    [Fact]
    public async Task ArticleIngestionService_DeduplicatesAndResolvesTopicsAndInvalidatesCache()
    {
        using var db = CreateInMemoryDb(Guid.NewGuid().ToString("N"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var slugService = new SlugService();
        var briefingMock = new Mock<IBriefingService>();

        var ingestionService = new ArticleIngestionService(
            db,
            slugService,
            briefingMock.Object,
            NullLogger<ArticleIngestionService>.Instance);

        var request = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = "Super Eagles Qualify for AFCON Finals",
                    Summary = "Nigeria defeats rivals in qualifying match.",
                    Url = "https://punchng.com/super-eagles-qualify",
                    Source = "Punch Newspaper",
                    Category = "Sports",
                    Topics =
                    [
                        new ArticleTopicIngestItem { Name = "Super Eagles", Slug = "super-eagles", Type = "Organization", Confidence = 0.95f }
                    ]
                },
                // Duplicate URL in same batch
                new ArticleIngestItem
                {
                    Title = "Different Headline Same URL",
                    Url = "https://punchng.com/super-eagles-qualify",
                    Source = "Punch Newspaper",
                    Category = "Sports"
                }
            ]
        };

        var response = await ingestionService.IngestArticlesAsync(request);

        Assert.Equal(1, response.IngestedCount);
        Assert.Equal(1, response.SkippedDuplicateCount);
        Assert.Equal(1, await db.Articles.CountAsync());
        Assert.Equal(1, await db.Topics.CountAsync());
        Assert.Equal(1, await db.ArticleTopics.CountAsync());
        briefingMock.Verify(b => b.InvalidateBriefingCache(), Times.Once);
    }

    [Fact]
    public async Task DataSeeder_SeedsDefaultPublishersArticlesAndTopicsIdempotently()
    {
        using var db = CreateInMemoryDb(Guid.NewGuid().ToString("N"));
        var seeder = new DataSeeder(db, NullLogger<DataSeeder>.Instance, new SlugService());

        await seeder.SeedAsync();
        var firstSourceCount = await db.Sources.CountAsync();
        var firstTopicCount = await db.Topics.CountAsync();
        var firstArticleCount = await db.Articles.CountAsync();

        Assert.True(firstSourceCount > 0);
        Assert.True(firstTopicCount > 0);
        Assert.True(firstArticleCount > 0);

        // Second run should be idempotent
        await seeder.SeedAsync();
        Assert.Equal(firstTopicCount, await db.Topics.CountAsync());
        Assert.Equal(firstArticleCount, await db.Articles.CountAsync());
    }

    [Fact]
    public async Task ScraperWorker_CircuitBreaker_TripsAfterConsecutiveFailuresAndIsolatesTimeouts()
    {
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scraper:CircuitBreakerFailureThreshold"] = "3",
                ["Scraper:CircuitBreakerCooldownMinutes"] = "15"
            })
            .Build();

        var categorizer = new MlCategorizerEngine(NullLogger<MlCategorizerEngine>.Instance);
        var extractor = new ArticleContentExtractor(NullLogger<ArticleContentExtractor>.Instance);
        var frontier = new UrlFrontierManager();
        var enricherMock = new Mock<IArticleEnricher>();
        var entityExtractorMock = new Mock<IEntityExtractor>();
        var lifetimeMock = new Mock<IHostApplicationLifetime>();

        var worker = new ScraperWorker(
            NullLogger<ScraperWorker>.Instance,
            httpFactoryMock.Object,
            config,
            categorizer,
            extractor,
            frontier,
            enricherMock.Object,
            entityExtractorMock.Object,
            lifetimeMock.Object);

        // 1. Verify circuit breaker trips on the 3rd failure
        Assert.False(worker.IsSourceCircuitOpen("SlowSource", out _));
        worker.RecordSourceFailure("SlowSource", failureThreshold: 3);
        Assert.False(worker.IsSourceCircuitOpen("SlowSource", out _));
        worker.RecordSourceFailure("SlowSource", failureThreshold: 3);
        Assert.False(worker.IsSourceCircuitOpen("SlowSource", out _));
        worker.RecordSourceFailure("SlowSource", failureThreshold: 3);
        Assert.True(worker.IsSourceCircuitOpen("SlowSource", out var cooldownUntil));
        Assert.True(cooldownUntil > DateTime.UtcNow);

        // 2. Verify RecordSourceSuccess resets the circuit breaker
        worker.RecordSourceSuccess("SlowSource");
        Assert.False(worker.IsSourceCircuitOpen("SlowSource", out _));

        // 3. Verify per-source timeout cancels stalled scraper without throwing out of ScrapeAndIngestSourceAsync
        var stallingScraperMock = new Mock<IScraper>();
        stallingScraperMock.SetupGet(s => s.SourceName).Returns("StallingPublisher");
        stallingScraperMock
            .Setup(s => s.ScrapeAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return Enumerable.Empty<ArticleModel>();
            });

        using var internalClient = new HttpClient();
        using var externalClient = new HttpClient();

        var ex = await Record.ExceptionAsync(() =>
            worker.ScrapeAndIngestSourceAsync(
                stallingScraperMock.Object,
                internalClient,
                externalClient,
                "http://localhost:56193",
                sourceTimeout: TimeSpan.FromMilliseconds(100),
                stoppingToken: CancellationToken.None));

        Assert.Null(ex);
    }
}
