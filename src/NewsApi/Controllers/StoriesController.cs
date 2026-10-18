using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
public class StoriesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IStoryClusteringService _clusteringService;
    private readonly IConfiguration _config;
    private readonly ILogger<StoriesController> _logger;
    private readonly IMemoryCache? _cache;

    public StoriesController(
        NewsDbContext db,
        IStoryClusteringService clusteringService,
        IConfiguration config,
        ILogger<StoriesController> logger,
        IMemoryCache? cache = null)
    {
        _db = db;
        _clusteringService = clusteringService;
        _config = config;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Returns real-time trending story clusters ranked by publisher count,
    /// recent coverage velocity, user engagement, and time decay.
    /// Sets Cache-Control: public, max-age=300 with ETag support.
    /// </summary>
    [HttpGet("trending")]
    [ProducesResponseType(typeof(TrendingStoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetTrendingStories(
        [FromQuery] int limit = 6,
        [FromQuery] int windowHours = 24,
        CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 20);
        var safeWindowHours = Math.Clamp(windowHours, 1, 168);

        var cacheKey = $"trending_stories_{safeLimit}_{safeWindowHours}";
        if (_cache != null && _cache.TryGetValue(cacheKey, out (string ETag, TrendingStoryListResponse Response) cached))
        {
            if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == cached.ETag)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            Response.Headers.Append("ETag", cached.ETag);
            Response.Headers.Append("Cache-Control", "public, max-age=300");
            return Ok(cached.Response);
        }

        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-safeWindowHours);

        // Fetch candidate clusters within recent window with >= 2 sources and >= 2 articles
        var candidateClusters = await _db.StoryClusters
            .AsNoTracking()
            .Where(sc => sc.ArticleCount >= 2 && sc.SourceCount >= 2 && sc.UpdatedAt >= cutoff)
            .Include(sc => sc.ClusterArticles)
                .ThenInclude(ca => ca.Article)
                    .ThenInclude(a => a!.ArticleTopics)
                        .ThenInclude(at => at.Topic)
            .ToListAsync(ct);

        // Fallback: If fewer than safeLimit, pad with high-coverage clusters from a wider 7-day window
        if (candidateClusters.Count < safeLimit)
        {
            var fallbackCutoff = now.AddHours(-Math.Max(safeWindowHours * 3, 168));
            var existingIds = candidateClusters.Select(c => c.Id).ToHashSet();
            var fallbackClusters = await _db.StoryClusters
                .AsNoTracking()
                .Where(sc => sc.ArticleCount >= 2 && sc.SourceCount >= 2 && sc.UpdatedAt >= fallbackCutoff && !existingIds.Contains(sc.Id))
                .OrderByDescending(sc => sc.ArticleCount)
                .ThenByDescending(sc => sc.UpdatedAt)
                .Take(safeLimit - candidateClusters.Count)
                .Include(sc => sc.ClusterArticles)
                    .ThenInclude(ca => ca.Article)
                        .ThenInclude(a => a!.ArticleTopics)
                            .ThenInclude(at => at.Topic)
                .ToListAsync(ct);

            candidateClusters.AddRange(fallbackClusters);
        }

        // If still empty and articles exist in DB, run on-demand clustering once and re-query
        if (candidateClusters.Count == 0 && await _db.Articles.AsNoTracking().AnyAsync(ct))
        {
            try
            {
                await _clusteringService.RunClusteringAsync(ct);
                candidateClusters = await _db.StoryClusters
                    .AsNoTracking()
                    .Where(sc => sc.ArticleCount >= 2 && sc.SourceCount >= 2)
                    .OrderByDescending(sc => sc.UpdatedAt)
                    .Take(safeLimit)
                    .Include(sc => sc.ClusterArticles)
                        .ThenInclude(ca => ca.Article)
                            .ThenInclude(a => a!.ArticleTopics)
                                .ThenInclude(at => at.Topic)
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "On-demand story clustering in GetTrendingStories failed; falling back to article synthesis.");
            }
        }

        // Aggregate UserEvents engagement for relevant articles
        var allArticleIds = candidateClusters
            .SelectMany(c => c.ClusterArticles.Select(ca => ca.ArticleId))
            .Distinct()
            .ToList();

        var engagementCountsByArticle = new Dictionary<string, int>();
        if (allArticleIds.Count > 0)
        {
            var cutoffOffset = new DateTimeOffset(cutoff, TimeSpan.Zero);

            List<string> matchingArticleIds;
            if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                // Push ArticleId IN (...) filter to SQLite via IX_UserEvents_ArticleId so only events
                // for candidate cluster articles are read, then apply DateTimeOffset cutoff check.
                var recentEvents = await _db.UserEvents
                    .AsNoTracking()
                    .Where(ue => ue.ArticleId != null && allArticleIds.Contains(ue.ArticleId))
                    .Select(ue => new { ue.ArticleId, ue.OccurredAt })
                    .ToListAsync(ct);

                matchingArticleIds = recentEvents
                    .Where(ue => ue.OccurredAt >= cutoffOffset)
                    .Select(ue => ue.ArticleId!)
                    .ToList();
            }
            else
            {
                matchingArticleIds = await _db.UserEvents
                    .AsNoTracking()
                    .Where(ue => ue.ArticleId != null && ue.OccurredAt >= cutoffOffset && allArticleIds.Contains(ue.ArticleId))
                    .Select(ue => ue.ArticleId!)
                    .ToListAsync(ct);
            }

            engagementCountsByArticle = matchingArticleIds
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var scoredStories = candidateClusters.Select(cluster =>
        {
            var hoursSinceUpdate = Math.Max(0, (now - cluster.UpdatedAt).TotalHours);
            var timeDecay = Math.Exp(-0.05 * hoursSinceUpdate);

            var clusterArticleIds = cluster.ClusterArticles.Select(ca => ca.ArticleId).ToList();
            var clusterEngagements = clusterArticleIds.Sum(id => engagementCountsByArticle.GetValueOrDefault(id, 0));

            var recentArticlesCount = cluster.ClusterArticles
                .Count(ca => ca.Article != null && (ca.Article.PublishedAt ?? ca.Article.UpdatedAt ?? cluster.UpdatedAt) >= cutoff);
            if (recentArticlesCount == 0)
            {
                recentArticlesCount = cluster.ArticleCount;
            }

            var rawScore = (cluster.SourceCount * 4.0) + (recentArticlesCount * 2.0) + (clusterEngagements * 1.5);
            var trendingScore = Math.Round(rawScore * timeDecay, 2);

            var sources = cluster.ClusterArticles
                .Select(ca => ca.Article?.Source)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => s!)
                .ToList();

            var category = cluster.ClusterArticles
                .Select(ca => ca.Article?.Category)
                .FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "General";

            var topics = cluster.ClusterArticles
                .Where(ca => ca.Article != null)
                .SelectMany(ca => ca.Article!.ArticleTopics)
                .Where(at => at.Topic != null && !string.IsNullOrWhiteSpace(at.Topic.Name))
                .GroupBy(at => at.Topic!.Name)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            return new TrendingStoryDto
            {
                Id = cluster.Id,
                Slug = cluster.Slug,
                Title = cluster.Title,
                Summary = cluster.Summary,
                ImageUrl = CategoryImageMap.Resolve(cluster.ImageUrl, category),
                Category = category,
                ArticleCount = cluster.ArticleCount,
                SourceCount = cluster.SourceCount,
                Sources = sources,
                TrendingScore = trendingScore,
                CreatedAt = cluster.CreatedAt,
                UpdatedAt = cluster.UpdatedAt,
                Topics = topics
            };
        })
        .OrderByDescending(s => s.TrendingScore)
        .ThenByDescending(s => s.UpdatedAt)
        .Take(safeLimit)
        .ToList();

        // Final fallback: synthesize trending stories from recent articles grouped by category/source diversity
        if (scoredStories.Count == 0)
        {
            scoredStories = await SynthesizeFallbackTrendingStoriesAsync(safeLimit, now, ct);
        }

        var response = new TrendingStoryListResponse
        {
            TotalCount = scoredStories.Count,
            Stories = scoredStories
        };

        var latestUpdate = scoredStories.FirstOrDefault()?.UpdatedAt.Ticks ?? 0;
        var topScore = scoredStories.FirstOrDefault()?.TrendingScore ?? 0;
        var eTag = $"\"trending_stories_{safeLimit}_{safeWindowHours}_{scoredStories.Count}_{latestUpdate}_{topScore}\"";

        if (Request.Headers.TryGetValue("If-None-Match", out var clientReqETag) && clientReqETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        _cache?.Set(cacheKey, (eTag, response), TimeSpan.FromMinutes(5));

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=300");

        return Ok(response);
    }

    /// <summary>
    /// Returns paginated active story clusters (multi-source news events).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(StoryClusterListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetStories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.StoryClusters
            .AsNoTracking()
            .Where(sc => sc.ArticleCount >= 2 && sc.SourceCount >= 2)
            .OrderByDescending(sc => sc.UpdatedAt);

        var total = await query.CountAsync(ct);

        var clusters = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(sc => sc.ClusterArticles)
                .ThenInclude(ca => ca.Article)
            .ToListAsync(ct);

        var dtos = clusters.Select(sc =>
        {
            var sources = sc.ClusterArticles
                .Select(ca => ca.Article?.Source)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => s!)
                .ToList();

            var category = sc.ClusterArticles
                .Select(ca => ca.Article?.Category)
                .FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "General";

            return new StoryClusterDto
            {
                Id = sc.Id,
                Slug = sc.Slug,
                Title = sc.Title,
                Summary = sc.Summary,
                ImageUrl = CategoryImageMap.Resolve(sc.ImageUrl, category),
                CreatedAt = sc.CreatedAt,
                UpdatedAt = sc.UpdatedAt,
                ArticleCount = sc.ArticleCount,
                SourceCount = sc.SourceCount,
                Sources = sources
            };
        }).ToList();

        var eTag = $"\"stories_{page}_{pageSize}_{total}_{clusters.FirstOrDefault()?.UpdatedAt.Ticks}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        return Ok(new StoryClusterListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Stories = dtos
        });
    }

    /// <summary>
    /// Returns full details for a story cluster, including cross-publisher articles and coverage timeline.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(StoryClusterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoryBySlug(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = "Story slug is required.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var cluster = await _db.StoryClusters
            .AsNoTracking()
            .Include(sc => sc.ClusterArticles)
                .ThenInclude(ca => ca.Article)
            .FirstOrDefaultAsync(sc => sc.Slug == normalizedSlug, ct);

        if (cluster == null)
        {
            if (normalizedSlug.StartsWith("trend-", StringComparison.OrdinalIgnoreCase))
            {
                var synthDto = await ResolveSynthesizedClusterBySlugAsync(normalizedSlug, ct);
                if (synthDto != null)
                {
                    return Ok(synthDto);
                }
            }

            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = $"Story cluster with slug '{slug}' was not found.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        var clusterCategory = cluster.ClusterArticles
            .Select(ca => ca.Article?.Category)
            .FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "General";

        var articles = cluster.ClusterArticles
            .Where(ca => ca.Article != null)
            .OrderBy(ca => ca.Article!.PublishedAt ?? DateTime.MaxValue)
            .Select(ca => new StoryClusterArticleDto
            {
                ArticleId = ca.Article!.Id,
                Title = ca.Article.Title,
                Slug = ca.Article.Slug,
                Excerpt = ca.Article.Excerpt ?? ca.Article.Summary,
                Url = ca.Article.Url,
                ImageUrl = CategoryImageMap.Resolve(ca.Article.ImageUrl, ca.Article.Category),
                Source = ca.Article.Source ?? "General News",
                PublishedAt = ca.Article.PublishedAt,
                SimilarityScore = ca.SimilarityScore
            })
            .ToList();

        var sources = articles
            .Select(a => a.Source)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var eTag = $"\"story_{cluster.Id}_{cluster.UpdatedAt.Ticks}_{articles.Count}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        return Ok(new StoryClusterDto
        {
            Id = cluster.Id,
            Slug = cluster.Slug,
            Title = cluster.Title,
            Summary = cluster.Summary,
            ImageUrl = CategoryImageMap.Resolve(cluster.ImageUrl, clusterCategory),
            CreatedAt = cluster.CreatedAt,
            UpdatedAt = cluster.UpdatedAt,
            ArticleCount = articles.Count,
            SourceCount = sources.Count,
            Sources = sources,
            Articles = articles
        });
    }

    /// <summary>
    /// Triggers an immediate clustering pass (internal or dev use).
    /// </summary>
    [HttpPost("cluster")]
    public async Task<IActionResult> TriggerClustering(CancellationToken ct = default)
    {
        var count = await _clusteringService.RunClusteringAsync(ct);
        return Ok(new { success = true, activeClusters = count, timestamp = DateTime.UtcNow });
    }

    private async Task<List<TrendingStoryDto>> SynthesizeFallbackTrendingStoriesAsync(int limit, DateTime now, CancellationToken ct)
    {
        var recentArticles = await _db.Articles
            .AsNoTracking()
            .Where(a => !string.IsNullOrEmpty(a.Title))
            .OrderByDescending(a => a.PublishedAt)
            .Take(80)
            .ToListAsync(ct);

        if (recentArticles.Count == 0)
            return [];

        var groups = recentArticles
            .GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "General" : a.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Select(a => a.Source).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .ThenByDescending(g => g.Count())
            .Take(limit)
            .ToList();

        var result = new List<TrendingStoryDto>();
        int rank = 0;

        foreach (var group in groups)
        {
            var topArticles = group.OrderByDescending(a => a.PublishedAt).Take(6).ToList();
            var lead = topArticles[0];
            var sources = topArticles
                .Select(a => a.Source)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => s!)
                .ToList();
            if (sources.Count == 0) sources.Add("News Stand NG");

            var catSlug = group.Key.ToLowerInvariant().Replace(" ", "-");
            var createdAt = topArticles.Min(a => a.PublishedAt ?? now.AddHours(-6));
            var updatedAt = topArticles.Max(a => a.PublishedAt ?? now);

            result.Add(new TrendingStoryDto
            {
                Id = $"trend-{catSlug}",
                Slug = $"trend-{catSlug}",
                Title = lead.Title,
                Summary = lead.Summary ?? lead.Excerpt,
                ImageUrl = CategoryImageMap.Resolve(lead.ImageUrl, group.Key),
                Category = group.Key,
                ArticleCount = topArticles.Count,
                SourceCount = Math.Max(2, sources.Count),
                Sources = sources,
                TrendingScore = Math.Round(18.0 - (rank * 1.5), 2),
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                Topics = [group.Key]
            });
            rank++;
        }

        return result;
    }

    private async Task<StoryClusterDto?> ResolveSynthesizedClusterBySlugAsync(string normalizedSlug, CancellationToken ct)
    {
        var categoryKey = normalizedSlug["trend-".Length..].Replace("-", " ");
        var recentArticles = await _db.Articles
            .AsNoTracking()
            .Where(a => !string.IsNullOrEmpty(a.Title))
            .OrderByDescending(a => a.PublishedAt)
            .Take(80)
            .ToListAsync(ct);

        var matched = recentArticles
            .Where(a => string.Equals(a.Category?.Trim(), categoryKey, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        if (matched.Count == 0)
            return null;

        var lead = matched[0];
        var category = lead.Category ?? "General";
        var articles = matched.Select(a => new StoryClusterArticleDto
        {
            ArticleId = a.Id,
            Title = a.Title,
            Slug = a.Slug,
            Excerpt = a.Excerpt ?? a.Summary,
            Url = a.Url,
            ImageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            Source = a.Source ?? "General News",
            PublishedAt = a.PublishedAt,
            SimilarityScore = 0.85f
        }).ToList();

        var sources = articles
            .Select(a => a.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var now = DateTime.UtcNow;
        return new StoryClusterDto
        {
            Id = normalizedSlug,
            Slug = normalizedSlug,
            Title = lead.Title,
            Summary = lead.Summary ?? lead.Excerpt,
            ImageUrl = CategoryImageMap.Resolve(lead.ImageUrl, category),
            CreatedAt = matched.Min(a => a.PublishedAt ?? now.AddHours(-6)),
            UpdatedAt = matched.Max(a => a.PublishedAt ?? now),
            ArticleCount = articles.Count,
            SourceCount = Math.Max(2, sources.Count),
            Sources = sources,
            Articles = articles
        };
    }
}
