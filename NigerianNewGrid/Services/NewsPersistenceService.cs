using System.Text.Json;
using Microsoft.Extensions.Logging;
using SQLite;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Models;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// High-performance SQLite persistence service for 14-day news stories,
/// bookmarks, and read history using sqlite-net-pcl with WAL mode.
/// </summary>
public class NewsPersistenceService(
    ILogger<NewsPersistenceService> logger) : INewsPersistenceService
{
    private static readonly string DbPath = Path.Combine(FileSystem.AppDataDirectory, "news_cache.db3");
    private const SQLiteOpenFlags Flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create;

    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public event EventHandler? BookmarksChanged;
    public event EventHandler? RecentlyReadChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", $"NewsPersistenceService.InitializeAsync START (_initialized={_initialized})");
#endif
        if (_initialized && _database is not null) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized && _database is not null) return;

#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", $"NewsPersistenceService opening SQLite connection at {DbPath}");
#endif
            _database = new SQLiteAsyncConnection(DbPath, Flags);
            try
            {
                // Enable Write-Ahead Logging to ensure concurrent reads are never blocked by writes
                await _database.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL;").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not set SQLite WAL mode; continuing with default journal mode");
            }
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "NewsPersistenceService creating CachedStoryEntity table");
#endif
            await _database.CreateTableAsync<CachedStoryEntity>().ConfigureAwait(false);

            _initialized = true;
            logger.LogInformation("SQLite NewsPersistenceService initialized successfully at {Path}", DbPath);
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "NewsPersistenceService initialized successfully");
#endif
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize SQLite database at {Path}", DbPath);
#if ANDROID
            Android.Util.Log.Error("APP_DEBUG", $"NewsPersistenceService failed: {ex}");
