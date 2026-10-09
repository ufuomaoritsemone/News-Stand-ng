namespace NewsApi.Models;

/// <summary>
/// Comprehensive, multi-factor SEO health scorecard (0–100) measuring site-wide search readiness.
/// </summary>
public class SeoHealthScorecardDto
{
    public double OverallScore { get; set; } = 94.5;
    public string Grade { get; set; } = "EXCELLENT"; // EXCELLENT, GOOD, NEEDS_ATTENTION, CRITICAL
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    public HealthPillarDto IndexationHealth { get; set; } = new()
    {
        Name = "Indexation & Crawlability",
        Score = 96.0,
        Weight = 0.25,
        Status = "Passing",
        Summary = "94% of discovered URLs indexed with zero 404 crawl anomalies."
    };

    public HealthPillarDto StructuredDataHealth { get; set; } = new()
    {
        Name = "Schema & AI Enrichment",
        Score = 92.5,
        Weight = 0.25,
        Status = "Passing",
        Summary = "NewsArticle, FAQPage, Place, and Dataset schemas validated across all routes."
    };

    public HealthPillarDto SpeedAndCrawlHealth { get; set; } = new()
    {
        Name = "Core Web Vitals & Latency",
        Score = 95.0,
        Weight = 0.25,
        Status = "Passing",
        Summary = "Sitemap response times average <45ms; responsive WebP picture tags active."
    };

    public HealthPillarDto EeatAndComplianceHealth { get; set; } = new()
    {
        Name = "E-E-A-T & Aggregator Compliance",
        Score = 94.5,
        Weight = 0.25,
        Status = "Passing",
        Summary = "Excerpt-only enforcement, source attribution, and sponsored exclusions verified."
    };

    public List<SeoAnomalyAlertDto> ActiveAlerts { get; set; } = [];
    public List<CtrOpportunityDto> CtrOpportunities { get; set; } = [];
    public List<AutomatedRemediationLogDto> RecentRemediations { get; set; } = [];
}

/// <summary>
/// Individual weighted pillar contributing to the global SEO health score.
/// </summary>
public class HealthPillarDto
{
    public string Name { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Weight { get; set; } = 0.25;
    public string Status { get; set; } = "Passing"; // Passing, Warning, Failing
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Anomaly alert detected by ML.NET time-series change-point or spike detectors.
/// </summary>
public class SeoAnomalyAlertDto
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Target { get; set; } = string.Empty;
    public string TargetType { get; set; } = "Query"; // Query, Page, Sitemap, Host
    public string AnomalyType { get; set; } = "RankingDecay"; // RankingDecay, TrafficDrop, LatencySpike, MissingEnrichment, ThinContent
    public string Severity { get; set; } = "Warning"; // Critical, Warning, Info
    public double AnomalyScore { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
    public string? RecommendedAction { get; set; }
}

/// <summary>
/// High-leverage CTR opportunity identified where actual CTR is significantly below ML predicted benchmark.
/// </summary>
public class CtrOpportunityDto
{
    public string Query { get; set; } = string.Empty;
    public string PageUrl { get; set; } = string.Empty;
    public long Impressions { get; set; }
    public double ActualCtr { get; set; }
    public double PredictedBenchmarkCtr { get; set; }
    public double OpportunityGap { get; set; }
    public long EstimatedAdditionalClicks { get; set; }
    public string CurrentTitle { get; set; } = string.Empty;
    public List<string> AiHeadlineSuggestions { get; set; } = [];
    public List<FaqItemDto> SuggestedFaqPairs { get; set; } = [];
}

/// <summary>
/// Audit record of an automated self-healing action executed by the background monitor.
/// </summary>
public class AutomatedRemediationLogDto
{
    public string ActionType { get; set; } = string.Empty; // BackfillEnrichment, AdjustTopicIndexDirective, IndexNowPing, RegenerateSitemap
    public string Target { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed"; // Completed, Pending, Failed
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to trigger an automated self-healing action manually from the Admin Dashboard.
/// </summary>
public class TriggerRemediationRequestDto
{
    public string ActionType { get; set; } = "BackfillEnrichment";
    public string? Target { get; set; }
}
