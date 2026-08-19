using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/video-stories")]
public class VideoStoriesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly YouTubeFeedService _ytService;
    private readonly ILogger<VideoStoriesController> _logger;

    public VideoStoriesController(NewsDbContext db, YouTubeFeedService ytService, ILogger<VideoStoriesController> logger)
    {
        _db = db;
        _ytService = ytService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the latest video stories posted by monitored YouTube channels (ordered by recency).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLatest([FromQuery] int limit = 20, [FromQuery] string? category = null)
    {
        var latestVideo = await _db.VideoStories.AsNoTracking().OrderByDescending(v => v.PublishedAt).FirstOrDefaultAsync();

        // If no video stories exist yet, or newest is > 2 hours old, trigger sync
        if (latestVideo == null)
        {
            try
            {
                await _ytService.SyncAllChannelsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Initial YouTube sync failed during GetLatest");
            }
        }
        else if (latestVideo.PublishedAt < DateTime.UtcNow.AddHours(-2))
        {
            // Stale-while-revalidate background refresh
            _ = Task.Run(async () =>
            {
                try { await _ytService.SyncAllChannelsAsync(); } catch { }
            });
        }

        var query = _db.VideoStories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.Category.ToLower() == category.ToLower());
        }

        var stories = await query
            .OrderByDescending(v => v.PublishedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync();

        return Ok(stories);
    }

    /// <summary>
    /// Triggers an immediate refresh of video stories from all registered YouTube channels.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncChannels()
    {
        try
        {
            var count = await _ytService.SyncAllChannelsAsync();
            return Ok(new { message = $"Successfully synced YouTube feeds. {count} new stories added.", newCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing YouTube video channels");
            return StatusCode(500, new { message = $"Sync failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets top trending video stories in Nigeria from YouTube.
    /// </summary>
    [HttpGet("trending")]
    public async Task<IActionResult> GetTrending([FromQuery] int limit = 20, [FromQuery] string? category = null)
    {
        var latestTrending = await _db.VideoStories.AsNoTracking().Where(v => v.IsTrending).OrderByDescending(v => v.PublishedAt).FirstOrDefaultAsync();

        // If no trending stories exist yet, or data is stale, trigger trending sync
        if (latestTrending == null)
        {
            try
            {
                await _ytService.SyncTrendingNewsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Initial YouTube trending sync failed during GetTrending");
            }
        }
        else if (latestTrending.PublishedAt < DateTime.UtcNow.AddHours(-2))
        {
            _ = Task.Run(async () =>
            {
                try { await _ytService.SyncTrendingNewsAsync(); } catch { }
            });
        }

        var query = _db.VideoStories.AsNoTracking().Where(v => v.IsTrending).AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.Category.ToLower() == category.ToLower());
        }

        var trendingStories = await query
            .OrderBy(v => v.TrendingRank ?? int.MaxValue)
            .ThenByDescending(v => v.ViewCount)
            .ThenByDescending(v => v.PublishedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync();

        return Ok(trendingStories);
    }

    /// <summary>
    /// Triggers an immediate refresh of trending YouTube video stories.
    /// </summary>
    [HttpPost("trending/sync")]
    public async Task<IActionResult> SyncTrending()
    {
        try
        {
            var count = await _ytService.SyncTrendingNewsAsync();
            return Ok(new { message = $"Successfully synced trending YouTube news. {count} stories updated.", updatedCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing trending YouTube stories");
            return StatusCode(500, new { message = $"Trending sync failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Triggers an immediate synchronization of both channel feeds and trending YouTube news.
    /// </summary>
    [HttpPost("sync/all")]
    public async Task<IActionResult> SyncAll()
    {
        try
        {
            var channelCount = await _ytService.SyncAllChannelsAsync();
            var trendingCount = await _ytService.SyncTrendingNewsAsync();
            return Ok(new
            {
                message = "Successfully synchronized all YouTube channels and trending feeds.",
                channelsStoriesSynced = channelCount,
                trendingStoriesSynced = trendingCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing all YouTube channels and trending stories");
            return StatusCode(500, new { message = $"Full sync failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Deletes a specific video story.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var story = await _db.VideoStories.FirstOrDefaultAsync(v => v.Id == id);
        if (story is not null)
        {
            _db.VideoStories.Remove(story);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }
}
