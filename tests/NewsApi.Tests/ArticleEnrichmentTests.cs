using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using NewsApi.Services.Embeddings;
using Xunit;

namespace NewsApi.Tests;

public class ArticleEnrichmentTests
{
    private readonly LocalFallbackEmbeddingProvider _embeddingProvider = new();

    private ArticleEnrichmentService CreateService(
        NewsDbContext? db = null,
        IConfiguration? config = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        config ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiEnrichment:GeminiEnabled"] = "false"
            })
            .Build();
        if (httpClientFactory == null)
        {
            var clientFactoryMock = new Mock<IHttpClientFactory>();
            clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
            httpClientFactory = clientFactoryMock.Object;
        }

        return new ArticleEnrichmentService(
            db ?? CreateInMemoryDbContext(),
            _embeddingProvider,
            config,
            httpClientFactory,
            NullLogger<ArticleEnrichmentService>.Instance);
    }

    private static NewsDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task GenerateEnrichment_ZeroCostEngine_ProducesVerbatimKeyTakeaways()
    {
        var service = CreateService();
        var article = new Article
        {
            Id = "art-1",
            Title = "Central Bank of Nigeria Raises Monetary Policy Rate to 27.25%",
            Summary = "The Central Bank of Nigeria has raised the benchmark interest rate to 27.25 percent to combat persistent inflation.",
            Content = """
                The Central Bank of Nigeria (CBN) Monetary Policy Committee (MPC) announced an increase in the Monetary Policy Rate (MPR) by 50 basis points to 27.25% on Tuesday.
                Governor Olayemi Cardoso stated during the press briefing in Abuja that the decision was driven by the urgent need to stabilize food prices and curb headline inflation.
                The committee noted that sustained monetary tightening has begun to restore relative stability to the foreign exchange market.
                Commercial banks and financial analysts in Lagos commended the proactive stance while warning of potential short-term borrowing costs for local businesses.
                A total of ₦1.5 trillion in credit facilities was reviewed during the two-day statutory monetary policy deliberations.
                """,
            Source = "BusinessDay Nigeria",
            Category = "Economy",
            PublishedAt = DateTime.UtcNow
        };

        var enrichment = await service.GenerateEnrichmentAsync(article);

        Assert.NotNull(enrichment);
        Assert.Equal("TextRank-Extractive", enrichment.Engine);
        Assert.NotEmpty(enrichment.KeyTakeaways);
        Assert.InRange(enrichment.KeyTakeaways.Count, 1, 3);

        // Verification of 0% hallucination: Every takeaway bullet must be a verbatim excerpt from the source text
        var fullSourceText = $"{article.Title} {article.Summary} {article.Content}";
        foreach (var bullet in enrichment.KeyTakeaways)
        {
            Assert.Contains(bullet, fullSourceText);
        }
    }

    [Fact]
    public async Task GenerateEnrichment_ExtractsTimelineAndMonetaryFacts()
    {
        var service = CreateService();
        var article = new Article
        {
            Id = "art-2",
            Title = "Federal Executive Council Approves ₦4.2 Trillion Road Infrastructure Fund",
            Content = """
                On Monday, the Federal Executive Council approved ₦4.2 trillion for critical national infrastructure development.
                Minister of Works Dave Umahi announced in Abuja that construction on the Lagos-Calabar coastal highway will accelerate in November 2026.
                The project aims to stimulate economic corridor activities across seven coastal states.
                """,
            Source = "The Guardian Nigeria",
            Category = "National"
        };

        var enrichment = await service.GenerateEnrichmentAsync(article);

        Assert.NotNull(enrichment);
        Assert.NotEmpty(enrichment.TimelineFacts);

        // At least one fact should contain monetary or temporal markers (e.g. ₦4.2 trillion or Monday/November)
        var hasMonetaryOrTemporal = enrichment.TimelineFacts.Any(f => 
            f.Fact.Contains("₦") || 
            f.Fact.Contains("trillion", StringComparison.OrdinalIgnoreCase) ||
            (f.DateOrTime != null && f.DateOrTime.Length > 0));

        Assert.True(hasMonetaryOrTemporal, "Expected timeline facts to capture monetary figures or date references.");
    }

    [Fact]
    public async Task GenerateEnrichment_GeneratesStructuredFaqs()
    {
        var service = CreateService();
        var article = new Article
        {
            Id = "art-3",
            Title = "Dangote Refinery Commences Direct Petrol Distribution to Marketers",
            Summary = "Dangote Petroleum Refinery has started supplying premium motor spirit directly to independent fuel marketers across Nigeria.",
            Content = """
                Dangote Petroleum Refinery announced on Thursday the commencement of direct distribution of premium motor spirit to major and independent marketers.
                The facility in Lekki Free Trade Zone has an installed capacity of 650,000 barrels per day.
                Aliko Dangote confirmed that this development will significantly reduce domestic reliance on imported petroleum products.
                """,
            Source = "Punch",
            Category = "Business"
        };

        var enrichment = await service.GenerateEnrichmentAsync(article);

        Assert.NotNull(enrichment);
        Assert.NotEmpty(enrichment.Faqs);
        Assert.InRange(enrichment.Faqs.Count, 2, 4);

        foreach (var faq in enrichment.Faqs)
        {
            Assert.False(string.IsNullOrWhiteSpace(faq.Question), "FAQ question should not be blank.");
            Assert.EndsWith("?", faq.Question.Trim());
            Assert.False(string.IsNullOrWhiteSpace(faq.Answer), "FAQ answer should not be blank.");
        }
    }

    [Fact]
    public async Task GetBySlug_IncludesEnrichment_AndMaintainsZeroContentLeak()
    {
        var db = CreateInMemoryDbContext();
        var service = CreateService(db);

        var article = new Article
        {
            Id = "art-4",
            Title = "NNPCL Secures New Deepwater Oil Exploration Partnership",
            Slug = "nnpcl-secures-new-deepwater-oil-exploration-partnership-4",
            Summary = "NNPCL has signed a major offshore partnership agreement.",
            Content = "This is proprietary, full copyrighted text that must NEVER be leaked to anonymous API consumers.",
            Source = "Vanguard",
            Category = "Energy",
            PublishedAt = DateTime.UtcNow
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        // Enrich the article
        var enrichment = await service.EnrichArticleAsync(article.Id);
        Assert.NotNull(enrichment);

        // Fetch through ArticlesController
        var relatedMock = new Mock<IRelatedContentService>();
        var searchMock = new Mock<IArticleSearchService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();

        var controller = new ArticlesController(
            db,
            relatedMock.Object,
            searchMock.Object,
            cache,
            NullLogger<ArticlesController>.Instance,
            new SlugService(),
            null,
            null,
            null,
            config,
            null,
            service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetBySlug(article.Slug);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var detailDto = Assert.IsType<ArticleDetailDto>(okResult.Value);

        // Verify enrichment is included in API response
        Assert.NotNull(detailDto.Enrichment);
        Assert.NotEmpty(detailDto.Enrichment.KeyTakeaways);
        Assert.NotEmpty(detailDto.Enrichment.Faqs);

        // Verify Zero Content Leak (Aggregator copyright safety & Google E-E-A-T compliance)
        // ArticleDetailDto has NO Content property, and excerpt/summary does not leak full body
        Assert.DoesNotContain("proprietary, full copyrighted text that must NEVER be leaked", detailDto.Excerpt ?? "");

        // Verify FullSummary is present in detail DTO
        Assert.NotNull(detailDto.Enrichment.FullSummary);
        Assert.NotEmpty(detailDto.Enrichment.FullSummary.ExecutiveOverview);
    }

    [Fact]
    public async Task GetOrGenerateAiSummary_Endpoint_ReturnsFullSummaryDto()
    {
        var db = CreateInMemoryDbContext();
        var service = CreateService(db);
        var article = new Article
        {
            Id = "art-ai-summary",
            Title = "Federal Executive Council Approves ₦4.2 Trillion Infrastructure Budget",
            Slug = "fec-approves-infrastructure-budget-2026",
            Summary = "The Federal Executive Council has greenlit a landmark infrastructure budget.",
            Content = """
                The Federal Executive Council (FEC) presided over by President Bola Tinubu has approved ₦4.2 trillion for key arterial highway projects across the six geopolitical zones.
                Minister of Works Dave Umahi briefed State House correspondents on Monday, explaining that the funding covers the Lagos-Calabar Coastal Highway and Sokoto-Badagry Superhighway corridors.
                He affirmed that construction milestones will be monitored rigorously with independent engineering oversight.
                Finance Minister Wale Edun emphasized that funding will be raised via non-inflationary infrastructure bonds.
                """,
            Source = "Channels TV",
            Category = "Politics",
            PublishedAt = DateTime.UtcNow
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var relatedMock = new Mock<IRelatedContentService>();
        var searchMock = new Mock<IArticleSearchService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();

        var controller = new ArticlesController(
            db,
            relatedMock.Object,
            searchMock.Object,
            cache,
            NullLogger<ArticlesController>.Instance,
            new SlugService(),
            null,
            null,
            null,
            config,
            null,
            service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetOrGenerateAiSummary(article.Slug);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var fullSummary = Assert.IsType<FullSummaryDto>(okResult.Value);

        Assert.NotNull(fullSummary);
        Assert.NotEmpty(fullSummary.ExecutiveOverview);
        Assert.True(fullSummary.ReadingTimeSeconds > 0);
    }

    [Fact]
    public async Task ProcessUnenrichedBatch_WhenCloudAiFails_ChecksOnceAndTripsCircuitBreakerForBatch()
    {
        // Reset any prior circuit breaker state
        ArticleEnrichmentService.ResetCircuitBreaker();

        var db = CreateInMemoryDbContext();
        for (int i = 1; i <= 3; i++)
        {
            db.Articles.Add(new Article
            {
                Id = $"batch-art-{i}",
                Title = $"Nigerian Economic Update Article {i}",
                Summary = $"Summary for article {i}",
                Content = $"This is full content for article {i} covering financial policy in Nigeria on Monday with ₦50 billion allocation.",
                Source = "BusinessDay",
                Category = "Economy",
                PublishedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "fake-test-api-key",
                ["AiEnrichment:GeminiEnabled"] = "true"
            })
            .Build();

        var countingHandler = new CountingErrorHttpMessageHandler(System.Net.HttpStatusCode.TooManyRequests);
        var client = new HttpClient(countingHandler);
        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var service = CreateService(db, config, clientFactoryMock.Object);

        // Process batch of 3 articles
        int enrichedCount = await service.ProcessUnenrichedBatchAsync(batchSize: 3);

        // All 3 articles must be successfully enriched via local ML fallback
        Assert.Equal(3, enrichedCount);

        // Key Assertion: The Cloud AI endpoint must have been called EXACTLY ONCE (probe/first article),
        // not 3 times. Once it failed, the circuit breaker tripped and the remaining 2 articles bypassed Cloud AI.
        Assert.Equal(1, countingHandler.CallCount);

        // Verify the circuit breaker is now active
        Assert.True(service.IsCircuitBreakerActive);
        Assert.False(await service.IsCloudAiAvailableAsync());

        // Verify that all 3 saved records used the local TextRank fallback engine
        var savedEnrichments = await db.ArticleEnrichments.ToListAsync();
        Assert.Equal(3, savedEnrichments.Count);
        Assert.All(savedEnrichments, e => Assert.Equal("TextRank-Extractive", e.Engine));
    }

    [Fact]
    public async Task GenerateEnrichmentAsync_WithSkipCloudAi_BypassesCloudAiNetworkCalls()
    {
        ArticleEnrichmentService.ResetCircuitBreaker();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "fake-test-api-key",
                ["AiEnrichment:GeminiEnabled"] = "true"
            })
            .Build();

        var countingHandler = new CountingErrorHttpMessageHandler(System.Net.HttpStatusCode.InternalServerError);
        var client = new HttpClient(countingHandler);
        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var service = CreateService(null, config, clientFactoryMock.Object);

        var article = new Article
        {
            Id = "test-skip",
            Title = "Nigeria Tech Ecosystem Expands in Lagos",
            Summary = "Lagos startups report increased investor participation.",
            Content = "Lagos startups secured fresh investment on Tuesday across fintech and logistics.",
            Category = "Technology"
        };

        // Call with skipCloudAi: true
        var result = await service.GenerateEnrichmentAsync(
            article.Title, article.Summary, article.Content, article.Category,
            knownEntities: null, skipCloudAi: true);

        Assert.NotNull(result);
        Assert.Equal("TextRank-Extractive", result.Engine);
        // Zero outbound HTTP calls were made
        Assert.Equal(0, countingHandler.CallCount);
    }

    private sealed class CountingErrorHttpMessageHandler(System.Net.HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                ReasonPhrase = "Quota Exceeded or Network Error",
                Content = new StringContent("{\"error\": \"resource exhausted\"}")
            });
        }
    }
}

