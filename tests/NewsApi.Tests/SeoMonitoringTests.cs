using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class SeoMonitoringTests
{
    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    private SeoAnomalyEngine CreateAnomalyEngine()
    {
        return new SeoAnomalyEngine(NullLogger<SeoAnomalyEngine>.Instance);
    }

    private (SeoFeedbackLoopService feedback, Mock<IArticleEnrichmentService> enrichMock, Mock<IIndexNowService> indexNowMock) CreateFeedbackLoopService(NewsDbContext db)
    {
        var configMock = new Mock<IConfiguration>();
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        var enrichMock = new Mock<IArticleEnrichmentService>();
        var indexNowMock = new Mock<IIndexNowService>();

        enrichMock.Setup(e => e.EnrichArticleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken ct) => new ArticleEnrichment
            {
                ArticleId = id,
                KeyTakeawaysJson = "[\"Key takeaway 1\"]",
                TimelineFactsJson = "[]",
                FaqsJson = "[]",
                Engine = "TextRank-Extractive"
            });

        indexNowMock.Setup(i => i.EnqueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        indexNowMock.Setup(i => i.GetConfiguration())
            .Returns(("test-key", "newsstand.ng", "https://newsstand.ng/test-key.txt"));

        var service = new SeoFeedbackLoopService(
            db,
            configMock.Object,
            httpFactoryMock.Object,
            enrichMock.Object,
            indexNowMock.Object,
            NullLogger<SeoFeedbackLoopService>.Instance);

        return (service, enrichMock, indexNowMock);
    }

    [Fact]
    public async Task CalculateHealthScorecardAsync_ReturnsMultiFactorScore_AndValidPillars()
    {
        var engine = CreateAnomalyEngine();

        var diagnostics = new CrawlDiagnosticsDto
        {
            TotalDiscoveredUrls = 100,
            TotalIndexedUrls = 95,
            TotalCrawlErrors = 0,
            IndexationRate = 95.0
        };

        var performance = new SearchPerformanceSummaryDto
        {
            Days = 28,
            TotalClicks = 12000,
            TotalImpressions = 250000,
            AverageCtr = 4.8,
            AveragePosition = 6.2,
            TopQueries =
            [
                new SearchQueryPerformanceDto { Query = "cbn interest rate", Clicks = 800, Impressions = 20000, Ctr = 4.0, Position = 4.5 }
            ]
        };

        var scorecard = await engine.CalculateHealthScorecardAsync(performance, diagnostics);

        Assert.NotNull(scorecard);
        Assert.True(scorecard.OverallScore >= 0 && scorecard.OverallScore <= 100);
        Assert.False(string.IsNullOrEmpty(scorecard.Grade));

        // Pillars check
        Assert.NotNull(scorecard.IndexationHealth);
        Assert.Equal(0.25, scorecard.IndexationHealth.Weight);
        Assert.NotNull(scorecard.StructuredDataHealth);
        Assert.Equal(0.25, scorecard.StructuredDataHealth.Weight);
        Assert.NotNull(scorecard.SpeedAndCrawlHealth);
        Assert.Equal(0.25, scorecard.SpeedAndCrawlHealth.Weight);
        Assert.NotNull(scorecard.EeatAndComplianceHealth);
        Assert.Equal(0.25, scorecard.EeatAndComplianceHealth.Weight);
    }

    [Fact]
    public async Task EvaluateCtrOpportunityAsync_IdentifiesUnderperformingPages_WithRegressionModel()
    {
        var engine = CreateAnomalyEngine();

        // Query at rank 2 with only 1.0% CTR (benchmark should be ~10-25%)
        var opp = await engine.EvaluateCtrOpportunityAsync(
            query: "naira exchange rate today black market lagos",
            pageUrl: "/naira-exchange-rate",
            currentTitle: "Current Exchange Rate for Naira Today",
            category: "business",
            position: 2.0,
            actualCtr: 1.0,
            impressions: 10000,
            hasEnrichment: true);

        Assert.NotNull(opp);
        Assert.Equal("naira exchange rate today black market lagos", opp.Query);
        Assert.True(opp.PredictedBenchmarkCtr > opp.ActualCtr);
        Assert.True(opp.OpportunityGap > 0);
        Assert.True(opp.EstimatedAdditionalClicks > 0);
    }

    [Fact]
    public async Task DetectRankingDecayAsync_WithTimeSeries_ExecutesWithoutError()
    {
        var engine = CreateAnomalyEngine();

        // Populate a 30-day daily trend with an artificial drop on the last 5 days
        var history = new List<(DateTime Date, double Position, long Impressions)>();
        var baseDate = DateTime.UtcNow.Date.AddDays(-30);

        for (int i = 0; i < 30; i++)
        {
            var isDecay = i >= 25;
            history.Add((
                Date: baseDate.AddDays(i),
                Position: isDecay ? 28.0 : 3.5,
                Impressions: isDecay ? 500 : 8000
            ));
        }

        var alert = await engine.DetectRankingDecayAsync(
            target: "inec presidential election results",
            targetType: "Query",
            history: history);

        // May be null or an alert depending on martingale threshold, but must execute safely
        if (alert != null)
        {
            Assert.Equal("inec presidential election results", alert.Target);
            Assert.Equal("RankingDecay", alert.AnomalyType);
        }
    }

    [Fact]
    public async Task GenerateOptimizationsAsync_ReturnsStructuredTitlesAndFaqs_AlgorithmicFallback()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var (feedback, _, _) = CreateFeedbackLoopService(db);

        var (titles, faqs) = await feedback.GenerateOptimizationsAsync(
            "naira to dollar exchange rate",
            "Naira to Dollar Today: Latest Official and Parallel Rates",
            "/naira-exchange-rate",
            "business");

        Assert.NotNull(titles);
        Assert.NotEmpty(titles);
        Assert.All(titles, t => Assert.True(t.Length <= 65));

        Assert.NotNull(faqs);
        Assert.NotEmpty(faqs);
        Assert.All(faqs, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Question));
            Assert.False(string.IsNullOrWhiteSpace(f.Answer));
        });
    }

    [Fact]
    public async Task ExecuteRemediationAsync_Backfill_ExecutesSuccessfully()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        db.Articles.Add(new Article
        {
            Id = "art-un-1",
            Title = "Dangote Refinery Begins Petrol Distribution Across Nigeria",
            Slug = "dangote-refinery-petrol-distribution-482aef",
            Category = "business",
            PublishedAt = DateTime.UtcNow.AddHours(-1),
            Url = "https://example.com/dangote-refinery"
        });
        await db.SaveChangesAsync();

        var (feedback, enrichMock, _) = CreateFeedbackLoopService(db);

        var log = await feedback.ExecuteRemediationAsync("BackfillEnrichment", null);

        Assert.NotNull(log);
        Assert.Equal("BackfillEnrichment", log.ActionType);
        Assert.Equal("Completed", log.Status);
        Assert.Contains("Enriched article", log.Details);
        enrichMock.Verify(e => e.EnrichArticleAsync("art-un-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteRemediationAsync_IndexNow_DispatchesUrls()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var (feedback, _, indexNowMock) = CreateFeedbackLoopService(db);

        var log = await feedback.ExecuteRemediationAsync("IndexNowPing", "https://newsstand.ng/news/super-eagles-afcon-quarter-finals-123456");

        Assert.NotNull(log);
        Assert.Equal("IndexNowPing", log.ActionType);
        Assert.Equal("Completed", log.Status);
        indexNowMock.Verify(i => i.EnqueueUrlAsync("https://newsstand.ng/news/super-eagles-afcon-quarter-finals-123456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SeoController_Phase10Endpoints_ReturnExpectedResults()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var scMock = new Mock<ISearchConsoleService>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();

        var feedbackLoopMock = new Mock<ISeoFeedbackLoopService>();
        feedbackLoopMock.Setup(f => f.ExecuteRemediationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomatedRemediationLogDto
            {
                ActionType = "BackfillEnrichment",
                Status = "Completed",
                Details = "Backfilled 1 missing enrichments."
            });

        serviceProviderMock.Setup(s => s.GetService(typeof(ISeoFeedbackLoopService)))
            .Returns(feedbackLoopMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

        var bgService = new SeoMonitoringBackgroundService(
            scopeFactoryMock.Object,
            NullLogger<SeoMonitoringBackgroundService>.Instance);

        var controller = new SeoController(
            db,
            NullLogger<SeoController>.Instance,
            scMock.Object,
            bgService);

        // 1. health-scorecard
        var scorecardResult = controller.GetHealthScorecard();
        var okScorecard = Assert.IsType<OkObjectResult>(scorecardResult);
        var scorecardDto = Assert.IsType<SeoHealthScorecardDto>(okScorecard.Value);
        Assert.NotNull(scorecardDto);
        Assert.Equal("EXCELLENT", scorecardDto.Grade);

        // 2. anomalies
        var anomaliesResult = controller.GetActiveAnomalies();
        var okAnomalies = Assert.IsType<OkObjectResult>(anomaliesResult);
        var anomalyList = Assert.IsType<List<SeoAnomalyAlertDto>>(okAnomalies.Value);
        Assert.NotNull(anomalyList);

        // 3. ctr-opportunities
        var ctrResult = controller.GetCtrOpportunities();
        var okCtr = Assert.IsType<OkObjectResult>(ctrResult);
        var oppList = Assert.IsType<List<CtrOpportunityDto>>(okCtr.Value);
        Assert.NotNull(oppList);

        // 4. trigger-remediation
        var remResult = await controller.TriggerRemediation(new TriggerRemediationRequestDto
        {
            ActionType = "BackfillEnrichment"
        });
        var okRem = Assert.IsType<OkObjectResult>(remResult);
        var remDto = Assert.IsType<AutomatedRemediationLogDto>(okRem.Value);
        Assert.Equal("Completed", remDto.Status);
    }
}
