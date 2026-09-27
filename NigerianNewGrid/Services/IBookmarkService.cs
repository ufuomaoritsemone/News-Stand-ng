using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public interface IBookmarkService
{
    IReadOnlyList<BriefingItem> GetBookmarks();
    bool IsBookmarked(string articleId);
    bool ToggleBookmark(BriefingItem item);
    void RemoveBookmark(string articleId);
    void ClearAllBookmarks();
    event EventHandler? BookmarksChanged;
}
