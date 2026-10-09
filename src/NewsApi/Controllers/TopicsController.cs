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
public class TopicsController(
    NewsDbContext db,
    ISlugService slugService,
    ILogger<TopicsController> logger) : ControllerBase
{
    /// <summary>
    /// Returns topic hub metadata and total article count by topic slug.
    /// Sets Cache-Control: public, max-age=60 with ETag support.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(TopicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTopicBySlug(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = "Topic slug cannot be empty.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        logger.LogDebug("Retrieving topic by slug: {Slug}", slug);
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var topic = await db.Topics
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, ct);

        if (topic == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = $"Topic with slug '{slug}' was not found.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        var articleCount = await db.ArticleTopics
            .AsNoTracking()
            .CountAsync(at => at.TopicId == topic.Id, ct);

        var eTag = $"\"topic_{topic.Id}_{articleCount}_{topic.Slug}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        return Ok(new TopicDto
        {
            Id = topic.Id,
            Slug = topic.Slug,
            Name = topic.Name,
            Type = topic.Type.ToString(),
            Description = topic.Description,
            ArticleCount = articleCount,
            CreatedAt = topic.CreatedAt
        });
    }

    /// <summary>
    /// Returns paginated articles tagged under a specific topic hub.
    /// </summary>
    [HttpGet("{slug}/articles")]
    [ProducesResponseType(typeof(PagedResponse<ArticleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTopicArticles(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var topic = await db.Topics
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, ct);

        if (topic == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = $"Topic with slug '{slug}' was not found.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.ArticleTopics
            .AsNoTracking()
            .Where(at => at.TopicId == topic.Id && at.Article != null)
            .OrderByDescending(at => at.Article!.PublishedAt);

        var totalCount = await query.CountAsync(ct);

        var rawArticles = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(at => at.Article!)
            .ToListAsync(ct);

        var articleDtos = rawArticles.Select(a => new ArticleDto
        {
            Id = a.Id,
            Title = a.Title,
            Slug = !string.IsNullOrEmpty(a.Slug) ? a.Slug : slugService.GenerateArticleSlug(a.Title, a.Id),
            Excerpt = a.Excerpt ?? slugService.GenerateExcerpt(a.Content, a.Summary, 300),
            SeoTitle = a.SeoTitle,
            SeoDescription = a.SeoDescription,
            Summary = a.Summary,
            Url = a.Url,
            ImageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            Source = a.Source ?? "News Stand NG",
            Category = a.Category ?? "General",
            PublishedAt = a.PublishedAt,
            AudioUrl = a.AudioUrl,
            Author = a.Author,
            ContentType = a.ContentType,
            IsSponsored = a.IsSponsored,
            SponsorName = a.SponsorName,
            SponsorUrl = a.SponsorUrl
        }).ToList();

        var eTag = $"\"topic_articles_{topic.Id}_{safePage}_{safePageSize}_{totalCount}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        return Ok(new PagedResponse<ArticleDto>
        {
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            Items = articleDtos
        });
    }
}
