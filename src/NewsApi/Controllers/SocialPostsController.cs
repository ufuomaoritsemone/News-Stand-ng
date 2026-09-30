using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/social-posts")]
public class SocialPostsController : ControllerBase
{
    private static int _isSyncing;

    private readonly NewsDbContext _db;
    private readonly ISocialFeedService _socialService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SocialPostsController> _logger;

    public SocialPostsController(
        NewsDbContext db,
        ISocialFeedService socialService,
        IServiceScopeFactory scopeFactory,
        ILogger<SocialPostsController> logger)
    {
        _db = db;
        _socialService = socialService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the latest breaking tweets and stories from monitored accounts (ordered by recency).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLatest(
        [FromQuery] int limit = 20,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var latestPost = await _db.SocialPosts.AsNoTracking().OrderByDescending(s => s.PublishedAt).FirstOrDefaultAsync(cancellationToken);

        // If no social posts exist yet, or newest is > 2 hours old, trigger sync
        if (latestPost == null)
        {
            try
            {
                await _socialService.SyncAllHandlesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Initial social sync failed during GetLatest");
            }
        }
        else if (latestPost.PublishedAt < DateTime.UtcNow.AddHours(-2))
        {
            // Gate background sync to a single concurrent execution with an isolated DI scope
            if (Interlocked.CompareExchange(ref _isSyncing, 1, 0) == 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<ISocialFeedService>();
                        await svc.SyncAllHandlesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Background social sync failed in SocialPostsController");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isSyncing, 0);
                    }
                });
            }
        }

        var query = _db.SocialPosts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Category.ToLower() == category.ToLower());
        }

        var posts = await query
            .OrderByDescending(s => s.PublishedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);

        var dtos = posts.Select(ToDto).ToList();
        return Ok(dtos);
    }

    private static SocialPostDto ToDto(SocialPost p) => new()
    {
        Id = p.Id,
        AuthorName = p.AuthorName,
        AuthorHandle = p.AuthorHandle,
        AuthorAvatarUrl = p.AuthorAvatarUrl,
        Content = p.Content,
        PostUrl = p.PostUrl,
        MediaUrl = p.MediaUrl,
        Category = p.Category,
        LikesCount = p.LikesCount,
        RetweetsCount = p.RetweetsCount,
        PublishedAt = p.PublishedAt,
        CreatedAt = p.CreatedAt
    };

    /// <summary>
    /// Triggers an immediate refresh of tweets from monitored handles.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncHandles(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _socialService.SyncAllHandlesAsync(cancellationToken);
            return Ok(new { message = $"Successfully synced social feeds. {count} new posts added.", newCount = count });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error syncing social posts");
            return StatusCode(500, new { message = "Sync failed. An error occurred while synchronizing social posts." });
        }
    }

    /// <summary>
    /// Deletes a specific social post.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        var post = await _db.SocialPosts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (post is not null)
        {
            _db.SocialPosts.Remove(post);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }
}
