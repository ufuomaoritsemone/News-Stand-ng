using NewsApi.Models;

namespace NewsApi.Services;

/// <summary>
/// Autonomous background service that continuously monitors search engine performance,
/// detects ML.NET ranking decay and CTR anomalies, calculates the global SEO health scorecard,
/// and executes automated self-healing remediation actions.
/// </summary>
public class SeoMonitoringBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SeoMonitoringBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<SeoMonitoringBackgroundService> _logger = logger;
    private static readonly TimeSpan CycleInterval = TimeSpan.FromHours(4);

    private SeoHealthScorecardDto _cachedScorecard = new();
    private readonly List<AutomatedRemediationLogDto> _recentRemediations = [];
    private readonly object _scorecardLock = new();

    public SeoHealthScorecardDto GetLatestScorecard()
    {
        lock (_scorecardLock)
        {
            return _cachedScorecard;
        }
    }

    public async Task<AutomatedRemediationLogDto> TriggerRemediationAsync(
        string actionType,
        string? target,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var feedbackLoop = scope.ServiceProvider.GetRequiredService<ISeoFeedbackLoopService>();

        var log = await feedbackLoop.ExecuteRemediationAsync(actionType, target, ct);

        lock (_scorecardLock)
        {
            _recentRemediations.Insert(0, log);
            if (_recentRemediations.Count > 20)
            {
                _recentRemediations.RemoveAt(_recentRemediations.Count - 1);
            }
            _cachedScorecard.RecentRemediations = [.. _recentRemediations];
        }

        return log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SeoMonitoringBackgroundService initialized. Initial audit starting in 15 seconds.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMonitoringCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during SeoMonitoringBackgroundService cycle.");
            }

            try
            {
                await Task.Delay(CycleInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("SeoMonitoringBackgroundService stopping.");
    }

    public async Task RunMonitoringCycleAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("SeoMonitoringBackgroundService: Starting autonomous SEO health & ML anomaly audit.");

        using var scope = _scopeFactory.CreateScope();
        var searchConsole = scope.ServiceProvider.GetRequiredService<ISearchConsoleService>();
        var anomalyEngine = scope.ServiceProvider.GetRequiredService<ISeoAnomalyEngine>();
        var feedbackLoop = scope.ServiceProvider.GetRequiredService<ISeoFeedbackLoopService>();

        // 1. Ingest telemetry and crawl diagnostics
        var performance = await searchConsole.GetSearchAnalyticsAsync(days: 28, dimension: "query", limit: 50, ct);
        var diagnostics = await searchConsole.GetCrawlDiagnosticsAsync(ct);

        // 2. Compute the 4-pillar health scorecard
        var scorecard = await anomalyEngine.CalculateHealthScorecardAsync(performance, diagnostics, ct);

        var alerts = new List<SeoAnomalyAlertDto>();
        var opportunities = new List<CtrOpportunityDto>();

        // 3. Evaluate ML.NET ranking decay across daily trends
        if (performance.DailyTrends.Count >= 7)
        {
            var history = performance.DailyTrends.Select(d => (d.Date, d.Position, d.Impressions));
            var overallDecay = await anomalyEngine.DetectRankingDecayAsync(
                target: performance.SiteUrl,
                targetType: "Site",
                history: history,
                ct: ct);

            if (overallDecay is not null)
            {
                alerts.Add(overallDecay);
            }
        }

        // 4. Evaluate individual queries for decay and CTR opportunity
        foreach (var q in performance.TopQueries.Take(15))
        {
            var matchingPage = performance.TopPages.FirstOrDefault(p => p.Page.Contains(q.Query, StringComparison.OrdinalIgnoreCase))?.Page
                ?? "/news";

            var opp = await anomalyEngine.EvaluateCtrOpportunityAsync(
                query: q.Query,
                pageUrl: matchingPage,
                currentTitle: q.Query,
                category: "general",
                position: q.Position,
                actualCtr: q.Ctr,
                impressions: q.Impressions,
                hasEnrichment: true,
                ct: ct);

            if (opp is not null)
            {
                // Invoke generative AI feedback loop for headline alternatives
                var (titles, faqs) = await feedbackLoop.GenerateOptimizationsAsync(q.Query, q.Query, matchingPage, "general", ct);
                opp.AiHeadlineSuggestions = titles;
                opp.SuggestedFaqPairs = faqs;
                opportunities.Add(opp);
            }
        }

        // 5. Check crawl issues for critical alerts
        foreach (var issue in diagnostics.CrawlIssues.Where(i => i.Severity == "Critical" || i.Severity == "Warning"))
        {
            alerts.Add(new SeoAnomalyAlertDto
            {
                Target = issue.Url,
                TargetType = "Page",
                AnomalyType = issue.IssueType,
                Severity = issue.Severity,
                AnomalyScore = 1.0,
                Description = issue.Message,
                DetectedAt = issue.DetectedAt,
                IsResolved = false,
                RecommendedAction = issue.RecommendedAction
            });
        }

        // 6. Autonomous Self-Healing: Backfill missing enrichments if needed
        var autoRemediation = await feedbackLoop.ExecuteRemediationAsync("BackfillEnrichment", null, ct);
        _recentRemediations.Insert(0, autoRemediation);
        if (_recentRemediations.Count > 20)
        {
            _recentRemediations.RemoveAt(_recentRemediations.Count - 1);
        }

        scorecard.ActiveAlerts = alerts;
        scorecard.CtrOpportunities = opportunities;
        scorecard.RecentRemediations = [.. _recentRemediations];

        lock (_scorecardLock)
        {
            _cachedScorecard = scorecard;
        }

        _logger.LogInformation(
            "SeoMonitoringBackgroundService cycle complete. Score: {Score}/100 ({Grade}), Alerts: {Alerts}, Opportunities: {Opps}",
            scorecard.OverallScore, scorecard.Grade, alerts.Count, opportunities.Count);
    }
}
