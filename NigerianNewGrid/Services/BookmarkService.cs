using Microsoft.Extensions.Logging;
using NigerianNewGrid.Extensions;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// In-memory caching facade for bookmark operations, using <see cref="IBookmarkRepository"/> (SQLite)
/// as the single source of truth. Eliminates dual-write hazards with Preferences (Issue #16).
/// </summary>
public class BookmarkService : IBookmarkService
{
    private readonly IBookmarkRepository? _repository;
    private readonly ILogger<BookmarkService>? _logger;
    private readonly List<BriefingItem> _bookmarks = [];
    private readonly object _lock = new();
    private bool _initialized;

    public event EventHandler? BookmarksChanged;

    public BookmarkService(
        IBookmarkRepository? repository = null,
        ILogger<BookmarkService>? logger = null)
    {
        _repository = repository;
        _logger = logger;

        if (_repository != null)
        {
            _repository.BookmarksChanged += OnRepositoryBookmarksChanged;
            InitializeFromRepositoryAsync().SafeFireAndForget(_logger, "BookmarkService:Init");
        }
    }

    private void OnRepositoryBookmarksChanged(object? sender, EventArgs e)
    {
        InitializeFromRepositoryAsync().SafeFireAndForget(_logger, "BookmarkService:RepoChanged");
    }

    private async Task InitializeFromRepositoryAsync()
    {
        if (_repository == null) return;

        try
        {
            var dbList = await _repository.GetBookmarksAsync();
            lock (_lock)
            {
                _bookmarks.Clear();
                _bookmarks.AddRange(dbList);
                _initialized = true;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                BookmarksChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[BookmarkService] Failed to load bookmarks from SQLite repository");
        }
    }

    public IReadOnlyList<BriefingItem> GetBookmarks()
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _bookmarks.ToList().AsReadOnly();
        }
    }

    public bool IsBookmarked(string articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return false;
        EnsureLoaded();
        lock (_lock)
        {
            return _bookmarks.Any(b => b.Id == articleId);
        }
    }

    public bool ToggleBookmark(BriefingItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;
        EnsureLoaded();

        bool wasAdded;
        lock (_lock)
        {
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
        }

        if (_repository != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _repository.ToggleBookmarkAsync(item);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[BookmarkService] SQLite ToggleBookmarkAsync failed; reverting state");
                    await InitializeFromRepositoryAsync();
                }
            }).SafeFireAndForget(_logger, "BookmarkService:ToggleWrite");
        }

        BookmarksChanged?.Invoke(this, EventArgs.Empty);
        return wasAdded;
    }

    public void RemoveBookmark(string articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return;
        EnsureLoaded();

        bool removed;
        lock (_lock)
        {
            removed = _bookmarks.RemoveAll(b => b.Id == articleId) > 0;
        }

        if (removed)
        {
            if (_repository != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _repository.RemoveBookmarkAsync(articleId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[BookmarkService] SQLite RemoveBookmarkAsync failed; reverting state");
                        await InitializeFromRepositoryAsync();
                    }
                }).SafeFireAndForget(_logger, "BookmarkService:RemoveWrite");
            }

            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearAllBookmarks()
    {
        EnsureLoaded();
        lock (_lock)
        {
            if (_bookmarks.Count == 0) return;
            _bookmarks.Clear();
        }

        if (_repository != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _repository.ClearAllBookmarksAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[BookmarkService] SQLite ClearAllBookmarksAsync failed; reverting state");
                    await InitializeFromRepositoryAsync();
                }
            }).SafeFireAndForget(_logger, "BookmarkService:ClearAllWrite");
        }

        BookmarksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureLoaded()
    {
        if (_initialized) return;

        if (_repository != null)
        {
            try
            {
                var task = _repository.GetBookmarksAsync();
                if (task.IsCompletedSuccessfully)
                {
                    lock (_lock)
                    {
                        _bookmarks.Clear();
                        _bookmarks.AddRange(task.Result);
                        _initialized = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[BookmarkService] EnsureLoaded sync check error");
            }
        }
    }
}
