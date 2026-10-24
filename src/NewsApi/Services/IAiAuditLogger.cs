namespace NewsApi.Services;

/// <summary>
/// Audit logger service for recording all Google Gemini AI requests, successes, errors/issues,
/// circuit breaker events, and local ML Summarizer (TextRank) fallbacks for offline analysis.
/// </summary>
public interface IAiAuditLogger
{
    /// <summary>Gets the full file path to the human-readable audit log.</summary>
    string LogFilePath { get; }

    /// <summary>Gets the full file path to the machine-parseable JSON Lines audit log.</summary>
    string JsonlFilePath { get; }

    /// <summary>
    /// Logs an outbound request to the Gemini API.
    /// </summary>
    Task LogGeminiRequestAsync(
        string? articleId,
        string title,
        string model,
        string? promptPreview,
        int promptLength,
        double temperature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a successful Gemini response and enrichment generation.
    /// </summary>
    Task LogGeminiSuccessAsync(
        string? articleId,
        string title,
        string model,
        long elapsedMilliseconds,
        int takeawaysCount,
        int timelineCount,
        int faqsCount,
        int readingTimeSeconds,
        string? overviewPreview,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a Gemini API error, HTTP non-200, timeout, or exception.
    /// </summary>
    Task LogGeminiErrorAsync(
        string? articleId,
        string title,
        string model,
        long elapsedMilliseconds,
        int? httpStatusCode,
        string? reasonPhrase,
        string? responseBody,
        Exception? exception,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs when the Cloud AI circuit breaker trips and enters cooldown.
    /// </summary>
    Task LogCircuitBreakerTrippedAsync(
        string reason,
        DateTime cooldownUntilUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs when the zero-API-key Local ML Summarizer (TextRank / MMR Centroid) takes effect.
    /// </summary>
    Task LogMlSummarizerEngagedAsync(
        string? articleId,
        string title,
        string? category,
        string engagementReason,
        int sentenceCount,
        long elapsedMilliseconds,
        int takeawaysCount,
        string? overviewPreview,
        CancellationToken cancellationToken = default);
}
