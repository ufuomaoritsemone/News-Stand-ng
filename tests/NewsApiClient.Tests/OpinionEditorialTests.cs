using System.Net;
using System.Net.Http.Json;
using NewsApi.Models;
using NewsScraperService.Services;
using NigerianNewsGrid.Client;
using Xunit;

namespace NewsApiClient.Tests;

/// <summary>
/// Unit and integration tests verifying the Editorial and Opinion pieces feature.
/// Tests detection heuristics, ingestion with author metadata, API filtering, and client methods.
/// </summary>
public class OpinionEditorialTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpinionEditorialTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);
    }

    // ──────────────────────────────────────────────────────────
    // 1. OpinionDetector Heuristics Unit Tests
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://punchng.com/opinion/navigating-nigerias-fiscal-reform/", "Opinion")]
    [InlineData("https://www.vanguardngr.com/category/opinion/the-perils-of-monetary-easing/", "Opinion")]
    [InlineData("https://guardian.ng/editorial/preserving-the-fourth-estate/", "Opinion")]
    [InlineData("https://www.thecable.ng/columns/perspective-on-constitutional-review/", "Opinion")]
    [InlineData("https://dailytrust.com/columnist/economic-outlook-for-the-north/", "Opinion")]
    [InlineData("https://businessday.ng/commentary/why-fintech-needs-prudential-guardrails/", "Opinion")]
    [InlineData("https://punchng.com/oped/a-letter-to-the-president/", "Opinion")]
    public void OpinionDetector_DetectsOpinion_FromUrlSegments(string url, string expected)
    {
        var result = OpinionDetector.Detect(url: url, title: "Standard Headline Here");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Punch Opinions")]
    [InlineData("Vanguard Opinions")]
    [InlineData("The Guardian Opinions")]
    [InlineData("Daily Trust Editorial Board")]
    public void OpinionDetector_DetectsOpinion_FromSourceName(string sourceName)
    {
        var result = OpinionDetector.Detect(
            url: "https://punchng.com/news/12345",
            title: "General News Headline",
            sourceName: sourceName);

        Assert.Equal("Opinion", result);
    }

    [Theory]
    [InlineData("OPINION: Reforming Nigeria's Power Grid for Good")]
    [InlineData("Editorial: The Urgency of Judicial Transparency")]
    [InlineData("Column: Why Sub-Saharan Africa Must Rethink Monetary Policy")]
    [InlineData("Commentary: The Cost of Bureaucratic Red Tape")]
    [InlineData("[Opinion] Rebuilding Civic Trust")]
    [InlineData("Letters to the Editor: A Call for Highway Maintenance")]
    public void OpinionDetector_DetectsOpinion_FromTitlePrefixes(string title)
    {
        var result = OpinionDetector.Detect(
            url: "https://punchng.com/post/999",
            title: title);

        Assert.Equal("Opinion", result);
    }

    [Theory]
    [InlineData("Opinion")]
    [InlineData("opinions")]
    [InlineData("Editorial")]
    [InlineData("Columnist")]
    [InlineData("Commentary")]
    public void OpinionDetector_DetectsOpinion_FromCategoryMarker(string category)
    {
        var result = OpinionDetector.Detect(
            url: "https://punchng.com/post/888",
            title: "Some Political Reflection",
            category: category);

        Assert.Equal("Opinion", result);
    }

    [Fact]
    public void OpinionDetector_DefaultsToNews_ForStandardArticles()
    {
        var result = OpinionDetector.Detect(
            url: "https://punchng.com/news/federal-government-signs-budget-bill/",
            title: "Federal Government Signs 2026 Budget Bill Into Law",
            category: "Politics",
            sourceName: "Punch Newspaper");

        Assert.Equal("News", result);
    }

    // ──────────────────────────────────────────────────────────
    // 2. API Ingestion & Filtering Integration Tests
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Ingest_StoresAuthorAndContentType_AndFiltersByContentType()
    {
        var testGuid = Guid.NewGuid().ToString("N");
        var opinionTitle = $"Opinion: Future of Infrastructure in Lagos {testGuid}";
        var newsTitle = $"FG Inaugurates Interstate Railway Line {testGuid}";

        var ingestPayload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = opinionTitle,
                    Summary = "An op-ed analyzing infrastructure financing models.",
                    Content = "Op-ed full long-form analysis of transport and municipal bonds.",
                    Url = $"https://guardian.ng/opinion/infrastructure-lagos-{testGuid}",
                    Source = "The Guardian Opinions",
                    Category = "Politics",
                    Author = "Dr. Olumide Adeleke",
                    ContentType = "Opinion",
                    PublishedAt = DateTime.UtcNow
                },
                new ArticleIngestItem
                {
                    Title = newsTitle,
                    Summary = "The federal transport ministry inaugurated a new railway line.",
                    Content = "News report on railway ribbon cutting.",
                    Url = $"https://punchng.com/news/railway-inauguration-{testGuid}",
                    Source = "Punch Newspaper",
                    Category = "Politics",
                    Author = "Staff Reporter",
                    ContentType = "News",
                    PublishedAt = DateTime.UtcNow
                }
            ]
        };

        // 1. Ingest both articles
        var ingestResp = await _client.PostAsJsonAsync("/api/v1/articles/ingest", ingestPayload);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        // 2. Filter by contentType=Opinion
        var opinionResp = await _client.GetAsync("/api/v1/articles?contentType=Opinion&limit=100");
        Assert.Equal(HttpStatusCode.OK, opinionResp.StatusCode);
        var opinionArticles = await opinionResp.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(opinionArticles);

        var matchedOpinion = opinionArticles.FirstOrDefault(a => a.Title == opinionTitle);
        Assert.NotNull(matchedOpinion);
        Assert.Equal("Dr. Olumide Adeleke", matchedOpinion.Author);
        Assert.Equal("Opinion", matchedOpinion.ContentType);

        // Verify the standard news article is NOT in the opinion results
        Assert.DoesNotContain(opinionArticles, a => a.Title == newsTitle);

        // 3. Filter by contentType=News
        var newsResp = await _client.GetAsync("/api/v1/articles?contentType=News&limit=100");
        Assert.Equal(HttpStatusCode.OK, newsResp.StatusCode);
        var newsArticles = await newsResp.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(newsArticles);

        var matchedNews = newsArticles.FirstOrDefault(a => a.Title == newsTitle);
        Assert.NotNull(matchedNews);
        Assert.Equal("Staff Reporter", matchedNews.Author);
        Assert.Equal("News", matchedNews.ContentType);
        Assert.DoesNotContain(newsArticles, a => a.Title == opinionTitle);

        // 4. Retrieve by single ID
        var singleResp = await _client.GetAsync($"/api/v1/articles/{matchedOpinion.Id}");
        Assert.Equal(HttpStatusCode.OK, singleResp.StatusCode);
        var singleDto = await singleResp.Content.ReadFromJsonAsync<ArticleDto>();
        Assert.NotNull(singleDto);
        Assert.Equal("Dr. Olumide Adeleke", singleDto.Author);
        Assert.Equal("Opinion", singleDto.ContentType);
    }

    // ──────────────────────────────────────────────────────────
    // 3. NigerianNewsGrid Client GetOpinionsAsync Tests
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NewsApiClient_GetOpinionsAsync_RetrievesOpinionPieces()
    {
        var testGuid = Guid.NewGuid().ToString("N");
        var opinionTitle = $"Column: Monetary Policy Realities in Abuja {testGuid}";

        var ingestPayload = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = opinionTitle,
                    Summary = "Analysis of exchange rate market forces.",
                    Url = $"https://businessday.ng/opinion/monetary-policy-{testGuid}",
                    Source = "BusinessDay Nigeria",
                    Category = "Business",
                    Author = "Prof. Chinedu Eze",
                    ContentType = "Opinion",
                    PublishedAt = DateTime.UtcNow
                }
            ]
        };

        var ingestResp = await _client.PostAsJsonAsync("/api/v1/articles/ingest", ingestPayload);
        Assert.Equal(HttpStatusCode.OK, ingestResp.StatusCode);

        var apiClient = new NigerianNewsGrid.Client.NewsApiClient(_client)
        {
            BaseUrl = _client.BaseAddress?.ToString() ?? "http://localhost"
        };

        var opinions = await apiClient.GetOpinionsAsync(days: 7, pageSize: 50);
        Assert.NotNull(opinions);

        var target = opinions.FirstOrDefault(o => o.Title == opinionTitle);
        Assert.NotNull(target);
        Assert.Equal("Prof. Chinedu Eze", target.Author);
        Assert.Equal("Opinion", target.ContentType);
    }
}
