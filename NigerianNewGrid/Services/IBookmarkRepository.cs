using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// Repository abstraction for bookmark persistence in SQLite.
/// </summary>
public interface IBookmarkRepository
{
    Task<bool> ToggleBookmarkAsync(BriefingItem item, CancellationToken cancellationToken = default);
    Task<bool> IsBookmarkedAsync(string articleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BriefingItem>> GetBookmarksAsync(CancellationToken cancellationToken = default);
    Task RemoveBookmarkAsync(string articleId, CancellationToken cancellationToken = default);
    Task ClearAllBookmarksAsync(CancellationToken cancellationToken = default);
    event EventHandler? BookmarksChanged;
}
