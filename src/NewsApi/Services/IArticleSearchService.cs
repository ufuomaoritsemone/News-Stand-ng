using NewsApi.Models;

namespace NewsApi.Services;

/// <summary>
/// Parameters for searching articles with filtering, pagination, and date constraints.
/// </summary>
public record ArticleSearchParams(
    string Search,
    string? Source = null,
    string? Category = null,
    bool TodayOnly = false,
    int? Days = null,
    int Page = 1,
    int PageSize = 50,
    string? ContentType = null
);

/// <summary>
/// High-performance full-text search service with relevance ranking across SQLite and PostgreSQL.
/// </summary>
public interface IArticleSearchService
{
    Task<List<ArticleDto>> SearchArticlesAsync(
        ArticleSearchParams searchParams,
        CancellationToken cancellationToken = default);
}
