using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// Repository abstraction for read history tracking in SQLite.
/// </summary>
public interface IReadHistoryRepository
{
    Task MarkAsReadAsync(string articleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BriefingItem>> GetRecentlyReadAsync(int limit = 50, CancellationToken cancellationToken = default);
    event EventHandler? RecentlyReadChanged;
}
