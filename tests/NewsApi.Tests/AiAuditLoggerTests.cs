using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using NewsApi.Services.Embeddings;
using Xunit;

namespace NewsApi.Tests;

public class AiAuditLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _logFile;
    private readonly string _jsonlFile;

    public AiAuditLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AiAuditLoggerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _logFile = Path.Combine(_tempDir, "test_audit.log");
        _jsonlFile = Path.Combine(_tempDir, "test_audit.jsonl");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private AiAuditLogger CreateLogger()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiEnrichment:AuditLogPath"] = _logFile
            })
            .Build();

        return new AiAuditLogger(config, NullLogger<AiAuditLogger>.Instance);
    }

    [Fact]
    public async Task AiAuditLogger_WritesGeminiRequestSuccessAndError_ToBothLogAndJsonl()
    {
        using var logger = CreateLogger();

        // 1. Log Request
        await logger.LogGeminiRequestAsync(
            articleId: "art-101",
            title: "Federal Government unveils new transport subsidy scheme",
            model: "gemini-3.1-flash-lite",
            promptPreview: "You are a senior news intelligence editor...",
            promptLength: 1540,
            temperature: 0.2);

        // 2. Log Success
        await logger.LogGeminiSuccessAsync(
            articleId: "art-101",
            title: "Federal Government unveils new transport subsidy scheme",
            model: "gemini-3.1-flash-lite",
            elapsedMilliseconds: 842,
            takeawaysCount: 3,
            timelineCount: 2,
            faqsCount: 3,
            readingTimeSeconds: 45,
            overviewPreview: "The Federal Government has launched a transportation subsidy program.");

        // 3. Log Error
        await logger.LogGeminiErrorAsync(
            articleId: "art-102",
            title: "Power sector grid collapse in northern states",
            model: "gemini-3.1-flash-lite",
            elapsedMilliseconds: 310,
            httpStatusCode: 429,
            reasonPhrase: "Too Many Requests",
            responseBody: "{\"error\":{\"code\":429,\"message\":\"Resource has been exhausted (e.g. check quota).\"}}",
            exception: null);

        // 4. Log Circuit Breaker Tripped
        await logger.LogCircuitBreakerTrippedAsync(
            reason: "Gemini enrichment returned HTTP 429",
            cooldownUntilUtc: DateTime.UtcNow.AddMinutes(10));

        // 5. Log ML Summarizer Engaged
        await logger.LogMlSummarizerEngagedAsync(
            articleId: "art-102",
            title: "Power sector grid collapse in northern states",
            category: "Energy",
            engagementReason: "Gemini AI Request Failed or Timed Out (Fallback to Local ML)",
            sentenceCount: 12,
            elapsedMilliseconds: 15,
            takeawaysCount: 3,
            overviewPreview: "The transmission company confirmed a system disruption on the northern grid.");

        Assert.True(File.Exists(_logFile), "Formatted .log file should exist");
        Assert.True(File.Exists(_jsonlFile), "Machine-parseable .jsonl file should exist");

        var logContent = await File.ReadAllTextAsync(_logFile);
        Assert.Contains("[GEMINI_REQUEST]", logContent);
        Assert.Contains("Federal Government unveils new transport subsidy scheme", logContent);
        Assert.Contains("[GEMINI_SUCCESS]", logContent);
        Assert.Contains("Takeaways: 3", logContent);
        Assert.Contains("[GEMINI_ERROR]", logContent);
        Assert.Contains("HTTP 429 Too Many Requests", logContent);
        Assert.Contains("[CIRCUIT_BREAKER_TRIPPED]", logContent);
        Assert.Contains("[ML_SUMMARIZER_ENGAGED]", logContent);
        Assert.Contains("TextRank-Extractive (MMR Centroid)", logContent);

        var jsonlLines = await File.ReadAllLinesAsync(_jsonlFile);
        Assert.Equal(5, jsonlLines.Length);
        Assert.Contains("\"EventType\":\"GEMINI_REQUEST\"", jsonlLines[0]);
        Assert.Contains("\"EventType\":\"GEMINI_SUCCESS\"", jsonlLines[1]);
        Assert.Contains("\"EventType\":\"GEMINI_ERROR\"", jsonlLines[2]);
        Assert.Contains("\"EventType\":\"CIRCUIT_BREAKER_TRIPPED\"", jsonlLines[3]);
        Assert.Contains("\"EventType\":\"ML_SUMMARIZER_ENGAGED\"", jsonlLines[4]);
    }

    [Fact]
    public async Task ArticleEnrichmentService_LogsMlSummarizerEngagement_WhenLocalTextRankRuns()
    {
        using var auditLogger = CreateLogger();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiEnrichment:GeminiEnabled"] = "false"
            })
            .Build();

        var mockDb = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NewsDbContext(mockDb);

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var service = new ArticleEnrichmentService(
            db,
            new LocalFallbackEmbeddingProvider(),
            config,
            clientFactoryMock.Object,
            NullLogger<ArticleEnrichmentService>.Instance,
            auditLogger: auditLogger);

        var article = new Article
        {
            Id = "art-test-local",
            Title = "Nigeria and France Sign Historic Bilateral Energy Agreement",
            Summary = "The bilateral pact aims to deepen investments in renewable solar energy across West Africa.",
            Content = "Nigeria and France on Thursday signed a landmark memorandum of understanding in Paris. The bilateral agreement involves 500 million euros in concessional financing. Officials from both ministries lauded the milestone as transformative for regional power stability.",
            Category = "Economy"
        };

        var result = await service.GenerateEnrichmentAsync(article);

        Assert.NotNull(result);
        Assert.Equal("TextRank-Extractive", result.Engine);

        var logContent = await File.ReadAllTextAsync(_logFile);
        Assert.Contains("[ML_SUMMARIZER_ENGAGED]", logContent);
        Assert.Contains("Nigeria and France Sign Historic Bilateral Energy Agreement", logContent);
        Assert.Contains("TextRank-Extractive", logContent);

        var jsonlLines = await File.ReadAllLinesAsync(_jsonlFile);
        Assert.NotEmpty(jsonlLines);
        Assert.Contains("\"EventType\":\"ML_SUMMARIZER_ENGAGED\"", jsonlLines[^1]);
        Assert.Contains("\"ArticleId\":\"art-test-local\"", jsonlLines[^1]);
    }
}
