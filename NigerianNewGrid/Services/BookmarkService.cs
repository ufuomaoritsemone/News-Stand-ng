using System.Diagnostics;
using System.Text.Json;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public class BookmarkService : IBookmarkService
{
    private const string BookmarksKey = "bookmarks";
    private readonly List<BriefingItem> _bookmarks = [];
    private bool _initialized;

    public event EventHandler? BookmarksChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<BriefingItem> GetBookmarks()
    {
        EnsureLoaded();
        return _bookmarks.AsReadOnly();
    }

    public bool IsBookmarked(string articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return false;
        EnsureLoaded();
        return _bookmarks.Any(b => b.Id == articleId);
    }

    public bool ToggleBookmark(BriefingItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;
        EnsureLoaded();

        bool wasAdded;
        var existing = _bookmarks.FirstOrDefault(b => b.Id == item.Id);
        if (existing != null)
        {
            _bookmarks.Remove(existing);
            wasAdded = false;
        }
        else
        {
            _bookmarks.Add(item);
            wasAdded = true;
        }

        Save();
        BookmarksChanged?.Invoke(this, EventArgs.Empty);
        return wasAdded;
    }

    public void RemoveBookmark(string articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return;
        EnsureLoaded();

        int removed = _bookmarks.RemoveAll(b => b.Id == articleId);
        if (removed > 0)
        {
            Save();
            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EnsureLoaded()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            var json = Preferences.Get(BookmarksKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var list = JsonSerializer.Deserialize<List<BriefingItem>>(json, JsonOptions);
                if (list != null)
                {
                    _bookmarks.Clear();
                    _bookmarks.AddRange(list);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BookmarkService] Failed to load bookmarks from Preferences: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_bookmarks, JsonOptions);
            Preferences.Set(BookmarksKey, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BookmarkService] Failed to persist bookmarks: {ex.Message}");
        }
    }
}
