using System.Diagnostics;
using System.Text.Json;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public class RecentlyReadService : IRecentlyReadService
{
    private const string RecentlyReadKey = "recently_read_articles";
    private const int MaxRecentlyRead = 50;

    private readonly List<BriefingItem> _recentlyRead = [];
    private bool _initialized;
    private readonly object _lock = new();

    public event EventHandler? RecentlyReadChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<BriefingItem> GetRecentlyRead()
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _recentlyRead.ToList().AsReadOnly();
        }
    }

    public void RecordRead(BriefingItem item)
    {
        if (item == null || (string.IsNullOrWhiteSpace(item.Id) && string.IsNullOrWhiteSpace(item.Url)))
            return;

        EnsureLoaded();

        lock (_lock)
        {
            _recentlyRead.RemoveAll(r =>
                (!string.IsNullOrWhiteSpace(item.Id) && r.Id == item.Id) ||
                (!string.IsNullOrWhiteSpace(item.Url) && string.Equals(r.Url, item.Url, StringComparison.OrdinalIgnoreCase)));

            _recentlyRead.Insert(0, item);

            if (_recentlyRead.Count > MaxRecentlyRead)
            {
                _recentlyRead.RemoveRange(MaxRecentlyRead, _recentlyRead.Count - MaxRecentlyRead);
            }

            Save();
        }

        RecentlyReadChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordRead(string url, string? title, string? imageUrl, string? category, string? articleId)
    {
        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(articleId))
            return;

        var item = new BriefingItem
        {
            Id          = articleId ?? Guid.NewGuid().ToString("N"),
            Title       = title ?? "Read Story",
            Url         = url ?? string.Empty,
            ImageUrl    = imageUrl ?? string.Empty,
            Category    = category ?? "General",
            Source      = ArticleDistillerService.IdentifyPublisher(url ?? string.Empty).Name,
            PublishedAt = DateTime.UtcNow
        };

        RecordRead(item);
    }

    public void ClearRecentlyRead()
    {
        lock (_lock)
        {
            _recentlyRead.Clear();
            Preferences.Remove(RecentlyReadKey);
        }

        RecentlyReadChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureLoaded()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                var json = Preferences.Get(RecentlyReadKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var items = JsonSerializer.Deserialize<List<BriefingItem>>(json, JsonOptions);
                    if (items != null)
                    {
                        _recentlyRead.Clear();
                        _recentlyRead.AddRange(items);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RecentlyReadService] Error loading: {ex.Message}");
            }
            finally
            {
                _initialized = true;
            }
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentlyRead, JsonOptions);
            Preferences.Set(RecentlyReadKey, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RecentlyReadService] Error saving: {ex.Message}");
        }
    }
}
