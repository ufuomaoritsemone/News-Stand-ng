using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/video-stories")]
public class VideoStoriesController : ControllerBase
{
    private static int _isSyncingChannels;
    private static DateTimeOffset _lastChannelSyncAttempt = DateTimeOffset.MinValue;

    private static int _isSyncingTrending;
    private static DateTimeOffset _lastTrendingSyncAttempt = DateTimeOffset.MinValue;

    private static readonly TimeSpan MinSyncCooldown = TimeSpan.FromMinutes(15);

    private readonly NewsDbContext _db;
    private readonly IYouTubeFeedService _ytService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VideoStoriesController> _logger;

    public VideoStoriesController(
        NewsDbContext db,
        IYouTubeFeedService ytService,
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<VideoStoriesController> logger)
    {
        _db           = db;
        _ytService    = ytService;
        _scopeFactory = scopeFactory;
        _cache        = cache;
        _logger       = logger;
    }

    /// <summary>
    /// Gets the latest video stories posted by monitored YouTube channels (ordered by recency).
    /// Highly optimized with in-memory caching and non-blocking background sync.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLatest(
        [FromQuery] int limit = 20,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 50);
        var normCategory = string.IsNullOrWhiteSpace(category) ? "All" : category.Trim();
        var cacheKey = $"videostories_latest_{normCategory.ToLowerInvariant()}_{effectiveLimit}";

        if (_cache.TryGetValue(cacheKey, out List<VideoStoryDto>? cached) && cached != null)
        {
            Response.Headers.Append("X-Cache", "HIT");
            return Ok(cached);
        }

        var query = _db.VideoStories.AsNoTracking().AsQueryable();

        if (!string.Equals(normCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.Category.ToLower() == normCategory.ToLower());
        }

        var stories = await query
            .OrderByDescending(v => v.PublishedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        foreach (var s in stories)
        {
            s.ThumbnailUrl = YouTubeFeedService.ResolveBestThumbnail(s.ThumbnailUrl, null, s.VideoId, s.Category);
        }

        // Stale-while-revalidate / initial background sync (throttled gate with isolated DI scope)
        if (stories.Count == 0 || stories[0].PublishedAt < DateTime.UtcNow.AddHours(-2))
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastChannelSyncAttempt >= MinSyncCooldown &&
                Interlocked.CompareExchange(ref _isSyncingChannels, 1, 0) == 0)
            {
                _lastChannelSyncAttempt = now;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var ytService = scope.ServiceProvider.GetRequiredService<IYouTubeFeedService>();
                        await ytService.SyncAllChannelsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Background channel sync failed in VideoStoriesController");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isSyncingChannels, 0);
                    }
                });
            }
        }

        var dtos = stories.Select(ToDto).ToList();

        if (dtos.Count > 0)
        {
            _cache.Set(cacheKey, dtos, new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(3))
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5)));
        }

        Response.Headers.Append("X-Cache", "MISS");
        return Ok(dtos);
    }

    /// <summary>
    /// Triggers an immediate refresh of video stories from all registered YouTube channels.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncChannels(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _ytService.SyncAllChannelsAsync(cancellationToken);
            return Ok(new { message = $"Successfully synced YouTube feeds. {count} new stories added.", newCount = count });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error syncing YouTube video channels");
            return StatusCode(500, new { message = "Sync failed. An error occurred while synchronizing YouTube feeds." });
        }
    }

    /// <summary>
    /// Gets top trending video stories in Nigeria from YouTube.
    /// Highly optimized with in-memory caching and non-blocking background sync.
    /// </summary>
    [HttpGet("trending")]
    public async Task<IActionResult> GetTrending(
        [FromQuery] int limit = 20,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 50);
        var normCategory = string.IsNullOrWhiteSpace(category) ? "All" : category.Trim();
        var cacheKey = $"videostories_trending_{normCategory.ToLowerInvariant()}_{effectiveLimit}";

        if (_cache.TryGetValue(cacheKey, out List<VideoStoryDto>? cached) && cached != null)
        {
            Response.Headers.Append("X-Cache", "HIT");
            return Ok(cached);
        }

        var query = _db.VideoStories.AsNoTracking().Where(v => v.IsTrending).AsQueryable();

        if (!string.Equals(normCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.Category.ToLower() == normCategory.ToLower());
        }

        var trendingStories = await query
            .OrderBy(v => v.TrendingRank ?? int.MaxValue)
            .ThenByDescending(v => v.ViewCount)
            .ThenByDescending(v => v.PublishedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        foreach (var s in trendingStories)
        {
            s.ThumbnailUrl = YouTubeFeedService.ResolveBestThumbnail(s.ThumbnailUrl, null, s.VideoId, s.Category);
        }

        // Stale-while-revalidate / initial background sync (throttled gate with isolated DI scope)
        if (trendingStories.Count == 0 || trendingStories[0].PublishedAt < DateTime.UtcNow.AddHours(-2))
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastTrendingSyncAttempt >= MinSyncCooldown &&
                Interlocked.CompareExchange(ref _isSyncingTrending, 1, 0) == 0)
            {
                _lastTrendingSyncAttempt = now;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var ytService = scope.ServiceProvider.GetRequiredService<IYouTubeFeedService>();
                        await ytService.SyncTrendingNewsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Background trending sync failed in VideoStoriesController");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isSyncingTrending, 0);
                    }
                });
            }
        }

        var dtos = trendingStories.Select(ToDto).ToList();

        if (dtos.Count > 0)
        {
            _cache.Set(cacheKey, dtos, new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(3))
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5)));
        }

        Response.Headers.Append("X-Cache", "MISS");
        return Ok(dtos);
    }

    private static VideoStoryDto ToDto(VideoStory s) => new()
    {
        Id = s.Id,
        VideoId = s.VideoId,
        Title = s.Title,
        Summary = s.Summary,
        VideoUrl = s.VideoUrl,
        ThumbnailUrl = s.ThumbnailUrl,
        ChannelName = s.ChannelName,
        ChannelId = s.ChannelId,
        Duration = s.Duration,
        Category = s.Category,
        PublishedAt = s.PublishedAt,
        CreatedAt = s.CreatedAt,
        IsTrending = s.IsTrending,
        TrendingRank = s.TrendingRank,
        ViewCount = s.ViewCount,
        LikeCount = s.LikeCount
    };

    /// <summary>
    /// Triggers an immediate refresh of trending YouTube video stories.
    /// </summary>
    [HttpPost("trending/sync")]
    public async Task<IActionResult> SyncTrending(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _ytService.SyncTrendingNewsAsync(cancellationToken);
            return Ok(new { message = $"Successfully synced trending YouTube news. {count} stories updated.", updatedCount = count });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error syncing trending YouTube stories");
            return StatusCode(500, new { message = "Trending sync failed. An error occurred while synchronizing trending stories." });
        }
    }

    /// <summary>
    /// Triggers an immediate synchronization of both channel feeds and trending YouTube news.
    /// </summary>
    [HttpPost("sync/all")]
    public async Task<IActionResult> SyncAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var channelCount = await _ytService.SyncAllChannelsAsync(cancellationToken);
            var trendingCount = await _ytService.SyncTrendingNewsAsync(cancellationToken);
            return Ok(new
            {
                message = "Successfully synchronized all YouTube channels and trending feeds.",
                channelsStoriesSynced = channelCount,
                trendingStoriesSynced = trendingCount
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error syncing all YouTube channels and trending stories");
            return StatusCode(500, new { message = "Full sync failed. An error occurred while synchronizing all feeds." });
        }
    }

    /// <summary>
    /// Deletes a specific video story.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        var story = await _db.VideoStories.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (story is not null)
        {
            _db.VideoStories.Remove(story);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }
}
