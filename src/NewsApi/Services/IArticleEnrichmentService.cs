using NewsApi.Models;

namespace NewsApi.Services;

public interface IArticleEnrichmentService
{
    /// <summary>
    /// Enriches an article by ID and persists the enrichment record to the database.
    /// </summary>
    Task<ArticleEnrichment?> EnrichArticleAsync(string articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a batch of up to <paramref name="batchSize"/> unenriched articles.
    /// Returns the number of articles newly enriched.
    /// </summary>
    Task<int> ProcessUnenrichedBatchAsync(int batchSize = 25, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pure computation: extracts 3 key takeaways, timeline facts, and structured FAQ pairs from the provided text and metadata.
    /// </summary>
    Task<ArticleEnrichmentResult> GenerateEnrichmentAsync(
        string title,
        string? summary,
        string? content,
        string? category,
        IEnumerable<string>? knownEntities = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload extracting takeaways, timeline facts, and FAQs directly from an Article model.
    /// </summary>
    Task<ArticleEnrichmentResult> GenerateEnrichmentAsync(
        Article article,
        CancellationToken cancellationToken = default);
}

public record ArticleEnrichmentResult(
    List<string> KeyTakeaways,
    List<TimelineFactDto> TimelineFacts,
    List<FaqItemDto> Faqs,
    string Engine,
    FullSummaryPayload? FullSummary = null
);

