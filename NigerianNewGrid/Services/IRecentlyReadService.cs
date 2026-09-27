using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public interface IRecentlyReadService
{
    IReadOnlyList<BriefingItem> GetRecentlyRead();
    void RecordRead(BriefingItem item);
    void RecordRead(string url, string? title, string? imageUrl, string? category, string? articleId);
    void ClearRecentlyRead();
    event EventHandler? RecentlyReadChanged;
}