#endif
            throw;
        }
        finally
        {
            _initLock.Release();
        }

        // Run one-time legacy Preferences migration in the background without blocking startup reads
        _ = Task.Run(async () =>
        {
            try
            {
                await MigrateLegacyPreferencesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Legacy Preferences migration to SQLite encountered a non-fatal error");
            }
        });
    }

    private async Task<SQLiteAsyncConnection> GetDbAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized || _database is null)
        {
            await InitializeAsync(cancellationToken);
        }
        return _database!;
    }

    public async Task<List<BriefingCategory>> GetCachedBriefingAsync(int days = 14, int topPerCategory = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var cutoff = DateTime.UtcNow.AddDays(-days);

            var entities = await db.Table<CachedStoryEntity>()
                .Where(e => e.PublishedAt >= cutoff)
                .OrderByDescending(e => e.PublishedAt)
                .ToListAsync();

            if (entities.Count == 0)
                return [];

            var grouped = entities
                .GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
                .Select(g => new BriefingCategory
                {
                    Category = g.Key,
                    Top = (topPerCategory > 0 ? g.Take(topPerCategory) : g).Select(e => e.ToBriefingItem()).ToList()
                })
                .ToList();

            return grouped;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve cached briefing from SQLite");
            return [];
        }
    }

    public async Task<List<BriefingItem>> GetStoriesByCategoryAsync(string category, int days = 14, int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var cutoff = DateTime.UtcNow.AddDays(-days);

            var query = db.Table<CachedStoryEntity>()
                .Where(e => e.PublishedAt >= cutoff);

            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.Category == category);
            }

            var entities = await query
                .OrderByDescending(e => e.PublishedAt)
                .Take(limit)
                .ToListAsync();

            return entities.Select(e => e.ToBriefingItem()).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get stories by category '{Category}' from SQLite", category);
            return [];
        }
    }

    public async Task<int> SaveBriefingAsync(IEnumerable<BriefingCategory> categories, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var itemsToSave = new List<CachedStoryEntity>();

            foreach (var cat in categories)
            {
                foreach (var item in cat.Top)
                {
                    itemsToSave.Add(CachedStoryEntity.FromBriefingItem(item, cat.Category));
                }
            }

            int newCount = await UpsertStoriesInternalAsync(db, itemsToSave);
            // Automatic rolling prune
            _ = PruneOldStoriesAsync(14, CancellationToken.None);
            return newCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save briefing to SQLite");
            return 0;
        }
    }

    public async Task<int> SaveStoriesAsync(IEnumerable<BriefingItem> stories, string? fallbackCategory = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var itemsToSave = stories
                .Select(s => CachedStoryEntity.FromBriefingItem(s, fallbackCategory ?? s.Category ?? "General"))
                .ToList();

            return await UpsertStoriesInternalAsync(db, itemsToSave);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save stories to SQLite");
            return 0;
        }
    }

    private static async Task<int> UpsertStoriesInternalAsync(SQLiteAsyncConnection db, List<CachedStoryEntity> items)
    {
        if (items.Count == 0) return 0;

        int insertedCount = 0;

        // Perform fast batched upsert inside a single SQLite transaction to avoid disk round-trips
        await db.RunInTransactionAsync(syncDb =>
        {
            foreach (var item in items)
            {
                var existing = syncDb.Find<CachedStoryEntity>(item.Id);
                if (existing != null)
                {
                    item.IsBookmarked = existing.IsBookmarked;
                    item.IsRead = existing.IsRead;
                    item.ReadAt = existing.ReadAt;
                    syncDb.Update(item);
                }
                else
                {
                    syncDb.Insert(item);
                    insertedCount++;
                }
            }
        });

        return insertedCount;
    }

    public async Task PruneOldStoriesAsync(int retentionDays = 14, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            // Delete non-bookmarked stories older than retention cutoff
            var deleted = await db.Table<CachedStoryEntity>()
                .DeleteAsync(s => s.PublishedAt < cutoff && !s.IsBookmarked);

            if (deleted > 0)
            {
                logger.LogInformation("Pruned {Count} expired news stories older than {Days} days from SQLite cache", deleted, retentionDays);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while pruning old news stories from SQLite");
        }
    }

    public async Task<bool> ToggleBookmarkAsync(BriefingItem item, CancellationToken cancellationToken = default)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;

        try
        {
            var db = await GetDbAsync(cancellationToken);
            var existing = await db.FindAsync<CachedStoryEntity>(item.Id);

            bool newBookmarkState;
            if (existing != null)
            {
                existing.IsBookmarked = !existing.IsBookmarked;
                newBookmarkState = existing.IsBookmarked;
                await db.UpdateAsync(existing);
            }
            else
            {
                var entity = CachedStoryEntity.FromBriefingItem(item);
                entity.IsBookmarked = true;
                newBookmarkState = true;
                await db.InsertAsync(entity);
            }

            BookmarksChanged?.Invoke(this, EventArgs.Empty);
            return newBookmarkState;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle bookmark for story {Id}", item.Id);
            return false;
        }
    }

    public async Task<bool> IsBookmarkedAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return false;

        try
        {
            var db = await GetDbAsync(cancellationToken);
            var existing = await db.FindAsync<CachedStoryEntity>(articleId);
            return existing?.IsBookmarked ?? false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check bookmark status for story {Id}", articleId);
            return false;
        }
    }

    public async Task<IReadOnlyList<BriefingItem>> GetBookmarksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var entities = await db.Table<CachedStoryEntity>()
                .Where(s => s.IsBookmarked)
                .OrderByDescending(s => s.PublishedAt)
                .ToListAsync();

            return entities.Select(e => e.ToBriefingItem()).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve bookmarks from SQLite");
            return [];
        }
    }

    public async Task RemoveBookmarkAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return;

        try
        {
            var db = await GetDbAsync(cancellationToken);
            var existing = await db.FindAsync<CachedStoryEntity>(articleId);
            if (existing != null && existing.IsBookmarked)
            {
                existing.IsBookmarked = false;
                await db.UpdateAsync(existing);
                BookmarksChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove bookmark for story {Id}", articleId);
        }
    }

    public async Task ClearAllBookmarksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var bookmarked = await db.Table<CachedStoryEntity>()
                .Where(s => s.IsBookmarked)
                .ToListAsync();

            if (bookmarked.Count > 0)
            {
                foreach (var item in bookmarked)
                {
                    item.IsBookmarked = false;
                    await db.UpdateAsync(item);
                }
                BookmarksChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear bookmarks in SQLite");
        }
    }

    public async Task MarkAsReadAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return;

        try
        {
            var db = await GetDbAsync(cancellationToken);
            var existing = await db.FindAsync<CachedStoryEntity>(articleId);
            if (existing != null)
            {
                existing.IsRead = true;
                existing.ReadAt = DateTime.UtcNow;
                await db.UpdateAsync(existing);
                RecentlyReadChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark story {Id} as read in SQLite", articleId);
        }
    }

    public async Task<IReadOnlyList<BriefingItem>> GetRecentlyReadAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            var entities = await db.Table<CachedStoryEntity>()
                .Where(s => s.IsRead && s.ReadAt != null)
                .OrderByDescending(s => s.ReadAt)
                .Take(limit)
                .ToListAsync();

            return entities.Select(e => e.ToBriefingItem()).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve recently read stories from SQLite");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task ApplyDeltasAsync(IEnumerable<NigerianNewsGrid.Client.Models.ArticleDeltaDto> deltas, CancellationToken cancellationToken = default)
    {
        var deltaList = deltas?.ToList();
        if (deltaList is not { Count: > 0 }) return;

        try
        {
            var db = await GetDbAsync(cancellationToken);

            await db.RunInTransactionAsync(syncDb =>
            {
                foreach (var delta in deltaList)
                {
                    if (string.IsNullOrWhiteSpace(delta.Id)) continue;

                    var existing = syncDb.Find<CachedStoryEntity>(delta.Id);
                    if (existing == null) continue;

                    bool changed = false;

                    // Apply category rename
                    if (!string.IsNullOrWhiteSpace(delta.Category) &&
                        !string.Equals(existing.Category, delta.Category, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.Category = delta.Category;
                        changed = true;
                    }

                    // Track server-side UpdatedAt timestamp locally
                    if (delta.UpdatedAt != default && delta.UpdatedAt != existing.UpdatedAt)
                    {
                        existing.UpdatedAt = delta.UpdatedAt;
                        changed = true;
                    }

                    if (changed)
                    {
                        syncDb.Update(existing);
                    }
                }
            });

            logger.LogInformation("[DeltaSync] Applied {Count} delta record(s) to local SQLite cache", deltaList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DeltaSync] Failed to apply article deltas to SQLite");
        }
    }

    private async Task MigrateLegacyPreferencesAsync()
    {
        try
        {
            // Migrate legacy bookmarks if present in Preferences
            var bookmarksJson = Preferences.Get("bookmarks", string.Empty);
            if (!string.IsNullOrWhiteSpace(bookmarksJson))
            {
                var legacyBookmarks = JsonSerializer.Deserialize<List<BriefingItem>>(bookmarksJson, JsonOptions);
                if (legacyBookmarks is { Count: > 0 } && _database is not null)
                {
                    await _database.RunInTransactionAsync(syncDb =>
                    {
                        foreach (var item in legacyBookmarks)
                        {
                            var entity = CachedStoryEntity.FromBriefingItem(item);
                            entity.IsBookmarked = true;
                            var existing = syncDb.Find<CachedStoryEntity>(entity.Id);
                            if (existing != null)
                            {
                                existing.IsBookmarked = true;
                                syncDb.Update(existing);
                            }
                            else
                            {
                                syncDb.Insert(entity);
                            }
                        }
                    });
                    Preferences.Remove("bookmarks");
                    logger.LogInformation("Successfully migrated {Count} legacy bookmarks to SQLite", legacyBookmarks.Count);
                }
            }

            // Migrate legacy LastBriefing if present
            var lastBriefingJson = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
            if (!string.IsNullOrWhiteSpace(lastBriefingJson))
            {
                var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(lastBriefingJson, JsonOptions);
                if (categories is { Count: > 0 })
                {
                    await SaveBriefingAsync(categories, CancellationToken.None);
                    Preferences.Remove(AppPreferenceKeys.LastBriefing);
                    logger.LogInformation("Successfully migrated legacy LastBriefing to SQLite");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Legacy Preferences migration to SQLite encountered a non-fatal error");
        }
    }
}
