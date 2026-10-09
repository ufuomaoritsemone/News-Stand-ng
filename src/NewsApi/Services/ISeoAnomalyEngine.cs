using NewsApi.Models;

namespace NewsApi.Services;

/// <summary>
/// Interface for the ML.NET-powered search anomaly and CTR opportunity scoring engine.
/// </summary>
public interface ISeoAnomalyEngine
{
    /// <summary>
    /// Evaluates daily ranking and impression time-series for a target query/page
    /// using ML.NET Singular Spectrum Analysis (SSA) change-point detection.
    /// </summary>
    Task<SeoAnomalyAlertDto?> DetectRankingDecayAsync(
        string target,
        string targetType,
        IEnumerable<(DateTime Date, double Position, long Impressions)> history,
        CancellationToken ct = default);

    /// <summary>
    /// Predicts benchmark expected CTR using a trained ML.NET regression model
    /// and flags pages where actual CTR is significantly below potential.
    /// </summary>
    Task<CtrOpportunityDto?> EvaluateCtrOpportunityAsync(
        string query,
        string pageUrl,
        string currentTitle,
        string category,
        double position,
        double actualCtr,
        long impressions,
        bool hasEnrichment,
        CancellationToken ct = default);

    /// <summary>
    /// Computes the complete site-wide SEO health scorecard based on telemetry,
    /// indexation audits, and ML anomalies.
    /// </summary>
    Task<SeoHealthScorecardDto> CalculateHealthScorecardAsync(
        SearchPerformanceSummaryDto performance,
        CrawlDiagnosticsDto diagnostics,
        CancellationToken ct = default);
}
