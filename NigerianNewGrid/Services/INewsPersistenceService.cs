using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// Manages SQLite on-device persistence for 14-day rolling story cache,
/// offline briefings, bookmarks, and recently read history.
/// </summary>
public interface INewsPersistenceService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<List<BriefingCategory>> GetCachedBriefingAsync(int days = 14, int topPerCategory = 0, CancellationToken cancellationToken = default);
    Task<List<BriefingItem>> GetStoriesByCategoryAsync(string category, int days = 14, int limit = 50, CancellationToken cancellationToken = default);
    Task<int> SaveBriefingAsync(IEnumerable<BriefingCategory> categories, CancellationToken cancellationToken = default);
    Task<int> SaveStoriesAsync(IEnumerable<BriefingItem> stories, string? fallbackCategory = null, CancellationToken cancellationToken = default);
    Task PruneOldStoriesAsync(int retentionDays = 14, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a set of delta records (e.g. category renames) to articles already
    /// stored in the local SQLite cache without re-fetching full content.
    /// </summary>
    Task ApplyDeltasAsync(IEnumerable<NigerianNewsGrid.Client.Models.ArticleDeltaDto> deltas, CancellationToken cancellationToken = default);

    Task<bool> ToggleBookmarkAsync(BriefingItem item, CancellationToken cancellationToken = default);
    Task<bool> IsBookmarkedAsync(string articleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BriefingItem>> GetBookmarksAsync(CancellationToken cancellationToken = default);
    Task RemoveBookmarkAsync(string articleId, CancellationToken cancellationToken = default);
    Task ClearAllBookmarksAsync(CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(string articleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BriefingItem>> GetRecentlyReadAsync(int limit = 50, CancellationToken cancellationToken = default);

    event EventHandler? BookmarksChanged;
    event EventHandler? RecentlyReadChanged;
}
