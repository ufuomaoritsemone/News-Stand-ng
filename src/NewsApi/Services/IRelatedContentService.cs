using NewsApi.Models;

namespace NewsApi.Services;

/// <summary>
/// Contract for fetching cross-content related stories (articles, videos, social posts).
/// </summary>
public interface IRelatedContentService
{
    Task<RelatedStoriesDto> GetRelatedStoriesAsync(
        string? id,
        string? title,
        string? category,
        int limit,
        CancellationToken cancellationToken = default);
}
