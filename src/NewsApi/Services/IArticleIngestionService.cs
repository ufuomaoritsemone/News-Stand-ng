using NewsApi.Models;
using NigerianNewsGrid.Client.Models;

namespace NewsApi.Services;

/// <summary>
/// Handles batch article ingestion, topic resolution, IndexNow notification,
/// category reclassification feedback, sponsored campaign management, and share-card HTML generation.
/// Extracted from ArticlesController (Priority 2.1) to adhere to Single Responsibility Principle.
/// </summary>
public interface IArticleIngestionService
{
    Task<IngestArticlesResponse> IngestArticlesAsync(
        IngestArticlesRequest request,
        CancellationToken cancellationToken = default);

    Task<Article?> UpdateCategoryAsync(
        string id,
        string newCategory,
        CancellationToken cancellationToken = default);

    Task<List<ArticleDeltaDto>> GetArticleDeltasAsync(
        DateTime? sinceUtc,
        CancellationToken cancellationToken = default);

    Task<List<ArticleDto>> GetSponsoredArticlesAsync(
        string? platform = null,
        string? zone = null,
        CancellationToken ct = default);

    Task<(Article? Article, string? ValidationError)> CreateSponsoredArticleAsync(
        CreateSponsoredArticleRequest request,
        CancellationToken ct = default);

    Task<(Article? Article, bool NotFound, string? ValidationError)> UpdateSponsoredArticleAsync(
        string id,
        UpdateSponsoredArticleRequest request,
        CancellationToken ct = default);

    Task<bool> DeleteSponsoredArticleAsync(
        string id,
        CancellationToken ct = default);

    Task<bool> TrackImpressionAsync(string id, CancellationToken ct = default);

    Task<bool> TrackClickAsync(string id, CancellationToken ct = default);

    Task<string?> RenderShareCardHtmlAsync(string id, CancellationToken ct = default);
}
