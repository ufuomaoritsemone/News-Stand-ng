using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// Repository abstraction for story persistence and cache queries in SQLite.
/// </summary>
public interface IStoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<List<BriefingCategory>> GetCachedBriefingAsync(int days = 14, int topPerCategory = 0, int maxTotalStories = 60, CancellationToken cancellationToken = default);
    Task<List<BriefingItem>> GetStoriesByCategoryAsync(string category, int days = 14, int limit = 50, CancellationToken cancellationToken = default);
    Task<int> SaveBriefingAsync(IEnumerable<BriefingCategory> categories, CancellationToken cancellationToken = default);
    Task<int> SaveStoriesAsync(IEnumerable<BriefingItem> stories, string? fallbackCategory = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single story by its unique Guid/ID from the local SQLite cache.
    /// </summary>
    Task<BriefingItem?> GetStoryByIdAsync(string articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a single story in the local SQLite cache.
    /// </summary>
    Task SaveStoryAsync(BriefingItem story, CancellationToken cancellationToken = default);

    Task PruneOldStoriesAsync(int retentionDays = 14, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a set of delta records (e.g. category renames) to articles already
    /// stored in the local SQLite cache without re-fetching full content.
    /// </summary>
    Task ApplyDeltasAsync(IEnumerable<ArticleDeltaDto> deltas, CancellationToken cancellationToken = default);
}
