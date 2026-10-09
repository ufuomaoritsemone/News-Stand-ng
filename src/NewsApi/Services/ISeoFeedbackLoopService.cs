using NewsApi.Models;

namespace NewsApi.Services;

/// <summary>
/// Service interface for the AI feedback loop, generating headline/FAQ optimizations
/// and orchestrating automated self-healing remediation actions.
/// </summary>
public interface ISeoFeedbackLoopService
{
    /// <summary>
    /// Generates 3 click-optimized search title alternatives (<= 60 chars)
    /// and relevant FAQ snippet suggestions for an underperforming page or query.
    /// </summary>
    Task<(List<string> Titles, List<FaqItemDto> Faqs)> GenerateOptimizationsAsync(
        string query,
        string currentTitle,
        string pageUrl,
        string category,
        CancellationToken ct = default);

    /// <summary>
    /// Executes an automated self-healing action (e.g. backfill enrichment, IndexNow ping).
    /// </summary>
    Task<AutomatedRemediationLogDto> ExecuteRemediationAsync(
        string actionType,
        string? target = null,
        CancellationToken ct = default);
}
