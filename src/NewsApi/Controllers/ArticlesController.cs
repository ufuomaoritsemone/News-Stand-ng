using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services;
using NigerianNewsGrid.Client.Models;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
public class ArticlesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IRelatedContentService _relatedService;
    private readonly IArticleSearchService _searchService;
    private readonly ISlugService _slugService;
    private readonly IArticleSeoBackfillService? _seoBackfillService;
    private readonly IStoryClusteringService? _clusteringService;
    private readonly IArticleEnrichmentService? _enrichmentService;
    private readonly IBriefingService _briefingService;
    private readonly IArticleIngestionService _ingestionService;
    private readonly IConfiguration? _config;

    public ArticlesController(
        NewsDbContext db,
        IRelatedContentService relatedService,
        IArticleSearchService searchService,
        IMemoryCache cache,
        ILogger<ArticlesController> logger,
        ISlugService slugService,
        IArticleSeoBackfillService? seoBackfillService = null,
        ICategorizerTrainingService? categorizerTrainingService = null,
        IIndexNowService? indexNowService = null,
        IConfiguration? config = null,
        IStoryClusteringService? clusteringService = null,
        IArticleEnrichmentService? enrichmentService = null,
        IBriefingService? briefingService = null,
        IArticleIngestionService? ingestionService = null)
    {
        _db                 = db;
        _relatedService     = relatedService;
        _searchService      = searchService;
        _slugService        = slugService;
        _seoBackfillService = seoBackfillService;
        _clusteringService  = clusteringService;
        _enrichmentService  = enrichmentService;
        _config             = config;
        _briefingService    = briefingService ?? new BriefingService(db, cache, logger);
        _ingestionService   = ingestionService ?? new ArticleIngestionService(
            db, slugService, _briefingService, logger, categorizerTrainingService, indexNowService);
    }

    private bool IsAuthorizedInternal()
    {
        if (_config == null) return false;
        var configuredKey = _config["ApiKey"] ?? _config["ApiSecurity:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey)) return false;

        if (Request.Headers.TryGetValue("X-Api-Key", out var providedKey) &&
            !string.IsNullOrEmpty(providedKey))
        {
            var providedBytes = System.Text.Encoding.UTF8.GetBytes(providedKey.ToString());
            var configuredBytes = System.Text.Encoding.UTF8.GetBytes(configuredKey);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
        }

        return false;
    }

    [HttpGet("related")]
    public async Task<IActionResult> GetRelatedStories(
        [FromQuery] string? id       = null,
        [FromQuery] string? title    = null,
        [FromQuery] string? category = null,
        [FromQuery] int     limit    = 4,
        CancellationToken   cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 20);
        var result = await _relatedService.GetRelatedStoriesAsync(id, title, category, effectiveLimit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("briefings")]
    public async Task<IActionResult> GetBriefings(
        [FromQuery] int     topPerCategory = 5,
        [FromQuery] string? language       = null,
        CancellationToken   cancellationToken = default)
    {
        var result = await _briefingService.GetBriefingsAsync(
            topPerCategory, language, IsAuthorizedInternal(), cancellationToken);

        if (result.CacheHit)
        {
            Response.Headers.Append("X-Cache", "HIT");
            return Ok(result.Categories);
        }

        if (result.Categories.Count > 0)
        {
            Response.Headers.Append("X-Data-Age-Hours", result.HoursStale.ToString("F1"));
            Response.Headers.Append("X-Cache", "MISS");
        }

        return Ok(result.Categories);
    }

    [HttpGet("briefings/availability")]
    public async Task<IActionResult> CheckBriefingAvailability(CancellationToken cancellationToken = default)
    {
        var availability = await _briefingService.CheckAvailabilityAsync(cancellationToken);
        return Ok(new
        {
            available    = availability.Available,
            articleCount = availability.ArticleCount,
            timestampUtc = availability.TimestampUtc
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetArticles(
        [FromQuery] string? source = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? contentType = null,
        [FromQuery] bool    todayOnly  = false,
        [FromQuery] int?    days       = null,
        [FromQuery] bool?   hasAiSummary = null,
        [FromQuery] int     limit      = 200,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 50,
        CancellationToken   cancellationToken = default)
    {
        var query = _db.Articles.AsNoTracking();

        if (hasAiSummary.HasValue)
        {
            query = hasAiSummary.Value
                ? query.Where(a => _db.ArticleEnrichments.Any(e => e.ArticleId == a.Id))
                : query.Where(a => !_db.ArticleEnrichments.Any(e => e.ArticleId == a.Id));
        }

        if (todayOnly)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(a => a.PublishedAt != null && a.PublishedAt >= today);
        }
        else if (days.HasValue && days.Value > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days.Value);
            query = query.Where(a => a.PublishedAt != null && a.PublishedAt >= cutoff);
        }

        if (!string.IsNullOrWhiteSpace(source) && !source.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var s = source.Trim().ToLowerInvariant();
            query = query.Where(a => a.Source != null && a.Source.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var c = category.Trim().ToLowerInvariant();
            query = query.Where(a => a.Category != null && a.Category.ToLower() == c);
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var ct = contentType.Trim().ToLowerInvariant();
            query = query.Where(a => a.ContentType != null && a.ContentType.ToLower() == ct);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchParams = new ArticleSearchParams(
                Search: search,
                Source: source,
                Category: category,
                TodayOnly: todayOnly,
                Days: days,
                Page: page,
                PageSize: pageSize > 0 ? pageSize : limit,
                ContentType: contentType
            );

            var searchResults = await _searchService.SearchArticlesAsync(searchParams, cancellationToken);
            return Ok(searchResults);
        }

        var pageIndex = Math.Max(1, page);
        var size      = Math.Clamp(pageSize > 0 ? pageSize : limit, 1, 200);

        var rawArticles = await query
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .Skip((pageIndex - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var pageIds  = rawArticles.Select(a => a.Id).ToList();
        var audioMap = pageIds.Count > 0
            ? (await _db.AudioAssets.AsNoTracking()
                .Where(x => x.ArticleId != null && pageIds.Contains(x.ArticleId))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ArticleId!)
                .ToDictionary(g => g.Key, g => g.First().Url)
            : new Dictionary<string, string>();

        var commentCounts = pageIds.Count > 0
            ? await _db.StoryComments.AsNoTracking()
                .Where(c => pageIds.Contains(c.ArticleId) && c.Status != "Hidden")
                .GroupBy(c => c.ArticleId)
                .Select(g => new { ArticleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ArticleId, x => x.Count, cancellationToken)
            : new Dictionary<string, int>();

        var enrichmentMap = pageIds.Count > 0
            ? await _db.ArticleEnrichments.AsNoTracking()
                .Where(e => pageIds.Contains(e.ArticleId))
                .ToDictionaryAsync(e => e.ArticleId, cancellationToken)
            : new Dictionary<string, ArticleEnrichment>();

        var isInternal = IsAuthorizedInternal();
        var articleDtos = rawArticles
            .Select(a => ArticleIngestionService.MapToArticleDto(
                a,
                _slugService,
                includeContent: isInternal,
                resolvedAudioUrl: audioMap.GetValueOrDefault(a.Id),
                commentCount: commentCounts.GetValueOrDefault(a.Id, 0),
                enr: enrichmentMap.GetValueOrDefault(a.Id)))
            .ToList();

        return Ok(articleDtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetArticleById(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Article ID cannot be empty." });

        var a = await _db.Articles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (a == null)
            return NotFound(new { message = $"Article with ID '{id}' was not found." });

        var audio = await _db.AudioAssets
            .AsNoTracking()
            .Where(x => x.ArticleId == a.Id)
            .Select(x => x.Url)
            .FirstOrDefaultAsync(cancellationToken);

        var commentCount = await _db.StoryComments.AsNoTracking()
            .CountAsync(c => c.ArticleId == a.Id && c.Status != "Hidden", cancellationToken);

        var enr = await _db.ArticleEnrichments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ArticleId == a.Id, cancellationToken);

        var dto = ArticleIngestionService.MapToArticleDto(
            a, _slugService, includeContent: true, resolvedAudioUrl: audio, commentCount: commentCount, enr: enr);

        return Ok(dto);
    }

    [HttpGet("by-slug/{slug}")]
    [ProducesResponseType(typeof(ArticleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload { Code = "NOT_FOUND", Message = "Article slug is required.", TraceId = HttpContext.TraceIdentifier }
            });
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var article = await _db.Articles
            .AsNoTracking()
            .Include(a => a.ArticleTopics).ThenInclude(at => at.Topic)
            .Include(a => a.Enrichment)
            .FirstOrDefaultAsync(a => a.Slug == normalizedSlug, cancellationToken);

        string? redirectSlug = null;

        if (article == null)
        {
            var parts = normalizedSlug.Split('-');
            if (parts.Length > 1 && parts[^1].Length >= 4)
            {
                var suffix = parts[^1];
                article = await _db.Articles
                    .AsNoTracking()
                    .Include(a => a.ArticleTopics).ThenInclude(at => at.Topic)
                    .Include(a => a.Enrichment)
                    .FirstOrDefaultAsync(a => a.Slug.EndsWith(suffix) || a.Id.StartsWith(suffix), cancellationToken);

                if (article != null && !string.Equals(article.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase))
                {
                    redirectSlug = article.Slug;
                }
            }
        }

        if (article == null)
        {
            article = await _db.Articles
                .AsNoTracking()
                .Include(a => a.ArticleTopics).ThenInclude(at => at.Topic)
                .Include(a => a.Enrichment)
                .FirstOrDefaultAsync(a => a.Id == slug.Trim() || a.Id.ToLower() == normalizedSlug, cancellationToken);

            if (article != null && !string.IsNullOrWhiteSpace(article.Slug))
            {
                redirectSlug = article.Slug;
            }
        }

        if (article == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload { Code = "NOT_FOUND", Message = $"No article found matching slug '{slug}'.", TraceId = HttpContext.TraceIdentifier }
            });
        }

        var lastModified = article.UpdatedAt ?? article.PublishedAt ?? DateTime.UtcNow;
        var eTag = $"\"{article.Id}_{lastModified.Ticks}_{article.Slug}\"";

        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        List<Article> relatedEntities;
        if (_clusteringService != null)
        {
            relatedEntities = await _clusteringService.FindRelatedArticlesAsync(article.Id, 6, cancellationToken);
        }
        else
        {
            relatedEntities = await _db.Articles
                .AsNoTracking()
                .Where(a => a.Id != article.Id && a.Category == article.Category)
                .OrderByDescending(a => a.PublishedAt)
                .Take(6)
                .ToListAsync(cancellationToken);
        }

        var relatedArticles = relatedEntities.Select(a => new ArticleDto
        {
            Id             = a.Id,
            Title          = a.Title,
            Slug           = !string.IsNullOrEmpty(a.Slug) ? a.Slug : _slugService.GenerateArticleSlug(a.Title, a.Id),
            Excerpt        = a.Excerpt ?? _slugService.GenerateExcerpt(a.Content, a.Summary, 300),
            Summary        = a.Summary,
            Url            = a.Url,
            ImageUrl       = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            Source         = a.Source ?? "News Stand NG",
            Category       = a.Category ?? "General",
            PublishedAt    = a.PublishedAt,
            AudioUrl       = a.AudioUrl,
            Author         = a.Author,
            ContentType    = a.ContentType,
            IsSponsored    = a.IsSponsored,
            SponsorName    = a.SponsorName,
            SponsorUrl     = a.SponsorUrl,
            PlacementZone  = a.PlacementZone ?? "MainPage_InFeed",
            TargetPlatform = a.TargetPlatform ?? "All"
        }).ToList();

        var detailDto = new ArticleDetailDto
        {
            Id             = article.Id,
            Title          = article.Title,
            Slug           = article.Slug,
            Excerpt        = article.Excerpt ?? _slugService.GenerateExcerpt(article.Content, article.Summary, 300),
            Source         = article.Source,
            SourceUrl      = article.Url,
            ImageUrl       = CategoryImageMap.Resolve(article.ImageUrl, article.Category),
            PublishedAt    = article.PublishedAt,
            UpdatedAt      = article.UpdatedAt,
            Author         = article.Author,
            Category       = article.Category,
            ContentType    = article.ContentType,
            AudioUrl       = article.AudioUrl,
            SeoTitle       = article.SeoTitle,
            SeoDescription = article.SeoDescription,
            IsSponsored    = article.IsSponsored,
            SponsorName    = article.SponsorName,
            SponsorUrl     = article.SponsorUrl,
            PlacementZone  = article.PlacementZone ?? "MainPage_InFeed",
            TargetPlatform = article.TargetPlatform ?? "All",
            RedirectSlug   = redirectSlug,
            Topics = article.ArticleTopics
                .Where(at => at.Topic != null)
                .Select(at => new TopicDto
                {
                    Id          = at.Topic!.Id,
                    Slug        = at.Topic.Slug,
                    Name        = at.Topic.Name,
                    Type        = at.Topic.Type.ToString(),
                    Description = at.Topic.Description,
                    CreatedAt   = at.Topic.CreatedAt
                })
                .ToList(),
            RelatedArticles = relatedArticles,
            Enrichment      = ArticleIngestionService.MapToEnrichmentDto(article.Enrichment)
        };

        return Ok(detailDto);
    }

    [HttpGet("{id}/ai-summary")]
    [HttpPost("{id}/ai-summary")]
    [ProducesResponseType(typeof(FullSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrGenerateAiSummary(string id, CancellationToken cancellationToken = default)
    {
        if (_enrichmentService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponseDto
            {
                Error = new ErrorPayload { Code = "SERVICE_UNAVAILABLE", Message = "Article enrichment service is not registered.", TraceId = HttpContext.TraceIdentifier }
            });
        }

        var article = await _db.Articles
            .Include(a => a.Enrichment)
            .FirstOrDefaultAsync(a => a.Id == id || a.Slug == id, cancellationToken);

        if (article == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload { Code = "NOT_FOUND", Message = $"Article '{id}' was not found.", TraceId = HttpContext.TraceIdentifier }
            });
        }

        if (article.Enrichment?.FullSummary != null)
        {
            var engine = article.Enrichment.Engine ?? "Cached";
            Response.Headers["X-AI-Engine"] = engine;
            Response.Headers["X-AI-Gemini-Engaged"] = engine.StartsWith("Gemini", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            Response.Headers["X-AI-Circuit-Breaker"] = _enrichmentService.IsCircuitBreakerActive ? "active" : "standby";
            Response.Headers["X-AI-Duration-Ms"] = "0";

            return Ok(ArticleIngestionService.MapToFullSummaryDto(article.Enrichment.FullSummary));
        }

        var sw = Stopwatch.StartNew();
        var enriched = await _enrichmentService.EnrichArticleAsync(article.Id, cancellationToken);
        sw.Stop();

        if (enriched?.FullSummary != null)
        {
            var engine = enriched.Engine ?? "Unknown";
            Response.Headers["X-AI-Engine"] = engine;
            Response.Headers["X-AI-Gemini-Engaged"] = engine.StartsWith("Gemini", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            Response.Headers["X-AI-Circuit-Breaker"] = _enrichmentService.IsCircuitBreakerActive ? "active" : "standby";
            Response.Headers["X-AI-Duration-Ms"] = sw.ElapsedMilliseconds.ToString();

            return Ok(ArticleIngestionService.MapToFullSummaryDto(enriched.FullSummary));
        }

        return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto
        {
            Error = new ErrorPayload { Code = "GENERATION_FAILED", Message = "Failed to generate AI executive summary.", TraceId = HttpContext.TraceIdentifier }
        });
    }

    [HttpPost("{id}/enrich")]
    [ProducesResponseType(typeof(ArticleEnrichmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnrichArticle(string id, CancellationToken cancellationToken = default)
    {
        if (_enrichmentService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponseDto
            {
                Error = new ErrorPayload { Code = "SERVICE_UNAVAILABLE", Message = "Article enrichment service is not registered.", TraceId = HttpContext.TraceIdentifier }
            });
        }

        var sw = Stopwatch.StartNew();
        var result = await _enrichmentService.EnrichArticleAsync(id, cancellationToken);
        sw.Stop();

        if (result == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload { Code = "NOT_FOUND", Message = $"Article with id '{id}' was not found.", TraceId = HttpContext.TraceIdentifier }
            });
        }

        var engine = result.Engine ?? "Unknown";
        Response.Headers["X-AI-Engine"] = engine;
        Response.Headers["X-AI-Gemini-Engaged"] = engine.StartsWith("Gemini", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
        Response.Headers["X-AI-Circuit-Breaker"] = _enrichmentService.IsCircuitBreakerActive ? "active" : "standby";
        Response.Headers["X-AI-Duration-Ms"] = sw.ElapsedMilliseconds.ToString();

        return Ok(ArticleIngestionService.MapToEnrichmentDto(result));
    }

    [HttpGet("{id}/enrichment")]
    [ProducesResponseType(typeof(ArticleEnrichmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticleEnrichment(string id, CancellationToken cancellationToken = default)
    {
        var enrichment = await _db.ArticleEnrichments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ArticleId == id, cancellationToken);

        if (enrichment == null)
        {
            var art = await _db.Articles.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Slug == id || a.Id == id, cancellationToken);
            if (art != null)
            {
                enrichment = await _db.ArticleEnrichments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.ArticleId == art.Id, cancellationToken);
            }
        }

        if (enrichment == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload { Code = "NOT_FOUND", Message = $"No enrichment found for article '{id}'.", TraceId = HttpContext.TraceIdentifier }
            });
        }

        return Ok(ArticleIngestionService.MapToEnrichmentDto(enrichment));
    }

    [HttpPost("enrich-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessEnrichmentBatch([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (_enrichmentService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Enrichment service not registered.");
        }

        int processed = await _enrichmentService.ProcessUnenrichedBatchAsync(limit, cancellationToken);
        return Ok(new { Processed = processed, Timestamp = DateTime.UtcNow });
    }

    [HttpGet("share/{id}")]
    [HttpGet("/share/{id}")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetArticleShareCard(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Article ID cannot be empty." });

        var html = await _ingestionService.RenderShareCardHtmlAsync(id, cancellationToken);
        if (html == null)
            return NotFound(new { message = $"Article with ID '{id}' was not found." });

        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> IngestArticles(
        [FromBody] IngestArticlesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Articles == null || !request.Articles.Any())
            return BadRequest(new { message = "No articles provided in payload." });

        var response = await _ingestionService.IngestArticlesAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id}/category")]
    public async Task<IActionResult> UpdateCategory(
        string id,
        [FromBody] UpdateArticleCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Category))
            return BadRequest(new { message = "Category is required." });

        var article = await _ingestionService.UpdateCategoryAsync(id, request.Category, cancellationToken);
        if (article == null)
            return NotFound(new { message = $"Article with ID '{id}' was not found." });

        return Ok(new { id = article.Id, category = article.Category, message = "Category updated successfully." });
    }

    [HttpGet("sync")]
    public async Task<IActionResult> GetArticleDeltas(
        [FromQuery] DateTime? sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var deltas = await _ingestionService.GetArticleDeltasAsync(sinceUtc, cancellationToken);
        return Ok(deltas);
    }

    // ── Direct Sponsorship Management Endpoints ──────────────────────────────

    [HttpGet("sponsored")]
    public async Task<IActionResult> GetSponsoredArticles(
        [FromQuery] string? platform = null,
        [FromQuery] string? zone = null,
        CancellationToken ct = default)
    {
        var sponsored = await _ingestionService.GetSponsoredArticlesAsync(platform, zone, ct);
        return Ok(sponsored);
    }

    [HttpPost("sponsored")]
    public async Task<IActionResult> CreateSponsoredArticle(
        [FromBody] CreateSponsoredArticleRequest request,
        CancellationToken ct = default)
    {
        var (article, validationError) = await _ingestionService.CreateSponsoredArticleAsync(request, ct);
        if (validationError != null)
            return BadRequest(new { Message = validationError });

        return Created($"/api/v1/articles/{article!.Id}", article);
    }

    [HttpPut("sponsored/{id}")]
    public async Task<IActionResult> UpdateSponsoredArticle(
        string id,
        [FromBody] UpdateSponsoredArticleRequest request,
        CancellationToken ct = default)
    {
        var (article, notFound, validationError) = await _ingestionService.UpdateSponsoredArticleAsync(id, request, ct);
        if (notFound)
            return NotFound(new { Message = $"Article with ID '{id}' was not found." });
        if (validationError != null)
            return BadRequest(new { Message = validationError });

        return Ok(article);
    }

    [HttpDelete("sponsored/{id}")]
    public async Task<IActionResult> DeleteSponsoredArticle(string id, CancellationToken ct = default)
    {
        var deleted = await _ingestionService.DeleteSponsoredArticleAsync(id, ct);
        if (!deleted)
            return NotFound(new { Message = $"Article with ID '{id}' was not found." });

        return NoContent();
    }

    [HttpPost("{id}/track-impression")]
    public async Task<IActionResult> TrackImpression(string id, CancellationToken ct = default)
    {
        var updated = await _ingestionService.TrackImpressionAsync(id, ct);
        if (!updated)
            return NotFound(new { message = $"Article with ID '{id}' not found." });

        return NoContent();
    }

    [HttpPost("{id}/track-click")]
    public async Task<IActionResult> TrackClick(string id, CancellationToken ct = default)
    {
        var updated = await _ingestionService.TrackClickAsync(id, ct);
        if (!updated)
            return NotFound(new { message = $"Article with ID '{id}' not found." });

        return NoContent();
    }

    [HttpPost("seo/backfill")]
    public async Task<IActionResult> BackfillSeo(
        [FromQuery] int batchSize = 500,
        CancellationToken cancellationToken = default)
    {
        if (_seoBackfillService == null)
        {
            return StatusCode(500, new { message = "ArticleSeoBackfillService is not registered." });
        }

        var result = await _seoBackfillService.BackfillArticleSeoAsync(batchSize, cancellationToken);
        return Ok(result);
    }
}

public class UpdateArticleCategoryRequest
{
    public string Category { get; set; } = string.Empty;
}
