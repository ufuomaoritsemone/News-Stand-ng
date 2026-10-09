using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public StoriesController(
        NewsDbContext db,
        IStoryClusteringService clusteringService,
        IConfiguration config,
        ILogger<StoriesController> logger)
    {
        _db = db;
        _clusteringService = clusteringService;
        _config = config;
        _logger = logger;
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
}
