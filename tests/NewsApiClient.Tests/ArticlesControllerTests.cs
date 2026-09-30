using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using Xunit;

namespace NewsApiClient.Tests;

/// <summary>
/// Integration tests for ArticlesController using WebApplicationFactory
/// with an in-memory database (Fix #40).
/// </summary>
public class ArticlesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ArticlesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetArticles_ReturnsOk_WithEmptyDatabase()
    {
        var response = await _client.GetAsync("/api/v1/articles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
    }

    [Fact]
    public async Task GetBriefings_ReturnsOk_WithLanguageParameter()
    {
        var response = await _client.GetAsync("/api/v1/articles/briefings?language=English&topPerCategory=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var briefing = await response.Content.ReadFromJsonAsync<List<BriefingCategoryDto>>();
        Assert.NotNull(briefing);
    }

    [Fact]
    public async Task PostIngest_WithoutApiKey_Returns401_WhenApiKeyConfigured()
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(new IngestArticlesRequest
            {
                Articles =
                [
                    new ArticleIngestItem
                    {
                        Title    = "Test Article",
                        Source   = "Test Source",
                        Category = "Politics"
                    }
                ]
            })
        };

        var response = await _client.SendAsync(requestMessage);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostIngest_WithValidApiKey_ReturnsOk_AndIngests()
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(new IngestArticlesRequest
            {
                Articles =
                [
                    new ArticleIngestItem
                    {
                        Title    = "Integration Test Article " + Guid.NewGuid(),
                        Source   = "Test Source",
                        Category = "Politics",
                        Url      = $"https://example.com/article-{Guid.NewGuid()}"
                    }
                ]
            })
        };
        requestMessage.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(requestMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IngestArticlesResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.IngestedCount);
        Assert.Equal(0, result.SkippedDuplicateCount);
    }

    [Fact]
    public async Task PostIngest_DuplicateUrl_SkipsDuplicate()
    {
        var url = $"https://example.com/dedup-{Guid.NewGuid()}";
        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem { Title = "Duplicate Article " + Guid.NewGuid(), Url = url, Source = "Test", Category = "Business" }
            ]
        };

        // First ingest with API Key
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        req1.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var resp1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        // Second ingest — same URL
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        req2.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var resp2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        var result = await resp2.Content.ReadFromJsonAsync<IngestArticlesResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.IngestedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public async Task PostIngest_DuplicateTitle_SkipsDuplicate()
    {
        var title = "Unique Breaking News Title " + Guid.NewGuid();
        var payload1 = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem { Title = title, Url = $"https://example.com/art-1-{Guid.NewGuid()}", Source = "Punch", Category = "Politics" }
            ]
        };

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest") { Content = JsonContent.Create(payload1) };
        req1.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var resp1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        // Second ingest with different URL but identical Title
        var payload2 = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem { Title = title, Url = $"https://example.com/art-2-{Guid.NewGuid()}", Source = "Vanguard", Category = "Politics" }
            ]
        };

        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest") { Content = JsonContent.Create(payload2) };
        req2.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var resp2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        var result = await resp2.Content.ReadFromJsonAsync<IngestArticlesResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.IngestedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public async Task PostIngest_CaseInsensitiveTitle_SkipsDuplicate()
    {
        var baseTitle = "Central Bank Directs Banks on Forex " + Guid.NewGuid();
        var payload1 = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem { Title = baseTitle.ToUpperInvariant(), Url = $"https://example.com/casing-1-{Guid.NewGuid()}", Source = "TheCable" }
            ]
        };

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest") { Content = JsonContent.Create(payload1) };
        req1.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var resp1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        // Second ingest with lowercase title
        var payload2 = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem { Title = baseTitle.ToLowerInvariant(), Url = $"https://example.com/casing-2-{Guid.NewGuid()}", Source = "Guardian" }
            ]
        };

        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest") { Content = JsonContent.Create(payload2) };
        req2.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var resp2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        var result = await resp2.Content.ReadFromJsonAsync<IngestArticlesResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.IngestedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public async Task PostIngest_BatchInternalDuplicateTitle_SkipsDuplicate()
    {
        var title = "Batch Internal Duplicate Headline " + Guid.NewGuid();
        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem { Title = title, Url = $"https://example.com/b1-{Guid.NewGuid()}", Source = "Punch" },
                new ArticleIngestItem { Title = title, Url = $"https://example.com/b2-{Guid.NewGuid()}", Source = "Premium Times" }
            ]
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest") { Content = JsonContent.Create(payload) };
        req.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = await resp.Content.ReadFromJsonAsync<IngestArticlesResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.IngestedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public async Task GetBriefings_ReturnsCacheHeader()
    {
        // First call
        var r1 = await _client.GetAsync("/api/v1/articles/briefings?language=English&topPerCategory=5");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Second call
        var r2 = await _client.GetAsync("/api/v1/articles/briefings?language=English&topPerCategory=5");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        Assert.True(r2.Headers.Contains("X-Cache"), "Expected X-Cache response header.");
    }

    [Fact]
    public async Task GetBriefingsAvailability_ReturnsOk_WithAvailabilityStatus()
    {
        var response = await _client.GetAsync("/api/v1/articles/briefings/availability");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(doc.TryGetProperty("available", out var availProp));
        Assert.True(doc.TryGetProperty("articleCount", out var countProp));
    }

    [Fact]
    public async Task Analytics_AudioListenEvents_AreIngestedAndAggregatedInSummary()
    {
        var deviceId = Guid.NewGuid().ToString("N");
        var events = new List<object>
        {
            new
            {
                DeviceId = deviceId,
                EventType = "audio_listen_start",
                ArticleId = "art-101",
                ArticleTitle = "Daily Audio Briefing",
                Category = "Daily Briefing",
                OccurredAt = DateTimeOffset.UtcNow
            },
            new
            {
                DeviceId = deviceId,
                EventType = "audio_listen_complete",
                ArticleId = "art-101",
                ArticleTitle = "Daily Audio Briefing",
                Category = "Daily Briefing",
                OccurredAt = DateTimeOffset.UtcNow
            }
        };

        var postResp = await _client.PostAsJsonAsync("/api/v1/analytics", events);
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var summaryReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/analytics/summary?days=7");
        summaryReq.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var summaryResp = await _client.SendAsync(summaryReq);
        Assert.Equal(HttpStatusCode.OK, summaryResp.StatusCode);

        var summary = await summaryResp.Content.ReadFromJsonAsync<NewsApi.Controllers.AnalyticsSummaryDto>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(summary);
        Assert.True(summary.AudioListensStarted >= 1);
        Assert.True(summary.AudioListensCompleted >= 1);
    }

    [Fact]
    public async Task GetArticles_WithDaysAndSearch_FiltersCorrectly()
    {
        var response = await _client.GetAsync("/api/v1/articles?days=30&search=Nigeria&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
    }

    [Fact]
    public async Task GetArticleById_ExistingId_ReturnsArticleWithContent()
    {
        var title = "Detailed Article For Reader Mode " + Guid.NewGuid();
        var content = "This is paragraph 1 of the full article.\n\nThis is paragraph 2 with detailed economic breakdown.";
        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = title,
                    Summary = "Short summary",
                    Content = content,
                    Url = $"https://example.com/reader-{Guid.NewGuid()}",
                    Source = "The Punch",
                    Category = "Business"
                }
            ]
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest") { Content = JsonContent.Create(payload) };
        req.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var ingestResp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        // Fetch articles list to retrieve the generated ID
        var listResp = await _client.GetAsync($"/api/v1/articles?search={Uri.EscapeDataString(title)}");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(list);
        var ingested = Assert.Single(list);

        // Query GetArticleById
        var getResp = await _client.GetAsync($"/api/v1/articles/{ingested.Id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var article = await getResp.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.NotNull(article);
        Assert.Equal(ingested.Id, article.Id);
        Assert.Equal(title, article.Title);
        Assert.Equal("Short summary", article.Summary);
        Assert.Equal(content, article.Content);
        Assert.Equal("The Punch", article.Source);
        Assert.Equal("Business", article.Category);
    }

    [Fact]
    public async Task GetArticleById_NonExistingId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/v1/articles/non-existent-id-99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_UpdatesArticleAndFtsTrigger_Successfully()
    {
        var title = "Category Trigger Test " + Guid.NewGuid();
        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = title,
                    Summary = "Initial summary",
                    Content = "Initial content",
                    Source = "Test Source",
                    Category = "General"
                }
            ]
        };

        var ingestReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        ingestReq.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var ingestResp = await _client.SendAsync(ingestReq);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        // Retrieve ingested article
        var searchResp = await _client.GetAsync($"/api/v1/articles?search={Uri.EscapeDataString(title)}");
        var list = await searchResp.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(list);
        var article = Assert.Single(list);
        Assert.Equal("General", article.Category);

        // Update Category
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/articles/{article.Id}/category")
        {
            Content = JsonContent.Create(new UpdateArticleCategoryRequest { Category = "Technology" })
        };
        updateReq.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var updateResp = await _client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Verify updated category
        var verifyResp = await _client.GetAsync($"/api/v1/articles/{article.Id}");
        var updated = await verifyResp.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.NotNull(updated);
        Assert.Equal("Technology", updated.Category);
    }

    [Fact]
    public async Task TrackImpression_IncrementsImpressionCountAtomically()
    {
        var title = $"Impression Test Article {Guid.NewGuid():N}";
        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = title,
                    Source = "Test Source",
                    Category = "Business"
                }
            ]
        };

        var ingestReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        ingestReq.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var ingestResp = await _client.SendAsync(ingestReq);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        var searchResp = await _client.GetAsync($"/api/v1/articles?search={Uri.EscapeDataString(title)}");
        var list = await searchResp.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(list);
        var article = Assert.Single(list);
        var initialImpressions = article.ImpressionCount;

        var trackResp = await _client.PostAsync($"/api/v1/articles/{article.Id}/track-impression", null);
        Assert.Equal(HttpStatusCode.NoContent, trackResp.StatusCode);

        var verifyResp = await _client.GetAsync($"/api/v1/articles/{article.Id}");
        var updated = await verifyResp.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.NotNull(updated);
        Assert.Equal(initialImpressions + 1, updated.ImpressionCount);
    }

    [Fact]
    public async Task TrackClick_IncrementsClickCountAtomically()
    {
        var title = $"Click Test Article {Guid.NewGuid():N}";
        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = title,
                    Source = "Test Source",
                    Category = "Sports"
                }
            ]
        };

        var ingestReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        ingestReq.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var ingestResp = await _client.SendAsync(ingestReq);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        var searchResp = await _client.GetAsync($"/api/v1/articles?search={Uri.EscapeDataString(title)}");
        var list = await searchResp.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(list);
        var article = Assert.Single(list);
        var initialClicks = article.ClickCount;

        var trackResp = await _client.PostAsync($"/api/v1/articles/{article.Id}/track-click", null);
        Assert.Equal(HttpStatusCode.NoContent, trackResp.StatusCode);

        var verifyResp = await _client.GetAsync($"/api/v1/articles/{article.Id}");
        var updated = await verifyResp.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.NotNull(updated);
        Assert.Equal(initialClicks + 1, updated.ClickCount);
    }

    [Fact]
    public async Task GetRelatedStories_ReturnsRelatedArticles_WithProjectedMetadata()
    {
        var category = "Technology";
        var title1 = $"AI Tech Breakthrough {Guid.NewGuid():N}";
        var title2 = $"AI Software Engineering {Guid.NewGuid():N}";

        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = title1,
                    Summary = "Artificial intelligence breakthrough in Nigeria",
                    Content = "Heavy full article text body with extensive paragraphs...",
                    Source = "Tech Cable",
                    Category = category
                },
                new ArticleIngestItem
                {
                    Title = title2,
                    Summary = "Artificial intelligence software engineering models",
                    Content = "Another heavy full text article body...",
                    Source = "Tech Cable",
                    Category = category
                }
            ]
        };

        var ingestReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        ingestReq.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var ingestResp = await _client.SendAsync(ingestReq);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        var searchResp = await _client.GetAsync($"/api/v1/articles?search={Uri.EscapeDataString(title1)}");
        var list = await searchResp.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(list);
        var article1 = Assert.Single(list);

        var relatedResp = await _client.GetAsync($"/api/v1/articles/related?id={article1.Id}&category={category}&limit=2");
        Assert.Equal(HttpStatusCode.OK, relatedResp.StatusCode);

        var relatedDto = await relatedResp.Content.ReadFromJsonAsync<NewsApi.Services.RelatedStoriesDto>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(relatedDto);
        Assert.NotNull(relatedDto.Articles);
        Assert.Contains(relatedDto.Articles, a => a.Title == title2);
        // Ensure Content blob was omitted from candidate scoring and response
        Assert.All(relatedDto.Articles, a => Assert.Null(a.Content));
        var relatedArt = Assert.Single(relatedDto.Articles, a => a.Title == title2);
        Assert.Equal(NewsApi.Infrastructure.CategoryImageMap.Resolve(null, category), relatedArt.ImageUrl);
    }

    [Fact]
    public async Task GetBriefings_CustomParameters_AreAtomicallyInvalidatedOnIngest()
    {
        // 1. Initial call with custom topPerCategory = 25
        var initialResp = await _client.GetAsync("/api/v1/articles/briefings?language=English&topPerCategory=25");
        Assert.Equal(HttpStatusCode.OK, initialResp.StatusCode);

        // 2. Second call should be a cache hit
        var cachedResp = await _client.GetAsync("/api/v1/articles/briefings?language=English&topPerCategory=25");
        Assert.Equal(HttpStatusCode.OK, cachedResp.StatusCode);
        Assert.True(cachedResp.Headers.Contains("X-Cache"));
        Assert.Equal("HIT", cachedResp.Headers.GetValues("X-Cache").First());

        // 3. Ingest a new article (which calls InvalidateBriefingCache)
        var newTitle = $"Briefing Cache Invalidation Test {Guid.NewGuid():N}";
        var payload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = newTitle,
                    Summary = "Briefing invalidation test summary",
                    Source = "Cache Invalidator",
                    Category = "Politics"
                }
            ]
        };

        var ingestReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(payload)
        };
        ingestReq.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
        var ingestResp = await _client.SendAsync(ingestReq);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        // 4. Third call should be a cache MISS and include the new article
        var invalidatedResp = await _client.GetAsync("/api/v1/articles/briefings?language=English&topPerCategory=25");
        Assert.Equal(HttpStatusCode.OK, invalidatedResp.StatusCode);
        Assert.True(invalidatedResp.Headers.Contains("X-Cache"));
        Assert.Equal("MISS", invalidatedResp.Headers.GetValues("X-Cache").First());

        var categories = await invalidatedResp.Content.ReadFromJsonAsync<List<BriefingCategoryDto>>();
        Assert.NotNull(categories);
        var politicsCategory = categories.FirstOrDefault(c => c.Category == "Politics");
        Assert.NotNull(politicsCategory);
        Assert.Contains(politicsCategory.Top, a => a.Title == newTitle);
    }
}

/// <summary>
/// WebApplicationFactory fixture configured with in-memory database and test API key.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-integration-api-key-abc123";
    private readonly string _testDbPath = Path.Combine(Path.GetTempPath(), $"news_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:NewsDb", $"Data Source={_testDbPath}");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_testDbPath}");
        builder.UseSetting("ApiKey", TestApiKey);

        builder.ConfigureServices(services =>
        {
            var initDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbInitializer));
            if (initDescriptor is not null) services.Remove(initDescriptor);
            services.AddScoped<IDbInitializer, TestDbInitializer>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }
        catch { }
    }
}

/// <summary>
/// Lightweight initializer for test host that ensures isolated SQLite schema is created.
/// </summary>
public class TestDbInitializer(NewsDbContext db) : IDbInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await DbInitializer.EnsureFullTextSearchCreatedAsync(db, null, cancellationToken);
    }
}
